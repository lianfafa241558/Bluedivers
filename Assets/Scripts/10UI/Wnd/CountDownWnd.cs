using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.UI;
using FPSGame.Audio;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;


// `CountDownTypeEnum` 已于 2026-10-01 下沉到 `00Core/CountDownTypeEnum.cs`（00_Core）：
// 它被 TaskManager/WndManager（01Manager）与 DeathUI（04UI）共见 ⇒ 住在这里会让管理器反向依赖 UI，
// `09_Managers` 切不出来。全局命名空间未变 ⇒ 调用点零改动。

/// <summary>
/// 通用倒计时窗口（外部驱动）。
/// 由调用方通过 <see cref="StartDown(Func{int}, CountDownTypeEnum, int)"/> 传入值提供者与启动阈值，每帧读取返回值驱动显示，
/// 自身不做计时。兼容旧用法：传 ()=>TaskManager.Instance.nowTask.Countdown。
/// 支持配置起始阈值、时间格式、是否显示动画/声音，并可通过事件回调感知倒计时变化与归零。
/// </summary>
[AddComponentMenu("UI/窗口/倒计时")]
public class CountDownWnd : Window
{
    [Serializable]
    private struct CountDownTypeInfo
    {
        public Color bgColor;
        public Color iconColor;
        public Sprite icon;
        public Color titleColor;
    }

    [SerializeField]
    private Transform bg, title, txt, icon,iconFrame;

    [SerializeField]
    private Animator anim;

    [InspectorName("根节点布局项")]
    [SerializeField]
    private LayoutElement layout;

    [SerializeField]
    private AudioClip warning;

    [InspectorName("首次显示值")]
    [SerializeField]
    private int countDown = 0;

    [Header("通用配置")]
    [Tooltip("低于此值才显示动画与音效(即倒计时启动阈值)")]
    [SerializeField]
    private int activeBelow = 16;

    [Tooltip("文本格式,{0}为秒数")]
    [SerializeField]
    private string timeFormat = "00:{0:D2}";

    [Tooltip("动画状态名")]
    [SerializeField]
    private string animStateName = "Idle";

    [InspectorName("进入动画状态名")]
    [Tooltip("根节点 Animator 上的进入状态（NoticeWnd 同款 controller：与退出共用同一 clip）")]
    [SerializeField]
    private string enterAnimName = "Entry";

    [InspectorName("退出动画状态名")]
    [Tooltip("根节点 Animator 上的退出状态（NoticeWnd 同款：同一 clip、speed=-1 倒放）")]
    [SerializeField]
    private string exitAnimName = "Exit";

    [InspectorName("退出动画时长(秒)")]
    [Tooltip("播完退出动画后再收起高度并隐藏窗口；留一点余量（NoticeWnd 的 Enter clip 长 0.333s）")]
    [SerializeField]
    private float exitAnimDuration = 0.4f;

    [SerializeField]
    private List<KVP<CountDownTypeEnum, CountDownTypeInfo>> infos;

    /// <summary>外部值提供者；为空表示尚未启动</summary>
    private Func<int> _valueProvider;

    /// <summary>窗口内可折叠高度(用于动画展开收起)</summary>
    private int height;

    /// <summary>当前是否处于「已展开」状态（决定归零时要不要播退场动画）</summary>
    private bool _shown;

    /// <summary>退场协程是否在跑（防止重复启动）</summary>
    private bool _exiting;

    /// <summary>每次显示数值跳变时触发(参数为当前剩余秒数)</summary>
    public event Action<int> OnValueChanged;

    ///// <summary>倒计时归零时触发</summary>
    //public event Action OnFinished;

    /// <summary>是否正在显示倒计时动画区域</summary>
    public bool IsCounting => _valueProvider != null;

    protected override void FirstShowWnd()
    {
        countDown = activeBelow;
        // ⚠ 不能用 rect.rect.height 当展开高度：本方法由 SetWndState(true) 里的
        //   gameObject.SetActive(true) 同步触发，此刻父 VerticalLayoutGroup 还没重排过，
        //   rect.height 可能是 0 或 prefab 旧值 ⇒ height 记成 0 后倒计时永远展开不了。
        //   改为直接读 LayoutElement 的设计高度（prefab 里预置，与 Inspector 同源）。
        if (Layout) height = Mathf.RoundToInt(Layout.preferredHeight);
        SetHeight(0);
    }

    protected override void ShowWnd()
    {
        SetActive(anim.transform, false);
        // 兼容旧用法：若未通过 Start(...) 指定提供者,默认跟随当前任务倒计时
        //if (_valueProvider == null)
        //{
        //    _valueProvider = () => taskManager.nowTask.Countdown;
        //}
    }

    protected override void HideWnd()
    {
        Stop();
    }

    /// <summary>
    /// 以外部驱动模式启动，每帧读取 provider 的返回值驱动显示。
    /// <paramref name="activeBelow"/> 为启动阈值(低于它才展开动画并播放音效)，≤0 表示沿用 Inspector 配置。
    /// 兼容旧用法：传 ()=>TaskManager.Instance.nowTask.Countdown。
    /// </summary>
    public void StartDown(Func<int> provider, CountDownTypeEnum type, int activeBelow = -1)
    {
        // 传入阈值(>0)优先于 Inspector 配置；-1/0 表示沿用序列化值
        if (activeBelow > 0) this.activeBelow = activeBelow;
        // 以阈值同步"首次显示值"，避免首帧把残留的 countDown 误判成数值跳变
        countDown = this.activeBelow;

        _valueProvider = provider;
        // 窗口可能被复用过 ⇒ 每次启动都复位进出场状态
        _shown = false;
        _exiting = false;
        var info = infos.Find(item => item.Key == type).Value;
        SetColor(bg, info.bgColor);
        SetColor(title, info.titleColor);
        SetColor(iconFrame, info.iconColor);
        SetSprite(icon, info.icon);

        SetWndState(true);
    }

    /// <summary>立即更新一次显示(由调用方驱动时手动刷新)</summary>
    public void SetValue(int value)
    {
        countDown = value;
        ApplyValue(value);
    }

    /// <summary>停止倒计时并隐藏动画区域(不关闭窗口)</summary>
    public void Stop()
    {
        _valueProvider = null;
        _shown = false;
        SetActive(anim.transform, false);
        SetHeight(0);
    }

    // Update is called once per frame
    private void Update()
    {
        if (_valueProvider == null) return;

        int nowcd = _valueProvider();
        // 只在数值发生变化时刷新,避免逐帧冗余操作
        if (nowcd == countDown) return;

        countDown = nowcd;

        // 归零 ⇒ 停止驱动并关闭窗口
        // ⚠ 原判据是 `countDown == 0`：countDown 上一步刚被赋成 provider 的读数，
        //   归零那一帧只会走 else 刷出 00:00，之后 nowcd==countDown 恒真 ⇒ 窗口永不消失。
        if (nowcd <= 0)
        {
            // 已展开过 ⇒ 播退场动画后再收起隐藏；从没展开过 ⇒ 直接收起隐藏（没东西可退场）
            _valueProvider = null;
            if (_shown)
            {
                _shown = false;
                SetActive(anim.transform, false);
                if (!_exiting) StartCoroutine(ExitAndHide());
            }
            else
            {
                Stop();
                SetWndState(false);
            }
            return;
        }

        ApplyValue(nowcd);
    }

    /// <summary>
    /// 统一根据当前剩余秒数刷新动画显隐、文本与音效。
    /// </summary>
    private void ApplyValue(int nowcd)
    {
        bool show = nowcd < activeBelow;
        if (show != _shown)
        {
            _shown = show;
            SetActive(anim.transform, show);
            if (show)
            {
                // 展开：先给占位高度（下方窗口让位），再播进入动画把 scale 拉回 (1,1,1)
                SetHeight(height);
                PlayRootAnim(enterAnimName);
            }
            else
            {
                SetHeight(0);
            }
        }

        if (show)
        {
            anim.Play(animStateName, 0, 0);
            SetText(txt, string.Format(timeFormat, nowcd));
            if (warning) AudioSvc.PlaySound(new(warning, AudioGroups.UI));
            OnValueChanged?.Invoke(nowcd);
        }
    }

    /// <summary>播放根节点 Animator 上的状态（根上没挂 Animator 时静默跳过，避免 NRE）。</summary>
    private void PlayRootAnim(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;
        var rootAnim = GetComponent<Animator>();
        if (!rootAnim) return;
        rootAnim.Play(stateName, 0, 0);
    }

    /// <summary>
    /// 退场：播根节点 Animator 的退出状态（NoticeWnd 同款，与进入共用同一 clip 的倒放），
    /// 等 <see cref="exitAnimDuration"/> 秒后收起占位高度并隐藏窗口。
    ///
    /// <para>▍为什么要等：直接 <c>SetWndState(false)</c> 会让窗口瞬间消失（scale 与高度一起跳变），
    /// 与本项目其它窗口的进出观感不一致。</para>
    ///
    /// <para>▍⚠ <c>SetWndState(false)</c> 会停掉本协程，所以 <c>_exiting</c> 必须在它之前复位，
    /// 否则窗口下次被复用时退场流程永远进不来。</para>
    /// </summary>
    private IEnumerator ExitAndHide()
    {
        _exiting = true;

        PlayRootAnim(exitAnimName);
        yield return new WaitForSeconds(exitAnimDuration);

        SetHeight(0);
        _exiting = false;
        SetWndState(false);
    }

    /// <summary>根节点上的布局项；Inspector 未绑定引用时自动就近获取（懒加载，只取一次）。</summary>
    private LayoutElement Layout
    {
        get
        {
            if (!layout) layout = GetComponent<LayoutElement>();
            return layout;
        }
    }

    /// <summary>
    /// 设置窗口的展开高度（<paramref name="h"/> = 0 表示收起）。
    ///
    /// <para>▍为什么改 <see cref="LayoutElement"/> 而不是 <c>sizeDelta</c>：父
    /// <see cref="VerticalLayoutGroup"/> 勾了 Child Control Height 时，它每次重排都会用
    /// 本节点的 <see cref="LayoutElement"/> 把 <c>sizeDelta.y</c> 覆盖掉（实测改 sizeDelta
    /// 完全无效）；而父级不勾时，手动改 <c>sizeDelta</c> 又不会通知父级重排。
    /// ⇒ 高度统一交给 LayoutElement 这一个来源。</para>
    ///
    /// <para>▍<c>minHeight</c> 与 <c>preferredHeight</c> 必须同时改：只改 preferred 会被
    /// 残留的 min 顶住，高度不会变。</para>
    ///
    /// <para>▍改完必须通知父级重排，否则下方窗口的位置不会跟随变化 —— 这正是
    /// "要隐藏再显示一次才正常"的根源（<c>RefreshLayout</c> 内部就是 MarkLayoutForRebuild）。</para>
    /// </summary>
    private void SetHeight(int h)
    {
        var le = Layout;
        if (!le) return;
        if (Mathf.Approximately(le.minHeight, h) && Mathf.Approximately(le.preferredHeight, h)) return;

        le.minHeight = h;
        le.preferredHeight = h;
        RefreshLayout(transform);
    }
}
}
