using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;
namespace Unity.FPS.Gameplay
{
    /// <summary>
    /// 玩家激光子弹
    /// </summary>
    public class ProjectilePlayerLaser : ProjectileLaser
    {

        [Header("运动")]
        [Tooltip("投射物将修正其轨迹以适应预期轨迹的距离（用于将投射物漂移到第一人称视图的屏幕中心）。在值小于0时，不进行修正")]
        [CustomLabel("轨迹修正距离")]
        public float TrajectoryCorrectionTime = -1;


        protected override void Update()
        {
            if (TrajectoryCorrectionTime>=0) {
                Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
                if (Physics.Raycast(ray, out RaycastHit hit, 300, FpsHelper.GetHittableLayers(999)))
                {
                    var dir = (hit.point - transform.position).normalized;
                    var dis = Vector3.Distance(hit.point, transform.position);
                    transform.forward = Vector3.Slerp(transform.forward, dir, dis / TrajectoryCorrectionTime);
                }
            }


            base.Update();
        }

    }
}