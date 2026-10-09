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
            // 联机：追击波次的中心由房主下发（成员不再自己算"最近玩家"，见 ApplyRemoteWaveCenter）
            FPSGame.Net.NetRoomFlow.OnWaveCenter += ApplyRemoteWaveCenter;

            // 联机战斗同步：成员端"移动由房主决定"（本端 AI 不再自己定目标点），并挂上同步桥
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            EnemyController.RemoteDrivenMovement = flow != null && flow.SelfSid != 0u;
            FPSGame.AI.EnemyRandom.Clear();   // NetId 是本局重新分配的 ⇒ 上一局的随机流要丢
            EnemyNetBridge.Install();
            NetGameFlowBridge.Install();      // 局内世界状态（本局结束 / 撤离）
            NetMissionBridge.Install();       // 任务状态 / 进度（房主权威）
            NetFurnitureBridge.Install();     // 家具交互（共享世界物件，一处收口）
            NetActionBridge.Install();        // 标记点位 / 呼叫凯伊
        }

        private void OnDestroy()
        {
            FPSGame.Net.NetRoomFlow.OnWaveStart -= ApplyRemoteWave;
            FPSGame.Net.NetRoomFlow.OnWaveCenter -= ApplyRemoteWaveCenter;
            EnemyNetBridge.Uninstall();
            NetGameFlowBridge.Uninstall();
            NetMissionBridge.Uninstall();
            NetFurnitureBridge.Uninstall();
            NetActionBridge.Uninstall();
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

                // ★ 追击中心：房主是**唯一**判定方 —— "离中心最近的玩家"会因位姿延迟翻边，
                //   各端自己算必然算出不同答案（2026-10-08 定案）。在房主的 centerGetter 外面包一层，
                //   按 ~2Hz 把解析结果下发；成员只跟随（见 SmoothRemoteCenter）。
                if (online && param.centerGetter != null)
                {
                    var inner = param.centerGetter;
                    int wave = waveSeq;
                    param.centerGetter = () =>
                    {
                        Vector3 c = inner();
                        PublishWaveCenter(wave, c);
                        return c;
                    };
                }
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
                // ★ 追击中心：**不再自己算"最近玩家"**（位姿延迟会翻转答案 ⇒ 中心跳变）。
                //   只跟随房主 ~2Hz 下发的中心，并在两包之间做插值平滑（见 ApplyRemoteWaveCenter / Tick）。
                m_remoteCenterTarget = param.center;
                m_remoteCenter = param.center;
                param.centerGetter = SmoothRemoteCenter;
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

        /// <summary>房主下发中心的最小间隔（秒）：~2Hz 足够（成员做插值），不必跟 Tick 同频。</summary>
        const float CenterSendInterval = 0.5f;

        /// <summary>成员端"跟随房主中心"的最大速度（米/秒）：要把 2Hz 的台阶平滑掉，但不该瞬间贴过去。</summary>
        const float RemoteCenterFollowSpeed = 24f;

        float m_lastCenterSend = Mathf.NegativeInfinity;

        /// <summary>房主下发的追击中心（"目标值"，由 <see cref="ApplyRemoteWaveCenter"/> 写入）。</summary>
        Vector3 m_remoteCenterTarget;
        /// <summary>成员端实际交出去的追击中心（向 <see cref="m_remoteCenterTarget"/> 平滑逼近）。</summary>
        Vector3 m_remoteCenter;
        /// <summary>是否收到过房主的中心（没收到前用开波包里的 center 兜底）。</summary>
        bool m_hasRemoteCenter;

        /// <summary>【房主】下发追击中心（~2Hz 节流）。由包在 <c>centerGetter</c> 外层的那个委托调用。</summary>
        void PublishWaveCenter(int waveIndex, Vector3 center)
        {
            if (Time.time < m_lastCenterSend + CenterSendInterval) return;
            m_lastCenterSend = Time.time;
            FPSGame.Net.NetRoomFlow.Instance?.SendWaveCenter(waveIndex, center);
        }

        /// <summary>【成员】收到房主的追击中心。
        /// <para>⚠ 按 <c>WaveCenterMsg.WaveIndex</c> 丢过期包：迟到的上一波中心会把这一波拽回去。</para></summary>
        void ApplyRemoteWaveCenter(FPSGame.Net.WaveCenterMsg msg)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (msg == null || flow == null || flow.IsHost) return;
            if (msg.WaveIndex != waveSeq) return;                  // 过期 / 未来包

            m_remoteCenterTarget = new Vector3(msg.X, msg.Y, msg.Z);
            if (!m_hasRemoteCenter)
            {
                m_hasRemoteCenter = true;
                m_remoteCenter = m_remoteCenterTarget;             // 第一包直接贴上，不要从旧的猜值慢慢挪
            }
        }

        /// <summary>【成员】把中心平滑逼近房主给的目标值。包在 <c>centerGetter</c> 里，被波次每 Tick 调用一次。</summary>
        Vector3 SmoothRemoteCenter()
        {
            if (m_hasRemoteCenter)
                m_remoteCenter = Vector3.MoveTowards(m_remoteCenter, m_remoteCenterTarget, RemoteCenterFollowSpeed * Time.deltaTime);
            return m_remoteCenter;
        }

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
            // ★ 跨端稳定的单位标识由 `Actor.Awake` 统一分配（见 `Actor.NetId` 的注释）——
            //   这里不能自己分：波次单位根本不走本方法（ZergWave/RobotWave 都是裸 Instantiate）。
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
