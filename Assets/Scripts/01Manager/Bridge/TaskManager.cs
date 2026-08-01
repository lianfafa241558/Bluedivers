using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core;
using Core.Interface;
using FpsGame.MapUtils;
using GameContract;

using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using Utils;
using Tool = Utils.Tool;

public class TaskManager : Singleton<TaskManager>,I_GlobaManager
{

    public int AreaCount,TaskCount;

    private Dictionary<MissionEnum, MissionData_SO> Missions;
    public Dictionary<EnemyVarietyType, CampData_SO> Camps;
    public Dictionary<string, MapData_SO> MapData;

    [SerializeField]
    private string[] codeA, codeB;
    private List<string> residualCodeA, residualCodeB;

    [SerializeField]
    private DisplayDic<string, Sprite> OccupierIcon;

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
        Camps = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<CampData_SO>("GameData/Camp"),item => item.enemyVarietyType);
        MapData = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<MapData_SO>("GameData/Map"), item => item.name.Substring(3));
        TaskCfgs = new TaskCfg[AreaCount,TaskCount];
        nowTask = new();
        CreatAllTask();
        if (GameRoot.Instance.IsLocal)
        {
            SetTask("Millennium", 0, DifficultyEnum.Insane, new int[4], 2);

        }
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
        TaskRandom = new(now.Month * 100 + now.Day + now.Hour * 100 + (now.Minute / 5 * 5));//半小时刷新一次
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
        var roleData = roleDataList.Find(r => r.ID == ArchiveSvc.Archive.lastSelectRole);
        if (roleData != null && roleData.DefaultAirdropIDs != null)
            task.RequiredAD.AddRange(roleData.DefaultAirdropIDs);

        task.RequiredAD = task.RequiredAD.Distinct().ToList();

        task.difficulty = difficulty;
        System.Array.Copy(extraDiff, task.ExtraDifficulty, 4);

        task.mapName = mapData.AreaName;
        task.PlayMode = playMode;
        task.SpecialtyPropertys = mapData.product;
        task.OtherPropertys = ((OOPartEnum[])System.Enum.GetValues(typeof(OOPartEnum))).Except(mapData.product).ToArray(); ;
        task.Countdown = 16;
        task.activeTask = true;


        task.BattleData.Clear();
        for (int i =0;i<RoomManager.Instance.players.Count;++i)
        {
            task.BattleData.Add(new(DefaultBattleData));
        }
        Constants.TaskBorder = task.MapBorder;
        GameRoot.GameState = GameStateEnum.Ready;
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

    public void EnterTransition()
    {
        nowTask.Countdown = 16;
        //WndManager.Instance.armamentWnd.SetWndState();
        GameRoot.GameState = GameStateEnum.Armament;
        //GameStateManager.GameState = GameStateEnum.Transition;
        //AudioManager.PlayMusic("Shooting Athletes",0.3f);
    }
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


    [System.Serializable]
    public class SelectTaskData
    {

        /// <summary>任务配置</summary>
        public TaskCfg taskCfg { get; set; }
        public MapData_SO mapCfg { get; set; }
        public CampData_SO campData { get; set; }

        public Dictionary<OOPartEnum, int> collectProperty = new();
        public List<Dictionary<string, int>> BattleData = new();
        /// <summary>任务所需战备</summary>
        public List<int> RequiredAD;

        public bool activeTask;

        public TaskItem main;
        public TaskItem evacuate;
        public TaskItem[] extras;
        public TaskItem[][] nests;
        public TaskItem[] subs;

        public GameResult result { get; set; }
        public DifficultyEnum difficulty { get; set; }
        public string mapName { get; set; }
        public int PlayMode { get; set; }
        public int Countdown { get; set; } = 16;
        public int[] ExtraDifficulty { get; set; } = new int[] { 0, 0, 0, 0 };
        public OOPartEnum[] SpecialtyPropertys { get; set; }
        public OOPartEnum[] OtherPropertys { get; set; }
        public MissionMainData_SO MainCfg => main?.cfg as MissionMainData_SO;
        /// <summary>场景模式下由 CampaignCfg 提供</summary>
        public SizeType SceneSizeType { get; set; } = SizeType.Mini;
        private SizeType EffectiveSizeType => MainCfg?.sizeType ?? SceneSizeType;

        public int MapSize => Constants.MapDefaultBorder
        +EffectiveSizeType switch {
            SizeType.Small => 256,
            SizeType.Medium => 384,
            SizeType.Large => 512,
            SizeType.Mini => 256,
            _ => 128,
        };

        public int CameraSize => EffectiveSizeType switch {
            SizeType.Mini => 192,
            _ => MapSize - Constants.MapDefaultBorder,
        };

        /// <summary>地图边缘的半径</summary>
        public int MapBorder => (MapSize - CameraSize) / 2;

        public int MapHeight => EffectiveSizeType switch {
            SizeType.Small => 64,
            SizeType.Medium => 80,
            SizeType.Large => 96,
            _ => 64,
        };

  
        public int MainReward =>main.complete ? main.reward : 0;
        public int ExtraReward => extras.Sum(item => item.complete ? item.reward : 0);
        public int NestReward {
            get {
                int re = 0;
                for (int i = 0; i < nests.Length; ++i)
                {
                    if(nests[i].Length>0) re += nests[i].Sum(item=>item.complete?1:0*item.reward) / nests[i].Length;
                }
                return re;
            }
        }

    }

    [System.Serializable]
    /// <summary>任务配置</summary>
    public struct TaskCfg
    {
        public bool enable;
        public string name;
        public int seed;
        public float scale;
        public MissionEnum main;
        public MissionEnum[] extra;
        public int[] nestCount;
        public TerrainType terrainType;
        public EnemyVarietyType enemyVarietyType;

        public string TaskType => (Main as MissionMainData_SO).name;
        public string TaskDesc => Main.desc;
        public Color Color => (Main as MissionMainData_SO).color;
        public Sprite Sprite => Main.sprite;

        public int MainReward=> Main.reward.Lerp(scale);
        public int ExtraReward =>extra.Select(item=> Instance.Missions[item].reward.y).Sum();

        private MissionData_SO Main => Instance.Missions[main];
    }

    [System.Serializable]
    public class TaskItem
    {
        public MissionData_SO cfg;
        public int targetCount;//主要任务需要的进度(感觉大部分其实都用不上)
        public int reward;//最终返回的报酬
        public bool complete;

        public TaskItem(MissionData_SO cfg)
        {
            this.cfg = cfg;
            targetCount = 0;
            reward = cfg.reward.x;
        }
        public TaskItem(MissionMainData_SO cfg, float scale)
        {
            this.cfg = cfg;
            targetCount = cfg.count.Lerp(scale);
            reward = cfg.reward.Lerp(scale);
        }
    }



}





public enum MissionEnum
{
    /// <summary>歼灭</summary>
    [InspectorName("主线/歼灭")]Annihilation,
    /// <summary>解救</summary>
    [InspectorName("主线/解救")]Rescue,
    /// <summary>采集</summary>
    [InspectorName("主线/采集")]Explore,
    /// <summary>护送</summary>
    [InspectorName("主线/护送")] Escort,
    /// <summary>上传数据</summary>
    [InspectorName("主线/上传数据")] RetrieveData,
    /// <summary>防御</summary>
    [InspectorName("主线/防御")]Defend,
    /// <summary>升旗</summary>
    [InspectorName("主线/升旗")] FlagRaising,
    /// <summary>彻底消灭</summary>
    [InspectorName("主线/彻底消灭")] Eradicate,
    /// <summary>搜索并摧毁</summary>
    [InspectorName("主线/搜索并摧毁")] SearchAndDestroy,


    /// <summary>摧毁虫卵</summary>
    [InspectorName("主线/摧毁虫卵")] DestroyEggs,
    /// <summary>采集虫蛋</summary>
    [InspectorName("主线/采集虫蛋")] CollectEggs,
    /// <summary>占位符</summary>
    [InspectorName("主线/钻机摧毁工厂")] NukeNursery,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder23,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder24,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder25,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder26,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder27,

    /// <summary>占位符</summary>
    [InspectorName("主线/摧毁空军基地")] Airport,
    /// <summary>占位符</summary>
    [InspectorName("主线/拦截车队")] Motorcade,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder30,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder31,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder32,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder33,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder34,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder35,

    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder36,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder37,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder38,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder39,


    [InspectorName("主线/战役")]
    /// <summary>主线/战役</summary>
    Campaign,

    /// <summary>撤离/迅速撤离</summary>
    [InspectorName("撤离/迅速撤离")] EvacuateFast,
    /// <summary>撤离/动态撤离</summary>
    [InspectorName("撤离/动态撤离")] EvacuateMove,
    /// <summary>撤离/静态撤离</summary>
    [InspectorName("撤离/静态撤离")] EvacuateStatic,
    /// <summary>占位符</summary>
    [InspectorName("主线/占位符")] Placeholder40,


    /// <summary>次要/黑盒</summary>
    [InspectorName("次要/黑盒")] BlackBox,
    /// <summary>次要/激光雷达站</summary>
    [InspectorName("次要/激光雷达站")] RadarStation,
    /// <summary>次要/非法广播</summary>
    [InspectorName("次要/非法广播")] Broadcast,
    /// <summary>科研哨站</summary>
    [InspectorName("次要/科研哨站")] ScienceFacility,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder2,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder3,

    /// <summary>次要/飞龙巢</summary>
    [InspectorName("次要/飞龙巢")] SpireNest,
    /// <summary>次要/隐刀巢穴</summary>
    [InspectorName("次要/隐刀巢穴")] StealthNest,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder4,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder5,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder6,


    /// <summary>次要/直升机制造厂</summary>
    [InspectorName("次要/直升机制造厂")]
    HelicopterFactory,
    /// <summary>次要/干扰塔/机器人</summary>
    [InspectorName("次要/干扰塔/机器人")]
    JammingTowerRoBot,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder7,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder8,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder9,

    /// <summary>次要/干扰塔/色彩</summary>
    [InspectorName("次要/干扰塔/色彩")]
    JammingTowerColour,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder10,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder11,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder12,
    /// <summary>占位符</summary>
    [InspectorName("次要/占位符")] Placeholder13,

    [InspectorName("巢穴/十字神明-S")] NestDecS = 100,
    [InspectorName("巢穴/十字神明-M")] NestDecM = 101,
    [InspectorName("巢穴/十字神明-L")] NestDecL = 102,

    [InspectorName("巢穴/凯撒-S")] NestKaiserS = 104,
    [InspectorName("巢穴/凯撒-M")] NestKaiserM = 105,
    [InspectorName("巢穴/凯撒-L")] NestKaiserL = 106,

    [InspectorName("巢穴/色彩-S")] NestColourS = 108,
    [InspectorName("巢穴/色彩-M")] NestColourM = 109,
    [InspectorName("巢穴/色彩-L")] NestColourL = 110,

    /// <summary>升旗子任务</summary>
    [InspectorName("子任务/升旗")] SubFlagRaising = 200,
    /// <summary>获取高价值数据</summary>
    [InspectorName("子任务/获取高价值数据")] SubGetData = 201,
    /// <summary>重启发电机</summary>
    [InspectorName("子任务/重启发电机")] SubRestartGenerator = 202,
    /// <summary>连接油管</summary>
    [InspectorName("子任务/连接油管")] ConnectPipes = 203,

}

public enum DifficultyEnum
{
    Normal,
    Hard,
    VeryHard,
    HardCode,
    Extreme,
    Insane,
    Torment,
    Lunatic,
}

/// <summary>
/// 游戏结果
/// </summary>
public enum GameResult
{
    /// <summary>未知</summary>
    [InspectorName("未知")] Unknow,
    /// <summary>胜利</summary>
    [InspectorName("胜利")] Victory,
    /// <summary>失败</summary>
    [InspectorName("失败")] Failure,
    /// <summary>中断</summary>
    [InspectorName("中断")] Interrupt,
}
/// <summary>
/// 尺寸大小(复制)
/// </summary>
public enum SizeType
{
    /// <summary> 小型</summary>
    Small,
    /// <summary> 中型</summary>
    Medium,
    /// <summary> 大型</summary>
    Large,
    /// <summary> 迷你</summary>
    Mini,
}