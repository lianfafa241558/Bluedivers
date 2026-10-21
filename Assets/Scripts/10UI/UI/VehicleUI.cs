using FPSGame.UI;
using TMPro;

using UnityEngine;
using UnityEngine.UI;
using FPSGame.Managers;

namespace FPSGame.UI
{
using FPSGame.Game;
using static FPSGame.WndTools.WndRootTool;
using static FPSGame.Managers.WndManager;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.GameData;
using FPSGame.Gameplay;
/// <summary>
/// 载具 HUD 与武器信息显示。
/// </summary>
[AddComponentMenu("UI/HUD/载具 UI")]
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

    /// <summary>绑定对象是背包时的引用（背包预制体里自带一份本 HUD，见下面的显隐门）。载具则为 null。</summary>
    private BagBase _bag;

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
        _bag = controller as BagBase;
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
            case WindowStateEnum.Airdrop:   // ★ 同 Game：Airdrop 只是叠加在 HUD 上的战备菜单，载具UI照旧要显示
                                            //   （否则"开战备菜单 → 开设置 → 关设置"之后载具UI一直是 0 透明度）
            case WindowStateEnum.Game:
                // 退出UI/自由相机时渐入恢复
                if (_shouldShow)
                {
                    float targetAlpha = ArchivesData_SO.Current.GetSetting("沉浸模式") > 0 ? 0 : 1;
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
        // 背包自带的这块 HUD 是**本人 HUD**（屏幕空间叠加画布就挂在背包身上）。联机时同一件背包会被
        // 远端重放装到盟友实例上 ⇒ 不挡就是"队友挨打、我的屏幕上亮一条"（2026-10-21 实测）。载具（_bag == null）不受影响。
        if (_bag != null && !_bag.IsLocalDriver) state = false;

        _shouldShow = state;
        anim.SetBool(Constants.k_AnimIsActiveParameter, state);

        if (WindowState == WindowStateEnum.Game)
        {
            Canvas.alpha = ArchivesData_SO.Current.GetSetting("沉浸模式") > 0 ? 0 : 1;
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
}
