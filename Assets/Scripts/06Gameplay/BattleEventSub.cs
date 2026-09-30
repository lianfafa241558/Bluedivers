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
public static class BattleEventSub
{
    /// <summary>呼叫凯伊</summary>
    public static event Action<GameObject, Vector3> OnCallKai;
    public static void CallKai(GameObject source, Vector3 point) => OnCallKai?.Invoke(source, point);

    // 【2026-09-30 拆分】单位/噪声/命中类事件已迁到 02Game/Game/UnitEventSub.cs（下层总线），
    // 原因见该文件头注释：这组事件的发布者都在单位内核，将来随 05_UnitCore 一起下沉。

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
    public static event Action<PEVector3> OnEvacuate;
    public static void Evacuate(PEVector3 pos) => OnEvacuate?.Invoke(pos);

    #endregion

    // 【2026-09-30 拆分】敌人/特殊单位/玩家 的创建与死亡事件已迁到 02Game/Game/UnitEventSub.cs；
    // 其中 OnFriendDead / OnFriendRevive 因全仓零订阅被直接删除（需要时可从 git 历史取回）。

    #region 团灭判负
    /// <summary>团灭判负倒计时，参数为剩余秒数</summary>
    public static event Action<float> OnWipeFailCountdown;
    public static void WipeFailCountdown(float remaining) => OnWipeFailCountdown?.Invoke(remaining);

    /// <summary>团灭判负倒计时取消（被救起或条件不再满足）</summary>
    public static event Action OnWipeFailCancel;
    public static void WipeFailCancel() => OnWipeFailCancel?.Invoke();

    //玩家创建在全局

    #endregion
}

// 【2026-09-30 拆分】NoiseData 结构已随 OnNoise 事件迁到 02Game/Game/UnitEventSub.cs。
}
