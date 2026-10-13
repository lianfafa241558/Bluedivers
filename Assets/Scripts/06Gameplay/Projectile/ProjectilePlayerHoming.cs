using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
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

            // 延迟结束后每帧重新采样目标点，并持续转向
            if (Time.time - m_StartTime >= TrackDelay)
            {
                float rayDistance = MaxRange > 0 ? MaxRange : 500f;
                if (TrySampleAimPoint(rayDistance, out m_TargetPoint))
                {
                    // ★ 制导弹的"表现终点"要跟着**实时**准心走（2026-10-13）：射手开火后还在引导 ⇒
                    //   开火那一刻的落点早过期了；远端那颗表现弹靠它才不至于一路飞到射程尽头。
                    //   ⚠ 只给表现弹刷：真人子弹的 OnHit 上挂着伤害，凭空补终点会在半路结算（见 ProjectileBase.IsVisualBullet）。
                    if (IsVisualBullet) SetVisualEndPoint(m_TargetPoint);

                    Vector3 toTarget = m_TargetPoint - Root.position;
                    if (toTarget.sqrMagnitude > 0.0001f)
                    {
                        float maxRadians = TurnSpeed * Mathf.Deg2Rad * Time.deltaTime;
                        // 只转向、不改变速度大小；方向与目标方向夹角为0时保持原速
                        Vector3 newDir = Vector3.RotateTowards(m_Velocity.normalized, toTarget.normalized, maxRadians, 1f);
                        m_Velocity = newDir * m_Velocity.magnitude;
                    }
                }
            }

            base.Update();
        }

        /// <summary>
        /// 本帧的制导目标点。两条来源（取不到 ⇒ false = 不制导，保持原"直线飞"行为）：
        /// <list type="number">
        ///   <item>本机玩家：开火那一刻缓存下来的 <see cref="m_Camera"/> 的屏幕中心射线（逐字保留原行为）；</item>
        ///   <item>远端盟友：盟友实例**没有相机** ⇒ 用它已同步的姿态（yaw + pitch）本地重算，
        ///         见 <c>FpsHelper.TryGetAimPoint</c>。⚠ 这一条就是"客机的制导火箭弹不同步"的修法：
        ///         原来取不到相机就整段跳过制导（且远端连终点都没有），表现弹会直线飞到射程尽头。</item>
        /// </list>
        /// </summary>
        bool TrySampleAimPoint(float rayDistance, out Vector3 point)
        {
            point = Vector3.zero;

            if (m_Camera != null)
            {
                // 从屏幕中心(视口0.5,0.5)发射射线采样目标点
                Ray ray = m_Camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, FpsHelper.GetHittableLayers(m_Velocity.magnitude), QueryTriggerInteraction.Collide))
                {
                    point = hit.point;
                }
                else
                {
                    // 未命中则把镜头前方 rayDistance 处的点作为目标，弹体仍会朝前方转向
                    point = ray.origin + ray.direction * rayDistance;
                }
                return true;
            }

            return FpsHelper.TryGetAimPoint(Owner, out point, rayDistance, m_Velocity.magnitude);
        }
    }
}
