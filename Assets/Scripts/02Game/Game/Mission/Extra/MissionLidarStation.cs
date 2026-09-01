using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;

namespace FpsGame.Mission
{
    /// <summary>
    /// 雷达站
    /// </summary>
    [AddComponentMenu("任务/次要/雷达站", 30)]
    public class MissionLidarStation : MissionBase
    {
        KeyScreen keyScreen;

        protected override void StartMission()
        {
            keyScreen = entity.transform.GetComponentInChildren<KeyScreen>();
            keyScreen.OnComple += OnKeyScreenComple;

        }

        private void OnKeyScreenComple()
        {
            CompleteMission();
            //雷达站完成，暴露全图所有未结束的任务
            var missions = BattleManager.Instance.MissionCont.missions;
            if (missions == null) return;
            foreach (var mission in missions)
            {
                if (mission.end) continue;
                if (mission.entity.IsValid()) mission.entity.TryDiscovered();
            }
        }
    }
}