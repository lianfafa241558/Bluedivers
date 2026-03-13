using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameContract
{
    public static class LayerDefinition
    {
        /// <summary>高速子弹碰撞层</summary>
        public static LayerMask HittableHighSpeedLayers { get; set; }

        /// <summary>子弹碰撞层</summary>
        public static LayerMask HittableLayers { get; set; }

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

    }

}
