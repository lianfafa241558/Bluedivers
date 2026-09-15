using FPSGame.UI;
using TMPro;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static WndTools.WndRootTool;
using static WndManager;
using Core;

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
public class VehicleUI : MonoBehaviour
{
    [SerializeField]
    GameObject contGo;

    [SerializeField]
    Animator anim;

    //[InspectorName("鐢诲竷")]
    public CanvasGroup Canvas;

    public DynamicBar weaponMainBar;
    public DynamicBar weaponSecBar;

    public TextMeshProUGUI weaponMainText;
    public TextMeshProUGUI weaponSecText;

    public Image weaponMainIcon;
    public Image weaponSecIcon;

    IVehicleUIController controller;

    /// <summary>
    /// 载具UI是否应该显示（由载具状态驱动）
    /// </summary>
    private bool _shouldShow;

    private void Awake()
    {
        //涓嶇劧alpha浼氳烦鍥炲幓
        //鎴戞病鎷涗簡
        //anim.enabled = false;
        //Canvas.alpha = 0;
        //anim.enabled = true;

        controller = contGo.GetComponent<IVehicleUIController>();
        if (controller == null) return;
        controller.SetWeaponState += SetWeaponState;
        controller.OnFillChange += FillChange;
        controller.OnColorChange += ColorChange;
        controller.OnStateChange += StateChange;
        controller.OnTextChange += TextChange;
        controller.OnIconChange += IconChange;

        OnWindowStateChange += OnWindowStateChangeHandler;
    }

    private void OnDestroy()
    {
        if (controller == null) return;
        controller.SetWeaponState -= SetWeaponState;
        controller.OnFillChange -= FillChange;
        controller.OnColorChange -= ColorChange;
        controller.OnStateChange -= StateChange;
        controller.OnTextChange -= TextChange;
        controller.OnIconChange -= IconChange;
        controller = null;

        OnWindowStateChange -= OnWindowStateChangeHandler;
    }

    /// <summary>
    /// 窗口状态变化时控制载具UI的显隐
    /// 进入UI/自由相机界面时隐藏，退出时用alpha在一秒内恢复
    /// </summary>
    private void OnWindowStateChangeHandler(WindowStateEnum oldState, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.UI:
            case WindowStateEnum.FreeCamera:
                // 进入UI或自由相机时立即隐藏
                SetAlpha(Canvas, 0);
                break;
            case WindowStateEnum.Game:
                // 退出UI/自由相机时渐入恢复
                if (_shouldShow)
                {
                    float targetAlpha = ArchiveSvc.GetSetting("沉浸模式") > 0 ? 0 : 1;
                    if (targetAlpha > 0)
                    {
                        SetAlpha(Canvas, 0, targetAlpha, 1000);
                    }
                }
                break;
        }
    }

    void ColorChange(bool isMain, Color color)
    {
        (isMain ? weaponMainBar : weaponSecBar).SetColor(color);
    }
    void FillChange(bool isMain, float value)
    {
        (isMain ? weaponMainBar : weaponSecBar).SetFill(value);
    }
    void StateChange(bool state)
    {
        _shouldShow = state;
        anim.SetBool(Constants.k_AnimIsActiveParameter, state);

        if (WindowState == WindowStateEnum.Game)
        {
            Canvas.alpha = ArchiveSvc.GetSetting("沉浸模式") > 0 ? 0 : 1;
        }
    }
    void TextChange(bool isMain, string text)
    {
        (isMain ? weaponMainText : weaponSecText).SetText(text);
    }
    void IconChange(bool isMain, Sprite icon)
    {
        (isMain ? weaponMainIcon : weaponSecIcon).sprite = icon;
    }
    void SetWeaponState(bool isMain, bool state)
    {
        var go = (isMain ? weaponMainBar : weaponSecBar).transform.parent;
        if (go) go.gameObject.SetActive(state);
    }
}
