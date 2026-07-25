using System.Collections.Generic;
using Core;
using PEMaths;

using UnityEngine;
using Utils;

namespace Unity.FPS.Game
{
    using Attr = WeaponAttrType;

    /// <summary>
    /// 伤害配置接口(FpsHelper.Hit 通过此接口统一处理完整/持续两种伤害配置)
    /// </summary>
    public interface IDamageData
    {
        PEInt GetExplosionDamage(PEInt chargeScale, PEInt distance);
        PEInt GetDirectDamage(PEInt chargeScale);
        PEInt GetDamageInnerRadius(PEInt chargeScale);
        PEInt GetDamageOuterRadius(PEInt chargeScale);
        PEInt GetDestructeRadius(PEInt chargeScale);
        PEInt GetShockwaveRadius(PEInt chargeScale);
        PEInt GetSoundRadius(PEInt chargeScale);
        public PEInt GetWeaknessBonus();
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

    [System.Serializable]
    /// <summary>
    /// 伤害配置(完整)
    /// </summary>
    public class DamageData : IDamageData
    {


        //[Header("运动")]
        /// <summary>速度</summary>
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
        /// <summary>爆炸伤害</summary> 
        [SerializeField]
        private float DamageExplosion;

        /// <summary>直击伤害</summary> 
        [SerializeField]
        private float DamageDirect;

        /// <summary>伤害成分</summary> 
        public List<SKVP<DamageTypeEnum, float>> DamageGroupDirect = new() { new(DamageTypeEnum.Gun, 1), new(DamageTypeEnum.Destruction, 1) };

        //[Header("爆炸伤害")]
        /// <summary>
        /// 弱点加成</summary>

        [SerializeField]
        private float WeaknessBonus;

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
        public List<SKVP<DamageTypeEnum, float>> DamageGroupExplosion = new() { new(DamageTypeEnum.Explosion, 1), new(DamageTypeEnum.Destruction, 1) };


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
        /// <summary>满蓄热量倍率</summary>
        [SerializeField]
        private float ChargeHeatScale = 1;
        /// <summary>满蓄散布倍率</summary>
        [SerializeField]
        private float ChargeSpreadScale = 1;

        #region 获取

        public bool UseExplode => DamageExplosion > 0;

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
        /// <summary>直击伤害</summary>
        public PEInt GetDirectDamage(PEInt ChargeScale) => _HandleValue(DamageDirect, ChargeDamageScale, ChargeScale);
        /// <summary>爆炸内半径</summary>
        public PEInt GetDamageInnerRadius(PEInt ChargeScale) => _HandleValue(ExplosionInnerRange, ChargeAOERangeScale, ChargeScale);
        /// <summary>爆炸外半径</summary>
        public PEInt GetDamageOuterRadius(PEInt ChargeScale) => _HandleValue(ExplosionRange, ChargeAOERangeScale, ChargeScale);
        /// <summary>地形破坏半径</summary>
        public PEInt GetDestructeRadius(PEInt ChargeScale) => _HandleValue(DestructeRadius, ChargeAOERangeScale, ChargeScale);
        /// <summary>冲击波半径</summary>
        public PEInt GetShockwaveRadius(PEInt ChargeScale) => _HandleValue(ShockwaveRadius, ChargeAOERangeScale, ChargeScale);
        public PEInt GetWeaknessBonus() => (PEInt)WeaknessBonus;


        /// <summary>速度</summary>
        public PEInt GetSpeed(PEInt ChargeScale) => _HandleValue(Speed, ChargeSpeedScale, ChargeScale);
        /// <summary>重力</summary>
        public PEInt GetGravity(PEInt ChargeScale) => _HandleValue(Gravity, ChargeGravityScale, ChargeScale);
        /// <summary>音量</summary>
        public PEInt GetSoundRadius(PEInt ChargeScale) => _HandleValue(SoundRadius, ChargeSoundScale, ChargeScale);
        /// <summary>散布</summary>
        public PEInt GetSpread(PEInt ChargeScale) => _HandleValue(1, ChargeSpreadScale, ChargeScale);

        //private PEInt _HandleValue(PEInt baseValue, PEInt scaleValue, PEInt charge)=> (PEInt)(baseValue * (UseCharge ? PEMath.Lerp(1, scaleValue, charge) : 1));

        private PEInt _HandleValue(float baseValue, float scaleValue, PEInt charge) => (PEInt)baseValue * (UseCharge ? PEMath.Lerp(1, (PEInt)scaleValue, charge) : 1);


        public float GetAttr(Attr type)
        {
            return type switch {
                Attr.DirectDamage => DamageDirect,
                Attr.ExplosionDamage => DamageExplosion,
                Attr.ExplosionRange => ExplosionRange,
                Attr.DirectDestruction => DamageGroupDirect.GetValue(DamageTypeEnum.Destruction),
                Attr.ExplosionDestruction => DamageGroupExplosion.GetValue(DamageTypeEnum.Destruction),
                Attr.BulletSpeed => Speed,
                Attr.ChargeDamageScale => ChargeDamageScale,
                Attr.ChargeExplosionRangeScale => ChargeAOERangeScale,
                Attr.ChargeHeatScale => ChargeHeatScale,
                Attr.WeaknessBonus => WeaknessBonus,
                Attr.LifeTime => MaxLifeTime,
                // 伤害类型系数
                Attr.DamageTypeGun => DamageGroupDirect.GetValue(DamageTypeEnum.Gun),
                Attr.DamageTypeExplosion => DamageGroupDirect.GetValue(DamageTypeEnum.Explosion),
                Attr.DamageTypeDestruction => DamageGroupDirect.GetValue(DamageTypeEnum.Destruction),
                Attr.DamageTypeReal => DamageGroupDirect.GetValue(DamageTypeEnum.Real),
                Attr.DamageTypeToxicity => DamageGroupDirect.GetValue(DamageTypeEnum.Toxicity),
                Attr.DamageTypeBurn => DamageGroupDirect.GetValue(DamageTypeEnum.Burn),
                Attr.DamageTypeFreeze => DamageGroupDirect.GetValue(DamageTypeEnum.Freeze),
                Attr.DamageTypeElectric => DamageGroupDirect.GetValue(DamageTypeEnum.Electric),
                Attr.DamageTypeVertigo => DamageGroupDirect.GetValue(DamageTypeEnum.Vertigo),
                Attr.DamageTypeTerror => DamageGroupDirect.GetValue(DamageTypeEnum.Terror),
                Attr.DamageTypeRadiation => DamageGroupDirect.GetValue(DamageTypeEnum.Radiation),
                Attr.DamageTypeHacker => DamageGroupDirect.GetValue(DamageTypeEnum.Hacker),
                // 伤害类型系数（爆炸伤害组）
                Attr.ExplosionTypeGun => DamageGroupExplosion.GetValue(DamageTypeEnum.Gun),
                Attr.ExplosionTypeExplosion => DamageGroupExplosion.GetValue(DamageTypeEnum.Explosion),
                Attr.ExplosionTypeDestruction => DamageGroupExplosion.GetValue(DamageTypeEnum.Destruction),
                Attr.ExplosionTypeReal => DamageGroupExplosion.GetValue(DamageTypeEnum.Real),
                Attr.ExplosionTypeToxicity => DamageGroupExplosion.GetValue(DamageTypeEnum.Toxicity),
                Attr.ExplosionTypeBurn => DamageGroupExplosion.GetValue(DamageTypeEnum.Burn),
                Attr.ExplosionTypeFreeze => DamageGroupExplosion.GetValue(DamageTypeEnum.Freeze),
                Attr.ExplosionTypeElectric => DamageGroupExplosion.GetValue(DamageTypeEnum.Electric),
                Attr.ExplosionTypeVertigo => DamageGroupExplosion.GetValue(DamageTypeEnum.Vertigo),
                Attr.ExplosionTypeTerror => DamageGroupExplosion.GetValue(DamageTypeEnum.Terror),
                Attr.ExplosionTypeRadiation => DamageGroupExplosion.GetValue(DamageTypeEnum.Radiation),
                Attr.ExplosionTypeHacker => DamageGroupExplosion.GetValue(DamageTypeEnum.Hacker),
                _ => 0,
            };
        }
        #endregion

        #region 设置


        public void SetDamageDirect(PEInt value) => DamageDirect = value.RawFloat;
        public void SetDamageExplosion(PEInt value) => DamageExplosion = value.RawFloat;
        public void SetExplosionRange(PEInt value)
        {
            var scale = ExplosionInnerRange / ExplosionRange;
            ExplosionRange = value.RawFloat;
            ExplosionInnerRange = value.RawFloat* scale;
        }
        public void SetDirectDestruction(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Destruction, value.RawFloat);
        public void SetExplosionDestruction(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Destruction, value.RawFloat);

        public void SetSpeed(PEInt value) => Speed = value.RawFloat;

        public void SetChargeDamageScale(PEInt value) => ChargeDamageScale = value.RawFloat;

        public void SetChargeExplosionRangeScale(PEInt value) => ChargeAOERangeScale = value.RawFloat;
        public void SetChargeHeatScale(PEInt value) => ChargeHeatScale = value.RawFloat;

        public void SetWeaknessBonus(PEInt value) => WeaknessBonus = value.RawFloat;
        public void SetLifeTime(PEInt value) => MaxLifeTime = value.RawFloat;

        // 伤害类型系数
        public void SetDamageTypeGun(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Gun, value.RawFloat);
        public void SetDamageTypeExplosion(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Explosion, value.RawFloat);
        public void SetDamageTypeDestruction(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Destruction, value.RawFloat);
        public void SetDamageTypeReal(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Real, value.RawFloat);
        public void SetDamageTypeToxicity(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Toxicity, value.RawFloat);
        public void SetDamageTypeBurn(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Burn, value.RawFloat);
        public void SetDamageTypeFreeze(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Freeze, value.RawFloat);
        public void SetDamageTypeElectric(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Electric, value.RawFloat);
        public void SetDamageTypeVertigo(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Vertigo, value.RawFloat);
        public void SetDamageTypeTerror(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Terror, value.RawFloat);
        public void SetDamageTypeRadiation(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Radiation, value.RawFloat);
        public void SetDamageTypeHacker(PEInt value) => DamageGroupDirect.SetValue(DamageTypeEnum.Hacker, value.RawFloat);

        // 伤害类型系数（爆炸伤害组）
        public void SetExplosionTypeGun(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Gun, value.RawFloat);
        public void SetExplosionTypeExplosion(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Explosion, value.RawFloat);
        public void SetExplosionTypeDestruction(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Destruction, value.RawFloat);
        public void SetExplosionTypeReal(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Real, value.RawFloat);
        public void SetExplosionTypeToxicity(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Toxicity, value.RawFloat);
        public void SetExplosionTypeBurn(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Burn, value.RawFloat);
        public void SetExplosionTypeFreeze(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Freeze, value.RawFloat);
        public void SetExplosionTypeElectric(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Electric, value.RawFloat);
        public void SetExplosionTypeVertigo(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Vertigo, value.RawFloat);
        public void SetExplosionTypeTerror(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Terror, value.RawFloat);
        public void SetExplosionTypeRadiation(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Radiation, value.RawFloat);
        public void SetExplosionTypeHacker(PEInt value) => DamageGroupExplosion.SetValue(DamageTypeEnum.Hacker, value.RawFloat);

        #endregion

        #region 接口映射(显式实现以保留字段的Unity序列化)
        List<SKVP<DamageTypeEnum, float>> IDamageData.DamageGroupDirect => DamageGroupDirect;
        List<SKVP<DamageTypeEnum, float>> IDamageData.DamageGroupExplosion => DamageGroupExplosion;
        bool IDamageData.NoSource => NoSource;
        GameObject IDamageData.ImpactVfx => ImpactVfx;
        float IDamageData.ImpactVfxSpawnOffset => ImpactVfxSpawnOffset;
        bool IDamageData.UseCollisionDirection => UseCollisionDirection;
        bool IDamageData.OnlyTerrain => OnlyTerrain;
        AudioClip IDamageData.ImpactSfx => ImpactSfx;
        bool IDamageData.UseHole => UseHole;
        GameObject IDamageData.Hole => Hole;
        // Get* 方法为 public，已隐式实现接口
        #endregion
    }


}