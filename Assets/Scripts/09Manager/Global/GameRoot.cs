using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace FPSGame.Managers
{

//<T>必须在终端才结束
/// <summary>
/// 游戏根节点：游戏状态与时间缩放的静态入口，负责驱动各系统初始化。
/// </summary>
public partial class GameRoot : GameRootBase<GameRoot>
{
    // ⚠ `IFlowService` 实现已于 2026-10-01 全部删除（本类不再实现该接口，接口文件也已删除）：
    //   · 状态查询（GameState/IsMainStage/IsLocal）→ 数据自持 `FPSGame.Data.FlowState`（本类负责**发布**）
    //   · 调度（RunCoroutine/CreateTimer/CreatePerTimer）→ `00_Core` 的 `TimerHost`（宿主由 GameRootBase 发布）
    //   · 切阶段命令（SetGameState）→ 事件 `GlobalEventSub.OnRequestGameState`（本类订阅，见 OnRequestGameState）

    public static float TimeScale
    {
        get => Instance ? Instance.timeScale : Time.timeScale;
        set
        {
            var oldstste = Instance.timeScale;
            Instance.timeScale = Time.timeScale = value;
            Debug.LogWarning("时间刻度被设置为" + value);
            GlobalEventSub.TimeScaleChange(oldstste, value);
        }
    }


    public static GameStateEnum GameState
    {
        get => Instance ? Instance.gameState : GameStateEnum.Front;
        set
        {
            var oldState = Instance.gameState;
            if (oldState != value)
            {
                Instance.gameState = value;
                FPSGame.Data.FlowState.GameState = value;//数据自持（2026-10-01 取代 ServiceLocator.Flow 的状态查询）
                Debug.LogWarning("游戏状态被设置为" + value);
                GlobalEventSub.SceneChange(oldState, value);
            }
        }
    }

    [InspectorName("游戏状态")]
    [SerializeField]
    private GameStateEnum gameState = GameStateEnum.Front;


    /// <summary>
    /// 不触发事件地设置状态（用于初始化场景）
    /// </summary>
    public static void SetWithoutNotify(GameStateEnum state)
    {
        if (Instance)
        {
            Instance.gameState = state;
            FPSGame.Data.FlowState.GameState = state;//⚠ 这条路径绕过事件，但**不能**绕过数据自持（否则读到过期值）
        }
    }


    [InspectorName("时间刻度")]
    [SerializeField]
    private float timeScale;

    public bool IsLocal;



    public override void Awake()
    {

        base.Awake();
        if (Instance != this) return;
        // ⚠ 2026-10-01：不再向 ServiceLocator 注册（`Flow` 槽已删除）——本类改当**发布方**：
        //   状态查询发布到 `FPSGame.Data.FlowState`、调度宿主由 GameRootBase 发布到 `TimerHost`、
        //   切阶段命令由本类订阅 `GlobalEventSub.OnRequestGameState`。
        // ⚠ IsLocal 是本类上的**序列化字段**，只在启动时确定 ⇒ 在此镜像一次（见 FPSGame.Data.FlowState）
        FPSGame.Data.FlowState.IsLocal = IsLocal;
        // 订阅"请求切阶段"命令（2026-10-01 取代 IFlowService.SetGameState；见 GlobalEventSub）
        GlobalEventSub.OnRequestGameState += OnRequestGameState;
        timeScale = Time.timeScale;

        if (IsLocal)
        {
            RoomManager.Instance.Self.airdrop = new int[4] {105,104,103,100 };
            BattleManager.Creat(true);
        }

        StartCoroutine(nameof(InitGameState));
    }
     
    IEnumerator InitGameState()
    {
        yield return null;
        SetWithoutNotify(GameStateEnum.GameEnd);
        GameState = GameStateEnum.Front;
    }

    /// <summary>订阅侧：把"请求切阶段"落成既有的静态 setter ⇒ 照旧触发 <c>SceneChange</c> 广播 + 数据自持镜像。</summary>
    private void OnRequestGameState(GameStateEnum state) => GameState = state;

    /// <summary>
    /// 服务下线：把流程服务槽位还回空对象（= "服务未就绪"的语义），避免它继续指向已销毁的实例。
    /// <para>⚠ 必须 override 而不是新写一个 <c>OnDestroy</c>：基类里还有"给所有 <c>I_GlobaManager</c> 调 <c>UnInit</c>"
    /// 的循环，另起一个会**隐藏**基类方法（CS0114）导致它再也不执行。</para>
    /// </summary>
    protected override void OnDestroy()
    {
        GlobalEventSub.OnRequestGameState -= OnRequestGameState;//与 Awake 的订阅成对
        base.OnDestroy();
    }


#if UNITY_EDITOR
    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.P)) UnityEditor.EditorApplication.isPaused = true;//如果是在unity编译器中
        if (Input.GetKeyDown(KeyCode.L))
        {
            if (Time.timeScale > 0.21f)
            {
                Time.timeScale = 0.2f;
            }
            else
            {
                Time.timeScale = 1f;
            }
        }

    }
#endif

}
}
