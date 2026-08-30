using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FpsGame.Mission
{

    /// <summary>防守</summary>
    [AddComponentMenu("任务/主要/防守", 30)]
    public class MissionDefensive : MissionBase
    {
        List<Vector3> wavePoints;
        KeyScreen keyScreen;
        [SerializeField]
        string targetId; 
        [SerializeField]
        AudioSource onDamageAud,onDeathAud;

        public int time;
        int m_TargetCount;

        protected override void StartMission()
        {
            Debug.LogWarning("start防守任务");
            UpdateText("防守任务目标", "");
            TickTime = 1;
            time = -60;
            keyScreen = entity.transform.GetComponentInChildren<KeyScreen>();
            pos = keyScreen.transform.parent.position;
            wavePoints = new();
            foreach (Transform child in entity.transform)
            {
                if (child.name.Contains("WavePoint"))
                {
                    wavePoints.Add(child.position);
                }
            }
            var targets = entity.transform.GetComponentsInChildren<Actor>().Where(item=>item.Id == targetId).Select(item=>item.GetComponent<I_AIController>()).ToList();
            foreach(var item in targets)
            {
                item.OnDamaged += OnTargetDamage;
                item.OnDie += OnTargetDeath;
            }
            m_TargetCount = targets.Count;
            keyScreen.OnUpdateStage += OnStageChange;
        }
        protected override void Uninit()
        {
            base.Uninit();
            keyScreen.OnUpdateStage -= OnStageChange;
        }

        public override bool Tick()
        {
            //主线不用
            //base.Tick();
            if (end) return true;
            ++time;

            if (time < 840)
            {
                var smallCount = time % 120;
                if (smallCount == 0) keyScreen.SetStage(1);//显示进度条
                //每 120 秒 作为一个大端，50秒一个小波，持续2波，30秒休息
                if (smallCount >= 0) percentage = smallCount / 120f;//显示进度条
                if (smallCount >= 0 && smallCount < 100 && smallCount % 50 == 0)
                {
                    Vector3[] points = new Vector3[2];
                    for (int i = 0; i < 2; ++i)
                    {
                        points[i] = wavePoints.RandomTake();
                    }
                    float playerCountScale=Mathf.Lerp(0.5f,1,(ActorsManager.Players.Count-1)/3);
                    BattleManager.Instance.CreatWave(WaveCreateParams.Defensive.Set(pos, points).Scale((0.4f + (time / 120) * 0.1f)* playerCountScale));

                }
                UpdateTip(time < 0 ? "做好防御准备  [" + Tool.FloatToTime(-time) + "]" : "坚持到撤离 [" + Tool.FloatToTime(840 - time) + "]");
            }
            //上面已经处理了，完成了就不会再执行
            if (time >= 840)
            {
                CompleteMission();
            }
            
            return true;
        }

        void OnStageChange(int stage)
        {
            if (stage==1&&time < -1)
            {
                time = -1;
            }
        }

        void OnTargetDamage(Collider _)
        {
            if(!onDamageAud.isPlaying)onDamageAud.Play();
        }

        void OnTargetDeath()
        {
            onDeathAud.Play();
            if ((--m_TargetCount)== 0){
                FailMission();
            }
            
        }
    }
}