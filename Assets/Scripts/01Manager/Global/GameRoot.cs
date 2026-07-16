using System;
using Core;
using GameContract;
using Unity.BaseTool;
using UnityEngine;
using UnityEngine.Events;

//<T>必须在终端才结束
public partial class GameRoot : GameRootBase<GameRoot>
{
    public static ArchivesData_SO Archive => Instance.showArchive;

    [SerializeField]
    private ArchivesData_SO showArchive;
    [SerializeField]
    protected ArchivesData_SO defaultArchive;

    public static WindowStateEnum WindowState
    {
        get => Instance ? Instance.windowState : WindowStateEnum.Game;
        set {
            var oldstste = Instance.windowState;
            Instance.windowState = value;
            //Debug.LogWarning("界面状态被设置为"+value);
            OnWindowStateChange?.Invoke(oldstste,value);
        }
    }
    public static event UnityAction<WindowStateEnum,WindowStateEnum> OnWindowStateChange;
    [InspectorName("界面状态")][SerializeField]
    private WindowStateEnum windowState = WindowStateEnum.Game;




    public static GameStateEnum GameState
    {
        get => Instance ? Instance.gameState : GameStateEnum.Front;
        set {
            var oldstste = Instance.gameState;
            Instance.gameState = value; 
            //Debug.LogWarning("游戏状态被设置为" + value);
            OnGameStateChange?.Invoke(oldstste,value);
        }
    }

    public static event UnityAction<GameStateEnum,GameStateEnum> OnGameStateChange;

    [InspectorName("游戏状态")][SerializeField]
    private GameStateEnum gameState = GameStateEnum.Front;

    public static float TimeScale
    {
        get => Instance ? Instance.timeScale : Time.timeScale;
        set
        {
            var oldstste = Instance.timeScale;
            Instance.timeScale = Time.timeScale = value;
            Debug.LogWarning("时间刻度被设置为" + value);
            OnTimeScaleChange?.Invoke(oldstste, value);
        }
    }
    public static event UnityAction<float, float> OnTimeScaleChange;

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
    #endregion


    public override void Awake()
    {
        showArchive = (ArchivesData_SO)ArchivesData_SO.Load();

        //Debug.LogWarning("游戏状态初始被设置为" + GameState);
        LayerDefinition.HittableHighSpeedLayers = hittableHighSpeedLayers | groundLayers | unitLayers;
        LayerDefinition.HittableLayers = groundLayers | unitLayers;
        LayerDefinition.MoveableLayers = airWallLayers| groundLayers;
        LayerDefinition.UnitLayers = unitLayers;
        LayerDefinition.GroundLayers = groundLayers;
        LayerDefinition.WeaponLayers = weaponLayers;
        LayerDefinition.AirWallLayers = airWallLayers;



        base.Awake();
        if (Instance != this) return;
        if (!defaultArchive) return;
        timeScale = Time.timeScale;

        if (IsLocal)
        {
            RoomManager.Instance.Self.airdrop = new int[4] {105,104,103,100 };
            BattleManager.Creat();
        }
        CreateTimer(InitSetting, 0.1f);
    }

    void InitSetting()
    {
        bool haveNewSetting=false;
        haveNewSetting |= Archive.settingDic.Synchronize(defaultArchive.settingDic);
        haveNewSetting |= Archive.roleDataDic.Synchronize(defaultArchive.roleDataDic);
        haveNewSetting |= Archive.propertys.Synchronize(defaultArchive.propertys);

        Archive.settingDic.ForEach((key, item) => GlobalEventManager.SettingCange(key, item.value.RawInt));
        if (haveNewSetting) Archive.Save();
    }


    private void Update()
    {
#if UNITY_EDITOR
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
#endif
    }

    public static float GetSetting(string name)=>Archive.settingDic[name].value.RawFloat;
}

