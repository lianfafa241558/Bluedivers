using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameContract;
using Unity.BaseTool;
using UnityEngine;
namespace FpsGame.Mission
{
    /// <summary>
    /// 炼油
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
        private MissionState state;
        private int tickCount;
        private GameObject plane;
        private int battleStage=0;
        private List<int> errorTimes;
        private List<MissionSubConnectPipes> subCP;

        protected override void CreatMission()
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
                        UpdateTip($"寻找油泵并连接管线  [{NowProgress}/{MaxProgress}]");
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
                    if (battleStage< errorTimes.Count-1&&tickCount == errorTimes[battleStage+1])
                    {
                        state = MissionState.Repair;
                        

                        //选择管子失灵
                        foreach (var sub in subCP)
                        {
                            var items = random.RandomOrdering(sub.pipes).Take(Mathf.Clamp(sub.pipes.Count/5,1,2));
                            foreach (var item in items)
                            {
                                item.Error();
                                //生成目标为某个管子的巡逻队
                            }
                        }
                    }
                    break;
                case MissionState.Repair:
                    keyScreen.AddTime(1);

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
                UpdateTip($"寻找油泵并连接管线  [{NowProgress}/{MaxProgress}]");
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
        void InitPlane(GameObject go)
        {
            plane = go;
            keyScreen = go.GetComponentInChildren<KeyScreen>();
            keyScreen.OnComple += OnComple;
            keyScreen.OnUpdateStage += OnUpdateStage;
            keyScreen.gameObject.SetActive(false);
        }
    }
}