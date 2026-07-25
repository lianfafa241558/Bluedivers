using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameContract
{


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
            Destroy(this);  // 配置完立即销毁
        }
    }


    public static class LayerDefinition
    {
        /// <summary>高速子弹碰撞层</summary>
        public static LayerMask HittableHighSpeedLayers { get; set; }

        /// <summary>子弹碰撞层</summary>
        public static LayerMask HittableLayers { get; set; }
        /// <summary>单位可见层</summary>
        public static LayerMask UnitSeeLayers { get; set; }
        /// <summary>玩家移动碰撞层</summary>
        public static LayerMask MoveableLayers { get; set; }

        /// <summary>地面层</summary>
        public static LayerMask GroundLayers { get; set; }
        /// <summary>单位层</summary>
        public static LayerMask UnitLayers { get; set; }

        /// <summary>武器层</summary>
        public static LayerMask WeaponLayers { get; set; }
        /// <summary>空气墙层</summary>
        public static LayerMask AirWallLayers { get; set; }

        /// <summary>烟雾层</summary>
        public static LayerMask SmokeLayers { get; set; }

        /// <summary>第一人称忽略层</summary>
        public static LayerMask FirstPersonIgnoreLayers { get; set; }

    }

}
