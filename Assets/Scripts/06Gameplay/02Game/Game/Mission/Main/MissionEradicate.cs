using System.Collections;
using System.Collections.Generic;
using FPSGame.Core;

using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Mission
{


    /// <summary>彻底消灭</summary>
    [AddComponentMenu("任务/主要/彻底消灭", 30)]
    public class MissionEradicate : MissionBase
    {
        [InspectorName("击杀基数")]
        [SerializeField]
        private int enemyBaseValue=15;
        [SerializeField]
        private int LastWaveTickCount;
        [SerializeField]
        private int freeCount;
        [SerializeField]
        private int showCount;
        protected override void StartMission()
        {
            UnitEventSub.OnEnemyDead += EnemyDead;
            var task = FPSGame.GameContract.ServiceLocator.Task;
            MaxProgress = enemyBaseValue * root.campData.enemyVarietyType.ToEnemyType() switch {
                EnemyType.Kaiser => 5,
                EnemyType.Decagrammaton => 6,
                EnemyType.Colour => 8,
                _ => 5
            };
            MaxProgress = (int)(MaxProgress * Mathf.Sqrt((int)task.Difficulty) * (1 + 0.1f * task.ExtraDifficulty[2]));
            //MaxProgress /= 100;
            UpdateText("消灭敌方部队", "");
            TickTime = 5;

        }

        public override bool Tick()
        {
            //主线不用
            //base.Tick();
            if (data.complete) return true;
            showCount = FPSGame.GameContract.ServiceLocator.Battle.WaveCount;
            if (TickCount - LastWaveTickCount >20|| FPSGame.GameContract.ServiceLocator.Battle.WaveCount == 0)
            {
                if (++freeCount >= 4)
                {
                    FPSGame.GameContract.ServiceLocator.Battle.CreatWave(WaveCreateParams.Extra.Set(ActorsManager.Players.RandomTake().Pos).Scale(0.8f));
                    freeCount = 0;
                    LastWaveTickCount = TickCount;
                }

            }
            return true;
        }

        void EnemyDead(Actor unit)
        {
            //达成后立刻退订，避免继续计数
            if (AddProgress()) UnitEventSub.OnEnemyDead -= EnemyDead;
        }


    }
}