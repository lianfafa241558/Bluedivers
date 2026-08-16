using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 激光制导火箭弹：每帧从屏幕中心发射射线，射线命中的世界坐标点作为目标点，
    /// 弹体持续带转弯速度转向该目标点飞行（准星指哪飞哪，实时跟随）。
    /// 无玩家相机来源时退化为普通标准子弹。
    /// </summary>
    [AddComponentMenu("子弹/玩家制导子弹", 30)]
    public class ProjectilePlayerHoming : ProjectileStandard
    {
        [Header("制导")]
        [Tooltip("转弯速度(度/秒)，越大转弯越灵活")]
        [InspectorName("转弯速度(度/秒)")]
        public float TurnSpeed = 60f;

        [Tooltip("发射后延迟开始追踪的时间")]
        [InspectorName("开始追踪延迟")]
        public float TrackDelay = 0f;

        [Tooltip("从屏幕中心发射射线所用的碰撞层级")]
        [InspectorName("射线层级")]
        public LayerMask RayLayers = Physics.DefaultRaycastLayers;

        /// <summary>玩家镜头相机（持续采样目标点用）</summary>
        Camera m_Camera;
        /// <summary>当前锁定的目标世界坐标点</summary>
        Vector3 m_TargetPoint;
        float m_StartTime;

        protected override void _OnShoot()
        {
            base._OnShoot();

            // 仅玩家手持武器发射时才有镜头中心作为制导源
            PlayerWeaponsManager playerWeaponsManager = Owner ? Owner.GetComponent<PlayerWeaponsManager>() : null;
            m_Camera = playerWeaponsManager && playerWeaponsManager.WeaponCamera ? playerWeaponsManager.WeaponCamera : null;

            m_StartTime = Time.time;
        }

        protected override void Update()
        {
            if (m_isStop) return;

            // 延迟结束后每帧重新从屏幕中心采样目标点，并持续转向
            if (m_Camera && Time.time - m_StartTime >= TrackDelay)
            {
                // 从屏幕中心(视口0.5,0.5)发射射线采样目标点
                Ray ray = m_Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                float rayDistance = MaxRange > 0 ? MaxRange : 500f;
                if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, RayLayers, QueryTriggerInteraction.Collide))
                {
                    m_TargetPoint = hit.point;
                }
                else
                {
                    // 未命中则把镜头前方 rayDistance 处的点作为目标，弹体仍会朝前方转向
                    m_TargetPoint = ray.origin + ray.direction * rayDistance;
                }

                Vector3 toTarget = m_TargetPoint - Root.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    float maxRadians = TurnSpeed * Mathf.Deg2Rad * Time.deltaTime;
                    // 只转向、不改变速度大小；方向与目标方向夹角为0时保持原速
                    Vector3 newDir = Vector3.RotateTowards(m_Velocity.normalized, toTarget.normalized, maxRadians, 1f);
                    m_Velocity = newDir * m_Velocity.magnitude;
                }
            }

            base.Update();
        }
    }
}
