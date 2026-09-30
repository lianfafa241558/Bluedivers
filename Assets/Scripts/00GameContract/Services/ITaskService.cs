using System.Collections.Generic;
using FPSGame.Core;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 任务/难度 服务契约（2026-09-30 为拆出 <c>05_UnitCore</c> 而建）。
    ///
    /// <para>▍为什么需要它：<c>HealthEnemy</c>/<c>Damageable</c> 在 <c>Awake</c> 里要按任务难度缩放血量与护甲，
    /// 原先直接写 <c>TaskManager.Instance.nowTask.XXX</c> ⇒ 单位内核反向依赖 <c>01Manager</c>，程序集拆不动。
    /// 上层管理器把"下层真正需要的那几个成员"以接口形式暴露，由 <see cref="ServiceLocator"/> 转交。</para>
    ///
    /// <para>▍设计原则：<b>只开下层真正用到的成员</b>（不要图省事把整个管理器搬进接口）。
    /// 目前全仓只有 2 处消费（<c>Damageable.Awake</c>、<c>HealthEnemy.Awake</c>），所以只开 2 个成员。</para>
    /// </summary>
    public interface ITaskService
    {
        /// <summary>当前任务的额外难度系数（固定 4 项；索引 3 = 生命/护甲缩放）。
        /// <para>⚠ 约定：实现方<b>不得返回 null</b>，长度固定 4（无任务时返回全 0）。</para></summary>
        int[] ExtraDifficulty { get; }

        /// <summary>当前难度（无任务时返回 <c>DifficultyEnum.Normal</c>）。</summary>
        DifficultyEnum Difficulty { get; }

        /// <summary>
        /// 本局需收集的欧帕兹数量表（= `nowTask.collectProperty`；无任务时返回空表而非 null）。
        /// ▍玩法层 3 处用它（`Furniture_KeiSubmit` ×2、`OOPart` ×1），原先写 `TaskManager.Instance.nowTask.collectProperty`。
        /// </summary>
        Dictionary<OOPartEnum, int> CollectProperty { get; }

        /// <summary>本局敌人种类（= `TaskManager.EnemyVarietyType`；MissionBase 用它选预制体）。</summary>
        EnemyVarietyType EnemyVarietyType { get; }

        // —— 以下 3 个成员为 `MedivacController`（撤离点）下沉玩法层时补（2026-10-01）——
        // 它原先直连 `TaskManager.Instance.nowTask`；而 `nowTask` 的类型 `SelectTaskData` 现在属**玩法层**
        // ⇒ 契约层无法命名它，只能按"窄投影"开放真正用到的这 3 样。

        /// <summary>是否已有当前任务（= `nowTask != null`；无任务时为 false）。</summary>
        bool HasTask { get; }

        /// <summary>当前任务的倒计时读数（无任务时读 0、写忽略）。</summary>
        int Countdown { get; set; }

        /// <summary>进入任务过渡（= `TaskManager.EnterTransition()`）。</summary>
        void EnterTransition();
    }
}
