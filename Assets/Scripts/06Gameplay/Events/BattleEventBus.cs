using System;
using System.Collections.Generic;
using FPSGame.Core.Interface;
using FPSGame.Mission;
using FPSGame.GameContract;
using PEMaths;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Data;
using FPSGame.Gameplay;

    namespace FPSGame.Gameplay
    {
    public static class BattleEventBus
    {
        /// <summary>呼叫凯伊</summary>
        public static event Action<GameObject, Vector3> OnCallKai;
        public static void CallKai(GameObject source, Vector3 point) => OnCallKai?.Invoke(source, point);


        #region 空投
        /// <summary>空投授权状态变化</summary>
        public static event Action OnAuthorizeAirdrop;
        public static void AuthorizeAirdrop() => OnAuthorizeAirdrop?.Invoke();

        /// <summary> 空投输入 </summary>
        public static Action<List<DirectionEnum>> OnInputAirdrop;
        public static void InputAirdrop(List<DirectionEnum> inputs) => OnInputAirdrop?.Invoke(inputs);



        /// <summary>完成选择空投</summary>
        public static event Action<GameObject, AirdropData> OnSelectAirdrop;
        public static void SelectAirdrop(GameObject go, AirdropData data) => OnSelectAirdrop?.Invoke(go, data);

        /// <summary>取消空投</summary>
        public static event Action<GameObject, AirdropData> OnCancelAirdrop;
        public static void CancelAirdrop(GameObject go, AirdropData data) => OnCancelAirdrop?.Invoke(go, data);

        /// <summary>建立空投信标(发射物,信标物体,位置,使用的空投</summary>
        public static event Action<GameObject, GameObject, Vector3, AirdropData> OnAirdrop;

        /// <summary>建立空投信标</summary>
        /// <param name="source">发射物?/param>
        /// <param name="beacon">信标物体</param>
        /// <param name="point">位置</param>
        /// <param name="data">使用的空投/param>
        public static void Airdrop(GameObject source, GameObject beacon, Vector3 point, AirdropData data) => OnAirdrop?.Invoke(source, beacon, point, data);


        #endregion

        #region 喊话（联机同步用）
        /// <summary>本机玩家**喊话**（<c>PlayerSpeechManager</c> 是唯一触发点，含"空投/标记/受击/会面"等）。
        /// <para>▍为什么要抛事件：喊话本体在 <c>06_Gameplay</c>（看不见 <c>02_Net</c>），
        /// 由 09 的联机桥订阅后上行 ⇒ 别的客户端才能听到/看到盟友喊话（2026-10-07 用户实测"没有同步"）。</para>
        /// <para>参数：喊话者（本机玩家 GameObject）/ <c>SpeechTypeEnum</c> 的 int。</para></summary>
        public static event Action<GameObject, int> OnPlayerSpeech;
        public static void PlayerSpeech(GameObject speaker, SpeechTypeEnum type) => OnPlayerSpeech?.Invoke(speaker, (int)type);
        #endregion

        #region 流程

        /// <summary>任务创建时/summary>
        public static event Action<MissionBase> OnMissionStart;
        public static void MissionStart(MissionBase mission) => OnMissionStart?.Invoke(mission);

        /// <summary>任务完成时/summary>
        public static event Action<MissionBase> OnMissionCompleted;
        public static void MissionCompleted(MissionBase mission) => OnMissionCompleted?.Invoke(mission);

        /// <summary>任务失败时/summary>
        public static event Action<MissionBase> OnMissionFail;
        public static void MissionFail(MissionBase mission) => OnMissionFail?.Invoke(mission);

        /// <summary>任务结束时 不管胜利还是失败)</summary>
        public static event Action<MissionBase> OnMissionEnd;
        public static void MissionEnd(MissionBase mission) => OnMissionEnd?.Invoke(mission);

        /// <summary>任务更新时 任务/是否刷新整个UI)</summary>
        public static event Action<MissionBase> OnMissionUpdate;
        public static void MissionUpdate(MissionBase mission) => OnMissionUpdate?.Invoke(mission);

        /// <summary>任务显示状态变化时(任务/状态任务窗口使用</summary>
        public static event Action<MissionBase, bool> OnMissionStateChange;
        /// <summary>任务显示状态变化时(任务/状态任务窗口使用</summary>
        public static void MissionStateChange(MissionBase mission, bool state) => OnMissionStateChange?.Invoke(mission, state);

        /// <summary>任务暴露时，小地图使用/summary>
        public static event Action<IEntity> OnMissionEntityShow;
        /// <summary>任务暴露时，小地图使用/summary>
        public static void MissionEnityShow(IEntity mission) => OnMissionEntityShow?.Invoke(mission);


        /// <summary> 开始撤离</summary>
        public static event Action<PEVector3> OnEvacuate;
        public static void Evacuate(PEVector3 pos) => OnEvacuate?.Invoke(pos);

        #endregion


        #region 团灭判负
        /// <summary>团灭判负倒计时，参数为剩余秒数</summary>
        public static event Action<float> OnWipeFailCountdown;
        public static void WipeFailCountdown(float remaining) => OnWipeFailCountdown?.Invoke(remaining);

        /// <summary>团灭判负倒计时取消（被救起或条件不再满足）</summary>
        public static event Action OnWipeFailCancel;
        public static void WipeFailCancel() => OnWipeFailCancel?.Invoke();

        //玩家创建在全局

        #endregion

 
        #region 战斗服务命令（原 IBattleService）
        /// <summary>结束游戏（延迟秒数 / 结果）</summary>
        public static event Action<int, GameResult> OnEndGame;
        public static void EndGame(int delay, GameResult result = GameResult.Unknow) => OnEndGame?.Invoke(delay, result);

        /// <summary>提交欧帕兹（提交者 / 类型 / 数量）</summary>
        public static event Action<GameObject, OOPartEnum, int> OnSubmitOOPart;
        public static void SubmitOOPart(GameObject user, OOPartEnum type, int count) => OnSubmitOOPart?.Invoke(user, type, count);

        /// <summary>暴露全图所有未结束的任务（雷达站完成时触发）</summary>
        public static event Action OnRevealAllMissions;
        public static void RevealAllMissions() => OnRevealAllMissions?.Invoke();

        /// <summary>记录战斗数据条目（玩家序号 / 条目名）</summary>
        public static event Action<int, string> OnAddBattleDataItem;
        public static void AddBattleDataItem(int playerIndex, string name) => OnAddBattleDataItem?.Invoke(playerIndex, name);

        /// <summary>
        /// **请求**授权 / 取消某战备（战备 Id / 是否授权）。
        /// ⚠ 与上方 <see cref="OnAuthorizeAirdrop"/> 区分：那个是授权**状态变化后的广播**（订阅方刷新 UI），
        /// 这个是**下达命令**（订阅方 = <c>BattleManager.Authorize</c> 去改计数）。
        /// </summary>
        public static event Action<int, bool> OnRequestAuthorize;
        public static void RequestAuthorize(int id, bool state) => OnRequestAuthorize?.Invoke(id, state);
        #endregion

        // 【2026-10-07 联机战斗同步】玩法层（06）拿不到网络层 ⇒ 这里只做"意图/结果的广播"，
        // 实际收发由 09 的联机桥订阅（方向与上面那批一致：发布者都在玩法层）。
        #region 联机战斗同步（移动意图 / 命中上报）
        /// <summary>【房主】某只怪要走向哪里（NetId / 目标点）</summary>
        public static event Action<int, Vector3> OnEnemyMove;
        public static void EnemyMove(int netId, Vector3 destination) => OnEnemyMove?.Invoke(netId, destination);

        /// <summary>【成员】本机打中了某只怪（NetId / 伤害）⇒ 上报房主结算（血量/死亡以房主为准）</summary>
        public static event Action<int, int> OnEnemyHit;
        public static void EnemyHit(int netId, int damage) => OnEnemyHit?.Invoke(netId, damage);

        /// <summary>【房主】**我**打中了某只怪（NetId / 伤害）⇒ 下发给成员，让他们那份副本一起掉血。
        /// <para>▍为什么与 <see cref="OnEnemyHit"/> 分两条：那条是"成员往上报"，这条是"房主往下播" ——
        /// 两边都往一条事件里塞会让订阅方分不清方向（同一个 `_OnDamaged` 里按角色二选一发射）。</para></summary>
        public static event Action<int, int> OnEnemyDamaged;
        public static void EnemyDamaged(int netId, int damage) => OnEnemyDamaged?.Invoke(netId, damage);

        /// <summary>【房主】某只怪死了（NetId）⇒ 成员照此干掉自己那份副本</summary>
        public static event Action<int> OnEnemyDiedRemote;
        public static void EnemyDiedRemote(int netId) => OnEnemyDiedRemote?.Invoke(netId);

        /// <summary>【房主】场景单位（NPC 这类**由场景摆好**、会自己走动的氛围单位）要走向哪 / 停下；键 = <c>Actor.Id</c>。
        /// <para>▍为什么另开一条：它们没有 AI 控制器、也不在 <c>ActorsManager.Enemys</c> 里 ⇒ 套不上敌人那条 4024；
        /// 而它们的游荡用的是**各端各自的随机** ⇒ 两边看到的 NPC 永远不在同一处。这里只发"意图"，
        /// 由 09 的落地桥（<c>SceneUnitMoveSink</c>，挂对应场景）转发上网络、并把房主下发的目标点应用回本端 NPC。</para></summary>
        public static event Action<string, Vector3, bool> OnSceneUnitMove;
        public static void SceneUnitMove(string id, Vector3 destination, bool stop = false) => OnSceneUnitMove?.Invoke(id, destination, stop);
        #endregion
    }

}
