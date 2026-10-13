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


        protected override void Update()
        {
            if (TrajectoryCorrectionTime>=0) {
                // ⚠ 必须取**射手**的准心点：原来读 `FpsHelper.PlayerCameraLookPoint`（语义 = "**本机玩家**的点"）
                //   ⇒ 联机时远端那颗表现弹会朝"我的准心"修正方向（2026-10-13 修）。
                //   `TryGetAimPoint` 会自己分流：本机玩家 = 自己的相机；盟友实例 = 用已同步姿态本地重算。
                if (FpsHelper.TryGetAimPoint(Owner, out var pos, 500f, 99f))
                {
                    var dir = (pos - transform.position).normalized;
                    var dis = Vector3.Distance(pos, transform.position);
                    transform.forward = Vector3.Slerp(transform.forward, dir, dis / TrajectoryCorrectionTime);
                }
            }
            base.Update();
        }

    }
}