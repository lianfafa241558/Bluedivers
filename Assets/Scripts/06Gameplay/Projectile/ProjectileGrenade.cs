using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 手雷：撞到东西**不结算伤害、也不消失**，而是按法线弹跳衰减；
    /// 真正的爆点由基类的引信（<see cref="DamageData.MaxLifeTime"/> 秒，经 <c>DelayedRelese</c>）决定，
    /// 到点在**当前位置**结算一次范围伤害。所以伤害配置里 <c>MaxRange</c> 要留 -1（别用米制自爆引信）。
    /// <para>粘性：第一个碰撞就贴住不再弹，并派发一次碰撞事件（用来点燃附着效果）。
    /// 黏住的这颗会**跟着被命中的节点走**（打在走动的敌人身上不会停在原地）。</para>
    /// </summary>
    [AddComponentMenu("子弹/手雷", 31)]
    public class ProjectileGrenade : ProjectileStandard
    {
        /// <summary>贴面留的余隙(米)：严格相切的话下一帧球扫的**起点**与碰撞体重叠，
        /// Unity 只给 distance=0 且 point/normal 未填充的退化命中 ⇒ 弹体被瞬移到世界原点。</summary>
        private const float k_SurfaceGap = 0.01f;

        /// <summary>球扫结果缓冲：NonAlloc 不分配数组（与基类 <c>ProjectileStandard</c> 的 s_HitBuffer 同口径）。
        /// <para>⚠ 静态共用安全：TryHit 全程同步、无 yield，命中回调里也不会再进另一颗手雷的 TryHit。</para></summary>
        private static readonly RaycastHit[] s_HitBuffer = new RaycastHit[32];

        [Header("弹跳")]
        [InspectorName("可弹跳次数")]
        [Tooltip("超过这个次数后不再反弹，直接趴在原地等引信")]
        [SerializeField]
        private int MaxBounce = 3;

        [InspectorName("弹跳速度保留")]
        [Range(0f, 1f)]
        [SerializeField]
        private float BounceDamping = 0.5f;

        [InspectorName("静止阈值(米/秒)")]
        [SerializeField]
        private float RestSpeed = 0.6f;

        [InspectorName("粘性")]
        [Tooltip("勾上=撞到第一个东西就贴住(不弹跳、不再受重力)，并在那一瞬间派发碰撞事件")]
        [SerializeField]
        private bool Sticky;

        [Header("自转(表现)")]
        [InspectorName("自转最低速度(米/秒)")]
        [Tooltip("低于此速度就定住不转。低速段速度方向几乎全由重力决定，越慢甩得越快，不拦就会出现\"快停下了还在原地高速自转\"")]
        [SerializeField]
        private float SpinMinSpeed = 2f;

        [InspectorName("自转最大角速度(度/秒)")]
        [Tooltip("只压低速段的甩头：高速飞行时方向本来就不变，调它不影响弹道")]
        [SerializeField]
        private float MaxSpinSpeed = 240f;

        [InspectorName("碰撞时(逻辑)")]
        [Tooltip("只有真实弹派发；远端那颗表现弹不派发 ⇒ 挂会结算伤害/持续效果的东西用这个")]
        [SerializeField]
        private UnityEvent OnCollisionLogic;

        [InspectorName("碰撞时(表现)")]
        [Tooltip("真实弹和远端表现弹都派发 ⇒ 挂音效/火花/贴附特效这类纯表现用这个")]
        [SerializeField]
        private UnityEvent OnCollisionView;

        [InspectorName("销毁时(逻辑)")]
        [Tooltip("只有真实弹派发；远端表现弹不派发 ⇒ 挂\"停掉伤害/撤掉持续效果\"用这个")]
        [SerializeField]
        private UnityEvent OnDestroyedLogic;

        [InspectorName("销毁时(表现)")]
        [Tooltip("真实弹和远端表现弹都派发 ⇒ 挂收尾音效/消散特效这类纯表现用这个")]
        [SerializeField]
        private UnityEvent OnDestroyedView;

        /// <summary>本次投掷已弹跳次数</summary>
        private int m_Bounced;

        /// <summary>已趴下：不再受重力（否则每帧往地里钻再被弹回来，表现为抖动）</summary>
        private bool m_Resting;

        /// <summary>黏住后跟随的节点：建筑物=碰撞体自身（不动），可移动单位=Actor 根/刚体</summary>
        private Transform m_StickPivot;

        /// <summary>黏住瞬间手雷相对跟随节点的位姿 ⇒ 之后每帧还原，才能贴在移动单位身上跟着走</summary>
        private Vector3 m_StickLocalPos;
        private Quaternion m_StickLocalRot;

        protected override void _OnShoot()
        {
            base._OnShoot();
            // 池化复用：上一颗的弹跳/静止/黏附状态必须清掉
            m_Bounced = 0;
            m_Resting = false;
            m_StickPivot = null;
        }

        /// <summary>回池(SetActive false)或真销毁都会走到 ⇒ 这就是"这颗手雷没了"的唯一时机。</summary>
        private void OnDisable()
        {
            // ⚠ 表现弹的循环标记在 OnDisable 时仍是上一颗的值：Shoot 里复位、MarkVisualBullet 之后再没动过 ⇒ 可靠
            OnDestroyedView?.Invoke();
            if (!IsVisualBullet) OnDestroyedLogic?.Invoke();
        }

        protected override void Move()
        {
            if (!m_Resting)
            {
                base.Move();
                return;
            }

            // 黏住的这颗要跟着目标走。⚠ 不 parent：目标被销毁会连子物体一起销毁，池里的手雷跟着消失 ⇒ 引信被打断、不炸。
            // 目标已销毁（Unity fake-null）⇒ 停在最后位置，引信照常爆。
            if (m_StickPivot)
            {
                transform.SetPositionAndRotation(
                    m_StickPivot.TransformPoint(m_StickLocalPos),
                    m_StickPivot.rotation * m_StickLocalRot);
            }
        }

        /// <summary>
        /// 手雷不能直接朝速度转向：方向每帧的变化率 ≈ 重力/速度，速度掉到 1 米/秒时已近 500 度/秒
        /// ⇒ 明明快停下了还在原地高速自转。这里"低速定住 + 角速度上限"压下来，只改表现、不动弹道。
        /// </summary>
        protected override void FaceVelocity()
        {
            float speed = m_Velocity.magnitude;
            if (speed < SpinMinSpeed || speed < 0.0001f) return;

            Vector3 target = m_Velocity / speed;
            float maxDegrees = MaxSpinSpeed * Mathf.Min(Time.deltaTime, 0.1f);
            if (Vector3.Angle(transform.forward, target) <= maxDegrees)
            {
                transform.forward = target;
                return;
            }
            transform.forward = Vector3.RotateTowards(transform.forward, target, maxDegrees * Mathf.Deg2Rad, 0f);
        }

        protected override void TryHit()
        {
            if (m_Resting || m_isStop) return;

            Vector3 displacement = Tip.position - m_LastRootPosition;
            float length = displacement.magnitude;
            if (length < 0.0001f) return;

            // NonAlloc：原来 SphereCastAll 每帧每颗手雷都 new 一个 RaycastHit[]
            int hitCount = Physics.SphereCastNonAlloc(m_LastRootPosition, Radius, displacement / length, s_HitBuffer, length,
                FpsHelper.GetHittableLayers(m_Velocity.magnitude), QueryTriggerInteraction.Collide);

            RaycastHit closest = default;
            float nearest = float.MaxValue;
            bool found = false;
            bool overlapped = false;      // 出现过"起点就重叠"的退化命中 = 球心已经陷在碰撞体里
            for (int i = 0; i < hitCount; ++i)
            {
                RaycastHit hit = s_HitBuffer[i];
                // ⚠ distance=0 = "扫掠起点已与该碰撞体重叠"的退化命中：point/normal 不填（全是 0），
                //   当命中用会把弹体瞬移到世界原点、到点就在原点炸开（基类 Hit 里也有同一条判据）
                if (hit.distance <= 0f)
                {
                    if (IsBounceValid(hit)) overlapped = true;
                    continue;
                }
                if (!IsBounceValid(hit) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                closest = hit;
                found = true;
            }

            if (found) Collide(closest);
            else if (overlapped) RecoverFromOverlap();
        }

        /// <summary>
        /// 球心已经陷进碰撞体里的自救。**不救就会一路掉到地下**：Unity 对"起点已重叠"只给 distance=0，
        /// 而上面必须跳过这种命中（它的 point 是 0）⇒ 之后每帧都扫不到它，重力把弹体越拽越深。
        /// <para>做法：从球心正上方往下探一次拿真实的面，再走 <see cref="Collide"/> 那套贴面 + 反弹。</para>
        /// </summary>
        private void RecoverFromOverlap()
        {
            float up = Radius * 3f;
            if (!Physics.SphereCast(transform.position + Vector3.up * up, Radius, Vector3.down, out RaycastHit hit,
                    up * 2f, FpsHelper.GetHittableLayers(m_Velocity.magnitude), QueryTriggerInteraction.Collide))
                return;
            if (hit.distance <= 0f) return;      // 探测起点自己也重叠 ⇒ 这次拿不到面，下一帧再探
            if (!IsBounceValid(hit)) return;     // 头顶压着的是触发器/投掷者，不是地面
            Collide(hit);
        }

        /// <summary>能不能拿来弹：这里不结算伤害，所以只排除"不该碰的"（自己/投掷者/双向忽略的触发器）。</summary>
        private bool IsBounceValid(RaycastHit hit)
        {
            Collider col = hit.collider;
            if (!col) return false;

            // 刚出手时还在投掷者身上：与基类同一条判据(速度×飞行时间不足1米不参与检测)
            if ((Time.time - m_ShootTime) * (m_Velocity + InheritedMuzzleVelocity).magnitude < 1f) return false;

            // 投掷者自己的碰撞箱（手雷出生在胸口位置，不排掉会立刻被反弹回来）
            if (Owner && col.transform.IsChildOf(Owner.transform)) return false;

            if (col.TryGetComponent(out IgnoreHitDetection ihd) && !ihd.Unidirectional) return false;

            // 纯触发器（无伤害组件）当空气，与基类一致
            if (col.isTrigger && !col.TryGetComponent(out IDamageable _)) return false;

            return true;
        }

        /// <summary>
        /// 黏住后跟谁走：单位跟 <see cref="Actor"/> 根（**别跟肢体碰撞体**——肢体带骨骼动画，手雷会跟着抖）；
        /// 其余（建筑/地形/家具）跟碰撞体自身，本来就不动。
        /// 项目里没有任何刚体（弹壳那套 <c>WeaponControllerPhysicalBullets</c> 未接线），所以不判 attachedRigidbody。
        /// </summary>
        private static Transform ResolveStickPivot(Collider col)
        {
            Actor actor = col.GetComponentInParent<Actor>();
            return actor ? actor.transform : col.transform;
        }

        private void Collide(RaycastHit hit)
        {
            Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -m_Velocity.normalized;

            // 贴到碰撞面外沿：贴住/弹跳共用，避免钻在碰撞体里(下一个 SphereCast 起点在内部会漏检)
            // ⚠ 必须留 k_SurfaceGap：恰好相切 ⇒ 下一帧起点重叠 ⇒ 退化命中(见 TryHit 里的判据)
            transform.position = hit.point + normal * (Radius + k_SurfaceGap);

            if (Sticky)
            {
                // 粘住：静止标记会同时关掉基类 Move()(含重力)，否则每帧往地里钻再被拉回来，表现为抖动
                m_Velocity = Vector3.zero;
                m_Resting = true;
                // 记下相对命中节点的位姿 ⇒ Move() 里每帧还原，贴在移动单位上才不会掉队
                m_StickPivot = ResolveStickPivot(hit.collider);
                m_StickLocalPos = m_StickPivot.InverseTransformPoint(transform.position);
                m_StickLocalRot = Quaternion.Inverse(m_StickPivot.rotation) * transform.rotation;
            }
            else
            {
                m_Bounced++;
                if (m_Bounced > MaxBounce)
                {
                    // 弹够了就趴下：停在命中点等引信
                    m_Velocity = Vector3.zero;
                    m_Resting = true;
                }
                else
                {
                    m_Velocity = Vector3.Reflect(m_Velocity, normal) * BounceDamping;
                    if (m_Velocity.magnitude < RestSpeed)
                    {
                        // 静止阈值：必须连重力一起关掉。只清零速度的话基类 Move 每帧还往下沉 1~2cm，
                        // 而球扫对"起点已重叠"是瞎的 ⇒ 慢慢沉进地里（见 RecoverFromOverlap）
                        m_Velocity = Vector3.zero;
                        m_Resting = true;
                    }
                    // 朝向不在这一帧直接甩到新方向：反弹是瞬时的 180° 翻面，低速时看起来就是"抽一下"。
                    // 交给 FaceVelocity 限速转过去（低于 SpinMinSpeed 干脆不转）。
                }
            }

            // ⚠ 远端那颗"表现弹"是同一颗预制体（只摘了 OnHit 上的伤害，见 WeaponBaseController.SpawnVisualBullet）
            //   ⇒ 逻辑侧必须按 IsVisualBullet 分流，否则两端各结算一次（SustainedEffect 的 tick 是绕过 OnHit 的）。
            OnCollisionView?.Invoke();
            if (!IsVisualBullet) OnCollisionLogic?.Invoke();
        }
    }
}
