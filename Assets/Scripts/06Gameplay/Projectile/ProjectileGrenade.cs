using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 手雷：撞到东西**不结算伤害、也不消失**，而是按法线弹跳衰减；
    /// 真正的爆点由基类的引信（<see cref="DamageData.MaxLifeTime"/> 秒，经 <c>DelayedRelese</c>）决定，
    /// 到点在**当前位置**结算一次范围伤害。所以伤害配置里 <c>MaxRange</c> 要留 -1（别用米制自爆引信）。
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

        /// <summary>本次投掷已弹跳次数</summary>
        private int m_Bounced;

        /// <summary>已趴下：不再受重力（否则每帧往地里钻再被弹回来，表现为抖动）</summary>
        private bool m_Resting;

        protected override void _OnShoot()
        {
            base._OnShoot();
            // 池化复用：上一颗的弹跳/静止状态必须清掉
            m_Bounced = 0;
            m_Resting = false;
        }

        protected override void Move()
        {
            if (m_Resting) return;
            base.Move();
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

            if (found) Bounce(closest);
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

        private void Bounce(RaycastHit hit)
        {
            Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -m_Velocity.normalized;
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
            }

            transform.position = hit.point + normal * Radius;
            if (m_Velocity.sqrMagnitude > 0.0001f) transform.forward = m_Velocity.normalized;
        }
    }
}
