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

        protected override void TryHit()
        {
            if (m_Resting || m_isStop) return;

            Vector3 displacement = Tip.position - m_LastRootPosition;
            float length = displacement.magnitude;
            if (length < 0.0001f) return;

            RaycastHit[] hits = Physics.SphereCastAll(m_LastRootPosition, Radius, displacement / length, length,
                FpsHelper.GetHittableLayers(m_Velocity.magnitude), QueryTriggerInteraction.Collide);

            RaycastHit closest = default;
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hits.Length; ++i)
            {
                if (!IsBounceValid(hits[i]) || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
                closest = hits[i];
                found = true;
            }

            if (found) Collide(closest);
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
            transform.position = hit.point + normal * Radius;

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
                    if (m_Velocity.magnitude < RestSpeed) m_Velocity = Vector3.zero;
                    if (m_Velocity.sqrMagnitude > 0.0001f) transform.forward = m_Velocity.normalized;
                }
            }

            // ⚠ 远端那颗"表现弹"是同一颗预制体（只摘了 OnHit 上的伤害，见 WeaponBaseController.SpawnVisualBullet）
            //   ⇒ 逻辑侧必须按 IsVisualBullet 分流，否则两端各结算一次（SustainedEffect 的 tick 是绕过 OnHit 的）。
            OnCollisionView?.Invoke();
            if (!IsVisualBullet) OnCollisionLogic?.Invoke();
        }
    }
}
