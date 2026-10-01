using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 层级定义（跨层共享的 LayerMask 配置容器）。
    /// <para>▍谁给它填值：Unity 侧的配置引导器 <c>LayerConfigInitializer</c>
    /// </summary>
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
