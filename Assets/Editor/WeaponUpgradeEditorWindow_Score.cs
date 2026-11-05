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
    /// 评分页：权重表 / 锚点 / 明细行 / 对比表 / 评级（只画，不算）。
    /// </summary>
    public partial class WeaponUpgradeEditorWindow
    {
        // ========== 评分系统 ==========
        // 口径：单目标、无弱点/易伤/队友增益、爆炸按中心满伤、不含命中率。
        // 只用于横向比"一把枪的底子"，不参与任何运行时逻辑 —— 别把它接进玩法。

        // 权重合计必须 = 1：改了记得同步 DrawScoreHeader 那两行提示文案
        // ⚠ 单发/穿甲/破甲/范围/弹匣这些是"综合能力"的输入项，权重必须压小，否则同一个数据被算两遍；
        //   真正的主导权交给清杂/反重/拆巢/控制（合计 58%，反重 20 里还含 60% 的有效伤害）。
        private const float WeightBurstDps = 0.03f;      // 输出（参考值）
        private const float WeightSustainDps = 0.03f;
        private const float WeightPerTrigger = 0.03f;
        private const float WeightMagazineDamage = 0.03f; // 弹药
        private const float WeightTotalDamage = 0.03f;
        private const float WeightAp = 0.02f;             // 穿透（反重的输入）
        private const float WeightArmorBreak = 0.03f;     // 破甲（单列项；同时是反重的 20% 输入）
        private const float WeightWeakness = 0.03f;
        private const float WeightAoe = 0.03f;            // 范围（清杂的输入）
        private const float WeightHandling = 0.08f;       // 人机工学（散布 + 后坐力，各 50%）
        private const float WeightBallistics = 0.04f;     // 弹道（弹速 + 有效射程）
        private const float WeightSpecial = 0.04f;        // 特殊标签
        private const float WeightClear = 0.20f;          // 综合能力：清杂 / 反重 / 拆巢 / 控制
        private const float WeightAntiHeavy = 0.20f;
        private const float WeightDemolish = 0.12f;
        private const float WeightControl = 0.06f;        // 控制（异常状态成分）

        // 人机工学内部配比：子弹散布角 / 后坐力 各占一半（**加权平均**，不是相乘）
        private const float HandlingSpreadWeight = 0.5f;
        private const float HandlingRecoilWeight = 0.5f;

        // 差值表的列宽：字比上方明细大一号，列宽要跟着放宽，否则武器名会被裁
        private const float CompareLabelWidth = 110f;
        private const float CompareColWidth = 150f;

        private const int CompareCellFontSize = 13;
        private const int CompareDeltaFontSize = 14;

        // 锚点成对 [数值, 分]，数值列必须递增。DPS/弹药类跨一个数量级 ⇒ 段内按 log10 插值
        // （线性插值会把中间档全挤在 0~5 分）；穿甲本身就是档位值，用线性。调平衡直接改这几张表。
        private static readonly float[] DpsAnchors =
            { 30f, 0f, 80f, 18f, 150f, 38f, 250f, 60f, 420f, 80f, 700f, 93f, 1100f, 100f };

        private static readonly float[] SustainAnchors =
            { 15f, 0f, 40f, 20f, 70f, 40f, 110f, 60f, 180f, 78f, 320f, 92f, 600f, 100f };

        private static readonly float[] MagazineDamageAnchors =
            { 150f, 0f, 300f, 22f, 450f, 42f, 600f, 60f, 1000f, 78f, 2000f, 92f, 4000f, 100f };

        private static readonly float[] TotalDamageAnchors =
            { 1200f, 0f, 2600f, 22f, 4000f, 42f, 6000f, 60f, 12000f, 78f, 25000f, 92f, 60000f, 100f };

        private static readonly float[] ApAnchors =
            { 0f, 10f, 1f, 35f, 2f, 48f, 3f, 60f, 4f, 72f, 5f, 82f, 6f, 91f, 7f, 100f };

        // 破甲（每次扣扳机的护甲破坏值）：敌人护甲 30~500，单发 200 上下就算能稳定扒甲
        private static readonly float[] ArmorBreakAnchors =
            { 0f, 0f, 20f, 18f, 50f, 40f, 100f, 62f, 200f, 82f, 350f, 94f, 600f, 100f };

        // 控制（每次扣扳机的异常状态成分值）：毒/燃烧/冰冻/电击/眩晕/恐惧/辐射/骇入/催化
        private static readonly float[] StatusAnchors =
            { 0f, 0f, 10f, 25f, 25f, 45f, 50f, 65f, 100f, 82f, 200f, 93f, 400f, 100f };

        // 单发（每次扣扳机总伤）：狙击/蓄力靠它拉分，只有 DPS 会把它们全判死
        private static readonly float[] PerTriggerAnchors =
            { 8f, 0f, 20f, 20f, 40f, 40f, 80f, 60f, 150f, 78f, 300f, 92f, 600f, 100f };

        private static readonly float[] WeaknessAnchors =
            { 0f, 0f, 0.05f, 25f, 0.1f, 48f, 0.15f, 66f, 0.2f, 80f, 0.3f, 93f, 0.5f, 100f };

        private static readonly float[] ExplosionDamageAnchors =
            { 4f, 0f, 10f, 22f, 25f, 45f, 60f, 70f, 120f, 88f, 250f, 100f };

        private static readonly float[] ExplosionRangeAnchors =
            { 0f, 0f, 1f, 25f, 2f, 45f, 3f, 65f, 5f, 85f, 8f, 100f };

        // ---- 综合能力改成"能打死什么"的实操口径 ----

        /// <summary>清杂的靶子：杂兵 50 血、护甲 0（所以不吃 AP 减免）。</summary>
        private const float ClearMobHp = 50f;
        /// <summary>清杂的观察窗口（秒）：口径 = 10 秒内能击杀多少只杂兵。</summary>
        private const float ClearWindowSeconds = 10f;
        /// <summary>反重的靶子：重型单位 1000 血、护甲等级 4。</summary>
        private const float HeavyTargetHp = 1000f;

        // 清杂：10 秒击杀数 → 分（线性：25 只 = 满分，< 5 只 = 0）
        private static readonly float[] ClearKillAnchors =
            { 5f, 0f, 25f, 100f };

        // 反重：击杀 1000 血 4 甲所需秒数 → 分（线性：10s = 满分，60s = 0；打不死 = 0）
        private static readonly float[] TtkAnchors =
            { 10f, 100f, 60f, 0f };

        // 下面两组是"越低越好"：锚点分数递减，用线性插值（值里有 0，log 会算出 NaN）
        // 散布：0° = 100 分、每多 1° 掉 20 分，5° 起直接 0 分（用户口径：差异要拉大）
        private static readonly float[] SpreadAnchors =
            { 0f, 100f, 1f, 80f, 2f, 60f, 3f, 40f, 4f, 20f, 5f, 0f };

        private static readonly float[] RecoilForceAnchors =
            { 0f, 100f, 0.3f, 88f, 0.6f, 72f, 1f, 58f, 1.4f, 38f, 2f, 15f };

        private static readonly float[] BulletSpeedAnchors =
            { 15f, 0f, 40f, 25f, 80f, 45f, 160f, 65f, 350f, 85f, 800f, 97f, 1500f, 100f };

        private static readonly float[] RangeAnchors =
            { 15f, 0f, 40f, 25f, 80f, 45f, 150f, 62f, 300f, 80f, 600f, 93f, 1200f, 100f };

        private static readonly float[] TagAnchors =
            { 0f, 0f, 1f, 45f, 2f, 65f, 3f, 80f, 4f, 90f, 5f, 100f };

        // 拆毁值：低于 30 直接 0 分（打不掉任何巢），30 起算
        private static readonly float[] DemolishAnchors =
            { 30f, 40f, 60f, 65f, 100f, 80f, 200f, 95f, 400f, 100f };

        private void DrawScoreContent(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            var contentRect = new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12);
            var report = BuildScoreReport(_currentController, _serializedWeapon, 1f);
            if (!report.Valid)
            {
                GUI.Label(new Rect(contentRect.x, contentRect.y + 20, contentRect.width, 24),
                    "该武器没有伤害配置（Damages 为空），无法评分", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // 蓄力武器再算一份"最低可发射蓄力"：同一行画两根条，分数取两者平均
            var minRatio = GetMinChargeRatio(_currentController);
            var minReport = report.UseCharge && minRatio < 0.999f
                ? BuildScoreReport(_currentController, _serializedWeapon, minRatio)
                : default;

            GUILayout.BeginArea(contentRect);
            _scoreScroll = EditorGUILayout.BeginScrollView(_scoreScroll);

            DrawScoreBlock(report, minReport, true);

            EditorGUILayout.Space(8);
            DrawCompareSection(report, minReport);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("计算口径", EditorStyles.boldLabel);

            // 把直击/爆炸拆开列出来：只给一个总数会让人怀疑爆炸没算进去
            var chargeNote = report.ChargeScale != 1f || report.ChargeRangeScale != 1f
                ? $"（满蓄：直击/爆炸 ×{report.ChargeScale:0.##}、爆炸半径 ×{report.ChargeRangeScale:0.##}）"
                : "";
            if (minReport.Valid)
            {
                chargeNote += $"　明细取满蓄，分数取满蓄(×{report.ChargeScale:0.##})与最低蓄力(×{minReport.ChargeScale:0.##})的均值";
            }
            var rangeText = report.ChargeRangeScale != 1f
                ? $"，满蓄 ×{report.ChargeRangeScale:0.##}"
                : "";
            var rangeNote = report.ExplosionRange > 0f
                ? $"（半径 {report.ExplosionRange:0.#}m{rangeText}，按爆心满伤）"
                : "";
            EditorGUILayout.LabelField(
                $"直击 {report.DirectDamage:0.##} + 爆炸 {report.ExplosionDamage:0.##}{rangeNote}" +
                $" = 单发 {report.DamagePerProjectile:0.##}{chargeNote}", EditorStyles.miniLabel);

            var muzzleNote = report.Muzzles > 1 ? $"（{report.Muzzles} 个齐射点位）" : "";
            var derivativeNote = report.DerivativeDamage > 0f ? $" + 衍生物 {report.DerivativeDamage:0}" : "";
            EditorGUILayout.LabelField(
                $"单发 {report.DamagePerProjectile:0.##} × {report.ProjectilesPerTrigger} 弹丸{muzzleNote}{derivativeNote}" +
                $" = 每次扣扳机 {report.DamagePerTrigger:0.##}", EditorStyles.miniLabel);
            if (report.DerivativeDamage > 0f)
            {
                // 衍生物单独列一行：它不在 Damages 里，混进"单发"里不写清楚会让人以为算错了
                EditorGUILayout.LabelField(
                    $"衍生物：{report.DerivativeText} ⇒ 额外总伤 {report.DerivativeDamage:0}" +
                    $"、破甲 {report.DerivativeArmorDamage:0}（按 {report.DerivativeDuration:0.#}s 存活折算）",
                    EditorStyles.miniLabel);
            }
            // 只有部分成分真的扣血时把两者都摆出来：DPS / 总伤 / 清杂 / 反重 用的都是右边这个数
            if (report.RealDamageRatio < 0.999f)
            {
                EditorGUILayout.LabelField(
                    $"其中计入血量的成分占 {report.RealDamageRatio * 100f:0.#}% ⇒ 有效单发 {report.RealDamagePerTrigger:0.##}" +
                    $"、控制值 {report.StatusPower:0}（Destruction 只削甲、Catalyst/Freeze/毒/骇入/恐惧/眩晕只加积蓄槽）",
                    EditorStyles.miniLabel);
            }
            if (report.IsBeamSplash)
            {
                // 光束的"范围"不是爆炸伤害，而是命中半径：判断口径来自 ProjectileLaser.useExplosionRange
                var penetrateNote = report.PenetratesUnits ? "、且穿透单位（沿途每个敌人各吃一次直击）" : "";
                EditorGUILayout.LabelField(
                    $"光束范围：命中半径 {report.ExplosionRange:0.#}m{penetrateNote}" +
                    $" ⇒ 一次节拍约打中 {report.MultiTargetFactor:0.#} 个目标（{report.RealDamagePerTrigger:0.##}" +
                    $"→{report.EffectiveDamagePerTrigger:0.##}）；只计入清杂，DPS/总伤/反重都按单体",
                    EditorStyles.miniLabel);
            }
            else if (report.PenetratesUnits)
            {
                EditorGUILayout.LabelField(
                    $"穿透单位：子弹打穿第一个目标继续往后 ⇒ 清杂按 {report.MultiTargetFactor:0.#} 个目标折算" +
                    $"（{report.RealDamagePerTrigger:0.##}→{report.EffectiveDamagePerTrigger:0.##}）；DPS/总伤/反重仍按单体",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.LabelField(
                $"射击间隔 {report.Interval:0.###}s · 换弹 {report.Reload:0.##}s · 每次射击消耗 {report.ShootCost}",
                EditorStyles.miniLabel);
            // 清杂用的节奏要把真空期摊进去，单独写一行，免得和上面的"射击间隔"混淆
            var heatPauseNote = report.HeatPauses > 0 ? $"（含 {report.HeatPauses} 次过热）" : "";
            EditorGUILayout.LabelField(
                $"清杂节奏：一匣 {report.MagazineShots:0.#} 发 · 打空 {report.DumpTime:0.#}s{heatPauseNote}" +
                $" + 换弹 {report.Reload:0.##}s ⇒ 实际 {report.ClearRate:0.##} 次/s" +
                $" ⇒ 10 秒击杀 {report.ClearKillsIn10s:0.#} 只" +
                $"（每次扣扳机 {report.Interval:0.###}s 只是开火时）",
                EditorStyles.miniLabel);

            var cost = Mathf.Max(1, report.ShootCost);
            if (!report.HasHeat)
            {
                EditorGUILayout.LabelField(
                    $"弹匣 {report.Magazine:0} / 后备 {report.Ammo:0} ⇒ 一匣可打 {report.MagazineShots:0.#} 次、" +
                    $"全部备弹 {((report.Magazine + report.Ammo) / cost):0.#} 次", EditorStyles.miniLabel);
            }
            else if (report.BurstShots <= 0f)
            {
                // 有效热量 <= 0：散热比加热快，连射永远到不了 100
                EditorGUILayout.LabelField(
                    $"热量：每发 +{report.HeatPerShot:0.##} 热，但散热 {report.CoolSpeed:0}/s ≥ 加热速度" +
                    $" ⇒ 连射不会过热，按整匣 {report.MagazineDamage:0} 算", EditorStyles.miniLabel);
            }
            else
            {
                // 热量武器的"弹匣"是 100 热：一轮几发、整池几轮、打空多久，三件事分开写清楚
                EditorGUILayout.LabelField(
                    $"热量：每发 +{report.HeatPerShot:0.##} 热（满 100，连射净攒 {report.EffectiveHeat:0.##}）" +
                    $" ⇒ 到过热 {report.BurstShots:0} 发 = 一轮总伤 {report.MagazineDamage:0}" +
                    $" · 过热 {report.OverheatDuration:0.##}s 归零", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"能量池 {report.Magazine:0} 发 ≈ {Mathf.Ceil(report.MagazineShots / report.BurstShots):0} 轮" +
                    $" ⇒ 打空需 {report.DumpTime:0.#}s（含 {report.HeatPauses} 次过热）· 整池总伤 {report.PoolDamage:0}",
                    EditorStyles.miniLabel);
            }

            if (report.UniqueTagCount > 0)
            {
                EditorGUILayout.LabelField($"特殊标签：{report.UniqueTagText}", EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField(
                "人机工学 = 子弹散布角 50% + 后坐力系数(RecoilForce) 50%（各自换算成 0~100 后加权平均；" +
                "散布 0°=100 分、每 1° 掉 20 分、≥5° 计 0）；" +
                "弹道 = 弹速 / 有效射程(最大射程<0 时按 弹速×生命周期)；特殊 = 专有属性注册数 + 改装池里的 Special 数。",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"清杂 = {ClearWindowSeconds:0} 秒内能击杀多少只 {ClearMobHp:0} 血 0 甲杂兵（25 只 = 100 分、<5 只 = 0 分，线性）；" +
                "已算进换弹/过热真空期、伤害溢出、光束/穿透的多目标；",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"反重 = 打靶输出 80% + 破甲 20%；打靶 = 打死一个 {HeavyTargetHp:0} 血 4 甲重型单位要多久" +
                "（10s = 100 分、60s = 0 分，线性；打不穿/备弹不够记 0）；AP 每低 1 级伤害 ×0.6、低 3 级完全免疫；",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "弱点加成不单列分数，而是折进伤害：×(1 + 弱点% ÷ 2)（÷2 模拟不是每枪都打中弱点）；",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "拆巢 = 拆毁值（<30 记 0 分）；破甲 = 直击/爆炸里的 Destruction 成分（伤害 × 系数）；" +
                "控制 = 异常状态成分值（毒/冰冻/电击/眩晕/恐惧/骇入/催化；燃烧·辐射只算持续伤害，不计控制）。",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "DPS / 总伤 / 单发只算真正扣血的成分，且一律按单体（Health.HandleDamage 对 " +
                "Destruction/Catalyst/Freeze/毒/骇入/恐惧/眩晕返回 0）；多目标只在清杂里生效。",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "不含弱点触发/易伤/队友增益与命中率；爆炸按爆心满伤；蓄力枪按满蓄伤害与满蓄耗时计。", EditorStyles.miniLabel);

            // 投掷物/护甲没有"持续射击"概念，DPS 与弹药量能算出来但没有参考意义，这里明说
            if (_currentController.WeaponTypeEnum == WeaponTypeEnum.Grenade ||
                _currentController.WeaponTypeEnum == WeaponTypeEnum.Armor)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("该类型不是枪械：不计 DPS，按单发伤害评；弹药类指标仅供参考。",
                    EditorStyles.miniBoldLabel);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// 一整块评分内容：顶部与对比区共用同一份，避免两边口径走样。
        /// <para>蓄力武器传两份（满蓄 / 最低蓄力）⇒ 每行上下两根条，分数取两者平均。</para>
        /// </summary>
        private void DrawScoreBlock(WeaponScoreReport max, WeaponScoreReport min, bool showWeights)
        {
            var hasRange = min.Valid;
            var overall = hasRange ? (max.Overall + min.Overall) * 0.5f : max.Overall;
            DrawScoreHeader(max, overall, min.Overall, showWeights, hasRange);
            EditorGUILayout.Space(6);

            // 投掷物没有"连续射击"：DPS 两行不显示，权重并给单发伤害
            if (!max.IsThrowable)
            {
                DrawScoreRow("爆发 DPS", $"{max.BurstDps:0.#}/s", $"{min.BurstDps:0.#}/s",
                    max.ScoreBurstDps, min.ScoreBurstDps, WeightBurstDps, hasRange);
                DrawScoreRow("持续 DPS", $"{max.SustainedDps:0.#}/s", $"{min.SustainedDps:0.#}/s",
                    max.ScoreSustainDps, min.ScoreSustainDps, WeightSustainDps, hasRange);
            }
            DrawScoreRow("单发伤害", RealDamageText(max), RealDamageText(min),
                max.ScorePerTrigger, min.ScorePerTrigger,
                max.IsThrowable
                    ? WeightPerTrigger + WeightBurstDps + WeightSustainDps + WeightMagazineDamage
                    : WeightPerTrigger,
                hasRange);
            // 投掷物没有"打完一匣"的节奏：弹匣总伤同样不计分（权重已并入单发）
            if (!max.IsThrowable)
            {
                // 热量武器的"弹匣"就是那 100 热：一匣 = 到过热为止的一轮，标签上写明免得看错
                DrawScoreRow(max.HasHeat ? "弹匣总伤(一轮)" : "弹匣总伤",
                    $"{max.MagazineDamage:0}", $"{min.MagazineDamage:0}",
                    max.ScoreMagazineDamage, min.ScoreMagazineDamage, WeightMagazineDamage, hasRange);
            }
            DrawScoreRow(max.HasHeat ? "全弹总伤(整池)" : "全弹总伤",
                $"{max.TotalDamage:0}", $"{min.TotalDamage:0}",
                max.ScoreTotalDamage, min.ScoreTotalDamage, WeightTotalDamage, hasRange);
            DrawScoreRow("穿甲等级", $"AP {max.AP}", $"AP {min.AP}",
                max.ScoreAp, min.ScoreAp, WeightAp, hasRange);
            DrawScoreRow("破甲能力", $"{max.ArmorDamage:0}", $"{min.ArmorDamage:0}",
                max.ScoreArmorBreak, min.ScoreArmorBreak, WeightArmorBreak, hasRange);
            DrawScoreRow("弱点加成", $"{max.WeaknessBonus * 100f:0}%", $"{min.WeaknessBonus * 100f:0}%",
                max.ScoreWeakness, min.ScoreWeakness, WeightWeakness, hasRange);
            DrawScoreRow("范围伤害", AoeText(max), AoeText(min),
                max.ScoreAoe, min.ScoreAoe, WeightAoe, hasRange);
            DrawScoreRow("人机工学", $"{max.Spread:0.#}° / {max.RecoilForce:0.##}x",
                $"{min.Spread:0.#}° / {min.RecoilForce:0.##}x",
                max.ScoreHandling, min.ScoreHandling, WeightHandling, hasRange);
            DrawScoreRow("弹道射程", $"{max.BulletSpeed:0}m/s / {max.EffectiveRange:0}m",
                $"{min.BulletSpeed:0}m/s / {min.EffectiveRange:0}m",
                max.ScoreBallistics, min.ScoreBallistics, WeightBallistics, hasRange);
            DrawScoreRow("特殊标签", $"注册{max.UniqueTagCount} · 改装{max.SpecialModCount}",
                $"注册{min.UniqueTagCount} · 改装{min.SpecialModCount}",
                max.ScoreSpecial, min.ScoreSpecial, WeightSpecial, hasRange);

            // 单项能力与综合能力之间留一行空档：上面是看得见的单项，下面是由单项二次合成的评估结论
            EditorGUILayout.Space(8);

            // 综合能力本来就是二次合成的平均分，不再画双条，直接给平均分
            var clear = AvgSub(max.ScoreClear, min.ScoreClear, hasRange);
            var antiHeavy = AvgSub(max.ScoreAntiHeavy, min.ScoreAntiHeavy, hasRange);
            var demolish = AvgSub(max.ScoreDemolish, min.ScoreDemolish, hasRange);
            var control = AvgSub(max.ScoreControl, min.ScoreControl, hasRange);
            DrawScoreRow("清杂能力", $"10s 击杀 {max.ClearKillsIn10s:0.#} 只（50血0甲）", "",
                clear, clear, WeightClear, false);
            DrawScoreRow("反重能力",
                max.HeavyTtk > 0f
                    ? $"AP {max.AP} · 击杀 {max.HeavyTtk:0.#}s（1000血4甲）"
                    : $"AP {max.AP} · 打不死（1000血4甲）", "",
                antiHeavy, antiHeavy, WeightAntiHeavy, false);
            DrawScoreRow("拆巢能力", $"拆毁 {max.DemolishValue:0}", "",
                demolish, demolish, WeightDemolish, false);
            DrawScoreRow("控制能力", $"{max.StatusPower:0}", "",
                control, control, WeightControl, false);
        }

        /// <summary>单发行文案：混了破甲/纯控制成分时把"实际扣血"标出来（单体口径，不含多目标）。</summary>
        private static string RealDamageText(WeaponScoreReport report)
            => report.RealDamageRatio < 0.999f
                ? $"{report.DamagePerTrigger:0.#}→{report.RealDamagePerTrigger:0.#}"
                : $"{report.DamagePerTrigger:0.#}";

        /// <summary>范围行文案：爆炸伤害·半径 / 光束半径×目标数 / 无。</summary>
        private static string AoeText(WeaponScoreReport report)
        {
            if (report.ExplosionDamage > 0f) return $"{report.ExplosionDamage:0}·{report.ExplosionRange:0.#}m";
            if (report.IsBeamSplash) return $"光束{report.ExplosionRange:0.#}m×{report.MultiTargetFactor:0.#}";
            return "无";
        }

        private void DrawScoreHeader(WeaponScoreReport max, float overall, float minOverall, bool showWeights,
            bool hasRange)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.contentColor;
                GUI.contentColor = ScoreColor(overall);
                GUILayout.Label(overall.ToString("0"), _scoreBigStyle, GUILayout.Width(80), GUILayout.Height(52));
                GUI.contentColor = oldColor;

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Space(6);
                    EditorGUILayout.LabelField($"综合评级 {GradeOf(overall)}（0-100）", EditorStyles.boldLabel);
                    // 权重行只在顶部出现一次，对比块重复它对读图没帮助
                    if (showWeights)
                    {
                        EditorGUILayout.LabelField("权重：清杂 20 · 反重 20 · 拆巢 12 · 人机工学 8 · 控制 6 · 弹道 4（%）",
                            EditorStyles.miniLabel);
                        EditorGUILayout.LabelField("　　　特殊 4 · 全弹 3 · 爆发 3 · 持续 3 · 单发 3 · 弹匣 3 · 穿甲 2 · 破甲 3 · 弱点 3 · 范围 3（%）",
                            EditorStyles.miniLabel);
                    }
                    if (hasRange)
                    {
                        EditorGUILayout.LabelField("条：实色=较低档、淡色延伸到较高档；分数取两者平均",
                            EditorStyles.miniLabel);
                    }
                    if (max.IsThrowable)
                    {
                        EditorGUILayout.LabelField("投掷物：不计 DPS 与弹匣总伤，这几项权重并入单发伤害（共 12%）",
                            EditorStyles.miniLabel);
                    }
                }
            }

            if (hasRange)
            {
                DrawScoreBarRange(minOverall, max.Overall, 16f);
            }
            else
            {
                DrawScoreBar(overall, 16f);
            }
        }

        /// <summary>一行明细。allowRange=true 且两项分数确实不同时，画上下两根条、分数取平均。</summary>
        private static void DrawScoreRow(string label, string maxText, string minText,
            float maxScore, float minScore, float weight, bool allowRange)
        {
            var showRange = allowRange && Mathf.Abs(maxScore - minScore) > 0.5f;
            var score = showRange ? (maxScore + minScore) * 0.5f : maxScore;

            using (new EditorGUILayout.HorizontalScope())
            {
                // 标签列 110：热量武器会显示"弹匣总伤(一轮)"这种 8 字标签
                GUILayout.Label(label, GUILayout.Width(110));
                // 值列 170：蓄力武器要摆"满蓄 / 最低"两段
                GUILayout.Label(showRange ? $"{maxText} / {minText}" : maxText,
                    EditorStyles.miniLabel, GUILayout.Width(170));

                if (showRange) DrawScoreBarRange(minScore, maxScore, 12f, 130f);
                else DrawScoreBar(score, 12f, 130f);

                var oldColor = GUI.contentColor;
                GUI.contentColor = ScoreColor(score);
                GUILayout.Label(score.ToString("0"), EditorStyles.boldLabel, GUILayout.Width(30));
                GUI.contentColor = oldColor;

                GUILayout.Label($"×{weight * 100f:0}%", EditorStyles.miniLabel, GUILayout.Width(48));
                GUILayout.Label($"= {score * weight:0.0}", EditorStyles.miniLabel, GUILayout.Width(52));
            }
        }

        /// <summary>对比区：按钮 + 选中后的四列对照（指标 / 当前 / 对比 / 差值）。</summary>
        private void DrawCompareSection(WeaponScoreReport current, WeaponScoreReport currentMin)
        {
            // 切左边列表正好切到自己、或资产被重导删掉时（Unity 的 == 能把已销毁判成 null），顺手清掉对比
            if (_compareController == null || _compareController == _currentController)
            {
                _compareController = null;
                _serializedCompare = null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var buttonText = _compareController != null
                    ? $"对比：{DisplayNameOf(_compareController)}（点击更换）"
                    : "＋ 选择对比武器";

                if (GUILayout.Button(buttonText, EditorStyles.miniButton, GUILayout.Height(22)))
                {
                    ShowCompareWeaponPicker();
                }

                using (new EditorGUI.DisabledScope(_compareController == null))
                {
                    if (GUILayout.Button("取消对比", EditorStyles.miniButton,
                            GUILayout.Width(72), GUILayout.Height(22)))
                    {
                        _compareController = null;
                        _serializedCompare = null;
                    }
                }
            }

            if (_compareController == null || _serializedCompare == null) return;

            _serializedCompare.Update();
            var other = BuildScoreReport(_compareController, _serializedCompare, 1f);
            if (!other.Valid)
            {
                EditorGUILayout.LabelField("对比武器没有伤害配置（Damages 为空）", EditorStyles.miniBoldLabel);
                return;
            }

            var otherMinRatio = GetMinChargeRatio(_compareController);
            var otherMin = other.UseCharge && otherMinRatio < 0.999f
                ? BuildScoreReport(_compareController, _serializedCompare, otherMinRatio)
                : default;

            // 先把对比武器自己的评分块整份再画一遍（与顶部同源），再出下面的差值表
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"■ 对比：{DisplayNameOf(_compareController)}", _titleStyle);
            EditorGUILayout.Space(2);
            DrawScoreBlock(other, otherMin, false);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("■ 数据对比", _titleStyle);
            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("指标", _compareHeaderStyle, GUILayout.Width(CompareLabelWidth));
                GUILayout.Label(DisplayNameOf(_currentController), _compareHeaderStyle, GUILayout.Width(CompareColWidth));
                GUILayout.Label(DisplayNameOf(_compareController), _compareHeaderStyle, GUILayout.Width(CompareColWidth));
                GUILayout.Label("差值（左-右）", _compareHeaderStyle, GUILayout.Width(CompareColWidth));
            }

            // 复合项目（范围/人机工学/弹道/特殊）直接比 0~100 的单项分：原始值单位不同，没法摆一列比。
            // 蓄力武器的单项分同样按"满蓄/最低蓄力"取平均，与主面板一致；明细数值仍取满蓄。
            var anyCharge = currentMin.Valid || otherMin.Valid;
            DrawCompareRow("综合评分", AvgScore(current.Overall, currentMin), AvgScore(other.Overall, otherMin), "0.0");
            DrawCompareRow("爆发 DPS", current.BurstDps, other.BurstDps, "0.#");
            DrawCompareRow("持续 DPS", current.SustainedDps, other.SustainedDps, "0.#");
            DrawCompareRow("单发伤害", current.RealDamagePerTrigger, other.RealDamagePerTrigger, "0.#");
            var anyHeat = current.HasHeat || other.HasHeat;
            DrawCompareRow(anyHeat ? "弹匣总伤(热=一轮)" : "弹匣总伤",
                current.MagazineDamage, other.MagazineDamage, "0");
            DrawCompareRow(anyHeat ? "全弹总伤(整池)" : "全弹总伤", current.TotalDamage, other.TotalDamage, "0");
            DrawCompareRow("穿甲等级", current.AP, other.AP, "0");
            DrawCompareRow("10s 清杂击杀", current.ClearKillsIn10s, other.ClearKillsIn10s, "0.#");
            // TTK 越低越好 ⇒ 差值着色要反过来（0 = 打不死，也按"最差"排）
            DrawCompareRow("重甲击杀耗时(s)", current.HeavyTtk, other.HeavyTtk, "0.#", true);
            DrawCompareRow("破甲能力（分）", AvgSub(current.ScoreArmorBreak, currentMin.ScoreArmorBreak, currentMin.Valid),
                AvgSub(other.ScoreArmorBreak, otherMin.ScoreArmorBreak, otherMin.Valid), "0");
            DrawCompareRow("弱点加成%", current.WeaknessBonus * 100f, other.WeaknessBonus * 100f, "0");
            DrawCompareRow("范围伤害（分）", AvgSub(current.ScoreAoe, currentMin.ScoreAoe, currentMin.Valid),
                AvgSub(other.ScoreAoe, otherMin.ScoreAoe, otherMin.Valid), "0");
            DrawCompareRow("人机工学（分）", AvgSub(current.ScoreHandling, currentMin.ScoreHandling, currentMin.Valid),
                AvgSub(other.ScoreHandling, otherMin.ScoreHandling, otherMin.Valid), "0");
            DrawCompareRow("弹道射程（分）", AvgSub(current.ScoreBallistics, currentMin.ScoreBallistics, currentMin.Valid),
                AvgSub(other.ScoreBallistics, otherMin.ScoreBallistics, otherMin.Valid), "0");
            DrawCompareRow("特殊标签（分）", AvgSub(current.ScoreSpecial, currentMin.ScoreSpecial, currentMin.Valid),
                AvgSub(other.ScoreSpecial, otherMin.ScoreSpecial, otherMin.Valid), "0");
            DrawCompareRow("清杂能力（分）", AvgSub(current.ScoreClear, currentMin.ScoreClear, currentMin.Valid),
                AvgSub(other.ScoreClear, otherMin.ScoreClear, otherMin.Valid), "0");
            DrawCompareRow("反重能力（分）", AvgSub(current.ScoreAntiHeavy, currentMin.ScoreAntiHeavy, currentMin.Valid),
                AvgSub(other.ScoreAntiHeavy, otherMin.ScoreAntiHeavy, otherMin.Valid), "0");
            DrawCompareRow("拆巢能力（分）", AvgSub(current.ScoreDemolish, currentMin.ScoreDemolish, currentMin.Valid),
                AvgSub(other.ScoreDemolish, otherMin.ScoreDemolish, otherMin.Valid), "0");
            DrawCompareRow("控制能力（分）", AvgSub(current.ScoreControl, currentMin.ScoreControl, currentMin.Valid),
                AvgSub(other.ScoreControl, otherMin.ScoreControl, otherMin.Valid), "0");

            if (anyCharge)
            {
                EditorGUILayout.LabelField("注：蓄力武器的综合分/单项分取满蓄与最低蓄力的均值，明细数值按满蓄显示。",
                    EditorStyles.miniLabel);
            }
        }

        private static float AvgScore(float maxOverall, WeaponScoreReport min)
            => min.Valid ? (maxOverall + min.Overall) * 0.5f : maxOverall;

        private static float AvgSub(float maxScore, float minScore, bool hasRange)
            => hasRange ? (maxScore + minScore) * 0.5f : maxScore;

        /// <summary>
        /// 一行对照：差值 = 左（当前武器）- 右（对比武器）。
        /// <para>默认"越高越好" ⇒ &gt;0 绿；<paramref name="lowerIsBetter"/>（如击杀耗时）反之。</para>
        /// </summary>
        private void DrawCompareRow(string label, float current, float other, string format,
            bool lowerIsBetter = false)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, _compareCellStyle, GUILayout.Width(CompareLabelWidth));
                GUILayout.Label(current.ToString(format), _compareCellStyle, GUILayout.Width(CompareColWidth));
                GUILayout.Label(other.ToString(format), _compareCellStyle, GUILayout.Width(CompareColWidth));

                var delta = current - other;
                var better = lowerIsBetter ? delta < 0f : delta > 0f;
                var oldColor = GUI.contentColor;
                GUI.contentColor = Mathf.Approximately(delta, 0f)
                    ? new Color(0.7f, 0.7f, 0.7f)
                    : better ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.95f, 0.5f, 0.45f);
                GUILayout.Label($"{(delta > 0f ? "+" : "")}{delta.ToString(format)}", _compareDeltaStyle,
                    GUILayout.Width(CompareColWidth));
                GUI.contentColor = oldColor;
            }
        }

        private void ShowCompareWeaponPicker()
        {
            var items = new List<GameObject>();
            foreach (var entry in _weaponEntries)
            {
                if (entry.Prefab == null || entry.Controller == null || entry.Controller == _currentController) continue;
                items.Add(entry.Prefab);
            }

            // 复用既有选择器（矮行样式：图标 + 中文名），弹在按钮下方，不新开窗口类
            var activatorRect = GUILayoutUtility.GetLastRect();
            PopupWindow.Show(activatorRect, new SOPickerPopup<GameObject>(
                items,
                go =>
                {
                    var entry = FindWeaponEntry(go);
                    if (entry != null) SetCompareWeapon(entry.Controller);
                },
                go => FindWeaponEntry(go)?.Icon,
                go => FindWeaponEntry(go)?.DisplayName ?? go.name));
        }

        private WeaponAssetEntry FindWeaponEntry(GameObject prefab)
        {
            for (var i = 0; i < _weaponEntries.Count; i++)
            {
                if (_weaponEntries[i].Prefab == prefab) return _weaponEntries[i];
            }
            return null;
        }

        private void SetCompareWeapon(WeaponPlayerController controller)
        {
            if (controller == null || controller == _currentController) return;
            _compareController = controller;
            _serializedCompare = new SerializedObject(controller);
            ResetFocus();
            Repaint();
        }

        /// <summary>显示名与左列表同口径：WeaponName 为空时退回 prefab 名。</summary>
        private static string DisplayNameOf(WeaponPlayerController controller)
        {
            if (controller == null) return "";
            return string.IsNullOrEmpty(controller.WeaponName) ? controller.name : controller.WeaponName;
        }

        private static void DrawScoreBar(float score, float height, float width = 0f)
        {
            var barRect = width > 0f
                ? GUILayoutUtility.GetRect(width, height)
                : GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));

            // DrawRect 只在 Repaint 生效，其它事件跳过以免刷无效指令
            if (Event.current.type != EventType.Repaint) return;

            DrawScoreBarRect(barRect, score);
        }

        /// <summary>一根条内画区间：0~min 实色、min~max 淡色，蓄力武器专用（不再上下两根条）。</summary>
        private static void DrawScoreBarRange(float minScore, float maxScore, float height, float width = 0f)
        {
            var barRect = width > 0f
                ? GUILayoutUtility.GetRect(width, height)
                : GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));

            if (Event.current.type != EventType.Repaint) return;

            DrawScoreBarRect(barRect, maxScore, minScore);
        }

        private static void DrawScoreBarRect(Rect barRect, float score, float minScore = -1f)
        {
            EditorGUI.DrawRect(barRect, new Color(0.16f, 0.16f, 0.18f, 0.9f));

            var color = ScoreColor(minScore >= 0f ? (score + minScore) * 0.5f : score);
            var scoreWidth = barRect.width * Mathf.Clamp01(score / 100f);
            if (minScore >= 0f)
            {
                // ⚠ 两根谁高谁低不确定（最低蓄力的 DPS 常常高于满蓄）⇒ 短的画实色、长的做淡色延伸。
                //   反过来画（实色总是 score）时，短条会把长条整段盖住，高的那一档反而看不见。
                var otherWidth = barRect.width * Mathf.Clamp01(minScore / 100f);
                var lowWidth = Mathf.Min(scoreWidth, otherWidth);
                var highWidth = Mathf.Max(scoreWidth, otherWidth);
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, highWidth, barRect.height),
                    new Color(color.r, color.g, color.b, 0.35f));
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, lowWidth, barRect.height), color);
            }
            else
            {
                EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, scoreWidth, barRect.height), color);
            }

            for (var i = 1; i < 5; i++)
            {
                var x = barRect.x + barRect.width * (i * 0.2f);
                EditorGUI.DrawRect(new Rect(x, barRect.y, 1f, barRect.height), new Color(0f, 0f, 0f, 0.35f));
            }
        }

        private static Color ScoreColor(float score)
        {
            if (score >= 80f) return new Color(0.42f, 0.85f, 0.45f);
            if (score >= 60f) return new Color(0.85f, 0.82f, 0.35f);
            if (score >= 40f) return new Color(0.95f, 0.62f, 0.32f);
            return new Color(0.92f, 0.36f, 0.33f);
        }

        /// <summary>
        /// 评级门槛按"11 项加权后的实际分布"定：普通枪械拿不到范围/特殊两块的分数，
        /// 用 90/80/70 老门槛会让全员掉到 E，所以整体下压。
        /// </summary>
        private static string GradeOf(float score)
        {
            if (score >= 70f) return "S";
            if (score >= 58f) return "A";
            if (score >= 42f) return "B";
            if (score >= 34f) return "C";
            if (score >= 26f) return "D";
            return "E";
        }

    }
}
