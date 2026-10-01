using System;
using System.Collections.Generic;
using FPSGame.Core;
using UnityEngine;
using FPSGame.UI;

namespace FPSGame.UI
{
using FPSGame.Gameplay;
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;


/// <summary>
/// UI窗口基类
/// <para>⚠⚠⚠ <b>子类不要定义 `Awake`/`OnEnable`</b> —— 会静默顶掉窗口自注册，
/// 导致该窗口跨层开关失效（不报错、只是偶发打不开）。详见 <see cref="Awake"/> 的 remarks。</para>
/// </summary>


public abstract class Window : MonoBehaviour
{
    protected WndManager wndManager; 
    protected ResSvc resManager;
    protected RoomManager roomManager;
    protected TaskManager taskManager;
    protected PropertyManager propertyManager;
    protected GameRoot root;

    private bool firstInit;

    /// <summary>
    /// 设置窗口显示状态
    /// </summary>
    /// <param name="isActive">是否显示</param>
    public virtual void SetWndState(bool isActive = true)
    {
        if (gameObject.activeSelf != isActive||(isActive&&!firstInit))
        {
            gameObject.SetActive(isActive);
            GlobalEventSub.WndSwitch(gameObject.name, isActive);
            if (isActive)
            {
                if (!firstInit)
                {
                    firstInit = true;
                    wndManager = WndManager.Instance;
                    resManager = ResSvc.Instance;
                    roomManager = RoomManager.Instance;
                    taskManager = TaskManager.Instance;
                    propertyManager = PropertyManager.Instance;
                    root = GameRoot.Instance;
                    FirstShowWnd();
                }
                ShowWnd();
            }
            else
            {
                HideWnd();
            }
        }
    }

    /// <summary>
    /// 窗口自注册（2026-10-01）：把"我是哪个窗口"登记到 UI 侧 <see cref="WndHub"/>，
    /// 由它把"开关窗口/弹提示/倒计时"以委托登记进契约层 <c>WindowRegistry</c>，
    /// 使 <c>01Manager</c> 不必再持有具体窗口字段（切断「管理器 → UI」反向依赖）。
    /// ⚠ 未激活的窗口不会跑本方法 ⇒ <c>WndHub.Scan()</c> 会用
    /// <c>FindObjectsOfType&lt;Window&gt;(true)</c> 兜底。**本类以下不要再定义 `Awake`**（会被顶掉）。
    /// </summary>
    /// <remarks>
    /// <para>⚠⚠⚠【禁令】<b>`Window` 的任何子类都不要再定义 `Awake`（`OnEnable` 同理）</b>。
    /// Unity 的消息方法**只找最派生的那一个** ⇒ 子类的 `Awake` 会**静默顶掉**本方法。
    /// 后果不是编译错误，而是：该窗口**永不注册** ⇒ `WndHub`/`WindowRegistry` 里没有它 ⇒
    /// 跨层 `GlobalEventSub.OpenWnd(WndTypeEnum.X)`（2026-10-01 前是 `ServiceLocator.Wnd.SetWndState`）对它**静默无效**
    /// （`WindowRegistry` 对未注册 key 的语义正是"静默跳过"）⇒ 表现为**"偶发打不开"**，
    /// 只有 `WndHub.Scan()` 的兜底扫描能救回来，而扫描时序不定。</para>
    ///
    /// <para>▍子类要做初始化请改用 <b>`FirstShowWnd()`</b>（首次打开时基类保证调用）或
    /// `ShowWnd()`/`HideWnd()`；确实需要"对象激活即执行"的，把逻辑放进 `FirstShowWnd()`。</para>
    ///
    /// <para>▍⚠ 为什么不能靠编译器强制：Unity 用**反射按名字**调用消息方法，C# 层面
    /// 子类 `private void Awake()` 与基类 `protected virtual void Awake()` **并不冲突、不报错**，
    /// 但 Unity 只会调用最派生的那个 ⇒ **类型系统在这里帮不上忙**，只能靠本注释 + 代码评审。
    /// （若要做机器校验：仿 `ProjectEditors` 加一个"扫全部 `Window` 子类是否声明了
    /// `Awake`/`OnEnable`"的检查，编译后 `LogWarning`。）</para>
    /// </remarks>
    protected virtual void Awake()
    {
        WndHub.SelfRegister(this);
    }

    public virtual void OnDestroy()
    {
        WndHub.SelfUnregister(this);
        HideWnd();
    }

    protected GameStateEnum GameState {
        get=> GameRoot.GameState;
        set => GameRoot.GameState = value;
    }
    protected WindowStateEnum WindowState {
        get => WndManager.WindowState;
        set => WndManager.WindowState = value;
    }
    protected float TimeScale
    {
        get => GameRoot.TimeScale;
        set => GameRoot.TimeScale = value;
    }
    public bool State => gameObject.activeSelf;


    /// <summary>窗口第一次打开时</summary>
    protected abstract void FirstShowWnd();
    /// <summary>窗口打开??/summary>
    protected abstract void ShowWnd();
    /// <summary>窗口关闭??/summary>
    protected abstract void HideWnd();
    /// <summary>
    /// 关闭窗口,仅anim中使用
    /// </summary>
    protected void CloseWnd() => SetWndState(false);

    /// <summary>播放动画</summary>
    /// <param name="name">状态名</param>
    /// <param name="interrupt">中断当前播放</param>
    /// <param name="layer">层级</param>
    protected bool PlayAnim(string name, bool interrupt = false, int layer = 0)
    {
        var anim = GetComponent<Animator>();
        if (anim.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1 || interrupt)
        {
            GetComponent<Animator>().Play(name, layer,0);
            return true;
        }
        return false;
    }

    public static void SetButtonEnter(Transform btn, Action<UnityEngine.EventSystems.PointerEventData> action) => btn.TryGetOrAddComponent<ButtonEnterDetector>().Enter = action;

    public static void SetButtonExit(Transform btn, Action<UnityEngine.EventSystems.PointerEventData> action) => btn.TryGetOrAddComponent<ButtonEnterDetector>().Exit = action;

}

}
