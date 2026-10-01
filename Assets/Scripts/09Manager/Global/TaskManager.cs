using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.Game;
using FPSGame.DayNightSystem;

namespace FPSGame.Managers
{

    using UnityEngine;
    using FPSGame.Utils;
    using FPSGame.Data;
    using FPSGame.Gameplay;
using FPSGame.GameData;


/// <summary>
/// 任务生成、难度与结算结果管理。
/// </summary>

public class TaskManager : Singleton<TaskManager>,I_GlobaManager
{
    /// <summary>无任务时的中性难度系数（全 0 ⇒ 不缩放）。</summary>
    private static readonly int[] s_neutralExtraDiff = new int[4];

    /// <summary>
    /// 把下层需要的那几个值发布到数据自持点（2026-10-01 取代 <c>ServiceLocator.Task</c> 槽 + <c>ITaskService</c> 契约）。
    ///
    /// <para>▍⚠ 必须在本类**所有**会改这些值的地方调用 —— 目前 2 处：<c>Init()</c>（建任务）与 <c>SetTask()</c>（选图/难度）。</para>
    /// <para>▍<c>CollectProperty</c> 传的是**引用**：<c>OOPart</c> 会直接往这个字典里累加，不能做快照
    /// （已核对全仓：该字段在 <c>SelectTaskData</c> 初始化后再未被重新赋值）。</para>
    /// <para>▍<c>Countdown</c> 不在同步之列：它是"运行时读数"，由 <c>MedivacController</c> 推进（递减 / 按强化项设初值）；
    /// 复位到 16 发生在**开始新一局**（本类 <c>SetTask()</c>，与 <c>task.Countdown = 16</c> 原本的位置一致）。
    /// <c>TaskState.Countdown</c> 就是它的唯一存放处。</para>
    /// </summary>
    private void SyncTaskState()
    {
        TaskState.HasTask = nowTask != null;
        TaskState.ExtraDifficulty = nowTask != null && nowTask.ExtraDifficulty != null ? nowTask.ExtraDifficulty : s_neutralExtraDiff;
        TaskState.Difficulty = nowTask != null ? nowTask.difficulty : DifficultyEnum.Normal;
        TaskState.EnemyVarietyType = nowTask != null ? nowTask.taskCfg.enemyVarietyType : default;
        TaskState.CollectProperty = nowTask != null ? nowTask.collectProperty : new Dictionary<OOPartEnum, int>();
    }

    public int AreaCount,TaskCount;

    private Dictionary<MissionEnum, MissionData_SO> Missions;
    public Dictionary<EnemyVarietyType, CampData_SO> Camps;
    public Dictionary<string, MapData_SO> MapData;

    [SerializeField]
    private string[] codeA, codeB;
    private List<string> residualCodeA, residualCodeB;

    [SerializeField]
    private DisplayDic<string, Sprite> OccupierIcon;

    public string MapId => nowTask.mapCfg.AreaName;

    public EnemyVarietyType EnemyVarietyType => nowTask.taskCfg.enemyVarietyType;


    //[SerializeField]
    //private DisplayDic<string,KVP<Sprite,Sprite>> MapIcon;



    public TaskCfg[,] TaskCfgs;

    public SelectTaskData nowTask;// { get;private set; }


    private System.Random TaskRandom { get; set; }

    internal static Dictionary<string, int> DefaultBattleData = new() {
        ["击杀敌人"] = 0,
        ["开火次数"] = 0,
        ["命中次数"] = 0,
        ["死亡次数"] = 0,
        ["救援次数"] = 0,
        
        ["使用补给次数"] = 0,
        ["呼叫战备次数"] = 0,
        ["采集欧帕兹数量"] = 0,
    };



    public void Init()
    {
        Awake();
        Missions = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<MissionData_SO>("GameData/Mission"),item => item.type);
        MissionData_SO.Catalog = Missions;//数据自持：玩法层的 TaskCfg 从这里读（见 MissionData_SO.Catalog）
        Camps = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<CampData_SO>("GameData/Camp"),item => item.enemyVarietyType);
        MapData = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<MapData_SO>("GameData/Map"), item => item.name.Substring(3));
        TaskCfgs = new TaskCfg[AreaCount,TaskCount];
        nowTask = new();
        CreatAllTask();
        if (GameRoot.Instance.IsLocal)
        {
            SetTask("Millennium", 0, DifficultyEnum.Insane, new int[4], 2);

        }
        SyncTaskState();//发布给下层（数据自持）
    }

    public void UnInit()
    {

    }

    private bool hasTriggeredThisMinute;
    void Update()
    {
        var now = System.DateTime.Now;
        if (now.Minute == 0 || now.Minute == 30)
        {
            if (!hasTriggeredThisMinute)
            {
                hasTriggeredThisMinute = true;
                CreatAllTask();
            }
        }
        else
        {
            hasTriggeredThisMinute = false;
        }
    }

    /// <summary>
    /// ��ʼ��������������
    /// </summary>
    private void CreatAllTask()
    {
        var now = System.DateTime.Now;
        //TaskRandom = new(now.Month*100+now.Day+now.Hour*100+(now.Minute/30*30));//半小时刷新一次
        TaskRandom = new(now.Month * 100 + now.Day + now.Hour * 100 + (now.Minute / 2 * 2));//2分钟刷新一次
        residualCodeA = new(codeA);
        residualCodeB = new(codeB);
        var values = MapData.Values.ToList();
        for (int i = 0; i < AreaCount; ++i)
        {
            var enemyType = values[i].enemyVarietyType;
            var camp = Camps[enemyType];
            var mainTypes = camp.mainTypes;
            var extraTypes = camp.extraTypes;
            var nestTypes = camp.nestTypes;
            if (values[i].mapItemInfos.Length == 0) continue;
            for (int u = 0; u < Mathf.Min(TaskCount, values[i].mapItemInfos.Length); ++u)
            {
                var mainType = mainTypes.RandomTake(TaskRandom);
                var missionCfg = (MissionMainData_SO)Missions[mainType];

                TaskCfgs[i, u] = new() {
                    enable = TaskRandom.Bool(),
                    name = RandomName(),
                    seed = TaskRandom.Range(0, 114514),
                    scale = TaskRandom.Range(0, 1f),
                    main = mainType,
                    extra = CreatExtra(extraTypes, missionCfg.sizeType switch {
                        SizeType.Small => 2,
                        SizeType.Medium => 3,
                        SizeType.Large => 5,
                        _ => 0
                    }),
                    nestCount = missionCfg.sizeType switch {
                        SizeType.Small => new int[3] { 1, 0, 0 },
                        SizeType.Medium => new int[3] { 2, 1, 0 },
                        SizeType.Large => new int[3] { 2, 2, 1 },
                        _ => new int[3] { 0, 0, 0 },
                    },
                    terrainType = values[i].mapItemInfos[u].terrainType,
                    enemyVarietyType = values[i].mapItemInfos[u].enemyVarietyType,
                };
            }
        }
        MissionEnum[] CreatExtra(MissionEnum[] arr,int count)
        {
        //TODO:为了方便测试
        //count = 5;
            MissionEnum[] re = new MissionEnum[count];
            for(int i = 0; i < re.Length; ++i)
            {
                re[i] = arr.RandomTake(TaskRandom);
            }
            return re;
        }
        /*
        TaskItem[][] CreatNest(MissionEnum[] arr, int[] counts)
        {
            //TODO:Ϊ�˷������
            counts = new int[3] { 8, 4, 1 };
            TaskItem[][] re = new TaskItem[counts.Length][];
            for (int u=0;u< counts.Length;++u)
            {
                    //Debug.LogError("类型 "+arr[u]+arr[u].GetEnumString());
                    //Debug.LogError("实例 " + Missions[arr[u]]);

                TaskItem[] item = new TaskItem[counts[u]];
                for (int i = 0; i < item.Length; ++i)
                {
                    item[i] = new(Missions[arr[u]]);
                }
                re[u] = item;
            }
            return re;
        }*/
    }


    private string RandomName()//不会重复出现
    {
        if (residualCodeA.Count==0 || residualCodeB.Count==0)
        {
            residualCodeA = new(codeA);
            residualCodeB = new(codeB);
        }
        return TaskRandom.RandomTake(residualCodeA, true) + TaskRandom.RandomTake(residualCodeB,true);
    }

    public float FinalDiffScale()
    {
        float re= DiffScale(nowTask.difficulty);
        for(int i = 0; i < 4; ++i)
        {
            re += ExtraDiffScale(nowTask.difficulty)* nowTask.ExtraDifficulty[i];
        }
        return re;
    }

    public float DiffScale(DifficultyEnum value)
    {
        return value switch {
            DifficultyEnum.Normal => 0.4f,
            DifficultyEnum.Hard => 0.6f,
            DifficultyEnum.VeryHard => 0.8f,
            DifficultyEnum.HardCode => 1f,
            DifficultyEnum.Extreme => 1.5f,
            DifficultyEnum.Insane => 2f,
            DifficultyEnum.Torment => 2.5f,
            DifficultyEnum.Lunatic => 3f,
            _ => 0
        };
    }

    public float ExtraDiffScale(DifficultyEnum value)
    {
        switch (value)
        {
            case DifficultyEnum.Normal:
                return 0.05f;
            case DifficultyEnum.Hard:
                return 0.06f;
            case DifficultyEnum.VeryHard:
                return 0.07f;
            case DifficultyEnum.HardCode:
                return 0.08f;
            case DifficultyEnum.Extreme:
                return 0.1f;
            case DifficultyEnum.Insane:
                return 0.15f;
            case DifficultyEnum.Torment:
                return 0.2f;
            case DifficultyEnum.Lunatic:
                return 0.25f;
            default:
                return 1f;
        }
    }

    public void SetTask(string mapId,int taskIndex,DifficultyEnum difficulty,int[] extraDiff,int playMode)
    {
        int mapIndex = MapData.Keys.ToList().FindIndex(item=>item==mapId);
        var mapData = MapData[mapId];
        var task = nowTask;
        task.taskCfg = TaskCfgs[mapIndex, taskIndex];
        task.campData = Camps[task.taskCfg.enemyVarietyType];
        task.mapCfg = mapData;
        // 地图雾色注入氛围桥：昼夜模块从桥上取雾色本色，避免昼夜系统跨层直引地图数据
        WeatherAtmosphereController.FogColorGradient = mapData.fogColor;
        // 地图可选覆盖天空盒的天空/赤道色（未勾选则注入 null → 昼夜模块沿用场景 Day-Night-Manager 上的渐变）
        bool overSkyColor = mapData.skyColor != null && mapData.equatorColor != null;
        WeatherAtmosphereController.SkyColorGradient = overSkyColor ? mapData.skyColor : null;
        WeatherAtmosphereController.EquatorColorGradient = overSkyColor ? mapData.equatorColor : null;
        //Debug.LogError("选择的敌人类�? + mapData.enemyVarietyType+" 名称" + task.campData.name);
        /*
        //TODO:测试
        var size = (Missions[MissionEnum.Explore] as MissionMainData_SO).sizeType;
        task.taskCfg = new() {
            main = MissionEnum.Explore,
            extra = task.taskCfg.extra.Take(size switch {
                SizeType.Small => 2,
                SizeType.Medium => 3,
                SizeType.Large => 5,
                _ => 0
            }).ToArray(),
            //extra = task.taskCfg.extra,
            nestCount = size switch {
                SizeType.Small => new int[3] { 1, 1, 0 },
                SizeType.Medium => new int[3] { 3, 1, 0 },
                SizeType.Large => new int[3] { 2, 2, 1 },
                _ => new int[3] { 0, 0, 0 },
            },
            name = task.taskCfg.name,
            scale = task.taskCfg.scale,
            seed = task.taskCfg.seed,
            enable = task.taskCfg.enable,
        };
        */



        task.main = new TaskItem((MissionMainData_SO)Missions[task.taskCfg.main], task.taskCfg.scale);
        task.evacuate = new TaskItem(Missions[task.MainCfg.evacuateType]);

        task.extras = task.taskCfg.extra.Select(item => new TaskItem(Missions[item])).ToArray();
        task.nests = task.taskCfg.nestCount.Select((count, index) =>
            Enumerable.Repeat(0, count)
            .Select(_ => new TaskItem(Missions[task.campData.nestTypes[index]])).ToArray()
        ).ToArray();

        var subTypes = (Missions[task.taskCfg.main] as MissionMainData_SO).subType;
        if (subTypes != null) task.subs = subTypes.Select(item => new TaskItem(Missions[item])).ToArray();
        else task.subs = new TaskItem[0];
        //task.RequiredAD = new() {10,11,16,17};
        task.RequiredAD = new() { Constants.SupplyId,Constants.HealBag, Constants.IlluminatorId, Constants.LampTowerId };
        task.RequiredAD.AddRange(task.MainCfg.RequiredAD.Select(item => item.ID));
        task.RequiredAD.AddRange(task.taskCfg.extra.SelectMany(item => Missions[item].RequiredAD).Select(item => item.ID));
        if (subTypes != null) task.RequiredAD.AddRange(subTypes.SelectMany(item => Missions[item].RequiredAD).Select(item => item.ID));

        // 加入当前角色默认战备ID
        var roleDataList = ResSvc.Instance.LoadObjects<RoleData_SO>("GameData/Role");
        var roleData = roleDataList.Find(r => r.ID == ArchivesData_SO.Current.lastSelectRole);
        if (roleData != null && roleData.DefaultAirdropIDs != null)
            task.RequiredAD.AddRange(roleData.DefaultAirdropIDs);

        task.RequiredAD = task.RequiredAD.Distinct().ToList();

        task.difficulty = difficulty;
        System.Array.Copy(extraDiff, task.ExtraDifficulty, 4);


        task.PlayMode = playMode;
        task.SpecialtyPropertys = mapData.product;
        task.OtherPropertys = mapData.otherProduct;
        TaskState.Countdown = 16;//倒计时读数复位（运行时唯一存放处是 TaskState；原 task.Countdown 已不参与逻辑）
        task.activeTask = true;


        task.BattleData.Clear();
        for (int i =0;i<RoomManager.Instance.players.Count;++i)
        {
            task.BattleData.Add(new(DefaultBattleData));
        }
        Constants.TaskBorder = task.MapBorder;
        GameRoot.GameState = GameStateEnum.Ready;
        //倒计时读数走数据自持（2026-10-01 取代 nowTask.Countdown）
        WndManager.Instance.CreatCountDown(()=> TaskState.Countdown,CountDownTypeEnum.Blue);
        SyncTaskState();//发布给下层（数据自持）：难度 / 系数 / 敌人种类 / 收集表
    }

    /// <summary>
    /// 场景模式：从 CampaignCfg 补全 nowTask 的地图级数据（RequiredAD、campData、BattleData）
    /// </summary>
    public void EnsureSceneData(CampaignCfg cfg)
    {
        var task = Instance.nowTask;
        task.RequiredAD = new List<int>(cfg.useAirdrops);
        task.campData = Instance.Camps[cfg.enemy];
        task.SceneSizeType = cfg.sizeType;
        if (task.BattleData == null || task.BattleData.Count == 0)
        {
            task.BattleData = new();
            for (int i = 0; i < RoomManager.Instance.players.Count; ++i)
                task.BattleData.Add(new(DefaultBattleData));
        }
    }

    // ⚠ `EnterTransition()` 已于 2026-10-01 删除（原实现 = `TaskState.Countdown = 16` + `GameRoot.GameState = Armament`）：
    //   · **切阶段** → 交给发布方 `MedivacController` 走事件 `GlobalEventSub.RequestGameState(GameStateEnum.Armament)`
    //     （⚠ 同日 `IFlowService` 与 `ServiceLocator.Flow` 已整体删除：`MissionEvacuate*` 各自的 1 处也改成同一事件）
    //   · **倒计时复位** → 归位到"开始新一局"的职责（本类 `SetTask()`），不再由"结束一局"兼任
    //   ⇒ 于是既不需要专属事件、也不需要订阅 `OnGameStateChange` 广播。
    //   ⚠ 前提（已与用户确认）：`MedivacController` **一局只会走到该分支一次**。
    /*

    public void GetMainTaskInfo(MainTaskEnum type,out Sprite sprite,out Color color)
    {
        var item=MainTask.Get(type);
        sprite = item.sprite;
        color = item.color;
    }
    
    public void GetExtraTaskInfo(ExtraTaskEnum type)
    {
        return ExtraTask.Get(type);
    }
    */

    public Sprite GetOccupierIcon(string name)
    {
        return OccupierIcon[name];
    }





}
}
