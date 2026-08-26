using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;

using UnityEngine;
using Utils;

namespace Unity.FPS.Game
{
    [System.Serializable]
    /// <summary>
    /// 持续效果专用伤害配置(无直击伤害、无蓄力、无速度/重力等运动参数)
    /// </summary>
    public class SustainedDamageData : IDamageData
    {
        //[Header("爆炸/范围伤害")]
        [SerializeField]
        private float DamageExplosion;

        /// <summary>伤害内半径</summary>
        [SerializeField]
        private float ExplosionInnerRange = 0;

        /// <summary>伤害外半径</summary>
        [SerializeField]
        private float ExplosionRange = 0;

        /// <summary>地形破坏半径</summary>
        [SerializeField]
        private float DestructeRadius = 0;

        /// <summary>冲击波半径</summary>
        [SerializeField]
        private float ShockwaveRadius = 0;

        /// <summary>伤害成分</summary>
        public List<SKVP<DamageTypeEnum, float>> DamageGroupExplosion = new() { new(DamageTypeEnum.Explosion, 1), new(DamageTypeEnum.Destruction, 1) };

        /// <summary>发出的声音影响范围</summary>
        public int SoundRadius = 20;

        /// <summary>无源伤害</summary>
        public bool NoSource = false;

        [SerializeField]
        /// <summary>爆炸穿甲等级（对所有爆炸成分类型统一有效）</summary>
        private int ExplosionAP;

        [SerializeField]
        /// <summary>拆毁值：大于目标 Health 的拆毁值时直接秒杀。默认 0</summary>
        private int demolishValue;

        //[Header("碰撞/特效")]
        /// <summary>特效使用碰撞点的朝向</summary>
        public bool UseCollisionDirection = true;

        /// <summary>特效沿法线偏移量</summary>
        public float ImpactVfxSpawnOffset = 0.1f;

        /// <summary>碰撞特效</summary>
        public GameObject ImpactVfx;

        /// <summary>碰撞音效</summary>
        public AudioClip ImpactSfx;

        /// <summary>创建弹坑</summary>
        public bool UseHole;

        /// <summary>弹坑/不填使用默认</summary>
        public GameObject Hole;

        /// <summary>只附着到地面</summary>
        public bool OnlyTerrain;

        // 持续效果不做直击伤害
        static readonly List<SKVP<DamageTypeEnum, float>> s_EmptyDirect = new();
        List<SKVP<DamageTypeEnum, float>> IDamageData.DamageGroupDirect => s_EmptyDirect;

        public bool UseExplode => DamageExplosion > 0;

        public PEInt GetDirectDamage(PEInt chargeScale) => 0;

        public PEInt GetExplosionDamage(PEInt chargeScale, PEInt distance)
        {
            PEInt outerRange = GetDamageOuterRadius(chargeScale);
            PEInt innerRange = GetDamageInnerRadius(chargeScale);
            PEInt damage = (PEInt)DamageExplosion;
            if (distance < innerRange)
            {
                return damage;
            }
            else if (distance < outerRange)
            {
                return damage * PEMath.Clamp((outerRange - distance) / (outerRange - innerRange), 0, 1);
            }
            else return 0;
        }

        /// <summary>爆炸内半径</summary>
        public PEInt GetDamageInnerRadius(PEInt chargeScale) => (PEInt)ExplosionInnerRange;
        /// <summary>爆炸外半径</summary>
        public PEInt GetDamageOuterRadius(PEInt chargeScale) => (PEInt)ExplosionRange;
        /// <summary>地形破坏半径</summary>
        public PEInt GetDestructeRadius(PEInt chargeScale) => (PEInt)DestructeRadius;
        /// <summary>冲击波半径</summary>
        public PEInt GetShockwaveRadius(PEInt chargeScale) => (PEInt)ShockwaveRadius;
        /// <summary>音量</summary>
        public PEInt GetSoundRadius(PEInt chargeScale) => (PEInt)SoundRadius;

        public PEInt GetWeaknessBonus() => 0;

        public int GetDirectAP(PEInt chargeScale) => ExplosionAP;
        public int GetExplosionAP(PEInt chargeScale) => ExplosionAP;
        public int GetDemolishValue() => demolishValue;
        #region 接口映射(显式实现以保留字段的Unity序列化)
        List<SKVP<DamageTypeEnum, float>> IDamageData.DamageGroupExplosion => DamageGroupExplosion;
        bool IDamageData.NoSource => NoSource;
        GameObject IDamageData.ImpactVfx => ImpactVfx;
        float IDamageData.ImpactVfxSpawnOffset => ImpactVfxSpawnOffset;
        bool IDamageData.UseCollisionDirection => UseCollisionDirection;
        bool IDamageData.OnlyTerrain => OnlyTerrain;
        AudioClip IDamageData.ImpactSfx => ImpactSfx;
        bool IDamageData.UseHole => UseHole;
        GameObject IDamageData.Hole => Hole;
        #endregion
    }
}
