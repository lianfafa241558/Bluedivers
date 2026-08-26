using System.Collections;
using System.Collections.Generic;

using UnityEngine;
namespace Unity.FPS.Gameplay
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
                var pos=FpsHelper.PlayerCameraLookPoint;
                var dir = (pos - transform.position).normalized;
                var dis = Vector3.Distance(pos, transform.position);
                transform.forward = Vector3.Slerp(transform.forward, dir, dis / TrajectoryCorrectionTime);
            }
            base.Update();
        }

    }
}