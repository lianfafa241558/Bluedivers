using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using FPSGame.Game;
using FPSGame.DayNightSystem;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Data;
using FPSGame.Gameplay;
using FPSGame.GameData;
using FPSGame.Net;

namespace FPSGame.Managers
{



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
        /// <para>▍⚠ 必须在本类**所有**会改这些值的地方调用 —— 目前 4 处：<c>Init()</c>（建任务）、<c>SetTask()</c>（选图/难度）、
        /// <c>EnsureSceneData()</c>（场景模式：<c>CampaignCfg</c> 补数据）与 <c>ResetTask()</c>（回大厅清空）。</para>
        /// <para>▍<c>CollectProperty</c> 传的是**引用**：<c>OOPart</c> 会直接往这个字典里累加，不能做快照
        /// （已核对全仓：该字段在 <c>SelectTaskData</c> 初始化后再未被重新赋值）。</para>
        /// <para>▍<c>Countdown</c> 不在同步之列：它是"运行时读数"，由 <c>MedivacController</c> 推进（递减 / 按强化项设初值）；
        /// 复位到 16 发生在**开始新一局**（本类 <c>SetTask()</c>，与 <c>task.Countdown = 16</c> 原本的位置一致）。
        /// <c>TaskState.Countdown</c> 就是它的唯一存放处。</para>
        /// </summary>
        private void SyncTaskState()
        {
            // 场景模式（无 TaskCfg/main，由 CampaignCfg 驱动）也算"有任务"，否则撤离点等下层逻辑会按"无任务"整段跳过
            bool haseTask= nowTask.main != null || nowTask.SceneMode;
            TaskState.HasTask = haseTask;
            TaskState.ExtraDifficulty = haseTask && nowTask.ExtraDifficulty != null ? nowTask.ExtraDifficulty : s_neutralExtraDiff;
            TaskState.Difficulty = haseTask ? nowTask.difficulty : DifficultyEnum.Normal;
            TaskState.EnemyVarietyType = haseTask ? nowTask.EnemyVarietyType : default;
            TaskState.CollectProperty = haseTask ? nowTask.collectProperty : new Dictionary<OOPartEnum, int>();
            // 本局权威随机种子（联机：房主决定、随开局广播下发；无任务时归 0 = 未指定）
            TaskState.Seed = haseTask ? _matchSeed : 0;
        }

        /// <summary>
        /// 回到大厅（<see cref="GameStateEnum.Bridge"/>）时清空上一局任务：<c>nowTask</c> 换空 + 发布中性值。
        ///
        /// <para>▍为什么挂在阶段广播上：任务的"起"是 <see cref="SetTask"/>（选图开新局），"止"就是回大厅
        /// —— 撤离结算后由场景里的 <c>BridgeRoleManager</c>（或 EndGame 判负分支）把阶段落成 <c>Bridge</c>，
        /// 本方法在那儿收摊。不这么做，上一局的 <c>nowTask</c>（难度 / 收集表 / 战备表 / <c>activeTask</c>）
        /// 会一直留在大厅，让"还有没有任务"的判断读到过期值
        /// （<c>TaskState.HasTask</c>、<c>SettingWnd</c> 里按 <c>nowTask.activeTask</c> 决定显示/隐藏任务卡）。</para>
        ///
        /// <para>▍与 <see cref="SyncTaskState"/> 同一套发布口径：复位后直接复用它发布中性值，不另写一份。</para>
        /// </summary>
        private void OnGameStateChange(GameStateEnum exit, GameStateEnum entry)
        {
            if (entry != GameStateEnum.Bridge) return;
            ResetTask();
        }

        /// <summary>
        /// 清空当前任务：<c>nowTask</c> 换成一个空任务，并把中性值发布给下层（见 <see cref="SyncTaskState"/>）。
        ///
        /// <para>▍⚠ 换实例是安全的：<c>TaskState.CollectProperty</c> 是**活引用**，而所有消费点
        /// （<c>OOPart</c> / <c>BattleManager.SubmitOOPart</c> / <c>Furniture_KeiSubmit</c>）都是**当次读取**
        /// <c>TaskState</c> 或 <c>nowTask</c>，没有谁把老字典存下来；换完立刻由 <c>SyncTaskState</c>
        /// 把 <c>TaskState.CollectProperty</c> 改指到新空表。</para>
        ///
        /// <para>▍倒计时读数 <c>TaskState.Countdown</c> 不在这里复位：它归"开始新一局"
        /// （见 <see cref="SetTask"/> 里的 <c>TaskState.Countdown = 16</c>）。
        /// 这里只让 <c>HasTask=false</c>，把一局内的逻辑（撤离点 <c>MedivacController</c> 等）关掉 ——
        /// 与"还没选图"时的中性态完全一致。</para>
        /// </summary>
        private void ResetTask()
        {
            nowTask = new ();
            _matchSeed = 0;   // 回大厅：本局种子一并复位（下一局由 SetTask 写入）
            SyncTaskState();
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

        public SelectTaskData nowTask { get;private set; }


        private System.Random TaskRandom { get; set; }

        /// <summary>正常刷新窗口：30 分钟（与"任务表每半小时换一批"的既有语义一致）。</summary>
        private const long BucketNormalSeconds = 1800;
        /// <summary>调试窗口：2 分钟（单机测试任务表刷新用，由 <see cref="useShortWindowForTest"/> 切换）。</summary>
        private const long BucketShortSeconds = 120;

        [InspectorName("单机调试：用 2 分钟窗口刷新任务表")]
        [SerializeField] private bool useShortWindowForTest;

        /// <summary>已应用过的窗口 id；<c>long.MinValue</c> = 尚未初始化（<see cref="Init"/> 会先定一次）。</summary>
        private long _lastBucket = long.MinValue;

        /// <summary>是否有"待执行"的刷新（跨桶时置位；只在舰桥真正执行）。</summary>
        private bool _pendingRefresh;

        /// <summary>本局权威随机种子（由 <see cref="SetTask"/> 写入；0 = 未指定）。发布到 <c>TaskState.Seed</c>。</summary>
        private int _matchSeed;

        /// <summary>
        /// 当前窗口 id：UTC 时间戳按窗口长度向下取整。
        /// <para>▍为什么用 UTC 而不是 <c>DateTime.Now</c>：后者是**本地时区**，跨时区必崩；
        /// 且桶 id 是连续整数，天然没有旧公式（<c>Month*100+Day+Hour*100+…</c>）的非单射碰撞问题。</para>
        /// </summary>
        private long CurrentBucket()
            => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (useShortWindowForTest ? BucketShortSeconds : BucketNormalSeconds);

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



        /// <summary>
        /// 本机当前任务的**主任务类型名**（如「歼灭」/「护送」，= <c>TaskCfg.TaskType</c>）。
        /// 没选任务 / 任务目录未就绪 / 该任务不在目录里 ⇒ null（**不抛异常**）。
        ///
        /// <para>▍为什么要这个属性：<c>TaskCfg.TaskType</c> 内部直接索引 <c>MissionData_SO.Catalog[main]</c>，
        /// Catalog 未初始化或键缺失都会抛；开房（<c>SettingWnd</c>/<c>SelectMapWnd</c>）与房间列表兜底
        /// 都要读它 ⇒ 保护只写一份，调用点直接判 null。</para>
        /// </summary>
        public string NowTaskType
        {
            get
            {
                var task = nowTask;
                if (task == null || !task.activeTask) return null;

                var catalog = MissionData_SO.Catalog;
                if (catalog == null || !catalog.ContainsKey(task.taskCfg.main)) return null;

                try { return task.taskCfg.TaskType; }
                catch { return null; }
            }
        }

        /// <summary>
        /// 本机当前任务的**主任务类型枚举值**（<c>-1</c> = 没选任务 / 未就绪；**不抛异常**）。
        /// <para>▍为什么要这个属性：<c>taskCfg.main</c> 在没选任务时是 <c>default</c>（= 第 0 个枚举，是个**合法值**）
        /// ⇒ 直接取会把"还没选任务"误报成一个真任务（开房时房间名就会带上错的枚举）。
        /// 保护只写一份，调用点一律判 <c>&lt; 0</c>。</para>
        /// <para>▍用途：开房 / 确认任务时随房间名下发（<c>RoomMeta</c> 的 <c>"#T=枚举|名字"</c>），
        /// 房间列表据此**精确**取任务类型图标与颜色。</para>
        /// </summary>
        public int NowTaskMain
        {
            get
            {
                var task = nowTask;
                if (task == null || !task.activeTask) return -1;
                return (int)task.taskCfg.main;
            }
        }

        /// <summary>
        /// 按**主任务枚举**精确取配置（房间列表**优先**走这条：广播里的 <c>MissionEnum</c> 是两端一致的整数）。
        /// 取不到（目录未就绪 / 枚举对不上）⇒ null，**不抛异常**。
        /// </summary>
        public MissionMainData_SO FindMainMission(MissionEnum main)
        {
            var catalog = MissionData_SO.Catalog;
            if (catalog == null) return null;

            MissionData_SO so;
            return catalog.TryGetValue(main, out so) ? so as MissionMainData_SO : null;
        }

        /// <summary>
        /// 按**任务类型名**（如「歼灭」/「收集任务」）反查主任务配置 —— 房间列表拿**图标 + 颜色**用。
        ///
        /// <para>▍为什么靠名字反查：局域网广播（<c>LanRoomInfo</c>）只有 9 个字段，任务类型只能由房主
        /// 把**名字**按 <c>#T=</c> 约定拼进房间名（<c>RoomMeta.TaskType</c>）⇒ 图标/颜色得自己找；
        /// 而"任务类型名"就是 <c>MissionMainData_SO.name</c>（= <c>TaskCfg.TaskType</c>），
        /// 两端任务资产一致 ⇒ **名字即键**（不必随广播下发 <c>MissionEnum</c> 下标）。</para>
        ///
        /// <para>▍取不到（旧版房主没拼后缀 / 传进来的其实是兜底的地图名 / 两端任务资产不一致）
        /// ⇒ 返回 null（**不抛异常**），调用方退回"地图图标 + 预制体本色"。</para>
        /// </summary>
        public MissionMainData_SO FindMainMission(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var catalog = MissionData_SO.Catalog;
            if (catalog == null) return null;

            // ⚠ **同名不唯一**（`Resources/GameData/Mission` 里「进攻任务」有 3 份、「歼灭任务」「渗透任务」各 2 份）
            //   ⇒ 这里取**确定性**的第一个（按 MissionEnum 值升序）：两端结果一致，
            //   不会因为 `Catalog` 的枚举顺序不同而给同一个房间换出不同的图标/颜色（同 OrderedMaps 的教训）。
            //   ⚠ 代价：同名任务若图标/颜色不同，展示层只会取到枚举值最小的那一份
            //   —— 要精确就得让广播带上 `MissionEnum`（现在只带得出名字，见 RoomMeta 的 TODO(库)）。
            MissionMainData_SO found = null;
            int best = int.MaxValue;
            foreach (var kv in catalog)
            {
                if (kv.Value is MissionMainData_SO main && main.name == typeName)
                {
                    int key = (int)kv.Key;
                    if (key < best)
                    {
                        best = key;
                        found = main;
                    }
                }
            }
            return found;
        }

        public void Init()
        {
            Awake();
            GlobalEventSub.OnGameStateChange += OnGameStateChange;//回大厅（Bridge）清空上一局任务（与 UnInit 的退订成对）
            Missions = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<MissionData_SO>("GameData/Mission"),item => item.type);
            MissionData_SO.Catalog = Missions;//数据自持：玩法层的 TaskCfg 从这里读（见 MissionData_SO.Catalog）
            Camps = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<CampData_SO>("GameData/Camp"),item => item.enemyVarietyType);
            MapData = Enumerable.ToDictionary(ResSvc.Instance.LoadObjects<MapData_SO>("GameData/Map"), item => item.name.Substring(3));
            TaskCfgs = new TaskCfg[AreaCount,TaskCount];

            // ⚠ 确定性排查用：把"任务表行下标 → 地图"的顺序打出来。
            //   两端这行顺序不同 ⇒ 同一时间窗口也会生成**完全不同的任务表**（原因见 OrderedMaps 注释）。
            Debug.Log($"[TaskManager] 任务表行下标→地图（按 mapId 排序）：" +
                      $"{string.Join(", ", OrderedMaps().Select(m => m.name.Substring(3)))}");

            ResetTask();
            _lastBucket = CurrentBucket();   // 先定桶，避免首帧 Update 因"桶变化"多刷一次
            CreatAllTask(_lastBucket);
            if (GameRoot.Instance.IsLocal)
            {
                SetTask("Millennium", 0, DifficultyEnum.Insane, new int[4], 2);

            }

        }

        public void UnInit()
        {
            GlobalEventSub.OnGameStateChange -= OnGameStateChange;//与 Init 的订阅成对
        }

        /// <summary>
        /// 窗口刷新（2026-10-06 改造）：**桶变化检测** + **只在舰桥真正重建任务表**。
        ///
        /// <para>▍为什么不用原来的 <c>Minute == 0 || Minute == 30</c>：那是"只在整点/半点的某一分钟内"才触发
        /// ⇒ 笔记本休眠（13:29 睡到 13:41）、长卡顿跨过整点，都会**永远不刷新**（一直停在旧桶）。</para>
        ///
        /// <para>▍为什么只在舰桥执行：战斗中重建 <c>TaskCfgs</c> 对本局**无害**（<c>nowTask.taskCfg</c> 是引用拷贝，
        /// 重建只替换数组元素、不改旧对象），但会让大厅任务列表在玩家眼皮底下跳变 ⇒ 挪到舰桥。</para>
        ///
        /// <para>▍为什么"跨桶就自愈"是联机成立的关键：30 分钟窗口下"启动时算出的桶"与"最近一次整点/半点
        /// 算出的桶"**必然相等** ⇒ 两端只要都在运行就会收敛到同一张任务表。</para>
        /// </summary>
        void Update()
        {
            long bucket = CurrentBucket();
            if (bucket != _lastBucket)
            {
                _lastBucket = bucket;
                _pendingRefresh = true;
            }

            if (!_pendingRefresh) return;
            // ★ 刷新门（2026-10-06 放宽）：**只要不在战斗中**就允许换表。
            //   · 为什么放宽到 Ready/Armament（原先只允许 Bridge）：跨桶后两端的表要尽快对齐，
            //     否则会出现"房主已选完任务、成员的表还停在旧桶"这种别扭状态；
            //   · **为什么"已确认的本局"不会因此改变**：`TaskCfg` 是 **struct**，`SetTask` 已把它
            //     **值拷贝**进 `nowTask`（连当时的 extra/nestCount 数组实例一起）⇒ 换表只换 `TaskCfgs` 这个数组；
            //     战斗与 UI 读的都是 `nowTask`，只有"选图界面"读 `TaskCfgs`（它本就该显示最新表）。
            if (GameRoot.GameState == GameStateEnum.Game) return;   // 战斗中不换表（回大厅/舰桥补刷）

            _pendingRefresh = false;
            CreatAllTask(bucket);
        }

        /// <summary>
        /// ��ʼ��������������
        /// </summary>
        private void CreatAllTask(long bucket)
        {
            //TaskRandom = new(now.Month*100+now.Day+now.Hour*100+(now.Minute/30*30));//半小时刷新一次
            // ⚠ 桶由调用方传入（不再自己取 DateTime.Now）：保证"刷新判定"与"生成内容"用的是同一个桶
            TaskRandom = new System.Random(unchecked((int)bucket));
            residualCodeA = new(codeA);
            residualCodeB = new(codeB);
            var values = OrderedMaps();   // ⚠ 必须排序：见 OrderedMaps 的注释（否则两端行下标对不上）
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


        /// <summary>
        /// 【确定性】按 <c>mapId</c> 排序后的地图列表 —— **`TaskCfgs` 的行下标就是它**。
        ///
        /// <para>▍⚠⚠ 为什么必须排序（2026-10-06 实测踩到）：`MapData` 来自
        /// <c>ResSvc.LoadObjects</c> = <c>Resources.LoadAll</c>（`ResSvc.cs:190`，**没有排序**），
        /// 其枚举顺序在 **编辑器 ↔ 打包版之间、以及不同构建/机器之间并不保证一致**。
        /// 而 `CreatAllTask` 里每个区域消费 `TaskRandom` 的次数取决于
        /// <c>Mathf.Min(TaskCount, mapItemInfos.Length)</c> ⇒ **顺序一变，整张表的随机序列分配全部错位**
        /// —— 即使在**同一个 30 分钟窗口**内，两端也会生成完全不同的任务表
        /// （表现为"同一个 `TaskIndex` 指向不同任务"，且静默）。
        /// 排序之后，`TaskCfgs` 只由「窗口 id + 地图集合 + Camp/Mission 资产」决定。</para>
        /// </summary>
        public List<MapData_SO> OrderedMaps()
        {
            if (MapData == null) return new List<MapData_SO>();
            return MapData.OrderBy(kv => kv.Key, System.StringComparer.Ordinal)
                          .Select(kv => kv.Value)
                          .ToList();
        }

        /// <summary>
        /// <c>TaskCfgs</c> 行下标 ↔ <c>mapId</c> 的**唯一权威换算**（与 <see cref="OrderedMaps"/> 同序）。
        /// <para>▍凡是"由 mapId 反查行下标"的地方都必须走它（`SetTask` / `TaskFingerprint`），
        /// 否则会出现"生成用一套顺序、查表用另一套"，正是上面那种静默错位。</para>
        /// </summary>
        public int MapIndex(string mapId)
        {
            if (MapData == null || string.IsNullOrEmpty(mapId)) return -1;
            var keys = MapData.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToList();
            return keys.IndexOf(mapId);
        }

        // ==================== 本局配置的"内容"上网（联机） ====================

        /// <summary>
        /// 【房主侧】把已确认的任务配置打成可上网的 DTO（随 <c>TaskConfirmNtf.Cfg</c> 下发）。
        /// <para>▍唯一来源 = `nowTask.taskCfg`（刚 SetTask 过的那一份），不重算、不查表。</para>
        /// </summary>
        public static TaskCfgDto ToDto(in TaskCfg c) => new TaskCfgDto
        {
            Main = (int)c.main,
            Extra = c.extra != null ? c.extra.Select(e => (int)e).ToArray() : new int[0],
            NestCount = c.nestCount != null ? (int[])c.nestCount.Clone() : new int[3],
            Seed = c.seed,
            Scale = c.scale,
            Terrain = (int)c.terrainType,
            EnemyVariety = (int)c.enemyVarietyType,
            Enable = c.enable,
            Name = c.name,
        };

        /// <summary>【成员侧】把房主下发的 DTO 还原成 <see cref="TaskCfg"/>
        /// （⚠ 仅当 <c>dto.Main &gt;= 0</c> 时调用，见 <see cref="SetTask"/>）。</summary>
        public static TaskCfg FromDto(TaskCfgDto dto) => new TaskCfg
        {
            main = (MissionEnum)dto.Main,
            extra = dto.Extra != null ? dto.Extra.Select(e => (MissionEnum)e).ToArray() : new MissionEnum[0],
            nestCount = dto.NestCount != null ? (int[])dto.NestCount.Clone() : new int[3],
            seed = dto.Seed,
            scale = dto.Scale,
            terrainType = (TerrainType)dto.Terrain,
            enemyVarietyType = (EnemyVarietyType)dto.EnemyVariety,
            enable = dto.Enable,
            name = dto.Name,
        };

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

        /// <summary>
        /// 计算"某个任务配置"的**指纹**（联机开局校验用：两端同一 (地图, 任务下标) 必须得到同一个指纹）。
        ///
        /// <para>▍为什么需要它：任务表 <c>TaskCfgs</c> 是**各端本地按时间窗口生成**的，
        /// 跨窗口时同一个 <c>TaskIndex</c> 可能指向完全不同的任务（任务类型 / 额外任务 / 巢穴数…），
        /// 而这是**静默**的 —— 指纹让不一致变成明确报错。</para>
        ///
        /// <para>▍⚠ 只用整数混合、**不掺任务名**：<c>string.GetHashCode()</c> 在 .NET Core / .NET 5+ 是
        /// 随机化的（跨进程不同），掺字符串会让指纹失去意义。</para>
        /// </summary>
        /// <returns>0 = 无法计算（地图/下标非法）；调用方应把 0 视为"不校验"</returns>
        public int TaskFingerprint(string mapId, int taskIndex)
        {
            if (MapData == null || string.IsNullOrEmpty(mapId) || !MapData.ContainsKey(mapId)) return 0;
            if (TaskCfgs == null || taskIndex < 0 || taskIndex >= TaskCount) return 0;

            int mapIndex = MapIndex(mapId);   // ⚠ 与 CreatAllTask / SetTask 同序
            if (mapIndex < 0 || mapIndex >= AreaCount) return 0;

            // ⚠ TaskCfg 是 struct ⇒ 不可能是 null，直接算
            return Fingerprint(TaskCfgs[mapIndex, taskIndex]);
        }

        private static int Fingerprint(TaskCfg c)
        {
            int h = 17;
            h = Mix(h, (int)c.main);
            h = Mix(h, c.enable ? 1 : 0);
            h = Mix(h, c.seed);
            h = Mix(h, (int)(c.scale * 1000f));
            if (c.extra != null) for (int i = 0; i < c.extra.Length; ++i) h = Mix(h, (int)c.extra[i]);
            if (c.nestCount != null) for (int i = 0; i < c.nestCount.Length; ++i) h = Mix(h, c.nestCount[i]);
            return h;
        }

        /// <summary>FNV-1a 风格的整数混合（确定性：与 .NET 版本、进程无关）。</summary>
        private static int Mix(int h, int v) => unchecked((h ^ v) * 16777619);

        /// <param name="seed">本局**权威随机种子**（房主决定；0 = 未指定 ⇒ 回落到该任务自己的 <c>taskCfg.seed</c>）。
        /// ⚠ 带默认值是为了不打乱既有调用点（含 <see cref="Init"/> 的单机自测路径）。</param>
        public void SetTask(string mapId,int taskIndex,DifficultyEnum difficulty,int[] extraDiff,int playMode,int seed = 0,
            TaskCfgDto remoteCfg = null)
        {
            if (MapData == null || string.IsNullOrEmpty(mapId) || !MapData.ContainsKey(mapId))
            {
                Debug.LogError($"[TaskManager] SetTask 失败：本地没有地图 {mapId}");
                return;
            }

            var mapData = MapData[mapId];
            var task = nowTask;

            // ★ 本局任务配置的来源（2026-10-06 联机）：
            //   · 房主**下发了内容** ⇒ 以内容为准、**不查本地表**。本局配置由此"冻结"：
            //     后进者 / 跨窗口的玩家即使本地表里已经没有这个任务，也照样复现（用户 2026-10-06 口径）。
            //   · 否则（单机 / 旧版房主）⇒ 按本地表下标取（老路径，行为不变）。
            if (remoteCfg != null && remoteCfg.Main >= 0)
            {
                task.taskCfg = FromDto(remoteCfg);
            }
            else
            {
                int mapIndex = MapIndex(mapId);   // ⚠ 必须与 CreatAllTask 同序（见 OrderedMaps 注释）
                if (mapIndex < 0 || mapIndex >= AreaCount || TaskCfgs == null
                    || taskIndex < 0 || taskIndex >= TaskCount)
                {
                    Debug.LogError($"[TaskManager] SetTask 失败：任务下标越界（地图 {mapId} 下标 {taskIndex}，" +
                                   $"mapIndex={mapIndex}）");
                    return;
                }
                task.taskCfg = TaskCfgs[mapIndex, taskIndex];
            }

            // 本局权威种子：优先用调用方给的（联机由房主随开局广播下发）；未指定则回落到该任务自己的种子。
            // ⚠ 归一到非 0：TaskCfg.seed 由 TaskRandom.Range(0,114514) 生成、理论可为 0，
            //   而 0 在本项目里是"未指定"的中性值（见 TaskState.Seed / TeamManager.SetSeed）。
            _matchSeed = seed != 0 ? seed : (task.taskCfg.seed != 0 ? task.taskCfg.seed : 1);
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


            task.collectProperty.Clear();
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

            task.RequiredAD = task.RequiredAD.Distinct().ToList();

            // ⚠ 角色默认战备（RoleData_SO.DefaultAirdropIDs）**不再**并进来：那是"每人一份"的数据，
            //   原先按本机存档角色追加 ⇒ 战备界面每一行、以及每个客户端显示的都是"本机角色的默认"，别人的行就错了
            //   （2026-10-07 两人实测）。现在 RequiredAD 只放**任务级**，谁用谁按角色补：界面按行取
            //   RequiredADOf(players[i].roleName)，战斗里取 RequiredADOf(自己角色)。

            task.difficulty = difficulty;
            System.Array.Copy(extraDiff, task.ExtraDifficulty, 4);


            task.PlayMode = playMode;
            task.SpecialtyPropertys = mapData.product;
            task.OtherPropertys = mapData.otherProduct;
            TaskState.Countdown = 16;//倒计时读数复位（运行时唯一存放处是 TaskState；原 task.Countdown 已不参与逻辑）
            task.activeTask = true;


            task.BattleData.Clear();
            for (int i =0;i<TeamManager.Instance.players.Count;++i)
            {
                task.BattleData.Add(new(DefaultBattleData));
            }
            Constants.TaskBorder = task.MapBorder;
            GameRoot.GameState = GameStateEnum.Ready;
            //倒计时读数走数据自持（2026-10-01 取代 nowTask.Countdown）
            WndManager.Instance.CreatCountDown(()=> TaskState.Countdown,CountDownTypeEnum.Blue,16);
            SyncTaskState();//发布给下层（数据自持）：难度 / 系数 / 敌人种类 / 收集表
        }

        /// <summary>
        /// 某个角色（玩家）的"任务所需战备" = 任务级 `RequiredAD` + 该角色的 `DefaultAirdropIDs`（去重）。
        ///
        /// <para>▍为什么必须按角色算：角色默认战备是每人一份的，不能塞进任务级的 RequiredAD（见 SetTask 末尾说明）。
        /// 战备界面每个玩家那一行、以及战斗里"本机玩家"的可用战备表，都该走这里。</para>
        /// </summary>
        public List<int> RequiredADOf(string roleId)
        {
            var list = new List<int>(nowTask.RequiredAD ?? new List<int>());
            if (string.IsNullOrEmpty(roleId)) return list;

            var roles = ResSvc.Instance != null ? ResSvc.Instance.LoadObjects<RoleData_SO>("GameData/Role") : null;
            var cfg = roles != null ? roles.Find(r => r.ID == roleId) : null;
            if (cfg == null || cfg.DefaultAirdropIDs == null) return list;

            for (int i = 0; i < cfg.DefaultAirdropIDs.Length; ++i)
            {
                if (!list.Contains(cfg.DefaultAirdropIDs[i])) list.Add(cfg.DefaultAirdropIDs[i]);
            }
            return list;
        }

        /// <summary>
        /// 场景模式：从 CampaignCfg 补全 nowTask 的地图级数据（RequiredAD、campData、BattleData），
        /// 置 <see cref="SelectTaskData.SceneMode"/> 并发布 <c>TaskState</c>（让本局被视为"有任务"）。
        /// </summary>
        public void EnsureSceneData(CampaignCfg cfg)
        {
            var task = Instance.nowTask;
            task.SceneMode = true;//场景模式标记：让 SyncTaskState 把本局视为"有任务"（TaskState.HasTask = true）
            task.RequiredAD = new List<int>(cfg.useAirdrops);
            task.campData = Instance.Camps[cfg.enemy];
            task.SceneSizeType = cfg.sizeType;
            if (task.BattleData == null || task.BattleData.Count == 0)
            {
                task.BattleData = new();
                for (int i = 0; i < TeamManager.Instance.players.Count; ++i)
                    task.BattleData.Add(new(DefaultBattleData));
            }
            SyncTaskState();//发布给下层：HasTask / 敌人种类 / 难度 / 收集表
        }

        // ⚠ `EnterTransition()` 已于 2026-10-01 删除（原实现 = `TaskState.Countdown = 16` + `GameRoot.GameState = Armament`）：
        //   · **切阶段** → 交给发布方 `MedivacController` 走事件 `GlobalEventSub.RequestGameState(GameStateEnum.Armament)`
        //     （⚠ 同日 `IFlowService` 与 `ServiceLocator.Flow` 已整体删除：`MissionEvacuate*` 各自的 1 处也改成同一事件）
        //   · **倒计时复位** → 归位到"开始新一局"的职责（本类 `SetTask()`），不再由"结束一局"兼任
        //   ⇒ 于是不需要专属事件；`OnGameStateChange` 广播直到 2026-10-02 才被本类订阅 ——
        //     只用于「回到大厅（Bridge）时清空上一局任务」（见 ResetTask()），与本段流程无关。
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
