namespace FPSGame.GameContract
{
    /// <summary>
    /// 窗口标识 —— <b>跨层"开窗"的唯一钥匙</b>。玩法层只发 <c>GlobalEventBus.OnOpenWnd</c> 事件，
    /// 由 <c>WndManager</c>(09_Managers) 转调 <c>WindowRegistry</c>，最终由 UI 侧的 <c>WndHub.TypeOf</c>
    /// 映射到 `10UI` 的具体窗口；玩法层因此不出现任何 UI 类型，
    /// 避免「玩法 → UI」的上行依赖把程序集焊死。
    /// <para>归属：被 <c>WndManager</c>(09_Managers)、<c>Window</c> 子类(10_UI)、玩法层三者共见 ⇒ 放三者共见的最低层＝契约层。
    /// （不跟着 <c>WindowStateEnum</c> 放 <c>00_Core</c>，那是历史原因。）</para>
    /// <para>⚠ 新增窗口：在这里加一个值，并在 <c>WndHub.TypeOf</c> 的映射里加一行，两处必须同步
    /// （漏了 = 窗口注册不进 <c>WndHub</c> 的表 ⇒ 跨层开窗静默失效）。</para>
    /// </summary>
    public enum WndType
    {
        /// <summary>未指定（调用方不应使用；映射不到窗口时静默跳过）。</summary>
        None = 0,

        /// <summary>操作提示窗（<c>OperationWnd</c>）。</summary>
        Operation,
        /// <summary>跑马灯提示（<c>TipWnd</c>）。</summary>
        Tip,
        /// <summary>NPC 通知（<c>NoticeWnd</c>）。</summary>
        Notice,
        /// <summary>倒计时（<c>CountDownWnd</c>）。</summary>
        CountDown,
        /// <summary>选图 / 选任务（<c>SelectMapWnd</c>）。</summary>
        SelectMap,
        /// <summary>选角色（<c>SelectRoleWnd</c>）。</summary>
        SelectRole,
        /// <summary>指引（<c>GuideWnd</c>）。</summary>
        Guide,
        /// <summary>载具（<c>VehicleWnd</c>）。</summary>
        Vehicle,
        /// <summary>空投配置（<c>AirdropConfigWnd</c>）。</summary>
        AirdropConfig,
        /// <summary>设置（<c>SettingWnd</c>）。</summary>
        Setting,
        /// <summary>信息提示（<c>InformationWnd</c>；按 string 键增删一条信息子项）。</summary>
        Information,
        /// <summary>单行输入弹窗（<c>PasswordWnd</c>；房间密码 / 首次设置玩家名等）。</summary>
        Password,
        /// <summary>消息播报（<c>MsgWnd</c>；玩家倒地 / 发现任务点 / 加入·退出任务…）。</summary>
        Msg,
    }
}
