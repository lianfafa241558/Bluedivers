using Unity.BaseTool;
using UnityEngine;

namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 标准子弹
    /// </summary>
    public class ProjectilePlayerStandard : ProjectileStandard
    {

        [Header("运动")]
        [Tooltip("投射物将修正其轨迹以适应预期轨迹的距离（用于将投射物漂移到第一人称视图的屏幕中心）。在值小于0时，不进行修正")]
        [CustomLabel("轨迹修正时间")]
        public float TrajectoryCorrectionTime = -1;

        /// <summary> 需要轨迹矫正(只有玩家武器且矫正系数！=-1) </summary>
        bool m_HasTrajectoryOverride;
        Vector3 m_TrajectoryCorrectionVector;
        /// <summary>还没有矫正完成时的矫正朝向</summary>
        Vector3 m_ConsumedTrajectoryCorrectionVector;

        protected override void _OnShoot()
        {

            base._OnShoot();
            //忽略玩家自己的碰撞箱
            //Collider[] ownerColliders = Owner.GetComponentsInChildren<Collider>();
            //if (ownerColliders.IsValid()) m_IgnoredColliders.AddRange(ownerColliders);

            //处理玩家射击的情况（使投射物不穿过墙壁，并记住屏幕轨迹的中心）
            PlayerWeaponsManager playerWeaponsManager = Owner.GetComponent<PlayerWeaponsManager>();
            if (playerWeaponsManager)
            {
                m_HasTrajectoryOverride = true;

                Vector3 cameraToMuzzle = (InitialPosition - playerWeaponsManager.WeaponCamera.transform.position);

                m_TrajectoryCorrectionVector = Vector3.ProjectOnPlane(-cameraToMuzzle,
                    playerWeaponsManager.WeaponCamera.transform.forward);
                //立即将子弹修正到屏幕中央位置(这太蠢了)
                if (TrajectoryCorrectionTime == 0)
                {
                    transform.position += m_TrajectoryCorrectionVector;
                    m_ConsumedTrajectoryCorrectionVector = m_TrajectoryCorrectionVector;
                }

                else if (TrajectoryCorrectionTime < 0)
                {
                    m_HasTrajectoryOverride = false;
                }
            }
            /*
            //好像是防止穿墙的
            if (Physics.Raycast(playerWeaponsManager.WeaponCamera.transform.position, cameraToMuzzle.normalized,
                out RaycastHit hit, cameraToMuzzle.magnitude, HittableLayers, k_TriggerInteraction))
            {
                //如果一开始就能命中
                if (IsHitValid(hit))
                {
                    OnHit?.Invoke(hit.point, hit.normal, hit.collider);
                }
            }*/
        }

        protected override void Update()
        {
            if (m_isStop) return;
            //这个还不是制导，只是偏移
            //向轨迹超控方向漂移（这是为了使弹丸能够居中
            //即使实际武器偏移，也要以相机中心为准）
            if (m_HasTrajectoryOverride && m_ConsumedTrajectoryCorrectionVector.sqrMagnitude <
                m_TrajectoryCorrectionVector.sqrMagnitude)
            {
                Vector3 correctionLeft = m_TrajectoryCorrectionVector - m_ConsumedTrajectoryCorrectionVector;
                float distanceThisFrame = (Root.position - m_LastRootPosition).magnitude;
                Vector3 correctionThisFrame =
                    (distanceThisFrame / (TrajectoryCorrectionTime * WeaponBase.CurrentSpeed)) * m_TrajectoryCorrectionVector;
                correctionThisFrame = Vector3.ClampMagnitude(correctionThisFrame, correctionLeft.magnitude);
                m_ConsumedTrajectoryCorrectionVector += correctionThisFrame;

                // 检测校正结束
                if (m_ConsumedTrajectoryCorrectionVector.sqrMagnitude == m_TrajectoryCorrectionVector.sqrMagnitude)
                {
                    m_HasTrajectoryOverride = false;
                }

                transform.position += correctionThisFrame;
            }
            base.Update();
        }

    }
}