using FPSGame.GameContract;
// 写入方 = 09_Managers 的 BattleManager（与 FlowState/TeamState/TaskState/UIState 同一套写入保护手法）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]


namespace FPSGame.Data
{
    /// <summary>
    /// 战斗状态**数据自持点**（2026-10-01 取代战斗服务契约的"读值"部分）。
    ///
    /// <para>▍为什么可行：这类成员是纯粹的"**一处写、多处读**"，与 <c>TaskState</c>/<c>TeamState</c>/<c>FlowState</c>
    /// 同型 ⇒ 走数据自持，连"服务是否就位"都不必判断（未就绪 = 中性值：<see cref="HaveBooster"/> 恒 false、
    /// <see cref="IsStartBattle"/> = false，与 <c>NullBattleService.Instance</c> 逐字一致）。</para>
    ///
    /// <para>▍它同时解决了"注入来不及"的问题：<c>HealthPlayer</c>/<c>HealthShield</c> 在 <c>Awake</c> 里
    /// 读 <see cref="HaveBooster"/> 决定 <c>MaxShield += 10</c>，那时任何"注入"都还没发生 ⇒ 只能读数据自持点。</para>
    ///
    /// <para>▍写入方 <c>BattleManager</c> 的同步点（漏一处就读到过期值）：
    /// ① <c>Awake</c> 里的**跨局重置**（静态字段不会随新实例自动归零，必须显式清，否则上一局的强化/开战状态会残留）；
    /// ② <c>ApplyTeamEnhance</c>（<see cref="ActiveTeamEnhance"/> 的**唯一**写入点）；
    /// ③ <c>IsStartBattle</c> 的两处置真点。</para>
    ///
    /// <para>▍⚠ 为何没有 <c>WaveCount</c>：它的真相 <c>WaveManager.WaveCount => ticks.Count - 1</c> 会**递减**
    /// ——<c>ticks</c> 由 <c>00_Core</c> 的 <c>TickBehaviour</c> 基类增删（<c>TickBehaviour.cs:23/38</c>），
    /// 加上 <c>CreatWave</c> 的 9 个分支 ⇒ **写入点分散且不在同一层**（Core 不能反向写 Data），
    /// 不满足"一处写" ⇒ 快照必然漂移。它只有 2 个消费点，收益也极低 ⇒ 继续留在 <c>IBattleService</c> 契约里。</para>
    /// </summary>
    public static class BattleState
    {
        /// <summary>未就绪时的战斗随机源（固定种子 0；= 原 <c>NullBattleService.BattleRandom</c> 的中性值）。</summary>
        private static readonly System.Random NullRandom = new System.Random(0);

        /// <summary>本局已启用的全队强化（未就绪时为空数组 ⇒ <see cref="HaveBooster"/> 恒 false）。</summary>
        public static BoosterType[] ActiveTeamEnhance { get; internal set; } = System.Array.Empty<BoosterType>();

        /// <summary>某类全队强化是否已启用（未就绪时恒 false）。</summary>
        public static bool HaveBooster(BoosterType type) => System.Array.IndexOf(ActiveTeamEnhance, type) >= 0;

        /// <summary>是否已开战（未就绪时 = false）。</summary>
        public static bool IsStartBattle { get; internal set; }

        /// <summary>
        /// 确定性战斗随机源（未就绪时 = 固定种子 0 的中性实例）。
        ///
        /// <para>⚠ 它是**有状态对象**：写入方只应在"新一局开始时"换实例（<c>BattleManager</c> 用任务种子 <c>new</c>），
        /// 中途替换会让同一局的随机序列重来。</para>
        /// </summary>
        public static System.Random BattleRandom { get; internal set; } = NullRandom;

        /// <summary>
        /// 全部状态归零（服务下线 / 新一局开始）—— 静态字段不会随实例销毁自动归零。
        ///
        /// <para>⚠ 调用处必须带**身份判定**（只允许存活实例重置），否则"重复实例销毁"会清掉活跃实例的状态。</para>
        /// </summary>
        internal static void Reset()
        {
            ActiveTeamEnhance = System.Array.Empty<BoosterType>();
            IsStartBattle = false;
            BattleRandom = NullRandom;
        }
    }
}
