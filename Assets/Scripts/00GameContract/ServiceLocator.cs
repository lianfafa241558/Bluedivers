// 只允许"注册方"（= 09_Managers，各服务的实现处）写入本类槽位；
// 下层（05_UnitCore/06_Gameplay/10_UI/10_Effect…）**只能读** —— 想改全局状态必须去管理器层，
// 这条约束由编译器保证（2026-10-01 架构收尾的硬化步骤）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]

namespace FPSGame.GameContract
{
    /// <summary>
    /// 服务定位器（**过渡期方案**，2026-09-30 为拆出 <c>05_UnitCore</c> 引入）。
    ///
    /// <para>▍它解决什么：<c>02Game</c> 里散落 300+ 处 <c>BattleManager.Instance.XXX</c>/<c>TaskManager.Instance.XXX</c>
    /// 这类"下层直连上层具体类型"的调用，是程序集拆不动的主因。把这些调用改成读契约接口，
    /// 下层就只依赖 <c>01_GameContract</c>，上层实现可以自由归位。</para>
    ///
    /// <para>▍注册方式：由实现方（<c>TaskManager</c>/<c>BattleManager</c>）在自己的 <c>Init()</c>/<c>Awake()</c> 里赋值。
    /// 使用方一律写 <c>ServiceLocator.Task?.</c> / <c>ServiceLocator.Battle?.</c> 并处理好 null（服务未就绪时取中性值，
    /// 不要抛）。</para>
    ///
    /// <para>▍边界（反模式禁令）：<b>不要用它来藏"随便什么静态数据"</b>；每加一个成员都要问"这是不是某个服务本来就该提供的能力"。
    /// 数据本身（如 <c>AboStateData_SO</c> 的运行时缓存）应当下沉到数据层自持，不要塞进定位器。</para>
    ///
    /// <para>▍退出条件：等 <c>09_Managers</c> 成集、或改用显式注入/装配根后，本类应当被删除。</para>
    /// </summary>
    public static class ServiceLocator
    {
        // ⚠ 各槽默认指向 NullServices 的空对象（见 NullServices.cs）：
        //   ⇒ 调用点可放心写 `ServiceLocator.X.Y(...)`，服务未就绪时自动取"中性值"（就是契约约定的语义），
        //     不必到处写 `?.`；批量替换（P5-1d）也才能机械进行。

        /// <summary>任务/难度服务（由 <c>TaskManager</c> 注册）。</summary>
        public static ITaskService Task { get; internal set; } = NullServices.Task;

        /// <summary>战斗服务（由 <c>BattleManager</c> 注册）。</summary>
        public static IBattleService Battle { get; internal set; } = NullServices.Battle;

        /// <summary>游戏流程状态（由 <c>GameRoot</c> 注册；Bridge/Ready 场景无 Battle，故必须独立）。</summary>
        public static IFlowService Flow { get; internal set; } = NullServices.Flow;

        /// <summary>特效服务（由 <c>VFXManager</c> 注册）。</summary>
        public static IVfxService Vfx { get; internal set; } = NullServices.Vfx;

        /// <summary>逻辑帧注册服务（由 <c>NetManager</c> 注册）。</summary>
        public static INetService Net { get; internal set; } = NullServices.Net;

        /// <summary>资源加载服务（由 <c>ResSvc</c> 注册）。</summary>
        public static IResService Res { get; internal set; } = NullServices.Res;

        /// <summary>存档设置服务（由 <c>ArchiveSvc</c> 注册）。</summary>
        public static IArchiveService Archive { get; internal set; } = NullServices.Archive;

        /// <summary>窗口 / UI 服务（由 <c>WndManager</c> 注册）。</summary>
        public static IWindowService Wnd { get; internal set; } = NullServices.Wnd;

        /// <summary>房间 / 玩家数（由 <c>RoomManager</c> 注册）。</summary>
        public static IRoomService Room { get; internal set; } = NullServices.Room;

        /// <summary>寻路请求（由 <c>PathRequestManager</c> 注册）。</summary>
        public static IPathService Path { get; internal set; } = NullServices.Path;

        /// <summary>
        /// 各槽当前指向的实现类型（排查"服务是否已注册"用；未注册时会看到 <c>Null*Service</c>）。
        /// </summary>
        public static string Dump()
        {
            return "ServiceLocator: Task=" + Task.GetType().Name
                 + " Battle=" + Battle.GetType().Name
                 + " Flow=" + Flow.GetType().Name
                 + " Vfx=" + Vfx.GetType().Name
                 + " Net=" + Net.GetType().Name
                 + " Res=" + Res.GetType().Name
                 + " Archive=" + Archive.GetType().Name
                 + " Wnd=" + Wnd.GetType().Name
                 + " Room=" + Room.GetType().Name
                 + " Path=" + Path.GetType().Name;
        }
    }
}
