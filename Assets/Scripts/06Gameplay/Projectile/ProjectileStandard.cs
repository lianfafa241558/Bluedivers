using System.Collections.Generic;
using FPSGame.GameContract;
using PEMaths;

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

        /// <summary>射程(<see cref="MaxRange"/>)的计算起点：弹射后重置为弹射点。
        /// <para>⚠ 必须重置：射程是"起点到当前位置"的直线距离，弹回头时不清零会让总路程瞬间超限 ⇒ 子弹当场消散。</para></summary>
        protected Vector3 m_RangeOrigin;

        /// <summary>本段生命时长（秒），弹射后重起一段也用它</summary>
        private float m_LifeTime;

        /// <summary>生命引信协程句柄：弹射时要停掉旧的、重起一段（见 <see cref="ResetLife"/>）</summary>
        private Coroutine m_LifeRoutine;

        protected virtual void OnEnable()
        {
            OnShoot += _OnShoot;
            OnHit += HitFX;
        }

        protected virtual void _OnShoot()
        {
            //这里可以有武器，但是update里面得脱离
            InitCatapult();          // 弹射次数（没配 = 0 = 老行为）+ 清空已命中表
            m_isStop = false;
            m_ShootTime = Time.time;
            m_RangeOrigin = InitialPosition;
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
            // 生命引信：时长记下来，弹射时要按同一时长重起一段（见 ResetLife）
            m_LifeTime = WeaponBase.CurrentLife;
            m_LifeRoutine = null;    // 池化复用：上一段的句柄已失效
            StartLife();
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
                // ⚠ 插值字符串是在**调用点**先拼好的，先判开关再拼，否则每颗弹都白分配一串
                if (NetSyncLog.BulletEnabled)
                    NetSyncLog.BulletLog("到终点收尾", $"位置={transform.position:F2} 终点={VisualEndPoint:F2} 本帧位移={(transform.position - m_LastRootPosition).magnitude:F2}m");
                // collider = null ⇒ 走 HitFX 的"无碰撞箱"分支：停下 + 收尾迹，不结算任何伤害
                OnHit?.Invoke(BuildHitData(VisualEndPoint, -transform.forward));
                return;
            }

            // 射程按"本段起点"算：弹射后 m_RangeOrigin 会被重置成弹射点（见 TryCatapult）
            var dis = Vector3.Distance(m_RangeOrigin, transform.position);
            //超出范围
            if (MaxRange > 0 && dis > MaxRange)
            {
                m_isStop = true;
                // 数据说话：这一条就是"子弹从目标身上飞过去"的直接证据（有终点约束时不该走到这里）
                // ⚠ 先判开关：插值字符串在调用点就先拼好了，且 Debug.Log 本身就是每次一大笔分配
                if (FPSGame.Utils.NetSyncLog.BulletEnabled)
                {
                    FPSGame.Utils.NetSyncLog.BulletLog("超射程消散", $"起点={m_RangeOrigin:F2} 位置={transform.position:F2} 飞了={dis:F1}m/上限{MaxRange:F1}m " +
                        (HasVisualEndPoint ? $"终点={VisualEndPoint:F2} 到终点还差={Vector3.Distance(transform.position, VisualEndPoint):F1}m ←不该发生" : "终点=<无>"));
                    Debug.Log("落空" + gameObject.name, gameObject);
                }
                OnHit?.Invoke(BuildHitData(transform.position, transform.forward));
            }
            else
            {
                Move();
            }

            // 首帧 dis 在 Move 前计算为 0，若用严格 > 会跳过发射点到第一帧末的整段路径，
            // 导致高速子弹(单帧>1m)第一帧就钻入地形，之后 SphereCast 起点在碰撞体内部永不命中。
            // 用 >= 保证发射瞬间即参与检测(m_LastRootPosition=发射点，扫掠段覆盖首段)。
            // ⚠ 安全引信(MinRange)必须从**发射点**算：弹射后子弹离弹射点很近，用 m_RangeOrigin 会把它锁死"不检测"。
            if ((InitialPosition - transform.position).sqrMagnitude >= MinRange * MinRange) { TryHit(); }
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

        /// <summary>SphereCast 的结果缓冲：NonAlloc 版不分配数组。
        /// <para>⚠ 静态共用是安全的：TryHit 全程同步、没有 yield；命中回调里也不会再进另一颗子弹的 Update。</para></summary>
        private static readonly RaycastHit[] s_HitBuffer = new RaycastHit[32];

        protected virtual void TryHit()
        {
            RaycastHit closestHit = new RaycastHit();
            closestHit.distance = Mathf.Infinity;
            bool foundHit = false;

            // Sphere cast
            Vector3 displacementSinceLastFrame = Tip.position - m_LastRootPosition;
            // 原来用 SphereCastAll：每颗子弹每帧都 new 一个 RaycastHit[]
            int hitCount = Physics.SphereCastNonAlloc(m_LastRootPosition, Radius,
                displacementSinceLastFrame.normalized, s_HitBuffer, displacementSinceLastFrame.magnitude, FpsHelper.GetHittableLayers(m_Velocity.magnitude),
                k_TriggerInteraction);

            // 标旗提到循环外：原来在每个 hit 上都 Enum.HasFlag 一次（每次装箱）
            bool penetrateUnits = HasBulletFlag(BulletFlag.PenetrateUnits);
            bool penetrateTerrain = HasBulletFlag(BulletFlag.PenetrateTerrain);

            for (int hi = 0; hi < hitCount; ++hi)
            {
                RaycastHit hit = s_HitBuffer[hi];
                if (!IsHitValid(hit) || hit.distance >= closestHit.distance) continue;

                // ⚠ "这一下停不停"只在这里判、只按标旗判。原来 TryHit 只判"够不够得着"、HitFX 又按标旗判一遍，
                //   两处对"算不算单位"的口径只要不一致（部位碰撞体上没 IDamageable / Source 没连），标旗就等于没生效。
                if (penetrateUnits || penetrateTerrain)
                {
                    IDamageable damageable = hit.collider.GetComponent<IDamageable>();
                    if (damageable.IsValid())
                    {
                        // 是单位：没开"穿透单位"就照常挡下；开了就只结算一次，随后连它的其它部位碰撞体一起穿过去
                        if (penetrateUnits)
                        {
                            // ⚠ Source 没连（TransferDamageable 忘配）时 ActorGo 会 NRE，这种部位直接穿过去
                            if (damageable.Source.IsValid() && !m_hasHits.Contains(damageable.ActorGo))
                            {
                                m_hasHits.Add(damageable.ActorGo);
                                Hit(hit);
                            }
                            continue;
                        }
                    }
                    else if (penetrateTerrain)
                    {
                        // 地形 / 道具（没有 IDamageable）：穿透地形就直接过去，不结算也不播命中特效
                        continue;
                    }
                }

                foundHit = true;
                closestHit = hit;
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
            // ⚠ 先判开关再拼字符串：这条每次命中都会走，插值串 + LayerToName 都是白分配的
            if (FPSGame.Utils.NetSyncLog.BulletEnabled)
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
            FaceVelocity();

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

        /// <summary>朝向速度（纯表现，不影响弹道）。抽成虚方法是给低速弹种关小它：
        /// 方向每帧的变化率 ≈ 重力×Δt/速度，速度越低甩得越快（手雷落地前会像原地自转）。</summary>
        protected virtual void FaceVelocity()
        {
            if (m_Velocity != Vector3.zero) transform.forward = m_Velocity.normalized;
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

            // ⚠ "穿透地形"不在这里判：停不停统一由 TryHit 一处按标旗决定（这里滤掉的话，
            //   同一个标旗就有两个解释点，改一边忘一边就是"标旗没反应"）。
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

            // 弹射：命中**单位**且还有次数 ⇒ 改朝最近的另一个目标继续飞，本发不结束
            // ⚠ 这里只改"往哪飞"：OnHit 的订阅顺序是 HitFX 先、FpsHelper.Hit（伤害）后
            //   （见 OnEnable 与 ProjectileBase.Shoot）⇒ 本次命中的伤害早已封装好，改位姿不影响结算。
            if (TryCatapult(hitdata.pos, hitdata.collider, out var catapultTarget))
            {
                Redirect(hitdata.pos, catapultTarget.CenterPos);
                return;
            }

            // ⚠ 这里的"停不停"必须与 TryHit 同口径（都对 collider.GetComponent<IDamageable>() 判"是不是单位"）：
            //   穿透单位打中的那份 Hit 也会走到这（要播命中特效、要结算伤害），不能被误停。
            //   两处口径一旦分叉（例如这边要求 Source 有效），标旗就等于没生效。
            bool damageable = hitdata.collider.GetComponent<IDamageable>().IsValid();
            if ((damageable && !HasBulletFlag(BulletFlag.PenetrateUnits))
                || (!damageable && !HasBulletFlag(BulletFlag.PenetrateTerrain)))
            {
                TryStop();
            }

        }
        void TryStop()
        {
            if (HasBulletFlag(BulletFlag.RetainOnHit))
            {
                m_isStop = true;
            }
            else
            {
                Release();
            }
            StopTrail();
        }

        /// <summary>把子弹挪到弹射点并改朝新目标飞（"选谁弹"在 <see cref="ProjectileBase.TryCatapult"/>）。</summary>
        private void Redirect(Vector3 from, Vector3 toPos)
        {
            Vector3 dir = toPos - from;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

            // ⚠ 必须先转向再摆位置：Root/Tip 是子物体，转过之后 Root 相对父级的偏移也变了
            //   （先摆位置会让弹体停在弹射点旁边几厘米到几十厘米处）
            transform.forward = dir;
            Vector3 rootOffset = Root.position - transform.position;
            // 沿新方向让开一点：下一帧 SphereCast 的起点还在刚命中的碰撞体里 ⇒ 会从内部起扫（漏检/自撞）
            transform.position = from + dir * (Radius + 0.05f) - rootOffset;

            // 速度直接换向：保留重力累加会让弹射变成抛物线，近距离目标直接打偏
            m_Velocity = dir * (WeaponBase ? WeaponBase.CurrentSpeed : m_Velocity.magnitude);
            m_ShootTime = Time.time;          // 重新给"刚起飞 1m 内不判碰撞"的宽限，防止立刻自撞刚打中的单位
            m_RangeOrigin = transform.position;
            m_LastRootPosition = Root.position;
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

 
        /// <summary>按 <see cref="m_LifeTime"/> 重新起一次引信（旧的那一段先停掉）。</summary>
        private void StartLife()
        {
            if (m_LifeRoutine != null)
            {
                StopCoroutine(m_LifeRoutine);
                m_LifeRoutine = null;
            }
            if (m_LifeTime > 0f) m_LifeRoutine = StartCoroutine(DelayedRelese(m_LifeTime));
        }

        /// <summary>弹射后重起生命：不重起的话，"快到期时才弹出去"的那一段会被剩几毫秒的寿命直接吃掉
        /// （到点就在弹射点就地结算并回收，看起来像"弹射没生效"）。</summary>
        protected override void ResetLife() => StartLife();


        protected virtual System.Collections.IEnumerator DelayedRelese(float time)
        {
            yield return new WaitForSeconds(time);
            OnHit?.Invoke(BuildHitData(transform.position, transform.forward));
            Release();
        }
    }
}