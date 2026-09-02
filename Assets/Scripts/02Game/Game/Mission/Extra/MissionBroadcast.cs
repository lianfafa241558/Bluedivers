using System.Collections;
using System.Collections.Generic;
using GameContract;
using UnityEngine;
namespace FpsGame.Mission
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
    }
}