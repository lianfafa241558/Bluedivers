using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using Core;
using PEMaths;

using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// 创建属性的工厂，可以将属性的配置预设好，创建只要输入基础值，其他配置由工厂注入
    /// </summary>
    public static class WeaponAttributeFactory
    {

        public static readonly Dictionary<WeaponAttrType, (bool, AttrTag, ModifierType)> attributeConfigs = new()
        {
            // 通用属性
            { WeaponAttrType.Ammo, (true, AttrTag.LimitCurr | AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.Magazine, (true, AttrTag.LimitCurr | AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ShootInterval, (true, AttrTag.Reciprocal, ModifierType.All) },
            { WeaponAttrType.StartCool, (false, default, ModifierType.All) },
            
            { WeaponAttrType.BulletsPerShot, (false, default, ModifierType.All) },
            { WeaponAttrType.BulletsSpreadAngle, (false, AttrTag.FlipPlus, ModifierType.All) },
            { WeaponAttrType.BulletsOffect, (false, AttrTag.FlipPlus, ModifierType.All) },
            { WeaponAttrType.MoveSpeedToShoot, (false,AttrTag.Percentage|AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ReloadTime, (true, AttrTag.FlipPlus, ModifierType.All) },
            { WeaponAttrType.AutoReloadTime, (true, AttrTag.FlipPlus|AttrTag.UpdateToZero, ModifierType.Extra) },
            { WeaponAttrType.AutoReloadSpeed, (false, default, ModifierType.All) },
            { WeaponAttrType.HeatPerShot, (false, AttrTag.FlipPlus, ModifierType.All) },
            { WeaponAttrType.CoolDelay, (false, AttrTag.FlipPlus, ModifierType.All) },
            { WeaponAttrType.CoolSpeed, (false, default, ModifierType.All) },
            { WeaponAttrType.OverheatDuration, (false, AttrTag.FlipPlus, ModifierType.All) },
            { WeaponAttrType.LifeTime, (false, AttrTag.FlipPlus, ModifierType.All) },

            //伤害属性
            { WeaponAttrType.DirectDamage, (false, default, ModifierType.All) },
            { WeaponAttrType.ExplosionDamage, (false, default, ModifierType.All) },
            { WeaponAttrType.ExplosionRange, (false, default, ModifierType.All) },
            { WeaponAttrType.BulletSpeed, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.Factor) },
            //{ WeaponAttrType.MaxDistance, (false, default, ModifierType.All) },

            // 激光武器属性
            { WeaponAttrType.LaserWaitTime, (false, default, ModifierType.All) },

            // 蓄力武器属性
            { WeaponAttrType.ChargeLowestStage, (false, AttrTag.FlipPlus|AttrTag.OnlyInt, ModifierType.Base) },
            { WeaponAttrType.ChargeHigheststage, (false, AttrTag.OnlyInt | AttrTag.UpdateToZero, ModifierType.Base) },
            { WeaponAttrType.ChargeAmmoOnLowest, (false, AttrTag.FlipPlus | AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ChargeAmmoOnHighest, (false, AttrTag.FlipPlus|AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ChargeDuration, (true,  AttrTag.FlipPlus|AttrTag.LimitCurr | AttrTag.UpdateToZero, ModifierType.All) },
            { WeaponAttrType.ChargeDamageScale, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ChargeExplosionRangeScale, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ChargeHeatScale, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },


            // 锁定武器属性
            { WeaponAttrType.LockDistance, (false, AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.LockRange, (false, AttrTag.OnlyInt | AttrTag.OneHide, ModifierType.Factor) },
            { WeaponAttrType.LockLayers, (false, AttrTag.OnlyInt | AttrTag.UpdateToZero, ModifierType.Extra) },
            { WeaponAttrType.LockPerCount, (false, AttrTag.OnlyInt, ModifierType.Extra) },
            { WeaponAttrType.LockInterval, (true, AttrTag.Reciprocal| AttrTag.UpdateToZero, ModifierType.All) },


            // 伤害组成属性
            { WeaponAttrType.DirectDestruction, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionDestruction, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.WeaknessBonus, (false, AttrTag.Percentage | AttrTag.DefaultHide, ModifierType.Extra) },

            // 伤害类型系数（直击伤害组）
            { WeaponAttrType.DamageTypeGun, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeExplosion, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeDestruction, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeReal, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeToxicity, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeBurn, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeFreeze, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeElectric, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeVertigo, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeTerror, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeRadiation, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DamageTypeHacker, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },

            // 伤害类型系数（爆炸伤害组）
            { WeaponAttrType.ExplosionTypeGun, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeExplosion, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeDestruction, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeReal, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeToxicity, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeBurn, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeFreeze, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeElectric, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeVertigo, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeTerror, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeRadiation, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionTypeHacker, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },

        };
        public static AttrTag GetTag(WeaponAttrType type)
        {
            if (attributeConfigs.TryGetValue(type ,out var item))
            {
                return item.Item2;
            }
            //特殊机制
            return default;
        }

        public static GameAttribute Create(WeaponAttrType type, PEInt baseValue)
        {

            if (!attributeConfigs.TryGetValue(type, out var cfg))
            {
                Debug.LogError("找不到对应的配置" + type);
                return default;
            }
            if (cfg.Item1)
            {
                return new GameCurrentAttribute(baseValue, cfg.Item2, cfg.Item3);
            }
            else
            {

                return new GameAttribute(baseValue, cfg.Item2, cfg.Item3);
            }

        }

    }


    /// <summary>修饰符枚举,没继续拓展是因为现在这三种情况已经极端复杂了，甚至都没必要要额外值其他</summary>
    [Flags]
    public enum ModifierType
    {

        /// <summary>基础值</summary>
        Base = 1 << 0,
        /// <summary>倍数值</summary>
        Factor = 1 << 1,
        /// <summary>额外值</summary>
        Extra = 1 << 2,

        All = ~0,
    }

    [Flags]
    public enum AttrTag
    {
        /// <summary> 显示百分比</summary>
        [InspectorName("显示百分比")]
        Percentage = 1 << 0,
        /// <summary> 显示倒数(例如射速)</summary>
        [InspectorName("显示倒数")]
        Reciprocal = 1 << 1,
        /// <summary> 反转加成显示(减速/负面)</summary>
        [InspectorName("反转加成显示(减速/负面)")]
        FlipPlus = 1 << 2,
        /// <summary> 默认值时隐藏</summary>
        [InspectorName("默认值时隐藏")]
        DefaultHide = 1 << 3,
        /// <summary> 1时隐藏</summary>
        [InspectorName("1时隐藏")]
        OneHide = 1 << 4,
        /// <summary> 仅文本(>0时以+名称形式显示在末尾，专为Special设计)</summary>
        [InspectorName("仅文本")]
        TextOnly = 1 << 5,
        /// <summary> 始终隐藏(不在参数预览中显示)</summary>
        [InspectorName("始终隐藏")]
        IsHide = 1 << 6,

        /// <summary> (Final)必须整数</summary>
        OnlyInt = 1 << 10,
        /// <summary>当前值更新归零,正常为变为最终值</summary>
        UpdateToZero = 1 << 11,
        /// <summary>当前值限制为[0,最终值]</summary>
        LimitCurr = 1 << 12,
    }


    /// <summary>
    /// 武器的属性类型，会塞进字典的，所以只能每人一个
    /// 我在想要不要全部字典化，根据选择的射击类型自动初始化属性字典
    /// </summary>
    public enum WeaponAttrType
    {
        //---------通用属性----------
        /// <summary>后备弹量</summary>
        [InspectorName("通用/后备弹量")]
        Ammo = 0,
        /// <summary>弹匣容量</summary>
        [InspectorName("通用/弹匣容量")]
        Magazine = 1,
        /// <summary>射击间隔</summary>
        [InspectorName("通用/射击间隔")]
        ShootInterval = 2,
        /// <summary>弹射次数</summary>
        [InspectorName("通用/弹射次数")]
        Catapult = 3,
        /// <summary>初始冷却</summary>
        [InspectorName("通用/初始冷却")]
        StartCool = 4,
        /// <summary>子弹生命周期</summary>
        [InspectorName("通用/生命周期")]
        LifeTime = 5,

        /// <summary>弹丸数量</summary>
        [InspectorName("通用/弹丸数量")]
        BulletsPerShot = 6,
        /// <summary>子弹散布角度</summary>
        [InspectorName("通用/子弹散布角度")]
        BulletsSpreadAngle = 7,
        /// <summary>子弹随机偏移</summary>
        [InspectorName("通用/子弹随机偏移")]
        BulletsOffect = 8,
        /// <summary>射击时移动速度</summary>
        [InspectorName("通用/射击时移动速度")]
        MoveSpeedToShoot = 9,
        /// <summary>手动装弹时间</summary>
        [InspectorName("通用/手动装弹时间")]
        ReloadTime = 10,
        /// <summary>自动装弹延迟</summary>
        [InspectorName("通用/自动装弹延迟")]
        AutoReloadTime = 11,
        /// <summary>自动装弹速度</summary>
        [InspectorName("通用/自动装弹速度")]
        AutoReloadSpeed = 12,

        //---------散热属性----------
        /// <summary>射击热量</summary>
        [InspectorName("热量/射击热量")]
        HeatPerShot = 13,
        /// <summary>散热延迟</summary>
        [InspectorName("热量/散热延迟")]
        CoolDelay = 14,
        /// <summary>散热速度</summary>
        [InspectorName("热量/散热速度")]
        CoolSpeed = 15,
        /// <summary>过热时间</summary>
        [InspectorName("热量/过热时间")]
        OverheatDuration = 16,


        //---------伤害属性----------
        /// <summary>直击伤害</summary>
        [InspectorName("伤害/直击伤害")]
        DirectDamage = 20,
        /// <summary>范围伤害</summary>
        [InspectorName("伤害/范围伤害")]
        ExplosionDamage = 21,
        /// <summary>作用半径</summary>
        [InspectorName("伤害/作用半径")]
        ExplosionRange = 22,
        /// <summary>投射物速度</summary>
        [InspectorName("通用/投射物速度")]
        BulletSpeed = 23,

        // / <summary>最大射程</summary>
        //[InspectorName("最大射程")]
        //MaxDistance = 24,

        //---------激光武器属性----------
        /// <summary>预热时间</summary>
        [InspectorName("激光/预热时间")]
        LaserWaitTime = 40,

        //---------蓄力武器属性----------
        /// <summary>最低(能释放的)蓄力段数</summary>
        [InspectorName("蓄力/最低蓄力段数")]
        ChargeLowestStage = 50,
        /// <summary>最高蓄力段数</summary>
        [InspectorName("蓄力/最高蓄力段数")]
        ChargeHigheststage = 51,
        /// <summary>最低蓄消耗弹药</summary>
        [InspectorName("蓄力/最低蓄消耗弹药")]
        ChargeAmmoOnLowest = 52,
        /// <summary>满蓄消耗弹药</summary>
        [InspectorName("蓄力/满蓄消耗弹药")]
        ChargeAmmoOnHighest = 53,
        /// <summary>蓄力时间</summary>
        [InspectorName("蓄力/蓄力时间")]
        ChargeDuration = 54,
        /// <summary>蓄力伤害倍率</summary>
        [InspectorName("蓄力/伤害倍率")]
        ChargeDamageScale =55,
        /// <summary>蓄力作用半径倍率</summary>
        [InspectorName("蓄力/作用半径倍率")]
        ChargeExplosionRangeScale =56,
        /// <summary>蓄力热量倍率</summary>
        [InspectorName("蓄力/热量倍率")]
        ChargeHeatScale =57,

        //---------锁定武器属性----------
        /// <summary>锁定距离</summary>
        [InspectorName("锁定/锁定距离")]
        LockDistance = 60,
        /// <summary>锁定半径</summary>
        [InspectorName("锁定/锁定半径")]
        LockRange = 61,
        /// <summary>最大锁定层数</summary>
        [InspectorName("锁定/锁定最大层数")]
        LockLayers = 62,
        /// <summary>每个敌人的最大锁定层数</summary>
        [InspectorName("锁定/锁定每敌最大层数")]
        LockPerCount = 63,
        /// <summary>锁定间隔</summary>
        [InspectorName("锁定/锁定间隔")]
        LockInterval = 64,

        /// <summary>直击-护甲破坏系数</summary>
        [InspectorName("直击/护甲破坏")]
        DirectDestruction = 100,
        /// <summary>爆炸-护甲破坏系数</summary>
        [InspectorName("爆炸/护甲破坏")]
        ExplosionDestruction = 101,

        /// <summary>弱点伤害加成</summary>
        [InspectorName("伤害/弱点加成")]
        WeaknessBonus = 102,

        //---------伤害类型系数（直击伤害组）----------
        /// <summary>伤害类型-动能系数</summary>
        [InspectorName("直击/直击动能伤害")]
        DamageTypeGun = 200,
        /// <summary>伤害类型-爆炸系数</summary>
        [InspectorName("直击/直击爆炸伤害")]
        DamageTypeExplosion = 201,
        /// <summary>伤害类型-护甲破坏系数</summary>
        [InspectorName("直击/直击护甲破坏伤害")]
        DamageTypeDestruction = 202,
        /// <summary>伤害类型-真实系数</summary>
        [InspectorName("直击/直击真实伤害")]
        DamageTypeReal = 203,
        /// <summary>伤害类型-毒系数</summary>
        [InspectorName("直击/直击毒伤害")]
        DamageTypeToxicity = 204,
        /// <summary>伤害类型-燃烧系数</summary>
        [InspectorName("直击/直击燃烧伤害")]
        DamageTypeBurn = 205,
        /// <summary>伤害类型-冰冻系数</summary>
        [InspectorName("直击/直击冰冻伤害")]
        DamageTypeFreeze = 206,
        /// <summary>伤害类型-电击系数</summary>
        [InspectorName("直击/直击电击伤害")]
        DamageTypeElectric = 207,
        /// <summary>伤害类型-眩晕系数</summary>
        [InspectorName("直击/直击眩晕伤害")]
        DamageTypeVertigo = 208,
        /// <summary>伤害类型-恐惧系数</summary>
        [InspectorName("直击/直击恐惧伤害")]
        DamageTypeTerror = 209,
        /// <summary>伤害类型-辐射系数</summary>
        [InspectorName("直击/直击辐射伤害")]
        DamageTypeRadiation = 210,
        /// <summary>伤害类型-骇入系数</summary>
        [InspectorName("直击/直击骇入伤害")]
        DamageTypeHacker = 211,

        //---------伤害类型系数（爆炸伤害组）----------
        /// <summary>伤害类型-动能系数(爆炸)</summary>
        [InspectorName("爆炸/范围动能伤害")]
        ExplosionTypeGun = 220,
        /// <summary>伤害类型-爆炸系数(爆炸)</summary>
        [InspectorName("爆炸/范围爆炸伤害")]
        ExplosionTypeExplosion = 221,
        /// <summary>伤害类型-护甲破坏系数(爆炸)</summary>
        [InspectorName("爆炸/范围护甲破坏伤害")]
        ExplosionTypeDestruction = 222,
        /// <summary>伤害类型-真实系数(爆炸)</summary>
        [InspectorName("爆炸/范围真实伤害")]
        ExplosionTypeReal = 223,
        /// <summary>伤害类型-毒系数(爆炸)</summary>
        [InspectorName("爆炸/范围毒伤害")]
        ExplosionTypeToxicity = 224,
        /// <summary>伤害类型-燃烧系数(爆炸)</summary>
        [InspectorName("爆炸/范围燃烧伤害")]
        ExplosionTypeBurn = 225,
        /// <summary>伤害类型-冰冻系数(爆炸)</summary>
        [InspectorName("爆炸/范围冰冻伤害")]
        ExplosionTypeFreeze = 226,
        /// <summary>伤害类型-电击系数(爆炸)</summary>
        [InspectorName("爆炸/范围电击伤害")]
        ExplosionTypeElectric = 227,
        /// <summary>伤害类型-眩晕系数(爆炸)</summary>
        [InspectorName("爆炸/范围眩晕伤害")]
        ExplosionTypeVertigo = 228,
        /// <summary>伤害类型-恐惧系数(爆炸)</summary>
        [InspectorName("爆炸/范围恐惧伤害")]
        ExplosionTypeTerror = 229,
        /// <summary>伤害类型-辐射系数(爆炸)</summary>
        [InspectorName("爆炸/范围辐射伤害")]
        ExplosionTypeRadiation = 230,
        /// <summary>伤害类型-骇入系数(爆炸)</summary>
        [InspectorName("爆炸/范围骇入伤害")]
        ExplosionTypeHacker = 231,

        /// <summary>特殊机制</summary>
        [InspectorName("特殊机制")]
        Special = 999,
    }

    public class GameAttribute
    {
        public event Action<PEInt> OnFinalValueChange;

        protected AttrTag tag;
        protected ModifierType allowModifier;

        /// <summary>原始值，不能被修改</summary>
        private readonly PEInt primeValue;
        /// <summary>基础值，可以被加成修改</summary>
        private PEInt baseValue;
        /// <summary>倍数值，可以被加成修改</summary>
        private PEInt factorValue;
        /// <summary>额外值，可以被加成修改</summary>
        private PEInt extraValue;
        /// <summary>最终值，不能被加成修改</summary>
        private PEInt finalValue;

        public GameAttribute(PEInt primeValue, AttrTag tag, ModifierType allowModifier)
        {
            this.primeValue = baseValue = primeValue;
            factorValue = 1;
            extraValue = 0;

            this.tag = tag;
            this.allowModifier = allowModifier;

            Recalculate();
           
        }

        public PEInt PrimeValue => primeValue;

        public PEInt FinalValue
        {
            get =>  HasFlag(AttrTag.OnlyInt)? PEMath.Floor(finalValue) : finalValue;
            private set
            {
                var oldValue = finalValue;
                finalValue = value;
                OnFinalValueChange?.Invoke(value);
            }
        }

        //没继续拓展是因为现在这三种情况已经极端复杂了，绝对够用了
        public void AddModifier(ModifierType modifier, PEInt value)
        {
            if (!allowModifier.HasFlag(modifier))
            {
                Debug.LogError($"不支持的Modifier类型:{modifier}     {this}");
                return;
            }
            switch (modifier)
            {
                case ModifierType.Base:
                    baseValue += value;
                    break;
                case ModifierType.Factor:
                    factorValue += value;
                    break;
                case ModifierType.Extra:
                    extraValue += value;
                    break;
            }
            Recalculate();

        }

        protected virtual void Recalculate()
        {
            //var oldValue = FinalValue;
            var re = baseValue * factorValue + extraValue;
            FinalValue = re;
        }


        public bool HasFlag(AttrTag flag) => tag.HasFlag(flag);


        // 重写隐式转换运算符
        public static implicit operator bool(GameAttribute obj)
        {
            return obj != null;
        }
    }


    public class GameCurrentAttribute: GameAttribute
    {

        public event Action<PEInt> OnCurrValueChange;
        public event Action<PEInt> OnStageValueChange;


        /// <summary>属性的当前值,可以直接修改</summary>
        private PEInt currValue;

        public GameCurrentAttribute(PEInt primeValue, AttrTag tag, ModifierType allowModifier):base(primeValue, tag, allowModifier)
        {
        }

        public PEInt CurrValue
        {
            get => currValue;
            set
            {
                var oldValue = currValue;
                currValue = value;
                if(tag.HasFlag(AttrTag.LimitCurr))currValue = PEMath.Clamp(value, 0, FinalValue);
                OnCurrValueChange?.Invoke(value);
                if (PEMath.Floor(oldValue) != PEMath.Floor(value))
                {
                    OnStageValueChange?.Invoke(value);
                }
            }
        }
      
        /// <summary>属性的当前值与最终值的比例[0,1] (连续值)</summary>
        public PEInt ScaleValue
        {
            get
            {
                if (FinalValue == 0) return 0;
                return CurrValue / FinalValue;
            }
        }

        /// <summary>段数百分比[0-1] (离散值)</summary>
        public PEInt StageScale
        {
            get
            {
                if (FinalValue == 0) return 0;

                // 1. 先算连续百分比
                var progress = ScaleValue;

                // 2. 总段数 = 向上取整(final)
                var totalSegments = PEMath.Ceil(FinalValue);

                return PEMath.Floor(progress * totalSegments) / totalSegments;
            }
        }

        protected override void Recalculate()
        {
            base.Recalculate();
            CurrValue = tag.HasFlag(AttrTag.UpdateToZero) ? 0 : FinalValue;
        }

    }

}