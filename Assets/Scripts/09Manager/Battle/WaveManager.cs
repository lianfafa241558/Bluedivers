using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Attributes;
using FPSGame.GameContract;


using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using FPSGame.Utils;
using FPSGame.AI;
using Random = System.Random;
using UnitWeightCfg = FPSGame.Data.CampData_SO.UnitWeightCfg;

namespace FPSGame.Managers
{
    /// <summary>
    /// 按阵营配置抽取单位权重并创建波次，管理波次状态与冷却。
    /// </summary>
    [AddComponentMenu("管理/波次管理")]
    public class WaveManager : TickBehaviour
    {
        [SerializeField]
        string Suffix;
        int WaveCool;
        /// <summary>波次间隔倍率（定位混淆强化：加长波次间隔），1 表示无加成</summary>
        float WaveCoolMul = 1f;




        Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>>> TierItemWeight;
        [SerializeField]
        List<SKVP<int, UnitTier>> TierWeight;

        List<KVP<int, List<UnitTier>>> Patrol;
        //List<Wave> waveGroup;
        List<GameObject> WaveUseObject;

        float m_lastWaveTime = Mathf.NegativeInfinity;

        /// <summary>本局已开波数（波次的随机细分键，见 <see cref="WaveBase.PendingWaveIndex"/>）。</summary>
        int waveSeq;

        /// <summary>本局已建单位数：当 <see cref="Actor.NetId"/> 用（两端创建顺序一致 ⇒ 同一个 NetId 指同一个单位）。</summary>
        int netIdSeq;

        [SerializeField]
        int waveValue;

        public int WaveCount => ticks.Count - 1;

        Random random;
        BattleManager manager;
        [DisplayField]
        EnemyVarietyType enemyVarietyType;

        private void Awake()
        {
            manager = BattleManager.Instance;
            random = manager.BattleRandom;
            var task = TaskManager.Instance.nowTask;
            var cfg = task.campData;
            Debug.Log(cfg.ShowName + " " + cfg.Suffix + task.campData.ShowName, task.campData);
            Suffix = cfg.Suffix;
            WaveCool = cfg.WaveCool;
            WaveUseObject = cfg.WaveUseObject;
            enemyVarietyType = cfg.enemyVarietyType;
            // ⚠ 必须走带 random 参数的重载：无参重载用的是全局静态流 RandomUtils，
            //   而那条流会被音效/弹孔/武器散布/谜题推进游标 ⇒ 两端"整场敌人构成"会不同。
            var tmp = cfg.templates.RandomTake(random);


            TierWeight = tmp.template.Select(item => new SKVP<int, UnitTier>(item.weight, item.tier)).Where(item => item.Key > 0).ToList();

            TierItemWeight = new Dictionary<UnitTier, List<KVP<int, UnitWeightCfg>>>();

            foreach (var kvp in tmp.template)
            {
                if (kvp.weight == 0) continue;
                var list = kvp.unitWeights
                    .Select(cfg => new KVP<int, UnitWeightCfg>(kvp.weight, cfg))
                    .ToList();

                //Debug.LogError(string.Join(",",list.Select(item=>item.Value.unit.name).ToList()));

                if (TierItemWeight.ContainsKey(kvp.tier))
                    TierItemWeight[kvp.tier] = list;
                else
                    TierItemWeight.Add(kvp.tier, list);
            }

            //Debug.LogError(cfg.ShowName + "选择" + tmp.name + "模板");

            Patrol = tmp.patrolTemplate
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => {
                    var patrolCfg = cfg.patrolCfgs.FirstOrDefault(p => p.name == kvp.Key);
                    if (patrolCfg == null) return null;
                    var units = patrolCfg.units
                        .SelectMany(item => Enumerable.Repeat(0, item.Value)
                            .Select(_ => (UnitTier)item.Key)
                        ).ToList();
                    return new KVP<int, List<UnitTier>>(kvp.Value, units);
                })
                .Where(kvp => kvp != null)
                .ToList();

            waveValue = (int)((1 + 0.15f * task.ExtraDifficulty[2]) * 60
                * task.difficulty switch {
                    DifficultyEnum.Normal => 0.6f,
                    DifficultyEnum.Hard => 0.75f,
                    DifficultyEnum.VeryHard => 0.9f,
                    DifficultyEnum.HardCode => 1.0f,
                    DifficultyEnum.Extreme => 1.1f,
                    DifficultyEnum.Insane => 1.2f,
                    DifficultyEnum.Torment => 1.3f,
                    DifficultyEnum.Lunatic => 1.5f,
                    _ => 1f,
                });
            //理论上的极限是100*1.5*1.45=217，之前的极限是35*7*2=490
            if (manager.HaveBooster(BoosterType.PositionConfusion)) WaveCoolMul = 1.4f;

            // 联机：成员按房主的开波广播复刻同一波
            FPSGame.Net.NetRoomFlow.OnWaveStart += ApplyRemoteWave;

            // 联机战斗同步：成员端"移动由房主决定"（本端 AI 不再自己定目标点），并挂上同步桥
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            EnemyController.RemoteDrivenMovement = flow != null && flow.SelfSid != 0u;
            FPSGame.AI.EnemyRandom.Clear();   // NetId 是本局重新分配的 ⇒ 上一局的随机流要丢
            EnemyNetBridge.Install();
        }

        private void OnDestroy()
        {
            FPSGame.Net.NetRoomFlow.OnWaveStart -= ApplyRemoteWave;
            EnemyNetBridge.Uninstall();
        }



        public bool CreatWave(WaveCreateParams param)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            bool online = flow != null && (flow.IsHost || flow.SelfSid != 0u);

            // ★ 联机时开波**时机**归房主：成员不在本地开波，等 WaveStartSync 过来再复刻同一波。
            //   否则两端各按本地进度开波（本地清场 → 下一波）⇒ 从第一波之后相位彻底错开（2026-10-07 实测）。
            if (!applyingRemoteWave && online && !flow.IsHost) return false;

            //时间没到或者不是强制刷新
            if (!applyingRemoteWave && !param.extraWave && Time.time < m_lastWaveTime + WaveCool * WaveCoolMul) return false;
            m_lastWaveTime = Time.time;
            if (!applyingRemoteWave)
            {
                // ★ 波次确定性：把"这是第几波"交给 WaveBase 当随机细分键（同一波 ⇒ 同一构成/同一落点，两端一致）
                WaveBase.PendingWaveIndex = ++waveSeq;
                if (online) PublishWaveStart(param, waveSeq);   // ★ 房主：广播（成员按同一 waveSeq 复刻）
            }
            else WaveBase.PendingWaveIndex = waveSeq;           // 复刻：waveSeq 已在 ApplyRemoteWave 里设成房主的波序

            switch (enemyVarietyType)
            {
                case EnemyVarietyType.KaiserBase:
                    ticks.Add(new RobotWave(param, InitWaveUnits(param.scale), WaveUseObject));
                    break;
                case EnemyVarietyType.KaiserPMC:
                    ticks.Add(new RobotWave(param, InitWaveUnits(param.scale), WaveUseObject));
                    break;
                case EnemyVarietyType.KaiserMengsk:
                    ticks.Add(new RobotWave(param, InitWaveUnits(param.scale), WaveUseObject));
                    break;
                case EnemyVarietyType.BlackMarket:
                    ticks.Add(new RobotWave(param, InitWaveUnits(param.scale), WaveUseObject));
                    break;
                case EnemyVarietyType.Decagrammaton:
                    ticks.Add(new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject, 10,"侦测到折跃信号"));
                    break;
                case EnemyVarietyType.UnNamedGuardian:
                    ticks.Add(new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject, 13, "侦测到坑道虫入侵"));
                    break;
                case EnemyVarietyType.Colour:
                    ticks.Add(new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject, 10, "侦测到空间裂隙"));
                    break;
                case EnemyVarietyType.Beatrice:
                    ticks.Add(new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject, 10, "侦测到空间裂隙"));
                    break;
                default:
                    ticks.Add(new ZergWave(param, InitWaveUnits(param.scale), WaveUseObject, 10, "侦测到折跃信号"));
                    break;
            }

            return true;
        }

        /// <summary>正在"复刻房主开的波"（见 <see cref="ApplyRemoteWave"/>）：跳过本端的房主闸门与冷却节流。</summary>
        bool applyingRemoteWave;

        /// <summary>【房主】把这次开波广播出去。⚠ 不做本地自派发：房主自己那份上面已经建过了。</summary>
        void PublishWaveStart(WaveCreateParams param, int waveIndex)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (flow == null) return;

            flow.SendWaveStart(new FPSGame.Net.WaveStartMsg
            {
                MatchId = FPSGame.Net.NetRoomFlow.CurrentMatchId,
                WaveIndex = waveIndex,
                ExtraWave = param.extraWave,
                Tip = param.tip,
                Scale = param.scale,
                Range = param.range,
                CenterX = param.center.x,
                CenterY = param.center.y,
                CenterZ = param.center.z,
                Points = PackPoints(param.points),
                ChaseCenter = param.centerGetter != null,   // 追击中心：成员端复现为"离中心最近的玩家"
            });
        }

        /// <summary>【成员】房主开波 ⇒ 在本端复刻同一波（同波序 ⇒ 同随机流 ⇒ 同构成/同落点）。</summary>
        void ApplyRemoteWave(FPSGame.Net.WaveStartMsg msg)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (msg == null || flow == null || flow.IsHost) return;   // 房主自己那份已经建过

            waveSeq = msg.WaveIndex;             // ★ 用房主的波序：两边算随机流的键必须相同
            var param = new WaveCreateParams
            {
                extraWave = msg.ExtraWave,
                tip = msg.Tip,
                scale = msg.Scale,
                range = msg.Range,
                center = new Vector3(msg.CenterX, msg.CenterY, msg.CenterZ),
                points = UnpackPoints(msg.Points),
                // ⚠ 不带 onEnd：续航继续由房主驱动（成员这波是复刻，自刷会变成"两端各刷一份"）
            };
            if (msg.ChaseCenter)
            {
                Vector3 c = param.center;
                param.centerGetter = () => c = ActorsManager.NearestPlayerPos(c);
            }

            applyingRemoteWave = true;
            try { CreatWave(param); }
            finally { applyingRemoteWave = false; }
        }

        static float[] PackPoints(Vector3[] pts)
        {
            if (pts == null || pts.Length == 0) return null;
            var re = new float[pts.Length * 3];
            for (int i = 0; i < pts.Length; ++i)
            {
                re[i * 3] = pts[i].x;
                re[i * 3 + 1] = pts[i].y;
                re[i * 3 + 2] = pts[i].z;
            }
            return re;
        }

        static Vector3[] UnpackPoints(float[] flat)
        {
            if (flat == null || flat.Length < 3) return null;
            var re = new Vector3[flat.Length / 3];
            for (int i = 0; i < re.Length; ++i) re[i] = new Vector3(flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
            return re;
        }

#if UNITY_EDITOR

        protected override void Update()
        {
            base.Update();
            if (Input.GetKeyUp(KeyCode.K))
            {
                CreatWave(new() {
                    center = ActorsManager.Player.Pos,
                    extraWave = false,
                    range = 30,
                    scale = 1,
                    tip = true
                });
            }
        }
#endif

        public override bool Tick()
        {
            return true;
        }

        Stack<GameObject> InitWaveUnits(float scale)
        {
            // ⚠ 必须用**本波派生的流**，不能用 `manager.BattleRandom`：那是全局战斗流，两端被各自系统推进的程度
            //   不同（音效/掉落/散布…）⇒ 同一波抽到的"单位构成"会不一样（2026-10-07 波次不同步的元凶之一）。
            //   无权威种子（单机）时保持原样。
            int seed = FPSGame.Data.TaskState.Seed;
            Random waveRandom = seed != 0
                ? new Random(SeedUtil.Derive(seed, (int)SeedStream.WaveUnits * 1000 + waveSeq))
                : random;

            Stack<GameObject> re = new();
            var remain = waveValue * scale;
            bool hasBoss = false;
            int skipCount = 0;

            while (remain > 0)
            {
                UnitTier tier = TierWeight.WeightTake(100, waveRandom);
                var item = TierItemWeight[tier].WeightTake(100, waveRandom);

                // 首领只能出现一个：已出现首领后，再随机到首领单位则跳过本次，接着重新随机
                if (IsBossUnit(item.unit))
                {
                    if (hasBoss)
                    {
                        // 防死循环：连续多次都是首领（如某 tier 只有首领单位）时，强制接受并告警
                        if (++skipCount >= 20)
                        {
                            Debug.LogWarning($"InitWaveUnits: 连续 {skipCount} 次随机到首领，可能某 tier 只含首领单位，强制接受该单位");
                            hasBoss = true;
                            remain -= item.size;
                            re.Push(item.unit);
                            continue;
                        }
                        continue;
                    }
                    hasBoss = true;
                }

                remain -= item.size;
                re.Push(item.unit);
            }
            return re;
        }

        /// <summary>判断单位预制体是否为首领（拥有 ActorFlag.Boss 标签）</summary>
        static bool IsBossUnit(GameObject unit)
        {
            return unit && unit.GetComponent<Actor>() is { } actor && actor.HasFlag(ActorFlag.Boss);
        }

        public GameObject CreatUnit(UnitTier tier, Vector3 pos, float range, bool IsFixed = true)
        {
            //var random = BattleManager.Instance.BattleRandom;
            if (!TierItemWeight.TryGetValue(tier, out var weightList) || weightList.Count == 0)
            {
                // 降级：如果指定 tier 不存在，尝试使用字典中任意可用的 tier
                Debug.LogWarning($"CreatUnit: tier '{tier}' 在当前模板配置中不存在，降级到默认 tier");
                var firstKvp = TierItemWeight.FirstOrDefault();
                if (firstKvp.Value == null || firstKvp.Value.Count == 0)
                {
                    Debug.LogError("CreatUnit: 模板配置中没有可用的 tier，无法创建单位");
                    return null;
                }
                weightList = firstKvp.Value;
            }
            // ⚠ 落点抖动/朝向也不能用 `random`（= 全局战斗流，两端游标不同 ⇒ 同一只怪会落在不同地方）。
            //   按"本波波序"派生一条独立流；无权威种子（单机）时退回原行为。
            int seed = FPSGame.Data.TaskState.Seed;
            Random spawnRandom = seed != 0
                ? new Random(SeedUtil.Derive(seed, (int)SeedStream.UnitPlacement * 1000 + waveSeq))
                : random;

            var item = weightList.WeightTake(100, spawnRandom);
            //先取到地点
            if (NavMesh.SamplePosition(pos, out var hit, 50, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            //再随机偏移
            if (NavMesh.SamplePosition(pos + spawnRandom.RandomVector2().ToVector3() * range, out hit, 10, UnityEngine.AI.NavMesh.AllAreas))
            {
                pos = hit.position;
            }
            else
            {
                // ⚠ 这里**不是错误**：偏移点落网格外就退回上面那个已吸附的原点（`pos` 没被覆盖），单位照样生成。
                //   原来打 LogError 会让人以为刷怪失败（2026-10-07 用户报告"进入游戏后错误…不存在"）。
                Debug.LogWarning($"CreatUnit: 随机偏移点不在导航网格上，已回退到吸附点 {pos}");
            }

            var go = Object.Instantiate(item.unit, pos, Quaternion.Euler(spawnRandom.RandomVector2().ToVector3()), manager.ACCont.transform);
            // ★ 跨端稳定的单位标识（波次/巡逻队都走这里，且两端生成顺序一致）
            var netActor = go.GetComponent<Actor>();
            if (netActor != null) netActor.NetId = ++netIdSeq;
            if (IsFixed)
            {
                go.GetComponent<I_AIController>().BirthDuration = 0;
                go.GetComponent<IActor>().IsFixed = true;
            }
            //Debug.LogWarning("创建单位   " + tier+"  " +go);
            return go;
        }

        public List<GameObject> CreatPatrol(Vector3 pos)
        {
            // ⚠ 巡逻队构成也不能用全局战斗流（`random`）：按**落点位置**派生一条流 ——
            //   位置本身由种子生成（任务点/兴趣点），两端一致 ⇒ 抽到的巡逻队也一致（2026-10-07）。
            int seed = FPSGame.Data.TaskState.Seed;
            Random patrolRandom = seed != 0
                ? new Random(SeedUtil.Derive(
                    SeedUtil.Derive(seed, (int)SeedStream.UnitPlacement * 1000 + 1),
                    Mathf.RoundToInt(pos.x * 10f) * 31 + Mathf.RoundToInt(pos.z * 10f)))
                : random;

            var re = new List<GameObject>();
            var temp = Patrol.WeightTake(100, patrolRandom);
            temp.ForEach(item => re.Add(CreatUnit(item, pos, 5, false)));
            return re;
        }


    }

    public enum WaveState
    {
        Start,//开始
        Ongoing,//进行中
        NearEnd,//即将结束
        End//结束
    }
}
