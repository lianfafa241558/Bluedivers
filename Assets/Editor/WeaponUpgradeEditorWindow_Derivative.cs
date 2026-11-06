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

        /// <summary>
        /// 尾迹类专有属性 → 它附加到 <c>Damages[DamageIndex].DamageGroupExplosion</c> 的伤害类型。
        /// <para>⚠ 与 <c>WeaponUniqueAttributeApplier</c> 的 "恐惧尾迹 / 电磁尾迹 / 燃烧尾迹" 一一对应；
        /// 运行时代码改名字这边必须跟着改（改不到就静默漏算）。</para>
        /// </summary>
        private static readonly Dictionary<string, DamageTypeEnum> TrailTypeSpecials = new()
        {
            ["恐惧尾迹"] = DamageTypeEnum.Vertigo,
            ["电磁尾迹"] = DamageTypeEnum.Electric,
            ["燃烧尾迹"] = DamageTypeEnum.Burn,
        };

        /// <summary>
        /// 专有属性的累计值（名字 → 值）：底子注册的 <c>m_UniqueAttr</c> + 这次选中的改装 Special。
        /// <para>运行时口径：<c>AttrInfo</c> 以专有属性的 value 为 prime，改装 modifys 再叠上去取 Final；
        /// 这里按同序累加（Special 基本都是 Base/Add）。</para>
        /// </summary>
        private static Dictionary<string, float> CollectSpecialValues(SerializedObject serialized,
            Dictionary<string, float> loadoutSpecials)
        {
            var values = new Dictionary<string, float>();
            var unique = serialized?.FindProperty("m_UniqueAttr");
            for (var i = 0; unique != null && i < unique.arraySize; i++)
            {
                var item = unique.GetArrayElementAtIndex(i);
                Add(item.FindPropertyRelative("name")?.stringValue,
                    item.FindPropertyRelative("value")?.floatValue ?? 0f);
            }
            if (loadoutSpecials != null)
            {
                foreach (var kv in loadoutSpecials) Add(kv.Key, kv.Value);
            }
            return values;

            void Add(string name, float value)
            {
                if (string.IsNullOrEmpty(name)) return;
                values.TryGetValue(name, out var old);
                values[name] = old + value;
            }
        }

        /// <summary>
        /// 尾迹要附加的伤害类型（类型 → 系数）。0 视为"不附加"（运行时 <c>SetValue(type, 0)</c> 等于没加）。
        /// </summary>
        private static Dictionary<DamageTypeEnum, float> CollectTrailTypeValues(Dictionary<string, float> specials)
        {
            var values = new Dictionary<DamageTypeEnum, float>();
            if (specials == null) return values;
            foreach (var kv in specials)
            {
                if (!TrailTypeSpecials.TryGetValue(kv.Key, out var type)) continue;
                values.TryGetValue(type, out var old);
                values[type] = Mathf.Max(0f, old + kv.Value);
            }
            return values;
        }

        /// <summary>
        /// 尾迹的实际命中半径（米）：1 号伤害档的爆炸半径 + 「尾迹范围」加成。
        /// <para>运行时 <c>WeaponUniqueAttrApplier.ApplyTrailRange</c> 就是把这笔加成写到那一档上，
        /// 尾迹每拍用这个半径沿飞行线扫一圈 ⇒ 清杂的"一次能打几个目标"该按它算。</para>
        /// </summary>
        private static float TrailRadius(WeaponPlayerController weapon, Dictionary<string, float> specials)
        {
            if (weapon?.Damages == null || weapon.Damages.Count <= 1) return 0f;
            var data = weapon.Damages[1];
            if (data == null) return 0f;
            var bonus = 0f;
            specials?.TryGetValue("尾迹范围", out bonus);
            return data.GetAttr(WeaponAttrType.ExplosionRange) + bonus;
        }

        /// <summary>
        /// 把尾迹附加类型写进伤害组（语义同 <c>List.SetValue</c>：已有该类型就替换系数，没有就追加）。
        /// <para>⚠ <see cref="SKVP{K,V}"/> 是引用类型：替换必须造新对象，直接改 <c>Value</c> 会写回 prefab 资产。</para>
        /// </summary>
        private static List<SKVP<DamageTypeEnum, float>> WithTrailTypes(
            List<SKVP<DamageTypeEnum, float>> groups, Dictionary<DamageTypeEnum, float> extra)
        {
            if (extra == null || extra.Count == 0) return groups;

            var list = groups != null
                ? new List<SKVP<DamageTypeEnum, float>>(groups)
                : new List<SKVP<DamageTypeEnum, float>>();
            foreach (var kv in extra)
            {
                if (kv.Value <= 0.0001f) continue;
                var replaced = false;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] == null || list[i].Key != kv.Key) continue;
                    list[i] = new SKVP<DamageTypeEnum, float>(kv.Key, kv.Value);
                    replaced = true;
                    break;
                }
                if (!replaced) list.Add(new SKVP<DamageTypeEnum, float>(kv.Key, kv.Value));
            }
            return list;
        }

        /// <summary>面板文案：尾迹附加了哪些类型（"（附加恐惧×10/燃烧×3）"）。</summary>
        private static string TrailTypeText(Dictionary<DamageTypeEnum, float> extra)
        {
            if (extra == null || extra.Count == 0) return "";
            var parts = new List<string>(extra.Count);
            foreach (var kv in extra)
            {
                if (kv.Value <= 0.0001f) continue;
                var name = kv.Key switch
                {
                    DamageTypeEnum.Vertigo => "恐惧",
                    DamageTypeEnum.Electric => "电击",
                    DamageTypeEnum.Burn => "燃烧",
                    _ => kv.Key.ToString(),
                };
                parts.Add($"{name}×{kv.Value:0.##}");
            }
            return parts.Count > 0 ? "（附加" + string.Join("/", parts) + "）" : "";
        }

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
            public bool HasTrail;       // 含尾迹类效果（沿飞行线段整条打穿 ⇒ 清杂按穿透单位算）
        }

        /// <summary>
        /// 扫一遍 prefab 上的衍生物：子弹自带的 <see cref="SustainedEffect"/>（按 TickInterval 反复结算）
        /// 与命中特效里挂的临时武器。<b>不跑运行时逻辑</b>，只读序列化配置。
        /// </summary>
        private static DerivativeOutput CollectDerivative(DamageData data, int depth,
            WeaponBaseController ownerWeapon = null, Dictionary<DamageTypeEnum, float> trailExtra = null)
        {
            var output = new DerivativeOutput();
            if (data == null || depth > MaxDerivativeDepth) return output;

            var parts = new List<string>(2);

            // ① 子弹自带持续效果（爆裂手里剑：命中后原地每 0.5s 结算一次范围伤害；
            //    书记的惩戒：ElectromagneticTrailEffect 沿飞行轨迹扫一条线）
            var bulletGo = data.BulletPrefab != null ? data.BulletPrefab.gameObject : null;
            if (bulletGo != null)
            {
                foreach (var sustained in bulletGo.GetComponentsInChildren<SustainedEffect>(true))
                {
                    var sustainedSo = new SerializedObject(sustained);
                    var sustainedData = sustainedSo.FindProperty("DamageData");
                    var tickProp = sustainedData?.FindPropertyRelative("DamageExplosion");
                    var groups = sustainedData?.FindPropertyRelative("DamageGroupExplosion");
                    var tickValue = tickProp != null ? tickProp.floatValue : 0f;

                    // 尾迹类（ElectromagneticTrailEffect）：自己的 DamageData 是运行时用
                    // `weapon.Damages[DamageIndex]` 转过来的，prefab 上是空的 ⇒ 直接取武器那一档。
                    // ⚠ 设计上尾迹**不乘蓄力缩放**（尾迹档的数值就是原值），所以这里按档位原值计是准的；
                    //   另外它默认就有爆炸伤害与范围，电磁/恐惧/燃烧 Special 只是附加对应类型伤害。
                    List<SKVP<DamageTypeEnum, float>> fallbackGroups = null;
                    var isTrail = false;
                    if (tickValue <= 0f && ownerWeapon != null && ownerWeapon.Damages != null
                        && ownerWeapon.Damages.Count > 0)
                    {
                        var indexProp = sustainedSo.FindProperty("DamageIndex");   // 尾迹专属字段
                        if (indexProp != null)
                        {
                            var index = Mathf.Clamp(indexProp.intValue, 0, ownerWeapon.Damages.Count - 1);
                            var source = ownerWeapon.Damages[index];
                            if (source != null)
                            {
                                tickValue = source.GetAttr(WeaponAttrType.ExplosionDamage);
                                // 运行时尾迹的伤害组是 source 的拷贝，之后 WeaponUniqueAttributeApplier
                                // 还会把「恐惧/电磁/燃烧尾迹」的类型 SetValue 进去 ⇒ 评分要把这些补上，
                                // 否则装「燃烧尾迹3」尾迹伤害 ×4、控制成分全部看不见。
                                fallbackGroups = WithTrailTypes(source.DamageGroupExplosion, trailExtra);
                                isTrail = true;
                            }
                        }
                    }
                    if (tickValue <= 0f) continue;

                    var tickInterval = Mathf.Max(0.05f,
                        sustainedSo.FindProperty("TickInterval")?.floatValue ?? 0.5f);
                    // 存活时长优先取 LimitedLife；没有就退回子弹的 MaxLifeTime（== 运行时 CurrentLife）
                    var life = ReadActiveLife(bulletGo, data.MaxLifeTime);
                    // 首拍在 OnEnable 立刻结算，之后每 TickInterval 一拍
                    var ticks = Mathf.Max(1, Mathf.FloorToInt(life / tickInterval) + 1);
                    var damage = tickValue * ticks;
                    var realRatio = fallbackGroups != null ? RawGroupSum(fallbackGroups, false) : ReadGroupSum(groups, false);
                    var statusRatio = fallbackGroups != null ? RawGroupSum(fallbackGroups, true) : ReadGroupSum(groups, true);
                    var armorRatio = fallbackGroups != null
                        ? RawGroupValue(fallbackGroups, DamageTypeEnum.Destruction)
                        : ReadGroupValue(groups, DestructionKey);

                    output.Damage += damage;
                    output.RealDamage += damage * realRatio;
                    output.Status += damage * statusRatio;
                    output.ArmorDamage += damage * armorRatio;
                    output.Demolish = Mathf.Max(output.Demolish, fallbackGroups != null
                        ? 0
                        : sustainedData.FindPropertyRelative("demolishValue")?.intValue ?? 0);
                    output.Duration = Mathf.Max(output.Duration, life);
                    // 尾迹每拍沿"开火点 → 子弹位置"整条线段结算 ⇒ 清杂按穿透单位（一整条线）算
                    if (isTrail) output.HasTrail = true;
                    parts.Add($"{(isTrail ? "尾迹" : "持续")} {tickValue:0}×{ticks}" +
                              (isTrail ? TrailTypeText(trailExtra) : ""));
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
            var nested = CollectDerivative(temp.Damages[index], depth + 1, temp);
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
        /// Destruction 只削护甲、Catalyst 只推已有积蓄槽、骇入/恐惧/眩晕只加积蓄槽。
        /// <para>⚠ <b>Freeze / Burn / Toxicity 计血</b>：它们一边推积蓄槽一边照常掉血
        /// （HandleDamage 对应分支是 `AddAboGauge/AddOpposedAboGauge(...); return value;`），
        /// 别按"异常状态不掉血"想当然排掉。</para>
        /// <para>⚠ 改这里之前先看 <c>05UnitCore/Health/Health.cs</c> 的 switch —— 两边不同步会把 DPS 算成"虚假伤害"。</para>
        /// </summary>
        private static bool CountsToHealth(DamageTypeEnum type) => type switch
        {
            DamageTypeEnum.Destruction => false,
            DamageTypeEnum.Catalyst => false,
            DamageTypeEnum.Hacker => false,
            DamageTypeEnum.Terror => false,
            DamageTypeEnum.Vertigo => false,
            _ => true,   // 动能 / 爆炸 / 真实 / 燃烧 / 冰冻 / 电击 / 辐射 / 毒（+ 以后新增的未列出类型）
        };

        /// <summary>
        /// 异常状态成分（控制能力看它）。
        /// <para>⚠ 燃烧 / 辐射只算"持续掉血"（DOT），不算控制 —— 由用户口径排除，别顺手加回来。</para>
        /// </summary>
        private static bool IsStatusType(DamageTypeEnum type) => type is
            DamageTypeEnum.Toxicity or DamageTypeEnum.Freeze or DamageTypeEnum.Electric
            or DamageTypeEnum.Vertigo or DamageTypeEnum.Terror or DamageTypeEnum.Hacker
            or DamageTypeEnum.Catalyst;

        /// <summary>
        /// 元素成分（毒/燃烧/冰冻/电击/眩晕/恐惧/辐射/骇入/催化）：范围/爆炸包是**按部位各发一份**，
        /// 肢体那份会被 <c>Damageable</c> 丢掉（`AreaHit &amp;&amp; !isMain`），只有主体吃 ⇒ 头部不吃爆炸里的这些。
        /// <para>⚠ 白名单必须与 <c>05UnitCore/Damage/Damageable.cs</c> 的 `IsElementType` 逐项同步；
        /// 别改成"除动能/爆炸/溶解外都算元素"—— Destruction（肢体也有护甲）会被误丢。</para>
        /// </summary>
        private static bool IsElementType(DamageTypeEnum type) => type is
            DamageTypeEnum.Toxicity or DamageTypeEnum.Burn or DamageTypeEnum.Freeze
            or DamageTypeEnum.Electric or DamageTypeEnum.Vertigo or DamageTypeEnum.Terror
            or DamageTypeEnum.Radiation or DamageTypeEnum.Hacker or DamageTypeEnum.Catalyst;

        /// <summary>
        /// 爆炸伤害组里**能在肢体（含头部）上结算**的计血系数和：元素成分已被剥离。
        /// <para>空组仍按"整笔动能"返回 1（与 <see cref="HealthCoefficient"/> 同一条兜底）。</para>
        /// </summary>
        private static float LimbHealthCoefficient(WeaponPlayerController weapon, DamageData data)
        {
            var any = 0f;
            var health = 0f;
            foreach (var type in AllDamageTypes)
            {
                var value = GroupCoefficient(weapon, data, type, true);
                if (Mathf.Approximately(value, 0f)) continue;
                any += value;
                if (IsElementType(type)) continue;
                if (CountsToHealth(type)) health += value;
            }
            return Mathf.Approximately(any, 0f) ? 1f : Mathf.Max(0f, health);
        }

        /// <summary>
        /// 伤害类型系数在 cfg 里的属性键：直击组从 200 起、爆炸组从 220 起，
        /// 两段顺序与 <see cref="DamageTypeEnum"/> 完全一致（Gun=200/220 … Catalyst=212/232）。
        /// </summary>
        private static WeaponAttrType GroupAttrOf(DamageTypeEnum type, bool explosion)
            => (WeaponAttrType)(200 + (int)type + (explosion ? 20 : 0));

        /// <summary>
        /// 某个伤害类型当前的系数：**cfg 优先**，cfg 里没这个键才退回 DamageData 的组。
        /// <para>⚠ 必须 cfg 优先：升级/模组改的就是 `DamageTypeBurn` / `ExplosionTypeBurn` 这类属性
        /// （运行时 `InitAttribute` 再把 cfg 回写进 DamageData）。只读组列表的话，
        /// 装"废热回导"（直击燃烧 +1.2、爆炸燃烧 +1.2）评分一点变化都没有 —— 2026-11-06 实测踩到。</para>
        /// </summary>
        private static float GroupCoefficient(WeaponPlayerController weapon, DamageData data, DamageTypeEnum type,
            bool explosion)
            => AttrOrData(weapon, data, GroupAttrOf(type, explosion));

        /// <summary>
        /// 伤害成分里"计入血量"的系数之和。一个系数都没配（组为空）= 整笔按动能结算
        /// （Damageable 的 `DamageGroups.Count == 0 ⇒ Gun` 分支）⇒ 1。
        /// </summary>
        private static float HealthCoefficient(WeaponPlayerController weapon, DamageData data, bool explosion)
        {
            var any = 0f;
            var health = 0f;
            foreach (var type in AllDamageTypes)
            {
                var value = GroupCoefficient(weapon, data, type, explosion);
                if (Mathf.Approximately(value, 0f)) continue;
                any += value;
                if (CountsToHealth(type)) health += value;
            }
            return Mathf.Approximately(any, 0f) ? 1f : Mathf.Max(0f, health);
        }

        /// <summary>
        /// 原始列表版的系数和（不走 cfg）：给"武器第 2 档"这类只存在于 <c>Damages[i]</c> 里的数据用
        /// （尾迹效果运行时是从 <c>weapon.Damages[DamageIndex]</c> 转的，prefab 上没有对应 cfg 键）。
        /// </summary>
        private static float RawGroupSum(List<SKVP<DamageTypeEnum, float>> groups, bool statusOnly)
        {
            if (groups == null || groups.Count == 0) return statusOnly ? 0f : 1f;
            var sum = 0f;
            for (var i = 0; i < groups.Count; i++)
            {
                var item = groups[i];
                if (item == null) continue;
                if (statusOnly ? IsStatusType(item.Key) : CountsToHealth(item.Key)) sum += item.Value;
            }
            return Mathf.Max(0f, sum);
        }

        /// <summary>原始列表版：取某个伤害类型的系数（没配 = 0）。</summary>
        private static float RawGroupValue(List<SKVP<DamageTypeEnum, float>> groups, DamageTypeEnum key)
        {
            if (groups == null) return 0f;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null && groups[i].Key == key) return groups[i].Value;
            }
            return 0f;
        }

        /// <summary>伤害成分里"异常状态（控制）"的系数之和。</summary>
        private static float StatusCoefficient(WeaponPlayerController weapon, DamageData data, bool explosion)
        {
            var status = 0f;
            foreach (var type in AllDamageTypes)
            {
                if (IsStatusType(type)) status += GroupCoefficient(weapon, data, type, explosion);
            }
            return Mathf.Max(0f, status);
        }

        /// <summary>伤害类型全集（顺序与 <see cref="GroupAttrOf"/> 的属性段一致）。</summary>
        private static readonly DamageTypeEnum[] AllDamageTypes =
            (DamageTypeEnum[])System.Enum.GetValues(typeof(DamageTypeEnum));

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
