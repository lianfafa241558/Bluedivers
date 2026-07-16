using System;
using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{

    /// <summary>
    /// 属于外挂组件，其他人不引用
    /// </summary>
    public abstract class AIInputUnitController : AIInputBaseController
    {
        [InspectorName("炮台")]
        [SerializeField]
        protected List<Turret> turrets=new();

        protected override void Start()
        {
            base.Start();
            turrets.ForEach(item=>item.Init(transform));
        }


        void LateUpdate()
        {
            UpdateTurretAiming();
        }


        /// <summary>炮台锁头(LateUpdate)</summary>
        protected virtual void UpdateTurretAiming()
        {
            turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
        }

        protected abstract bool AimTargrt();

        /// <summary> 设置锁定目标</summary>
        protected void CalculationAimTargrt(Vector3 targetPos)
        {
            turrets.ForEach(item => item.Look(targetPos));
        }



        [System.Serializable]
        public class Turret {


            [InspectorName("底盘")]//左右Y
            public Transform chassis;

            [InspectorName("炮管(上下X)")]//上下X
            public Transform barrel;

            [InspectorName("绑定武器")]
            public WeaponEnemyController weapon;

            public Transform firePoint=> weapon.WeaponMuzzle;

            [InspectorName("瞄准旋转锐度")]
            public float aimSharpness = 5f;
            [InspectorName("侦测开火延迟")]
            public float detectionFireDelay = 1f;
            [InspectorName("瞄准时间")]
            public float aimBlendTime = 1f;
            [InspectorName("限制旋转角度")]
            [Range(0, 90)]
            /// <summary> 限制旋转角度 </summary>
            public int limitRotation;

            [SerializeField]
            [InspectorName("允许的偏差弧度")]
            [Tooltip("(弧度，转角度为*57)")]
            private float allowDeviation = 0.005f;

            /// <summary>底座目标旋转</summary>
            protected Quaternion chassisRotation { get; set; }
            /// <summary>底座和物体根的旋转偏移</summary>
            public Quaternion chassisOffset { get; set; }
            /// <summary>底座初始旋转</summary>
            protected Quaternion chassisStartRotation { get; set; }


            /// <summary>炮管手动偏移</summary>
            [SerializeField]
            [InspectorName("炮管手动偏移")]
            public Quaternion barrelSetOffset = Quaternion.identity;

            /// <summary>炮管目标旋转</summary>
            protected Quaternion barrelRotation { get; set; }

            /// <summary>炮管初始旋转</summary>
            protected Quaternion barrelStartRotation { get; set; }

            /// <summary>炮管和发射点的旋转偏移</summary>
            public Quaternion barrelOffset { get; set; }

            /// <summary>一体式炮塔(底盘炮塔相同)</summary>
            private bool integrated;
            private bool HaveBarrel => barrel;

            /// <summary>根</summary>
            private Transform root;
            public void Init(Transform root) {
                this.root = root;
                integrated = chassis == barrel;
                //记录本体旋转没用，因为中间的层级的旋转不计入
                //还是要记录一下的，不然初始不改的话旋转会乱
                chassisOffset = Quaternion.Inverse(root.rotation) * chassis.rotation;
                //记录Y轴旋转的反向处理
                //barrelOffset = (noChassis ? chassisOffset : Quaternion.identity) * Quaternion.Inverse(firePoint.rotation) * barrel.rotation;
                if(HaveBarrel) barrelOffset = Quaternion.Inverse(firePoint.rotation) * barrel.rotation;
                //Debug.Log($"[Turret Init] firePoint={(firePoint != null ? firePoint.name : "NULL")}, barrelOffset={barrelOffset.eulerAngles}");
                //将当前旋转记录
                chassisStartRotation =chassisRotation = chassis.rotation;
                if (HaveBarrel) barrelStartRotation =barrelRotation = barrel.rotation;

            }

            /// <summary>
            /// 计算抬高后的的射击目标
            /// </summary>
            private Vector3 CalculateLaunchPoint(Vector3 startPos, Vector3 targetPos, float speed, float gravity) {

                // 计算水平距离
                Vector3 horizontalVec = new Vector3(targetPos.x - startPos.x, 0, targetPos.z - startPos.z);
                float d = horizontalVec.magnitude;
                float height = 0;
                if (gravity >= speed)
                {
                    //最远可以到达的距离(45度
                    float maxx = speed * speed / gravity;
                    float scale = Mathf.Clamp01(d / maxx);
                    height = Mathf.Tan((-29.167f * (scale * scale) - 8.947f * scale + 87.25f) * Mathf.Deg2Rad) * d;
                }
                else
                {
                    var dy = targetPos.y - startPos.y;
                    var time = d / speed;
                    height = dy + 0.5f * gravity * time * time;
                }

                // 返回目标点x,z坐标加上计算高度
                return new Vector3(targetPos.x, firePoint.position.y + height, targetPos.z);
            }
            /// <summary>
            /// 瞄准就绪(弧度，转角度为*57)
            /// </summary>
            [SerializeField]
            [InspectorName("瞄准就绪度")]
            [Tooltip("(弧度，转角度为*57")]
            private float dot = 1;
            public void Look(Vector3 targetPos) {
                //修正重力因素
                if (weapon.CurrentGravity > 0) {
                    targetPos = CalculateLaunchPoint(firePoint.position, targetPos, weapon.CurrentSpeed, weapon.CurrentGravity);
                }

                //从底盘到目标的方向
                Vector3 chassisDir = Vector3.ProjectOnPlane(targetPos - chassis.position, Vector3.up).normalized;
                Vector3 barrelDir = (targetPos - (HaveBarrel?barrel:chassis).position).normalized;
                Vector3 unlimitBarrelDir = barrelDir;
                if (limitRotation > 0) {

                    // 获取当前根节点水平方向
                    Vector3 rootForwardXZ = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;

                    // 限制底盘水平旋转
                    chassisDir = GetLimitedDirection(
                        rootForwardXZ,
                        chassisDir,
                        limitRotation
                    );

                    // 保持炮管垂直旋转与底盘同轴
                    barrelDir = new Vector3(chassisDir.x, barrelDir.y, chassisDir.z).normalized;
                    //没有做垂直的限制
                }

                //看向目标Y轴加上原本偏移方向的修正
                Quaternion tarChassisRotation = Quaternion.LookRotation(chassisDir) * chassisOffset;

                //看向目标X轴加上原本偏移方向的修正
                Quaternion tarBarrelRotation = Quaternion.LookRotation(barrelDir) * barrelOffset * barrelSetOffset;

                //看向目标X轴加上原本偏移方向的修正(未受限制的
                Quaternion tarUnlimitBarrelRotation = Quaternion.LookRotation(unlimitBarrelDir) * barrelOffset * barrelSetOffset;
                //Debug.Log($"barrelSetOffset=({barrelSetOffset.x},{barrelSetOffset.y},{barrelSetOffset.z},{barrelSetOffset.w}), tarUnlimit=({tarUnlimitBarrelRotation.x:F4},{tarUnlimitBarrelRotation.y:F4},{tarUnlimitBarrelRotation.z:F4},{tarUnlimitBarrelRotation.w:F4}), dot={Mathf.Abs(Quaternion.Dot(tarUnlimitBarrelRotation, barrelRotation)):F4}");


                if (!integrated)chassisRotation = Quaternion.Slerp(chassisRotation, tarChassisRotation, aimSharpness * Time.deltaTime);

                if(HaveBarrel) barrelRotation = Quaternion.Slerp(barrelRotation, tarBarrelRotation, aimSharpness * Time.deltaTime);

                dot = Mathf.Abs(Quaternion.Dot(tarUnlimitBarrelRotation, barrelRotation));
            }

            private int rotateDir=1;

            public void Rotate(float angle)
            {
                if (integrated) return;//一体式炮塔不吃这套
                angle *= rotateDir;

                //底盘旋转后的四元数
                Quaternion chassisDir = (chassisRotation * Quaternion.AngleAxis(angle, Vector3.up));
                
                if (limitRotation > 0)
                {
                    // 获取原始根节点水平方向
                    Vector3 rootForwardXZ = chassisStartRotation * Vector3.forward;
                    // 获取当前根节点水平方向
                    Vector3 chassisForwardXZ = Vector3.ProjectOnPlane(chassisDir * chassisOffset * Vector3.forward, Vector3.up).normalized;
                    //反方向
                    if (Vector3.Angle(rootForwardXZ, chassisForwardXZ) >= limitRotation)
                    {
                        rotateDir *= -1;
                        chassisDir = (chassisRotation * Quaternion.AngleAxis(angle, Vector3.up));
                    }

                }

                // 计算绕世界Y轴旋转angle后的目标旋转
                Quaternion tarChassisRotation = chassisDir * chassisOffset;

                chassisRotation = tarChassisRotation;
                chassis.rotation = chassisRotation;
            }


            private Vector3 GetLimitedDirection(Vector3 from, Vector3 to, float maxAngle) {
                float angle = Vector3.Angle(from, to);
                if (angle <= maxAngle) return to;

                Vector3 axis = Vector3.Cross(from, to).normalized;
                return Quaternion.AngleAxis(maxAngle, axis) * from;
            }

            public void Aiming(float time) {
                float scale = time / Mathf.Max(aimBlendTime,0.1f);

                //插值系数，=0时直接是a，=1时是b
                if (scale>=1|| aimBlendTime==0) {
                    if (!integrated) chassis.rotation = chassisRotation;
                    if (HaveBarrel) barrel.rotation = barrelRotation;
                }
                else{
                    if (!integrated) chassis.rotation =  Quaternion.Slerp(chassis.rotation, chassisRotation, scale);
                    if (HaveBarrel) barrel.rotation = Quaternion.Slerp(barrel.rotation, barrelRotation, scale);
                }
            }
            /// <summary> 在Idle状态下同步 </summary>
            public void Synchro()
            {
                if (!integrated) chassisRotation = chassis.rotation;
                if (HaveBarrel) barrelRotation = barrel.rotation;
            }

            public bool IsLockTarget(Vector3 target) {
                //Vector3 chassisDir = Vector3.ProjectOnPlane(target, Vector3.up).normalized;
                //看向目标Y轴加上原本偏移方向的修正
                //Quaternion tarChassisRotation = Quaternion.LookRotation(chassisDir) * chassisOffset;
                // 计算点积绝对值
                //float dot = Mathf.Abs(Quaternion.Dot(tarChassisRotation, chassis.rotation));
                //Debug.LogError("dot"+ (1 - dot));
                if ((1 - dot) < allowDeviation) {
                    return true;
                }
                return false;
            }

            public Vector3 AimDir() {             
                return HaveBarrel ? (barrel.rotation * Quaternion.Inverse(barrelRotation)) * Vector3.forward: chassisRotation*Vector3.forward;
            }

        }
    }



}