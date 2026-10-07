// 写入方唯一 = 09_Managers 的 WndManager（与 TeamState/LogicFrame 同一套写入保护手法）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]

namespace FPSGame.Data
{
    /// <summary>
    /// 界面状态（<see cref="FPSGame.Core.WindowStateEnum"/>）的**数据自持点**
    /// —— 2026-10-01 取代 <c>ServiceLocator.Wnd.WindowState</c> 查询。
    ///
    /// <para>▍为什么是"数据"而不是"事件"：玩法层（<c>InputManager</c>/<c>PlayerInputHandler</c>）要的是
    /// "**现在**是什么状态"（输入门控，随时可能读），事件只能通知"变了"、答不了"现在是什么"
    /// ⇒ 按"值走数据自持、无返回值的命令走事件"的判据，这里放值。</para>
    ///
    /// <para>▍写入方：<c>WndManager</c>（它本来就是窗口状态机）——在 <c>Awake</c> 里同步一次序列化初值，
    /// 并在 <c>WindowState</c> setter 里每次赋值都同步。</para>
    ///
    /// <para>▍⚠ 初值必须是 <see cref="FPSGame.Core.WindowStateEnum.Game"/>，**不能**用枚举的 0 值
    /// （<c>WindowStateEnum</c> 的第一个值是 <c>All</c>）：旧写法在 <c>WndManager</c> 未就绪时返回
    /// <c>Game</c>（= 允许输入），初值取错会导致"未就绪时输入被锁死"。</para>
    /// </summary>
    public static class UIState
    {
        /// <summary>当前界面状态。未就绪时 = <c>Game</c>（与旧 <c>WndManager.WindowState</c> 的兜底语义一致）。</summary>
        public static FPSGame.Core.WindowStateEnum WindowState { get; internal set; } = FPSGame.Core.WindowStateEnum.Game;
    }
}
