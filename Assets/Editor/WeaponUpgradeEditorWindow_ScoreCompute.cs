using System;
using System.Collections.Generic;
using System.Linq;

using FPSGame.Game;
using UnityEditorInternal;

using UnityEditor;
using UnityEngine;
using FPSGame.EditorExt;
using FPSGame.GameData;
using FPSGame.Weapon;
using FPSGame.Core;
using FPSGame.Gameplay;

namespace FPSGame.Game.Editor
{
    /// <summary>
    /// 评分计算：BuildScoreReport 与各类取值/曲线工具（口径改动看这里）。
    /// </summary>
    public partial class WeaponUpgradeEditorWindow
    {
        /// <summary>反重用的"重型目标"护甲等级：与 Damageable 的 armorLevel 同单位（普通杂兵 0~3、重型 4~5）。</summary>
        private const float HeavyArmorLevel = 4f;

        /// <summary>穿甲不足的减伤步长，与 Damageable.ArmorPenaltyPerLevel 必须一致。</summary>
        private const float ArmorPenaltyPerLevel = 0.4f;

        /// <summary>
        /// 目标护甲对伤害的折减系数：<c>clamp01(1 - (护甲等级 - AP) × 0.4)</c>。
        /// <para>⚠ AP 超出护甲不回补伤害（上限 1），AP 相差 3 级及以上直接归零（完全打不动）。</para>
        /// <para>清杂（护甲 1）与反重（护甲 4）共用这一条公式，与 <c>Damageable.InflictDamage</c> 同口径。</para>
        /// </summary>
        private static float ArmorFactor(float armorLevel, float ap)
            => Mathf.Clamp01(1f - (armorLevel - Mathf.RoundToInt(ap)) * ArmorPenaltyPerLevel);

        /// <summary>
        /// 一次扣扳机能命中几个目标（估算）。
        /// <para>· 光束把「作用范围」当命中半径、普通爆炸用爆炸半径 ⇒ 都按面积估（每个目标按 2m×2m 站位 = 4 m²）；</para>
        /// <para>· 「穿透单位」至少算 2 个目标（穿过第一个继续往后）；光束 + 穿透上限放宽到 8。</para>
        /// <para>⚠ 只喂清杂：DPS / 总伤 / 单发 / 反重一律按单体，别拿这个系数去乘它们。</para>
        /// </summary>
        private static float MultiTargetCount(bool beamSplash, bool hasExplosion, float radius, bool penetratesUnits)
        {
            var factor = 1f;
            // 光束把"作用范围"当命中半径；普通爆炸弹用爆炸半径圈面积（每个杂兵按 2m×2m = 4㎡ 站位）
            // ⇒「连锁爆弹」这种 +4m 半径的模组才能真正体现在清杂上，而不是只加一个"范围伤害"单项分
            if ((beamSplash || hasExplosion) && radius > 0f)
            {
                var r = Mathf.Max(0.1f, radius);
                var cap = beamSplash && penetratesUnits ? 8f : 6f;
                factor = Mathf.Clamp(1f + Mathf.PI * r * r / 4f, 1f, cap);
            }
            if (penetratesUnits) factor = Mathf.Max(factor, 2f);
            return factor;
        }

        /// <summary>
        /// 是否"穿透单位"：① 武器上的 <c>BulletFlag</c> 标旗；② <c>m_UniqueAttr</c> 里注册的「穿透单位」
        /// （运行时由 WeaponUniqueAttributeApplier 按 value&gt;0 写回 BulletFlag，prefab 上那个字段仍是 0）。
        /// </summary>
        private static bool HasUnitPenetration(SerializedObject serialized)
        {
            var flagProp = serialized?.FindProperty("BulletFlag");
            if (flagProp != null && (flagProp.intValue & (int)BulletFlag.PenetrateUnits) != 0) return true;
            return HasUniqueAttr(serialized, "穿透单位");
        }

        /// <summary>武器 prefab 的专有属性里是否存在该名字且 value &gt; 0（= 运行时真的会生效）。</summary>
        private static bool HasUniqueAttr(SerializedObject serialized, string name)
        {
            var unique = serialized?.FindProperty("m_UniqueAttr");
            if (unique == null) return false;
            for (var i = 0; i < unique.arraySize; i++)
            {
                var item = unique.GetArrayElementAtIndex(i);
                var nameProp = item.FindPropertyRelative("name");
                if (nameProp == null || nameProp.stringValue != name) continue;
                var valueProp = item.FindPropertyRelative("value");
                return valueProp == null || valueProp.floatValue > 0f;   // value<=0 = 运行时把标旗清掉
            }
            return false;
        }

        /// <summary>把指标压到 0~100：锚点成对 [数值, 分]，logScale 时段内按 log10 插值。</summary>
        private static float ScoreCurve(float value, float[] anchors, bool logScale)
        {
            if (value <= anchors[0]) return anchors[1];

            var last = anchors.Length - 2;
            if (value >= anchors[last]) return anchors[last + 1];

            for (var i = 0; i < last; i += 2)
            {
                var hi = anchors[i + 2];
                if (value > hi) continue;

                var lo = anchors[i];
                var t = logScale
                    ? (Mathf.Log10(value) - Mathf.Log10(lo)) / (Mathf.Log10(hi) - Mathf.Log10(lo))
                    : (value - lo) / (hi - lo);
                return Mathf.Lerp(anchors[i + 1], anchors[i + 3], Mathf.Clamp01(t));
            }

            return anchors[last + 1];
        }

        /// <summary>
        /// 伤害类属性的取值口径：cfg 里有就用 cfg，否则退回 DamageData 原值。
        /// <para>⚠ 两类都得看：DirectDamage / BulletSpeed 只在 DamageData；而 WeaknessBonus、
        /// 部分武器手填的 ExplosionRange/AP 是直接配在 cfg 里的（运行时 InitAttribute 才回写到 DamageData）。</para>
        /// </summary>
        private static float AttrOrData(WeaponPlayerController weapon, DamageData data, WeaponAttrType attr)
        {
            var cfgAttr = weapon.cfg[attr];
            return cfgAttr != null ? cfgAttr.FinalValue.RawFloat : data.GetAttr(attr);
        }

        /// <summary>当前生效的伤害配置元素（`Damages[UseDamageIndex]`）；取不到返回 null。</summary>
        private static SerializedProperty FindDamageElement(SerializedObject serialized, WeaponPlayerController weapon)
        {
            if (serialized == null) return null;
            var damagesProp = serialized.FindProperty("Damages");
            if (damagesProp == null || damagesProp.arraySize == 0) return null;
            return damagesProp.GetArrayElementAtIndex(Mathf.Clamp(weapon.UseDamageIndex, 0, damagesProp.arraySize - 1));
        }

        /// <summary>
        /// DamageData.UseCharge（私有序列化字段）决定 ChargeDamageScale / ChargeAOERangeScale 生不生效。
        /// <para>⚠ 别看 <c>ShootType == Charge</c> 代替：蓄力射击类型 ≠ 伤害配置开了蓄力倍率（测试武器就是反例）。</para>
        /// </summary>
        private static bool ReadUseCharge(SerializedObject serialized, WeaponPlayerController weapon)
        {
            var element = FindDamageElement(serialized, weapon);
            if (element == null) return false;
            var useCharge = element.FindPropertyRelative("UseCharge");
            return useCharge != null && useCharge.boolValue;
        }

        /// <summary>最低可发射的蓄力比例 = 最低段数 ÷ 最高段数（最高段为 0 视为只有一档 = 1）。</summary>
        private static float GetMinChargeRatioCore(WeaponPlayerController weapon)
        {
            var lowest = weapon.AttrFinal(WeaponAttrType.ChargeLowestStage).RawFloat;
            var highest = weapon.AttrFinal(WeaponAttrType.ChargeHigheststage).RawFloat;
            return highest > 0f ? Mathf.Clamp01(lowest / highest) : 1f;
        }

        /// <summary>
        /// 带改装的"最低可发射蓄力比例"。
        /// <para>⚠ 必须在改装 cfg 下读：`ChargeLowestStage` 正是"降低 N 层最低蓄力"这类升级项改的属性，
        /// 直接读原 cfg ⇒ 选完"蓄 1 层"评分一点变化都没有（因为最低档那一份 report 根本没变）。</para>
        /// </summary>
        private float GetMinChargeRatio(WeaponPlayerController weapon, SerializedObject serialized, LoadoutMods mods)
        {
            if (mods == null || mods.IsEmpty || weapon == null) return GetMinChargeRatioCore(weapon);

            var original = weapon.cfg;
            try
            {
                weapon.cfg = BuildLoadoutCfg(weapon, serialized, mods);
                return GetMinChargeRatioCore(weapon);
            }
            finally
            {
                weapon.cfg = original;
            }
        }

        /// <summary>
        /// 评分入口：<paramref name="mods"/> 非空时先把改装加成叠到一份 cfg 副本上再算（见 <c>_Loadout.cs</c>）。
        /// <para>▍为什么换 cfg 而不是改逐个读数：属性读取散在 <c>AttrFinal</c> / <c>AttrOrData</c> 三十多处，
        /// 换掉 <c>weapon.cfg</c> 这一处等于所有读数自动吃改装；副本用完立刻还原，资产一个字节都不动。</para>
        /// </summary>
        private WeaponScoreReport BuildScoreReport(WeaponPlayerController weapon, SerializedObject serialized,
            float chargeRatio = 1f, LoadoutMods mods = null)
        {
            if (mods == null || mods.IsEmpty) return BuildScoreReportCore(weapon, serialized, chargeRatio);

            var original = weapon.cfg;
            try
            {
                weapon.cfg = BuildLoadoutCfg(weapon, serialized, mods);
                var report = BuildScoreReportCore(weapon, serialized, chargeRatio, mods);
                report.LoadoutCount = mods.SelectedCount;
                return report;
            }
            finally
            {
                weapon.cfg = original;
            }
        }

        /// <summary>
        /// 读 prefab 原始数据算一份评分（不跑 InitAttribute、不改资产）。
        /// <para>⚠ 伤害取 <c>DamageData.GetAttr</c> 而不是 <c>cfg[DirectDamage]</c>：prefab 上的
        /// cfg 本来就没有伤害键，那是运行时 <c>InitAttribute</c> 才从 Damages 补进去的。</para>
        /// </summary>
        private WeaponScoreReport BuildScoreReportCore(WeaponPlayerController weapon, SerializedObject serialized,
            float chargeRatio = 1f, LoadoutMods mods = null)
        {
            var report = new WeaponScoreReport();
            var damages = weapon.Damages;
            if (damages == null || damages.Count == 0) return report;

            // 投掷物没有"连续射击"这回事：DPS 不参与计分，权重并给单发伤害
            report.IsThrowable = weapon.WeaponTypeEnum == WeaponTypeEnum.Grenade;

            var data = damages[Mathf.Clamp(weapon.UseDamageIndex, 0, damages.Count - 1)];
            if (data == null) return report;

            report.Muzzles = GetManyMuzzleCount(serialized);

            var direct = AttrOrData(weapon, data, WeaponAttrType.DirectDamage);
            var explosion = AttrOrData(weapon, data, WeaponAttrType.ExplosionDamage);
            // 蓄力判定看 DamageData.UseCharge（不是 ShootType）：_HandleValue 只按 Lerp(1, scale, charge) 缩放
            report.ChargeRatio = chargeRatio;
            report.ChargeScale = 1f;
            report.ChargeRangeScale = 1f;
            if (ReadUseCharge(serialized, weapon))
            {
                report.UseCharge = true;
                report.ChargeScale = Mathf.Lerp(1f,
                    Mathf.Max(0f, AttrOrData(weapon, data, WeaponAttrType.ChargeDamageScale)), chargeRatio);
                report.ChargeRangeScale = Mathf.Lerp(1f,
                    Mathf.Max(0f, AttrOrData(weapon, data, WeaponAttrType.ChargeExplosionRangeScale)), chargeRatio);
                // 直击和爆炸都吃 ChargeDamageScale（GetDirectDamage / GetExplosionDamage 同口径）
                direct *= report.ChargeScale;
                explosion *= report.ChargeScale;
            }
            report.DirectDamage = direct;
            report.ExplosionDamage = explosion;
            // 单发 = 直击 + 爆炸（爆心满伤）：命中目标同时吃"直击"和"半径内的爆炸"两笔，见 FpsHelper_Hit
            report.DamagePerProjectile = direct + explosion;

            var bullets = Mathf.Clamp(weapon.AttrFinal(WeaponAttrType.BulletsPerShot, 1).RawInt, 1, 64);
            report.ProjectilesPerTrigger = bullets * report.Muzzles;
            report.ShootCost = Mathf.Max(1, weapon.AttrFinal(WeaponAttrType.ShootCost, report.Muzzles).RawInt);
            // 蓄力枪真正的扣弹在 TryReleaseCharge 里按段数插值：UseMagazine(Lerp(lowest, highest, 蓄力比例))
            if (weapon.ShootType == WeaponShootType.Charge)
            {
                var lowestCost = weapon.AttrFinal(WeaponAttrType.ChargeAmmoOnLowest, report.ShootCost).RawInt;
                var highestCost = weapon.AttrFinal(WeaponAttrType.ChargeAmmoOnHighest, report.ShootCost).RawInt;
                report.ShootCost = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(lowestCost, highestCost, chargeRatio)));
            }

            report.Interval = Mathf.Max(0.02f, weapon.AttrFinal(WeaponAttrType.ShootInterval).RawFloat);
            // 连射（弹丸数 > 1 且连射间隔 > 0）一轮打完可能比射击间隔长，实际节奏取两者较大值
            var burstInterval = weapon.AttrFinal(WeaponAttrType.BurstInterval).RawFloat;
            if (bullets > 1 && burstInterval > 0f)
            {
                report.Interval = Mathf.Max(report.Interval, burstInterval * (bullets - 1));
            }
            if (weapon.ShootType == WeaponShootType.Charge)
            {
                // 蓄力耗时按比例计：最低蓄力可以早放，间隔里就不该加满 3 秒
                report.Interval += weapon.AttrFinal(WeaponAttrType.ChargeDuration).RawFloat * chargeRatio;
            }

            report.FireRate = 1f / report.Interval;
            // 引信延迟：ProjectileGrenade 撞到东西既不结算也不消失，要等基类引信（MaxLifeTime）到点
            // 才在当前位置爆一次 ⇒ 扣完扳机后 delay 秒伤害才落地。DPS / 反中 / 反重 / 清杂窗口都得算这笔。
            // ⚠ 别拿 ShootInterval 当节奏：爆裂手里剑 0.1s 一发，但伤害要等 4 秒才到。
            report.DamageDelay = data.BulletPrefab is ProjectileGrenade
                ? Mathf.Max(0f, AttrOrData(weapon, data, WeaponAttrType.LifeTime))
                : 0f;

            report.Magazine = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.Magazine).RawFloat);
            report.Ammo = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.Ammo).RawFloat);
            report.Reload = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.ReloadTime).RawFloat);

            report.MagazineShots = report.Magazine / report.ShootCost;

            // ---- 锁定类：锁定 → 射击，每发都要先"占用"一层锁定 ----
            // 机制（WeaponController.UpdateLock / GetLockBullet）：每 LockInterval 秒攒 1 层，
            // 攒满 LockLayers 层后每次开火消耗 1 层（换一个目标），打空后才需要重新锁。
            // ⇒ 一整轮 = 锁 N 层 + 打 N 发，摊到每发就是 **射击间隔 + LockInterval**。
            // ⚠ 别按"每发都重新锁满 N 层"算：那会把 DPS 压成 1/N（阿波罗 12 层会从 ~200 掉到 16）。
            if (weapon.ShootType == WeaponShootType.Lock)
            {
                report.LockLayers = Mathf.Max(0, weapon.AttrFinal(WeaponAttrType.LockLayers).RawInt);
                report.LockInterval = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.LockInterval).RawFloat);
                report.LockTime = report.LockInterval;      // 每发摊销的锁定耗时
                report.Interval += report.LockTime;
                report.FireRate = 1f / report.Interval;
            }

            report.DamagePerTrigger = report.DamagePerProjectile * report.ProjectilesPerTrigger;
            // 衍生物（子弹上的 SustainedEffect / 命中特效里的临时武器）按"一次扣扳机额外造成"并入单发：
            // 只看 Damages 会把爆裂手里剑、战术铅爆雷这类靠衍生物输出的武器评低
            // 尾迹附加伤害类型（恐惧/电磁/燃烧尾迹）：运行时由 WeaponUniqueAttributeApplier 写进
            // Damages[DamageIndex].DamageGroupExplosion，prefab 上这些组是空的 ⇒ 必须由评分自己补，
            // 否则装了「燃烧尾迹3」尾迹伤害该 ×4、「恐惧尾迹10」控制成分该 ×10，评分全看不见。
            var specialValues = CollectSpecialValues(serialized, mods?.Specials);
            var derivative = CollectDerivative(data, 0, weapon, CollectTrailTypeValues(specialValues));
            report.DerivativeDamage = derivative.Damage;
            report.DerivativeRealDamage = derivative.RealDamage;
            report.DerivativeArmorDamage = derivative.ArmorDamage;
            report.DerivativeDuration = derivative.Duration;
            report.DerivativeText = derivative.Text;
            report.DamagePerTrigger += derivative.Damage;

            // ---- 有效伤害 / 控制：伤害组里每个类型各结算一次 ----
            // Damageable.InflictDamage：`value = damage × 系数` 逐类型入账 ⇒ 直击 10 配「动能1 + 燃烧1」= 动能 10 + 燃烧 10 = **20**，
            // 不是把 10 平摊成 5+5。所以"计血系数"是**求和**（HealthCoefficient），结果可能 > 1。
            // 返回 0 的类型只有 Destruction（只削甲）、Catalyst/毒/骇入/恐惧/眩晕（只推积蓄槽）；
            // 冰冻/燃烧/电击/辐射既推槽又照常掉血 ⇒ DPS / 总伤 / 清杂 / 反重都按它们计入后的量算。
            report.HealthDamagePerProjectile = direct * HealthCoefficient(weapon, data, false)
                                               + explosion * HealthCoefficient(weapon, data, true);
            report.RealDamagePerTrigger = report.HealthDamagePerProjectile * report.ProjectilesPerTrigger
                                          + derivative.RealDamage;
            // ⚠ 不 Clamp01：多成分叠加时这个比值会 >1（1.0 = 面板值，2.0 = 实际是面板的两倍），
            //   压到 1 会把"10→20"这种最有用的信息吃掉（曾导致单发行只显示 10）。
            report.RealDamageRatio = report.DamagePerTrigger > 0f
                ? report.RealDamagePerTrigger / report.DamagePerTrigger : 0f;
            report.StatusPower = (direct * StatusCoefficient(weapon, data, false)
                                  + explosion * StatusCoefficient(weapon, data, true))
                                 * report.ProjectilesPerTrigger + derivative.Status;

            // ---- 一次扣扳机能打中几个目标：爆炸/光束半径 + 穿透单位 ----
            // 微波枪这类激光把「作用范围」当命中半径（ProjectileLaser.useExplosionRange ⇒ finalRaudis = 作用范围），
            // 普通爆炸弹按爆炸半径圈面积估"能罩住几个杂兵"；再叠加「穿透单位」标旗
            // （prefab 的 BulletFlag、或改装里点开的「穿透单位」专有属性 ⇒ 运行时由
            //   WeaponUniqueAttributeApplier 写回 BulletFlag）。
            // ⚠ 这个系数只喂清杂：DPS / 总伤 / 单发 / 反重全部按单体，别做成"身边永远站着 4 个人"。
            var baseExplosionRange = AttrOrData(weapon, data, WeaponAttrType.ExplosionRange);
            report.IsBeamSplash = data.BulletPrefab is ProjectileLaser laser && laser.useExplosionRange;
            // 「穿透单位」可能是 prefab 标旗、这次选中的升级项（Special，cfg 里看不到），
            // 也可能是尾迹类衍生物（每拍沿飞行线整条打穿，等价于一整条线都在挨打）
            report.TrailsUnits = derivative.HasTrail;
            report.PenetratesUnits = HasUnitPenetration(serialized)
                                     || (mods != null && mods.HasSpecial("穿透单位"))
                                     || report.TrailsUnits;
            // 尾迹的命中范围是"尾迹档自己的爆炸半径 + 尾迹范围加成"（运行时 ApplyTrailRange 就写在那里），
            // 不是直击档的爆炸半径 —— 否则装「尾迹范围 +1」清杂一点变化都没有。
            report.TrailRange = report.TrailsUnits ? TrailRadius(weapon, specialValues) : 0f;
            var targetRadius = report.TrailsUnits
                ? Mathf.Max(0.1f, report.TrailRange)
                : baseExplosionRange;
            report.MultiTargetFactor = MultiTargetCount(report.IsBeamSplash, report.ExplosionDamage > 0f,
                targetRadius, report.PenetratesUnits);
            report.EffectiveDamagePerTrigger = report.RealDamagePerTrigger * report.MultiTargetFactor;

            // 整池总伤：能量池/弹匣打光的理论伤害，只用来算持续 DPS
            // ⚠ 一律按**单体**：多目标只进清杂（见下面 ScoreClear 的 groupFactor）
            report.PoolDamage = report.RealDamagePerTrigger * report.MagazineShots;
            report.TotalDamage = report.RealDamagePerTrigger * ((report.Magazine + report.Ammo) / report.ShootCost);
            // 有引信延迟的（手雷）第一笔伤害要等它落地，不能按"扣扳机即结算"算爆发 DPS
            report.BurstDps = report.RealDamagePerTrigger / Mathf.Max(0.02f, report.Interval + report.DamageDelay);

            // ---- 热量武器：热量是独立的 0~100 池（WeaponBaseController.MaxHeat），不是弹匣 ----
            // 每发加热，满 100 过热并锁 OverheatDuration 秒（期间热量线性归零）。
            var heatPerShot = weapon.AttrFinal(WeaponAttrType.HeatPerShot).RawFloat;
            report.OverheatDuration = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.OverheatDuration).RawFloat);
            if (heatPerShot > 0f)
            {
                report.HasHeat = true;
                // 热量按 ChargeHeatScale 随蓄力比例放大（AddHeatPerShot 的口径）
                if (weapon.ShootType == WeaponShootType.Charge)
                {
                    heatPerShot *= Mathf.Lerp(1f,
                        Mathf.Max(0f, AttrOrData(weapon, data, WeaponAttrType.ChargeHeatScale)), chargeRatio);
                }
                // 连射时每发都会重置散热延迟；只有射击间隔比散热延迟长，中间才真的散掉一点
                var coolSpeed = report.CoolSpeed = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.CoolSpeed).RawFloat);
                var coolDelay = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.CoolDelay).RawFloat);
                var effectiveHeat = heatPerShot - coolSpeed * Mathf.Max(0f, report.Interval - coolDelay);
                report.HeatPerShot = heatPerShot;
                report.EffectiveHeat = Mathf.Max(0f, effectiveHeat);
                // ⚠ 有效热量 <= 0 = 散热比加热快，连射永远到不了 100 ⇒ 不算过热。
                //   以前钳到 0.01 会算出 ceil(100/0.01)=1 万发，弹匣总伤直接变成 150 万。
                if (effectiveHeat > 0.0001f)
                {
                    // Mathf.Ceil：100 恰好被整除时那一发打在满热上，仍算进本轮
                    report.BurstShots = Mathf.Max(1f, Mathf.Ceil(100f / effectiveHeat));
                    // 一轮最多打到弹匣耗尽，别让极小的每发热量算出天文数字
                    if (report.MagazineShots > 0f)
                    {
                        report.BurstShots = Mathf.Min(report.BurstShots, Mathf.Max(1f, report.MagazineShots));
                    }
                }
            }

            // ---- 热量武器的节奏择优：打满过热 vs 主动停火自然散热 ----
            // A 打满过热：n 发 → 锁 OverheatDuration 秒（期间热量线性归零）
            // B 主动散热：只打 n-1 发就停手，等 CoolDelay 后按 CoolSpeed 散掉 (n-1)×净攒，再打下一轮
            //   （"我 6 发过热，那就打 5 发等自然散热"）——两条路各算"每秒能扣几次扳机"，取吞吐高的。
            // ⚠ 别只算过热：慢射速 / 快散热的枪，主动散热往往比吃满过热锁定更划算。
            report.HeatCycleShots = report.BurstShots;
            report.HeatCyclePause = report.OverheatDuration;
            report.HeatStyleVent = false;
            if (report.HasHeat && report.BurstShots > 0f)
            {
                report.CoolDelay = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.CoolDelay).RawFloat);
                if (report.CoolSpeed > 0.0001f)
                {
                    var ventShots = Mathf.Max(0f, report.BurstShots - 1f);
                    if (ventShots >= 1f)
                    {
                        // 稳态：一轮开始时热量刚好在"再打 ventShots 发就到 100"的位置，
                        // 一轮结束又回到这里 ⇒ 需要散掉 ventShots × 每发净攒
                        var ventPause = report.CoolDelay + ventShots * report.EffectiveHeat / report.CoolSpeed;
                        var rateOverheat = report.BurstShots
                                           / Mathf.Max(0.001f, report.BurstShots * report.Interval + report.OverheatDuration);
                        var rateVent = ventShots
                                       / Mathf.Max(0.001f, ventShots * report.Interval + ventPause);
                        if (rateVent > rateOverheat)
                        {
                            report.HeatCycleShots = ventShots;
                            report.HeatCyclePause = ventPause;
                            report.HeatStyleVent = true;
                        }
                    }
                }
            }

            // 弹匣总伤：热量武器的"一匣" = 到过热为止的一轮（热量 100 才是它的弹匣），
            // 拿整池算会把 128 发的能量池当成「一口气能打完的一匣」，弹匣总伤虚高一个数量级。
            // ⚠ 这里仍按"理论过热发数"算：它描述热量上限，与下面实战节奏（可能主动散热）分开
            report.MagazineDamage = report.HasHeat && report.BurstShots > 0f
                ? report.RealDamagePerTrigger * report.BurstShots
                : report.PoolDamage;

            // 持续 DPS：打完一匣 + 一次换弹为一个循环。热量武器在中间还要插入过热停顿，
            // 否则 128 发的能量池会被算成"12.8 秒打空"这种根本不存在的节奏。
            // ⚠ 分子必须用 PoolDamage 而不是 MagazineDamage：后者已被热量口径改成"一轮"。
            var dumpTime = report.MagazineShots * report.Interval;
            if (report.HasHeat && report.BurstShots > 0f)
            {
                // 用择优后的"每轮发数 / 每轮停顿"：过热流停顿 = OverheatDuration，散热流停顿 = 散热耗时
                var cycleShots = Mathf.Max(1f, report.HeatCycleShots);
                report.HeatPauses = Mathf.Max(0, Mathf.CeilToInt(report.MagazineShots / cycleShots) - 1);
                dumpTime += report.HeatPauses * report.HeatCyclePause;
            }
            report.DumpTime = dumpTime;
            report.SustainedDps = dumpTime > 0f
                ? report.PoolDamage / (dumpTime + report.Reload + report.DamageDelay)
                : 0f;

            // 清杂节奏：把换弹 / 过热的**真空期**摊进每次扣扳机。
            // ⚠ 不能用 1/Interval：那只是"扣扳机时多快"，一发霰弹枪 3 秒换弹的枪也会被算成高射速。
            var clearCycle = dumpTime + report.Reload;
            report.ClearRate = report.MagazineShots > 0.01f && clearCycle > 0.01f
                ? report.MagazineShots / clearCycle
                : report.FireRate;

            // 穿甲只取真正造成伤害的那一侧：纯爆炸弹看 ExplosionAP，直击弹看 DirectAP。
            // _HandleAP 也会随蓄力缩放（DirectAPChargeScale / ExplosionAPChargeScale）
            var directAp = AttrOrData(weapon, data, WeaponAttrType.DirectAP);
            var explosionAp = AttrOrData(weapon, data, WeaponAttrType.ExplosionAP);
            if (report.UseCharge)
            {
                var element = FindDamageElement(serialized, weapon);
                var directScale = element != null ? element.FindPropertyRelative("DirectAPChargeScale") : null;
                var explosionScale = element != null ? element.FindPropertyRelative("ExplosionAPChargeScale") : null;
                if (directScale != null) directAp *= Mathf.Lerp(1f, directScale.floatValue, chargeRatio);
                if (explosionScale != null) explosionAp *= Mathf.Lerp(1f, explosionScale.floatValue, chargeRatio);
            }
            report.AP = Mathf.RoundToInt(Mathf.Max(direct > 0f ? directAp : 0f, explosion > 0f ? explosionAp : 0f));

            // ---- 破甲：直击/爆炸里的护甲破坏（Destruction）成分 ----
            // ⚠ 各成分是"整笔伤害 × 各自系数"并排结算（见 Damageable.InflictDamage），不是从总伤里分摊
            //   ⇒ 直击 100 配 Destruction=1 就是 100 点护甲破坏，与 Gun 系数是多少无关。
            report.DirectDestruction = Mathf.Max(0f, AttrOrData(weapon, data, WeaponAttrType.DirectDestruction));
            report.ExplosionDestruction = Mathf.Max(0f, AttrOrData(weapon, data, WeaponAttrType.ExplosionDestruction));
            report.ArmorDamage = (direct * report.DirectDestruction + explosion * report.ExplosionDestruction)
                                 * report.ProjectilesPerTrigger + derivative.ArmorDamage;

            // ---- 细项统计：弱点 / 范围 / 人机工学 / 弹道 / 特殊 ----
            report.WeaknessBonus = AttrOrData(weapon, data, WeaponAttrType.WeaknessBonus);
            report.BaseExplosionRange = baseExplosionRange;
            // 满蓄时 GetDamageOuterRadius 会乘 ChargeAOERangeScale（_HandleValue(ExplosionRange, scale, 1)）
            report.ExplosionRange = report.BaseExplosionRange * report.ChargeRangeScale;
            // 蓄力会缩放散布：运行时 DamageData.GetSpread 返回乘数 Lerp(1, ChargeSpreadScale, 蓄力比例)，
            // 由 GetShotDirectionWithinSpread 乘到实际散布上（ChargeSpreadScale 不在 WeaponAttrType 里，只能读字段）。
            // ⚠ 漏乘 ⇒ 皇室蔷薇满蓄 ChargeSpreadScale=0（激光精度）仍按 2° 算，弱点命中率被压到 33.75%。
            report.Spread = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.BulletsSpreadAngle).RawFloat);
            if (report.UseCharge)
            {
                var spreadScaleProp = FindDamageElement(serialized, weapon)?.FindPropertyRelative("ChargeSpreadScale");
                var spreadScale = spreadScaleProp != null ? spreadScaleProp.floatValue : 1f;
                report.Spread *= Mathf.Lerp(1f, Mathf.Max(0f, spreadScale), chargeRatio);
            }
            // 后坐力取"基础 × 改装倍率"：模组改的是 Recoil 属性（RecoilScale），裸字段 RecoilForce 看不到改装
            report.RecoilForce = weapon.RecoilForceFinal;
            report.BulletSpeed = AttrOrData(weapon, data, WeaponAttrType.BulletSpeed);
            // 最大射程 -1 = 不设上限：真实射程按"生命周期 × 弹速"估（与子弹超距消散的口径一致）
            var maxRange = AttrOrData(weapon, data, WeaponAttrType.MaxRange);
            report.EffectiveRange = maxRange > 0f
                ? maxRange
                : report.BulletSpeed * Mathf.Max(0f, AttrOrData(weapon, data, WeaponAttrType.LifeTime));

            ReadSpecialInfo(serialized, out var uniqueCount, out var specialModCount, out var uniqueNames);
            report.UniqueTagCount = uniqueCount;
            report.SpecialModCount = specialModCount;
            report.UniqueTagText = uniqueNames;

            // 拆毁值（私有序列化字段）：大于目标的拆毁值即可直接秒杀巢穴，<30 视为没有拆巢能力
            var demolishProp = FindDamageElement(serialized, weapon)?.FindPropertyRelative("demolishValue");
            // 衍生物（持续效果 / 临时武器）自己也可能带拆毁值，取两者较大者
            report.DemolishValue = Mathf.Max(demolishProp != null ? demolishProp.intValue : 0, derivative.Demolish);

            report.ScoreBurstDps = ScoreCurve(report.BurstDps, DpsAnchors, true);
            report.ScoreSustainDps = ScoreCurve(report.SustainedDps, SustainAnchors, true);
            // 单发只按"真的扣血"的部分评分（纯控制/纯破甲武器不能靠 0 伤害拿分）；按单体，不乘目标数
            report.ScorePerTrigger = ScoreCurve(report.RealDamagePerTrigger, PerTriggerAnchors, true);
            // 一轮/整池总伤只留作参考与对比表，不再单独计分（已并入下面的续航）
            report.ScoreMagazineDamage = ScoreCurve(report.MagazineDamage, MagazineDamageAnchors, true);
            report.ScoreTotalDamage = ScoreCurve(report.TotalDamage, TotalDamageAnchors, true);

            // ---- 续航：整池弹药打完的实际秒数 ----
            // ClearRate 已经把换弹/过热真空期摊进去了 ⇒ 整池时长 = 全弹次数 ÷ ClearRate。
            // 弹匣量（每匣几次）、总备弹（共几次）、换弹时长（真空期占比）三件事全在这一个数里。
            var stockShotsAll = report.ShootCost > 0 ? (report.Magazine + report.Ammo) / report.ShootCost : 0f;
            report.EnduranceSeconds = report.ClearRate > 0.0001f ? stockShotsAll / report.ClearRate : 0f;
            report.ScoreEndurance = report.IsThrowable
                ? 0f
                : ScoreCurve(report.EnduranceSeconds, EnduranceAnchors, true) * EnduranceTimeWeight
                  + report.ScoreTotalDamage * (1f - EnduranceTimeWeight);
            report.ScoreAp = ScoreCurve(report.AP, ApAnchors, false);
            // 破甲值里有 0 ⇒ 只能走线性插值（log(0) 会算出 NaN）
            report.ScoreArmorBreak = ScoreCurve(report.ArmorDamage, ArmorBreakAnchors, false);
            report.ScoreControl = ScoreCurve(report.StatusPower, StatusAnchors, false);
            report.ScoreWeakness = ScoreCurve(report.WeaknessBonus, WeaknessAnchors, false);

            // 范围：爆炸伤害占 6 成、半径占 4 成（纯直击武器没有这一项，记 0 分而不是给保底）
            // 光束武器没有爆炸伤害但有命中半径，只给"半径"那一档的分
            report.ScoreAoe = report.ExplosionDamage > 0f
                ? ScoreCurve(report.ExplosionDamage, ExplosionDamageAnchors, true) * 0.65f
                  + ScoreCurve(report.ExplosionRange, ExplosionRangeAnchors, false) * 0.35f
                : report.IsBeamSplash
                    ? ScoreCurve(report.ExplosionRange, ExplosionRangeAnchors, false) * 0.65f
                    : 0f;

            // 散布 / 后坐力"越低越好"⇒ 锚点分数递减；值里含 0，必须走线性插值（log 会算出 NaN）
            report.ScoreSpread = ScoreCurve(report.Spread, SpreadAnchors, false);
            report.ScoreRecoil = ScoreCurve(report.RecoilForce, RecoilForceAnchors, false);
            // 人机工学：两项各自换算成 0~100 后各占 50%（加权平均）。⚠ 不是相乘 ——
            // 相乘会让"散布大、后坐小"的枪被双倍惩罚，与手感无关。
            report.ScoreHandling = report.ScoreSpread * HandlingSpreadWeight
                                   + report.ScoreRecoil * HandlingRecoilWeight;

            report.ScoreSpeed = ScoreCurve(report.BulletSpeed, BulletSpeedAnchors, true);
            report.ScoreRange = ScoreCurve(report.EffectiveRange, RangeAnchors, true);
            report.ScoreBallistics = (report.ScoreSpeed + report.ScoreRange) * 0.5f;

            // 特殊标签：底子注册的专有属性占 6 成，改装池里还能拿到的 Special 占 4 成
            report.ScoreSpecial = ScoreCurve(report.UniqueTagCount, TagAnchors, false) * 0.6f
                                  + ScoreCurve(report.SpecialModCount, TagAnchors, false) * 0.4f;

            // ---- 综合能力：直接"打靶算账"，不再用加权合成 ----
            // 每扣一次扳机、打到指定护甲等级的目标身上真正进血条的量：
            // 直击/爆炸/衍生物各自按自己的 AP 折算（AP 0 打 1 甲 ×0.6、打 4 甲 ×0.2=直接免疫）
            // limb = 打的是肢体（头部）：范围/爆炸里的元素成分只落主体，肢体那份被 Damageable 丢掉
            // ⇒ 肢体只吃「直击的全部成分 + 爆炸的非元素部分」。主体（躯干/清杂靶）仍吃全部。
            float HealthPerTriggerAt(float armorLevel, bool limb = false)
                => (direct * HealthCoefficient(weapon, data, false) * ArmorFactor(armorLevel, directAp)
                    + explosion * (limb ? LimbHealthCoefficient(weapon, data) : HealthCoefficient(weapon, data, true))
                      * ArmorFactor(armorLevel, explosionAp))
                   * report.ProjectilesPerTrigger
                   + derivative.RealDamage * ArmorFactor(armorLevel, report.AP);

            // 清杂 = 10 秒内能击杀多少只 50 血 1 甲的杂兵：
            //   每次扣扳机能杀几只 = clamp01(对 1 甲的单发有效伤害 / 50) × 目标数（多目标只在这里生效）
            //   10 秒能扣几次     = 实战节奏 ClearRate（摊过换弹/过热）× 10，再被总备弹卡住
            //   ⚠ 尾迹是"扣完扳机还在持续 5 秒"的，不能整个塞进"每次扣扳机"，否则 5 秒的持续伤害
            //     只被算 1 次（书记的惩戒因此清杂恒 0）。这里把它拆出来单独按时间轴算。
            var clearArmorDamage = report.ClearDamagePerTrigger = HealthPerTriggerAt(ClearMobArmorLevel);
            var multiTarget = Mathf.Max(1f, report.MultiTargetFactor);
            var trailArmorFactor = ArmorFactor(ClearMobArmorLevel, report.AP);
            var trailDamage = report.TrailsUnits
                ? report.DerivativeRealDamage * trailArmorFactor
                : 0f;
            var shootDamage = Mathf.Max(0f, clearArmorDamage - trailDamage);
            var killsPerTrigger = Mathf.Clamp01(shootDamage / ClearMobHp) * multiTarget;
            var stockTriggers = report.ShootCost > 0
                ? (report.Magazine + report.Ammo) / report.ShootCost
                : 0f;
            // 引信延迟：窗口末尾才扔出去的还没炸 ⇒ 只有 (窗口 - 延迟) 内扣的扳机算数（延迟 0 时与原口径一致）
            var windowForHit = Mathf.Max(0f, ClearWindowSeconds - report.DamageDelay);
            var triggersInWindow = Mathf.Min(
                report.ClearRate * windowForHit + (report.DamageDelay > 0f ? 1f : 0f), stockTriggers);
            // 尾迹覆盖时长 = 扣扳机次数 × 存活时间（一条尾迹只活 Duration 秒，也超不过 10 秒窗口）
            var trailSeconds = report.TrailsUnits && report.DerivativeDuration > 0.01f
                ? Mathf.Min(ClearWindowSeconds, Mathf.Max(0f, triggersInWindow) * report.DerivativeDuration)
                : 0f;
            report.TrailKills = trailSeconds > 0f
                ? trailDamage / report.DerivativeDuration * trailSeconds * multiTarget / ClearMobHp
                : 0f;
            report.ClearKillsIn10s = killsPerTrigger * Mathf.Max(0f, triggersInWindow) + report.TrailKills;
            // 投掷物改按"一颗能清几只"评：它们备弹只有 5 发、全卡在引信里，再乘 10 秒窗口只会把
            // 同一颗手雷的覆盖面算两遍（备弹/节奏已经由续航与引信延迟那两项负责）。
            report.ClearKillsPerThrow = killsPerTrigger;
            report.ScoreClear = report.IsThrowable
                ? ScoreCurve(report.ClearKillsPerThrow, ClearPerThrowAnchors, false)
                : ScoreCurve(report.ClearKillsIn10s, ClearKillAnchors, false);
            // 反重 / 反中：打靶 TTK（打不穿 / 备弹不够 = 打不死 = 0 分）
            // ⚠ 不单列 AP 档位分：AP 已经通过有效伤害体现（旧版 AP 45% + 有效伤害 35% 等于把 AP 算两遍）。
            // 有效伤害按目标护甲折算（与 Damageable.InflictDamage 同一条公式）：
            //   armorFactor = clamp01(1 - (护甲等级 - AP) × 0.4) ⇒ AP 每低 1 级伤害 ×0.6，低 3 级即完全免疫。
            // 分子只用"计入血量"的成分：护甲破坏/控制成分不打血，不能拿去换反重分。
            //
            // ▍弱点（头部）模型：
            //   靶子分「躯干 + 头部」两个血池/护甲，头部是弱点。打中头部的比例 = 人机工学评分 × 0.9
            //   （人机低 = 散布/后坐压不住，打不出稳定爆头 ⇒ 这块奖励自然拿不到）。
            //   **打爆头部即视为死亡**：两条路线各自按分到的命中率算"只打这一条要多久"，取更快的那条：
            //     头路线 = ceil(头血 / (p × 头单发))；躯干路线 = ceil(躯干血 / ((1-p) × 躯干单发))
            //   p=1 退化成纯爆头、p=0 退化成纯躯干；混合时谁能先打穿就走谁 —— 这也是"精准武器
            //   靠爆头快速解决高血目标"的直译，而不是把两个血池加在一起当总血条（那样爆头反而没收益）。
            // ⚠ 弱点加成（WeaknessBonus）只乘在**打中头部**的那一份上：它不是"整枪伤害倍率"，
            //   否则不打头的枪也能白拿弱点词条的收益。
            report.WeakPointPrecision = Mathf.Clamp01(report.ScoreHandling / 100f) * WeakPointPrecisionCap;
            // 首发命中率只看散布：后坐力是"打出这一发之后"才把枪口顶起来的，对第一发不成立。
            // ⚠ 别把它直接当总命中率（见 KillTtk）：要连打时第一发的后坐照样会顶在第二发上。
            report.FirstShotPrecision = Mathf.Clamp01(report.ScoreSpread / 100f) * WeakPointPrecisionCap;
            var precision = report.WeakPointPrecision;
            var headScale = 1f + Mathf.Max(0f, report.WeaknessBonus);

            // 击杀耗时 = min(打爆头部, 打空躯干)，再按实战节奏换成秒（打不穿 / 备弹不够 = 0）
            // 单发就够杀（该路线 hp ≤ 单发伤害）⇒ 用首发精度：一发带走，后坐力根本不参与；
            // 要两发以上才回到人机工学（分散布 + 后坐）。
            float KillTtk(float bodyHp, float bodyDamage, float headHp, float headDamage)
            {
                var bodyPrecision = bodyHp <= bodyDamage ? report.FirstShotPrecision : precision;
                var headPrecision = headHp <= headDamage ? report.FirstShotPrecision : precision;
                var bodyRate = (1f - bodyPrecision) * bodyDamage;
                var headRate = headPrecision * headDamage;
                var shots = Mathf.Min(
                    bodyRate > 0.01f ? Mathf.Ceil(bodyHp / bodyRate) : float.PositiveInfinity,
                    headRate > 0.01f ? Mathf.Ceil(headHp / headRate) : float.PositiveInfinity);
                if (float.IsInfinity(shots) || shots > stockTriggers || report.ClearRate <= 0.01f) return 0f;
                // 引信延迟：第一笔伤害要等引信到点，之后的每一发再按投掷节奏累加
                var seconds = report.DamageDelay > 0f
                    ? report.DamageDelay + (shots - 1) / report.ClearRate
                    : shots / report.ClearRate;
                return Mathf.Min(seconds, 9999f);
            }

            // ---- 反重：躯干 1000 血 4 甲 + 头部 350 血 4 甲（弱点）----
            report.HeavyArmorLevel = HeavyArmorLevel;
            var heavyBodyDamage = HealthPerTriggerAt(HeavyArmorLevel);
            // 头部是肢体：爆炸里的元素成分被 Damageable 丢掉 ⇒ 单独按"直击全部 + 爆炸非元素"算，再乘弱点
            var heavyHeadDamage = HealthPerTriggerAt(HeavyArmorLevel, true) * headScale;
            report.HeavyDamagePerTrigger = heavyBodyDamage;
            // 系数只反映护甲折算（不含弱点/头部），别让弱点把它顶过 1
            report.HeavyDamageFactor = report.RealDamagePerTrigger > 0f
                ? Mathf.Clamp01(heavyBodyDamage / report.RealDamagePerTrigger) : 0f;
            report.HeavyTtk = KillTtk(HeavyTargetHp, heavyBodyDamage, HeavyHeadHp, heavyHeadDamage);
            report.ScoreHeavyOutput = report.HeavyTtk > 0f
                ? ScoreCurve(report.HeavyTtk, TtkAnchors, false)
                : 0f;

            // 反重 = 打靶输出 80% + 破甲 20%
            report.ScoreAntiHeavy = report.ScoreHeavyOutput * 0.80f
                                    + report.ScoreArmorBreak * 0.20f;

            // ---- 反中：躯干 500 血 3 甲 + 头部 200 血 1 甲（弱点）----
            // 头部护甲只有 1 ⇒ 低穿甲的枪也能靠爆头打出有效伤害，"精准 + 够用的穿甲"在这里有回报。
            var mediumBodyDamage = HealthPerTriggerAt(MediumBodyArmorLevel);
            var mediumHeadDamage = HealthPerTriggerAt(MediumHeadArmorLevel, true) * headScale;
            report.MediumDamagePerTrigger = precision * mediumHeadDamage + (1f - precision) * mediumBodyDamage;
            report.MediumTtk = KillTtk(MediumBodyHp, mediumBodyDamage, MediumHeadHp, mediumHeadDamage);
            report.ScoreMediumOutput = report.MediumTtk > 0f
                ? ScoreCurve(report.MediumTtk, MediumTtkAnchors, false)
                : 0f;
            report.ScoreAntiMedium = report.ScoreMediumOutput;
            // 拆巢：只看拆毁值，低于 30 直接 0 分
            report.ScoreDemolish = report.DemolishValue >= 30f
                ? ScoreCurve(report.DemolishValue, DemolishAnchors, false)
                : 0f;

            // 投掷物：爆发/持续/续航三项不计分，权重并进单发
            // （单发 3 + 爆发 3 + 持续 3 + 续航 6 = 15%）
            var perTriggerWeight = report.IsThrowable
                ? WeightPerTrigger + WeightBurstDps + WeightSustainDps + WeightEndurance
                : WeightPerTrigger;

            report.Overall = report.ScorePerTrigger * perTriggerWeight
                             + (report.IsThrowable
                                 ? 0f
                                 : report.ScoreBurstDps * WeightBurstDps + report.ScoreSustainDps * WeightSustainDps)
                             + report.ScoreEndurance * (report.IsThrowable ? 0f : WeightEndurance)
                             + report.ScoreAp * WeightAp
                             + report.ScoreArmorBreak * WeightArmorBreak
                             + report.ScoreWeakness * WeightWeakness
                             + report.ScoreAoe * WeightAoe
                             + report.ScoreHandling * WeightHandling
                             + report.ScoreBallistics * WeightBallistics
                             + report.ScoreSpecial * WeightSpecial
                             + report.ScoreClear * WeightClear
                             + report.ScoreAntiHeavy * WeightAntiHeavy
                             + report.ScoreAntiMedium * WeightAntiMedium
                             + report.ScoreDemolish * WeightDemolish
                             + report.ScoreControl * WeightControl;
            report.Valid = true;
            return report;
        }

        /// <summary>
        /// 统计"特殊标签"两个来源：
        /// <para>① <c>m_UniqueAttr</c>：武器底子注册的专有属性（这些才会被 ApplyUpgrade 建成 AttrInfo）；</para>
        /// <para>② 改装池：<c>Upgrade</c> / <c>Modules</c> 里所有 WUD/WMD 的 <c>modifys.type == Special</c>，按名字去重。</para>
        /// <para>⚠ 只统计不校验消费端 —— "注册了但没人读"的标签一样计入，排查失效要看 weapon-upgrade 的 §5.3。</para>
        /// </summary>
        private static void ReadSpecialInfo(SerializedObject serialized, out int uniqueCount,
            out int specialModCount, out string uniqueNames)
        {
            uniqueCount = 0;
            specialModCount = 0;
            uniqueNames = "";
            if (serialized == null) return;

            var uniqueProp = serialized.FindProperty("m_UniqueAttr");
            if (uniqueProp != null && uniqueProp.arraySize > 0)
            {
                var names = new List<string>(uniqueProp.arraySize);
                for (var i = 0; i < uniqueProp.arraySize; i++)
                {
                    var nameProp = uniqueProp.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                    if (nameProp == null || string.IsNullOrEmpty(nameProp.stringValue)) continue;
                    if (!names.Contains(nameProp.stringValue)) names.Add(nameProp.stringValue);
                }
                uniqueCount = names.Count;
                uniqueNames = string.Join("、", names);
            }

            var specials = new HashSet<string>();
            var upgradeProp = serialized.FindProperty("Upgrade");
            if (upgradeProp != null)
            {
                for (var i = 0; i < upgradeProp.arraySize; i++)
                {
                    var valueProp = upgradeProp.GetArrayElementAtIndex(i).FindPropertyRelative("Value");
                    if (valueProp == null) continue;
                    for (var j = 0; j < valueProp.arraySize; j++)
                    {
                        var data = valueProp.GetArrayElementAtIndex(j).objectReferenceValue as WeaponUpgradeData_SO;
                        if (data != null) AddSpecialNames(data.modifys, specials);
                    }
                }
            }

            var modulesProp = serialized.FindProperty("Modules");
            if (modulesProp != null)
            {
                for (var i = 0; i < modulesProp.arraySize; i++)
                {
                    var data = modulesProp.GetArrayElementAtIndex(i).objectReferenceValue as WeaponModuleData_SO;
                    if (data != null) AddSpecialNames(data.modifys, specials);
                }
            }

            specialModCount = specials.Count;
        }

        private static void AddSpecialNames(List<ModifyAttrData> modifys, HashSet<string> into)
        {
            if (modifys == null) return;
            for (var i = 0; i < modifys.Count; i++)
            {
                var item = modifys[i];
                if (item.type != WeaponAttrType.Special || string.IsNullOrEmpty(item.name)) continue;
                into.Add(item.name);
            }
        }

        /// <summary>齐射点数：UseManyMuzzle 是 protected，只能走 SerializedObject 读（对比武器要传它自己的）。</summary>
        private static int GetManyMuzzleCount(SerializedObject serialized)
        {
            if (serialized == null) return 1;
            var manyProp = serialized.FindProperty("UseManyMuzzle");
            if (manyProp == null || !manyProp.boolValue) return 1;
            var listProp = serialized.FindProperty("WeaponManyMuzzles");
            return Mathf.Max(1, listProp != null ? listProp.arraySize : 1);
        }
    }
}
