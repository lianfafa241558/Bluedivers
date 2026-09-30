using System;
using FPSGame.Core;

using UnityEngine;
using UnityEngine.Events;
using FPSGame.Data;
using FPSGame.Audio;
using FPSGame.Gameplay;

namespace FPSGame.Managers
{


/// <summary>
/// 窗口状态机与窗口引用中心。
/// </summary>
[AddComponentMenu("管理/窗口管理器")]
public class WndManager : Singleton<WndManager>, FPSGame.GameContract.IWindowService
{
    public static WindowStateEnum WindowState
    {
        get => Instance ? Instance.windowState : WindowStateEnum.Game;
        set
        {
            var oldState = Instance.windowState;
            Instance.windowState = value;
            OnWindowStateSet?.Invoke(oldState, value);
            if (oldState != value)
            {
                OnWindowStateChange?.Invoke(oldState, value);
            }
        }
    }
    /// <summary>WindowState 设置时无条件触发（oldState==value 也触发），用于窗口间感知彼此的 UI 状态变更</summary>
    public static event UnityAction<WindowStateEnum, WindowStateEnum> OnWindowStateSet;
    /// <summary>WindowState 改变时触发（oldState!=value）</summary>
    public static event UnityAction<WindowStateEnum, WindowStateEnum> OnWindowStateChange;

    [InspectorName("界面状态")]
    [SerializeField]
    private WindowStateEnum windowState = WindowStateEnum.UI;

    // ⚠ 2026-10-01 起，10 个**具体窗口字段**（operationWnd/tipWnd/noticeWnd/selectMapWnd/selectRoleWnd/
    //   guideWnd/vehicleWnd/airdropConfigWnd/settingWnd/countDownWnd）**已删除**：
    //   它们是 04UI 的类型，被本类（01Manager）持有 ⇒「管理器 → UI」反向依赖，`09_Managers`/`10_UI` 切不出来。
    //   现在：窗口在 `Window.Awake` 里自注册到 UI 侧 `WndHub`（另有 `WndHub.Scan()` 兜底未激活窗口），
    //   `WndHub` 把能力以**委托**登记进契约层 `FPSGame.GameContract.WindowRegistry`，本类的动词转调它。
    //   见 00GameContract/WindowRegistry.cs 与 04UI/WndHub.cs。

    // FPSGame.GameContract.IWindowService：以下成员在原类里是**静态**的 ⇒ 无法隐式实现接口，故显式转发
    WindowStateEnum FPSGame.GameContract.IWindowService.WindowState => WindowState;
    event UnityAction<WindowStateEnum, WindowStateEnum> FPSGame.GameContract.IWindowService.OnWindowStateChange
    {
        add => OnWindowStateChange += value;
        remove => OnWindowStateChange -= value;
    }

    public Sprite empty;

    public override void Awake()
    {
        base.Awake();
        FPSGame.GameContract.ServiceLocator.Wnd = this;//注册窗口服务：供玩法层等下层访问（见 ServiceLocator.cs）

        OnWindowStateChange += OnWindowStateChangeHandler;
        GlobalEventSub.OnSettingCange += OnSettingCange;
    }

    protected void Start()
    {
        // 界面状态/游戏状态已迁移到 WndManager/GameStateManager
    }

    void OnDestroy()
    {
        OnWindowStateChange -= OnWindowStateChangeHandler;
        GlobalEventSub.OnSettingCange -= OnSettingCange;
    }

    private void OnWindowStateChangeHandler(WindowStateEnum oldState, WindowStateEnum state)
    {
        switch (state)
        {
            case WindowStateEnum.Game:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                break;
            case WindowStateEnum.UI:
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

                break;
            case WindowStateEnum.Airdrop:

                break;
        }
    }

    private void OnSettingCange(string key, float value)
    {
        if (key == "显示模式")
        {
            switch ((int)value)
            {
                case 0: Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true); break;
                case 1: Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, false); break;
                case 2: Screen.SetResolution(1920, 1080, false); break;
            }
        }
        if (key == "UI缩放系数")
        {
            for(var i=0;i< transform.childCount;++i)
            {
                transform.GetChild(i).GetComponent<UnityEngine.UI.CanvasScaler>().scaleFactor = value / 100f;
            }
           
        }
    }

    /// <summary>启动倒计时窗（转调契约注册表；UI 未就绪时静默跳过）。</summary>
    public void CreatCountDown(Func<int> func, CountDownTypeEnum type)
    {
        FPSGame.GameContract.WindowRegistry.CountDown(func, type);
    }

    // ⚠ `CreatTip(TipWndInfo)` 已删除：参数类型 `TipWndInfo` 属 04UI，而调用方全在 UI 层
    //   ⇒ 直接写 `WndHub.Tip.Creat(info)` 即可（原 7 处已由 p6_wnd_sweep.py 改写）。

    /// <summary>弹一条 NPC 提示（转调契约注册表；UI 未就绪时静默跳过）。</summary>
    public void CreatNotice(string role, string type, System.Func<bool> func = default,float vaildTime=-1)
    {
        FPSGame.GameContract.WindowRegistry.Notice(role, type, func, vaildTime);
    }

    /// <summary>清掉当前 NPC 提示（转调契约注册表）。</summary>
    public void ClearNotice()
    {
        FPSGame.GameContract.WindowRegistry.Clear();
    }
    
    //public void CreatSpeech(NoticeData_SO data, System.Func<bool> func = default)
    //{
    //    subtitleWnd.Creat(data, func);
    //}

    public void PlaySound(AudioPlayInfo info)
    {
        AudioSvc.PlaySound(info);
    }

    public void PlaySoundData(RuntimeSoundData group)
    {
        AudioSvc.PlaySound(group);
    }

    /// <summary>
    /// <c>FPSGame.GameContract.IWindowService</c> 实现：按枚举开关窗口，转调契约注册表
    /// <see cref="FPSGame.GameContract.WindowRegistry"/>（由 UI 侧 <c>WndHub</c> 填充）。
    ///
    /// <para>▍为什么是"枚举 + 委托"而不是字段：具体窗口类型（<c>SelectRoleWnd</c>/<c>GuideWnd</c>…）属 `04UI`，
    /// 本类（01Manager）持有它们就构成「管理器 → UI」的反向依赖 ⇒ `09_Managers`/`10_UI` 都切不出来。
    /// 现在 UI 类型**只出现在 `04UI` 内**，本文件零 UI 类型。</para>
    ///
    /// <para>▍行为说明：UI 尚未就绪 / 没这个窗口 ⇒ 静默跳过（原字段版是 null 跳过，语义一致）。
    /// ⚠ 新增窗口只改 <c>04UI/WndHub.TypeOf</c> 一处。</para>
    /// </summary>
    public void SetWndState(FPSGame.GameContract.WndTypeEnum type, bool isActive = true)
    {
        FPSGame.GameContract.WindowRegistry.SetState(type, isActive);
    }

    /// <summary>窗口是否已打开（转调契约注册表；未登记返回 false）。</summary>
    public bool IsWndOpen(FPSGame.GameContract.WndTypeEnum type)
    {
        return FPSGame.GameContract.WindowRegistry.GetOpen(type);
    }
}
}
