namespace FPSGame.GameContract
{
    /// <summary>
    /// 房间/玩家 服务契约（由 <c>RoomManager</c> 实现并注册，2026-09-30 为 P5 加入）。
    ///
    /// <para>▍为什么只开两个"投影成员"：玩法层真实用法只有 3 处（`EnemyController` 1、`MissionOilRefining` 2），
    /// 而 <c>RoomManager.Master</c>/<c>players</c> 的类型是 <c>PlayerData</c>（在 01Manager＝上层）
    /// ⇒ 契约层无法命名它们 ⇒ 只暴露玩法层真正要用的**整数投影**（大师序号、玩家数）。
    /// 这是"接口隔离"在本项目的第二次应用（第一次是 <c>IUnitScale</c>）。</para>
    /// </summary>
    public interface IRoomService
    {
        /// <summary>房主（非机器人玩家）的索引；没有房主时返回 0。</summary>
        int MasterIndex { get; }

        /// <summary>当前玩家数（含机器人）。</summary>
        int PlayerCount { get; }
    }
}
