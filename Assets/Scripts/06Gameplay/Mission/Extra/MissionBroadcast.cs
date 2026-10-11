using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.AI;
using FPSGame.Gameplay;
namespace FPSGame.Mission
{
    /// <summary>
    /// 非法广播
    /// </summary>
    [AddComponentMenu("任务/次要/非法广播", 30)]
    public class MissionBroadcast : MissionBase
    {
        I_AIController tower;
        KeyScreen keyScreen;

        protected override void StartMission()
        {
            var towerGo = entity.transform.Find("BroadcastTower");
            tower = towerGo.GetComponent<I_AIController>();
            tower.OnDie += OnTowerDeath;

            keyScreen = towerGo.GetComponentInChildren<KeyScreen>();
            keyScreen.OnComple.AddListener(OnKeyScreenComple);

        }

        private void OnKeyScreenComple()
        {
            //Debug.LogError("激活控制台完成" + gameObject + entity.transform, entity.transform);
            //tower.Kill();
            entity.transform.Find("BroadcastTower").Find("Audio").gameObject.SetActive(false);
            CompleteMission();

        }


        void OnTowerDeath()
        {
            Debug.Log("单位死亡，完成" + gameObject + entity.transform, entity.transform);
            tower.OnDie -= OnTowerDeath;
            keyScreen.furn.canOperate = false;
            CompleteMission();
        }

        /// <summary>⚠ 成对退订（2026-10-11 补）：基类 <c>Uninit</c> 不管这两条 ⇒ 原来是"任务结束了、
        /// 事件还挂在塔和终端上"（塔先倒下时 <c>OnKeyScreenComple</c> 甚至会打到已收尾的任务上）。</summary>
        protected override void Uninit()
        {
            base.Uninit();
            if (tower != null) tower.OnDie -= OnTowerDeath;
            if (keyScreen != null) keyScreen.OnComple.RemoveListener(OnKeyScreenComple);
        }
    }
}