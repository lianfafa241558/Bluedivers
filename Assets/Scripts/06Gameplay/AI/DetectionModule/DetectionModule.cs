using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;
using FPSGame.Utils;
using FPSGame.Gameplay;
using FPSGame.Weapon;

namespace FPSGame.AI
{
    public class DetectionModule : TickBehaviour
    {

        [SerializeField]
        [InspectorName("单位眼睛的点")]
        protected Transform EyePoint;

        [InspectorName("视线距离")]
        public float DetectionRange = 20f;

        [InspectorName("听力距离")]
        public float HearingRange = 10f;

        [InspectorName("攻击距离")]
        public float AttackRange = 10f;

        [InspectorName("警告距离")]
        public float AlertRange = 0;

        [InspectorName("受击不还击")]
        public bool Lazy;

        /// <summary>丢失目标时间</summary>
        [InspectorName("丢失目标时间")]
        public float KnownTargetTimeout = 4f;

        /// <summary>最短转火时间（秒）：锁定目标后冷却期内不因受击/视野转火，避免被围殴时反复切换目标导致无法开火</summary>
        [InspectorName("最短转火时间(秒)")]
        public float MinSwitchTargetTime = 4f;

        /// <summary>锁定当前目标的时间（用于最短转火冷却）</summary>
        [SerializeField]
        [DisplayField]
        [InspectorName("锁定当前目标的时间（用于最短转火冷却）")]
        private float TimeLastTargetSet = Mathf.NegativeInfinity;




        /// <summary>是否处于转火冷却期（有目标且锁定时间不足最短转火时间）</summary>
        protected bool IsSwitchingCooldown => Target.Actor.IsValidMono() && Time.time - TimeLastTargetSet < MinSwitchTargetTime;

        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;


        [SerializeField]
        private bool showGizmos;

        //[DisplayField]
        public TargetData Target = new();// { get; protected set; } = new();


        Collider[] targetColliders;

        /// <summary>
        /// 目标在攻击范围内(肯定的吧，有目标包在范围内吧")
        /// </summary>
        public bool IsTargetInAttackRange { get; private set; }

        [DisplayField]
        /// <summary>目标是否可见</summary>
        public bool IsSeeingTarget;// { get; private set; }

        [DisplayField]
        [SerializeField]
        private GameObject obstacle;

        /// <summary>上次丢失目标的时间</summary>
        [SerializeField]
        [DisplayField]
        protected float TimeLastSeenTarget = Mathf.NegativeInfinity;

        /// <summary>目标最后已知位置（目标可见时持续更新，丢失后供 AI 前往搜索）</summary>
        public PEVector3? LastKnownTargetPos;

        /// <summary>上一帧是否看见目标（用于检测重新发现）</summary>
        private bool m_WasSeeingTargetLastFrame;

        protected Actor m_Actor;


        private Transform CorePoint;

        public Transform GetCorePoint()=> EyePoint == null ? transform : EyePoint;


        protected override void Start()
        {
            /*
            if (!BattleManager.Instance)
            {
                enabled = false;
                return;
            }*/
            base.Start();
            CorePoint = GetCorePoint();
            //不知道为什么=无效，只能老老实实if
            //if (!SearchPoint) SearchPoint = transform;
            //逻辑层噪声统一入口(开枪/命中/爆炸都发这里，不再直接听"命中点"事件)
            UnitEventSub.OnNoise += OnNoise;

        }
        private void OnDestroy()
        {
            UnitEventSub.OnNoise -= OnNoise;
        }

        [DisplayField]
        public float show;

        public void SetActor(Actor actor)
        {
            m_Actor = actor;
        }


        public override bool Tick()
        {
            if(!FPSGame.GameContract.BattleHub.Current.IsPresent) return true;
            HandleTargetDetection();

            return true;
        }

        public void SetTargetActor(IActor actor)
        {
            if (Target.Actor != actor)
            {
                //Debug.LogError(transform.parent + "设置目标" + Target.Actor + "变为" + actor, transform);
                Target.Set(actor);

                if (actor != null)
                {

                    TimeLastTargetSet = Time.time;
                    targetColliders = actor.transform.GetComponentsInChildren<Collider>();
                    TimeLastSeenTarget = Time.time;

                }
                else
                {
                    targetColliders = null;
                }
                
            }
            
        }


        public virtual void HandleTargetDetection()
        {
            bool haveOldTarget = Target.Actor.IsValidMono();


            show = (Time.time - TimeLastSeenTarget);
            IsSeeingTarget = false;

            if (!FpsHelper.VaildTarget(Target.Actor) || !CheckOldTarget())
            {
                CheckNewTarget();
            }
            //处理已知的目标检测超时
            if (Target.Actor != null && !IsSeeingTarget && Time.time > TimeLastSeenTarget + KnownTargetTimeout)
            {
                SetTargetActor(null);
            }

            var haveNewTarget = FpsHelper.VaildTarget(Target.Actor);

            //目标是否在范围内
            IsTargetInAttackRange = haveNewTarget && Vector3.Distance(CorePoint.position, Target.Pos) <= AttackRange + Target.Actor.HalfRange;

            // 目标可见时持续记录最后已知位置，供丢失后搜索
            if (haveNewTarget && IsSeeingTarget)
            {
                LastKnownTargetPos = (PEVector3?)Target.Pos;
            }
   

            //发现目标（从无到有，或重新看见旧目标）
            if ((!haveOldTarget && haveNewTarget) || (haveNewTarget && !m_WasSeeingTargetLastFrame && IsSeeingTarget))
            {
                OnDetect();
            }
            //丢失目标
            if (haveOldTarget && !haveNewTarget)
            {
                OnLostTarget();
            }

            m_WasSeeingTargetLastFrame = IsSeeingTarget;

        }

        protected bool CanLook(Vector3 targetPos, bool ignoreAngle, out RaycastHit hit)
        {
            Vector3 startPoint = CorePoint.position;
            float distance = (targetPos - startPoint).magnitude;
            bool allowRay = ignoreAngle;
            if (!ignoreAngle)
            {   //将前方向绕y轴进行eulerAngles.y度的旋转
                var fow = Quaternion.Euler(0, CorePoint.eulerAngles.y, 0) * Vector3.forward;
                float angle = Vector3.Angle(targetPos - startPoint, fow);
                float scale = FastAngleScale(angle);

                //Debug.LogError(transform.parent+"到目标点" + targetPos + "的距离为" + distance+"角度"+ angle+"角度允许的距离为"+ DetectionRange * scale,gameObject);
                //如果到目标的距离小于视野范围*角度系数
                allowRay = distance < DetectionRange * scale;
            }

            if (allowRay)
            {
                //检查是否有地形/雾障碍物
                if (!Physics.Raycast(startPoint, (targetPos - startPoint).normalized, out hit, distance, LayerDefinition.UnitSeeLayers))
                { 
                    obstacle = null;
                    return true;
                }
                else if (Target.Actor != null && hit.collider.GetComponentInParent<IActor>() == Target.Actor)
                {
                    obstacle = hit.collider.gameObject;
                    return true;

                }else if (LayerDefinition.SmokeLayers.Contains(hit.collider.gameObject.layer))
                {
                    //立即丢失目标
                    obstacle = hit.collider.gameObject;
                    TimeLastSeenTarget = Mathf.NegativeInfinity;

                }
                else
                {
                    obstacle = hit.collider.gameObject;
                }
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// 到某点的视线是否无遮挡（用于"能不能看清某个位置"这类地形/位置查询，**不会**影响目标锁定状态）。
        /// 与 <see cref="CanLook"/> 的区别：不做视野角度判定，命中烟幕时也不会改写 TimeLastSeenTarget。
        /// </summary>
        /// <param name="pos">要观察的世界坐标（建议用该位置上方的观察高度，避免被地面自身挡住）</param>
        public bool HasLineOfSight(Vector3 pos)
        {
            Vector3 startPoint = CorePoint != null ? CorePoint.position : transform.position;
            Vector3 delta = pos - startPoint;
            float distance = delta.magnitude;
            if (distance <= 0.01f) return true;
            return !Physics.Raycast(startPoint, delta / distance, distance, LayerDefinition.UnitSeeLayers);
        }

        /// <summary>检查目标是否被友方单位遮挡</summary>
        protected bool IsBlockedByFriendly(Vector3 targetPos)
        {
            if (m_Actor == null) return false;

            Vector3 startPoint = CorePoint.position;
            Vector3 dir = (targetPos - startPoint).normalized;
            float distance = (targetPos - startPoint).magnitude;

            // 用单位层做射线检测，看中间是否有同队单位
            var hits = Physics.RaycastAll(startPoint, dir, distance, LayerDefinition.UnitLayers);
            foreach (var h in hits)
            {
                var actor = h.collider.GetComponentInParent<IActor>();
                if (actor != null && actor.IsValidMono() && actor != Target.Actor && actor.Team == m_Actor.Team)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查老对象是否还有效
        /// </summary>
        protected bool CheckOldTarget()
        {
            //旧目标锁上了无视角度限制
            if (CanLook(Target.Pos, true, out var hit))//可见反而是没有hit，因为排除的单位层)
                //&& targetColliders.Contains(hit.collider))
            {
                // 如果有友方单位遮挡，视为不可见
                if (IsBlockedByFriendly(Target.Pos))
                {
                    return false;
                }

                TimeLastSeenTarget = Time.time;
                IsSeeingTarget = true;
                return true;
            }
            //Debug.LogError(transform.parent+"没有找到旧目标+ TargetActor,transform);
            return false;
        }

        /// <summary>
        /// 寻找新对象
        /// </summary>
        protected bool CheckNewTarget()
        {

            PEInt closestThreat = 0;
            //var fow = Quaternion.Euler(0, EyePoint.eulerAngles.y, 0) * Vector3.forward;
            var pos = m_Actor != null ? m_Actor.LogicPos : new(EyePoint.position);
            //没有无敌
            var list = FPSGame.Game.UnitQuery.FindUnits(new PECircle(pos, (PEInt)DetectionRange), TargetCfg.EnemyAI
                ,item=> item.Team != m_Actor.Team && FpsHelper.VaildTarget(item));

            IActor newTarget=null;
            PEInt newThreat = 0;
            foreach (IActor item in list)
            {
                if (CanLook(item.CenterPos,false, out var hit)//可见反而是没有hit
                    && (newThreat=FpsHelper.ThreatValue(CorePoint.position, item))>closestThreat)
                {
                    //Debug.LogError(transform.parent + "可以看见" + item+"碰撞"+hit.collider, transform);
                    newTarget = item;
                    closestThreat = newThreat;
                }

            }
            if (newTarget != null)
            {
                IsSeeingTarget = true;
                // 转火冷却期内保持当前目标，避免被围殴/目标被遮挡时反复切换
                if (!IsSwitchingCooldown)
                {
                    //Debug.LogError(transform.parent + "设置新目标从" + Target.Actor + "变为" + newTarget, transform);
                    SetTargetActor(newTarget);
                }
            }

            return false;
        }



        /// <summary>丢失目标</summary>
        protected virtual void OnLostTarget()
        {
            //Debug.LogWarning(gameObject + "丢失目标", gameObject);
            onLostTarget?.Invoke();
        }
        /// <summary>发现目标</summary>
        protected virtual void OnDetect()
        {
            //Debug.LogWarning(gameObject+"发现目标"+Target.Actor,gameObject);
            onDetectedTarget?.Invoke();
        }

        /// <summary>被击中</summary>
        public virtual void OnDamaged(GameObject damageSource,bool noSource)
        {
            //Debug.LogError("" + damageSource + "击中，无源伤害" + noSource);
            if (damageSource != null) {
                var actor = damageSource.GetComponent<Actor>();
                if (actor.Team == m_Actor.Team) return;
                //damageSource = actor.AimPoint.gameObject;
            }
            //无源伤害
            if (noSource)
            {
                return;
            }
            //警告
            if (AlertRange > 0)
            {
                var list = FPSGame.Game.UnitQuery.FindUnits(new PECircle(m_Actor.LogicPos, (PEInt)AlertRange), new(){},(unit)=>unit.Team == m_Actor.Team)
                    .Select(item => item.transform.GetComponentInChildren<DetectionModule>())
                    .ToList().ToVaild();
                foreach (var target in list)
                {
                    target.OnDamagedDiffuse(damageSource);
                }
            }
            else{
                OnDamagedDiffuse(damageSource);
            }

        }

        /// <summary>当前警惕点(最近听到的噪声点)（没有目标则为null</summary>
        public PEVector3? BewarePoint { get; protected set; }

        /// <summary>当前警惕点的响度(噪声半径/米)：只有更响的噪声才能覆盖它</summary>
        private PEInt m_BewareNoise;

        /// <summary>搜索指令点：丢失目标后要去查看的目标最后已知位置(优先于听觉噪声点，由 AI 消费后清空)</summary>
        public PEVector3? SearchPoint { get; set; }

        /// <summary>
        /// 警惕：记录"要去查看的噪声点"及其响度。
        /// 响度比当前警惕点更轻时直接忽略(不覆盖)——免得弹着点的小动静把枪声点顶掉。
        /// </summary>
        /// <param name="point">噪声点</param>
        /// <param name="noise">该噪声的响度(半径/米)</param>
        /// <param name="spread">是否把同一噪声扩散给附近队友</param>
        public virtual void Beware(PEVector3 point, PEInt noise, bool spread)
        {
            if (BewarePoint.HasValue && noise < m_BewareNoise) return;

            BewarePoint = point;
            m_BewareNoise = noise;

            if (spread && AlertRange > 0)
            {
                var list = FPSGame.Game.UnitQuery.FindUnits(new PECircle(m_Actor.LogicPos, (PEInt)AlertRange), new() { }, (unit) => unit.Team == m_Actor.Team)
                    .Select(item => item.transform.GetComponent<I_AIController>())
                    .ToList().ToVaild();
                foreach (var item in list)
                {
                    item.Beware(point, noise, false);//不反复扩散
                }
            }

        }

        /// <summary>清除警惕</summary>
        public void ClearBeware()
        {
            BewarePoint = null;
            m_BewareNoise = 0;
        }




        /// <summary>
        /// 受击修改目标
        /// </summary>
        /// <param name="damageSource"></param>
        protected virtual void OnDamagedDiffuse(GameObject damageSource)
        {
            if (this == null|| Lazy || !damageSource || !damageSource.GetComponent<IActor>().IsValidMono())
            {
                return;
            }
            var newActor = damageSource.GetComponent<IActor>();
            //其他组件还要用OnDetect
            /*
            if (newActor.Equals(Target.Actor))
            {
                TimeLastSeenTarget = Time.time;
                return;
            }*/
            //Debug.LogError("受击设置锁定目标" + damageSource + "老目标存在" + Target.Actor.IsValid());

            //if (Target.Actor.IsValid())
            //{
            //Debug.LogError("老目标" + Target.Actor + "  老目标仇恨" + FpsHelper.ThreatValue(CorePoint.position, Target.Actor) +
            //    "新目标仇恨" + FpsHelper.ThreatValue(CorePoint.position, newActor));

            //}
            if (!newActor.IsValidMono()|| CorePoint == null) return;

            var oldThreat = Target!=null? FpsHelper.ThreatValue(CorePoint.position, Target.Actor):0;
            var newThreat = FpsHelper.ThreatValue(CorePoint.position, newActor);
            if (!Target.Actor.IsValidMono()|| oldThreat > newThreat)
            {
                // 有当前目标且处于转火冷却期时，不因受击切换目标，避免被围殴时反复转火无法开火
                if (!Target.Actor.IsValidMono() || !IsSwitchingCooldown)
                {
                    //Debug.LogWarning(transform.parent + "受击设置新目标" + Target.Actor + "变为" + newActor, transform);
                    SetTargetActor(newActor);
                    OnDetect();
                }
            }
            
            
        }

        public virtual void OnAttack(WeaponBaseController weapon)
        {

        }

        /// <summary>
        /// 逻辑层噪声入口(订阅 UnitEventSub.OnNoise)：开枪(枪口)与命中(弹着点/爆心)都走这里。
        /// 判定：无源噪声忽略；同队噪声忽略；噪声球(半径=响度)罩到本单位听力范围内就算听见。
        /// </summary>
        void OnNoise(NoiseData noise)
        {
            if (!m_Actor.IsValidMono()
                || noise.source == null
                || noise.radius <= 0)
            {
                return;
            }
            if (!noise.source.TryGetComponent(out Actor actor) || actor.Team == m_Actor.Team) return;

            if (PEVector3.Distance(noise.pos, m_Actor.Logic3Pos) - noise.radius < (PEInt)HearingRange)
            {
                Beware(noise.pos, noise.radius, false);
            }
        }


 
        //直接算好，查表
        //15度一表
        public static float[] angleScale = new float[] {
            1,1,1,1,0.7f,0.57f,0.51f,0.49f,0.5f,0.5f,0.5f,0.5f,0.5f,
        };
         
        public static float FastAngleScale(float angle)
        {
            if (angle > 180) angle = -angle + 360;
            else if (angle < -180) angle += 360;
            else if (angle < 0) angle *= -1;
            return angleScale[Mathf.FloorToInt(angle / 15)];
        }
        /*
        [ContextMenu("计算偏移")]
        private void Test()
        {
            for (int i = 0; i <= 180; i+=15)
            {
                Debug.Log(i+"度"+DetectionAngleScale(i));
            }
        }*/


        /// <summary>
        /// 将夹角转为距离系数
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        private static float DetectionAngleScale(float angle)
        {
            if (angle > 180) angle = -angle+360;
            else if (angle < -180) angle+= 360;
            else if (angle < 0) angle *= -1;
            if (angle <= 45f)
            {
                return 1f;
            }
            else if (angle <= 120f)
            {
            // 45-90度 计算视觉上的直线连接
            // 我们需要找到一条直线连接(45°,1)和(90°,0.2)

                // 将角度转换为弧度
                float angleRad = angle * Mathf.Deg2Rad;

                // 两个端点的笛卡尔坐标
                Vector2 p1 = new Vector2(0.71f,0.71f);
                Vector2 p2 = new Vector2(-0.25f,0.435f);

                // 直线方程参数
                float A = p2.y - p1.y;//y差值
                float B = p1.x - p2.x;//x差值
                float C = p2.x * p1.y - p1.x * p2.y;//返回

                // 计算当前角度方向与直线的交点
                // 直线方程: A*x + B*y + C = 0
                // 射线方程: x = t*cosθ, y = t*sinθ

                // 解方程求t(即线段长度
                float t = -C / (A * Mathf.Cos(angleRad) + B * Mathf.Sin(angleRad));
                return t;
            }
            else
            {
                return 0.5f;
            }
        }

        /// <summary>
        /// 返回一个vector在y，vector面上的法向量
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        //因为c 在平面b 上，所以c 可以表示为a 和vector3.up 的线性组合：
        //c=X*a+Y*up;
        //并且c垂直a，所以Vector3.Dot(c, a)=0

        private Vector3 NormalY(Vector3 vector)
        {
            return ((-Vector3.Dot(Vector3.up, vector) / Vector3.Dot(vector, vector)) * vector + Vector3.up).normalized;
        }
        #region 调试

        private const int dx = 15;

        void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;
            var eyeRange = DetectionRange;
            var earRange = HearingRange;
            var pos = (EyePoint == null ? transform : EyePoint).position;
            var fow = Quaternion.Euler(0, (EyePoint == null ? transform : EyePoint).eulerAngles.y, 0) * Vector3.forward;
            if (EyePoint == null)
            {
                //(没有眼睛)侦测范围
                for (int i = 0; i < 360; i += dx)
                {
                    var dirA = Quaternion.AngleAxis(i, NormalY(fow)) * fow;
                    var dirB = Quaternion.AngleAxis(i + dx, NormalY(fow)) * fow;
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(pos + dirA * eyeRange, pos + dirB * eyeRange);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(pos + dirA * earRange, pos + dirB * earRange);
                }

            }
            else
            {
                //侦测范围


                for (int i = 0; i < 360; i += dx)
                {
                    var scaleA = FastAngleScale(i);
                    var scaleB = FastAngleScale(i + dx);
                    var dirA = Quaternion.AngleAxis(i, NormalY(fow)) * fow;
                    var dirB = Quaternion.AngleAxis(i + dx, NormalY(fow)) * fow;
                    //从旋转x度到旋转x+dx度的连线
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(pos + dirA * eyeRange * scaleA, pos + dirB * eyeRange * scaleB);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(pos + dirA * earRange, pos + dirB * earRange);
                    //UnityEditor.Handles.Label(SearchPoint.position + Quaternion.Euler(0, i, 0) * fow * DetectionRange* scaleA + new Vector3(0, 0, 1), "夹角:" + angle+"距离A"+scaleA);
                }

                //攻击范围
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(pos, 0.1f);

                //法线方向(做由方向向量和垂直向上的向量组成的平面的法线)
                Vector3 normalForward = Vector3.Cross(fow, Vector3.up).normalized;
                Vector3 endPos = pos + fow * AttackRange;
                Gizmos.DrawLine(pos + normalForward, endPos + normalForward);
                Gizmos.DrawLine(pos - normalForward, endPos - normalForward);
                Gizmos.DrawLine(endPos + normalForward, endPos - normalForward);
            }


            if (m_Actor)
            {
                //碰撞范围
                Gizmos.color = Color.yellow;
                switch (m_Actor.shape)
                {
                    case ShapeType.Circle:
                        Gizmos.DrawWireSphere(pos, m_Actor.HalfRange);
                        break;
                    case ShapeType.Rectangle:

                        Gizmos.DrawWireCube(pos, Vector3.one * m_Actor.HalfRange * 2);
                        break;
                }
            }
            //警告范围
            if (AlertRange > 0)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, AlertRange);
            }

        }

        #endregion
    }
}