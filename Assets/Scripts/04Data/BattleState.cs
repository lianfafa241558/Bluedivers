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

        /// <summary>【联机】本端是不是 **tick 伤害（破甲流血 / 异常状态跳伤）的权威端**。
        /// <para>▍为什么需要（2026-10-12）：tick 是从**已同步的状态**推导出来的（护甲破了 → 每拍流血；积蓄满 → 每拍跳伤），
        /// 而 <c>Damageable.Tick</c> / <c>Health.AboTick</c> 会**各自本地结算、再各自上报** ⇒ 同一段 tick 被 N 份上报叠加（N 端 ≈ N 倍伤害）。
        /// 所以钉成：**房主（或单机）本地结算并广播；成员端非玩家单位不本地扣血**（靠伤害广播落地，见 <c>EnemyNetBridge.ApplyRemoteDamage</c>）。</para>
        /// <para>⚠ 玩家/盟友是例外：他们的血量本来就是**本机权威**（自己管自己的血）⇒ 见 <see cref="ShouldApplyTickDamage"/>。</para>
        /// <para>写入方 = <c>WaveManager.Awake</c>（按角色）与 <c>EnemyNetBridge.OnHostLost</c>（房主没了 ⇒ 自己重新权威）。</para></summary>
        public static bool IsTickDamageAuthority { get; internal set; } = true;

        /// <summary>本端该不该本地结算这个单位的 tick 伤害 —— 见 <see cref="IsTickDamageAuthority"/>。</summary>
        /// <param name="isPlayerLike">是不是玩家 / 盟友（他们的血本机权威，任何角色下都要本地算）。</param>
        public static bool ShouldApplyTickDamage(bool isPlayerLike) => IsTickDamageAuthority || isPlayerLike;

        /// <summary>【联机】本端是不是 **载具伤害的权威端**（房主 / 单机）。
        /// <para>▍为什么必须单独钉（2026-10-14）：载具（机甲 / 可驾驶炮台）两端的**子弹是各自模拟的**，
        /// 敌人 AI 的子弹也两端各打一遍 ⇒ 各扣一次必然分叉（同一发被扣 N 份）。而载具又不像敌人那样有
        /// "成员上报 ⇒ 房主结算"的完整通道（它没有 <c>EnemyController</c>）⇒ 口径钉成：
        /// **AI 来源的载具伤害只有房主那台落地并广播**；**玩家侧来源的照样落地 + 上报**（那笔房主算不出来）。</para>
        /// <para>⚠ 与 <see cref="IsTickDamageAuthority"/> 刻意**分两位**：tick 的判据是"状态推导"、落点在
        /// <c>Damageable.Tick</c>；载具的判据是"子弹模拟"、落点在注入的 <c>Damageable.LocalDamageGate</c>；
        /// 合成一位会让两套例外互相牵连。</para>
        /// <para>写入方 = <c>WaveManager.Awake</c>（按角色）与 <c>EnemyNetBridge.OnHostLost</c>（房主没了 ⇒ 自己重新权威）。</para></summary>
        public static bool IsVehicleDamageAuthority { get; internal set; } = true;

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
            IsTickDamageAuthority = true;   // 未就绪 = 中性（单机）⇒ 本端自己算 tick
            IsVehicleDamageAuthority = true;   // 同理：未就绪 = 中性（单机）⇒ 载具伤害本端自己算
        }
    }
}
