using System.Collections.Generic;
using PEMaths;
using Unity.FPS.AI;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Game
{
    public class HealthEnemy : Health {

        protected override void Awake()
        {
            base.Awake();
            PEInt scale = 1 + (TaskManager.Instance.nowTask.ExtraDifficulty[3] * (PEInt)0.1f);
            switch (TaskManager.Instance.nowTask.difficulty)
            {
                case DifficultyEnum.Normal:
                    scale *= (PEInt)0.5f;
                    break;
                case DifficultyEnum.Hard:
                    scale *= (PEInt)0.6f;
                    break;
                case DifficultyEnum.VeryHard:
                    scale *= (PEInt)0.7f;
                    break;
                case DifficultyEnum.HardCode:
                    scale *= (PEInt)0.85f;
                    break;
                case DifficultyEnum.Extreme:
                    scale *= (PEInt)1f;
                    break;
                case DifficultyEnum.Insane:
                    scale *= (PEInt)1.15f;
                    break;
                case DifficultyEnum.Torment:
                    scale *= (PEInt)1.2f;
                    break;
                case DifficultyEnum.Lunatic:
                    scale *= (PEInt)1.35f;
                    break;
            }
            MaxHealth = showHealth = (CurrentHealth * scale).RawInt;
            foreach (var item in AboGauge)
            {
                item.Value.Max *= scale;
            }
        }
    }
}