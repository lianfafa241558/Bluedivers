using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;

namespace FpsGame.Mission
{
    /// <summary>
    /// 雷达站
    /// </summary>
    [AddComponentMenu("任务/次要/完成控制台", 30)]
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
        }
    }
}