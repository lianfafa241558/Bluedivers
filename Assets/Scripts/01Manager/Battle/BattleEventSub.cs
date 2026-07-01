using System;
using System.Collections.Generic;
using Core.Interface;
using FpsGame.Mission;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using static AirdropController;

public static class BattleEventSub
{
    /// <summary>呼叫凯伊</summary>
    public static event Action<GameObject, Vector3> OnCallKai;
    public static void CallKai(GameObject source, Vector3 point) => OnCallKai?.Invoke(source, point);

    public static event Action<I_Actor> OnUnitPosChange;
    /// <summary>单位位置改变</summary>
    public static void UnitPosChange(I_Actor unit)=> OnUnitPosChange?.Invoke(unit);

    /// <summary>单位死亡</summary>
    public static event Action<Actor> OnUnitDeath;
    public static void UnitDeath(Actor unit) => OnUnitDeath?.Invoke(unit);

    /// <summary>单位被杀死/summary>
    public static event Action<Actor, Actor> OnUnitKill;
    public static void UnitKill(Actor attacker, Actor victim) => OnUnitKill?.Invoke(attacker, victim);

    /// <summary>单位被击中(受击者，来源)</summary>
    public static event Action<GameObject, GameObject> OnUnitHit;
    public static void UnitHit(GameObject victim, GameObject attacker) => OnUnitHit?.Invoke(victim, attacker);


    /// <summary>子弹击中地面(近战，超射程消失也算)</summary>
    public static event Action<GameObject, Vector3> OnBulletHit;
    /// <summary>子弹击中地面(近战，超射程消失也算)</summary>
    public static void BulletHit(GameObject source, Vector3 pos) => OnBulletHit?.Invoke(source, pos);



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
    public static event Action<I_Entity> OnMissionEntityShow;
    /// <summary>任务暴露时，小地图使用/summary>
    public static void MissionEnityShow(I_Entity mission) => OnMissionEntityShow?.Invoke(mission);


    /// <summary> 开始撤离</summary>
    public static event Action OnEvacuate;
    public static void Evacuate() => OnEvacuate?.Invoke();

    #endregion

    #region 单位
    /// <summary>敌人被创建</summary>
    public static event Action<Actor> OnEnemyCreate;
    public static void EnemyCreate(Actor unit) => OnEnemyCreate?.Invoke(unit);

    /// <summary>敌人死亡</summary>
    public static event Action<Actor> OnEnemyDead;

    /// <summary>敌人死亡 </summary>
    public static void EnemyDead(Actor unit) => OnEnemyDead?.Invoke(unit);

    /// <summary> 特殊单位被创建</summary>
    public static event Action<Actor> OnSpecUnitCreate;
    public static void SpecUnitCreate(Actor unit) => OnSpecUnitCreate?.Invoke(unit);
    /// <summary> 特殊单位倒地 </summary>
    public static event Action<Actor> OnSpecUnitDead;
    public static void SpecUnitDead(Actor unit) => OnSpecUnitDead?.Invoke(unit);

    //盟友创建在全局

    /// <summary> 盟友倒地 </summary>
    public static event Action<Actor> OnFriendDead;
    public static void FriendDead(Actor unit) => OnFriendDead?.Invoke(unit);

    /// <summary>玩家倒地</summary>
    public static event Action<Actor> OnPlayerDead;
    public static void PlayerDead(Actor unit) => OnPlayerDead?.Invoke(unit);

    /// <summary>玩家复活 </summary>
    public static event Action<Actor> OnPlayerRevive;
    public static void PlayerRevive(Actor unit) => OnPlayerRevive?.Invoke(unit);

    //玩家创建在全局

    #endregion
}