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
            // ★ 上限与当前血必须**一起**缩放。血在 base.Awake() 里刚被置成**未缩放**的 MaxHealth，
            //   原来这里只写了 MaxHealth（⇒ 血留着旧值）：
            //   - 系数 <1（Normal 0.5 / Hard 0.6 / VeryHard 0.7 / **HardCode 0.85**）⇒ 血 200 / 上限 169
            //     （**血超上限**），随后在 Actor.OnDie 那条"单位死亡时生命值>0"的守卫里被当真异常报出来；
            //   - 系数 >1（Insane 1.15 等）⇒ 反而**出生不满血**（血 200 / 上限 230）。
            //   口径对齐 Damageable.cs 的护甲（remainArmor = maxArmor = armorValue * scale）：缩放后满血出生。
            PEInt scaled = CurrentHealth * scale;
            MaxHealth = showHealth = scaled.RawInt;
            CurrentHealth = MaxHealth;      // 满血（与 base.Awake 的 CurrentHealth = MaxHealth 同口径）
            foreach (var item in AboGauge)
            {
                item.Value.Max *= scale;
            }
        }
    }
}
