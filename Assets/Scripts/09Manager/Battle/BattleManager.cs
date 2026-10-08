using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.MapUtils;
using FPSGame.Game;
using FPSGame.GameContract;
using PEMaths;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Data;
using FPSGame.Rendering;
using FPSGame.Gameplay;

namespace FPSGame.Managers
{


/// <summary>
/// 战斗总控：单位查询、波次与天气调度、团灭判负与初始化。
/// </summary>
[AddComponentMenu("管理/战斗总控")]
public class BattleManager : Singleton<BattleManager>, IBattleService, FPSGame.Game.IUnitQuerySink
{
    // 契约成员里"字段 / 静态方法 / 子管理器字段"无法隐式实现接口 ⇒ 显式转发（见 IBattleService.cs）

    /// <summary>契约：服务已就位（本类被加载即 true）—— 等价于老的 `BattleManager.Instance != null`。</summary>
    bool IBattleService.IsPresent => true;
    int IBattleService.WaveCount => WaveCont.WaveCount;

    /// <summary>事件处理：暴露全图所有未结束任务（原 <c>IBattleService.RevealAllMissions</c>，2026-10-01 事件化）。</summary>
    private void HandleRevealAllMissions() => MissionCont.RevealAll();

    public bool IsStartBattle;
    public bool IsNormal;

    public ActorsManager ACCont;
    public AirdropController ADCont;
    public BattleRoleManager BRCont;
    public WaveManager WaveCont;
    public MissionController MissionCont;
    public PatrolContriller PatrolCont;
    public PathRequestManager RequestManager;
    public WeatherSystem WeatherCont;

    /// <summary>本局"结束"是否已经请求过。
    /// <para>▍联机：房主广播(4033)与本地触发（团灭/撤离）可能并存 ⇒ 不加门会排两个定时器（两次加载场景）。
    /// 本类由 <see cref="Creat"/> 每局新建 ⇒ 不需要额外复位。</para></summary>
    private bool _gameOverRequested;

    private UnitQueryGrid unitQueryGrid;
    private MapRoot mapRoot;

    private static readonly Queue<Action> _initQueue = new();
    public static void EnqueueInit(Action action) => _initQueue.Enqueue(action);

    public System.Random BattleRandom { get;private set; }

    /// <summary>本局天气（开局随机抽取）</summary>
    public WeatherType Weather { get; private set; }

    /// <summary>
    /// 本局随机种子：**优先用权威种子**（联机由房主随开局广播下发，见 <c>TaskState.Seed</c>），
    /// 未指定（0）时回落到该任务自己的 <c>taskCfg.seed</c>（单机 / 旧版房主行为不变）。
    /// </summary>
    private int ResolveBattleSeed()
    {
        int seed = FPSGame.Data.TaskState.Seed;
        return seed != 0 ? seed : TaskManager.Instance.nowTask.taskCfg.seed;
    }

    /// <summary>开局按地图配置的天气权重表抽取天气并应用（使用 BattleRandom，同种子结果一致）</summary>
    private void RandomWeather()
    {
        Weather = WeatherSystem.RollWeather(TaskManager.Instance.nowTask.mapCfg?.WeatherInfos, BattleRandom);
        WeatherCont = WeatherSystem.Create(Weather, transform);
        Debug.Log($"[BattleManager] 本局天气: {Weather}");
    }

    /// <summary>本局选择的全队强化类型（null 表示未选择）</summary>
    private BoosterType[] _activeTeamEnhance;

    /// <summary>团灭判负的宽限时间（秒）。需大于治疗包部署时间，避免最后一次增援还在下落时误判</summary>
    private const float WipeFailGrace = 10f;

    /// <summary>增援战备（HealBag），初始化后缓存，避免判定时反复遍历</summary>
    private AirdropData _reinforceAd;

    /// <summary>是否已挂起判负计时器（防重复触发）</summary>
    private bool _wipeCheckPending;

    /// <summary>团灭判负倒计时计时器，用于中止倒计时</summary>
    private LogicTimer _wipeTimer;

    /// <summary>最近一次昼夜状态（true=白天）；null 表示本局还没收到过昼夜事件</summary>
    private bool? _dayIsNoon;

    /// <summary>是否已按"夜晚"给照明战备加过授权（授权是计数制，用于避免重复加减）</summary>
    private bool _nightAuthorized;

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

        BattleRandom = new(ResolveBattleSeed());
        FPSGame.Data.BattleState.BattleRandom = BattleRandom;//数据自持同步点（见 BattleState.cs）
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
        // 战备控制器就绪，补发一次初始昼夜授权（开局即夜晚时夜间照明战备才能解锁）
        ApplyInitDaySwitch();
        BRCont = new GameObject("BattleRoleCont").AddComponent<BattleRoleManager>();
        BRCont.transform.SetParent(transform);
        WaveCont = new GameObject("WaveCont").AddComponent<WaveManager>();
        WaveCont.transform.SetParent(transform);
        PatrolCont = new GameObject("PatrolCont").AddComponent<PatrolContriller>();
        PatrolCont.transform.SetParent(transform);
        //WndManager.Instance.CreatNotice("Yuuka", "MissionStart");
        RequestManager = new GameObject("RequestManager").AddComponent<PathRequestManager>();
        RequestManager.transform.SetParent(transform);
        RandomWeather();
        
        Debug.Log("完成主要内容初始?");
        yield return null;

        //ResManager.Instance.SetLoadSceneExtraProgress(1);
        GameRoot.GameState = GameStateEnum.Game;
        yield return null;
        WndManager.WindowState = WindowStateEnum.Game;
        DrainInitQueue();
        IsStartBattle = true;
        FPSGame.Data.BattleState.IsStartBattle = true;//数据自持同步点（见 BattleState.cs）
        Debug.Log($"其他初始化耗时 {sw.ElapsedMilliseconds} ms");


    }

    private IEnumerator Init()
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        BattleRandom = new(ResolveBattleSeed());
        BattleState.BattleRandom = BattleRandom;//数据自持同步点（见 BattleState.cs）
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
        // 战备控制器就绪，补发一次初始昼夜授权（开局即夜晚时夜间照明战备才能解锁）
        ApplyInitDaySwitch();
        BRCont = new GameObject("BattleRoleCont").AddComponent<BattleRoleManager>();
        BRCont.transform.SetParent(transform);
        WaveCont = new GameObject("WaveCont").AddComponent<WaveManager>();
        WaveCont.transform.SetParent(transform);
        PatrolCont = new GameObject("PatrolCont").AddComponent<PatrolContriller>();
        PatrolCont.transform.SetParent(transform);
        WndManager.Instance.CreatNotice("Yuuka", "MissionStart");
        RequestManager = new GameObject("RequestManager").AddComponent<PathRequestManager>();
        RequestManager.transform.SetParent(transform);
        RandomWeather();
        
        Debug.Log("完成主要内容初始?");
        yield return null;

        //ResManager.Instance.SetLoadSceneExtraProgress(1);
        GameRoot.GameState = GameStateEnum.Game;
        yield return null;
        WndManager.WindowState = WindowStateEnum.Game;
        DrainInitQueue();
        IsStartBattle = true;
        BattleState.IsStartBattle = true;//数据自持同步点（见 BattleState.cs）
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
        // 新战斗的新地形：重置积雪遮罩，避免上一场战斗的弹坑痕迹残留（下次擦雪时按地形按需重建）
        SnowController.ResetMask();
        //Debug.LogWarning("地图尺寸" + cfg.MainCfg.sizeType + " 地图大小 + cfg.MapSize);
        //Debug.LogWarning("地图真实" + mapRoot.terrain.terrainData.size);
        //terrainData.size = new(cfg.MapSize, cfg.MapHeight, cfg.MapSize);
        mapRoot.Init(true);
        List<TerrainItemInfo> infos = new(TaskManager.Instance.nowTask.mapCfg.TerrainItem);
        var nestinfo = TaskManager.Instance.nowTask.campData.NestTerrainItem;
        infos[3] = nestinfo;
        yield return mapRoot.GetComponent<GenerateNoiseTerrain>().SetTextures(infos.Select(item => item.diffuseTexture).ToArray(), infos.Select(item => item.tileSize).ToArray());

        // 原型与地图级倍率都由 MapData_SO 提供：
        //   地形树原型 = 石块 + 树（石块在前）；细节（草）原型 = detailPrototypes
        //   倍率：树密度 / 悬崖数量（缺省 1，0 = 本图不长树 / 不放悬崖）
        yield return mapRoot.GetComponent<GenerateNoiseTerrain>().ApplyFractalNoiseToTerrain(
            cfg.taskCfg.terrainType,
            cfg.mapCfg?.stonePrototypes,
            cfg.mapCfg?.treePrototypes,
            cfg.mapCfg?.detailPrototypes,
            cfg.mapCfg?.StoneSpawnMultiplier ?? 1f,
            cfg.mapCfg?.TreeSpawnMultiplier ?? 1f,
            cfg.mapCfg?.RockCoverMultiplier ?? 1f,
            cfg.mapCfg?.DetailSpawnMultiplier ?? 1f,
            ResolveBattleSeed()   // 本局权威种子 ⇒ 地形/装饰派生流（两端同种子即同地形）
        );

        var debugger = transMapRoot.GetComponent<UnitQueryGridDebugger>();
        if (debugger.IsValid())
        {
            debugger.grid = unitQueryGrid;
        }
    }

    #endregion



    #region 生命周期


    public override void Awake()
    {
        base.Awake();
        // 重复实例已被 base.Awake 判为待销毁 ⇒ 不得继续注册/订阅，
        // 否则它销毁时会顶掉存活实例刚注册的服务（见下方 OnDestroy 的身份判定）。
        if (Instance != this) return;

        BattleHub.Current = this;//注册战斗服务：供 05_UnitCore 等下层只读访问（见 ServiceLocator.cs）

        // 数据自持的**跨局重置**：BattleState 是静态的，不会随新实例自动归零
        // ⇒ 不清就会把上一局的全队强化 / 开战状态 / 随机源带进新一局（原实例字段是天然归零的）。
        FPSGame.Data.BattleState.Reset();

        UnitEventBus.OnUnitPosChange += OnUnitPosChange;
        UnitEventBus.OnEnemyCreate += OnEnemyCreate;
        UnitEventBus.OnEnemyDead += OnEnemyDeath;
        UnitEventBus.OnPlayerCreate += OnPlayerCreate;
        UnitEventBus.OnPlayerDead += OnPlayerDeath;
        //GlobalEventSub.OnOOPartCollect += OOPartCollect;
        GlobalEventBus.OnDaySwitch += OnDatSwitch;
        // 战斗命令事件（2026-10-01 由服务契约下沉，见 BattleEventSub）
        BattleEventBus.OnEndGame += EndGame;
        BattleEventBus.OnSubmitOOPart += SubmitOOPart;
        BattleEventBus.OnRevealAllMissions += HandleRevealAllMissions;
        BattleEventBus.OnAddBattleDataItem += AddBattleDataItem;
        BattleEventBus.OnRequestAuthorize += Authorize;
    }

    private void Start()
    {
        var pos = mapRoot.rect;
        unitQueryGrid = new(new((PEVector2)pos.center, pos.size.x / 2, pos.size.z / 2), 30);
        // 查询入口接管：网格建好后对外可用（见 05_UnitCore/UnitQuery.cs）
        UnitQuery.Sink = this;

    }

    private void OnDestroy()
    {
        UnitEventBus.OnUnitPosChange -= OnUnitPosChange;
        UnitEventBus.OnEnemyCreate -= OnEnemyCreate;
        UnitEventBus.OnEnemyDead -= OnEnemyDeath;
        UnitEventBus.OnPlayerCreate -= OnPlayerCreate;
        UnitEventBus.OnPlayerDead -= OnPlayerDeath;
        //GlobalEventSub.OnOOPartCollect -= OOPartCollect;
        GlobalEventBus.OnDaySwitch -= OnDatSwitch;
        // 战斗命令事件退订（成对，见 Awake）
        BattleEventBus.OnEndGame -= EndGame;
        BattleEventBus.OnSubmitOOPart -= SubmitOOPart;
        BattleEventBus.OnRevealAllMissions -= HandleRevealAllMissions;
        BattleEventBus.OnAddBattleDataItem -= AddBattleDataItem;
        BattleEventBus.OnRequestAuthorize -= Authorize;
        if (_reinforceAd != null) _reinforceAd.OnStateChange -= OnReinforceStateChange;
        if (_wipeTimer != null) GameRoot.RemoveTimer(_wipeTimer);
        _initQueue.Clear();

        // 服务下线：把入口 / 查询入口都还回中性值，数据自持点归零，避免它们继续指向已销毁的实例
        // （接口引用不走 Unity 的 ==null 重载 ⇒ 上层不会自动回落到中性值）。
        // 身份判定 ⇒ 若已被新实例接管则不动，防止误清后来者。
        if (ReferenceEquals(BattleHub.Current, this))
        {
            BattleHub.Current = NullBattleService.Instance;
            if (ReferenceEquals(UnitQuery.Sink, this)) UnitQuery.Sink = null;
            FPSGame.Data.BattleState.Reset();
        }
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


    public List<IActor> FindUnits(IPERange range, TargetCfg targetCfg, System.Func<IActor, bool> customFilter = null)
    {
        return new List<IActor>(unitQueryGrid.QueryUnits(range, targetCfg, customFilter));
    }
    public List<IActor> FindUnits(TargetCfg targetCfg, System.Func<IActor, bool> customFilter = null)
    {
        return new List<IActor>(unitQueryGrid.QueryUnits(targetCfg, customFilter));
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

    private void OnUnitPosChange(IActor unit)
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
        if (_reinforceAd.State != AirdropState.Unavailable) return;
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
            BattleEventBus.WipeFailCountdown(WipeFailGrace - count - 1);
        }, 1, Mathf.CeilToInt(WipeFailGrace), () =>
        {
            _wipeTimer = null;
            _wipeCheckPending = false;
            // 结算前最后校验一次
            if (!CheckWipeFailValid()) return;
            EndGame(1, GameResult.Failure);
        });
        // 立即广播初始值，避免首发回调前界面空白
        BattleEventBus.WipeFailCountdown(WipeFailGrace);
    }

    /// <summary>团灭判负条件是否仍然成立</summary>
    private bool CheckWipeFailValid()
    {
        if (!IsStartBattle || GameRoot.GameState != GameStateEnum.Game) return false;
        if (_reinforceAd == null) return false;
        if (_reinforceAd.State != AirdropState.Unavailable) return false;
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
        BattleEventBus.WipeFailCancel();
    }

    /// <summary>增援战备状态变化：次数耗尽（Unavailable）且全队阵亡时进入判负流程</summary>
    private void OnReinforceStateChange(AirdropData data, AirdropState state)
    {
        if (state == AirdropState.Unavailable) TryWipeFail();
    }

    private void OnPlayerCreate(IActor unit)
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
        // 走契约取组件（原 `GetComponent<VFXAirdropEffect>()` ⇒ 管理器反向依赖 Effect 层，
        // 且要传 `AirdropData_SO`；现在只传 id，配置解析交给实现方。见 FPSGame.GameContract.IAirdropEffect）
        beacon.GetComponent<FPSGame.GameContract.IAirdropEffect>()?.TmpAirdrop(point, id, action);
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

        // ★ 联机：本局只结束一次 —— 房主广播(4033)与本地触发可能并存，重复调用会排两个定时器。
        //   这里只挡"再排一次"，**结果仍允许被补正**（如本地以 Unknow 结束、随后收到房主权威的 Victory）。
        if (_gameOverRequested) return;
        _gameOverRequested = true;

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
        GlobalEventBus.KeiSubmit(type, count);
    }
    private void OnDatSwitch(bool isNoon)
    {
        //Debug.LogError("昼夜交替"+ isNoon);
        // 战备控制器由初始化协程在地形/任务之后创建，早到的事件先存下来，
        // 等 ADCont 就绪后由 ApplyInitDaySwitch 补一次（否则开局即夜晚时夜间照明战备不会解锁）
        _dayIsNoon = isNoon;
        ApplyDaySwitch();
        //应该加语音播报
    }

    /// <summary>
    /// 战备控制器就绪后补一次昼夜授权。
    /// 场景里的 DayNightBrain.Start 会抛开局那次昼夜事件，但 BattleManager 是被
    /// AsyncLoadScene 的回调（被延后一帧）创建的，订阅永远晚于事件 → 开局那一次会漏，
    /// 所以这里按 GlobalEventSub 缓存的状态补一次（开局即夜晚时尤其重要）。
    /// </summary>
    private void ApplyInitDaySwitch()
    {
        bool? lastNoon = GlobalEventBus.LastDaySwitchIsNoon;
        if (lastNoon.HasValue) _dayIsNoon = lastNoon;
        ApplyDaySwitch();
    }

    /// <summary>
    /// 按当前昼夜状态授权夜间照明战备（夜晚解锁、白天收回）。
    /// 授权是计数制（authorizeCounter，0=未授权）且 0 就是"白天"的自然状态，所以白天不能做减法：
    /// 开局白天时计数本来就是 0，补发再 -1 会变 -1，之后入夜 +1 只回到 0，永远不满足
    /// IsAuthorize(counter > 0)（ADSO_Y_Lamp/ADSO_Y_Llluminator 都是 authorize=1）⇒ 战备永久锁死。
    /// 因此只在"入夜加一次 / 从夜晚回白天把那次收回"时动计数，同一状态不重复应用。
    /// </summary>
    private void ApplyDaySwitch()
    {
        if (ADCont == null || !_dayIsNoon.HasValue) return;
        bool isNight = !_dayIsNoon.Value;
        if (isNight == _nightAuthorized) return;
        _nightAuthorized = isNight;
        Authorize(Constants.LampTowerId, isNight);
        Authorize(Constants.IlluminatorId, isNight);
    }

    /// <summary>
    /// 应用本局选择的全队强化效果。
    /// 从 TeamManager.Self.teamEnhance 读取 ID，映射到对应强化类型并应用到各系统。
    /// </summary>
    private void ApplyTeamEnhance()
    {
        _activeTeamEnhance = TeamManager.Instance.players.Where(item => item.boosterId > 0).Select(item =>ResSvc.boostDic[item.boosterId].type).ToArray();
        FPSGame.Data.BattleState.ActiveTeamEnhance = _activeTeamEnhance;//数据自持同步点（唯一写入点，见 BattleState.cs）

    }

    public bool HaveBooster(BoosterType type)
    {
       return _activeTeamEnhance.Contains(type);
    }

    #endregion
}
}
