using System.Collections.Generic;
using Core;
using PEMaths;

using UnityEngine;
using Utils;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;

    [System.Serializable]
    /// <summary>
    /// 伤害配置
    /// </summary>
    public class DamageData
    {


        //[Header("运动")]
        /// <summary>下坠速度</summary>
        public float Speed = 20f;
        /// <summary>下坠速度</summary>
        public float Gravity = 0f;
        /// <summary>继承武器初速度</summary>
        public bool InheritWeaponSpeed = false;
        /// <summary>生命周期</summary>
        public float MaxLifeTime = 5f;
        /// <summary>自爆引信(单位:M)</summary>
        public float MaxRange = -1;
        /// <summary>安全引信(单位:M)</summary>
        public float MinRange = -1;
        /// <summary>无源伤害</summary>
        public bool NoSource = false;
        /// <summary>发出的声音影响范围</summary> 
        public int SoundRadius = 20;


        //[Header("直击伤害")]
        /// <summary>直击伤害</summary> 
        [SerializeField]
        private float DamageDirect;

        /// <summary>伤害成分</summary> 
        public List<SKVP<DamageTypeEnum, float>> DamageGroupDirect = new() { new(DamageTypeEnum.Gun, 1), new(DamageTypeEnum.Destruction, 1) };

        //[Header("爆炸伤害")]
        /// <summary>爆炸伤害</summary>

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

        //TODO:还没实现
        /// <summary>冲击波半径</summary>
        [SerializeField]
        private float ShockwaveRadius = 0;

        /// <summary>伤害成分</summary>
        public List<SKVP<DamageTypeEnum, float>> DamageGroupExplosion2 = new() { new(DamageTypeEnum.Explosion, 1), new(DamageTypeEnum.Destruction, 1) };

        /// <summary>伤害成分</summary>
        public List<KVP<DamageTypeEnum, float>> DamageGroupExplosion = new() { new(DamageTypeEnum.Explosion, 1), new(DamageTypeEnum.Destruction, 1) };


        //[Header("碰撞")]
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

        /// <summary>只附着到地面/summary>
        public bool OnlyTerrain;

        //[Header("蓄力")]
        /// <summary>使用蓄力</summary>
        [SerializeField]
        private bool UseCharge = false;


        /// <summary>满蓄伤害倍率</summary>
        [SerializeField]
        private float ChargeDamageScale = 1;
        /// <summary>满蓄溅射范围倍率</summary>
        [SerializeField]
        private float ChargeAOERangeScale = 1;
        /// <summary>满蓄子弹速度</summary>
        [SerializeField]
        private float ChargeSpeedScale = 1;
        /// <summary>满蓄子弹重力</summary>
        [SerializeField]
        private float ChargeGravityScale = 1;
        /// <summary>满蓄子弹声音范围</summary>
        [SerializeField]
        private float ChargeSoundScale = 1;

        #region 获取

        public PEInt GetExplosionDamage(PEInt ChargeScale, PEInt distance)
        {
            PEInt outerRange = GetDamageOuterRadius(ChargeScale);
            PEInt innerRange = GetDamageInnerRadius(ChargeScale);
            PEInt damage = _HandleValue(DamageExplosion, ChargeDamageScale, ChargeScale);
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
        /// <summary>爆炸伤害</summary>
        public PEInt GetDirectDamage(PEInt ChargeScale) => _HandleValue(DamageDirect, ChargeDamageScale, ChargeScale);
        /// <summary>爆炸内半径</summary>
        public PEInt GetDamageInnerRadius(PEInt ChargeScale) => _HandleValue(ExplosionInnerRange, ChargeAOERangeScale, ChargeScale);
        /// <summary>爆炸外半径</summary>
        public PEInt GetDamageOuterRadius(PEInt ChargeScale) => _HandleValue(ExplosionRange, ChargeAOERangeScale, ChargeScale);
        /// <summary>地形破坏半径</summary>
        public PEInt GetDestructeRadius(PEInt ChargeScale) => _HandleValue(DestructeRadius, ChargeAOERangeScale, ChargeScale);
        /// <summary>冲击波半径</summary>
        public PEInt GetShockwaveRadius(PEInt ChargeScale) => _HandleValue(ShockwaveRadius, ChargeAOERangeScale, ChargeScale);

        /// <summary>速度</summary>
        public PEInt GetSpeed(PEInt ChargeScale) => _HandleValue(Speed, ChargeSpeedScale, ChargeScale);
        /// <summary>重力</summary>
        public PEInt GetGravity(PEInt ChargeScale) => _HandleValue(Gravity, ChargeGravityScale, ChargeScale);
        /// <summary>音量</summary>
        public PEInt GetSoundRadius(PEInt ChargeScale) => _HandleValue(SoundRadius, ChargeSoundScale, ChargeScale);

        //private PEInt _HandleValue(PEInt baseValue, PEInt scaleValue, PEInt charge)=> (PEInt)(baseValue * (UseCharge ? PEMath.Lerp(1, scaleValue, charge) : 1));

        private PEInt _HandleValue(float baseValue, float scaleValue, PEInt charge) => (PEInt)baseValue * (UseCharge ? PEMath.Lerp(1, (PEInt)scaleValue, charge) : 1);

        public float GetAttr(Attr type)
        {
            return type switch {
                Attr.DirectDamage => DamageDirect,
                Attr.ExplosionDamage => DamageExplosion,
                Attr.ExplosionRange => ExplosionRange,
                Attr.DirectDestruction => DamageGroupDirect.GetValue(DamageTypeEnum.Destruction),
                Attr.ExplosionDestruction => DamageGroupExplosion2.GetValue(DamageTypeEnum.Destruction),
                Attr.BulletSpeed => Speed,
                Attr.ChargeDamageScale => ChargeDamageScale,
                Attr.ChargeExplosionRangeScale => ChargeAOERangeScale,
                Attr.DirectWeakness => DamageGroupDirect.GetValue(DamageTypeEnum.Weakness),
                Attr.ExplosionWeakness => DamageGroupExplosion2.GetValue(DamageTypeEnum.Weakness),
                _ => 0,
            };
        }
        #endregion

        #region 设置


        public void SetDamageDirect(PEInt value) => DamageDirect = value.RawFloat;
        public void SetDamageExplosion(PEInt value) => DamageExplosion = value.RawFloat;
        public void SetExplosionRange(PEInt value) => ExplosionRange = value.RawFloat;
        public void SetDirectDestruction(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Destruction, value.RawFloat);
        public void SetExplosionDestruction(PEInt value) => DamageGroupExplosion2.SetValue(DamageTypeEnum.Destruction, value.RawFloat);

        public void SetSpeed(PEInt value) => Speed = value.RawFloat;

        public void SetChargeDamageScale(PEInt value) => ChargeDamageScale = value.RawFloat;

        public void SetChargeExplosionRangeScale(PEInt value) => ChargeAOERangeScale = value.RawFloat;

        public void SetDirectWeakness(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Weakness, value.RawFloat);
        public void SetExplosionWeakness(PEInt value) => DamageGroupExplosion2.SetValue(DamageTypeEnum.Weakness, value.RawFloat);


        #endregion
    }


}