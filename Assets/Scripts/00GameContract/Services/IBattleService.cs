using System;
using System.Collections.Generic;
using PEMaths;
using UnityEngine;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 战斗 服务契约（2026-09-30 为拆出 <c>05_UnitCore</c> 而建）。
    ///
    /// <para>▍为什么需要它：<c>Damageable</c>（友伤减免/生命力强化）、<c>HealthPlayer</c>/<c>HealthShield</c>
    /// （护盾强化 +10）要查询"全队强化是否启用"，原先直接写 <c>BattleManager.Instance.HaveBooster(...)</c>。</para>
    ///
    /// <para>▍成员是**按玩法层实测调用面**逐个开的（`p5_recon.py` 量出来，只开真正用到的）：
    /// <c>FindUnits 12</c>、<c>Authorize 12</c>、<c>BattleRandom 8</c>、<c>CreatWave 7</c>、
    /// <c>AddBattleDataItem 5</c>、<c>ReleaseAirdrop 3</c>、<c>EnqueueInit 3</c>、<c>EndGame 2</c>、
    /// <c>CreatUnit 1</c>、<c>SubmitOOPart 1</c>。</para>
    ///
    /// <para>⚠ 实现方注意：<c>IsStartBattle</c>（字段）与 <c>EnqueueInit</c>（**静态**方法）无法隐式实现，
    /// 需在 <c>BattleManager</c> 里显式转发。</para>
    /// </summary>
    public interface IBattleService
    {
        /// <summary>某类全队强化是否已启用（未开战/无战斗管理器时返回 false）。</summary>
        bool HaveBooster(BoosterType type);

        /// <summary>按范围 + 配置找单位（与 <c>BattleManager.FindUnits</c> 同义）。</summary>
        List<I_Actor> FindUnits(IPERange range, TargetCfg targetCfg, Func<I_Actor, bool> customFilter = null);

        /// <summary>按配置找单位（不限范围）。</summary>
        List<I_Actor> FindUnits(TargetCfg targetCfg, Func<I_Actor, bool> customFilter = null);

        /// <summary>是否已开战。</summary>
        bool IsStartBattle { get; }

        /// <summary>
        /// 战斗服务**是否已就位**（实现方恒为 true；<see cref="NullServices"/> 空对象为 false）。
        ///
        /// <para>▍它等价于老的 <c>BattleManager.Instance != null</c> 判空 —— 用于"**战斗系统存在才做**"这类逻辑
        /// （典型：<c>ModifyTerrain</c> 在 `Awake`/`Start` 阶段就改地形，那时战斗还没开始，
        /// **不能用 <see cref="IsStartBattle"/> 代替**：空对象返回 false 会把整段逻辑跳过）。</para>
        /// </summary>
        bool IsPresent { get; }

        /// <summary>是否全队团灭。</summary>
        bool IsTeamWiped { get; }

        /// <summary>剩余增援数。</summary>
        int ReinforcementCount { get; }

        /// <summary>确定性战斗随机源（勿用 <c>UnityEngine.Random</c>）。</summary>
        System.Random BattleRandom { get; }

        /// <summary>把动作排进战斗初始化队列（原 <c>BattleManager.EnqueueInit</c> 是静态方法）。</summary>
        void EnqueueInit(Action action);

        /// <summary>在指定位置生成单位（返回实例根物体）。</summary>
        GameObject CreatUnit(UnitTier tier, Vector3 pos, float range, bool isFixed = true);

        /// <summary>按参数创建一波敌人（<see cref="WaveCreateParams"/> 已在契约层）。</summary>
        bool CreatWave(WaveCreateParams param);

        /// <summary>释放空投。</summary>
        void ReleaseAirdrop(Vector3 point, int id, Action<GameObject> action = default);

        /// <summary>释放空投（带角度）。</summary>
        void ReleaseAirdrop(Vector3 point, float angle, int id, Action<GameObject> action = default);

        /// <summary>授权 / 取消某战备。</summary>
        void Authorize(int id, bool state);

        /// <summary>记录战斗数据条目。</summary>
        void AddBattleDataItem(int playerIndex, string name);

        /// <summary>结束游戏。</summary>
        void EndGame(int delay, GameResult result = GameResult.Unknow);

        /// <summary>提交欧帕兹。</summary>
        void SubmitOOPart(GameObject user, OOPartEnum type, int count);

        /// <summary>在马点位置生成巡逻队（返回实例根物体；MissionOilRefining 用）。</summary>
        List<GameObject> CreatPatrol(Vector3 pos);

        /// <summary>当前波次（等价 <c>BattleManager.WaveCont.WaveCount</c>；MissionEradicate 用）。</summary>
        int WaveCount { get; }

        /// <summary>
        /// 暴露全图所有未结束任务（雷达站完成时用）。
        /// ▍把原先散在玩法层里"遍历 <c>MissionCont.missions</c> 并调 <c>entity.TryDiscovered()</c>"的行为
        /// 收进 <c>MissionController</c>，契约只暴露语义（下层不该看见 <c>MissionBase</c>）。
        /// </summary>
        void RevealAllMissions();
    }
}
