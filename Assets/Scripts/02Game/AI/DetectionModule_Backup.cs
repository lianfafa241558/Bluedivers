/*
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
    public class DetectionModule : MonoBehaviour
    {
        /// <summary>必须在不碰撞自身的地方</summary>
        [NullCheck]
        [SerializeField]
        [CustomLabel("单位眼睛的点")]
        protected Transform EyePoint;

        [CustomLabel("索敌距离")]
        public float DetectionRange = 20f;

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

        Collider[] targetColliders;

        public bool IsTargetInAttackRange { get; private set; }

        /// <summary>目标是否可见</summary>
        public bool IsSeeingTarget { get; private set; }
        public bool HadKnownTarget { get; private set; }
        /// <summary>上次丢失目标的时间</summary>
        protected float TimeLastSeenTarget = Mathf.NegativeInfinity;

        protected Actor m_Actor;



        protected virtual void Start()
        {
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

        public void SetTargetActor(I_Actor actor)
        {
            if (TargetActor != actor)
            {
                TargetActor = actor;

                if (actor != null)
                {
                    targetColliders = actor.transform.GetComponentsInChildren<Collider>();
                    TimeLastSeenTarget = Time.time;
                }
                else targetColliders = null;
            }
            
        }

        //在单位的update中调用
        public virtual void HandleTargetDetection(Collider[] selfColliders)
        {

            show = (Time.time - TimeLastSeenTarget);

            if (!TargetActor.IsValid() || !CheckOldTarget())
            {
                CheckNewTarget(m_Actor, selfColliders);
            }
            //处理已知的目标检测超时
            if (!TargetActor.IsValid() && !IsSeeingTarget && Time.time > TimeLastSeenTarget + KnownTargetTimeout)
            {
                HadKnownTarget = true;
                SetTargetActor(null);

                //Debug.LogError("设置目标为null");
            }

            IsTargetInAttackRange = TargetActor != null
                && Vector3.Distance(EyePoint.position, TargetActor.CenterPos) <= AttackRange;

            //发现目标
            if (!HadKnownTarget &&
                TargetActor != null)
            {
                OnDetect();
            }
            //丢失目标
            if (HadKnownTarget &&
                TargetActor == null)
            {
                OnLostTarget();
                
            }

            //记住我们是否已经知道下一帧的目标
            HadKnownTarget = TargetActor != null;
        }

        /// <summary>
        /// 检查老对象是否还有效
        /// </summary>
        protected bool CheckOldTarget()
        {
            IsSeeingTarget = false;
            if (FpsHelper.VaildTarget(TargetActor)) return false;

            var aimPoint = TargetActor.CenterPos;
            var fow = Quaternion.Euler(0, EyePoint.eulerAngles.y, 0) * Vector3.forward;
            Vector3 startPoint = EyePoint.position;
            float angle = Vector3.Angle(aimPoint - startPoint, fow);
            float scale = DetectionAngleScale(angle);

            float distance = (aimPoint - startPoint).magnitude;
            //如果到目标的距离小于视野范围*角度系数
            if (distance < DetectionRange * scale )
            {
                //检查是否有障碍物
                RaycastHit[] hits = Physics.RaycastAll(startPoint,
                    (aimPoint - startPoint).normalized, DetectionRange,
                    -1, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits)
                {
                    //Debug.LogError("击中" + hit.collider,hit.collider.gameObject);
                    //只要视角上还看得见就行
                    if (targetColliders.Contains(hit.collider))
                    {
                        //Debug.LogError("有效");
                        TimeLastSeenTarget = Time.time;
                        IsSeeingTarget = true;
                        return true;
                    }
                }
            }
            else
            {
                //Debug.LogError("不在视野内");
            }

            return false;
        }
        
        protected bool CanLook(Vector3 tragetPos,Vector3 fow,out Collider collider)
        {
            Vector3 startPoint = EyePoint.position;
            float angle = Vector3.Angle(tragetPos - startPoint, fow);
            float scale = DetectionAngleScale(angle);

            float distance = (tragetPos - startPoint).magnitude;
            //如果到目标的距离小于视野范围*角度系数
            if (distance < DetectionRange * scale)
            {
                //检查是否有障碍物
                //如果被其他单位阻挡就丢失感觉怪怪的？
                //先排除单位
                if (Physics.Raycast(startPoint, (tragetPos - startPoint).normalized, out RaycastHit hit, DetectionRange, LayerDefinition.GroundLayers))
                {
                    collider = hit.collider;
                    return true;
                }
            }
            collider = null;
            return false;
        }

        protected bool CheckNewTarget(Actor actor, Collider[] selfColliders)
        {
            //找到最接近的可见敌对单位
            IsSeeingTarget = false;

            float closestSqrDistance = Mathf.Infinity;
            var fow = Quaternion.Euler(0, EyePoint.eulerAngles.y, 0) * Vector3.forward;
            //没有无敌
            var list = BattleManager.Instance.FindUnits(new PECircle((PEVector2)actor.Pos, (PEInt)DetectionRange), TargetCfg.EnemyAI,item=>!item.HasFlag( ActorFlag.Invincible));

            foreach (I_Actor target in list)
            {
                if (target.Team != actor.Team && target.ActorState!=ActorState.Dead)//队伍不同
                {

                    float angle = Vector3.Angle(target.CenterPos - EyePoint.position, fow);
                    float scale = DetectionAngleScale(angle);

                    //angle *= Mathf.Sign(Vector3.Cross(KnownDetectedTarget.transform.position - Detectiontransform.position.normalized, fow).y);
                    //var dir = Quaternion.AngleAxis(angle* Mathf.Sign(Vector3.Cross(target.transform.position - Detectiontransform.position.normalized, fow).y), NormalY(fow)) * fow;
                    //Debug.DrawRay(Detectiontransform.position, dir* DetectionRange * scale,Color.yellow,Time.deltaTime*2);

                    float distance = (target.Pos - transform.position).magnitude;
                    //如果到目标的距离小于视野范围*角度系数
                    if (distance < DetectionRange * scale && distance < closestSqrDistance)
                    {
                        //Debug.LogError(actor + "到" + target + "角度" + angle + "范围" + DetectionRange * scale);
                        //检查是否有障碍物
                        //RaycastAll的原因是要排除自身的碰撞箱
                        //那我再设置一个瞄准的起始点，保证不在碰撞箱内行不行
                        
                        RaycastHit[] hits = Physics.RaycastAll(transform.position,
                            (target.AimPoint.position - transform.position).normalized, DetectionRange,
                            -1, QueryTriggerInteraction.Ignore);
                        RaycastHit closestValidHit = new RaycastHit();
                        closestValidHit.distance = Mathf.Infinity;
                        bool foundValidHit = false;
                        foreach (var hit in hits)
                        {
                            if (!selfColliders.Contains(hit.collider) && hit.distance < closestValidHit.distance)
                            {
                                closestValidHit = hit;
                                foundValidHit = true;
                            }
                        }
                        
                        //发现单位
                        if (foundValidHit)
                        {
                            I_Actor hitActor = closestValidHit.collider.GetComponentInParent<I_Actor>();
                            if (hitActor.Equals(target))
                            {
                                IsSeeingTarget = true;
                                closestSqrDistance = distance;
                                SetTargetActor(target);
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }



        /// <summary>丢失目标</summary>
        protected virtual void OnLostTarget() => onLostTarget?.Invoke();
        /// <summary>发现目标</summary>
        protected virtual void OnDetect() => onDetectedTarget?.Invoke();

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
            //Debug.LogError("受击设置锁定目标"+ damageSource);
            
            if (!TargetActor.IsValid()|| FpsHelper.ThreatValue(transform.position, TargetActor) > FpsHelper.ThreatValue(transform.position, damageSource.GetComponent<I_Actor>()))
            {
                SetTargetActor(damageSource.GetComponent<I_Actor>());
            }
            else if(TargetActor.Equals(damageSource))
            {
                TimeLastSeenTarget = Time.time;
            }
            
        }

        public virtual void OnAttack(WeaponController weapon)
        {

        }

        void BulletHit(GameObject source, Vector3 pos)
        {

            if (m_Actor.IsValid()&&source.TryGetComponent(out Actor actor)&&actor.Team != m_Actor.Team && Vector3.Distance(pos, m_Actor.CenterPos) < 10)
            {
                OnDamaged(source, false);
            }
        }

        #region 调试

        private const int dx = 45;

        void OnDrawGizmosSelected()
        {

            var range = DetectionRange;
            var pos= EyePoint.position;
            var fow = Quaternion.Euler(0, EyePoint.eulerAngles.y,0)*Vector3.forward;

            //侦测范围
            Gizmos.color = Color.blue;
            
            for (int i = 0; i < 360; i += dx)
            {
                var scaleA = DetectionAngleScale(i);
                var scaleB = DetectionAngleScale(i+ dx);
                var dirA = Quaternion.AngleAxis(i, NormalY(fow))* fow;
                var dirB = Quaternion.AngleAxis(i+dx, NormalY(fow)) * fow;
                //从旋转x度到旋转x+dx度的连线
                Gizmos.DrawLine(pos + dirA * range * scaleA, pos + dirB* range * scaleB);
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
            
            if (m_Actor)
            {
                //碰撞范围
                Gizmos.color = Color.yellow;
                switch (m_Actor.shape)
                {
                    case ShapeType.Circle:
                        Gizmos.DrawWireSphere(pos, m_Actor.rangeLength);
                        break;
                    case ShapeType.Rectangle:
                        
                        Gizmos.DrawWireCube(pos, Vector3.one * m_Actor.rangeLength * 2);
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
        //public float show2;

        /// <summary>
        /// 输入(-360,360)自动转换为夹角(0,180)
        /// </summary>
        /// <param name="angle"></param>
        /// <returns></returns>
        private float DetectionAngleScale(float angle)
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
*/