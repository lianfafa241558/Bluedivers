using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.GameContract;

using UnityEngine;
using FPSGame.AI;
using FPSGame.Gameplay;
using FPSGame.Utils;
namespace FPSGame.Mission
{
    /// <summary>
    /// 炼油
    /// 初始需要在地图上寻找油井，从炼油平台(战备id15，预制体Assets/Resources/Prefabs/Airdrop/OilPlane.prefab)拉出管线连到油井
    /// 需要连接的数量和玩家数量有关，最少一个最多三个
    /// 连接完管线后进入等待状态，等玩家交互控制台进入开始状态
    /// 开始状态，控制台会进入一个180秒的加载，然后每隔一段时间触发一波波次
    /// 到了一定时间会进入一个维修状态，随机选择管道错误，并刷出以受损管道为巡逻目标的巡逻队
    /// 要求修好了全部错误的管道就恢复start阶段
    /// </summary>
    [AddComponentMenu("任务/主要/炼油", 30)]
    public class MissionOilRefining : MissionCompleteKeyScreen
    {
        private enum MissionState
        {
            Init,
            Wait,
            Start,
            Repair,
            End,
        }
        /// <summary>维修巡逻队与受损管道的最小距离</summary>
        private const float MinPatrolDistance = 45f;
        /// <summary>维修巡逻队与受损管道的最大距离</summary>
        private const float MaxPatrolDistance = 70f;

        private MissionState state;
        private int tickCount;
        private GameObject plane;
        private int battleStage=0;
        private List<int> errorTimes;
        private List<MissionSubConnectPipes> subCP;
        /// <summary>本次维修中损坏的管道（管道没有独立的"已修复"标记，修复成功即 Id 从 PipeError 变回 PipeComplete）</summary>
        private List<Furniture_Pipe> errorPipes = new();
        /// <summary>上一次刷新提示时的已修复数量，避免每 tick 重复刷新 UI</summary>
        private int fixedPipeCount = -1;

        /// <summary>本局"确定性修复"的种子（0 = 单机/无种子 ⇒ 走全局随机流，行为与改造前一致）。</summary>
        private int repairSeed;

        protected override void StartMission()
        {
            //base.CreatMission();
            subCP = new();
            // 房间人数走「数据自持」（2026-10-01 取代 ServiceLocator.Room）：
            // TeamState 未就绪时为 0 ⇒ 按"单人"兜底（与原空对象 PlayerCount=1 的语义一致）
            int playerCount = FPSGame.Data.TeamState.PlayerCount > 0 ? FPSGame.Data.TeamState.PlayerCount : 1;
            MaxProgress =Mathf.Min(playerCount,subTask.Length);
            //Debug.LogError("子任务长度"+ MaxProgress);
            foreach (var sub in subTask)
            {
                sub.OnMissionCompleted += OnSubMissionCompleted;
            }
            TickTime = 13;
            UpdateTip("等待平台部署");
            state = MissionState.Init;
            errorTimes = new() {0};
            // ★ 联机确定性（2026-10-11）：修坏管的时间点用**纯函数**（本局种子 + 任务稳定键 + 序号）生成 ⇒ 两端恒等；
            //   单机（Seed == 0）保持原来的全局随机流。
            repairSeed = ResolveRepairSeed();
            int now = 0,min,max;
            switch (playerCount)
            {
                case 1 :
                    min = 50; max = 70;
                    break;
                case 2:
                    min = 45; max = 65;
                    break;
                case 3:
                    min = 40; max = 60;
                    break;
                default:
                    min = 35; max = 55;
                    break;
            }
            int repairIndex = 0;
            while (now<150)
            {
                now += repairSeed != 0
                    ? DeriveRange(repairSeed, repairIndex++, min, max + 1)
                    : random.Range(min, max+1);
                if (now < 150) errorTimes.Add(now);
            }
        }

        public override bool Tick()
        {
            if (completed) return false;
            ++tickCount;
            switch (state)
            {
                case MissionState.Init:
                    if (tickCount==1)
                    {
                        manager.ReleaseAirdrop(pos, 15, InitPlane);
                        CreatNotice("Kotama", "Airdrop");
                    }
                    else
                    {
                        var points = plane.GetComponentsInChildren<Transform>()
                         .Where(t => t.CompareTag("MissionPoint"));
                        foreach (var item in points)
                        {
                            manager.ReleaseAirdrop(item.position,item.eulerAngles.y, 14);
                        }
                        UpdateTip($"寻找油泵并连接管道 [{NowProgress}/{MaxProgress}]");
                        state = MissionState.Wait;
                    }
                    break;
                case MissionState.Wait:

                    break;
                case MissionState.Start:
                    tickCount = (int)keyScreen.GetTime();
                    percentage = tickCount / keyScreen.nowProcedure.time;
                    UpdateMission();
                    if(tickCount == errorTimes[battleStage] + 5)
                    {
                        manager.CreatWave(WaveCreateParams.Extra.Set(pos));
                    }
                    if (battleStage < errorTimes.Count - 1 && tickCount == errorTimes[battleStage + 1])
                    {
                        state = MissionState.Repair;
                        EnterRepair();
                    }
                    break;
                case MissionState.Repair:
                    //维修期间让终端计时停摆：TickTime 恒为 1，正好抵消每 tick 补回的 1 秒
                    keyScreen.AddTime(1);
                    if (IsRepairFinished())
                    {
                        //修好了全部损坏的管道，回到抽取阶段继续推进
                        ++battleStage;
                        state = MissionState.Start;
                        errorPipes.Clear();
                        UpdateTip("保护管线直到香料提取完成");
                    }
                    break;
                case MissionState.End:

                    break;
            }
            return true;
        }


        void OnSubMissionCompleted(MissionBase mission)
        {
            mission.OnMissionCompleted -= OnSubMissionCompleted;
            subCP.Add(mission as MissionSubConnectPipes);
            if (TryAddProgress())
            {
                AddTag(MissionTag.IsActive);
                TickTime = 1;
                keyScreen.gameObject.SetActive(true);
                foreach (var sub in subTask)
                {
                    sub.OnMissionCompleted -= OnSubMissionCompleted;
                    sub.RemoveTag(MissionTag.IsActive);
                    sub.UpdateMission();
                }
                UpdateTip("启动终端，抽取香料");
            }
            else
            {
                UpdateTip($"寻找油泵并连接管道 [{NowProgress}/{MaxProgress}]");
            }
            
        }

        protected override void OnUpdateStage(int stage)
        {
            if (!keyScreen.IsActive || keyScreen.IsEnd) return;
            switch (stage) {
                case 1:
                    percentage = 0f;
                    tickCount = 0;
                    state = MissionState.Start;
                    break;
                case 2:
                    state = MissionState.End;

                    break;
            }
            UpdateTip("保护管线直到香料提取完成");
        }
        /// <summary>
        /// 进入维修状态：随机打坏每段管线中的部分管道，并为每根受损管道生成一支巡逻队
        /// </summary>
        private void EnterRepair()
        {
            errorPipes.Clear();
            fixedPipeCount = -1;
            int subIndex = 0;
            foreach (var sub in subCP)
            {
                if (!sub || sub.pipes == null) continue;
                var items = PickRepairPipes(sub.pipes, Mathf.Clamp(sub.pipes.Count/5,1,2), subIndex++);
                foreach (var item in items)
                {
                    if (!item) continue;
                    item.Error();
                    errorPipes.Add(item);
                    //生成目标为某个管子的巡逻队
                    CreatRepairPatrol(item);
                }
            }
        }

        /// <summary>是否全部受损管道都已修复（并顺带刷新维修进度提示）</summary>
        private bool IsRepairFinished()
        {
            int count = 0;
            foreach (var pipe in errorPipes)
            {
                //管道被销毁时不再阻挡维修进度，避免任务卡死
                if (!pipe || pipe.Id != "PipeError") ++count;
            }
            if (count != fixedPipeCount)
            {
                fixedPipeCount = count;
                UpdateTip($"修复损坏的管线 [{count}/{errorPipes.Count}]");
            }
            return count >= errorPipes.Count;
        }

        /// <summary>由"本局种子 + 任务稳定键(<see cref="MissionBase.netOrder"/>) + 阶段"派生本任务的修复流。</summary>
        private int ResolveRepairSeed()
        {
            int seed = FPSGame.Data.TaskState.Seed;
            if (seed == 0) return 0;
            return SeedUtil.Derive(SeedUtil.Derive(seed, (int)SeedStream.MissionEntity * 1000 + netOrder), 7771);
        }

        /// <summary>纯函数取 [min,max) 内的整数（同 seed+index ⇒ 恒等；**重试/调用次数差异都不影响**，
        /// 这正是不能用"一条 System.Random 流"的原因 —— 流会因为消费次数不同而错位）。</summary>
        private static int DeriveRange(int seed, int index, int min, int max)
        {
            if (max <= min) return min;
            int h = SeedUtil.Derive(seed, index);
            return min + (int)(unchecked((uint)h) % (uint)(max - min));
        }

        /// <summary>挑本次维修要打坏的管道：联机走**纯函数**（按 (种子, 阶段, 子任务序, 管序) 排序取前 n），
        /// 单机保持原来的 <c>random.RandomOrdering</c> 行为。</summary>
        private List<Furniture_Pipe> PickRepairPipes(List<Furniture_Pipe> pipes, int count, int subIndex)
        {
            if (repairSeed == 0) return random.RandomOrdering(pipes).Take(count).ToList();

            var order = new List<int>(pipes.Count);
            for (int i = 0; i < pipes.Count; ++i) order.Add(i);

            int stageKey = SeedUtil.Derive(repairSeed, battleStage * 1000 + subIndex);
            order.Sort((a, b) =>
            {
                int ha = SeedUtil.Derive(stageKey, a);
                int hb = SeedUtil.Derive(stageKey, b);
                int c = unchecked((uint)ha).CompareTo(unchecked((uint)hb));
                return c != 0 ? c : a.CompareTo(b);   // ⚠ 哈希相等时按原始下标 ⇒ 稳定，避免两端顺序不同
            });

            var re = new List<Furniture_Pipe>();
            for (int i = 0; i < count && i < order.Count; ++i) re.Add(pipes[order[i]]);
            return re;
        }

        /// <summary>为受损管道生成一支以它为巡逻目标点的巡逻队</summary>
        private void CreatRepairPatrol(Furniture_Pipe pipe)
        {
            Vector3 target = pipe.transform.position;
            Vector3 spawn = GetPatrolSpawnPos(target);
            var units = manager.CreatPatrol(spawn);
            foreach (var unit in units)
            {
                if (!unit || !unit.TryGetComponent(out EnemyController controller)) continue;
                controller.HomePoint = target;
            }

            // ★ 联机（2026-10-11）：巡逻队是房主权威 —— 房主把"生成点 + HomePoint"报给桥，成员端复刻同一支（4052）。
            //   ⚠ 成员端这一句是空操作（CreatPatrol 已被闸门挡成空表 + 桥只认房主），不会重复生成。
            if (units != null && units.Count > 0)
                FPSGame.Gameplay.BattleEventBus.ReportPatrolSpawn(spawn, target, true);
        }

        /// <summary>在受损管道周围找一个合法的巡逻队生成点（与管道拉开距离，避免一出生就到达而自毁）</summary>
        private Vector3 GetPatrolSpawnPos(Vector3 pipePos)
        {
            Vector3 fallback = pipePos;
            for (int i = 0; i < 8; i++)
            {
                Vector2 dir = random.RandomVector2();
                Vector3 pos = pipePos + new Vector3(dir.x, 0f, dir.y) * random.Range(MinPatrolDistance, MaxPatrolDistance);
                if (!UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 20f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, pipePos) >= MinPatrolDistance * 0.5f) return hit.position;
                fallback = hit.position;
            }
            return fallback;
        }

        void InitPlane(GameObject go)
        {
            plane = go;
            keyScreen = go.GetComponentInChildren<KeyScreen>();
            keyScreen.OnComple.AddListener(OnComple);
            //因为这个战备是后摇下来的，要重新绑定
            keyScreen.OnUpdateStage += OnUpdateStage;
            keyScreen.gameObject.SetActive(false);
        }
    }
}