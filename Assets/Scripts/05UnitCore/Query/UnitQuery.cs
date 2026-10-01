using System;
using System.Collections.Generic;
using FPSGame.GameContract;
using PEMaths;

// 只允许"装配方"（= 09_Managers 的 BattleManager）写入 Sink；本程序集与更上层只能读。
// 与 00_Core 的 VfxPool / TimerHost / LogicFrame 同一套写入保护手法。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("09_Managers")]

namespace FPSGame.Game
{
    /// <summary>
    /// 单位空间查询的**全局访问入口**（2026-10-01 为把 <c>IBattleService.FindUnits</c> 从契约里摘掉而建）。
    ///
    /// <para>▍为什么可以下沉回 <c>05_UnitCore</c>：查询设施 <see cref="UnitQueryGrid"/> 本来就住在本程序集
    /// （`05UnitCore/UnitQueryGrid.cs`），数据节点 <c>UnitQueryGridNode</c> 更在契约层
    /// —— 只有"**实例**"被 <c>BattleManager</c>（09_Managers）创建并喂单位，属"**设施在 05、入口在 09**"，
    /// 恰好违反「设施与入口同层」。本类把入口搬回设施所在层，与 <c>FPSGame.Core.VfxPool</c>/<c>TimerHost</c>/
    /// <c>LogicFrame</c> 同款（静态入口 + <c>Sink</c> + <c>internal set</c> + <c>InternalsVisibleTo</c>）⇒ 该形态第 4 例。</para>
    ///
    /// <para>▍生命周期：<c>BattleManager.Start()</c> 构造 grid 后 <c>UnitQuery.Sink = this</c>；
    /// <c>OnDestroy</c> 里**身份判定**通过后归还 <c>null</c>。未接管时 <see cref="FindUnits"/> 返回**空列表**
    /// （与原 <c>NullBattleService.FindUnits</c> 的中性值逐字一致）。</para>
    ///
    /// <para>▍为什么不能走事件：调用方要**同步拿到 <c>List&lt;I_Actor&gt;</c>**（还要 <c>Remove</c>/<c>Count</c>），
    /// 事件没有返回值。</para>
    /// </summary>
    public static class UnitQuery
    {
        /// <summary>真正的查询实现（= <c>BattleManager</c>，它持有并维护 <see cref="UnitQueryGrid"/>）。</summary>
        public static IUnitQuerySink Sink { get; internal set; }

        /// <summary>查询入口是否已被接管（= 战斗网格已建好）。</summary>
        public static bool IsReady => Sink != null;

        /// <summary>按范围 + 配置找单位；未接管时返回空列表。</summary>
        public static List<IActor> FindUnits(IPERange range, TargetCfg targetCfg, Func<IActor, bool> customFilter = null)
            => Sink != null ? Sink.FindUnits(range, targetCfg, customFilter) : new List<IActor>();

        /// <summary>按配置找单位（不限范围）；未接管时返回空列表。</summary>
        public static List<IActor> FindUnits(TargetCfg targetCfg, Func<IActor, bool> customFilter = null)
            => Sink != null ? Sink.FindUnits(targetCfg, customFilter) : new List<IActor>();

        /// <summary>排查"查询入口是否已接管"（与 <c>VfxPool.Dump()</c> 同款）。</summary>
        public static string Dump() => "UnitQuery: " + (Sink == null ? "<未接管>" : Sink.GetType().Name);
    }

    /// <summary>
    /// 查询实现方的最小接口 —— 让 <see cref="UnitQuery"/> 不必依赖 <c>BattleManager</c> 具体类型（跨程序集也编不过）。
    /// </summary>
    public interface IUnitQuerySink
    {
        /// <summary>按范围 + 配置找单位。</summary>
        List<IActor> FindUnits(IPERange range, TargetCfg targetCfg, Func<IActor, bool> customFilter = null);

        /// <summary>按配置找单位（不限范围）。</summary>
        List<IActor> FindUnits(TargetCfg targetCfg, Func<IActor, bool> customFilter = null);
    }
}
