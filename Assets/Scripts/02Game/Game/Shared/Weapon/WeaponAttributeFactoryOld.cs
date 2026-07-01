/*
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using PEMaths;

using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// 创建属性的工厂，可以将属性的配置预设好，创建只要输入基础值，其他配置由工厂注入
    /// </summary>
    public static class WeaponAttributeFactory
    {

        public static readonly Dictionary<WeaponAttrType, (Type, AttrTag, ModifierType)> attributeConfigs = new()
        {
            // 通用属性
            { WeaponAttrType.Ammo, (typeof(WeaponCurrentAttribute), AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.Magazine, (typeof(WeaponCurrentAttribute), AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ShootInterval, (typeof(WeaponCurrentAttribute), AttrTag.Reciprocal, ModifierType.All) },
            { WeaponAttrType.BulletsPerShot, (typeof(WeaponAttribute), AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.BulletsSpreadAngle, (typeof(WeaponAttribute), AttrTag.None, ModifierType.All) },
            { WeaponAttrType.ReloadTime, (typeof(WeaponCurrentAttribute), AttrTag.None, ModifierType.All) },
            { WeaponAttrType.AutoReloadTime, (typeof(WeaponCurrentAttribute), AttrTag.None, ModifierType.Extra) },
            { WeaponAttrType.AutoReloadSpeed, (typeof(WeaponAttribute), AttrTag.None, ModifierType.Extra) },

            // 蓄力武器属性
            { WeaponAttrType.LaserWaitTime, (typeof(WeaponCurrentAttribute), AttrTag.None, ModifierType.All) },

            // 激光武器属性
            { WeaponAttrType.ChargeLowestStage, (typeof(WeaponAttribute), AttrTag.OnlyInt, ModifierType.Extra) },
            { WeaponAttrType.ChargeHigheststage, (typeof(WeaponStageAttribute), AttrTag.OnlyInt, ModifierType.Extra) },
            { WeaponAttrType.ChargeAmmoOnLowest, (typeof(WeaponAttribute), AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ChargeAmmoOnHighest, (typeof(WeaponAttribute), AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.ChargeDuration, (typeof(WeaponCurrentAttribute), AttrTag.None, ModifierType.All) },

            // 锁定武器属性
            { WeaponAttrType.LockDistance, (typeof(WeaponAttribute), AttrTag.OnlyInt, ModifierType.All) },
            { WeaponAttrType.LockRange, (typeof(WeaponAttribute), AttrTag.OnlyInt| AttrTag.Percentage|AttrTag.DefaultHide, ModifierType.Factor) },
            { WeaponAttrType.LockLayers, (typeof(WeaponCurrentAttribute), AttrTag.OnlyInt, ModifierType.Extra) },
            { WeaponAttrType.LockPerCount, (typeof(WeaponAttribute), AttrTag.OnlyInt, ModifierType.Extra) },
            { WeaponAttrType.LockInterval, (typeof(WeaponCurrentAttribute), AttrTag.Percentage| AttrTag.DefaultHide, ModifierType.All) }
        };

    // 缓存构造函数委托
    private static readonly ConcurrentDictionary<Type, Func<PEInt, AttrTag, ModifierType, WeaponAttribute>> attributeCtorCache = new();

        public static T Create<T>(WeaponAttrType type, PEInt baseValue) where T : WeaponAttribute
        {
            if (!attributeConfigs.TryGetValue(type, out var cfg))
            {
                Debug.LogError("找不到对应的配置" + type);
                return default;
            }

            // 1. 获取或编译AttributeBase 子类的构造函数委托
            var attributeCtor = attributeCtorCache.GetOrAdd(typeof(T), RegisterCtor);

            // 调用缓存的构造函数委托
            return (T)attributeCtor(baseValue, cfg.Item2, cfg.Item3);
        }

        public static WeaponAttribute Create(WeaponAttrType type, PEInt baseValue)
        {

            if (!attributeConfigs.TryGetValue(type, out var cfg))
            {
                Debug.LogError("找不到对应的配置" + type);
                return default;
            }

            // 获取或编译AttributeBase 子类的构造函数委托
            var attributeCtor = attributeCtorCache.GetOrAdd(cfg.Item1, RegisterCtor);

            // 调用缓存的构造函数委托
            return attributeCtor(baseValue, cfg.Item2, cfg.Item3);
        }

        public static Func<PEInt, AttrTag, ModifierType, WeaponAttribute> RegisterCtor(Type type)
        {
            var ctor = type.GetConstructor(new Type[]
                {
                    typeof(PEInt),
                    typeof(AttrTag),
                    typeof(ModifierType),
                });

            if (ctor == null)
            {
                Debug.LogError($"未能获取到构造函数{type.Name}");
            }

            // 编译表达式树生成委托
            var paramBase = Expression.Parameter(typeof(PEInt));
            var paramTag = Expression.Parameter(typeof(AttrTag));
            var paramModifiers = Expression.Parameter(typeof(ModifierType));
            var newExpr = Expression.New(ctor, paramBase, paramTag, paramModifiers);
            var lambda = Expression.Lambda<Func<PEInt, AttrTag, ModifierType, WeaponAttribute>>(
                newExpr,
                paramBase,
                paramTag,
                paramModifiers
            );
            return lambda.Compile();

        }

    }


    /// <summary>修饰符枚举，没继续拓展是因为现在这三种情况已经极端复杂了，甚至都没必要要额外值其他</summary>
    [Flags]
    public enum ModifierType
    {
        /// <summary> 无</summary>
        None = 0,
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
        /// <summary> 无</summary>
        None = 0,
        /// <summary> 显示百分比</summary>
        Percentage = 1 << 0,
        /// <summary> 显示倒数(例如射速)</summary>
        Reciprocal = 1 << 1,
        /// <summary> 反转加成显示(加→减)</summary>
        FlipPlus = 1 << 2,
        /// <summary> 默认值时隐藏</summary>
        DefaultHide = 1 << 3,
        /// <summary> (Final)必须整数</summary>
        OnlyInt = 1 << 4,
    }


    /// <summary>
    /// 武器的属性类型，会塞进字典的，所以只能每人一个
    /// 我在想要不要全部字典化，根据选择的射击类型自动初始化属性字典
    /// </summary>
    public enum WeaponAttrType
    {
        //---------通用属性----------
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

        /// <summary>弹丸数量</summary>
        [InspectorName("弹丸数量")]
        BulletsPerShot = 6,
        /// <summary>子弹散布角度</summary>
        [InspectorName("子弹散布角度")]
        BulletsSpreadAngle = 7,

        /// <summary>手动装弹时间</summary>
        [InspectorName("手动装弹时间")]
        ReloadTime = 10,
        /// <summary>自动装弹延迟</summary>
        [InspectorName("自动装弹延迟")]
        AutoReloadTime = 11,
        /// <summary>自动装弹速度</summary>
        [InspectorName("自动装弹速度")]
        AutoReloadSpeed = 12,

        //---------激光武器属性----------
        /// <summary>激光预热时间</summary>
        [InspectorName("激光预热时间")]
        LaserWaitTime = 40,

        //---------蓄力武器属性----------
        /// <summary>最低（能释放的）蓄力段数</summary>
        [InspectorName("最低（能释放的）蓄力段数")]
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

        //---------锁定武器属性----------
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
    }

    public class WeaponAttribute
    {
        public event Action<PEInt, PEInt> OnFinalValueChange;

        private AttrTag tag;
        private ModifierType allowModifier;

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
            this.primeValue = primeValue;
            factorValue = 1;
            extraValue = 0;

            this.tag = tag;
            this.allowModifier = allowModifier;

            Recalculate();
        }

        public PEInt PrimeValue => primeValue;

        public PEInt FinalValue
        {
            get => finalValue;
            private set
            {
                var oldValue = finalValue;
                finalValue = value;
                OnFinalValueChange?.Invoke(oldValue, value);
            }
        }


 
        //没继续拓展是因为现在这三种情况已经极端复杂了，绝对够用了的
        public void AddModifier(ModifierType modifier, PEInt value)
        {
            if (!allowModifier.HasFlag(modifier))
            {
                Debug.LogError($"不支持的Modifier类型:{modifier}" + this);
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
        private void Recalculate()
        {
            //var oldValue = FinalValue;
            var re = baseValue * factorValue + extraValue;
            if (tag.HasFlag(AttrTag.OnlyInt)) re = PEMath.Floor(re);
            FinalValue = re;
            //OnRecalculate(oldValue);
        }

    }

    /// <summary>
    /// 带有当前值的属性(子弹量等)
    /// </summary>
    public class WeaponCurrentAttribute : WeaponAttribute
    {
        public event Action<PEInt> OnCurrValueChange;


        private PEInt currValue;

        /// <summary>属性的当前值</summary>
        public PEInt CurrValue
        {
            get => currValue;
            set
            {
                currValue = value;
                OnCurrValueChange?.Invoke(value);
            }
        }
        /// <summary>属性的当前值与最终值的比值[0,1] (连续值)</summary>
        public PEInt ScaleValue
        {
            get
            {
                if (FinalValue == 0) return 1;
                return CurrValue / FinalValue;
            }
        }


        public WeaponCurrentAttribute(PEInt baseValue, AttrTag tag, ModifierType allowModifier) : base(baseValue, tag, allowModifier)
        {
            ResetCurrValue();
        }

        public void ResetCurrValue()
        {
            CurrValue = FinalValue;
        }

    }

    /// <summary>
    /// 带有段数值的属性(蓄力段数)
    /// 阶段数直接就是CurrentValue
    /// </summary>
    public class WeaponStageAttribute : WeaponAttribute
    {
        public event Action<PEInt> OnCurrValueChange;
        public event Action<PEInt> OnStageValueChange;


        private PEInt currValue;

        /// <summary>段数值[0,Final](连续值)</summary>
        public PEInt CurrValue
        {
            get => currValue;
            set
            {
                var oldValue = currValue;
                currValue = value;
                OnCurrValueChange?.Invoke(value);
                if (PEMath.Floor(oldValue) != PEMath.Floor(value))
                {
                    OnStageValueChange?.Invoke(value);
                }
            }
        }
        /// <summary>当前阶段  [0,Final](离散值)</summary>
        public PEInt StageValue => PEMath.Floor(CurrValue);


        /// <summary>段数百分比[0,1] (连续值)</summary>
        public PEInt CurrScale
        {
            get
            {
                if (FinalValue == 0) return 1;
                return CurrValue / FinalValue;
            }
        }

        /// <summary>段数百分比[0-1] (离散值)</summary>
        public PEInt StageScale
        {
            get
            {
                if (FinalValue == 0) return 1;
                return StageValue / FinalValue;
            }
        }


        public WeaponStageAttribute(PEInt baseValue, AttrTag tag, ModifierType allowModifier) : base(baseValue, tag, allowModifier)
        {
            ResetCurrValue();
        }
        public void ResetCurrValue()
        {
            CurrValue = 0;
        }

    }
    
}*/