using System.Linq;
using Core;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.AI
{
    public class DetectionModule : TickBehaviour
    {

        [SerializeField]
        [CustomLabel("单位眼睛的点")]
        protected Transform EyePoint;

        [CustomLabel("视线距离")]
        public float DetectionRange = 20f;

        [CustomLabel("听力距离")]
        public float HearingRange = 10f;

        [CustomLabel("攻击距离")]
        public float AttackRange = 10f;

        [CustomLabel("警告距离")]
        public float AlertRange = 0;

        /// <summary>丢失目标时间</summary>
        [CustomLabel("丢失目标时间")]
        public float KnownTargetTimeout = 4f;

        public UnityAction onDetectedTarget;
        public UnityAction onLostTarget;

        /// <summary>当前锁定的目标</summary>
        public I_Actor TargetActor { get; private set; }

        public TargetData Target { get; protected set; } = new();

        Collider[] targetColliders;

        /// <summary>
        /// 目标在攻击范围内(肯定的吧，有目标包在范围内吧?)
        /// </summary>
        public bool IsTargetInAttackRange { get; private set; }

        /// <summary>目标是否可见</summary>
        public bool IsSeeingTarget;// { get; private set; }


        /// <summary>上次丢失目标的时间</summary>
        protected float TimeLastSeenTarget = Mathf.NegativeInfinity;

        protected Actor m_Actor;

        public GameObject showTarget;

        
        private Transform CorePoint;

        public Transform GetCorePoint()=> EyePoint == null ? transform : EyePoint;

        protected override void Start()
        {
            base.Start();
            CorePoint = GetCorePoint();
            //不知道为什么??=无效，只能老老实实if
            //if (!SearchPoint) SearchPoint = transform;
            GlobalEventManager.OnBulletHit += BulletHit;

        }
        private void OnDestroy()
        {
            GlobalEventManager.OnBulletHit -= BulletHit;
        }

        [DisplayField]
        public float show;

        public void SetActor(Actor actor)
        {
            m_Actor = actor;
        }


        public override bool Tick()
        {
            HandleTargetDetection();
            return true;
        }

        public void SetTargetActor(I_Actor actor)
        {
            if (Target.Actor != actor)
            {
                //Debug.LogError(transform.parent + "设置目标从" + Target.Actor + "变为" + actor, transform);
                Target.Set(actor);

                if (actor != null)
                {

                    targetColliders = actor.transform.GetComponentsInChildren<Collider>();
                    TimeLastSeenTarget = Time.time;
                    showTarget = actor.gameObject;
                }
                else
                {
                    showTarget = null;
                    targetColliders = null;
                }
                
            }
            
        }

        public bool ShowTargetState = false;
        public virtual void HandleTargetDetection()
        {
            bool haveOldTarget = Target.Actor.IsValid();
            ShowTargetState = haveOldTarget;

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
            IsTargetInAttackRange = haveNewTarget
                && Vector3.Distance(CorePoint.position, Target.Pos) <= AttackRange;

            //发现目标
            if (!haveOldTarget && haveNewTarget)
            {
                OnDetect();
            }
            //丢失目标
            if (haveOldTarget && !haveNewTarget)
            {
                OnLostTarget();
            }

        }

        protected bool CanLook(Vector3 targetPos,bool ignoreAngle,out RaycastHit hit)
        {
            Vector3 startPoint = CorePoint.position;
            float distance = (targetPos - startPoint).magnitude;
            bool allowRay = ignoreAngle;
            if (!ignoreAngle)
            {   //将前方向绕y轴进行eulerAngles.y度的旋转
                var fow = Quaternion.Euler(0, CorePoint.eulerAngles.y, 0) * Vector3.forward;
                float angle = Vector3.Angle(targetPos - startPoint, fow);
                float scale = FastAngleScale(angle);

                //Debug.LogError(transform.parent+"到目标点" + targetPos + "的距离" + distance+"角度"+ angle+"角度允许的距离"+ DetectionRange * scale,gameObject);
                //如果到目标的距离小于视野范围*角度系数
                allowRay = distance < DetectionRange * scale;
            }
            
            if (allowRay)
            {
                //检查是否有地形障碍物
                if (!Physics.Raycast(startPoint, (targetPos - startPoint).normalized, out hit, distance, LayerDefinition.GroundLayers))
                {
                    return true;
                }
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// 检查老对象是否还有效
        /// </summary>
        protected bool CheckOldTarget()
        {
            //旧目标锁上了无视角度限制
            if (CanLook(Target.Pos,true, out var hit))//可见反而是没有hit的(因为排除的单位层)
                //&& targetColliders.Contains(hit.collider))
            {
                TimeLastSeenTarget = Time.time;
                IsSeeingTarget = true;
                return true;
            }
            //Debug.LogError(transform.parent+"没有找到旧目标"+ TargetActor,transform);
            return false;
        }

        /// <summary>
        /// 寻找新对象
        /// </summary>
        protected bool CheckNewTarget()
        {

            PEInt closestThreat = 0;
            //var fow = Quaternion.Euler(0, EyePoint.eulerAngles.y, 0) * Vector3.forward;
            //没有无敌
            var list = BattleManager.Instance.FindUnits(new PECircle(m_Actor.LogicPos, (PEInt)DetectionRange), TargetCfg.EnemyAI
                ,item=> item.Team != m_Actor.Team && FpsHelper.VaildTarget(item));

            I_Actor newTarget=null;
            PEInt newThreat = 0;
            foreach (I_Actor item in list)
            {
                if (CanLook(item.CenterPos,false, out var hit)//可见反而是没有hit的
                    && (newThreat=FpsHelper.ThreatValue(CorePoint.position, item))>closestThreat)
                {
                    //Debug.LogError(transform.parent + "可以看见" + item, transform);
                    newTarget = item;
                    closestThreat = newThreat;
                }

            }
            if (newTarget != null)
            {
                IsSeeingTarget = true;
                Debug.LogWarning(transform.parent + "设置新目标从" + Target.Actor + "变为" + newTarget, transform);
                SetTargetActor(newTarget);
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
            //Debug.LogWarning(gameObject+"发现目标"+TargetActor,gameObject);
            onDetectedTarget?.Invoke();
        }

        /// <summary>被击中</summary>
        public virtual void OnDamaged(GameObject damageSource,bool noSource)
        {
            //Debug.LogError("被"+damageSource+"击中，无源伤害"+ noSource);
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
                var list = BattleManager.Instance.FindUnits(new PECircle(m_Actor.LogicPos, (PEInt)AlertRange), new(){},(unit)=>unit.Team == m_Actor.Team)
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

        /// <summary>
        /// 受击修改目标
        /// </summary>
        /// <param name="damageSource"></param>
        protected virtual void OnDamagedDiffuse(GameObject damageSource)
        {
            if (!damageSource && !damageSource.GetComponent<I_Actor>().IsValid())
            {
                return;
            }
            var newActor = damageSource.GetComponent<I_Actor>();
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
            if (!newActor.IsValid()) return;
            if (!Target.Actor.IsValid()|| FpsHelper.ThreatValue(CorePoint.position, Target.Actor) > FpsHelper.ThreatValue(CorePoint.position, newActor))
            {
                SetTargetActor(newActor);
                OnDetect();
            }
            
            
        }

        public virtual void OnAttack(WeaponBaseController weapon)
        {

        }

        void BulletHit(GameObject source, Vector3 pos)
        {

            if (m_Actor.IsValid()
                && source
                && source.TryGetComponent(out Actor actor)
                && actor.Team != m_Actor.Team
                && Vector3.Distance(pos, m_Actor.CenterPos) < HearingRange)
            {
                OnDamaged(source, false);
            }
        }

        #region 调试

        private const int dx = 15;

        void OnDrawGizmosSelected()
        {

            var eyeRange = DetectionRange;
            var earRange = HearingRange;
            var pos= (EyePoint==null?transform: EyePoint).position;
            var fow = Quaternion.Euler(0, (EyePoint == null ? transform : EyePoint).eulerAngles.y, 0) * Vector3.forward;
            if (EyePoint==null)
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
                    var scaleB = FastAngleScale(i+ dx);
                    var dirA = Quaternion.AngleAxis(i, NormalY(fow))* fow;
                    var dirB = Quaternion.AngleAxis(i+dx, NormalY(fow)) * fow;
                    //从旋转x度到旋转x+dx度的连线
                    Gizmos.color = Color.blue;
                    Gizmos.DrawLine(pos + dirA * eyeRange * scaleA, pos + dirB* eyeRange * scaleB);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(pos + dirA * earRange, pos + dirB * earRange);
                    //UnityEditor.Handles.Label(SearchPoint.position + Quaternion.Euler(0, i, 0) * fow * DetectionRange* scaleA + new Vector3(0, 0, 1), "夹角:" + angle+"距离A"+scaleA);
                }

                //攻击范围
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(pos, 0.1f);

                //法线方向(做由方向向量和垂直向上的向量组成的平面的法线)
                Vector3 normalForward = Vector3.Cross(fow,Vector3.up).normalized;
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
 
        //直接算好，查表
        //每15度一算
        public static float[] angleScale = new float[] {
            1,1,1,1,0.45f,0.3f,0.24f,0.21f,0.2f,0.2f,0.2f,0.2f,0.2f,
        };

        public static float FastAngleScale(float angle)
        {
            if (angle > 180) angle = -angle + 360;
            else if (angle < -180) angle += 360;
            else if (angle < 0) angle *= -1;
            return angleScale[Mathf.FloorToInt(angle / 15)];
        }

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
                // 45-90度: 计算视觉上的直线连接
                // 我们需要找到一条直线连接(45°,1)和(90°,0.2)

                // 将角度转换为弧度
                float angleRad = angle * Mathf.Deg2Rad;

                // 两个端点的笛卡尔坐标
                Vector2 p1 = new Vector2(0.71f,0.71f);
                Vector2 p2 = new Vector2(-0.1f,0.174f);

                // 直线方程参数
                float A = p2.y - p1.y;//y差值
                float B = p1.x - p2.x;//x差值
                float C = p2.x * p1.y - p1.x * p2.y;//返回值

                // 计算当前角度方向与直线的交点
                // 直线方程: A*x + B*y + C = 0
                // 射线方程: x = t*cosθ, y = t*sinθ

                // 解方程求t(即线段长度)
                float t = -C / (A * Mathf.Cos(angleRad) + B * Mathf.Sin(angleRad));
                return t;
            }
            else
            {
                return 0.2f;
            }
        }

        /// <summary>
        /// 返回一个vector在y，vector面上的法向量
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        //因为c 在平面 b 上，所以 c 可以表示为 a 和vector3.up 的线性组合：
        //c=X*a+Y*up;
        //并且c垂直a，所以Vector3.Dot(c, a)=0

        private Vector3 NormalY(Vector3 vector)
        {
            return ((-Vector3.Dot(Vector3.up, vector) / Vector3.Dot(vector, vector)) * vector + Vector3.up).normalized;
        }

    }
}