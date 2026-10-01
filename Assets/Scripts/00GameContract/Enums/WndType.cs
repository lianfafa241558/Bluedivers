namespace FPSGame.GameContract
{
    /// <summary>
    /// 窗口标识 —— <b>跨层"开窗"的唯一钥匙</b>。玩法层只发 <c>GlobalEventSub.OpenWnd</c> 事件，
    /// 由 <c>WndManager.SetWndState</c> 映射到 <c>04UI</c> 的具体窗口；玩法层因此不出现任何 UI 类型，
    /// 避免 <c>02Game → 04UI</c> 的上行依赖把程序集焊死。
    /// <para>归属：被 <c>WndManager</c>(09_Managers)、<c>Window</c> 子类(10_UI)、玩法层三者共见 ⇒ 放三者共见的最低层＝契约层。
    /// （不跟着 <c>WindowStateEnum</c> 放 <c>00_Core</c>，那是历史原因。）</para>
    /// <para>⚠ 新增窗口：在这里加一个值，并在 <c>WndManager.SetWndState</c> 的映射里加一行，两处必须同步。</para>
    /// </summary>
    public enum WndType
    {
        /// <summary>未指定（调用方不应使用；映射不到窗口时静默跳过）。</summary>
        None = 0,

        /// <summary>操作提示窗（<c>WndManager.operationWnd</c>）。</summary>
        Operation,
        /// <summary>跑马灯提示（<c>WndManager.tipWnd</c>）。</summary>
        Tip,
        /// <summary>NPC 通知（<c>WndManager.noticeWnd</c>）。</summary>
        Notice,
        /// <summary>倒计时（<c>WndManager.countDownWnd</c>）。</summary>
        CountDown,
        /// <summary>选图 / 选任务（<c>WndManager.selectMapWnd</c>）。</summary>
        SelectMap,
        /// <summary>选角色（<c>WndManager.selectRoleWnd</c>）。</summary>
        SelectRole,
        /// <summary>指引（<c>WndManager.guideWnd</c>）。</summary>
        Guide,
        /// <summary>载具（<c>WndManager.vehicleWnd</c>）。</summary>
        Vehicle,
        /// <summary>空投配置（<c>WndManager.airdropConfigWnd</c>）。</summary>
        AirdropConfig,
        /// <summary>设置（<c>WndManager.settingWnd</c>）。</summary>
        Setting,
    }
}
