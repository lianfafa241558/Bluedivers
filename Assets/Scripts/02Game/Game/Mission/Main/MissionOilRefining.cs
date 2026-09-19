using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FPSGame.AI;
using GameContract;

using UnityEngine;
namespace FpsGame.Mission
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
    public class MissionOilRefining : MissionCompleteKeySceern
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

        protected override void StartMission()
        {
            //base.CreatMission();
            subCP = new();
            MaxProgress =Mathf.Min(RoomManager.Instance.players.Count,subTask.Length);
            //Debug.LogError("子任务长度"+ MaxProgress);
            foreach (var sub in subTask)
            {
                sub.OnMissionCompleted += OnSubMissionCompleted;
            }
            TickTime = 13;
            UpdateTip("等待平台部署");
            state = MissionState.Init;
            errorTimes = new() {0};
            int now = 0,min,max;
            switch (RoomManager.Instance.players.Count)
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
            while (now<150)
            {
                now += random.Range(min, max+1);
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
            if (++NowProgress == MaxProgress)
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
            foreach (var sub in subCP)
            {
                if (!sub || sub.pipes == null) continue;
                var items = random.RandomOrdering(sub.pipes).Take(Mathf.Clamp(sub.pipes.Count/5,1,2));
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

        /// <summary>为受损管道生成一支以它为巡逻目标点的巡逻队</summary>
        private void CreatRepairPatrol(Furniture_Pipe pipe)
        {
            Vector3 target = pipe.transform.position;
            var units = manager.CreatPatrol(GetPatrolSpawnPos(target));
            foreach (var unit in units)
            {
                if (!unit || !unit.TryGetComponent(out EnemyController controller)) continue;
                controller.HomePoint = target;
            }
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