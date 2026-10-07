using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Data;
using FPSGame.Game;
using FPSGame.GameData;

namespace FPSGame.Gameplay
{

// ============================================================================
// 以下三个 DTO 原先是 `TaskManager` 的**嵌套类型**（写在 01Manager/Global/TaskManager.cs 里），
// 2026-09-30 抽出到玩法层并**解嵌套**（文件作用域）：
//   ① 玩法核心类 MissionBase/MissionItem 的公开 API 用了 `TaskManager.TaskItem`/`SelectTaskData`
//      ⇒ 不搬出来，它们就进不了 06_Gameplay；
//   ② 它们只依赖 SO/枚举/基础类型（`MissionData_SO` 也已在玩法层）⇒ 可安全落这一层。
// ⚠ `TaskCfg` 原先读 `TaskManager.Instance.Missions`（向上依赖）⇒ 改成读 `MissionData_SO.Catalog`
//    （**数据自持**：由 TaskManager 在加载任务配置时写入）。
// ============================================================================

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

        public int PlayMode { get; set; }
        /// <summary>⚠ 2026-10-01 起**不再参与运行时逻辑**：撤离倒计时的唯一存放处改为
        /// <c>FPSGame.Data.TaskState.Countdown</c>（写和读都走它）。本字段保留仅为兼容既有数据。</summary>
        public int Countdown { get; set; } = 16;
        public int[] ExtraDifficulty { get; set; } = new int[] { 0, 0, 0, 0 };
        public OOPartEnum[] SpecialtyPropertys { get; set; }
        public OOPartEnum[] OtherPropertys { get; set; }
        public MissionMainData_SO MainCfg => main?.cfg as MissionMainData_SO;

        /// <summary>场景模式标记：本局由场景里的 <c>CampaignCfg</c> 驱动（见 <c>TaskManager.EnsureSceneData</c>），
        /// 此时没有任务配置（<c>TaskCfg</c>/<c>main</c>）⇒ <c>TaskState.HasTask</c> 依据本标记为真。</summary>
        public bool SceneMode { get; set; }

        /// <summary>本局敌人种类：任务模式取任务配置，场景模式取 <c>CampaignCfg.enemy</c>（经 <c>campData</c>）。</summary>
        public EnemyVarietyType EnemyVarietyType => SceneMode
            ? (campData != null ? campData.enemyVarietyType : default)
            : taskCfg.enemyVarietyType;

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
        public int ExtraReward =>extra.Select(item=> MissionData_SO.Catalog[item].reward.y).Sum();

        private MissionData_SO Main => MissionData_SO.Catalog[main];
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
