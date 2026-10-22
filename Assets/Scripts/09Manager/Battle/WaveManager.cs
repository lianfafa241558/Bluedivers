using System.Collections.Generic;
using System.Linq;
using FPSGame.Core;
using FPSGame.Core.Interface;
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
            // ★ tick 伤害（破甲流血 / 异常状态跳伤）**只由房主（或单机）本地结算并广播**：
            //   两端各算各的、各报各的 ⇒ 同一段 tick 会被叠加 N 份（见 BattleState.IsTickDamageAuthority）。
            FPSGame.Data.BattleState.IsTickDamageAuthority = flow == null || flow.IsHost || flow.SelfSid == 0u;
            // ★ 载具（机甲/可驾驶炮台）的伤害**同样收敛到房主**：两端各自模拟子弹 ⇒ 各扣一次必分叉。
            //   口径见 BattleState.IsVehicleDamageAuthority（AI 来源只有房主落地；玩家侧来源落地 + 上报）。
            FPSGame.Data.BattleState.IsVehicleDamageAuthority = flow == null || flow.IsHost || flow.SelfSid == 0u;
            FPSGame.AI.EnemyRandom.Clear();   // NetId 是本局重新分配的 ⇒ 上一局的随机流要丢
            EnemyNetBridge.Install();
            NetDestructibleBridge.Install();  // 场景可破坏物（油桶这类）被打掉 ⇒ 远端也炸（2026-10-10）
            NetGameFlowBridge.Install();      // 局内世界状态（本局结束 / 撤离）
            NetMissionBridge.Install();       // 任务状态 / 进度（房主权威）
            // 家具交互桥（4036）已改成**常驻**（NetFurnitureBridge.Bootstrap）：舰桥里也有 NPC 对话/门，
            // 原来只在这里装 ⇒ 舰桥阶段没有任何家具同步通道（2026-10-22 用户报"和 NPC 对话不同步"）。
            NetActionBridge.Install();        // 标记点位 / 呼叫凯伊
            NetKeyScreenBridge.Install();     // 按键终端阶段推进（驱动端上报 / 其余端只应用）
            NetHostSpawnBridge.Install();     // 房主生成族（巡逻队 + 巢穴/召唤师/刷怪器/环绕装甲）：房主唯一生成 + 4052 复刻
            NetVehicleDamageBridge.Install(); // 载具伤害（上行 + 权威门；下行复用 EnemyNetBridge）（2026-10-14）
        }

        private void OnDestroy()
        {
            FPSGame.Net.NetRoomFlow.OnWaveStart -= ApplyRemoteWave;
            FPSGame.Net.NetRoomFlow.OnWaveCenter -= ApplyRemoteWaveCenter;
            EnemyNetBridge.Uninstall();
            NetDestructibleBridge.Uninstall();
            NetGameFlowBridge.Uninstall();
            NetMissionBridge.Uninstall();
            // ⚠ 别在这里 Uninstall 家具桥：它是常驻的（见 NetFurnitureBridge.Bootstrap），
            //   在这里摘掉会让"打完一局回舰桥"之后 NPC 对话/门又不同步了。
            NetActionBridge.Uninstall();
            NetKeyScreenBridge.Uninstall();
            NetHostSpawnBridge.Uninstall();
            NetVehicleDamageBridge.Uninstall();
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

        /// <summary>按 tier 取一把 prefab，并把落点吸附 / 抖动、朝向一并算好（上面那条 tier 重载与刷怪器那条共用）。
        /// <para>⚠ 取值顺序 = 权重 → 落点抖动 → 朝向：改顺序等于改随机序列（两端会错位）。</para></summary>
        GameObject TakeTierUnit(UnitTier tier, ref Vector3 pos, float range, Random rand, out Quaternion rot)
        {
            rot = default;
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

            var item = weightList.WeightTake(100, rand);

            //先取到地点
            if (NavMesh.SamplePosition(pos, out var hit, 50, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            //再随机偏移
            if (NavMesh.SamplePosition(pos + rand.RandomVector2().ToVector3() * range, out hit, 10, UnityEngine.AI.NavMesh.AllAreas))
            {
                pos = hit.position;
            }
            else
            {
                // ⚠ 这里**不是错误**：偏移点落网格外就退回上面那个已吸附的原点（`pos` 没被覆盖），单位照样生成。
                //   原来打 LogError 会让人以为刷怪失败（2026-10-07 用户报告"进入游戏后错误…不存在"）。
                Debug.LogWarning($"CreatUnit: 随机偏移点不在导航网格上，已回退到吸附点 {pos}");
            }

            rot = Quaternion.Euler(rand.RandomVector2().ToVector3());
            return item.unit;
        }

        /// <summary>落点 / 取样用的随机源：有权威种子走**派生流**，单机退回全局战斗流。
        /// ⚠ 绝不能用全局战斗流做"两端必须一致"的抽取（它会被音效 / 弹孔 / 武器散布推进游标）。</summary>
        Random NewSpawnRandom()
        {
            int seed = FPSGame.Data.TaskState.Seed;
            return seed != 0
                ? new Random(SeedUtil.Derive(seed, (int)SeedStream.UnitPlacement * 1000 + waveSeq))
                : random;
        }

        public GameObject CreatUnit(UnitTier tier, Vector3 pos, float range, bool IsFixed = true)
        {
            var prefab = TakeTierUnit(tier, ref pos, range, NewSpawnRandom(), out var rot);
            if (prefab == null) return null;

            // ★ 跨端稳定的单位标识由 `Actor.Awake` 统一分配（见 `Actor.NetId` 的注释）——
            //   这里不能自己分：波次单位根本不走本方法（ZergWave/RobotWave 都是裸 Instantiate）。
            var go = Object.Instantiate(prefab, pos, rot, manager.ACCont.transform);
            if (IsFixed)
            {
                go.GetComponent<I_AIController>().BirthDuration = 0;
                go.GetComponent<IActor>().IsFixed = true;
            }
            //Debug.LogWarning("创建单位   " + tier+"  " +go);
            return go;
        }

        /// <summary>【联机·刷怪器】房主侧：按 tier 抽 prefab + 落点/朝向，再走统一入口（自动上报 4052）。
        /// <para>▍为什么不复用上面那条 tier 重载：那条两端各自生成、靠"同落点派生构成"对齐、**不发消息**；
        /// 刷怪器的生成时机由场景摆放 + 概率驱动、两端必然不同 ⇒ 必须房主唯一生成。槽位 = <c>(int)tier</c>，
        /// 成员按 <c>PrefabId</c> 回同一张 tier 表取回同一把 prefab（见 <see cref="FindTierPrefab"/>）。</para></summary>
        public GameObject CreatSpawnerUnit(UnitTier tier, Vector3 pos, float range, INetSpawner source)
        {
            var prefab = TakeTierUnit(tier, ref pos, range, NewSpawnRandom(), out var rot);
            if (prefab == null) return null;

            var go = CreatUnit(prefab, pos, rot, source, (int)tier, default);
            if (go != null)
            {
                // 与上面那条的默认 IsFixed=true 对齐（成员复刻时也照抄这两行）
                go.GetComponent<I_AIController>().BirthDuration = 0;
                go.GetComponent<IActor>().IsFixed = true;
            }
            return go;
        }

        /// <summary>【成员】按 (tier, prefabId) 取回房主抽中的那把 prefab。
        /// <para>① 正常路径：tier 在表里 ⇒ 按 prefabId 找，找不到退回该 tier 第一把。</para>
        /// <para>② **tier 不在表里 ⇒ 按 prefabId 全表找**（见下）。</para>
        /// </summary>
        public GameObject FindTierPrefab(UnitTier tier, string prefabId)
        {
            if (TierItemWeight.TryGetValue(tier, out var list) && list.Count > 0)
            {
                if (!string.IsNullOrEmpty(prefabId))
                {
                    for (int i = 0; i < list.Count; ++i)
                    {
                        var unit = list[i].Value.unit;
                        if (unit == null) continue;
                        var entity = unit.GetComponent<IEntity>();
                        if (entity != null && entity.Id == prefabId) return unit;
                    }
                }
                return list[0].Value.unit;
            }

            // ② 本端这张表里没有该 tier ⇒ 只能靠 4052 里的 **prefabId** 还原。
            //    ▍为什么会有这种消息：房主侧 `TakeTierUnit` 在"tier 不在模板里"时会**降级**抽别的 tier 的单位
            //      （只打一条 `CreatUnit: tier 'xxx' 不存在，降级到默认 tier`），但 4052 里的 `slot` 仍是 `(int)tier`
            //      ⇒ 不按 prefabId 兜底就必然复刻失败：成员端少一只怪，之后它的移动/伤害/死亡消息全部"找不到副本"
            //      （2026-10-21 实测：`tier 'Small'` 缺失 ⇒ `复刻失败（生成器=CreatSmall）` + 一串 `远端死亡落地 找不到 netId`）。
            if (!string.IsNullOrEmpty(prefabId))
            {
                foreach (var kv in TierItemWeight)
                {
                    var l = kv.Value;
                    if (l == null) continue;
                    for (int i = 0; i < l.Count; ++i)
                    {
                        var unit = l[i].Value.unit;
                        if (unit == null) continue;
                        var entity = unit.GetComponent<IEntity>();
                        if (entity != null && entity.Id == prefabId) return unit;
                    }
                }
            }

            Debug.LogWarning($"[WaveManager] 复刻取不到 prefab：tier={tier}（slot={(int)tier}）prefabId={prefabId}" +
                             " ⇒ 本端这张表里既没有该 tier，也没有同 Id 的 prefab");
            return null;
        }

        /// <summary>【联机】所有"运行时生成"的**唯一入口**（巢穴 / 召唤师 / 刷怪器）：房主（或单机）创建，成员直接返回 null
        /// （不生成、等 4052 复刻）—— 否则两端各生一份，<c>Actor.Awake</c> 的号段立刻错位。
        ///
        /// <para>▍取号：房主侧走 <c>ActorsManager.HostSpawnNetIdBase</c> 段（与敌人自增段解耦：这类生成的时机由各自 AI / 计时
        /// 驱动，两端必然交错）；成员复刻回来的号由 <c>INetSpawner.ReplaySpawn</c> 覆盖。</para>
        /// <para>⚠ 波次（ZergWave/RobotWave，走 4023 整批复刻、两端同序）与场景 / 任务实体（派生号）**不要**走这条。</para>
        /// </summary>
        /// <param name="slot">生成者自己表里的下标（由生成器决定口径，成员复刻时按同一口径取回同一把 prefab）。</param>
        /// <param name="target">首帧目标点（全 0 = 无）。</param>
        public GameObject CreatUnit(GameObject prefab, Vector3 pos, Quaternion rot, INetSpawner source, int slot, Vector3 target = default)
        {
            if (prefab == null || source == null) return null;

            // ★ 成员端不生成：本端生成的单位会领号 ⇒ 与房主下发的号撞车（其后整条号段全错位）
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (flow != null && !flow.IsHost && flow.SelfSid != 0u) return null;

            GameObject go;
            ActorsManager.ForceHostSpawnNetId = true;
            try
            {
                // ⚠ 挂到 ActorsManager 下（不是生成者下）：生成者（巢穴）死了不能把它生出来的单位一起带走
                var parent = ActorsManager.Instance != null ? ActorsManager.Instance.transform : null;
                go = Object.Instantiate(prefab, pos, rot, parent);
            }
            finally { ActorsManager.ForceHostSpawnNetId = false; }

            // 房主侧自己先设目标（成员那份由图中的消息带来，顺序见第 6 步的桥）
            if (target.sqrMagnitude > 0.0001f)
            {
                var ec = go.GetComponent<EnemyController>();
                if (ec != null) ec.SetNavDestination(target);
            }

            NetHostSpawnBridge.ReportUnit(source, slot, go, rot, target);   // 房主：把"怎么造出来的"下发（成员据此复刻）
            return go;
        }

        /// <summary>正在"复刻房主广播的巡逻队"（见 <see cref="CreatPatrolRemote"/>）：跳过本端的成员闸门。</summary>
        bool applyingRemotePatrol;

        public List<GameObject> CreatPatrol(Vector3 pos)
        {
            // ★ 联机：巡逻队是**房主权威**（2026-10-11）—— 成员端不在本地造：`Actor.Awake` 会给新单位分配 NetId，
            //   而 NetId 的口径是"房主分配 + 两端创建顺序一致" ⇒ 成员端自造就会与房主下发的号段撞号（移错位/错杀）。
            //   成员端等房主广播（4052）后由 NetHostSpawnBridge 调 CreatPatrolRemote 复刻（同 CreatWave 的 applyingRemoteWave 模式）。
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            bool online = flow != null && (flow.IsHost || flow.SelfSid != 0u);
            if (!applyingRemotePatrol && online && !flow.IsHost) return new List<GameObject>();

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

        /// <summary>【联机】按房主广播复刻一支巡逻队（跳过成员闸门）。返回本端新建的单位，**顺序与房主一致**
        /// （构成由落点派生流决定 ⇒ 同落点同顺序），供 <c>NetHostSpawnBridge</c> 逐单位设目标点。</summary>
        public List<GameObject> CreatPatrolRemote(Vector3 pos)
        {
            applyingRemotePatrol = true;
            try { return CreatPatrol(pos); }
            finally { applyingRemotePatrol = false; }
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
