using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.GameContract
{


    /// <summary>
    /// 把 Inspector 上配置的 LayerMask 注入 LayerDefinition 静态类，注入完成后自毁。
    /// </summary>
    [AddComponentMenu("框架/层级配置初始化")]
    public class LayerConfigInitializer : MonoBehaviour
    {
        [SerializeField]
        [InspectorName("高速子弹碰撞层")]
        private LayerMask hittableHighSpeedLayers = -1;
        [SerializeField]
        [InspectorName("武器层")]
        private LayerMask weaponLayers = -1;
        [SerializeField]
        [InspectorName("地面层")]
        private LayerMask groundLayers = -1;
        [SerializeField]
        [InspectorName("单位层")]
        private LayerMask unitLayers = -1;
        [SerializeField]
        [InspectorName("空气墙层")]
        private LayerMask airWallLayers = -1;
        [SerializeField]
        [InspectorName("烟雾层")]
        private LayerMask smokeLayers = -1;

        /// <summary>第一人称忽略层</summary>
        [SerializeField]
        [InspectorName("第一人称忽略层")]
        private LayerMask firstPersonIgnoreLayers = -1;


        private void Awake()
        {
            LayerDefinition.HittableHighSpeedLayers = hittableHighSpeedLayers | groundLayers | unitLayers;
            LayerDefinition.HittableLayers = groundLayers | unitLayers;
            LayerDefinition.UnitSeeLayers= groundLayers| smokeLayers;
            LayerDefinition.MoveableLayers = airWallLayers | groundLayers | unitLayers;
            LayerDefinition.UnitLayers = unitLayers;
            LayerDefinition.GroundLayers = groundLayers;
            LayerDefinition.WeaponLayers = weaponLayers;
            LayerDefinition.AirWallLayers = airWallLayers;
            LayerDefinition.SmokeLayers = smokeLayers;
            LayerDefinition.FirstPersonIgnoreLayers = weaponLayers|firstPersonIgnoreLayers;
            Destroy(this);  // 配置完立即销毁
        }
    }
}
