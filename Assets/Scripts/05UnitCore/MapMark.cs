using FPSGame.Core.Interface;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 一条「世界关注物」小地图标记：位置与身份来自 <see cref="Entity"/>，图标可另行指定。
    /// <para>▍为什么要单独带 <see cref="Icon"/>：武器 prefab 的 <c>BaseObject.ExtraPortrait</c> 与
    /// <c>WeaponPlayerController.WeaponIcon</c> 不保证同一张图，界面侧不该猜用哪张 ⇒ 登记时就说清楚。</para>
    /// </summary>
    public readonly struct MapMark
    {
        /// <summary>位置 / 身份的来源（必是挂在 MonoBehaviour 上的实体，否则无法判销毁）。</summary>
        public readonly IEntity Entity;

        /// <summary>图标；为 null 时界面退化为 <c>Entity.ExtraPortrait</c>。</summary>
        public readonly Sprite Icon;

        public MapMark(IEntity entity, Sprite icon)
        {
            Entity = entity;
            Icon = icon;
        }
    }
}
