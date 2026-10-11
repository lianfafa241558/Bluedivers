using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Gameplay;

namespace FPSGame.Mission
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
            // ⚠ 2026-10-11 修：原来写的是 RemoveListener（在 StartMission 里退订一个从没订阅过的回调）
            //   ⇒ 终端完成事件永远打不到本任务 ⇒ 雷达站任务永远完不成、RevealAllMissions 也不触发。
            keyScreen.OnComple.AddListener(OnKeyScreenComple);

        }

        private void OnKeyScreenComple()
        {
            CompleteMission();
            //雷达站完成，暴露全图所有未结束的任务
            // 走战斗事件总线：原先这里遍历 MissionCont.missions 的逻辑已收进 MissionController.RevealAll()
            FPSGame.Gameplay.BattleEventBus.RevealAllMissions();
        }
    }
}