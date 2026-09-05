using System.Collections.Generic;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 标准子弹
    /// </summary>
    [AddComponentMenu("子弹/标准子弹", 30)]
    public class ProjectileStandard : ProjectileBase
    {


        [Header("通用")]
        [InspectorName("碰撞半径")]
        public float Radius = 0.01f;
        [InspectorName("根部变换(精确碰撞检测)")]
        public Transform Root;
        [InspectorName("尖端变换(精确碰撞检测)")]
        public Transform Tip;

        [Header("尾迹")]
        [InspectorName("尾迹")]
        public List<GameObject> Trails;
        [InspectorName("尾迹宽度")]
        public float TrailWidth = 0f;
        [InspectorName("尾迹持续时间")]
        public float LiftTime = 1.5f;



        public static Color RadiusColor = Color.cyan * 0.2f;

        protected float m_ShootTime;
        protected Vector3 m_LastRootPosition;
        protected Vector3 m_Velocity;
        protected float m_lastTime;

        //protected List<Collider> m_IgnoredColliders;
        protected bool m_isStop;
        const QueryTriggerInteraction k_TriggerInteraction = QueryTriggerInteraction.Collide;

        private List<GameObject> m_Trails=new();

        protected List<GameObject> m_hasHits;

        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
            OnHit += HitFX;
        }

        protected virtual void _OnShoot()
        {
            //这里可以有武器，但是update里面得脱离
            m_hasHits = new();
            m_isStop = false;
            m_ShootTime = Time.time;
            m_LastRootPosition = Root.position;
            m_Velocity = transform.forward * WeaponBase.CurrentSpeed;
            //m_IgnoredColliders = new List<Collider>();
            transform.position += InheritedMuzzleVelocity * Time.deltaTime;
            m_Trails.Clear();
            if (Trails.Count > 0)
            {
                for(int i = 0; i < Trails.Count; ++i)
                {
                    var go = VFXManager.Creat(Trails[i], Root.position, Root.rotation);
                    m_Trails.Add(go);
                    go.GetComponent<LimitedLife>().SetLift(Mathf.Max(WeaponBase.CurrentLife, 0)+LiftTime);
                    if (go.TryGetComponent<TrailRenderer>(out var tr))
                    {
                        tr.Clear();
                        tr.SetPositions(new Vector3[0]);
                        tr.time = LiftTime;
                        tr.startWidth = tr.endWidth = TrailWidth;
                    }
                    if (go.TryGetComponent<ParticleSystem>(out var ps))
                    {
                        var emission = ps.emission;
                        emission.enabled = true;
                    }
                }
               
            }


            //Debug.LogError("创建了" + gameObject.name, gameObject);
            //忽略武器的碰撞箱
            //Debug.LogError("所有" + Owner);
            /*
            Collider[] weaponColliders = Owner.GetComponentsInChildren<Collider>();
            if (weaponColliders.IsValid()) m_IgnoredColliders.AddRange(weaponColliders);
            */
            if (WeaponBase.CurrentLife > 0) StartCoroutine(DelayedRelese(WeaponBase.CurrentLife));
            m_lastTime = Time.time;
        }

        protected virtual void Update()
        {

            if (m_isStop) return;
            var dis = Vector3.Distance(InitialPosition, transform.position);
            //超出范围
            if (MaxRange > 0 && dis > MaxRange)
            {
                m_isStop = true;
                //GlobalEventManager.BulletHit(Owner,transform.position);
                Debug.Log("落空"+gameObject.name,gameObject);
                OnHit?.Invoke(new() {
                    pos = transform.position,
                    normal = transform.forward,
                    collider = null,
                    data = DamageData,
                    chargeScale = Charge,
                    soure = Owner,
                    self = gameObject,
                    sfxRange = SFXRange,
                    weapon = WeaponBase,
                    useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
                });
            }
            else
            {
                Move();
            }

            // 首帧 dis 在 Move 前计算为 0，若用严格 > 会跳过发射点到第一帧末的整段路径，
            // 导致高速子弹(单帧>1m)第一帧就钻入地形，之后 SphereCast 起点在碰撞体内部永不命中。
            // 用 >= 保证发射瞬间即参与检测(m_LastRootPosition=发射点，扫掠段覆盖首段)。
            if (dis >= MinRange) { TryHit(); }
            m_lastTime = Time.time;
            m_LastRootPosition = Root.position;
        }


        protected virtual void TryHit()
        {
            RaycastHit closestHit = new RaycastHit();
            closestHit.distance = Mathf.Infinity;
            bool foundHit = false;

            // Sphere cast
            Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
            RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, Radius,
                displacementSinceLastFrame.normalized, displacementSinceLastFrame.magnitude, FpsHelper.GetHittableLayers(m_Velocity.magnitude),
                k_TriggerInteraction);
            foreach (var hit in hits)
            {
                if (IsHitValid(hit) && hit.distance < closestHit.distance)
                {
                    //可以伤害说明是单位
                    //if ((damageable.IsValid() && !BulletFlag.HasFlag(BulletFlag.PenetrateUnits))
                    //    || (!damageable.IsValid() && !BulletFlag.HasFlag(BulletFlag.PenetrateTerrain)))
                    //{
                    //这里的意思就是，使用穿透单位击中单位不会结束，而是添加到列表并继续
                    //这里不控制停止，只考虑击中伤害
                    //穿透地面直接写ishitVaild里面了，因为反正不用考虑对地面进行控制（大概吧）
                    I_Damagable damagable = hit.collider.GetComponent<I_Damagable>();
                    if (BulletFlag.HasFlag(BulletFlag.PenetrateUnits) && damagable.IsValid() && damagable.Source.IsValid() && !m_hasHits.Contains(damagable.ActorGo))
                    {
                        m_hasHits.Add(damagable.ActorGo);
                        Hit(hit);

                    }
                    else
                    {
                        foundHit = true;
                        closestHit = hit;
                    }
                }
            }

            if (foundHit)
            {
                Hit(closestHit);
            }
        }

        private void Hit(RaycastHit closestHit)
        {
            // 处理在碰撞箱内部的问题
            if (closestHit.distance <= 0f)
            {
                closestHit.point = Root.position;
                closestHit.normal = -transform.forward;
            }
            //Debug.LogError("击中于" + gameObject.name, gameObject);
            //Debug.LogError("击中了 "+ closestHit.collider.name, closestHit.collider);
            OnHit?.Invoke(new() {
                pos = closestHit.point,
                normal = closestHit.normal,
                collider = closestHit.collider,
                data = DamageData,
                chargeScale = Charge,
                soure = Owner,
                self = gameObject,
                sfxRange = SFXRange,
                weapon = WeaponBase,
                useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
            });
        }

        protected virtual void Move()
        {
            //位置是时间的平方而速度是一次方，所以时间波动会导致精度不同
            var tick = Time.time - m_lastTime;
            //Debug.DrawRay(transform.position, m_Velocity * Time.deltaTime, Color.HSVToRGB(Time.time % 1, 1, 1), WeaponBase.MaxLifeTime);
            // 向速度方向移动
            //if (InheritedMuzzleVelocity.sqrMagnitude>0)//(反正0不影响效果，可以直接不要if)
            transform.position += (m_Velocity + InheritedMuzzleVelocity) * tick;

            //朝向速度(仅仅为展示方向，与逻辑速度无关)
            if(m_Velocity!=Vector3.zero) transform.forward = m_Velocity.normalized;
            
            // 下坠速度
            if (Gravity > 0)
            {
                m_Velocity += Vector3.down * Gravity * tick;
            }
            for (int i = m_Trails.Count - 1; i >= 0; --i)
            {
                m_Trails[i].transform.position = Root.position;
                m_Trails[i].transform.rotation = Root.rotation;
            }
        }

        bool IsHitValid(RaycastHit hit)
        {
            //Debug.LogError("尝试碰撞"+ hit.collider);
            var ihd = hit.collider.GetComponent<IgnoreHitDetection>();
            //使用忽略组件忽略点击
            if (ihd)
            {
                //双向忽略
                if (!ihd.Unidirectional) {
                    //Debug.LogError("双向忽略而失败" + hit.collider);
                    return false;
                }
                //单向忽略(如果发射点在碰撞箱内部就忽略)
                else if(hit.collider.bounds.Contains(InitialPosition)){
                    //Debug.LogError("单向忽略而失败" + hit.collider);
                    return false;
                }
            }

            //忽略没有可损坏组件的触发器的命中
            if (hit.collider.isTrigger && hit.collider.GetComponent<I_Damagable>() == null)
            {
                //Debug.LogError("没有伤害组件" + hit.collider);
                return false;
            }
            /*
            //忽略具有特定忽略碰撞器（默认情况下为自碰撞器）的碰撞
            if (m_IgnoredColliders != null && m_IgnoredColliders.Contains(hit.collider))
            {
                //Debug.LogError("自碰撞忽略" + hit.collider);
                return false;
            }*/

            //小于0.01m不进行碰撞
            if ((Time.time- m_ShootTime) * (m_Velocity + InheritedMuzzleVelocity).magnitude<1f)
            {
                //Debug.LogError("自碰撞忽略" + hit.collider);
                return false;
            }

            //如果有忽略地面标签并且目标没有伤害组件就直接忽略
            if (BulletFlag.HasFlag(BulletFlag.PenetrateTerrain) && hit.collider.GetComponent<I_Damagable>() == null)
            {
                //Debug.LogError("穿透地形忽略" + hit.collider);
                return false;
            }
            //Debug.LogError("碰撞成功" + hit.collider);
            return true;
        }
        
        /// <summary>击中 </summary>
        void HitFX(ProjectileHitData hitdata)
        {
            //目标没有碰撞箱,真的有这个可能吗
            if (!hitdata.collider.IsValid()) {
                TryStop();
                return;
            }
            bool damageable = hitdata.collider.GetComponent<I_Damagable>().IsValid();
            //可以伤害说明是单位
            //Debug.LogError("是单位?"+ damageable+"有穿透单位标记"+ BulletFlag.HasFlag(BulletFlag.PenetrateUnits)+"有穿透地形标记"+ BulletFlag.HasFlag(BulletFlag.PenetrateTerrain));


            if ((damageable && !BulletFlag.HasFlag(BulletFlag.PenetrateUnits))
                ||(!damageable && !BulletFlag.HasFlag(BulletFlag.PenetrateTerrain))) 
            {
                TryStop();
            }

        }
        void TryStop()
        {
            if (BulletFlag.HasFlag(BulletFlag.RetainOnHit))
            {
                m_isStop = true;
            }
            else
            {
                Release();
            }
            StopTrail();
        }


        void StopTrail()
        {
            for (int i = m_Trails.Count-1; i >=0; --i)
            {
                m_Trails[i].GetComponent<LimitedLife>().ResetLift(LiftTime);
                if (m_Trails[i].TryGetComponent<ParticleSystem>(out var ps))
                {
                    var emission = ps.emission;
                    emission.enabled = false;
                }
            }
            m_Trails.Clear();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = RadiusColor;
            Gizmos.DrawWireSphere(Root.position, Radius);
            if(Root!= Tip) Gizmos.DrawWireSphere(Tip.position, Radius);
        }

 
        protected virtual System.Collections.IEnumerator DelayedRelese(float time)
        {
            yield return new WaitForSeconds(time);
            OnHit?.Invoke(new() {
                pos = transform.position,
                normal = transform.forward,
                collider = null,
                data = DamageData,
                chargeScale = Charge,
                soure = Owner,
                self =gameObject,
                sfxRange = SFXRange,
                weapon = WeaponBase,
                useDiffScale = BulletFlag.HasFlag(BulletFlag.EnemyIntensify),
            });
            Release();
        }
    }
}