using System;
using System.Collections.Generic;
using PEMaths;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 战斗 服务契约（2026-09-30 为拆出 <c>05_UnitCore</c> 而建）。
    ///
    /// <para>▍为什么需要它：玩法层（<c>06_Gameplay</c>）不引用 <c>09_Managers</c> ⇒ 生波 / 释放空投 / 取巡逻队
    /// 这类"**需要返回值**"的战斗能力，编译期写不出 <c>BattleManager.Instance</c>，只能由本契约承接
    /// （配合 <see cref="BattleHub"/>）；**无返回值的命令**走事件、**纯读值**走数据自持，
    /// 都不再占用契约（见下方 ②③）。</para>
    ///
    /// <para>▍成员是**按玩法层实测调用面**逐个开的（2026-10-01 复核，只开真正用到的）：
    /// <c>CreatWave 8</c>、<c>ReleaseAirdrop 5</c>、<c>WaveCount 2</c>、<c>CreatPatrol 1</c>。</para>
    ///
    /// <para>⚠ 2026-10-01 四轮瘦身：
    /// ① 删 <c>EnqueueInit</c>/<c>CreatUnit</c>/<c>IsTeamWiped</c>/<c>ReinforcementCount</c> —— **下层消费点 = 0**
    ///   （真实调用方 <c>10_Effect/CreatOOPart</c>、<c>CreateSupple</c>、<c>CreateBuilding</c> 走静态
    ///   <c>BattleManager.EnqueueInit</c>，<c>CreatEnemy</c>、<c>04UI/DeathUI</c> 走 <c>BattleManager.Instance</c>，
    ///   都没走契约 ⇒ 删后静态方法/属性本身仍在）；
    /// ② 删 <c>HaveBooster</c>(7) / <c>IsStartBattle</c>(2) / <c>BattleRandom</c>(7) —— 属**纯读值** ⇒ 按「数据自持」
    ///   改读 <c>FPSGame.Data.BattleState</c>（<c>HealthPlayer</c>/<c>HealthShield</c> 在 <c>Awake</c> 里读强化，
    ///   注入根本来不及，数据自持天然免疫；<c>BattleManager</c> 的方法/字段本身保留）；
    /// ③ 删 <c>EndGame</c>(2) / <c>SubmitOOPart</c>(1) / <c>RevealAllMissions</c>(1) / <c>AddBattleDataItem</c>(5)
    ///   / <c>Authorize</c>(12) —— 都是**无返回值的命令** ⇒ 按「事件下沉」改走 <c>FPSGame.Gameplay.BattleEventSub</c>
    ///   （发布者都在玩法层、订阅者 <c>BattleManager</c> 在 09_Managers ⇒ 总线放 min 层 = 06_Gameplay；
    ///   订阅方 Awake 订阅 / OnDestroy 退订，重复实例不得订阅）；
    /// ④ 删 <c>FindUnits</c>(14) —— 查询设施 <c>UnitQueryGrid</c> **本就在 05_UnitCore**（数据节点 <c>UnitQueryGridNode</c>
    ///   更在契约层），只有"**实例**"挂在 <c>BattleManager</c> ⇒ "设施在 05、入口在 09"违反「设施与入口同层」，
    ///   故把入口搬回 05：<c>FPSGame.Game.UnitQuery</c>（静态入口 + <c>Sink</c> + <c>internal set</c>，
    ///   与 <c>VfxPool</c>/<c>TimerHost</c>/<c>LogicFrame</c> 同款第 4 例）。</para>
    /// </summary>
    public interface IBattleService
    {
        /// <summary>
        /// 战斗服务**是否已就位**（实现方恒为 true；<see cref="NullBattleService"/> 空对象为 false）。
        ///
        /// <para>▍它等价于老的 <c>BattleManager.Instance != null</c> 判空 —— 用于"**战斗系统存在才做**"这类逻辑
        /// （典型：<c>ModifyTerrain</c> 在 `Awake`/`Start` 阶段就改地形，那时战斗还没开始，
        /// **不能用"是否已开战"（<c>FPSGame.Data.BattleState.IsStartBattle</c>）代替**：
        /// 未开战时它是 false，会把整段逻辑跳过）。</para>
        ///
        /// <para>▍2026-10-01：原先 10 处"恒真判空"（旧写法 <c>ServiceLocator.Battle != null</c> 4 处、
        /// <c>?.AddBattleDataItem</c> 1 处、<c>battleSvc == null</c> 2 处、<c>IsValid()</c> 3 处）统一改为读本属性
        /// —— 入口默认指向 <see cref="NullBattleService"/> 空对象、永不 null，那些判空在 P5 批量替换后已失去意义。
        /// 其中 4 处随后随 `<c>HaveBooster</c>` 改数据自持、1 处随 `<c>AddBattleDataItem</c>` 改事件而**进一步消失**；
        /// 现存 4 处消费点：<c>ModifyTerrain</c>（改地形）、<c>WeaponBaseController</c>（发枪声噪声）、
        /// <c>DetectionModule</c> 与 <c>EnemyMobile_AboState</c>（检测 / 锁定）—— 都是"战斗系统存在才做"。</para>
        /// </summary>
        bool IsPresent { get; }

        /// <summary>按参数创建一波敌人（<see cref="WaveCreateParams"/> 已在契约层）。</summary>
        bool CreatWave(WaveCreateParams param);

        /// <summary>释放空投。</summary>
        void ReleaseAirdrop(Vector3 point, int id, Action<GameObject> action = default);

        /// <summary>释放空投（带角度）。</summary>
        void ReleaseAirdrop(Vector3 point, float angle, int id, Action<GameObject> action = default);

        /// <summary>在马点位置生成巡逻队（返回实例根物体；MissionOilRefining 用）。</summary>
        List<GameObject> CreatPatrol(Vector3 pos);

        /// <summary>当前波次（等价 <c>BattleManager.WaveCont.WaveCount</c>；MissionEradicate 用）。</summary>
        int WaveCount { get; }
    }
}
