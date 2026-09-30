namespace FPSGame.GameContract
{
    /// <summary>
    /// 窗口标识（2026-09-30 为 P5 加入；是 <see cref="IWindowService.SetWndState"/> 的"钥匙"）。
    ///
    /// <para>▍它解决什么：<c>WndManager</c> 里那 10 个窗口字段（<c>selectRoleWnd</c>/<c>guideWnd</c>/<c>settingWnd</c>…）
    /// 都是 <c>04UI</c> 的**具体窗口类型** ⇒ 玩法层（如 <c>Furniture_General</c>）直连它们就形成
    /// `02Game → 04UI` 的上行依赖，程序集切不开。有了这个枚举，玩法层只说"开哪个窗口"，**不出现任何 UI 类型**。</para>
    ///
    /// <para>▍归属：被 <c>WndManager</c>(01Manager)、<c>Window</c> 子类(04UI)、玩法层**三者共见**
    /// ⇒ 放在三者共见的最低层＝契约层（不跟着 <c>WindowStateEnum</c> 放 <c>00_Core</c>，那是历史原因）。</para>
    ///
    /// <para>⚠ 新增窗口时：在这里加一个值，并在 <c>WndManager.SetWndState</c> 的映射里加一行。</para>
    /// </summary>
    public enum WndTypeEnum
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
