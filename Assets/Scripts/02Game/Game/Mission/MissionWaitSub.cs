using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FpsGame.Mission
{

    /// <summary>
    /// 空任务，只需要完成全部子任务
    /// </summary>
    [AddComponentMenu("任务/完成全部子任务", 30)]
    public class MissionWaitSub : MissionBase
    {
        protected override void CreatMission()
        {
            MaxProgress = subTask.Length;
            foreach (var sub in subTask)
            {
                sub.OnMissionCompleted += OnSubMissionCompleted;
            }
        }

        void OnSubMissionCompleted(MissionBase mission)
        {
            mission.OnMissionCompleted -= OnSubMissionCompleted;
            if (++NowProgress == MaxProgress)
            {
                CompleteMission();
            }
            else
            {
                UpdateMission();
            }
            
        }

    }
}