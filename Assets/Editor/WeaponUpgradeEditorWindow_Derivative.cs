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
    /// 衍生物折算（持续效果 / 临时武器 / 地面火焰）与伤害成分口径（对齐 Health.HandleDamage）。
    /// </summary>
    public partial class WeaponUpgradeEditorWindow
    {
        // ========== 衍生物折算 ==========
        // 有些武器的输出不在 Damages 里：子弹命中后留下的 SustainedEffect 会持续 tick，
        // 命中特效里还可能生成临时武器（WeaponTemporaryController）自己开火。
        // 这里把两者折算成"一次扣扳机额外造成的伤害/破甲"，并入单发与破甲两项。

        /// <summary>递归上限：临时武器的子弹/特效再带衍生物时只往下挖两层，挡住互相引用。</summary>
        private const int MaxDerivativeDepth = 2;

        private static readonly int DestructionKey = (int)DamageTypeEnum.Destruction;

        /// <summary>衍生物折算结果，口径统一为"一次扣扳机"。</summary>
        private struct DerivativeOutput
        {
            public float Damage;        // 额外总伤（全部成分）
            public float RealDamage;    // 其中真的扣血的部分
            public float Status;        // 其中异常状态（控制）成分
            public float ArmorDamage;   // 其中护甲破坏值
            public float Demolish;      // 其中最大拆毁值
            public float Duration;      // 衍生物存活时长
            public string Text;         // 面板文案（"持续 25×9" / "扫射 64×250"）
        }

        /// <summary>
        /// 扫一遍 prefab 上的衍生物：子弹自带的 <see cref="SustainedEffect"/>（按 TickInterval 反复结算）
        /// 与命中特效里挂的临时武器。<b>不跑运行时逻辑</b>，只读序列化配置。
        /// </summary>
        private static DerivativeOutput CollectDerivative(DamageData data, int depth)
        {
            var output = new DerivativeOutput();
            if (data == null || depth > MaxDerivativeDepth) return output;

            var parts = new List<string>(2);

            // ① 子弹自带持续效果（爆裂手里剑：命中后原地每 0.5s 结算一次范围伤害）
            var bulletGo = data.BulletPrefab != null ? data.BulletPrefab.gameObject : null;
            if (bulletGo != null)
            {
                foreach (var sustained in bulletGo.GetComponentsInChildren<SustainedEffect>(true))
                {
                    var sustainedSo = new SerializedObject(sustained);
                    var sustainedData = sustainedSo.FindProperty("DamageData");
                    var tickProp = sustainedData?.FindPropertyRelative("DamageExplosion");
                    if (tickProp == null || tickProp.floatValue <= 0f) continue;

                    var tickInterval = Mathf.Max(0.05f,
                        sustainedSo.FindProperty("TickInterval")?.floatValue ?? 0.5f);
                    // 存活时长优先取 LimitedLife；没有就退回子弹的 MaxLifeTime（== 运行时 CurrentLife）
                    var life = ReadActiveLife(bulletGo, data.MaxLifeTime);
                    // 首拍在 OnEnable 立刻结算，之后每 TickInterval 一拍
                    var ticks = Mathf.Max(1, Mathf.FloorToInt(life / tickInterval) + 1);
                    var damage = tickProp.floatValue * ticks;
                    var groups = sustainedData.FindPropertyRelative("DamageGroupExplosion");
                    output.Damage += damage;
                    output.RealDamage += damage * ReadGroupSum(groups, false);
                    output.Status += damage * ReadGroupSum(groups, true);
                    output.ArmorDamage += damage * ReadGroupValue(groups, DestructionKey);
                    output.Demolish = Mathf.Max(output.Demolish,
                        sustainedData.FindPropertyRelative("demolishValue")?.intValue ?? 0);
                    output.Duration = Mathf.Max(output.Duration, life);
                    parts.Add($"持续 {tickProp.floatValue:0}×{ticks}");
                }
            }

            // ② 命中特效里的衍生物：地面火焰（GroundFireEffect : SustainedEffect）与临时武器
            //    （战术铅爆雷：落点生成扫射枪，自己开火到超时）
            if (data.ImpactVfx != null)
            {
                var vfxLife = ReadEffectLife(data.ImpactVfx, 0f);

                // 燃烧弹的落点火：每秒 TickInterval 拍一次范围灼烧，靠粒子系统时长回收（没有 LimitedLife）
                foreach (var sustained in data.ImpactVfx.GetComponentsInChildren<SustainedEffect>(true))
                {
                    var sustainedSo = new SerializedObject(sustained);
                    var sustainedData = sustainedSo.FindProperty("DamageData");
                    var tickProp = sustainedData?.FindPropertyRelative("DamageExplosion");
                    if (tickProp == null || tickProp.floatValue <= 0f) continue;

                    var tickInterval = Mathf.Max(0.05f,
                        sustainedSo.FindProperty("TickInterval")?.floatValue ?? 0.5f);
                    var life = ReadEffectLife(sustained.gameObject, vfxLife);
                    var ticks = Mathf.Max(1, Mathf.FloorToInt(life / tickInterval) + 1);
                    var damage = tickProp.floatValue * ticks;
                    var groups = sustainedData.FindPropertyRelative("DamageGroupExplosion");
                    output.Damage += damage;
                    output.RealDamage += damage * ReadGroupSum(groups, false);
                    output.Status += damage * ReadGroupSum(groups, true);
                    output.ArmorDamage += damage * ReadGroupValue(groups, DestructionKey);
                    output.Demolish = Mathf.Max(output.Demolish,
                        sustainedData.FindPropertyRelative("demolishValue")?.intValue ?? 0);
                    output.Duration = Mathf.Max(output.Duration, life);
                    parts.Add($"{(sustained is GroundFireEffect ? "灼烧" : "持续")} {tickProp.floatValue:0}×{ticks}");
                }

                foreach (var temp in data.ImpactVfx.GetComponentsInChildren<WeaponBaseController>(true))
                {
                    var tempOutput = MeasureTemporaryWeapon(temp, vfxLife, depth);
                    if (tempOutput.Damage <= 0f) continue;
                    output.Damage += tempOutput.Damage;
                    output.RealDamage += tempOutput.RealDamage;
                    output.Status += tempOutput.Status;
                    output.ArmorDamage += tempOutput.ArmorDamage;
                    output.Demolish = Mathf.Max(output.Demolish, tempOutput.Demolish);
                    output.Duration = Mathf.Max(output.Duration, tempOutput.Duration);
                    parts.Add(tempOutput.Text);
                }
            }

            output.Text = string.Join("、", parts);
            return output;
        }

        /// <summary>
        /// 临时武器折算：弹药与存活时长谁先到算谁，输出 = 射击次数 × 每次伤害。
        /// <para>⚠ 临时武器不吃武器的蓄力/改装，读它自己 prefab 上的 cfg 与 Damages。</para>
        /// </summary>
        private static DerivativeOutput MeasureTemporaryWeapon(WeaponBaseController temp, float life, int depth)
        {
            var output = new DerivativeOutput();
            var serialized = new SerializedObject(temp);
            var damages = serialized.FindProperty("Damages");
            if (damages == null || damages.arraySize == 0) return output;

            var index = Mathf.Clamp(serialized.FindProperty("UseDamageIndex")?.intValue ?? 0,
                0, damages.arraySize - 1);
            var element = damages.GetArrayElementAtIndex(index);
            var direct = element.FindPropertyRelative("DamageDirect")?.floatValue ?? 0f;
            var explosion = element.FindPropertyRelative("DamageExplosion")?.floatValue ?? 0f;
            var perProjectile = direct + explosion;
            if (perProjectile <= 0f) return output;

            var muzzles = GetManyMuzzleCount(serialized);
            var bullets = Mathf.Clamp(temp.AttrFinal(WeaponAttrType.BulletsPerShot, 1).RawInt, 1, 64);
            var interval = Mathf.Max(0.02f, temp.AttrFinal(WeaponAttrType.ShootInterval).RawFloat);
            var cost = Mathf.Max(1, temp.AttrFinal(WeaponAttrType.ShootCost, muzzles).RawInt);
            var stock = Mathf.Max(0f, temp.AttrFinal(WeaponAttrType.Magazine).RawFloat)
                        + Mathf.Max(0f, temp.AttrFinal(WeaponAttrType.Ammo).RawFloat);

            // 射击次数：弹药 / 消耗 与 存活时长 / 间隔 取小；两者都没配就按 1 次
            var shotsByStock = stock > 0f ? stock / cost : float.MaxValue;
            var shotsByLife = life > 0f ? life / interval : float.MaxValue;
            var shots = Mathf.Min(Mathf.Min(shotsByStock, shotsByLife), 256f);
            if (shots <= 0f || float.IsInfinity(shots)) shots = 1f;

            // 临时武器的子弹也可能再带衍生物（递归，靠 depth 兜住互引）
            var nested = CollectDerivative(temp.Damages[index], depth + 1);
            var directGroups = element.FindPropertyRelative("DamageGroupDirect");
            var explosionGroups = element.FindPropertyRelative("DamageGroupExplosion");
            var perTrigger = perProjectile * bullets * muzzles + nested.Damage;
            // 同一次扣扳机里，各成分按 Health.HandleDamage 的口径拆成"扣血的/只控制/只破甲"
            var realPerTrigger = (direct * ReadGroupSum(directGroups, false)
                                  + explosion * ReadGroupSum(explosionGroups, false)) * bullets * muzzles
                                 + nested.RealDamage;
            var statusPerTrigger = (direct * ReadGroupSum(directGroups, true)
                                    + explosion * ReadGroupSum(explosionGroups, true)) * bullets * muzzles
                                   + nested.Status;
            var armorPerTrigger = (direct * ReadGroupValue(directGroups, DestructionKey)
                                   + explosion * ReadGroupValue(explosionGroups, DestructionKey))
                                  * bullets * muzzles + nested.ArmorDamage;

            output.Damage = shots * perTrigger;
            output.RealDamage = shots * realPerTrigger;
            output.Status = shots * statusPerTrigger;
            output.ArmorDamage = shots * armorPerTrigger;
            output.Demolish = Mathf.Max(element.FindPropertyRelative("demolishValue")?.intValue ?? 0, nested.Demolish);
            output.Duration = life > 0f ? life : shots * interval;
            output.Text = $"扫射 {shots:0}×{perTrigger:0}";
            return output;
        }

        /// <summary>衍生物存活时长：优先 LimitedLife.LiftTime，没有则用兜底值（子弹给 MaxLifeTime）。</summary>
        private static float ReadActiveLife(GameObject root, float fallback)
        {
            if (root == null) return fallback;
            var limited = root.GetComponentInChildren<LimitedLife>(true);
            if (limited == null) return fallback;
            var life = new SerializedObject(limited).FindProperty("LiftTime")?.floatValue ?? 0f;
            return life > 0f ? life : fallback;
        }

        /// <summary>
        /// 特效类衍生物的存活时长：LimitedLife → 粒子系统 <c>lengthInSec</c> → 兜底。
        /// <para>⚠ 地面火焰（GroundFireEffect）没有 LimitedLife，由 VFXManager 按 <c>ParticleSystem.IsAlive</c> 回收
        /// ⇒ 只能读粒子系统时长（Vfx_GroundFlame4 = 10s）。这个兜底只给命中特效用，别套到子弹上（子弹有尾迹 PS）。</para>
        /// </summary>
        private static float ReadEffectLife(GameObject root, float fallback)
        {
            var life = ReadActiveLife(root, 0f);
            if (life > 0f) return life;
            var ps = root != null ? root.GetComponentInChildren<ParticleSystem>(true) : null;
            if (ps != null)
            {
                var length = new SerializedObject(ps).FindProperty("lengthInSec")?.floatValue ?? 0f;
                if (length > 0f) return length;
            }
            return fallback;
        }

        /// <summary>从 SKVP 列表里取某个伤害类型的系数（没配 = 0）。</summary>
        private static float ReadGroupValue(SerializedProperty list, int key)
        {
            if (list == null) return 0f;
            for (var i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                var keyProp = item.FindPropertyRelative("Key");
                if (keyProp == null || keyProp.intValue != key) continue;
                return item.FindPropertyRelative("Value")?.floatValue ?? 0f;
            }
            return 0f;
        }

        // ========== 伤害成分口径（与 Health.HandleDamage 对齐）==========

        /// <summary>
        /// 这个成分会不会真的扣血 —— 与 <c>Health.HandleDamage</c> 返回 0 的分支一一对应：
        /// Destruction 只削护甲、Catalyst 只推已有积蓄槽、Freeze/毒/骇入/恐惧/眩晕只加积蓄槽。
        /// <para>⚠ 改这里之前先看 <c>05UnitCore/Health/Health.cs</c> 的 switch —— 两边不同步会把 DPS 算成"虚假伤害"。</para>
        /// </summary>
        private static bool CountsToHealth(DamageTypeEnum type) => type switch
        {
            DamageTypeEnum.Destruction => false,
            DamageTypeEnum.Catalyst => false,
            DamageTypeEnum.Freeze => false,
            DamageTypeEnum.Toxicity => false,
            DamageTypeEnum.Hacker => false,
            DamageTypeEnum.Terror => false,
            DamageTypeEnum.Vertigo => false,
            _ => true,   // 动能 / 爆炸 / 真实 / 燃烧 / 电击 / 辐射（+ 以后新增的未列出类型）
        };

        /// <summary>
        /// 异常状态成分（控制能力看它）。
        /// <para>⚠ 燃烧 / 辐射只算"持续掉血"（DOT），不算控制 —— 由用户口径排除，别顺手加回来。</para>
        /// </summary>
        private static bool IsStatusType(DamageTypeEnum type) => type is
            DamageTypeEnum.Toxicity or DamageTypeEnum.Freeze or DamageTypeEnum.Electric
            or DamageTypeEnum.Vertigo or DamageTypeEnum.Terror or DamageTypeEnum.Hacker
            or DamageTypeEnum.Catalyst;

        /// <summary>伤害成分里"计入血量"的系数之和。空组 = 整笔按动能结算（Damageable 的第一个分支）⇒ 1。</summary>
        private static float HealthCoefficient(List<SKVP<DamageTypeEnum, float>> groups)
        {
            if (groups == null || groups.Count == 0) return 1f;
            var sum = 0f;
            for (var i = 0; i < groups.Count; i++)
            {
                var item = groups[i];
                if (item != null && CountsToHealth(item.Key)) sum += item.Value;
            }
            return Mathf.Max(0f, sum);
        }

        /// <summary>伤害成分里"异常状态（控制）"的系数之和。</summary>
        private static float StatusCoefficient(List<SKVP<DamageTypeEnum, float>> groups)
        {
            if (groups == null) return 0f;
            var sum = 0f;
            for (var i = 0; i < groups.Count; i++)
            {
                var item = groups[i];
                if (item != null && IsStatusType(item.Key)) sum += item.Value;
            }
            return Mathf.Max(0f, sum);
        }

        /// <summary>序列化版：组内"计入血量"或"异常状态"的系数之和（空组同 <see cref="HealthCoefficient"/>）。</summary>
        private static float ReadGroupSum(SerializedProperty list, bool statusOnly)
        {
            if (list == null || list.arraySize == 0) return statusOnly ? 0f : 1f;
            var sum = 0f;
            for (var i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                var keyProp = item.FindPropertyRelative("Key");
                var valueProp = item.FindPropertyRelative("Value");
                if (keyProp == null || valueProp == null) continue;
                var type = (DamageTypeEnum)keyProp.intValue;
                if (statusOnly ? IsStatusType(type) : CountsToHealth(type)) sum += valueProp.floatValue;
            }
            return Mathf.Max(0f, sum);
        }
    }
}
