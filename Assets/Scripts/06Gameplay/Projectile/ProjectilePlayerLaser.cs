using System.Collections;
using System.Collections.Generic;

using UnityEngine;
namespace FPSGame.Gameplay
{
    /// <summary>
    /// 玩家激光子弹
    /// </summary>
    [AddComponentMenu("子弹/玩家激光子弹", 30)]
    public class ProjectilePlayerLaser : ProjectileLaser
    {

        [Header("运动")]
        [Tooltip("投射物将修正其轨迹以适应预期轨迹的距离（用于将投射物漂移到第一人称视图的屏幕中心）。在值小时，不进行修正")]
        [InspectorName("轨迹修正距离")]
        public float TrajectoryCorrectionTime = -1;

        [Tooltip("朝准心掰的最大角度（度）。第三人称枪口离屏幕中心很远，不设上限时贴脸/抬枪会把光束甩成直角")]
        [InspectorName("最大修正角度")]
        public float MaxCorrectionAngle = 20f;

        [Tooltip("准心点比这更近就不改方向（贴在墙里/脚下时那个方向没有意义）")]
        [InspectorName("最小修正距离")]
        public float MinCorrectionDistance = 1.5f;


        protected override void Update()
        {
            if (TrajectoryCorrectionTime >= 0f) CorrectTrajectory();
            base.Update();
        }

        /// <summary>
        /// 让光束始终跟着枪管，只在"武器真的在朝屏幕中心打"时掰向准心（角度有上限）。
        /// <para>⚠ 不能无条件锁准心：第三人称未瞄准时角色朝向跟着移动方向/镜头各走各的
        /// （<c>PlayerWeaponsManager.UpdateWeaponThirdPersonAim</c> 只在这时注入 <c>ThirdPersonAimTarget</c>），
        /// 光束硬锁准心就成了"枪指东、光打北"（2026-11-02 用户截图）。</para>
        /// </summary>
        private void CorrectTrajectory()
        {
            // Muzzle.forward 就是武器的射击方向（WeaponController.GetShotDirectionWithinSpread 用的就是它）
            Vector3 barrel = Muzzle ? Muzzle.forward : transform.forward;
            Vector3 dir = barrel;
            float t = 1f;

            // ⚠ 必须取**射手**的准心点：原来读 `FpsHelper.PlayerCameraLookPoint`（语义 = "**本机玩家**的点"）
            //   ⇒ 联机时远端那颗表现弹会朝"我的准心"修正方向（2026-10-13 修）。
            //   `TryGetAimPoint` 会自己分流：本机玩家 = 自己的相机；盟友实例 = 用已同步姿态本地重算。
            if (ShouldLockCrosshair() && FpsHelper.TryGetAimPoint(Owner, out var pos, 500f, 99f))
            {
                Vector3 want = pos - transform.position;
                float dis = want.magnitude;
                if (dis >= MinCorrectionDistance)
                {
                    dir = Vector3.RotateTowards(barrel, want / dis, MaxCorrectionAngle * Mathf.Deg2Rad, 0f);
                    // 沿用原手感：够远直接对齐，近处按 TrajectoryCorrectionTime 平滑（0 = 立即对齐）
                    t = TrajectoryCorrectionTime > 0f ? Mathf.Clamp01(dis / TrajectoryCorrectionTime) : 1f;
                }
            }

            transform.forward = Vector3.Slerp(transform.forward, dir, t);
        }

        /// <summary>
        /// 现在该不该把光束掰向屏幕中心。第一人称相机贴着头/枪口，偏折看不出来 ⇒ 照旧；
        /// 第三人称只有瞄准时枪口才与屏幕中心同向（其余时候武器的射击方向就是枪管朝向）。
        /// </summary>
        private bool ShouldLockCrosshair()
        {
            var pc = Owner ? Owner.GetComponent<PlayerController>() : null;
            if (pc == null || !pc.IsThirdPerson) return true;

            var weapons = pc.WeaponsManager;
            return weapons != null && weapons.IsAiming;
        }

    }
}