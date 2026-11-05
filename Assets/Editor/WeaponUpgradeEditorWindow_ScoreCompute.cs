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
        /// 对重型目标的伤害系数：<c>clamp01(1 - (护甲 - AP) × 0.4)</c>。
        /// <para>⚠ AP 超出护甲不回补伤害（上限 1），AP 相差 3 级及以上直接归零（完全打不动）。</para>
        /// </summary>
        private static float HeavyArmorFactor(float ap)
            => Mathf.Clamp01(1f - (HeavyArmorLevel - Mathf.RoundToInt(ap)) * ArmorPenaltyPerLevel);

        /// <summary>
        /// 一次扣扳机能命中几个目标（估算）。
        /// <para>· 光束把「作用范围」当命中半径 ⇒ 按面积估（每个目标按 2m×2m 站位 = 4 m²）；</para>
        /// <para>· 普通弹只有勾了「穿透单位」才不止一个目标（穿过第一个继续往后，按 +1 保守算）；</para>
        /// <para>· 光束 + 穿透 = 沿整条光束穿过去，上限放宽到 8。</para>
        /// <para>⚠ 只喂清杂：DPS / 总伤 / 单发 / 反重一律按单体，别拿这个系数去乘它们。</para>
        /// </summary>
        private static float MultiTargetCount(bool beamSplash, float radius, bool penetratesUnits)
        {
            if (beamSplash)
            {
                var r = Mathf.Max(0.1f, radius);
                return Mathf.Clamp(1f + Mathf.PI * r * r / 4f, 1f, penetratesUnits ? 8f : 6f);
            }
            return penetratesUnits ? 2f : 1f;
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
        private static float GetMinChargeRatio(WeaponPlayerController weapon)
        {
            var lowest = weapon.AttrFinal(WeaponAttrType.ChargeLowestStage).RawFloat;
            var highest = weapon.AttrFinal(WeaponAttrType.ChargeHigheststage).RawFloat;
            return highest > 0f ? Mathf.Clamp01(lowest / highest) : 1f;
        }

        /// <summary>
        /// 读 prefab 原始数据算一份评分（不跑 InitAttribute、不改资产）。
        /// <para>⚠ 伤害取 <c>DamageData.GetAttr</c> 而不是 <c>cfg[DirectDamage]</c>：prefab 上的
        /// cfg 本来就没有伤害键，那是运行时 <c>InitAttribute</c> 才从 Damages 补进去的。</para>
        /// </summary>
        private WeaponScoreReport BuildScoreReport(WeaponPlayerController weapon, SerializedObject serialized,
            float chargeRatio = 1f)
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

            report.Magazine = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.Magazine).RawFloat);
            report.Ammo = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.Ammo).RawFloat);
            report.Reload = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.ReloadTime).RawFloat);

            report.MagazineShots = report.Magazine / report.ShootCost;
            report.DamagePerTrigger = report.DamagePerProjectile * report.ProjectilesPerTrigger;
            // 衍生物（子弹上的 SustainedEffect / 命中特效里的临时武器）按"一次扣扳机额外造成"并入单发：
            // 只看 Damages 会把爆裂手里剑、战术铅爆雷这类靠衍生物输出的武器评低
            var derivative = CollectDerivative(data, 0);
            report.DerivativeDamage = derivative.Damage;
            report.DerivativeArmorDamage = derivative.ArmorDamage;
            report.DerivativeDuration = derivative.Duration;
            report.DerivativeText = derivative.Text;
            report.DamagePerTrigger += derivative.Damage;

            // ---- 有效伤害 / 控制：伤害成分里只有一部分真的扣血 ----
            // Health.HandleDamage 对 Destruction（只削甲）、Catalyst（只推积蓄槽）、Freeze、毒、骇入、恐惧、眩晕返回 0
            // ⇒ DPS / 总伤 / 清杂 / 反重都必须按"计入血量"的成分算，否则纯控制武器会凭 0 伤害拿满分。
            report.HealthDamagePerProjectile = direct * HealthCoefficient(data.DamageGroupDirect)
                                               + explosion * HealthCoefficient(data.DamageGroupExplosion);
            report.RealDamagePerTrigger = report.HealthDamagePerProjectile * report.ProjectilesPerTrigger
                                          + derivative.RealDamage;
            report.RealDamageRatio = report.DamagePerTrigger > 0f
                ? Mathf.Clamp01(report.RealDamagePerTrigger / report.DamagePerTrigger) : 0f;
            report.StatusPower = (direct * StatusCoefficient(data.DamageGroupDirect)
                                  + explosion * StatusCoefficient(data.DamageGroupExplosion))
                                 * report.ProjectilesPerTrigger + derivative.Status;

            // ---- 一次扣扳机能打中几个目标：光束半径 + 穿透单位 ----
            // 微波枪这类激光把「作用范围」当命中半径（ProjectileLaser.useExplosionRange ⇒ finalRaudis = 作用范围），
            // 再叠加「穿透单位」标旗 ⇒ 光束沿途每个敌人各吃一次直击（7 点真实伤害、按自己的 AP 结算），
            // 而不是"打到第一个就停"。
            // ⚠ 这个系数只喂清杂：DPS / 总伤 / 单发 / 反重全部按单体，别做成"身边永远站着 4 个人"。
            var baseExplosionRange = AttrOrData(weapon, data, WeaponAttrType.ExplosionRange);
            report.IsBeamSplash = data.BulletPrefab is ProjectileLaser laser && laser.useExplosionRange;
            report.PenetratesUnits = HasUnitPenetration(serialized);
            report.MultiTargetFactor = MultiTargetCount(report.IsBeamSplash, baseExplosionRange,
                report.PenetratesUnits);
            report.EffectiveDamagePerTrigger = report.RealDamagePerTrigger * report.MultiTargetFactor;

            // 整池总伤：能量池/弹匣打光的理论伤害，只用来算持续 DPS
            // ⚠ 一律按**单体**：多目标只进清杂（见下面 ScoreClear 的 groupFactor）
            report.PoolDamage = report.RealDamagePerTrigger * report.MagazineShots;
            report.TotalDamage = report.RealDamagePerTrigger * ((report.Magazine + report.Ammo) / report.ShootCost);
            report.BurstDps = report.RealDamagePerTrigger / report.Interval;

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

            // 弹匣总伤：热量武器的"一匣" = 到过热为止的一轮（热量 100 才是它的弹匣），
            // 拿整池算会把 128 发的能量池当成「一口气能打完的一匣」，弹匣总伤虚高一个数量级。
            report.MagazineDamage = report.HasHeat && report.BurstShots > 0f
                ? report.RealDamagePerTrigger * report.BurstShots
                : report.PoolDamage;

            // 持续 DPS：打完一匣 + 一次换弹为一个循环。热量武器在中间还要插入过热停顿，
            // 否则 128 发的能量池会被算成"12.8 秒打空"这种根本不存在的节奏。
            // ⚠ 分子必须用 PoolDamage 而不是 MagazineDamage：后者已被热量口径改成"一轮"。
            var dumpTime = report.MagazineShots * report.Interval;
            if (report.HasHeat && report.BurstShots > 0f)
            {
                report.HeatPauses = Mathf.Max(0, Mathf.CeilToInt(report.MagazineShots / report.BurstShots) - 1);
                dumpTime += report.HeatPauses * report.OverheatDuration;
            }
            report.DumpTime = dumpTime;
            report.SustainedDps = dumpTime > 0f ? report.PoolDamage / (dumpTime + report.Reload) : 0f;

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
            report.Spread = Mathf.Max(0f, weapon.AttrFinal(WeaponAttrType.BulletsSpreadAngle).RawFloat);
            report.RecoilForce = weapon.RecoilForce;
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
            report.ScoreMagazineDamage = ScoreCurve(report.MagazineDamage, MagazineDamageAnchors, true);
            report.ScoreTotalDamage = ScoreCurve(report.TotalDamage, TotalDamageAnchors, true);
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
            // 清杂 = 10 秒内能击杀多少只 50 血 0 甲的杂兵：
            //   每次扣扳机能杀几只 = clamp01(单发有效伤害 / 50) × 目标数（多目标只在这里生效）
            //   10 秒能扣几次     = 实战节奏 ClearRate（摊过换弹/过热）× 10，再被总备弹卡住
            var killsPerTrigger = Mathf.Clamp01(report.RealDamagePerTrigger / ClearMobHp)
                                  * Mathf.Max(1f, report.MultiTargetFactor);
            var stockTriggers = report.ShootCost > 0
                ? (report.Magazine + report.Ammo) / report.ShootCost
                : 0f;
            var triggersInWindow = Mathf.Min(report.ClearRate * ClearWindowSeconds, stockTriggers);
            report.ClearKillsIn10s = killsPerTrigger * Mathf.Max(0f, triggersInWindow);
            report.ScoreClear = ScoreCurve(report.ClearKillsIn10s, ClearKillAnchors, false);
            // 反重 = 打靶输出（TTK 分）80% + 破甲 20%
            // ⚠ 不单列 AP 档位分：AP 已经通过有效伤害体现（旧版 AP 45% + 有效伤害 35% 等于把 AP 算两遍）。
            // ⚠ 也不单列弱点分：弱点按 ×(1 + 弱点%/2) 折进伤害（见下面 weaknessScale）。
            // 有效伤害按"护甲 4 的重型目标"折算（与 Damageable.InflictDamage 同一条公式）：
            //   armorFactor = clamp01(1 - (4 - AP) × 0.4) ⇒ AP 每低 1 级伤害 ×0.6，低 3 级即完全免疫。
            // 分子只用"计入血量"的成分：护甲破坏/控制成分不打血，不能拿去换反重分。
            report.HeavyArmorLevel = HeavyArmorLevel;
            var heavyArmorDamage = (direct * HealthCoefficient(data.DamageGroupDirect) * HeavyArmorFactor(directAp)
                                    + explosion * HealthCoefficient(data.DamageGroupExplosion) * HeavyArmorFactor(explosionAp))
                                   * report.ProjectilesPerTrigger
                                   + derivative.RealDamage * HeavyArmorFactor(report.AP);
            // 弱点不再单列 20% 分，而是折进伤害：弱点 20% ⇒ ×(1 + 0.2/2) = ×1.1。
            // /2 是"不可能每枪都打中弱点"的折中，避免弱点词条被当成稳定倍率。
            var weaknessScale = 1f + Mathf.Max(0f, report.WeaknessBonus) * 0.5f;
            report.HeavyDamagePerTrigger = heavyArmorDamage * weaknessScale;
            // 系数只反映护甲折算（不含弱点），别让弱点把它顶过 1
            report.HeavyDamageFactor = report.RealDamagePerTrigger > 0f
                ? Mathf.Clamp01(heavyArmorDamage / report.RealDamagePerTrigger) : 0f;
            var heavyDamage = report.HeavyDamagePerTrigger;

            // 反重 = 打死一个 1000 血 4 甲的重型单位要多久（打不穿 / 备弹不够 = 打不死 = 0 分）
            //   需要几枪 = ceil(1000 / 单发对重甲的有效伤害)，耗时 = 需要枪数 ÷ 实战节奏（已摊换弹/过热）。
            var heavyShotsNeeded = heavyDamage > 0.01f
                ? Mathf.Ceil(HeavyTargetHp / heavyDamage)
                : float.PositiveInfinity;
            var canKillHeavy = !float.IsInfinity(heavyShotsNeeded) && heavyShotsNeeded <= stockTriggers;
            report.HeavyTtk = canKillHeavy && report.ClearRate > 0.01f
                ? Mathf.Min(heavyShotsNeeded / report.ClearRate, 9999f)
                : 0f;   // 0 = 打不死
            report.ScoreHeavyOutput = report.HeavyTtk > 0f
                ? ScoreCurve(report.HeavyTtk, TtkAnchors, false)
                : 0f;

            // 反重 = 打靶输出 80% + 破甲 20%（弱点已折进 TTK 的伤害里，不再单列一项）
            report.ScoreAntiHeavy = report.ScoreHeavyOutput * 0.80f
                                    + report.ScoreArmorBreak * 0.20f;
            // 拆巢：只看拆毁值，低于 30 直接 0 分
            report.ScoreDemolish = report.DemolishValue >= 30f
                ? ScoreCurve(report.DemolishValue, DemolishAnchors, false)
                : 0f;

            // 投掷物：爆发/持续/弹匣三项不计分，权重并进单发
            // （单发 3 + 爆发 3 + 持续 3 + 弹匣 3 = 12%）
            var perTriggerWeight = report.IsThrowable
                ? WeightPerTrigger + WeightBurstDps + WeightSustainDps + WeightMagazineDamage
                : WeightPerTrigger;
            var magazineWeight = report.IsThrowable ? 0f : WeightMagazineDamage;

            report.Overall = report.ScorePerTrigger * perTriggerWeight
                             + (report.IsThrowable
                                 ? 0f
                                 : report.ScoreBurstDps * WeightBurstDps + report.ScoreSustainDps * WeightSustainDps)
                             + report.ScoreMagazineDamage * magazineWeight
                             + report.ScoreTotalDamage * WeightTotalDamage
                             + report.ScoreAp * WeightAp
                             + report.ScoreArmorBreak * WeightArmorBreak
                             + report.ScoreWeakness * WeightWeakness
                             + report.ScoreAoe * WeightAoe
                             + report.ScoreHandling * WeightHandling
                             + report.ScoreBallistics * WeightBallistics
                             + report.ScoreSpecial * WeightSpecial
                             + report.ScoreClear * WeightClear
                             + report.ScoreAntiHeavy * WeightAntiHeavy
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
