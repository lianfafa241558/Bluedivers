// 写入方唯一 = 09_Managers 的 TeamManager（与 LogicFrame/ServiceLocator 同一套写入保护手法）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]

namespace FPSGame.Data
{
    /// <summary>
    /// 房间状态的**数据自持点**（2026-10-01 取代 <c>ServiceLocator.Room</c> 槽）。
    ///
    /// <para>▍为什么需要它：玩法层（<c>06_Gameplay</c>）有 2 处要用"玩家数 / 房主序号"这两个整数，
    /// 而 <c>TeamManager</c>（与 <c>PlayerData</c>）在 <c>09_Managers</c> ⇒ 玩法层看不见它。
    /// 与 <c>ArchivesData_SO.Current</c>、<c>AboStateData_SO.Dic</c> 完全同一套路：
    /// **上层写入、下层直读数据**，不走"服务定位器"。</para>
    ///
    /// <para>▍⚠ 写入约定（重要）：<c>TeamManager</c> 必须在它的**所有**玩家列表变更点调用
    /// <c>SyncTeamState()</c> —— 目前是 4 处：<c>Init</c> / <c>AddPlayer</c> / <c>LeavePlayer</c> / <c>JoinPlayer</c>。
    /// 原因是旧接口 <c>IRoomService</c> 是"每次读时现算"，而这里是**快照** ⇒ 漏一个点就会读到过期值。
    /// （<c>AddPlayer</c>/<c>LeavePlayer</c> 目前无人调用，是为将来联机预留的 API，但仍必须同步。）</para>
    ///
    /// <para>▍中性值：未就绪时两个值都是 0；<c>PlayerCount</c> 的调用方按"单人 = 1"自行兜底
    /// （与原空对象 <c>NullRoomService.PlayerCount = 1</c> 的语义对齐）。</para>
    /// </summary>
    public static class TeamState
    {
        /// <summary>当前玩家数（含机器人）。未就绪为 0。</summary>
        public static int PlayerCount { get; internal set; }

        /// <summary>房主（非机器人玩家）的索引；未就绪为 0。</summary>
        public static int MasterIndex { get; internal set; }
    }
}
