using System.Collections.Generic;
using FPSGame.Core;
using PEMaths;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 伤害配置接口(FpsHelper.Hit 通过此接口统一处理完整/持续两种伤害配置)
    /// </summary>
    public interface IDamageData
    {
        PEInt GetExplosionDamage(PEInt chargeScale, PEInt distance);
        PEInt GetDirectDamage(PEInt chargeScale);
        PEInt GetDamageInnerRadius(PEInt chargeScale);
        PEInt GetDamageOuterRadius(PEInt chargeScale);
        /// <summary>效果系数，内半径1，外半径衰减到0</summary>
        PEInt GetEffectRadiusScale(PEInt distance, PEInt chargeScale);

        PEInt GetDestructeRadius(PEInt chargeScale);
        PEInt GetShockwaveRadius(PEInt chargeScale);
        PEInt GetSoundRadius(PEInt chargeScale);
        /// <summary>逻辑层：命中点(AI 听觉用)的噪声半径(米)。表现层音效播放距离用 SoundRadius</summary>
        PEInt GetImpactSoundRadius(PEInt chargeScale);
        public PEInt GetWeaknessBonus();
        public int GetDirectAP(PEInt chargeScale);
        public int GetExplosionAP(PEInt chargeScale);
        public int GetDemolishValue(PEInt distance);
        List<SKVP<DamageTypeEnum, float>> DamageGroupDirect { get; }
        List<SKVP<DamageTypeEnum, float>> DamageGroupExplosion { get; }
          
        bool NoSource { get; }
        GameObject ImpactVfx { get; }
        float ImpactVfxSpawnOffset { get; }
        bool UseCollisionDirection { get; }

        bool UseExplode { get; }
        bool OnlyTerrain { get; }
        AudioClip ImpactSfx { get; }
        bool UseHole { get; }
        GameObject Hole { get; }
    }
}
