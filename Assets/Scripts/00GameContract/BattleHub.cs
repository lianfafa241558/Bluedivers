using System;
using System.Collections.Generic;
using UnityEngine;

// 只允许"装配方"（= 09_Managers 的 BattleManager）写入 Current；下层（05_UnitCore/06_Gameplay/10_UI…）只能读。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]

namespace FPSGame.GameContract
{
    /// <summary>
    /// 战斗服务的**唯一入口**（2026-10-01 由「服务定位器 <c>ServiceLocator</c>」降格而来）。
    ///
    /// <para>▍为什么只剩它：2026-10-01 的逐槽收缩把另外 9 个槽全部拆掉了（`Archive` 数据自持 / `Net`→<c>LogicFrame</c>
    /// / `Path`→事件 / `Room`→<c>TeamState</c> / `Wnd`→数据+事件 / `Task`→<c>TaskState</c> / `Vfx`→<c>VfxPool</c>
    /// / `Res`→装配根注入 / `Flow`→<c>FlowState</c>+<c>TimerHost</c>+事件；`Battle` 自身也做了四轮瘦身：
    /// 死成员删除、纯读值 → <c>FPSGame.Data.BattleState</c>、无返回值命令 → <c>BattleEventSub</c>、
    /// <c>FindUnits</c> → <c>FPSGame.Game.UnitQuery</c>）⇒ "槽位/定位器"这层抽象只剩一个实例，名字便不再贴切。</para>
    ///
    /// <para>▍⚠ 为什么**不能彻底删掉这个接缝**：剩下的 5 个成员全都"**需要返回值或回调**"
    /// —— <c>CreatWave</c>(<c>bool</c>)、<c>ReleaseAirdrop</c>(带 <c>Action&lt;GameObject&gt;</c>)、
    /// <c>CreatPatrol</c>(<c>List&lt;GameObject&gt;</c>)、<c>WaveCount</c>（真相会递减，不适合快照）、
    /// <c>IsPresent</c>(存在性判断)，事件下沉与数据自持都覆盖不了。⇒ 它是**合理保留**，不是过渡物。</para>
    ///
    /// <para>▍用法：调用方直接写 <c>BattleHub.Current.Xxx(...)</c>（<see cref="Current"/> 默认指向
    /// <see cref="NullBattleService.Instance"/> ⇒ 永不 null，服务未就绪时取中性值、不抛）；
    /// 要区分"战斗系统是否就位"读 <see cref="IBattleService.IsPresent"/>，**不要写 <c>!= null</c>**。</para>
    ///
    /// <para>▍注销义务（注册必须成对）：<c>BattleManager.OnDestroy</c> 里
    /// <c>if (ReferenceEquals(BattleHub.Current, this)) BattleHub.Current = NullBattleService.Instance;</c>
    /// —— 接口引用**不走 Unity 的 <c>==null</c> 重载**，槽位一旦指向已销毁的实例不会自动回落。
    /// 身份判定用于防止"新实例已接管、旧实例随后销毁"时误清新实例。</para>
    /// </summary>
    public static class BattleHub
    {
        /// <summary>当前战斗服务实现（未注册时 = <see cref="NullBattleService.Instance"/>）。</summary>
        public static IBattleService Current { get; internal set; } = NullBattleService.Instance;

        /// <summary>
        /// 排查用：当前指向哪个实现类型，并附上下游原语的宿主（与 <c>VfxPool.Dump()</c>/<c>TimerHost.Dump()</c> 同一套）。
        /// ⚠ 单位查询入口 <c>FPSGame.Game.UnitQuery.Dump()</c> 在 `05_UnitCore`，契约层看不到它 ⇒ 需手动调。
        /// </summary>
        public static string Dump()
        {
            return "BattleHub.Current = " + Current.GetType().Name
                 + " | " + FPSGame.Core.VfxPool.Dump()
                 + " | " + FPSGame.Core.TimerHost.Dump();
        }
    }

    /// <summary>
    /// <see cref="BattleHub.Current"/> 未注册时的**空对象**（中性值：布尔 false / 数值 0 / 集合空 / 对象 null / 命令空操作）。
    /// </summary>
    internal sealed class NullBattleService : IBattleService
    {
        /// <summary>空对象单例（默认值来源，避免每次 new）。</summary>
        public static readonly NullBattleService Instance = new NullBattleService();

        private NullBattleService() { }

        /// <summary>空对象 ⇒ 服务未就位（对应老写法 `BattleManager.Instance != null` 为 false）。</summary>
        public bool IsPresent => false;

        public bool CreatWave(WaveCreateParams param) => false;

        public void ReleaseAirdrop(Vector3 point, int id, Action<GameObject> action = default) { }

        public void ReleaseAirdrop(Vector3 point, float angle, int id, Action<GameObject> action = default) { }

        /// <summary>⚠ 每次返回**新实例**，避免调用方 <c>Remove</c> 污染共享列表。</summary>
        public List<GameObject> CreatPatrol(Vector3 pos) => new List<GameObject>();

        public int WaveCount => 0;
    }
}
