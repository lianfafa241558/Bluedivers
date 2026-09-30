using UnityEngine;
using UnityEngine.Events;

namespace FPSGame.GameContract
{
    public interface IVehicleUIController
    {
        /// <summary>是否是主手，状态</summary>
        UnityAction<bool, bool> SetWeaponState { get; set; }

        UnityAction<bool> OnStateChange { get; set; }
        /// <summary>是否是主手，颜色</summary>
        UnityAction<bool, Color> OnColorChange { get; set; }
        /// <summary>是否是主手，值</summary>
        UnityAction<bool, float> OnFillChange { get; set; }
        /// <summary>是否是主手，文本</summary>
        UnityAction<bool, string> OnTextChange { get; set; }
        /// <summary>是否是主手，图标</summary>
        UnityAction<bool, Sprite> OnIconChange { get; set; }
    }
}
