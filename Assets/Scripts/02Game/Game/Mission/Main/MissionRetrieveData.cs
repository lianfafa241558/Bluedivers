using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;
namespace FpsGame.Mission
{
    /// <summary>
    /// 检索有价值的信息
    /// </summary>
    [AddComponentMenu("任务/主要/检索有价值的信息", 30)]
    public class MissionRetrieveData : MissionCompleteKeySceern
    {

        protected override void StartMission()
        {
            base.StartMission();
            MaxProgress = subTask.Length;
            //Debug.LogError("子任务长度"+ MaxProgress);
            foreach (var sub in subTask)
            {
                sub.OnMissionCompleted += OnSubMissionCompleted;
            }
            keyScreen.gameObject.SetActive(false);
            
        }

        void OnSubMissionCompleted(MissionBase mission)
        {
            mission.OnMissionCompleted -= OnSubMissionCompleted;
            if (++NowProgress == MaxProgress)
            {
                AddTag(MissionTag.IsActive);
                keyScreen.gameObject.SetActive(true);
                UpdateMission();
            }

        }
    }
}