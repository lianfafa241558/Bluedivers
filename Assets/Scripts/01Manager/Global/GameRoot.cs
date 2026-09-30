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
[AddComponentMenu("管理/游戏根")]
public partial class GameRoot : GameRootBase<GameRoot>, FPSGame.GameContract.IFlowService
{
    /// <summary>FPSGame.GameContract.IFlowService 实现（用显式实现，避免与已有的**静态** GameState 成员重名冲突）。
    /// 由 05_UnitCore（<c>Actor</c> 的等待逻辑）经 ServiceLocator.Flow 读取，见 Interface_Manager.cs。</summary>
    GameStateEnum FPSGame.GameContract.IFlowService.GameState => gameState;

    /// <summary>是否已进入主流程（与 <c>FpsHelper.IsMainStage()</c> 同义）。</summary>
    bool FPSGame.GameContract.IFlowService.IsMainStage => gameState == GameStateEnum.Game
                                              || gameState == GameStateEnum.Ready
                                              || gameState == GameStateEnum.Bridge;

    /// <summary>是否本地实例（等价本类的 IsLocal 字段）。</summary>
    bool FPSGame.GameContract.IFlowService.IsLocal => IsLocal;

    /// <summary>代下层在游戏根节点上跑协程（下层拿不到 GameRoot.Instance）。</summary>
    Coroutine FPSGame.GameContract.IFlowService.RunCoroutine(System.Collections.IEnumerator routine) => StartCoroutine(routine);

    /// <summary>设置游戏状态（赋值给已有的静态 GameState，从而照旧触发 GlobalEventSub.SceneChange）。</summary>
    void FPSGame.GameContract.IFlowService.SetGameState(GameStateEnum state) => GameState = state;

    // 定时器：本类的实现是**静态**方法（在 GameRootBase 里）⇒ 接口需显式转发
    LogicTimer FPSGame.GameContract.IFlowService.CreateTimer(Action cb, float waitTime, int counter, Action endcb) => CreateTimer(cb, waitTime, counter, endcb);
    LogicTimer FPSGame.GameContract.IFlowService.CreateTimer(Action<int> cb, float waitTime, int counter, Action endcb) => CreateTimer(cb, waitTime, counter, endcb);
    LogicTimer FPSGame.GameContract.IFlowService.CreatePerTimer(Action percb, float waitTime, Action endcb) => CreatePerTimer(percb, waitTime, endcb);

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
        //注册流程状态服务：供 05_UnitCore 等下层只读访问（放在单例判定之后，避免重复实例覆盖）
        FPSGame.GameContract.ServiceLocator.Flow = this;
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
