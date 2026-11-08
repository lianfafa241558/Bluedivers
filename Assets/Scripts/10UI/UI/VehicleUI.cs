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

    /// <summary>本 HUD **根**（VehicleCanvas）上的 CanvasGroup。
    /// <para>▍它才是"整块 HUD 看不看得见"的开关：预制体里 <c>alpha = 0</c>，原本指望
    /// <c>Vehicle_Entry</c> 动画把它写成 1、<c>Vehicle_Exit</c> 写回 0。可这两条曲线在运行时**不生效**
    /// （实测把值改成 0.5 再 <c>Animator.Update</c>，子层与根都不回写）⇒ 根组永远是 0 ⇒ HUD 全黑，
    /// 而代码里的 <see cref="Canvas"/>（Root 子组）一直是 1，看 Inspector 还以为没问题。
    /// 现在根组也归 <see cref="RefreshAlpha"/> 管，显隐不再依赖动画（2026-11-08 实测）。</para></summary>
    private CanvasGroup _rootGroup;

    /// <summary>
    /// 载具UI是否应该显示（由载具状态驱动）
    /// </summary>
    private bool _shouldShow;

    private void Awake()
    {
        // 不拿「若动画会写就不管」来省事：动画在这套 prefab 上不写根组（见 _rootGroup 注释），必须自己拿
        _rootGroup = GetComponent<CanvasGroup>();
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
    /// 窗口状态变化时控制载具UI的显隐（进入UI/自由相机隐藏，回到游戏画面恢复）
    /// </summary>
    private void OnWindowStateChangeHandler(WindowStateEnum oldState, WindowStateEnum state)
    {
        RefreshAlpha();
    }

    /// <summary>
    /// 载具 HUD 透明度的**唯一出口**：把"该不该显示"从三份状态现算出来再写一次。
    ///
    /// <para>▍为什么不能各分支各写：显隐同时取决于 ①载具状态 <see cref="_shouldShow"/> ②窗口状态 ③沉浸模式。
    /// 原来 <see cref="StateChange"/> 只在 <c>WindowState==Game</c> 时写 alpha —— 于是"在战备菜单(Airdrop)里上/下载具"
    /// 那一拍根本不写，而 alpha 早已被 UI/自由相机置 0 ⇒ 之后一直是 0，HUD 永远看不见（2026-11-08 实测）。</para>
    ///
    /// <para>⚠ 也别再用 <c>SetAlpha(Canvas, 0, 1, 1000)</c> 这种"先把 0 写上、再等定时器淡入"的写法：
    /// 定时器一旦没跑（<c>viewTimer</c> 为空 / 那一帧对象被禁用）就永远停在 0；而且出场本来就有 Entry 动画。</para>
    /// </summary>
    private void RefreshAlpha()
    {
        // Airdrop 只是**叠加在 HUD 上的战备菜单**，不是"离开游戏画面" ⇒ 与 Game 同口径
        bool inGame = WindowState == WindowStateEnum.Game || WindowState == WindowStateEnum.Airdrop;
        bool immersive = ArchivesData_SO.Current != null && ArchivesData_SO.Current.GetSetting("沉浸模式") > 0;
        float target = _shouldShow && inGame && !immersive ? 1f : 0f;

        // ⚠ 两个组都要写：根组才是总开关（预制体里是 0、动画又不写 ⇒ 只写 Root 子组时 HUD 依然全黑），
        //   Root 子组是老代码一直在写的那一个，留着以免别的路径依赖它。
        if (_rootGroup != null) _rootGroup.alpha = target;
        SetAlpha(Canvas, target);
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
        RefreshAlpha();
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
