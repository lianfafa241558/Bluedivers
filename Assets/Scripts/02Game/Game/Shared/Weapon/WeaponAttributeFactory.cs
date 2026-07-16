using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using PEMaths;
using Unity.BaseTool;
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

            //伤害属性
            { WeaponAttrType.DirectDamage, (false, default, ModifierType.All) },
            { WeaponAttrType.ExplosionDamage, (false, default, ModifierType.All) },
            { WeaponAttrType.ExplosionRange, (false, default, ModifierType.All) },
            { WeaponAttrType.BulletSpeed, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            //{ WeaponAttrType.MaxDistance, (false, default, ModifierType.All) },

            // 激光武器属性
            { WeaponAttrType.LaserWaitTime, (false, default, ModifierType.All) },

            // 蓄力武器属性
            { WeaponAttrType.ChargeLowestStage, (false, AttrTag.OnlyInt, ModifierType.Extra) },
            { WeaponAttrType.ChargeHigheststage, (false, AttrTag.OnlyInt | AttrTag.UpdateToZero, ModifierType.All) },
            { WeaponAttrType.ChargeAmmoOnLowest, (false, AttrTag.FlipPlus | AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ChargeAmmoOnHighest, (false, AttrTag.FlipPlus|AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ChargeDuration, (true,  AttrTag.FlipPlus|AttrTag.LimitCurr | AttrTag.UpdateToZero, ModifierType.All) },
            { WeaponAttrType.ChargeDamageScale, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ChargeExplosionRangeScale, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },


            // 锁定武器属性
            { WeaponAttrType.LockDistance, (false, AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.LockRange, (false, AttrTag.OnlyInt | AttrTag.Percentage | AttrTag.OneHide, ModifierType.Factor) },
            { WeaponAttrType.LockLayers, (false, AttrTag.OnlyInt | AttrTag.UpdateToZero, ModifierType.Extra) },
            { WeaponAttrType.LockPerCount, (false, AttrTag.OnlyInt, ModifierType.Extra) },
            { WeaponAttrType.LockInterval, (true, AttrTag.Percentage| AttrTag.UpdateToZero| AttrTag.FlipPlus, ModifierType.All) },


            // 伤害组成属性
            { WeaponAttrType.DirectDestruction, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.ExplosionDestruction, (false, AttrTag.Percentage | AttrTag.OneHide, ModifierType.All) },
            { WeaponAttrType.DirectWeakness, (false, AttrTag.Percentage | AttrTag.DefaultHide, ModifierType.All) },
            { WeaponAttrType.ExplosionWeakness, (false, AttrTag.Percentage | AttrTag.DefaultHide, ModifierType.All) },

        };
        public static AttrTag GetTag(WeaponAttrType type) => attributeConfigs[type].Item2;

        public static WeaponAttribute Create(WeaponAttrType type, PEInt baseValue)
        {

            if (!attributeConfigs.TryGetValue(type, out var cfg))
            {
                Debug.LogError("找不到对应的配置" + type);
                return default;
            }
            if (cfg.Item1)
            {
                return new WeaponCurrentAttribute(baseValue, cfg.Item2, cfg.Item3);
            }
            else
            {

                return new WeaponAttribute(baseValue, cfg.Item2, cfg.Item3);
            }

        }

    }


    /// <summary>修饰符枚举,没继续拓展是因为现在这三种情况已经极端复杂了，甚至都没必要要额外值其实</summary>
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
        /// <summary> 显示百分数</summary>
        [InspectorName("显示百分数")]
        Percentage = 1 << 0,
        /// <summary> 显示倒数(例如射速)</summary>
        [InspectorName("显示倒数")]
        Reciprocal = 1 << 1,
        /// <summary> 反转加成显示(绿/红)</summary>
        [InspectorName("反转加成显示(绿-红)")]
        FlipPlus = 1 << 2,
        /// <summary> 默认值时隐藏</summary>
        [InspectorName("默认值时隐藏")]
        DefaultHide = 1 << 3,
        /// <summary> 1时隐藏</summary>
        [InspectorName("1时隐藏")]
        OneHide = 1 << 4,


        /// <summary> (Final)必须整数</summary>
        OnlyInt = 1 << 10,
        /// <summary>当前值更新归零(正常为变为最终值)</summary>
        UpdateToZero = 1 << 11,
        /// <summary>当前值限制为[0,最终值]</summary>
        LimitCurr = 1 << 12,
    }


    /// <summary>
    /// 武器的属性类型，会塞进字典的，所以只能每人一份
    /// 我在想要不要全部字典化，根据选择的射击类型自动初始化属性字典
    /// </summary>
    public enum WeaponAttrType
    {
        //---------通用属性-----------
        /// <summary>后备弹量</summary>
        [InspectorName("后备弹量")]
        Ammo = 0,
        /// <summary>弹匣容量</summary>
        [InspectorName("弹匣容量")]
        Magazine = 1,
        /// <summary>射击间隔</summary>
        [InspectorName("射击间隔")]
        ShootInterval = 2,
        /// <summary>弹射次数</summary>
        [InspectorName("弹射次数")]
        Catapult = 3,
        /// <summary>初始冷却</summary>
        [InspectorName("初始冷却")]
        StartCool = 4,

        /// <summary>弹丸数量</summary>
        [InspectorName("弹丸数量")]
        BulletsPerShot = 6,
        /// <summary>子弹散布角度</summary>
        [InspectorName("子弹散布角度")]
        BulletsSpreadAngle = 7,
        /// <summary>子弹随机偏移</summary>
        [InspectorName("子弹随机偏移")]
        BulletsOffect = 8,
        /// <summary>射击时移动速度</summary>
        [InspectorName("射击时移动速度")]
        MoveSpeedToShoot = 9,
        /// <summary>手动装弹时间</summary>
        [InspectorName("手动装弹时间")]
        ReloadTime = 10,
        /// <summary>自动装弹延迟</summary>
        [InspectorName("自动装弹延迟")]
        AutoReloadTime = 11,
        /// <summary>自动装弹速度</summary>
        [InspectorName("自动装弹速度")]
        AutoReloadSpeed = 12,


        //---------伤害属性-----------
        /// <summary>直击伤害</summary>
        [InspectorName("直击伤害")]
        DirectDamage = 20,
        /// <summary>范围伤害</summary>
        [InspectorName("范围伤害")]
        ExplosionDamage = 21,
        /// <summary>作用半径</summary>
        [InspectorName("作用半径")]
        ExplosionRange = 22,
        /// <summary>投射物速度</summary>
        [InspectorName("投射物速度")]
        BulletSpeed = 23,

        // / <summary>最大射程</summary>
        //[InspectorName("最大射程")]
        //MaxDistance = 24,

        //---------激光武器属性-----------
        /// <summary>预热时间</summary>
        [InspectorName("预热时间")]
        LaserWaitTime = 40,

        //---------蓄力武器属性-----------
        /// <summary>最低(能释放的)蓄力段数</summary>
        [InspectorName("最低(能释放的)蓄力段数")]
        ChargeLowestStage = 50,
        /// <summary>最高蓄力段数</summary>
        [InspectorName("最高蓄力段数")]
        ChargeHigheststage = 51,
        /// <summary>最低蓄消耗弹药</summary>
        [InspectorName("最低蓄消耗弹药")]
        ChargeAmmoOnLowest = 52,
        /// <summary>满蓄消耗弹药</summary>
        [InspectorName("满蓄消耗弹药")]
        ChargeAmmoOnHighest = 53,
        /// <summary>蓄力时间</summary>
        [InspectorName("蓄力时间")]
        ChargeDuration = 54,
        /// <summary>蓄力伤害倍率</summary>
        [InspectorName("蓄力伤害倍率")]
        ChargeDamageScale =55,
        /// <summary>蓄力作用半径倍率</summary>
        [InspectorName("蓄力作用半径倍率")]
        ChargeExplosionRangeScale =56,

        //---------锁定武器属性-----------
        /// <summary>锁定距离</summary>
        [InspectorName("锁定距离")]
        LockDistance = 60,
        /// <summary>锁定半径</summary>
        [InspectorName("锁定半径")]
        LockRange = 61,
        /// <summary>最大锁定层数</summary>
        [InspectorName("最大锁定层数")]
        LockLayers = 62,
        /// <summary>每个敌人的最大锁定层数</summary>
        [InspectorName("每个敌人的最大锁定层数")]
        LockPerCount = 63,
        /// <summary>锁定间隔</summary>
        [InspectorName("锁定间隔")]
        LockInterval = 64,

        /// <summary>直击-护甲破坏系数</summary>
        [InspectorName("直击护甲破坏")]
        DirectDestruction = 100,
        /// <summary>爆炸-护甲破坏系数</summary>
        [InspectorName("范围护甲破坏")]
        ExplosionDestruction = 101,

        /// <summary>弱点伤害加成</summary>
        [InspectorName("直击弱点伤害加成")]
        DirectWeakness = 102,
        /// <summary>弱点伤害加成</summary>
        [InspectorName("范围弱点伤害加成")]
        ExplosionWeakness = 103,

        /// <summary>特殊机制</summary>
        [InspectorName("特殊机制")]
        Special = 999,
    }

    public class WeaponAttribute
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

        public WeaponAttribute(PEInt primeValue, AttrTag tag, ModifierType allowModifier)
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

        //没继续拓展是因为现在这三种情况已经极端复杂了，绝对够用了。
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
        public static implicit operator bool(WeaponAttribute obj)
        {
            return obj != null;
        }
    }


    public class WeaponCurrentAttribute: WeaponAttribute
    {

        public event Action<PEInt> OnCurrValueChange;
        public event Action<PEInt> OnStageValueChange;


        /// <summary>属性的当前值,可以直接修改</summary>
        private PEInt currValue;

        public WeaponCurrentAttribute(PEInt primeValue, AttrTag tag, ModifierType allowModifier):base(primeValue, tag, allowModifier)
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
      
        /// <summary>属性的当前值与最终值的比值 [0,1] (连续的)</summary>
        public PEInt ScaleValue
        {
            get
            {
                if (FinalValue == 0) return 0;
                return CurrValue / FinalValue;
            }
        }

        /// <summary>段数百分比 [0-1] (离散的)</summary>
        public PEInt StageScale
        {
            get
            {
                if (FinalValue == 0) return 0;
                return currValue.RawInt / FinalValue;
            }
        }

        protected override void Recalculate()
        {
            base.Recalculate();
            CurrValue = tag.HasFlag(AttrTag.UpdateToZero) ? 0 : FinalValue;
        }

    }

}