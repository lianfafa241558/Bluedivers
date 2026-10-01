
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
// 写入方 = 09_Managers 的 TaskManager（与 RoomState/UIState/LogicFrame 同一套写入保护手法）。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]


namespace FPSGame.Data
{
    /// <summary>
    /// 任务 / 难度的**数据自持点**（2026-10-01 取代 <c>ServiceLocator.Task</c> 槽 + <c>ITaskService</c> 契约）。
    ///
    /// <para>▍为什么可行：`ServiceLocator.Task` 的 8 个消费点里 **7 个是"读值"**
    /// （`Damageable`/`HealthEnemy`/`FpsHelper_Extension` 读难度系数、`MissionBase` 读敌人种类、
    /// `MissionEradicate` 读难度、`Furniture_KeiSubmit`/`OOPart` 读写欧帕兹收集表），
    /// 只有 `MedivacController` 涉及"推进倒计时 + 请求切到配置战备阶段" ⇒ 值走数据自持；
    /// 切阶段由它自己走事件 `GlobalEventSub.RequestGameState(GameStateEnum.Armament)`
    /// （本类为此**不**提供任何命令；`Countdown` 的复位归"开始新一局" = `TaskManager.SetTask()`）。</para>
    ///
    /// <para>▍为什么不能靠注入：`Damageable`/`HealthEnemy` 是在 <c>Awake</c> 里读难度系数的
    /// —— 而 <c>Awake</c> 在 <c>Instantiate</c> 内部就跑完，**晚注入来不及**；数据自持天然没有这个时序问题。</para>
    ///
    /// <para>▍写入约定（重要）：`TaskManager` 必须在**所有**会改这些值的地方调用 <c>SyncTaskState()</c>
    /// —— 目前 2 处：`Init()`（建任务）与 `SetTask()`（选图/难度）。漏一处就会读到过期值。</para>
    ///
    /// <para>▍中性值（对齐原 <c>NullTaskService</c> 语义，供"服务未就绪"时使用）：
    /// 系数全 0、难度 <c>Normal</c>、敌人种类 <c>default</c>、收集表空、<c>HasTask=false</c>、<c>Countdown=0</c>。</para>
    /// </summary>
    public static class TaskState
    {
        /// <summary>当前任务的额外难度系数（**固定 4 项**，索引 3 = 生命/护甲缩放、2 = 任务进度、0 = 伤害）。
        /// <para>⚠ 契约约定：**永不为 null**，长度固定 4。</para></summary>
        public static int[] ExtraDifficulty { get; internal set; } = new int[4];

        /// <summary>当前难度（无任务时 = <c>Normal</c>）。</summary>
        public static DifficultyEnum Difficulty { get; internal set; } = DifficultyEnum.Normal;

        /// <summary>本局敌人种类（无任务时 = <c>default</c>）。</summary>
        public static EnemyVarietyType EnemyVarietyType { get; internal set; }

        /// <summary>
        /// 本局"需提交给凯伊"的欧帕兹累计表（无任务时 = 空表，**永不为 null**）。
        ///
        /// <para>⚠ 这是**活引用**（不是快照）：`OOPart.Operate()` 会直接往里累加
        /// （`dic.TryAdd(type,count)` / `dic[type] += count`），而该字段在 `SelectTaskData` 里初始化后
        /// **再未被重新赋值**（已核对全仓）⇒ 传引用安全、写读一致。</para>
        /// </summary>
        public static Dictionary<OOPartEnum, int> CollectProperty { get; internal set; } = new Dictionary<OOPartEnum, int>();

        /// <summary>是否已有当前任务（无任务时 false）。</summary>
        public static bool HasTask { get; internal set; }

        /// <summary>
        /// 撤离倒计时读数（= 原 <c>SelectTaskData.Countdown</c>）。
        ///
        /// <para>⚠⚠ **本类唯一允许下层写入的字段**（所以是 public set 而不是 <c>internal set</c>）：
        /// 由 `MedivacController`（06_Gameplay 的撤离点）在撤离流程里推进
        /// （`--TaskState.Countdown` / 按强化项 `= Mathf.RoundToInt(16*mul)`），`TaskManager` 与倒计时 UI 只读它。
        /// 它是一块**被多方共享的运行时读数**，不是配置数据。</para>
        ///
        /// <para>▍复位到 16 的唯一位置 = **开始新一局**（`TaskManager.SetTask()`，原始位置就是它的
        /// <c>task.Countdown = 16</c>）—— 而不是"撤离结束"：撤离点一局只走一次该分支，把复位挂在"结束"上
        /// 会诱导后人以为"每次过渡都要复位"。</para>
        /// </summary>
        public static int Countdown { get; set; }
    }
}
