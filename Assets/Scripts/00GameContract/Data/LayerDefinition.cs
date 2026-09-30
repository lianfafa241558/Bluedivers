using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 层级定义（跨层共享的 LayerMask 配置容器）。
    ///
    /// <para>▍为什么留在契约层：被 <c>06_Gameplay</c>（25 处）与 <c>Effect</c> 层（5 处）共见，
    /// 而这两层之间是"玩法层在 Effect 层之下"的关系，把它放进玩法层会让 Effect 层反向依赖；
    /// 且它只是 <c>LayerMask</c> 值容器的静态载体。</para>
    ///
    /// <para>▍谁给它填值：Unity 侧的配置引导器 <c>LayerConfigInitializer</c>
    /// （2026-10-01 **从本文件拆出并迁往 `01Manager/`**——契约层不得出现 <c>MonoBehaviour</c>／<c>[SerializeField]</c>／<c>AddComponentMenu</c>）。</para>
    ///
    /// <para>⚠ 成员是 <c>static</c> 且有 setter（全局可变状态）：由引导器在 <c>Awake</c> 一次性注入、
    /// 之后只读 ⇒ 定为可接受的折衷（改成构造注入需改场景/prefab 结构，收益不成比例）。</para>
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
