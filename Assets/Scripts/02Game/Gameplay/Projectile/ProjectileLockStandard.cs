
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 智能武器锁头
    /// </summary>
    [AddComponentMenu("子弹/锁定子弹", 30)]
    public class ProjectileLockStandard : ProjectileStandard
    {

        [Header("运动")]

        [InspectorName("转向速度")]
        public float SteeringSpeed = 5;

        private Transform target;
        public void SetTarget(Transform target)
        {
            this.target = target;
        }

        protected override void Update()
        {
            if (m_isStop) return;
            if (target)
            {
                var tick = Time.time - m_lastTime;
                m_Velocity = Vector3.Lerp(m_Velocity, (target.position - transform.position).normalized * m_Velocity.magnitude, SteeringSpeed * tick);
            }
            base.Update();
        }

    }
}