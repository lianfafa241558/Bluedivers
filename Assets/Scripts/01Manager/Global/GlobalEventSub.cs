using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Core.Interface;
using FpsGame.Mission;
using FPSGame.Furn;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using static AirdropController;

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
    /// <summary>昼夜交替时</summary>
    public static void DaySwitch(bool isNoon)
    {
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

    #region 单位



    /// <summary>玩家被创建</summary>
    public static event Action<I_Actor> OnPlayerCreate;
    public static void PlayerCreate(I_Actor unit) => OnPlayerCreate?.Invoke(unit);

    /// <summary> 盟友被创建</summary>
    public static event Action<Actor> OnFriendCreate;
    public static void FriendCreate(Actor unit) => OnFriendCreate?.Invoke(unit);

    #endregion


}
