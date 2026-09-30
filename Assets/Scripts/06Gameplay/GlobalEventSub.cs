using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Mission;
using FPSGame.Furn;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Data;
using FPSGame.Gameplay;

namespace FPSGame.Gameplay
{
public static class GlobalEventSub
{

    public static event Action<GameStateEnum, GameStateEnum> OnGameStateChange;

    public static void SceneChange(GameStateEnum oldState,GameStateEnum newState)
    {
        OnGameStateChange?.Invoke(oldState, newState);
    }

    public static event Action<float, float> OnTimeScaleChange;
    public static void TimeScaleChange(float oldSpeed, float newSpeed)
    {
        OnTimeScaleChange?.Invoke(oldSpeed, newSpeed);
    }


    public static event Action<string> OnSceneChange;
    /// <summary>切换场景</summary>
    public static void SceneChange(string name)
    {
        OnSceneChange?.Invoke(name);
    }

    public static event Action<string,bool> OnWndSwitch;
    /// <summary>窗口状态变化时</summary>
    public static void WndSwitch(string name, bool state)
    {
        OnWndSwitch?.Invoke(name,state);
    }

    public static event Action<bool> OnDaySwitch;

    /// <summary>
    /// 最近一次昼夜状态（true=白天）；null 表示本次运行还没产生过昼夜事件。
    /// 用于晚于事件触发才创建/订阅的模块（如 BattleManager）在初始化完成后补一次初始状态，
    /// 因为场景里的 DayNightBrain.Start 早于 BattleManager 创建，开局那次事件会被漏掉。
    /// </summary>
    public static bool? LastDaySwitchIsNoon { get; private set; }

    /// <summary>昼夜交替时</summary>
    public static void DaySwitch(bool isNoon)
    {
        //Debug.LogError($"昼夜交替事件触发，当前状态：{(isNoon ? "白天" : "夜晚")}");
        LastDaySwitchIsNoon = isNoon;
        OnDaySwitch?.Invoke(isNoon);
    }

    #region 存档和设置
    public static event Action<string,float> OnSettingCange;
    public static void SettingCange(string key, float value) => OnSettingCange?.Invoke(key,value);
    #endregion


    #region 玩家相关

    /// <summary>
    /// 标记点位
    /// </summary>
    public static event Action<GameObject, GameObject, Vector3> OnMark;
    public static void Mark(GameObject owner, GameObject target, Vector3 point) => OnMark?.Invoke(owner, target, point);


    /// <summary>
    /// 获得经验(角色ID,等级，经验系数
    /// </summary>
    public static Action<string, int, float> OnGainExp;

    /// <summary>
    /// 切换角色(角色ID,等级，经验系数
    /// </summary>
    public static Action<PlayerController> OnSwitchRole;

    /// <summary>
    /// 舰桥选人界面切换角色预览
    /// </summary>
    public static event Action<RoleData_SO> OnSelectRolePreview;
    public static void SelectRolePreview(RoleData_SO data) => OnSelectRolePreview?.Invoke(data);


    /// <summary>
    /// 交互家具
    /// </summary>
    public static event Action<GameObject, IFurniture> OnFurnitureOperate;
    public static void FurnitureOperate(GameObject user, IFurniture furniture) => OnFurnitureOperate?.Invoke(user, furniture);

    /// <summary>
    /// 玩家视角切换（第一/第三人称）
    /// </summary>
    public static event Action<bool> OnViewSwitch;
    public static void ViewSwitch(bool isThirdPerson) => OnViewSwitch?.Invoke(isThirdPerson);

    /// <summary>
    /// 收集道具
    /// </summary>
    public static event Action<GameObject, OOPartEnum,int> OnOOPartCollect;
    public static void OOPartCollect(GameObject user, OOPartEnum type, int count) => OnOOPartCollect?.Invoke(user, type, count);


    /// <summary>
    /// 欧帕兹已提交给凯伊(Kei)（参数：类型，数量）。用于已提交列表 UI 展示。
    /// </summary>
    public static event Action<OOPartEnum, int> OnKeiSubmit;
    public static void KeiSubmit(OOPartEnum type, int count) => OnKeiSubmit?.Invoke(type, count);


    /// <summary>
    /// 放弃思考，直接让玩家喊话
    /// </summary>
    public static event Action<GameObject, SpeechTypeEnum> OnPlayMeetSpeech;
    public static void PlayMeetSpeech(GameObject user, SpeechTypeEnum state) => OnPlayMeetSpeech?.Invoke(user, state);
    
    /// <summary>
    /// 单位发言
    /// </summary>
    public static event Action<GameObject, RuntimeSoundData> OnActorSpeech;
    public static void ActorSpeech(GameObject go, RuntimeSoundData data) => OnActorSpeech?.Invoke(go, data);


    #endregion

    #region 单位（已迁出）

    // 【2026-09-30 拆分】OnPlayerCreate / OnFriendCreate 已迁到 02Game/Game/UnitEventSub.cs（下层总线）。
    // 判据：两者的**发布者都是 Actor.cs**（属于将来的 05_UnitCore），按"事件层 = min(发布者层, 订阅者层)"
    // 它们必须跟单位内核同层，否则会把上层总线也拖到 UnitCore 之下。

    #endregion


}
}
