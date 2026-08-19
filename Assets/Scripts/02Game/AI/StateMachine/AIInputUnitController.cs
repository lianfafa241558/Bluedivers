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
        protected List<Turret> turrets = new();

        protected override void Start()
        {
            base.Start();
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].Init(transform);
            }
        }


        void LateUpdate()
        {
            UpdateTurretAiming();
        }


        /// <summary>炮台锁头(LateUpdate)</summary>
        protected virtual void UpdateTurretAiming()
        {
            float blendTime = Time.time - m_TimeStartedDetection;
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].Aiming(blendTime);
            }
        }

        protected abstract bool AimTargrt();

        /// <summary> 设置锁定目标</summary>
        protected void CalculationAimTargrt(Vector3 targetPos)
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].Look(targetPos);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 绘制炮塔限制角度与朝向（仅编辑器，选中时显示）
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                Turret t = turrets[i];
                if (!t) continue;
                t.DrawGizmosSelected();
            }
        }
#endif


        [System.Serializable]
        public class Turret
        {
            public enum TurretType
            {
                [InspectorName("炮塔")]
                /// <summary>完整炮台：底盘+炮管+武器，水平/垂直双轴瞄准</summary>
                Full,
                [InspectorName("自体")]
                /// <summary>自体炮塔：无武器、无垂直旋转，仅控制底盘水平旋转</summary>
                SelfBody,
            }

            [InspectorName("炮塔类型")]
            public TurretType type;

            [InspectorName("底盘")]//左右Y
            public Transform chassis;

            [InspectorName("炮管(上下X)")]//上下X
            public Transform barrel;

            [InspectorName("绑定武器")]
            public WeaponEnemyController weapon;

            public Transform firePoint => weapon ? weapon.GetMuzzle(0) : null;

            [InspectorName("转向速度")]
            [Tooltip("匀速转向速度（度/秒）。例如 30=30°/秒，90=90°/秒。注意：若本炮塔是某父级底座(如limitFollow/spine0)的子物体，此速度应 ≥ 父级底座转向速度，否则会被父级旋转拖乱导致指向错误")]
            public float aimSharpness = 60f;
            [InspectorName("自动巡逻旋转速度(°/秒)")]
            [Tooltip(">0 时该炮塔在非攻击状态自动巡逻旋转；0=不自动旋转。用于选择性控制哪些炮塔巡逻")]
            public float autoRotateSpeed;
            [InspectorName("侦测开火延迟")]
            public float detectionFireDelay = 1f;
            [InspectorName("最小攻击距离")]
            [Tooltip("目标距离炮塔小于该值时不开火(太近打不到)。0=不限")]
            public float minAttackRange;
            [InspectorName("水平限制旋转角度")]
            [Range(0, 120)]
            /// <summary> 限制底盘水平旋转角度 </summary>
            public int limitRotation;

            [InspectorName("水平限制跟随物体")]
            [Tooltip("以该物体 forward 的水平投影作为水平限制基准。留空则跟随根物体(坦克)。用于炮塔挂在不同层级、需各自跟随不同父节点的情况。若此炮塔是该物体的子级(如主炮挂在spine0下)，则炮塔转向速度须 ≥ 该物体(父级底座)转向速度，否则会被父级旋转拖乱导致指向错误")]
            public Transform limitFollow;

            [InspectorName("垂直限制旋转角度")]
            [Range(0, 90)]
            /// <summary> 限制炮管垂直(俯仰)旋转角度 </summary>
            public int verticalLimitRotation;

            [SerializeField]
            [InspectorName("允许的偏差弧度")]
            [Tooltip("(弧度，转角度为*57)")]
            private float allowDeviation = 0.005f;

            /// <summary>底座目标旋转</summary>
            protected Quaternion chassisRotation { get; set; }
            /// <summary>底座和物体根的旋转偏移</summary>
            public Quaternion chassisOffset { get; set; }


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

            public static implicit operator bool(Turret t) => t != null;

            public void Init(Transform root)
            {
                this.root = root;
                // 缺底盘则无意义，直接跳过
                if (!chassis)
                {
                    Debug.LogError($"[Turret Init] 缺少底盘引用: {root.name}", root);
                    return;
                }
                integrated = chassis == barrel;
                //记录本体旋转没用，因为中间的层级的旋转不计入
                //还是要记录一下的，不然初始不改的话旋转会乱
                chassisOffset = Quaternion.Inverse(root.rotation) * chassis.rotation;
                //记录Y轴旋转的反向处理
                if (HaveBarrel)
                {
                    if (!firePoint)
                        Debug.LogError($"[Turret Init] 绑定武器缺少发射点位: {weapon}", weapon);
                    else
                        barrelOffset = Quaternion.Inverse(firePoint.rotation) * barrel.rotation;
                }
                //将当前旋转记录
                chassisRotation = chassis.rotation;
                if (HaveBarrel) barrelStartRotation = barrelRotation = barrel.rotation;
            }

            /// <summary>
            /// 计算抬高后的的射击目标
            /// </summary>
            private Vector3 CalculateLaunchPoint(Vector3 startPos, Vector3 targetPos, float speed, float gravity)
            {
                // 计算水平距离
                Vector3 horizontalVec = new Vector3(targetPos.x - startPos.x, 0, targetPos.z - startPos.z);
                float d = horizontalVec.magnitude;
                float height = 0;
                if (gravity >= speed/2)//高抛
                {
                    //最远可以到达的距离(45度
                    float maxx = speed * speed / gravity;
                    float scale = Mathf.Clamp01(d / maxx);
                    height = Mathf.Tan((-29.167f * (scale * scale) - 8.947f * scale + 87.25f) * Mathf.Deg2Rad) * d;
                }
                else//平射
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

            public void Look(Vector3 targetPos)
            {
                //修正重力因素（仅完整炮台且武器带重力时）
                if (weapon && weapon.CurrentGravity > 0)
                {
                    targetPos = CalculateLaunchPoint(firePoint.position, targetPos, weapon.CurrentSpeed, weapon.CurrentGravity);
                }

                //从底盘到目标的方向
                Vector3 chassisDir = Vector3.ProjectOnPlane(targetPos - chassis.position, Vector3.up).normalized;
                // 目标在底盘正上/下方时水平方向接近零向量：保持底盘当前水平朝向，
                // 避免 LookRotation 异常和水平方向乱跳（如玩家钻到坦克正下方时机枪不应水平旋转 180°）
                if (chassisDir.sqrMagnitude < 0.0001f)
                    chassisDir = Vector3.ProjectOnPlane(chassisRotation * Vector3.forward, Vector3.up).normalized;

                Vector3 barrelDir = Vector3.zero;
                if (HaveBarrel)
                {
                    barrelDir = (targetPos - barrel.position).normalized;
                }
                Vector3 unlimitBarrelDir = barrelDir;

                // 水平限制：限制底盘朝向（仅水平）
                if (limitRotation > 0)
                {
                    // 基准方向：跟随 limitFollow（缺省根物体=坦克）的 forward 水平投影，
                    // 可让不同炮塔挂在不同父层级、各自跟随不同朝向
                    Vector3 baseForwardXZ = GetLimitBaseForward();
                    // 限制底盘水平旋转
                    chassisDir = GetLimitedDirection(baseForwardXZ, chassisDir, limitRotation);
                    // 保持炮管垂直旋转与底盘同轴（同步水平转向）。
                    // 注意：不能用 new Vector3(chassisDir.x, barrelDir.y, chassisDir.z).normalized——
                    // 受限后的 chassisDir 是单位水平向量(长度1)，替换后归一化会压缩 dy、改变俯仰角，
                    // 导致炮管下压/上抬不足（水平方向被钳制得越狠，Y 轴偏差越大）。
                    // 正确做法：水平方向取受限后的 chassisDir，俯仰角保持原 barrelDir 的俯仰角，按球坐标重建。
                    if (HaveBarrel)
                    {
                        float elevation = Mathf.Asin(Mathf.Clamp(barrelDir.y, -1f, 1f));
                        Vector3 horizDir = new Vector3(chassisDir.x, 0f, chassisDir.z);
                        float horizLen = horizDir.magnitude;
                        if (horizLen < 0.0001f)
                        {
                            // 目标在底盘正上/正下方：无水平分量，用当前朝向兜底
                            horizDir = Vector3.ProjectOnPlane(barrelRotation * Vector3.forward, Vector3.up).normalized;
                            if (horizDir.sqrMagnitude < 0.0001f) horizDir = Vector3.forward;
                        }
                        else
                        {
                            horizDir /= horizLen;
                        }
                        barrelDir = (horizDir * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation)).normalized;
                    }
                }

                // 垂直限制：限制炮管俯仰角（相对水平面）
                if (verticalLimitRotation > 0 && HaveBarrel)
                {
                    barrelDir = GetLimitedVerticalDirection(barrelDir, verticalLimitRotation);
                }

                //看向目标Y轴加上原本偏移方向的修正
                Quaternion tarChassisRotation = Quaternion.LookRotation(chassisDir) * chassisOffset;

                //匀速转向：aimSharpness 直接表示转向速度（度/秒）。
                // 注意：本炮塔若为某父级底座(limitFollow/spine0)的子物体，此速度必须 ≥ 父级底座转向速度，
                // 否则父级旋转会拖乱子炮塔的绝对瞄准，导致指向错误（如主炮指向 back）。
                float maxDegreesDelta = aimSharpness * Time.deltaTime;

                if (!integrated)
                    chassisRotation = Quaternion.RotateTowards(chassisRotation, tarChassisRotation, maxDegreesDelta);

                if (HaveBarrel)
                {
                    //看向目标X轴加上原本偏移方向的修正
                    Quaternion tarBarrelRotation = Quaternion.LookRotation(barrelDir) * barrelOffset * barrelSetOffset;
                    barrelRotation = Quaternion.RotateTowards(barrelRotation, tarBarrelRotation, maxDegreesDelta);

                    //看向目标X轴加上原本偏移方向的修正(未受限制的)
                    Quaternion tarUnlimitBarrelRotation = Quaternion.LookRotation(unlimitBarrelDir) * barrelOffset * barrelSetOffset;
                    dot = Mathf.Abs(Quaternion.Dot(tarUnlimitBarrelRotation, barrelRotation));
                }
            }

            private int rotateDir = 1;

            public void Rotate(float angle)
            {
                if (integrated) return;//一体式炮塔不吃这套
                if (!chassis) return;
                angle *= rotateDir;

                // 绕世界 Y 轴旋转（左乘 = 世界空间旋转；右乘 = 局部空间旋转）
                Quaternion nextRotation = Quaternion.AngleAxis(angle, Vector3.up) * chassisRotation;

                if (limitRotation > 0)
                {
                    // 跟随物体的水平前向（限制基准）
                    Vector3 rootForwardXZ = GetLimitBaseForward();
                    // 旋转后底盘的水平方向
                    Vector3 chassisForwardXZ = Vector3.ProjectOnPlane(nextRotation * Vector3.forward, Vector3.up).normalized;
                    if (chassisForwardXZ.sqrMagnitude < 0.0001f) chassisForwardXZ = rootForwardXZ;

                    // 当前相对基准朝向的带符号偏角（顺时针为正）
                    float curAngle = Vector3.SignedAngle(rootForwardXZ, chassisForwardXZ, Vector3.up);

                    // 达到边界：反向，并把超出的部分增量回退贴边（不重建旋转，避免跳变回正）
                    if (Mathf.Abs(curAngle) >= limitRotation)
                    {
                        rotateDir = -rotateDir;
                        float clamped = Mathf.Clamp(curAngle, -limitRotation, limitRotation);
                        nextRotation = Quaternion.AngleAxis(clamped - curAngle, Vector3.up) * nextRotation;
                    }
                }

                chassisRotation = nextRotation;
                chassis.rotation = chassisRotation;
            }

            /// <summary>是否启用自动巡逻旋转</summary>
            public bool HasAutoRotate => autoRotateSpeed > 0;

            /// <summary>
            /// 自动巡逻旋转（应只在非瞄准状态调用）。
            /// 内部按 autoRotateSpeed 匀速旋转并同步，速度<=0 时自动跳过。
            /// </summary>
            public void AutoRotate(float deltaTime)
            {
                if (autoRotateSpeed <= 0) return;
                Rotate(autoRotateSpeed * deltaTime);
                Synchro();
            }

            /// <summary>
            /// 水平限制基准方向：跟随 limitFollow（缺省根物体=坦克）的 forward 水平投影。
            /// 用于不同炮塔挂在不同父层级、各自跟随不同朝向。
            /// </summary>
            private Vector3 GetLimitBaseForward()
            {
                Transform follow = limitFollow ? limitFollow : root;
                // 无跟随物体时兜底为正前方，避免 NRE
                if (!follow) return Vector3.forward;
                Vector3 fwd = Vector3.ProjectOnPlane(follow.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                return fwd;
            }

            private Vector3 GetLimitedDirection(Vector3 from, Vector3 to, float maxAngle)
            {
                // 带符号偏角（绕 up）：对反向/平行方向也能稳定给出 ±180°，
                // 避免原实现用 Cross(from,to) 在反向时退化为零轴导致 NaN 乱跳
                float angle = Vector3.SignedAngle(from, to, Vector3.up);
                float clamped = Mathf.Clamp(angle, -maxAngle, maxAngle);
                return Quaternion.AngleAxis(clamped, Vector3.up) * from;
            }

            /// <summary>
            /// 限制炮管俯仰角（相对水平面，范围 [-maxAngle, +maxAngle]）
            /// </summary>
            private Vector3 GetLimitedVerticalDirection(Vector3 barrelDir, float maxAngle)
            {
                if (maxAngle <= 0) return barrelDir;

                Vector3 horizontal = Vector3.ProjectOnPlane(barrelDir, Vector3.up);
                float horizLen = horizontal.magnitude;
                float elevation = Mathf.Asin(Mathf.Clamp(barrelDir.y, -1f, 1f));
                float maxElev = maxAngle * Mathf.Deg2Rad;

                if (Mathf.Abs(elevation) <= maxElev) return barrelDir;

                // 超限：把俯仰角钳制到上限，水平朝向保持不变
                float limitedElev = Mathf.Sign(elevation) * maxElev;
                if (horizLen < 0.0001f)
                {
                    // 几乎垂直向上/下，水平方向丢失，退回初始水平前向
                    Vector3 startFwd = Vector3.ProjectOnPlane(barrelStartRotation * Vector3.forward, Vector3.up).normalized;
                    if (startFwd.sqrMagnitude < 0.0001f) startFwd = Vector3.forward;
                    return (startFwd * Mathf.Cos(limitedElev) + Vector3.up * Mathf.Sin(limitedElev)).normalized;
                }

                Vector3 horizDir = horizontal / horizLen;
                return (horizDir * Mathf.Cos(limitedElev) + Vector3.up * Mathf.Sin(limitedElev)).normalized;
            }

            public void Aiming(float time)
            {
                // 直接应用 Look() 中用 aimSharpness 渐进算出的目标旋转，转向速度完全由 aimSharpness 决定
                if (!integrated) chassis.rotation = chassisRotation;
                if (HaveBarrel) barrel.rotation = barrelRotation;
            }
            /// <summary> 在Idle状态下同步 </summary>
            public void Synchro()
            {
                if (!integrated) chassisRotation = chassis.rotation;
                if (HaveBarrel) barrelRotation = barrel.rotation;
            }

            public bool IsLockTarget(Vector3 target)
            {
                if ((1 - dot) < allowDeviation)
                {
                    return true;
                }
                return false;
            }

            [InspectorName("近战射程阈值")]
            [Tooltip("武器射程(CurrentWeaponExtremeRange)不超过该值视为近战，近战才会检查垂直高度差(目标过高/过低够不着就不开火)；超过则视为远程，忽略该规则")]
            public float meleeRangeThreshold = 5f;

            /// <summary>
            /// 该炮塔当前是否允许对 targetPos 开火（目标距离不小于最小攻击距离）。
            /// 基于底盘位置判定；minAttackRange<=0 时不限。
            /// 近战武器额外检查垂直高度差：目标与发射点的垂直高度差超过武器射程则视为够不着，不允许开火。
            /// </summary>
            public bool CanFireAt(Vector3 targetPos)
            {
                // 最小攻击距离：目标过近不能开火（minAttackRange<=0 时不限制）
                if (minAttackRange > 0 && Vector3.Distance(chassis.position, targetPos) < minAttackRange)
                {
                    return false;
                }

                // 近战武器(射程<=阈值)：目标垂直高度差超过射程则够不着，不攻击。
                // 远程武器(射程>阈值)可抛射/直线命中高处，跳过该规则。
                if (weapon && weapon.CurrentWeaponExtremeRange <= meleeRangeThreshold)
                {
                    Vector3 firePos = firePoint ? firePoint.position : chassis.position;
                    float heightDelta = Mathf.Abs(targetPos.y - firePos.y);
                    if (heightDelta > weapon.CurrentWeaponExtremeRange)
                    {
                        return false;
                    }
                }

                return true;
            }

            public Vector3 AimDir()
            {
                return HaveBarrel ? (barrel.rotation * Quaternion.Inverse(barrelRotation)) * Vector3.forward : chassisRotation * Vector3.forward;
            }

#if UNITY_EDITOR
            /// <summary>
            /// 编辑器 Gizmos：绘制底盘/炮管朝向、水平限制扇形与垂直限制范围
            /// </summary>
            public void DrawGizmosSelected()
            {
                if (!chassis) return;

                Transform pivot = HaveBarrel ? barrel : chassis;
                Vector3 pivotPos = pivot.position;

                // 水平限制基准：跟随 limitFollow（缺省根物体）的 forward 水平投影，与 Look()/Rotate() 一致
                Vector3 baseForwardXZ = GetLimitBaseForward();

                // ---- 水平限制扇形 ----
                Gizmos.color = Color.cyan;
                if (limitRotation > 0)
                {
                    DrawArcSector(pivotPos, baseForwardXZ, limitRotation, 4f);
                }

                // 底盘当前朝向（黄色射线）
                Vector3 chassisForward = baseForwardXZ;
                // 底盘当前朝向射线
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pivotPos, chassisForward * 4f);

                // ---- 炮管当前朝向 ----
                if (HaveBarrel)
                {
                    Vector3 barrelDir = barrel.rotation * Vector3.forward;
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(pivotPos, barrelDir * 4f);

                    // ---- 垂直限制范围（相对水平面） ----
                    if (verticalLimitRotation > 0)
                    {
                        Gizmos.color = new Color(1f, 0.5f, 0f, 1f); // 橙
                        Vector3 horiz = Vector3.ProjectOnPlane(barrelDir, Vector3.up).normalized;
                        if (horiz.sqrMagnitude < 0.0001f) horiz = baseForwardXZ;
                        float maxElev = verticalLimitRotation * Mathf.Deg2Rad;
                        Vector3 upDir = (horiz * Mathf.Cos(maxElev) + Vector3.up * Mathf.Sin(maxElev)).normalized;
                        Vector3 downDir = (horiz * Mathf.Cos(maxElev) - Vector3.up * Mathf.Sin(maxElev)).normalized;
                        Gizmos.DrawRay(pivotPos, upDir * 3.5f);
                        Gizmos.DrawRay(pivotPos, downDir * 3.5f);
                        Gizmos.DrawWireSphere(pivotPos + upDir * 3.5f, 0.15f);
                        Gizmos.DrawWireSphere(pivotPos + downDir * 3.5f, 0.15f);
                    }
                }

                // 武器发射点朝向
                if (firePoint)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawRay(firePoint.position, firePoint.forward * 2.5f);
                }
            }

            /// <summary>
            /// 在水平面绘制以 baseDir 为中心、halfAngle 为半角的扇形弧线
            /// </summary>
            private static void DrawArcSector(Vector3 origin, Vector3 baseDir, float halfAngle, float radius)
            {
                baseDir = Vector3.ProjectOnPlane(baseDir, Vector3.up).normalized;
                int segments = 20;
                Vector3 prev = origin + Quaternion.AngleAxis(-halfAngle, Vector3.up) * baseDir * radius;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float ang = Mathf.Lerp(-halfAngle, halfAngle, t);
                    Vector3 cur = origin + Quaternion.AngleAxis(ang, Vector3.up) * baseDir * radius;
                    Gizmos.DrawLine(prev, cur);
                    prev = cur;
                }
            }
#endif

        }
    }

}
