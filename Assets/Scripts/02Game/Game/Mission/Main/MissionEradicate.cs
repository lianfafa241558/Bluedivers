using System.Collections;
using System.Collections.Generic;
using Core;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;

namespace FpsGame.Mission
{


    /// <summary>彻底消灭</summary>
    [AddComponentMenu("任务/主要/彻底消灭", 30)]
    public class MissionEradicate : MissionBase
    {

        public int freeCount;

        protected override void CreatMission()
        {
            GlobalEventManager.OnEnemyDead += EnemyDead;
            var task = TaskManager.Instance.nowTask;
            MaxProgress = root.campData.enemyVarietyType.ToEnemyType() switch {
                EnemyType.Kaiser => 70,
                EnemyType.Decagrammaton => 90,
                EnemyType.Colour => 120,
                _ => 70
            };
            MaxProgress = (int)(MaxProgress * Mathf.Sqrt((int)task.difficulty) * (1 + 0.3f * task.ExtraDifficulty[2]));
            MaxProgress /= 100;
            UpdateText("消灭敌方部队", "");
            TickTime = 5;

        }

        public override bool Tick()
        {
            //主线不用
            //base.Tick();
            if (data.complete) return true;
            if (BattleManager.Instance.WaveCont.WaveCount == 0)
            {
                if (++freeCount >= 4)
                {
                    BattleManager.Instance.CreatWave(WaveCreateParams.Extra.Set(ActorsManager.Players.RandomTake().Pos).Scale(0.8f));
                    freeCount = 0;
                }

            }
            return true;
        }

        void EnemyDead(Actor unit)
        {
            if (++NowProgress < MaxProgress)
            {
                UpdateMission();
            }
            else
            {
                CompleteMission();
                GlobalEventManager.OnEnemyDead -= EnemyDead;
            }
        }


    }
}