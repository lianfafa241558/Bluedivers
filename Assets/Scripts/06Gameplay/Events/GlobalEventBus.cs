using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.Mission;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.Events;
using FPSGame.Data;
using FPSGame.Gameplay;
using FPSGame.GameData;

namespace FPSGame.Gameplay
{
    public static class GlobalEventBus
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


        #region 窗口 / UI（2026-10-01 加入：供玩法层等下层使用，取代 ServiceLocator.Wnd 槽）
        /// <summary>
        /// 界面状态改变（旧值, 新值）。
        ///
        /// <para>▍层判据：发布者 = <c>WndManager</c>（09_Managers）、订阅者 = 玩法层（06_Gameplay）
        /// ⇒ 事件总线放 <c>min(两层)</c> = 06_Gameplay ✓（就是本类）。</para>
        /// <para>▍与 <c>WndManager.OnWindowStateChange</c> 的关系：那个是给**上层（UI）**直接订阅的
        /// （UI 看得见 WndManager）；本事件是给**下层**用的（下层看不见 WndManager）。
        /// 两者由同一个 setter 同时发布 —— **受众不同、语义同源**，是为了让下层能删掉服务槽而保留，
        /// 不是"两个真相"。</para>
        /// </summary>
        public static event Action<WindowStateEnum, WindowStateEnum> OnWindowStateChange;
        public static void WindowStateChange(WindowStateEnum oldState, WindowStateEnum newState) => OnWindowStateChange?.Invoke(oldState, newState);

        /// <summary>请求打开某个窗口：玩法层只说"开哪个"（<see cref="WndType"/> 在契约层），
        /// 由 <c>WndManager</c> 订阅后转调 <c>WindowRegistry</c>。⚠ 无人订阅时静默无操作。</summary>
        public static event Action<WndType> OnOpenWnd;
        public static void OpenWnd(WndType type) => OnOpenWnd?.Invoke(type);

        /// <summary>弹一条 NPC 提示（角色 / 类型 / 持续条件 / 有效时长）。同样由 <c>WndManager</c> 订阅转调。</summary>
        public static event Action<string, string, Func<bool>, float> OnNotice;
        public static void Notice(string role, string type, Func<bool> func, float vaildTime) => OnNotice?.Invoke(role, type, func, vaildTime);

        /// <summary>
        /// 请求切换游戏流程阶段（原 <c>ServiceLocator.Flow.SetGameState</c>）。
        ///
        /// <para>▍层判据：发布者 = 玩法层（<c>MissionEvacuateMobile/Static</c>、<c>MedivacController</c>，均在 06_Gameplay），
        /// 订阅者 = <c>GameRoot</c>（09_Managers）⇒ 放 <c>min(两层)</c> = 06_Gameplay ✓（本类）。
        /// 它是**无返回值的命令** ⇒ 事件比"下层调用上层服务"更贴语义。</para>
        ///
        /// <para>⚠ 为什么不能"直接写 <c>FPSGame.Data.FlowState.GameState</c>"：那会**绕过广播副作用**
        /// （<c>GameRoot.GameState</c> 的 setter 会发 <c>GlobalEventSub.SceneChange</c>，UI/音频都在听）
        /// —— **切阶段是命令，不是数据**：数据自持只承担"状态查询"，写入必须走命令通道。</para>
        /// </summary>
        public static event Action<GameStateEnum> OnRequestGameState;
        public static void RequestGameState(GameStateEnum state) => OnRequestGameState?.Invoke(state);
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


    }
}
