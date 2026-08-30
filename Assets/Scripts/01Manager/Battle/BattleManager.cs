using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using FpsGame.MapUtils;
using FPSGame.Game;
using GameContract;
using PEMaths;

using Unity.FPS.Game;
using UnityEngine;
using Utils;


public class BattleManager : Singleton<BattleManager>
{
    public bool IsStartBattle;
    public bool IsNormal;

    public ActorsManager ACCont;
    public AirdropController ADCont;
    public BattleRoleManager BRCont;
    public WaveManager WaveCont;
    public MissionController MissionCont;
    public PatrolContriller PatrolCont;
    public PathRequestManager RequestManager;

    private UnitQueryGrid unitQueryGrid;
    private MapRoot mapRoot;

    private static readonly Queue<Action> _initQueue = new();
    public static void EnqueueInit(Action action) => _initQueue.Enqueue(action);

    public System.Random BattleRandom { get;private set; }

    /// <summary>本局选择的全队强化类型（null 表示未选择）</summary>
    private BoosterType[] _activeTeamEnhance;

    /// <summary>团灭判负的宽限时间（秒）。需大于治疗包部署时间，避免最后一次增援还在下落时误判</summary>
    private const float WipeFailGrace = 10f;

    /// <summary>增援战备（HealBag），初始化后缓存，避免判定时反复遍历</summary>
    private AirdropController.AirdropData _reinforceAd;

    /// <summary>是否已挂起判负计时器（防重复触发）</summary>
    private bool _wipeCheckPending;

    /// <summary>团灭判负倒计时计时器，用于中止倒计时</summary>
    private LogicTimer _wipeTimer;

    #region 初始化

    public static void Creat(bool isNormal)
    {
        var manager = new GameObject("BattleManager").AddComponent<BattleManager>();
        manager.IsNormal = isNormal;
        if (isNormal)
        {
            manager.StartCoroutine(manager.Init());
        }
        else
        {
            manager.StartCoroutine(manager.InitSpecial());
        }

    }

    private IEnumerator InitSpecial()
    {
        Transform transMapRoot = GameObject.FindGameObjectWithTag("MapRoot").transform;
        TaskManager.Instance.EnsureSceneData(transMapRoot.GetComponent<CampaignCfg>());
        mapRoot = transMapRoot.GetComponent<MapRoot>();
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        BattleRandom = new(TaskManager.Instance.nowTask.taskCfg.seed);
        ApplyTeamEnhance();
        TerrainUtils.Main = mapRoot.terrain;
        mapRoot.Init(false);
        
        ACCont = new GameObject("ActorsManager").AddComponent<ActorsManager>();
        ACCont.transform.SetParent(transform);

        MissionCont = new GameObject("MissionController").AddComponent<MissionController>();
        MissionCont.Init(MissionInitMode.FindFromScene);
        MissionCont.transform.SetParent(transform);
        Debug.Log($"开始任务");
        yield return MissionCont.WaitForInitialization();
        Debug.Log($"任务耗时: {sw.ElapsedMilliseconds} ms");
        sw.Restart();

        ADCont = new GameObject("AirdropController").AddComponent<AirdropController>();
        ADCont.Init();
        CacheReinforceAd();
        ADCont.transform.SetParent(transform);
        BRCont = new GameObject("BattleRoleCont").AddComponent<BattleRoleManager>();
        BRCont.transform.SetParent(transform);
        WaveCont = new GameObject("WaveCont").AddComponent<WaveManager>();
        WaveCont.transform.SetParent(transform);
        PatrolCont = new GameObject("PatrolCont").AddComponent<PatrolContriller>();
        PatrolCont.transform.SetParent(transform);
        //WndManager.Instance.CreatNotice("Yuuka", "MissionStart");
        RequestManager = new GameObject("RequestManager").AddComponent<PathRequestManager>();
        RequestManager.transform.SetParent(transform);
        
        Debug.Log("完成主要内容初始?");
        yield return null;

        //ResManager.Instance.SetLoadSceneExtraProgress(1);
        GameRoot.GameState = GameStateEnum.Game;
        yield return null;
        WndManager.WindowState = WindowStateEnum.Game;
        DrainInitQueue();
        IsStartBattle = true;
        Debug.Log($"其他初始化耗时 {sw.ElapsedMilliseconds} ms");


    }

    private IEnumerator Init()
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        BattleRandom = new(TaskManager.Instance.nowTask.taskCfg.seed);
        ApplyTeamEnhance();

        yield return InitTerrain();
        Debug.Log($"地形耗时: {sw.ElapsedMilliseconds} ms");
        sw.Restart();


        ACCont = new GameObject("ActorsManager").AddComponent<ActorsManager>();
        ACCont.transform.SetParent(transform);

        MissionCont = new GameObject("MissionController").AddComponent<MissionController>();
        MissionCont.Init(MissionInitMode.GenerateFromData);
        MissionCont.transform.SetParent(transform);
        Debug.Log($"开始任务");
        yield return MissionCont.WaitForInitialization();
        Debug.Log($"任务耗时: {sw.ElapsedMilliseconds} ms");
        sw.Restart();

        ADCont = new GameObject("AirdropController").AddComponent<AirdropController>();
        ADCont.Init();
        CacheReinforceAd();
        ADCont.transform.SetParent(transform);
        BRCont = new GameObject("BattleRoleCont").AddComponent<BattleRoleManager>();
        BRCont.transform.SetParent(transform);
        WaveCont = new GameObject("WaveCont").AddComponent<WaveManager>();
        WaveCont.transform.SetParent(transform);
        PatrolCont = new GameObject("PatrolCont").AddComponent<PatrolContriller>();
        PatrolCont.transform.SetParent(transform);
        WndManager.Instance.CreatNotice("Yuuka", "MissionStart");
        RequestManager = new GameObject("RequestManager").AddComponent<PathRequestManager>();
        RequestManager.transform.SetParent(transform);
        
        Debug.Log("完成主要内容初始?");
        yield return null;

        //ResManager.Instance.SetLoadSceneExtraProgress(1);
        GameRoot.GameState = GameStateEnum.Game;
        yield return null;
        WndManager.WindowState = WindowStateEnum.Game;
        DrainInitQueue();
        IsStartBattle = true;
        Debug.Log($"其他初始化耗时 {sw.ElapsedMilliseconds} ms");


    }
    IEnumerator InitTerrain()
    {
        Transform transMapRoot = GameObject.FindGameObjectWithTag("MapRoot").transform;
        mapRoot = transMapRoot.GetComponent<MapRoot>();

        //var terrain = TerrainUtils.Main = mapRoot.terrain;
        var terrain = mapRoot.terrain;
        var cfg = TaskManager.Instance.nowTask;
        var terrainData = terrain.terrainData;


        var mapRes = cfg.MainCfg.sizeType switch {
            SizeType.Small => 512,
            SizeType.Medium => 1024,
            SizeType.Large => 1024,
            _ => 512
        };

        terrainData.heightmapResolution = mapRes + 1;
        terrainData.alphamapResolution = mapRes;
        // 分辨率变更后重新设置Main，同步静态缓存
        //TerrainUtils.Main = terrain;
        //不能调换顺序，会出问题
        terrainData.size = new(cfg.MapSize, cfg.MapHeight, cfg.MapSize);
        // size.y 变更后刷新 terrainHeight 缓存，否则 AdditionTerrain 高度计算使用旧值
        TerrainUtils.Main = terrain;
        //Debug.LogWarning("地图尺寸" + cfg.MainCfg.sizeType + " 地图大小 + cfg.MapSize);
        //Debug.LogWarning("地图真实" + mapRoot.terrain.terrainData.size);
        //terrainData.size = new(cfg.MapSize, cfg.MapHeight, cfg.MapSize);
        mapRoot.Init(true);
        List<TerrainItemInfo> infos = new(TaskManager.Instance.nowTask.mapCfg.TerrainItem);
        var nestinfo = TaskManager.Instance.nowTask.campData.NestTerrainItem;
        infos[3] = nestinfo;
        yield return mapRoot.GetComponent<GenerateNoiseTerrain>().SetTextures(infos.Select(item => item.diffuseTexture).ToArray(), infos.Select(item => item.tileSize).ToArray());

        yield return mapRoot.GetComponent<GenerateNoiseTerrain>().ApplyFractalNoiseToTerrain(cfg.taskCfg.terrainType);

        var debugger = transMapRoot.GetComponent<UnitQueryGridDebugger>();
        if (debugger.IsValid())
        {
            debugger.grid = unitQueryGrid;
        }
    }

    #endregion



    #region 生命周期

    void Start()
    {

        var pos = mapRoot.rect;
        unitQueryGrid = new(new((PEVector2)pos.center, pos.size.x / 2, pos.size.z / 2), 30);
        

        BattleEventSub.OnUnitPosChange += OnUnitPosChange;
        BattleEventSub.OnEnemyCreate += OnEnemyCreate;
        BattleEventSub.OnEnemyDead += OnEnemyDeath;
        GlobalEventSub.OnPlayerCreate += OnPlayerCreate;
        BattleEventSub.OnPlayerDead += OnPlayerDeath;
        //GlobalEventSub.OnOOPartCollect += OOPartCollect;
        GlobalEventSub.OnDaySwitch += OnDatSwitch;
    }
    private void OnDestroy()
    {
        BattleEventSub.OnUnitPosChange -= OnUnitPosChange;
        BattleEventSub.OnEnemyCreate -= OnEnemyCreate;
        BattleEventSub.OnEnemyDead -= OnEnemyDeath;
        GlobalEventSub.OnPlayerCreate -= OnPlayerCreate;
        BattleEventSub.OnPlayerDead -= OnPlayerDeath;
        //GlobalEventSub.OnOOPartCollect -= OOPartCollect;
        GlobalEventSub.OnDaySwitch -= OnDatSwitch;
        if (_reinforceAd != null) _reinforceAd.OnStateChange -= OnReinforceStateChange;
        if (_wipeTimer != null) GameRoot.RemoveTimer(_wipeTimer);
        _initQueue.Clear();
    }

    private void DrainInitQueue()
    {
        while (_initQueue.Count > 0)
        {
            var action = _initQueue.Dequeue();
            try { action?.Invoke(); }
            catch (System.Exception e) { Debug.LogError($"[BattleManager] 初始化队列执行异常: {e}"); }
        }
    }

    #endregion

    #region API


    public List<I_Actor> FindUnits(IPERange range, TargetCfg targetCfg, System.Func<I_Actor, bool> customFilter = null)
    {
        return new List<I_Actor>(unitQueryGrid.QueryUnits(range, targetCfg, customFilter));
    }
    public List<I_Actor> FindUnits(TargetCfg targetCfg, System.Func<I_Actor, bool> customFilter = null)
    {
        return new List<I_Actor>(unitQueryGrid.QueryUnits(targetCfg, customFilter));
    }

    public GameObject CreatUnit(UnitTier tier,Vector3 pos,float range,bool isFixed=true)
    {
       return WaveCont.CreatUnit(tier, pos,range, isFixed);
    }

    public List<GameObject> CreatPatrol(Vector3 pos)
    {
        return WaveCont.CreatPatrol(pos);
    }

    public bool CreatWave(WaveCreateParams param) => WaveCont.CreatWave(param);

    private void OnUnitPosChange(I_Actor unit)
    {
        if (unit.Type != UnitTypeEnum.None)
        {
            unitQueryGrid.UpdateNodes(unit);
        }
    }

    private void OnEnemyDeath(Actor unit)
    {
        unitQueryGrid.RemoveUnit(unit);
    }

    private void OnEnemyCreate(Actor unit)
    {
        unitQueryGrid.AddUnit(unit);
    }
    private void OnPlayerDeath(Actor unit)
    {
        //unitQueryGrid.RemoveUnit(unit.GetComponent<Actor>());
        TryWipeFail();
    }

    /// <summary>缓存增援战备（HealBag）并订阅其状态变化</summary>
    private void CacheReinforceAd()
    {
        _reinforceAd = ADCont.useAd.FirstOrDefault(item => item.cfg.ID == Constants.HealBag);
        if (_reinforceAd != null) _reinforceAd.OnStateChange += OnReinforceStateChange;
    }

    /// <summary>是否全队阵亡</summary>
    public bool IsTeamWiped =>
        ActorsManager.Players.Count > 0
        && ActorsManager.Players.All(item => item.ActorState == ActorState.Dead);

    /// <summary>剩余增援次数（未携带增援时返回 0）</summary>
    public int ReinforcementCount => _reinforceAd != null ? _reinforceAd.count : 0;

    /// <summary>
    /// 尝试判定团灭失败：全队阵亡且增援已耗尽（State 为 Unavailable）时进入倒计时并结算失败。
    /// 倒计时期间逐秒广播剩余秒数，并持续校验条件，被救起则中止。
    /// </summary>
    private void TryWipeFail()
    {
        if (_wipeCheckPending || !IsStartBattle) return;
        // 本局未携带增援战备时不判负，避免误伤不带增援的任务
        if (_reinforceAd == null) return;
        if (_reinforceAd.State != AirdropController.AirdropState.Unavailable) return;
        if (!IsTeamWiped) return;

        _wipeCheckPending = true;
        _wipeTimer = GameRoot.CreateTimer((count) =>
        {
            // 逐秒校验：期间可能被在途治疗包救起，或已进入结算流程
            if (!CheckWipeFailValid())
            {
                CancelWipeFail();
                return;
            }
            // 首次回调在 1 秒后（count=0），此时剩余 WipeFailGrace-1 秒
            BattleEventSub.WipeFailCountdown(WipeFailGrace - count - 1);
        }, 1, Mathf.CeilToInt(WipeFailGrace), () =>
        {
            _wipeTimer = null;
            _wipeCheckPending = false;
            // 结算前最后校验一次
            if (!CheckWipeFailValid()) return;
            EndGame(1, GameResult.Failure);
        });
        // 立即广播初始值，避免首发回调前界面空白
        BattleEventSub.WipeFailCountdown(WipeFailGrace);
    }

    /// <summary>团灭判负条件是否仍然成立</summary>
    private bool CheckWipeFailValid()
    {
        if (!IsStartBattle || GameRoot.GameState != GameStateEnum.Game) return false;
        if (_reinforceAd == null) return false;
        if (_reinforceAd.State != AirdropController.AirdropState.Unavailable) return false;
        return IsTeamWiped;
    }

    /// <summary>中止团灭判负倒计时（被救起或条件失效）</summary>
    private void CancelWipeFail()
    {
        if (_wipeTimer != null)
        {
            GameRoot.RemoveTimer(_wipeTimer);
            _wipeTimer = null;
        }
        if (!_wipeCheckPending) return;
        _wipeCheckPending = false;
        BattleEventSub.WipeFailCancel();
    }

    /// <summary>增援战备状态变化：次数耗尽（Unavailable）且全队阵亡时进入判负流程</summary>
    private void OnReinforceStateChange(AirdropController.AirdropData data, AirdropController.AirdropState state)
    {
        if (state == AirdropController.AirdropState.Unavailable) TryWipeFail();
    }

    private void OnPlayerCreate(I_Actor unit)
    {
        unitQueryGrid.AddUnit(unit);
    }


    public void ReleaseAirdrop(Vector3 point,int id, System.Action<GameObject> action=default)
    {
        ReleaseAirdrop(point, RandomUtils.Range(0, 360), id, action);
    }
    public void ReleaseAirdrop(Vector3 point,float angle, int id, System.Action<GameObject> action = default)
    {
        var beacon = VFXManager.Creat(ResSvc.Instance.LoadObject<GameObject>("Prefabs/Airdrop/VFX_AirdropPoint"), point, Quaternion.Euler(0,angle, 0), null);
        beacon.GetComponent<VFXAirdropEffect>()?.TmpAirdrop(point, ResSvc.Instance.GetAirdrop(id), action);
    }

    public void Authorize(int id, bool state)
    {
        ADCont.Authorize(id,state);
    }


    public void AddBattleDataItem(int playerIndex, string name)
    {
        MissionCont.AddBattleDataItem(playerIndex, name);
    }

    public void EndGame(int delay,GameResult result= GameResult.Unknow)
    {
        if (result != GameResult.Unknow) TaskManager.Instance.nowTask.result = result;
        //GlobalEventManager.Evacuate();
        GameRoot.CreateTimer(() => {
            // 先切到 UI 状态，让 PlayerWnd/SubtitleWnd 的 Update 不再执行，避免场景卸载期间 NRE
            WndManager.WindowState = WindowStateEnum.UI;
            if (IsNormal)
            {
                ResSvc.Instance.AsyncLoadScene("GameEnd", () => {

                    GameRoot.GameState = GameStateEnum.GameEnd;
                    WndManager.WindowState = WindowStateEnum.UI;
                }, false);
            }
            else
            {
                ResSvc.Instance.AsyncLoadScene("Utnapishitim", () => {
                    GameRoot.GameState = GameStateEnum.Bridge;
                    WndManager.WindowState = WindowStateEnum.Game;
                });
            }
        }, delay);
    }

    //private void OOPartCollect(GameObject user, OOPartEnum type, int count)
    //{
        // 采集事件：统计采集行为（采集动作），任务采集量由 Kei 交付时 SubmitOOPart 累加
    //    if (user && user.TryGetComponent(out PlayerController player))
    //}

    /// <summary>欧帕兹提交给凯伊(Kei)：累加任务采集量</summary>
    public void SubmitOOPart(GameObject user, OOPartEnum type, int count)
    {
        var dic = TaskManager.Instance.nowTask.collectProperty;
        if (!dic.TryAdd(type, count)) dic[type] += count;
        AddBattleDataItem(user.GetComponent<PlayerController>().PlayerIndex, "采集欧帕兹数量");
        GlobalEventSub.KeiSubmit(type, count);
    }
    private void OnDatSwitch(bool isNoon)
    {
        //Debug.LogError("昼夜交替"+ isNoon);
        ADCont.Authorize(16, !isNoon);
        ADCont.Authorize(17, !isNoon);
        //应该加语音播报
    }

    /// <summary>
    /// 应用本局选择的全队强化效果。
    /// 从 RoomManager.Self.teamEnhance 读取 ID，映射到对应强化类型并应用到各系统。
    /// </summary>
    private void ApplyTeamEnhance()
    {
        _activeTeamEnhance = RoomManager.Instance.players.Where(item => item.boosterId > 0).Select(item =>ResSvc.boostDic[item.boosterId].type).ToArray();
       
    }

    public bool HaveBooster(BoosterType type)
    {
       return _activeTeamEnhance.Contains(type);
    }

    #endregion
}

public struct WaveCreateParams
{
    public Vector3 center;
    public Vector3[] points;

    public bool extraWave;
    public float range;
    public float scale;
    public bool tip;

    public static WaveCreateParams Default => new WaveCreateParams {
        extraWave = false,
        range = 35,
        scale = 1,
        tip = true
    };

    public static WaveCreateParams Extra => new WaveCreateParams {
        extraWave = true,
        range = 35,
        scale = 0.5f,
        tip = true
    };

    public static WaveCreateParams Defensive => new WaveCreateParams {
        extraWave = true,
        range = 5,
        scale = 1,
        tip = true,
    };


}
public static class WaveUtil
{
    public static WaveCreateParams Set(this WaveCreateParams para, Vector3 center)
    {
        para.center = center;
        return para;
    }
    public static WaveCreateParams Set(this WaveCreateParams para, Vector3 center,Vector3[] points)
    {
        para.center = center;
        para.points = points;
        return para;
    }
    public static WaveCreateParams Scale(this WaveCreateParams para, float scale)
    {
        para.scale = scale;
        return para;
    }
}