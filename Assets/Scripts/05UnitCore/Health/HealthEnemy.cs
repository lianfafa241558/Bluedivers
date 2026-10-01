using FPSGame.GameContract;
using PEMaths;
using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 敌人单位的生命实现。
    /// </summary>
    [AddComponentMenu("生命/敌人生命")]
    public class HealthEnemy : Health {

        protected override void Awake()
        {
            base.Awake();
            // 难度缩放走「数据自持」（2026-10-01 取代 ServiceLocator.Task 槽）：TaskManager 发布、单位内核直读。
            // ⚠ Awake 期注入来不及；TaskState 未就绪时就是中性值（系数 0 / Normal）⇒ 原判空为死代码，一并清掉。
            PEInt scale = 1 + (FPSGame.Data.TaskState.ExtraDifficulty[3] * (PEInt)0.1f);
            switch (FPSGame.Data.TaskState.Difficulty)
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
