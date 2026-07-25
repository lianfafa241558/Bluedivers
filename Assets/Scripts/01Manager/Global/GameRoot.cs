using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using Core.Interface;
using GameContract;

using UnityEngine;
using UnityEngine.Events;

//<T>必须在终端才结束
public partial class GameRoot : GameRootBase<GameRoot>
{

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

    #region 层级
    public bool IsLocal;
    /// <summary>高速子弹碰撞层</summary>
    [SerializeField]
    [InspectorName("高速子弹碰撞层")]
    private LayerMask hittableHighSpeedLayers = -1;

    

    /// <summary>武器层</summary>
    [SerializeField]
    [InspectorName("武器层")]
    private LayerMask weaponLayers = -1;

    /// <summary>地面层</summary>
    [SerializeField]
    [InspectorName("地面层")]
    private LayerMask groundLayers = -1;

    /// <summary>单位层</summary>
    [SerializeField]
    [InspectorName("单位层")]
    private LayerMask unitLayers = -1;


    /// <summary>空气墙层</summary>
    [SerializeField]
    [InspectorName("空气墙层")]
    private LayerMask airWallLayers = -1;


    /// <summary>第一人称忽略层</summary>
    [SerializeField]
    [InspectorName("第一人称忽略层")]
    private LayerMask firstPersonIgnoreLayers=-1;
    #endregion


    public override void Awake()
    {
        //Debug.LogWarning("游戏状态初始被设置为" + GameState);
        LayerDefinition.HittableHighSpeedLayers = hittableHighSpeedLayers | groundLayers | unitLayers;
        LayerDefinition.HittableLayers = groundLayers | unitLayers;
        LayerDefinition.MoveableLayers = airWallLayers| groundLayers|unitLayers;
        LayerDefinition.UnitLayers = unitLayers;
        LayerDefinition.GroundLayers = groundLayers;
        LayerDefinition.WeaponLayers = weaponLayers;
        LayerDefinition.AirWallLayers = airWallLayers;
        LayerDefinition.FirstPersonIgnoreLayers = firstPersonIgnoreLayers;


        base.Awake();
        if (Instance != this) return;
        timeScale = Time.timeScale;

        if (IsLocal)
        {
            RoomManager.Instance.Self.airdrop = new int[4] {105,104,103,100 };
            BattleManager.Creat();
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
