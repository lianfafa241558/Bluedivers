using System.Collections.Generic;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
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
                    var go = FPSGame.Core.VfxPool.Creat(Trails[i], Root.position, Root.rotation);
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

            // ★【表现弹】先判"是否已飞抵发送端给的终点"（联机远程弹道，见 ProjectileBase.SetVisualEndPoint）。
            //   ⚠ 必须放在 Move/TryHit **之前**：那颗终点通常就在目标身上，而本端 SphereCast 未必撞得到
            //   （两端的敌人位置只是近似一致）⇒ 只在"自己撞上"才停的话，子弹就从目标身上穿过去了（2026-10-09 实测）。
            if (ReachedVisualEndPoint())
            {
                m_isStop = true;
                // 数据说话：到点收尾（这才是"命中"该有的样子）
                NetSyncLog.BulletLog("到终点收尾", $"位置={transform.position:F2} 终点={VisualEndPoint:F2} 本帧位移={(transform.position - m_LastRootPosition).magnitude:F2}m");
                // collider = null ⇒ 走 HitFX 的"无碰撞箱"分支：停下 + 收尾迹，不结算任何伤害
                OnHit?.Invoke(BuildHitData(VisualEndPoint, -transform.forward));
                return;
            }

            var dis = Vector3.Distance(InitialPosition, transform.position);
            //超出范围
            if (MaxRange > 0 && dis > MaxRange)
            {
                m_isStop = true;
                // 数据说话：这一条就是"子弹从目标身上飞过去"的直接证据（有终点约束时不该走到这里）
                FPSGame.Utils.NetSyncLog.BulletLog("超射程消散", $"起点={InitialPosition:F2} 位置={transform.position:F2} 飞了={dis:F1}m/上限{MaxRange:F1}m " +
                    (HasVisualEndPoint ? $"终点={VisualEndPoint:F2} 到终点还差={Vector3.Distance(transform.position, VisualEndPoint):F1}m ←不该发生" : "终点=<无>"));
                //GlobalEventManager.BulletHit(Owner,transform.position);
                Debug.Log("落空"+gameObject.name,gameObject);
                OnHit?.Invoke(BuildHitData(transform.position, transform.forward));
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


        /// <summary>
        /// 【表现弹】是否已飞抵 / 越过"发送端给的终点"（普通子弹恒 false）。
        ///
        /// <para>▍判据用"本帧位移"而不是单纯的距离：子弹单帧位移可能有几米（高速弹），
        /// 只看"当前位置离终点近不近"会整套漏过去 ⇒ 把终点投影到本帧位移方向上，落在本帧走过的长度内就算到点。</para>
        /// </summary>
        private bool ReachedVisualEndPoint()
        {
            if (!HasVisualEndPoint) return false;

            float reach = Mathf.Max(Radius, 0.15f);        // 与弹体半径同量级，别让子弹"擦着过去"
            Vector3 toEnd = VisualEndPoint - m_LastRootPosition;
            Vector3 step = transform.position - m_LastRootPosition;
            float stepLen = step.magnitude;
            if (stepLen > 0.0001f)
            {
                float along = Vector3.Dot(toEnd, step) / stepLen;   // 终点在"本帧位移"上的投影长度
                if (along > 0f && along <= stepLen + reach) return true;
            }
            return toEnd.sqrMagnitude <= reach * reach;
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
                    IDamageable damagable = hit.collider.GetComponent<IDamageable>();
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

            // 数据说话：是"自己撞上了"还是"被终点收尾"—— 两条日志对照就知道表现弹怎么收场的
            FPSGame.Utils.NetSyncLog.BulletLog("撞到碰撞体", $"对象={closestHit.collider.name} 点={closestHit.point:F2} 层={LayerMask.LayerToName(closestHit.collider.gameObject.layer)}" +
                (HasVisualEndPoint ? $" 终点={VisualEndPoint:F2} 距终点={Vector3.Distance(closestHit.point, VisualEndPoint):F1}m" : " 终点=<无>"));
            //Debug.LogError("击中于" + gameObject.name, gameObject);
            //Debug.LogError("击中了 "+ closestHit.collider.name, closestHit.collider);

            //var terrain = Terrain.activeTerrain;
            //float groundY = terrain ? terrain.SampleHeight(closestHit.point) + terrain.transform.position.y : 0f;
            //Debug.LogError($"击中 {closestHit.collider.name}({closestHit.collider.GetType().Name}) " +
            //               $"层={LayerMask.LayerToName(closestHit.collider.gameObject.layer)} " +
            //               $"点={closestHit.point} 离地={closestHit.point.y - groundY:F2}m", closestHit.collider);

            OnHit?.Invoke(BuildHitData(closestHit.point, closestHit.normal, closestHit.collider));
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
            if (hit.collider.isTrigger && hit.collider.GetComponent<IDamageable>() == null)
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
            if (BulletFlag.HasFlag(BulletFlag.PenetrateTerrain) && hit.collider.GetComponent<IDamageable>() == null)
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
            bool damageable = hitdata.collider.GetComponent<IDamageable>().IsValid();
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
            OnHit?.Invoke(BuildHitData(transform.position, transform.forward));
            Release();
        }
    }
}