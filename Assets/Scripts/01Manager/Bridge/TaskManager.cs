using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core;
using Core.Interface;
using GameContract;
using Unity.BaseTool;
using UnityEditor;
using UnityEngine;
using Utils;
using Tool = Utils.Tool;

public class TaskManager : Singleton<TaskManager>,I_GlobaManager
{


    public int AreaCount,TaskCount;

    private Dictionary<MissionEnum, MissionData_SO> Missions;
    private Dictionary<EnemyVarietyType, CampData_SO> Camps;

    [SerializeField]
    private string[] codeA, codeB;
    private List<string> residualCodeA, residualCodeB;

    [SerializeField]
    private DisplayDic<string, Sprite> OccupierIcon;

    //[SerializeField]
    //private DisplayDic<string,KVP<Sprite,Sprite>> MapIcon;

    public DisplayDic<string, _MapData> MapData;


    public TaskInfo[,] TaskInfos;

    public SelectTaskData nowTaskCfg { get;private set; }


    private System.Random TaskRandom { get; set; }

    private static Dictionary<string, int> _DefaultBattleData = new() {
        ["击杀敌人"] = 0,
        ["开火次数"] = 0,
        ["命中次数"] = 0,
        ["死亡次数"] = 0,
        ["救援次数"] = 0,
        
        ["使用补给次数"] = 0,
        ["呼叫战备次数"] = 0,
        ["采集欧帕兹数量"] = 0,//这个还没做
    };

    public void Init()
    {
        Awake();
        Missions = Enumerable.ToDictionary(ResManager.Instance.LoadObjects<MissionData_SO>("GameData/Mission"),item => item.type);
        Camps = Enumerable.ToDictionary(ResManager.Instance.LoadObjects<CampData_SO>("GameData/Camp"),item => item.enemyVarietyType);

        TaskInfos = new TaskInfo[AreaCount,TaskCount];
        nowTaskCfg = new();
        CreatAllTask();
        //if (GameRoot.Instance.IsLocal)
        {
            SetTask("Millennium", 0, DifficultyEnum.Insane, new int[4], 2);
            nowTaskCfg.nowTask.main.cfg= Missions[MissionEnum.Eradicate];
            nowTaskCfg.nowTask.evacuate.cfg = Missions[MissionEnum.EvacuateFast];

            //nowTaskCfg.nowTask.main.complete = true;
            /*
            for (int i=0;i< nowTaskCfg.nowTask.extra.Length;++i)
            {
                nowTaskCfg.nowTask.extra[i].cfg = ExtraTask[MissionEnum.Broadcast];
            }*/

            /*
            for (int i=0;i< nowTaskCfg.SpecialtyPropertys.Length;++i)
            {
                nowTaskCfg.collectProperty[nowTaskCfg.SpecialtyPropertys[i]]= TaskRandom.Range(1, 20);
            }*/
            /*
            for (int i = 0; i < nowTaskCfg.BattleData.Count; ++i)
            {
                nowTaskCfg.BattleData[i]["击杀敌人"] = TaskRandom.Range(1, 500);
                nowTaskCfg.BattleData[i]["开火次数"] = TaskRandom.Range(1, 1000);
                nowTaskCfg.BattleData[i]["死亡次数"] = TaskRandom.Range(1, 10);
                nowTaskCfg.BattleData[i]["救援次数"] = TaskRandom.Range(1, 10);

                nowTaskCfg.BattleData[i]["命中次数"] = TaskRandom.Range(1, 1000);
                nowTaskCfg.BattleData[i]["使用补给次数"] = TaskRandom.Range(1, 100);
                nowTaskCfg.BattleData[i]["呼叫战备次数"] = TaskRandom.Range(1, 100);
                nowTaskCfg.BattleData[i]["采集欧帕兹数量"] = TaskRandom.Range(1, 100);
            }*/
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
    /// 初始化创建所有任务
    /// </summary>
    private void CreatAllTask()
    {
        var now = System.DateTime.Now;
        TaskRandom = new(now.Month*100+now.Day+now.Hour*100+(now.Minute/30*30));//每小时刷新
        residualCodeA = new(codeA);
        residualCodeB = new(codeB);
        for (int i = 0; i < AreaCount; ++i)
        {
            var enemyType = MapData.TryGetIndex(i).Value.enemyVarietyType;
            var camp = Camps[enemyType];
            var mainTypes = camp.mainTypes;
            var extraTypes = camp.extraTypes;
            var nestTypes = camp.nestTypes;

            for (int u = 0; u < TaskCount; ++u)
            {
                var mainType = mainTypes.RandomTake(TaskRandom);
                float scale = TaskRandom.Range(0, 1f);

                MissionMainData_SO missionCfg = (MissionMainData_SO)Missions[mainType];

                TaskInfos[i, u] = new() {
                    enable = TaskRandom.Bool(),
                    name = RandomName(),
                    seed = TaskRandom.Range(0,114514),
                    main = new(missionCfg, scale),
                    extra = CreatExtra(extraTypes, missionCfg.sizeType switch {
                        SizeType.Small => 0,
                        SizeType.Medium => 3,
                        SizeType.Large => 5,
                        _ => 0
                    }),
                    nest = CreatNest(nestTypes, missionCfg.sizeType switch {
                        SizeType.Small => new int[3] { 0, 0, 0 },
                        SizeType.Medium => new int[3] { 6, 3, 0 },
                        SizeType.Large => new int[3] { 8, 4, 1 },
                        _ => new int[3] { 0, 0, 0 },
                    }),
                    evacuate = new(Missions[missionCfg.evacuateType])
                };
            }
        }
        TaskItem[] CreatExtra(MissionEnum[] arr,int count)
        {
            //TODO:为了方便测试
            count = 3;
            TaskItem[] re = new TaskItem[count];
            for(int i = 0; i < re.Length; ++i)
            {
                re[i] = new(Missions[arr.RandomTake(TaskRandom)]);
            }
            return re;
        }

        TaskItem[][] CreatNest(MissionEnum[] arr, int[] counts)
        {
            //TODO:为了方便测试
            counts = new int[3] { 3, 1, 0 };
            TaskItem[][] re = new TaskItem[counts.Length][];
            for (int u=0;u< counts.Length;++u)
            {
                //Debug.LogError("类型"+arr[u]+arr[u].GetEnumString());
                //Debug.LogError("实例" + Missions[arr[u]]);

                TaskItem[] item = new TaskItem[counts[u]];
                for (int i = 0; i < item.Length; ++i)
                {
                    item[i] = new(Missions[arr[u]]);
                }
                re[u] = item;
            }
            return re;
        }
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
        float re= DiffScale(nowTaskCfg.difficulty);
        for(int i = 0; i < 4; ++i)
        {
            re += ExtraDiffScale(nowTaskCfg.difficulty)* nowTaskCfg.ExtraDifficulty[i];
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
        int mapIndex = MapData.Keys.FindIndex(item=>item==mapId);
        var data = MapData[mapId];
        var task = nowTaskCfg;
        task.nowTask = TaskInfos[mapIndex, taskIndex];

        //task.RequiredAD = new() {10,11};
        task.RequiredAD = new() { 10, 11 ,12};//先直接加上
        task.RequiredAD.AddRange(task.MainCfg.RequiredAD.Select(item=>item.ID));
        for(int i=0;i< task.nowTask.extra.Length; ++i)
        {
            task.RequiredAD.AddRange(task.nowTask.extra[i].cfg.RequiredAD.Select(item => item.ID));
        }
        task.RequiredAD=task.RequiredAD.Distinct().ToList();

        task.nestCount = task.MainCfg.sizeType switch {
            SizeType.Small => new int[3] { 0, 0, 0 },
            SizeType.Medium => new int[3] { 6, 3, 0 },
            SizeType.Large => new int[3] { 8, 4, 1 },
            _ => new int[3] { 0, 0, 0 },
        };

        task.difficulty = difficulty;
        task.mapName = data.MapName;
        task.PlayMode = playMode;
        task.SpecialtyPropertys = data.product;
        task.OtherPropertys = ((OOPartEnum[])System.Enum.GetValues(typeof(OOPartEnum))).Except(data.product).ToArray(); ;
        task.mapIcon = data.Icon;
        task.mapImage = data.Map;
        task.activeTask = true;
        task.campData = Camps[data.enemyVarietyType];
        for (int i = 0; i < 4; ++i)
        {
            task.ExtraDifficulty[i] = extraDiff[i];
        }
        task.BattleData.Clear();
        for (int i =0;i<RoomManager.Instance.players.Count;++i)
        {
            task.BattleData.Add(new(_DefaultBattleData));
        }

        nowTaskCfg.Countdown = 16;

        GameRoot.GameState = GameStateEnum.Ready;

    }

    public void EnterTransition()
    {
        nowTaskCfg.Countdown = 16;
        //WndManager.Instance.armamentWnd.SetWndState();
        GameRoot.GameState = GameStateEnum.Armament;
        //GameRoot.GameState = GameStateEnum.Transition;
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
        /// <summary>任务所需战备</summary>
        public List<int> RequiredAD;

        public GameResult result { get; set; }
        public DifficultyEnum difficulty { get; set; }
        public string mapName { get; set; }
        public int PlayMode { get; set; }
        public int Countdown { get; set; } = 16;
        public int[] ExtraDifficulty { get; set; } = new int[] { 0, 0, 0, 0 };
        public OOPartEnum[] SpecialtyPropertys { get; set; }
        public OOPartEnum[] OtherPropertys { get; set; }

        public int MapSize => MainCfg.sizeType switch {
            SizeType.Small => 256+Constants.MapBorder,
            SizeType.Medium => 512+Constants.MapBorder,
            SizeType.Large => 1024 + Constants.MapBorder,
            _ => 128+Constants.MapBorder,
        };
        public int CameraSize => MapSize-Constants.MapBorder;

        public int MainReward => nowTask.main.complete ? nowTask.main.reward : 0;
        public int ExtraReward => nowTask.extra.Sum(item => item.complete ? item.reward : 0);
        public int NestReward {
            get {
                int re = 0;
                for (int i = 0; i < nowTask.nest.Length; ++i)
                {
                    re += nowTask.nest[i].Sum(item=>item.complete?1:0*item.reward) / nestCount[i];
                }
                return re;
            }
        }
        public MissionMainData_SO MainCfg => (MissionMainData_SO)nowTask.main.cfg;


        /// <summary>选择的任务</summary>
        public TaskInfo nowTask { get; set; }

        /// <summary>巢穴刷新数</summary>
        public int[] nestCount;


        public Dictionary<OOPartEnum,int> collectProperty=new();

        public List<Dictionary<string, int>> BattleData=new();

        public CampData_SO campData;

        public Sprite mapImage;
        public Sprite mapIcon;
        public bool activeTask;
    }
    [System.Serializable]
    /// <summary>任务配置</summary>
    public struct TaskInfo
    {
        public bool enable;
        public string name;
        public int seed;
        public TaskItem main;
        public TaskItem[] extra;
        public TaskItem[][] nest;
        public TaskItem evacuate;
        public int ExtraAllReward=>extra.Sum(item=>item.reward);
        public Color Color => ((MissionMainData_SO)main.cfg).color;
        public Sprite Sprite => main.cfg.sprite;
    }

    [System.Serializable]
    public class TaskItem
    {
        public MissionData_SO cfg;
        public int targetCount;//主要任务需要的进度(感觉大部分其实都用不到)
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
            targetCount = (int)Tool.Mapping(cfg.count, scale);
            reward = (int)Tool.Mapping(cfg.reward, scale);
        }
    }


   
    [System.Serializable]
    public struct _MapData
    {
        public string MapName;
        public Sprite Icon, Map;
        public MapItemInfo[] mapItemInfos;
        [TextArea(5,10)]
        public string AreaDesc;
        public Sprite AreaBackground;
        [CustomLabel("特产")]
        public OOPartEnum[] product;
        [CustomLabel("敌对类型")]
        public EnemyVarietyType enemyVarietyType;

        public EnemyType enemyType => enemyVarietyType.ToEnemyType();

        [System.Serializable]
        public struct MapItemInfo
        {
            public string name;
            public Vector2Int pos;
            public bool noTask;
        }
    }

}





public enum MissionEnum
{
    /// <summary>歼灭</summary>
    [CustomLabel("主要/歼灭")]Annihilation,
    /// <summary>解救</summary>
    [CustomLabel("主要/解救")]Rescue,
    /// <summary>采油</summary>
    [CustomLabel("主要/采油")]Explore,
    /// <summary>护送</summary>
    [CustomLabel("主要/护送")] Escort,
    /// <summary>上传数据</summary>
    [CustomLabel("主要/上传数据")] RetrieveData,
    /// <summary>防守</summary>
    [CustomLabel("主要/防守")]Defend,
    /// <summary>升旗</summary>
    [CustomLabel("主要/升旗")] FlagRaising,
    /// <summary>彻底消灭</summary>
    [CustomLabel("主要/彻底消灭")] Eradicate,
    /// <summary>搜索并摧毁</summary>
    [CustomLabel("主要/搜索并摧毁")] SearchAndDestroy,

    /// <summary>摧毁虫蛋</summary>
    [CustomLabel("主要/摧毁虫蛋")] DestroyEggs,
    /// <summary>采集虫蛋</summary>
    [CustomLabel("主要/采集虫蛋")] CollectEggs,
    /// <summary>占位符</summary>
    [CustomLabel("主要/钻机摧毁工厂")] NukeNursery,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder23,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder24,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder25,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder26,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder27,

    /// <summary>占位符</summary>
    [CustomLabel("主要/摧毁空军基地")] Airport,
    /// <summary>占位符</summary>
    [CustomLabel("主要/拦截车队")] Motorcade,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder30,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder31,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder32,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder33,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder34,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder35,

    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder36,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder37,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder38,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder39,


    [CustomLabel("主要/战役")]
    /// <summary>主要/战役</summary>
    Campaign,

    /// <summary>撤离/迅速撤离</summary>
    [CustomLabel("撤离/迅速撤离")] EvacuateFast,
    /// <summary>撤离/动态撤离</summary>
    [CustomLabel("撤离/动态撤离")] EvacuateMove,
    /// <summary>撤离/静态撤离</summary>
    [CustomLabel("撤离/静态撤离")] EvacuateStatic,
    /// <summary>占位符</summary>
    [CustomLabel("主要/占位符")] Placeholder40,


    /// <summary>次要/黑盒子</summary>
    [CustomLabel("次要/黑盒子")]
    BlackBox,
    /// <summary>次要/激光雷达站</summary>
    [CustomLabel("次要/激光雷达站")]
    RadarStation,
    /// <summary>次要/非法广播</summary>
    [CustomLabel("次要/非法广播")]
    Broadcast,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder1,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder2,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder3,

    /// <summary>次要/飞龙塔</summary>
    [CustomLabel("次要/飞龙塔")] SpireNest,
    /// <summary>次要/隐刀巢穴</summary>
    [CustomLabel("次要/隐刀巢穴")] StealthNest,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder4,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder5,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder6,


    /// <summary>次要/直升机制造厂</summary>
    [CustomLabel("次要/直升机制造厂")]
    HelicopterFactory,
    /// <summary>次要/干扰塔(机器人)</summary>
    [CustomLabel("次要/干扰塔(机器人)")]
    JammingTowerRoBot,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder7,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder8,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder9,

    /// <summary>次要/干扰塔(色彩)</summary>
    [CustomLabel("次要/干扰塔(色彩)")]
    JammingTowerColour,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder10,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder11,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder12,
    /// <summary>占位符</summary>
    [CustomLabel("次要/占位符")] Placeholder13,

    [CustomLabel("巢穴/十字神明-小")] NestDecS = 100,
    [CustomLabel("巢穴/十字神明-中")] NestDecM = 101,
    [CustomLabel("巢穴/十字神明-大")] NestDecL = 102,

    [CustomLabel("巢穴/凯撒-小")] NestKaiserS = 104,
    [CustomLabel("巢穴/凯撒-中")] NestKaiserM = 105,
    [CustomLabel("巢穴/凯撒-大")] NestKaiserL = 106,

    [CustomLabel("巢穴/色彩-小")] NestColourS = 108,
    [CustomLabel("巢穴/色彩-中")] NestColourM = 109,
    [CustomLabel("巢穴/色彩-大")] NestColourL = 110,



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
    [CustomLabel("未知")] Unknow,
    /// <summary>胜利</summary>
    [CustomLabel("胜利")] Victory,
    /// <summary>失败</summary>
    [CustomLabel("失败")] Failure,
    /// <summary>中止</summary>
    [CustomLabel("中止")] Interrupt,
}
/// <summary>
/// 尺寸大小(复用)
/// </summary>
public enum SizeType
{
    /// <summary> 小</summary>
    Small,
    /// <summary> 中</summary>
    Medium,
    /// <summary> 大</summary>
    Large,

}