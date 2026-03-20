using System.Collections.Generic;
using PEMaths;
using Unity.FPS.AI;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public class HealthEnemy : Health {

        protected override void Start() {
            base.Start();
            PEInt scale = 1+(TaskManager.Instance.nowTask.ExtraDifficulty[3]* (PEInt)0.1f);
            switch (TaskManager.Instance.nowTask.difficulty)
            {
                case DifficultyEnum.Normal:
                    scale *= (PEInt)0.7f;
                    break;
                case DifficultyEnum.Hard:
                    scale *= (PEInt)0.8f;
                    break;
                case DifficultyEnum.VeryHard:
                    scale *= (PEInt)0.9f;
                    break;
                case DifficultyEnum.HardCode:
                    break;
                case DifficultyEnum.Extreme:
                    scale *= (PEInt)1.1f;
                    break;
                case DifficultyEnum.Insane:
                    scale *= (PEInt)1.2f;
                    break;
                case DifficultyEnum.Torment:
                    scale *= (PEInt)1.35f;
                    break;
                case DifficultyEnum.Lunatic:
                    scale *= (PEInt)1.5f;
                    break;
            }
            showHealth = (CurrentHealth *= scale).RawInt;

        }
    }
}