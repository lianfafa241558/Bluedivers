using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Managers;

namespace FPSGame.UI
{

/// <summary>
/// UI 侧窗口中心（2026-10-01 建）：替代 <c>WndManager</c> 原先持有的 10 个**具体窗口字段**。
///
/// <para>▍背景：那些字段都是 `04UI` 的类型，被 `01Manager` 持有 ⇒「管理器 → UI」反向依赖，
/// `09_Managers`/`10_UI` 都切不出来。现在改成：</para>
/// <list type="bullet">
///   <item>窗口在 `Window.Awake` 里把 `this` 注册到**本类**（不再往 `WndManager` 塞字段）；</item>
///   <item>本类把"开关窗口 / 弹提示 / 倒计时 / 清提示"以**委托**登记进契约层的
///         <see cref="WindowRegistry"/>，管理器经契约转调（不出现任何 UI 类型）；</item>
///   <item><see cref="Scan"/> 扫场景里**全部**窗口（<c>includeInactive: true</c>）兜底 ——
///         窗口平时是关着的、`Awake` 可能还没跑，靠它保证"从未打开过的窗口也能被打开"。</item>
/// </list>
///
/// <para>▍取值方式（2026-10-01 按评审意见改为"单一数据源 + 泛型"）：所有窗口只存在于
/// <see cref="map"/> 一个字典里（键 <see cref="WndType"/>、值实例），
/// UI 内部取实例用 <see cref="Get{T}(WndType)"/>/<see cref="Get{T}()"/>（带强类型、免强转）。
/// <b>⚠ 纪律：`Get&lt;T&gt;` 只能在 `04UI` 内使用</b>（它要求调用方写出具体窗口类型）；
/// 跨层（管理器）一律用契约动词 <c>WindowRegistry.*</c>；玩法层则发 <c>GlobalEventSub.OpenWnd</c>（2026-10-01 起 Wnd 槽已删）。</para>
///
/// <para>▍为什么是 <c>internal</c>：把上面那条纪律**交给编译器**（连 `Register`/`Scan` 也不外露）。
/// ⚠ 但它今天还**没有牙齿** —— `04UI/` 根目录目前仍在 `Assembly-CSharp` 里，和 `01Manager`/`06Gameplay` 同集
/// ⇒ 同集内 `internal` 拦不住。**等 `10_UI` 切成独立 asmdef（`04UI/` 整体迁进 asmdef 目录）后，
/// `internal` 才会真正挡住管理器/玩法/表现层** —— 这是"意图自证 + 未来自动生效"，不是现在就有强制力。</para>
/// </summary>
internal static class WndHub
{
    /// <summary>窗口表：键 = 窗口标识（跨层唯一能命名的东西），值 = 实例。**唯一数据源**。</summary>
    private static readonly Dictionary<WndType, Window> map = new Dictionary<WndType, Window>();

    static WndHub()
    {
        // 契约层插槽为空时会回调这里做一次"全场景扫描"
        WindowRegistry.Scanner = Scan;
    }

    /// <summary>
    /// 启动时就把扫描器交给契约层（2026-10-01）。
    /// ⚠ 必要性：<c>Window.Awake</c> 只在窗口**被激活**时跑；若场景里所有窗口都是关着的，
    /// 就不会有人触发本类的静态构造 ⇒ 契约层拿不到 <see cref="WindowRegistry.Scanner"/>，
    /// "第一次要打开某个窗口"会失败。用启动钩子保证扫描器一定可用（扫描本身仍是按需触发）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        WindowRegistry.Scanner = Scan;
    }

    // ---------------- 取值（仅限 04UI 内使用） ----------------

    /// <summary>按枚举取窗口实例（类型不符或未登记 ⇒ null）。⚠ 仅在 `04UI` 内使用。</summary>
    public static T Get<T>(WndType type) where T : Window
    {
        Window w;
        if (map.TryGetValue(type, out w) && w != null) return w as T;
        Scan();
        return map.TryGetValue(type, out w) && w != null ? w as T : null;
    }

    /// <summary>
    /// 按类型取窗口实例（免写枚举；窗口总数 ≤ 26，线性扫足够）。
    /// ⚠ 仅在 `04UI` 内使用；跨层请用契约动词。
    /// </summary>
    public static T Get<T>() where T : Window
    {
        foreach (var kv in map)
        {
            var t = kv.Value as T;
            if (t != null) return t;
        }
        Scan();
        foreach (var kv in map)
        {
            var t = kv.Value as T;
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>跑马灯提示窗（原 <c>WndManager.tipWnd</c>；只读，避免"字段/字典两份真相"）。</summary>
    public static TipWnd Tip { get { return Get<TipWnd>(WndType.Tip); } }

    /// <summary>NPC 通知窗（原 <c>WndManager.noticeWnd</c>）。</summary>
    public static NoticeWnd Notice { get { return Get<NoticeWnd>(WndType.Notice); } }

    /// <summary>倒计时窗（原 <c>WndManager.countDownWnd</c>）。</summary>
    public static CountDownWnd CountDown { get { return Get<CountDownWnd>(WndType.CountDown); } }

    /// <summary>操作提示窗（原 <c>WndManager.operationWnd</c>）。</summary>
    public static OperationWnd Operation { get { return Get<OperationWnd>(WndType.Operation); } }

    // ---------------- 注册 ----------------

    /// <summary>
    /// 窗口自注册（由 <c>Window.Awake</c> 调用，覆盖全部 26 个窗口子类，
    /// 包括原先靠 Inspector 字段挂上去的 <c>TipWnd</c>/<c>NoticeWnd</c>/<c>CountDownWnd</c>/<c>OperationWnd</c>）。
    /// ⚠ 未激活的窗口不会跑 <c>Awake</c> ⇒ 由 <see cref="Scan"/> 兜底。
    /// </summary>
    public static void SelfRegister(Window wnd)
    {
        if (wnd == null) return;
        Register(TypeOf(wnd), wnd);
    }

    /// <summary>窗口销毁时的反注册（由 <c>Window.OnDestroy</c> 调用）。</summary>
    public static void SelfUnregister(Window wnd)
    {
        if (wnd == null) return;
        Unregister(TypeOf(wnd), wnd);
    }

    /// <summary>按枚举登记一个窗口。</summary>
    public static void Register(WndType type, Window wnd)
    {
        if (type == WndType.None || wnd == null) return;
        map[type] = wnd;
        Bind();
    }

    /// <summary>反注册（仅当登记的仍是自己）。</summary>
    public static void Unregister(WndType type, Window wnd)
    {
        if (type == WndType.None || wnd == null) return;
        Window cur;
        if (map.TryGetValue(type, out cur) && cur == wnd) map.Remove(type);
        Bind();
    }

    // ---------------- 兜底扫描 ----------------

    /// <summary>
    /// 扫描场景里全部窗口（**含未激活**）并登记。
    /// ⚠ 必须用 <c>FindObjectsOfType&lt;Window&gt;(true)</c>：窗口平时 `SetActive(false)`，
    /// 其 `Awake` 根本不会跑，否则"第一次要打开某个窗口"就会失败。
    /// </summary>
    public static void Scan()
    {
        var all = UnityEngine.Object.FindObjectsOfType<Window>(true);
        for (var i = 0; i < all.Length; i++)
        {
            var w = all[i];
            if (w == null) continue;
            Register(TypeOf(w), w);
        }
    }

    /// <summary>
    /// 窗口类型 → 枚举。⚠ **新增窗口只改这一处**（`Window` 不做抽象属性：那要动 26 个子类）。
    /// </summary>
    private static WndType TypeOf(Window w)
    {
        if (w is SelectRoleWnd) return WndType.SelectRole;
        if (w is SelectMapWnd) return WndType.SelectMap;
        if (w is GuideWnd) return WndType.Guide;
        if (w is VehicleWnd) return WndType.Vehicle;
        if (w is AirdropConfigWnd) return WndType.AirdropConfig;
        if (w is SettingWnd) return WndType.Setting;
        if (w is CountDownWnd) return WndType.CountDown;
        if (w is OperationWnd) return WndType.Operation;
        if (w is TipWnd) return WndType.Tip;
        if (w is NoticeWnd) return WndType.Notice;
        return WndType.None;
    }

    // ---------------- 把能力登记进契约 ----------------

    private static void Bind()
    {
        WindowRegistry.SetWndState = (type, active) =>
        {
            Window w;
            if (map.TryGetValue(type, out w) && w != null) { w.SetWndState(active); return true; }
            // 还没这个窗口 ⇒ 请兜底扫描再试一次（覆盖"窗口刚被实例化"的情况）
            Scan();
            if (map.TryGetValue(type, out w) && w != null) { w.SetWndState(active); return true; }
            return false;
        };

        WindowRegistry.IsOpen = type =>
        {
            Window w;
            return map.TryGetValue(type, out w) && w != null && w.State;
        };

        WindowRegistry.CreatNotice = (role, type, func, vaildTime) =>
        {
            var notice = Notice;
            if (notice == null) return;
            // 原 WndManager.CreatNotice 的实现（用 ResSvc 取语音；UI 层在管理器之上，直连合法）
            ResSvc.Instance.GetVoice(role, type, out var data, out var sourceName, out var portrait);
            var noticeData = new NoticeWnd.NoticeData()
            {
                data = data.Get(),
                sourceName = sourceName,
                portrait = portrait,
                func = func,
                allowWait = true,
                vaildTime = vaildTime
            };
            notice.Creat(noticeData);
        };

        WindowRegistry.CreatCountDown = (provider, type) =>
        {
            var cd = CountDown;
            if (cd != null) cd.StartDown(provider, type);
        };

        WindowRegistry.ClearNotice = () =>
        {
            var notice = Notice;
            if (notice != null) notice.Clear();
        };
    }

    /// <summary>登记状态摘要（排查用）。</summary>
    public static string Dump()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("WndHub: 窗口=").Append(map.Count)
          .Append(" Tip=").Append(Tip != null)
          .Append(" Notice=").Append(Notice != null)
          .Append(" CountDown=").Append(CountDown != null)
          .Append(" Operation=").Append(Operation != null)
          .Append(" | keys=");
        foreach (var kv in map) sb.Append(kv.Key).Append(',');
        return sb.ToString();
    }
}
}
