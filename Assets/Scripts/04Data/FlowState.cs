using FPSGame.Core;
// 写入方 = 09_Managers 的 GameRoot（与 RoomState/UIState/TaskState 同一套写入保护手法）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]



namespace FPSGame.Data
{
    /// <summary>
    /// 游戏流程状态**数据自持点**（2026-10-01 取代 <c>ServiceLocator.Flow</c> 槽的"状态查询"部分）。
    ///
    /// <para>▍为什么可行：`Flow` 槽其实混着两类东西——① **状态查询**（`GameState`/`IsMainStage`/`IsLocal`，13 处）
    /// ② **调度/命令**（协程、定时器、切阶段，12 处）。第①类是纯粹的"一处写、多处读"，
    /// 与 <c>UIState.WindowState</c> / <c>RoomState</c> / <c>TaskState</c> 同型 ⇒ 走数据自持，
    /// 连"服务未就绪"都不用处理（未就绪 = 默认值 <c>Front</c> = 未进入主流程，与原空对象语义一致）。</para>
    ///
    /// <para>▍写入方 <c>GameRoot</c> 的 3 个同步点（漏一处就读到过期值）：
    /// ① <c>GameState</c> 的 <b>setter</b>；② <c>SetWithoutNotify</c>（绕过事件的那条路，必须一起镜像）；
    /// ③ <c>Awake</c> 里的 <c>IsLocal</c>（它是 <c>GameRoot</c> 上的**序列化字段**，只在启动时确定）。</para>
    ///
    /// <para>▍<see cref="IsMainStage"/> 是**计算属性**，不参与同步——口径与原 <c>IFlowService.IsMainStage</c>
    /// 逐字一致（Game / Ready / Bridge）⇒ 零同步点、零漂移风险。</para>
    /// </summary>
    public static class FlowState
    {
        /// <summary>当前游戏状态（未就绪时 = <c>Front</c>）。</summary>
        public static GameStateEnum GameState { get; internal set; } = GameStateEnum.Front;

        /// <summary>是否本地实例（= <c>GameRoot.IsLocal</c> 序列化字段，启动时镜像一次）。</summary>
        public static bool IsLocal { get; internal set; }

        /// <summary>
        /// 是否已进入主流程（Game / Ready / Bridge）——口径与原 <c>IFlowService.IsMainStage</c> 一致。
        ///
        /// <para>⚠ 消费者里 <c>Actor.WaitSetPos</c> 是**每帧 while 轮询**它，所以这里刻意做成纯计算属性
        /// （无锁、无事件、无转换开销）。</para>
        /// </summary>
        public static bool IsMainStage => GameState == GameStateEnum.Game
                                       || GameState == GameStateEnum.Ready
                                       || GameState == GameStateEnum.Bridge;
    }
}
