using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;
using PEMaths;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 圣水：在范围内**治疗友军、伤害敌人**的持续效果（每 <c>TickInterval</c> 一次）。
    /// 友军判定用单位类型（玩家/盟友），不依赖队伍号（单机时队伍号可能都是 0）。
    /// <para>▍敌人那一半复用 <c>FpsHelper.HitAreaKinetic</c>：它是"直击包"结算（绕开爆炸抗性与遮挡），
    /// 伤害成分、半径、穿甲全取自本组件的 <c>DamageData</c>；友军那一半直接 <c>IHealth.Heal</c>。</para>
    /// </summary>
    [AddComponentMenu("持续效果/圣水")]
    public class HolyWaterEffect : SustainedEffect
    {
        [Header("治疗")]
        [InspectorName("每次治疗量")]
        [SerializeField]
        private float HealPerTick = 12f;

        protected override void ApplyEffect()
        {
            if (!DamageData.IsValid()) return;

            PEInt outer = DamageData.GetDamageOuterRadius(1);
            if (outer <= 0) return;

            Vector3 center = DamageAnchor ? DamageAnchor.position : transform.position;

            // 治疗：范围内所有玩家/盟友
            List<IActor> units = FPSGame.Game.UnitQuery.FindUnits(
                new PECircle((PEVector2)center, outer), TargetCfg.All, unit => FpsHelper.VaildTarget(unit));
            for (int i = 0; i < units.Count; ++i)
            {
                IActor unit = units[i];
                if (!IsFriend(unit)) continue;
                if (unit.transform.TryGetComponent(out IHealth health)) health.Heal(HealPerTick);
            }

            // 伤害：范围内的非友军
            FpsHelper.HitAreaKinetic(new FpsHelper.KineticAreaHitData
            {
                data = DamageData,
                groups = DamageData.DamageGroupExplosion,
                pos = (PEVector3)center,
                soure = Origin,
                exclude = null,
                filter = unit => !IsFriend(unit),
                knockbackForce = 0,
            });
        }

        private static bool IsFriend(IActor unit)
            => unit.Type == UnitTypeEnum.Player || unit.Type == UnitTypeEnum.Friend;
    }
}
