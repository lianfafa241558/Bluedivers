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
        //   真正的主导权交给清杂/反中/反重/拆巢/控制（合计 59%，反重 15 里还含 20% 的破甲分）。
        // ⚠ 新增「反中」时从清杂/续航/拆巢/控制各挪 1~2% 出来，反重 20 → 15：反中（500 血 3 甲）是
        //   比反重更常见的目标，但单发秒杀的重型武器仍然要有优势，所以 15 + 10 而不是 12.5 平分。
        private const float WeightBurstDps = 0.02f;      // 输出（参考值）
        private const float WeightSustainDps = 0.02f;
        private const float WeightPerTrigger = 0.02f;
        // 弹匣总伤 / 全弹总伤：**行留着、权重归零**（纯展示，避免"数据被藏起来"），
        // 它们代表的东西改由下面的「续航」统一计分（否则同一批弹药被算两遍）。
        private const float WeightMagazineDamage = 0f;
        private const float WeightTotalDamage = 0f;
        // 续航：整池弹药打完能撑多久（弹匣量 / 总备弹 / 换弹时长都在这里体现）。
        // 放在「综合能力」那一片（和清杂/反重/拆巢/控制/人机工学并列）。
        private const float WeightEndurance = 0.12f;
        private const float WeightAp = 0.02f;             // 穿透（反重的输入）
        private const float WeightArmorBreak = 0.04f;     // 破甲（单列项；同时是反重的 20% 输入）
        private const float WeightWeakness = 0.01f;       // 弱点加成
        private const float WeightAoe = 0.03f;            // 范围（清杂的输入）
        private const float WeightHandling = 0.08f;       // 人机工学（散布 + 后坐力，各 50%；已挪到"综合能力"区）
        private const float WeightBallistics = 0.04f;     // 弹道（弹速 + 有效射程）
        private const float WeightSpecial = 0.00f;        // 特殊标签
        private const float WeightClear = 0.15f;          // 综合能力：清杂 / 反中 / 反重 / 续航 / 拆巢 / 控制 / 人机工学
        private const float WeightAntiHeavy = 0.12f;
        private const float WeightAntiMedium = 0.12f;     // 反中（500 血 3 甲 + 200 血 1 甲头部弱点）
        private const float WeightDemolish = 0.09f;
        private const float WeightControl = 0.12f;        // 控制（异常状态成分）

        // 人机工学内部配比：子弹散布角 / 后坐力 各占一半（**加权平均**，不是相乘）
        private const float HandlingSpreadWeight = 0.5f;
        private const float HandlingRecoilWeight = 0.5f;

        // 差值表的列宽：字比上方明细大一号，列宽要跟着放宽，否则武器名会被裁
        private const float CompareLabelWidth = 110f;
        private const float CompareColWidth = 150f;
        // 得分差列窄一点（只放 "+1.23"）：110 + 150×3 + 124 = 684，刚好塞进最小窗口 960 下的内容宽
        //（右面板 ≈ 960-244-12-18 = 686）；再宽就会顶出面板、被 Clip 掉右边。
        private const float CompareScoreWidth = 124f;

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

        // 续航：整池弹药打完的实际秒数（已摊换弹/过热）。实测区间 17s（星辰脉冲 24 发）~ 189s（皇室蔷薇）
        // ⇒ 12s 记 0、240s 记 92、480s 满分；跨一个数量级用 log 插值
        private static readonly float[] EnduranceAnchors =
            { 12f, 0f, 30f, 25f, 60f, 50f, 120f, 75f, 240f, 92f, 480f, 100f };

        /// <summary>续航分内部配比：撑多久 70% + 整池总伤 30%（只比时间会奖励"低伤高弹量"，只比总伤又回到旧口径）。</summary>
        private const float EnduranceTimeWeight = 0.7f;

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

        /// <summary>清杂的靶子：杂兵 50 血、护甲等级 1（AP 0 的枪打它要 ×0.6，AP ≥ 1 才是满伤）。</summary>
        private const float ClearMobHp = 50f;
        /// <summary>清杂模板敌人的护甲等级（用户口径按 1 算；原来按 0 算，低穿甲枪会白拿清杂分）。</summary>
        private const float ClearMobArmorLevel = 1f;
        /// <summary>清杂的观察窗口（秒）：口径 = 10 秒内能击杀多少只杂兵。</summary>
        private const float ClearWindowSeconds = 10f;
        /// <summary>反重的靶子：重型单位躯干 1000 血、护甲等级 4，外加头部 350 血 / 4 甲（弱点）。</summary>
        private const float HeavyTargetHp = 1000f;
        private const float HeavyHeadHp = 350f;

        /// <summary>反中的靶子：中型单位躯干 500 血 / 2 甲，头部 100 血 / 2 甲（弱点，打爆即死）。</summary>
        private const float MediumBodyHp = 500f;
        private const float MediumBodyArmorLevel = 2f;
        private const float MediumHeadHp = 100f;
        private const float MediumHeadArmorLevel = 2f;

        /// <summary>
        /// 命中弱点部位（头部）的比例上限：由人机工学评分换算（<c>人机/100</c>），再乘这个上限。
        /// <para>0.9 而不是 1：再准的枪也会有目标移动、爆头判定偏移、连射抬枪口，"每枪都爆头"不该被当成默认值。</para>
        /// </summary>
        private const float WeakPointPrecisionCap = 0.9f;

        // 清杂：10 秒击杀数 → 分（线性：25 只 = 满分，< 5 只 = 0）
        private static readonly float[] ClearKillAnchors =
            { 5f, 0f, 40f, 100f };

        // 投掷物清杂：一颗能清几只（口径 = 单颗的伤害覆盖目标数，不吃备弹/节奏）。
        // 8 = MultiTargetCount 的硬上限（光束 + 穿透），爆炸弹按面积公式封顶 6。
        // ⚠ 1→0 / 8→100 是照着旧口径"5 发 × k 只进 5→40"反推的等价刻度（k=6 → 71 分，与改前一致）；
        //   要整体上调就把 8 改成 6（一颗清满 6 只即满分，投掷物总分约 +5）。
        private static readonly float[] ClearPerThrowAnchors =
            { 1f, 0f, 8f, 100f };

        // 反重：击杀 1000 血 4 甲（+350 血头部弱点、打爆即死）所需秒数 → 分（线性：3s = 满分，30s = 0）
        private static readonly float[] TtkAnchors =
            { 3f, 100f, 30f, 0f };

        // 反中：击杀 500 血 3 甲（头部 100 血 1 甲、打爆即死）所需秒数 → 分。
        // 头部只有 100 血 + 护甲 1 ⇒ 精准枪 1~3 秒就能爆头收掉，锚点按实测区间定：1s = 满分、12s = 0。
        private static readonly float[] MediumTtkAnchors =
            { 1f, 100f, 12f, 0f };

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

        /// <summary>
        /// 评分页内容区的可用宽度（<see cref="DrawScoreContent"/> 每帧重算）。
        /// <para>▍别再拿 <c>position.width</c> 减一堆常数去推面板宽：左边列表宽、helpBox 边距、
        /// 竖向滚动条三者一变就对不上，卡片会顶出面板。这里直接量到的就是内容真实宽度。</para>
        /// </summary>
        private float _scoreContentWidth = 700f;

        /// <summary>对比表行号：只用来算隔行底色，每帧从 0 重数（别在 DrawCompareRow 之外读它）。</summary>
        private int _compareRowIndex;

        private void DrawScoreContent(Rect rect)
        {
            GUI.Box(rect, "", EditorStyles.helpBox);

            var contentRect = new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12);
            // 右上角 6 个格子选的升级/模组：有选择时评分按"改装后"算，没有就是 prefab 原始数据
            var loadout = GetLoadoutMods(_loadout, _currentController, _serializedWeapon);
            var report = BuildScoreReport(_currentController, _serializedWeapon, 1f, loadout);
            if (!report.Valid)
            {
                GUI.Label(new Rect(contentRect.x, contentRect.y + 20, contentRect.width, 24),
                    "该武器没有伤害配置（Damages 为空），无法评分", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // 蓄力武器再算一份"最低可发射蓄力"：同一行画两根条，分数取两者平均
            var minRatio = GetMinChargeRatio(_currentController, _serializedWeapon, loadout);
            var minReport = report.UseCharge && minRatio < 0.999f
                ? BuildScoreReport(_currentController, _serializedWeapon, minRatio, loadout)
                : default;

            // 未改装的原始评级：改装后要能一眼看出"这分数是选出来的，不是底子"
            var stockOverall = -1f;
            if (!loadout.IsEmpty)
            {
                var stock = BuildScoreReport(_currentController, _serializedWeapon, 1f);
                var stockMin = minReport.Valid
                    ? BuildScoreReport(_currentController, _serializedWeapon, minRatio)
                    : default;
                stockOverall = stockMin.Valid ? (stock.Overall + stockMin.Overall) * 0.5f : stock.Overall;
            }

            GUILayout.BeginArea(contentRect);
            // 横向滚动条一律不要：内容全部关进"固定宽度"的容器里，再长的说明文字也只会被裁/折行，
            // 不会把滚动视图的内容宽撑出去（横向一滚，左边武器列表会跟着漂走，看起来就是面板错位）。
            // 18 = 常驻竖向滚动条约 14 + 取整余量；竖向常驻是为了内容变高时宽度不跳。
            _scoreContentWidth = Mathf.Max(320f, contentRect.width - 18f);
            _scoreScroll = EditorGUILayout.BeginScrollView(_scoreScroll, false, true);
            GUILayout.BeginVertical(GUILayout.Width(_scoreContentWidth));   // 配对在方法末尾的 EndVertical

            DrawScoreBlock(report, minReport, true, stockOverall, _loadout);

            EditorGUILayout.Space(8);
            DrawCompareSection(report, minReport);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("计算口径", EditorStyles.boldLabel);

            // ⚠ 长说明行一律用 wordWrapped：普通 miniLabel 不换行，minWidth = 整句宽度，
            //   会把滚动视图的内容宽撑到屏幕外 ⇒ 底部冒横向滚动条（一横向滚动，整页内容都跟着漂）。
            var note = EditorStyles.wordWrappedMiniLabel;

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
                $" = 单发 {report.DamagePerProjectile:0.##}{chargeNote}", note);

            var muzzleNote = report.Muzzles > 1 ? $"（{report.Muzzles} 个齐射点位）" : "";
            var derivativeNote = report.DerivativeDamage > 0f ? $" + 衍生物 {report.DerivativeDamage:0}" : "";
            EditorGUILayout.LabelField(
                $"单发 {report.DamagePerProjectile:0.##} × {report.ProjectilesPerTrigger} 弹丸{muzzleNote}{derivativeNote}" +
                $" = 每次扣扳机 {report.DamagePerTrigger:0.##}", note);
            if (report.DerivativeDamage > 0f)
            {
                // 衍生物单独列一行：它不在 Damages 里，混进"单发"里不写清楚会让人以为算错了
                EditorGUILayout.LabelField(
                    $"衍生物：{report.DerivativeText} ⇒ 额外总伤 {report.DerivativeDamage:0}" +
                    $"、破甲 {report.DerivativeArmorDamage:0}（按 {report.DerivativeDuration:0.#}s 存活折算）",
                    note);
            }
            // 面板单发（直击 + 爆炸）与实际进血量的单发不一致时把两者都摆出来：
            //   <1 = 混了"只削甲 / 只推积蓄槽"的成分；>1 = 伤害组里多个类型**各结算一次**
            if (Mathf.Abs(report.RealDamageRatio - 1f) > 0.001f)
            {
                if (report.RealDamageRatio > 1f)
                {
                    EditorGUILayout.LabelField(
                        $"伤害组里每个类型各结算一次 ⇒ 实际单发 {report.RealDamagePerTrigger:0.##}" +
                        $"（面板单发 {report.DamagePerTrigger:0.##}，按 ×{report.RealDamageRatio:0.##} 结算）；" +
                        $"DPS / 总伤 / 清杂 / 反重 用的都是实际值，控制值 {report.StatusPower:0}",
                        note);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        $"其中计入血量的成分只占 {report.RealDamageRatio * 100f:0.#}% ⇒ 有效单发 {report.RealDamagePerTrigger:0.##}" +
                        $"、控制值 {report.StatusPower:0}（Destruction 只削甲；Catalyst/骇入/恐惧/眩晕只加积蓄槽）",
                        note);
                }
            }
            if (report.IsBeamSplash)
            {
                // 光束的"范围"不是爆炸伤害，而是命中半径：判断口径来自 ProjectileLaser.useExplosionRange
                var penetrateNote = report.PenetratesUnits ? "、且穿透单位（沿途每个敌人各吃一次直击）" : "";
                EditorGUILayout.LabelField(
                    $"光束范围：命中半径 {report.ExplosionRange:0.#}m{penetrateNote}" +
                    $" ⇒ 一次节拍约打中 {report.MultiTargetFactor:0.#} 个目标（{report.RealDamagePerTrigger:0.##}" +
                    $"→{report.EffectiveDamagePerTrigger:0.##}）；只计入清杂，DPS/总伤/反重都按单体",
                    note);
            }
            else if (report.PenetratesUnits)
            {
                var reason = report.TrailsUnits
                    ? "尾迹每隔一拍沿「开火点 → 子弹位置」整条线结算（等价于整条线穿透）"
                    : "子弹打穿第一个目标继续往后";
                EditorGUILayout.LabelField(
                    $"{reason} ⇒ 清杂按 {report.MultiTargetFactor:0.#} 个目标折算" +
                    (report.TrailsUnits ? $"（半径 {report.TrailRange:0.##}m）" : "") +
                    $"（{report.RealDamagePerTrigger:0.##}→{report.EffectiveDamagePerTrigger:0.##}）；DPS/总伤/反重仍按单体",
                    note);
            }
            else if (report.ExplosionDamage > 0f && report.MultiTargetFactor > 1.01f)
            {
                EditorGUILayout.LabelField(
                    $"爆炸范围：半径 {report.ExplosionRange:0.#}m 按面积估一次约罩住 {report.MultiTargetFactor:0.#} 个杂兵" +
                    $"（{report.RealDamagePerTrigger:0.##}→{report.EffectiveDamagePerTrigger:0.##}）；只计入清杂，DPS/总伤/反重仍按单体",
                    note);
            }
            EditorGUILayout.LabelField(
                $"射击间隔 {report.Interval:0.###}s · 换弹 {report.Reload:0.##}s · 每次射击消耗 {report.ShootCost}",
                note);
            if (report.LockTime > 0f)
            {
                EditorGUILayout.LabelField(
                    $"锁定：{report.LockLayers} 层 × {report.LockInterval:0.##}s = {report.LockLayers * report.LockInterval:0.##}s 锁满，" +
                    $"之后可连打 {report.LockLayers} 发（每发消耗 1 层）⇒ 每发摊销 {report.LockTime:0.##}s，" +
                    $"真实周期 = 射击间隔 + 每层锁定 = {report.Interval:0.###}s；DPS / 清杂都按这个算。",
                    note);
            }
            // 清杂用的节奏要把真空期摊进去，单独写一行，免得和上面的"射击间隔"混淆
            var heatPauseNote = report.HeatPauses > 0
                ? $"（含 {report.HeatPauses} 次{(report.HeatStyleVent ? "停手散热" : "过热")}）"
                : "";
            EditorGUILayout.LabelField(
                $"清杂节奏：一匣 {report.MagazineShots:0.#} 发 · 打空 {report.DumpTime:0.#}s{heatPauseNote}" +
                $" + 换弹 {report.Reload:0.##}s ⇒ 实际 {report.ClearRate:0.##} 次/s" +
                $" ⇒ 10 秒击杀 {report.ClearKillsIn10s:0.#} 只" +
                $"（每次扣扳机 {report.Interval:0.###}s 只是开火时）",
                note);

            var cost = Mathf.Max(1, report.ShootCost);
            if (!report.HasHeat)
            {
                EditorGUILayout.LabelField(
                    $"弹匣 {report.Magazine:0} / 后备 {report.Ammo:0} ⇒ 一匣可打 {report.MagazineShots:0.#} 次、" +
                    $"全部备弹 {((report.Magazine + report.Ammo) / cost):0.#} 次", note);
            }
            else if (report.BurstShots <= 0f)
            {
                // 有效热量 <= 0：散热比加热快，连射永远到不了 100
                EditorGUILayout.LabelField(
                    $"热量：每发 +{report.HeatPerShot:0.##} 热，但散热 {report.CoolSpeed:0}/s ≥ 加热速度" +
                    $" ⇒ 连射不会过热，按整匣 {report.MagazineDamage:0} 算", note);
            }
            else
            {
                // 热量武器的"弹匣"是 100 热：一轮几发、整池几轮、打空多久，三件事分开写清楚
                EditorGUILayout.LabelField(
                    $"热量：每发 +{report.HeatPerShot:0.##} 热（满 100，连射净攒 {report.EffectiveHeat:0.##}）" +
                    $" ⇒ 到过热 {report.BurstShots:0} 发 = 一轮总伤 {report.MagazineDamage:0}" +
                    $" · 过热 {report.OverheatDuration:0.##}s 归零", note);
                // 两种打法（打满过热 / 打一发少一点就停手散热）取吞吐高的那条，这里把它摆出来
                EditorGUILayout.LabelField(report.HeatStyleVent
                        ? $"实战节奏取优：打 {report.HeatCycleShots:0} 发就停手，等 {report.CoolDelay:0.##}s 散热延迟后按 {report.CoolSpeed:0}/s 散热，" +
                          $"每轮停顿约 {report.HeatCyclePause:0.#}s —— 吞吐比打满过热（{report.BurstShots:0} 发 + 锁 {report.OverheatDuration:0.##}s）更高，DPS / 清杂按这条算。"
                        : $"实战节奏：打满过热更划算（停火散热的吞吐低于锁 {report.OverheatDuration:0.##}s）。",
                    note);
                EditorGUILayout.LabelField(
                    $"能量池 {report.Magazine:0} 发 ≈ {Mathf.Ceil(report.MagazineShots / Mathf.Max(1f, report.HeatCycleShots)):0} 轮" +
                    $" ⇒ 打空需 {report.DumpTime:0.#}s（含 {report.HeatPauses} 次{(report.HeatStyleVent ? "停手散热" : "过热")}）" +
                    $" · 整池总伤 {report.PoolDamage:0}",
                    note);
            }

            if (report.UniqueTagCount > 0)
            {
                EditorGUILayout.LabelField($"特殊标签：{report.UniqueTagText}", note);
            }

            EditorGUILayout.LabelField(
                "人机工学 = 子弹散布角 50% + 后坐力系数(RecoilForce) 50%（各自换算成 0~100 后加权平均；" +
                "散布 0°=100 分、每 1° 掉 20 分、≥5° 计 0）；" +
                "弹道 = 弹速 / 有效射程(最大射程<0 时按 弹速×生命周期)；特殊 = 专有属性注册数 + 改装池里的 Special 数。",
                note);
            EditorGUILayout.LabelField(
                $"清杂 = {ClearWindowSeconds:0} 秒内能击杀多少只 {ClearMobHp:0} 血 {ClearMobArmorLevel:0} 甲杂兵（40 只 = 100 分、5 只 = 0 分，线性）；" +
                $"已算进换弹/过热真空期、伤害溢出、光束/穿透的多目标；热量武器按「打满过热」与「打 N-1 发停手散热」" +
                $"里吞吐更高的一条算节奏；杂兵护甲 {ClearMobArmorLevel:0} ⇒ AP 0 的枪伤害 ×0.6、AP ≥ 1 满伤；" +
                "尾迹类按「扣扳机次数 × 存活时长」单独折算持续伤害（不会因为一发就被算完）；" +
                "投掷物改按「一颗能清几只」评（1 只 = 0 分、8 只 = 100 分，线性；爆炸按面积折目标数、封顶 6），" +
                "不吃备弹与节奏 —— 备弹/引信已经由续航和反中/反重负责；",
                note);
            EditorGUILayout.LabelField(
                "尾迹的附加伤害类型（恐惧/电磁/燃烧尾迹）按运行时口径补进尾迹的伤害组：" +
                "恐惧只推积蓄槽（记控制）、电击/燃烧同时掉血；「尾迹范围」加宽尾迹半径，清杂按半径折成更多目标；",
                note);
            EditorGUILayout.LabelField(
                $"反中 = 打死一个 {MediumBodyHp:0} 血 {MediumBodyArmorLevel:0} 甲中型单位要多久" +
                $"（头部 {MediumHeadHp:0} 血 {MediumHeadArmorLevel:0} 甲弱点，打爆即死；1s = 100 分、12s = 0 分，线性）；" +
                "反重 = 打靶输出 80% + 破甲 20%，靶子是 1000 血 4 甲 + 头部 350 血 4 甲（3s = 100 分、30s = 0 分）；" +
                "打不穿 / 备弹不够记 0；AP 每低 1 级伤害 ×0.6、低 3 级完全免疫；",
                note);
            EditorGUILayout.LabelField(
                $"弱点（头部）模型：首发命中率 = 散布分 × {WeakPointPrecisionCap:0.##}（后坐力要打完这一发才顶枪口，对第一发不成立）；" +
                $"要连打才改用人机工学分 × {WeakPointPrecisionCap:0.##}（分散布 + 后坐）；" +
                "「打爆头」与「打空躯干」两条路线各按命中率折算，取更快的一条（爆头即死），弱点加成只乘在头部那一条；" +
                "头部按肢体算：范围/爆炸里的元素成分（毒/燃烧/冰冻/电击/眩晕/恐惧/辐射/骇入/催化）只落主体，" +
                "头部只吃直击的全部成分 + 爆炸的非元素部分；",
                note);
            EditorGUILayout.LabelField(
                "拆巢 = 拆毁值（<30 记 0 分）；破甲 = 直击/爆炸里的 Destruction 成分（伤害 × 系数）；" +
                "控制 = 异常状态成分值（毒/冰冻/电击/眩晕/恐惧/骇入/催化；燃烧·辐射只算持续伤害，不计控制）。",
                note);
            EditorGUILayout.LabelField(
                "续航 = 整池弹药打完的实际秒数（全弹次数 ÷ 实战节奏，已摊换弹与过热）占 70% + 整池总伤分 30%；" +
                "弹匣量、总备弹、换弹时长都只在这一项里体现（12s 记 0、60s 记 50、240s 记 92、480s 满分，log 插值）；" +
                "「弹匣总伤 / 全弹总伤」两行保留但权重 0，只作数据参考。",
                note);
            EditorGUILayout.LabelField(
                "伤害组里每个类型各结算一次（直击 10 配「动能1 + 燃烧1」⇒ 动能 10 + 燃烧 10 = 20，不是平摊），" +
                "所以实际单发 = 面板值 × 计血系数和，可能翻倍；只有 Destruction（只削甲）与 Catalyst/骇入/恐惧/眩晕" +
                "（只推积蓄槽）返回 0 —— 毒/冰冻/燃烧/电击/辐射既推积蓄槽也照常掉血，同一笔伤害可以同时计入单发与控制。" +
                "DPS / 总伤 / 单发一律按单体，多目标只在清杂里生效。",
                note);
            EditorGUILayout.LabelField(
                "不含弱点触发/易伤/队友增益与命中率；爆炸按爆心满伤；蓄力枪按满蓄伤害与满蓄耗时计；" +
                "锁定类按「射击间隔 + 每层锁定时间」算周期（锁满 N 层可连打 N 发，每发消耗 1 层）。", note);
            EditorGUILayout.LabelField(
                "右上 6 格 = 改装模拟：把选中升级项/模组的 modifys 叠到属性副本上重算（Final = (base+ΣBase)×(1+ΣFactor)+ΣExtra），" +
                "不动资产；Special 只认数值、玩法效果不计 ⇒ 满改装 = 5 档全选 + 1 个模组。",
                note);

            // 投掷物/护甲没有"持续射击"概念，DPS 与弹药量能算出来但没有参考意义，这里明说
            if (_currentController.WeaponTypeEnum == WeaponTypeEnum.Grenade ||
                _currentController.WeaponTypeEnum == WeaponTypeEnum.Armor)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("该类型不是枪械：不计 DPS，按单发伤害评；弹药类指标仅供参考。",
                    EditorStyles.miniBoldLabel);
            }

            GUILayout.EndVertical();    // 对应方法开头的 BeginVertical（固定内容宽，挡住横向滚动）
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// 一整块评分内容：顶部与对比区共用同一份，避免两边口径走样。
        /// <para>蓄力武器传两份（满蓄 / 最低蓄力）⇒ 每行上下两根条，分数取两者平均。</para>
        /// </summary>
        private void DrawScoreBlock(WeaponScoreReport max, WeaponScoreReport min, bool showWeights,
            float stockOverall = -1f, LoadoutState loadout = null)
        {
            var hasRange = min.Valid;
            var overall = hasRange ? (max.Overall + min.Overall) * 0.5f : max.Overall;
            DrawScoreHeader(max, overall, min.Overall, showWeights, hasRange, stockOverall, loadout);
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
                max.ScorePerTrigger, min.ScorePerTrigger, PerTriggerWeight(max), hasRange);
            // 弹匣/全弹总伤：行留着、权重 0（只展示）—— 弹药相关的评分统一由「续航能力」负责，
            // 三项都计分等于同一批弹药算两遍。
            // 热量武器的"弹匣"就是那 100 热：一匣 = 到过热为止的一轮，标签上写明免得看错
            DrawScoreRow(max.HasHeat ? "弹匣总伤(一轮)" : "弹匣总伤",
                $"{max.MagazineDamage:0}", $"{min.MagazineDamage:0}",
                max.ScoreMagazineDamage, min.ScoreMagazineDamage, WeightMagazineDamage, hasRange);
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
            // 人机工学已挪到下面「综合能力」区（与清杂/反重/续航/拆巢/控制并列），这里不再重复
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
            var antiMedium = AvgSub(max.ScoreAntiMedium, min.ScoreAntiMedium, hasRange);
            var demolish = AvgSub(max.ScoreDemolish, min.ScoreDemolish, hasRange);
            var control = AvgSub(max.ScoreControl, min.ScoreControl, hasRange);
            DrawScoreRow("清杂能力",
                max.IsThrowable
                    ? $"一颗清 {max.ClearKillsPerThrow:0.#} 只（50血1甲·单发{max.ClearDamagePerTrigger:0.#}）"
                    : $"10s 击杀 {max.ClearKillsIn10s:0.#} 只（50血1甲·单发{max.ClearDamagePerTrigger:0.#}" +
                      (max.TrailKills > 0.05f ? $"·尾迹{max.TrailKills:0.#}" : "") + "）", "",
                clear, clear, WeightClear, false);
            // 弱点命中率对反中/反重是同一个系数：显示在"反中"行，反重行不重复
            var weakNote = $"命中 首发{max.FirstShotPrecision * 100f:0}%/连射{max.WeakPointPrecision * 100f:0}%";
            DrawScoreRow("反中能力",
                max.MediumTtk > 0f
                    ? $"AP {max.AP} · 击杀 {max.MediumTtk:0.#}s（500血3甲+头100血1甲爆头即死·{weakNote}）"
                    : $"AP {max.AP} · 打不死（500血3甲+头100血1甲·{weakNote}）", "",
                antiMedium, antiMedium, WeightAntiMedium, false);
            DrawScoreRow("反重能力",
                max.HeavyTtk > 0f
                    ? $"AP {max.AP} · 击杀 {max.HeavyTtk:0.#}s（1000血4甲+头350血4甲）"
                    : $"AP {max.AP} · 打不死（1000血4甲+头350血4甲）", "",
                antiHeavy, antiHeavy, WeightAntiHeavy, false);
            // 续航：弹匣量 / 总备弹 / 换弹时长都在这一项里（上面的弹匣/全弹总伤只是展示）
            var endurance = AvgSub(max.ScoreEndurance, min.ScoreEndurance, hasRange);
            DrawScoreRow("续航能力",
                max.IsThrowable ? "投掷物固定 5 发，不计这项" : $"{max.EnduranceSeconds:0}s · 全弹 {max.TotalDamage:0}", "",
                endurance, endurance, max.IsThrowable ? 0f : WeightEndurance, false);
            DrawScoreRow("拆巢能力", $"拆毁 {max.DemolishValue:0}", "",
                demolish, demolish, WeightDemolish, false);
            DrawScoreRow("控制能力", $"{max.StatusPower:0}", "",
                control, control, WeightControl, false);
            // 人机工学：散布 + 后坐力，属于"用起来顺不顺手"的评估结论，跟上面几项一起放综合区
            var handling = AvgSub(max.ScoreHandling, min.ScoreHandling, hasRange);
            DrawScoreRow("人机工学", $"{max.Spread:0.#}° / {max.RecoilForce:0.##}x", "",
                handling, handling, WeightHandling, false);
        }

        /// <summary>
        /// 单发行文案：与实际进血量不一致时把"实际值"标出来（单体口径，不含多目标）。
        /// <para>⚠ 判断用"与 1 的绝对差"，别写成 <c>ratio &lt; 0.999</c>：多成分叠加时 ratio &gt; 1
        /// （10 + 动能1 + 燃烧1 ⇒ 20），旧写法会把"10→20"漏掉，看起来像真实伤害没算。</para>
        /// </summary>
        private static string RealDamageText(WeaponScoreReport report)
            => Mathf.Abs(report.RealDamageRatio - 1f) > 0.001f
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
            bool hasRange, float stockOverall = -1f, LoadoutState loadout = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.contentColor;
                GUI.contentColor = ScoreColor(overall);
                // 72：最大 "100"（三位数）也放得下；再宽就是白占卡片的地盘了
                GUILayout.Label(overall.ToString("0"), _scoreBigStyle, GUILayout.Width(72), GUILayout.Height(48));
                GUI.contentColor = oldColor;

                // 评级文字列固定宽：右边的改装卡片要按"屏幕宽度 - 已知列宽"算卡片宽，
                // 留着弹性列会让卡片宽度每帧浮动（拖动窗口时卡片跟着抖）。
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(LoadoutLabelWidth)))
                {
                    GUILayout.Space(8);
                    // 去掉"（0-100）"：这行宽度直接决定右边六张卡各能多宽
                    EditorGUILayout.LabelField($"综合评级 {GradeOf(overall)}", EditorStyles.boldLabel);
                    // 权重行只在顶部出现一次，对比块重复它对读图没帮助
                    if (showWeights)
                    {
                        //EditorGUILayout.LabelField("权重：清杂 18 · 反重 15 · 反中 10 · 续航 8 · 拆巢 8 · 控制 8 · 人机 9 · 弹道 4（%）",
                        //    EditorStyles.miniLabel);
                        //EditorGUILayout.LabelField("　　　特殊 3 · 破甲 3 · 弱点 3 · 范围 3 · 单发 2 · 爆发 2 · 持续 2 · 穿甲 2 · 全弹 0 · 弹匣 0（%）",
                        //    EditorStyles.miniLabel);
                    }
                    if (stockOverall >= 0f)
                    {
                        EditorGUILayout.LabelField($"改装{max.LoadoutCount}·原{stockOverall:0}", EditorStyles.miniLabel);
                    }
                    else if (showWeights)
                    {
                        EditorGUILayout.LabelField("未改装·点卡片试", EditorStyles.miniLabel);
                    }
                }

            }

            // 改装模拟：5 档升级 + 1 模组，横排带图标/名称/效果，点击弹选择列表。
            // ⚠ 必须独占一整行（别再塞进评级右边）：内容区宽 700 分给 6 张卡已经只有 118px，
            //   再和"综合评级 + 大分数"分反而每张只剩 90px，汉字必被裁。
            // 当前武器与对比武器各传自己的 LoadoutState，互不干扰。
            if (loadout != null)
            {
                EditorGUILayout.Space(4);
                DrawLoadoutStrip(loadout, _scoreContentWidth);
                EditorGUILayout.Space(4);
            }

            // 长提示整行放：文字列被卡片挤到 170 宽，这两句塞进去只会被裁掉
            if (hasRange)
            {
                EditorGUILayout.LabelField("条：实色=较低档、淡色延伸到较高档；分数取两者平均",
                    EditorStyles.miniLabel);
            }
            if (max.IsThrowable)
            {
                // 引信延迟必须写出来：不然"0.1s 一发、伤害 5000/s"会让人以为算错了
                EditorGUILayout.LabelField(
                    $"投掷物：不计 DPS 与续航，这几项权重并入单发伤害（共 15%）；" +
                    $"伤害要等引信 {max.DamageDelay:0.#}s 到点才落地 ⇒ 反中/反重/清杂窗口都按这个延迟算",
                    EditorStyles.wordWrappedMiniLabel);
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

        /// <summary>对比区：按钮 + 选中后的五列对照（指标 / 当前 / 对比 / 差值 / 得分差）。</summary>
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
            // 对比武器也有自己的 5 档 + 模组选择（与当前武器互不干扰），评分按它自己的改装算
            var otherLoadout = GetLoadoutMods(_compareLoadout, _compareController, _serializedCompare);
            var other = BuildScoreReport(_compareController, _serializedCompare, 1f, otherLoadout);
            if (!other.Valid)
            {
                EditorGUILayout.LabelField("对比武器没有伤害配置（Damages 为空）", EditorStyles.miniBoldLabel);
                return;
            }

            var otherMinRatio = GetMinChargeRatio(_compareController, _serializedCompare, otherLoadout);
            var otherMin = other.UseCharge && otherMinRatio < 0.999f
                ? BuildScoreReport(_compareController, _serializedCompare, otherMinRatio, otherLoadout)
                : default;

            // "未改装"参照：与顶部同一个口径
            var otherStock = -1f;
            if (!otherLoadout.IsEmpty)
            {
                var stock = BuildScoreReport(_compareController, _serializedCompare, 1f);
                var stockMin = otherMin.Valid
                    ? BuildScoreReport(_compareController, _serializedCompare, otherMinRatio)
                    : default;
                otherStock = stockMin.Valid ? (stock.Overall + stockMin.Overall) * 0.5f : stock.Overall;
            }

            // 先把对比武器自己的评分块整份再画一遍（与顶部同源），再出下面的差值表
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"■ 对比：{DisplayNameOf(_compareController)}", _titleStyle);
            EditorGUILayout.Space(2);
            DrawScoreBlock(other, otherMin, false, otherStock, _compareLoadout);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("■ 数据对比", _titleStyle);
            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("指标", _compareHeaderStyle, GUILayout.Width(CompareLabelWidth));
                GUILayout.Label(DisplayNameOf(_currentController), _compareHeaderStyle, GUILayout.Width(CompareColWidth));
                GUILayout.Label(DisplayNameOf(_compareController), _compareHeaderStyle, GUILayout.Width(CompareColWidth));
                GUILayout.Label("差值（左-右）", _compareHeaderStyle, GUILayout.Width(CompareColWidth));
                GUILayout.Label("得分差", _compareHeaderStyle, GUILayout.Width(CompareScoreWidth));
            }

            // 复合项目（范围/人机工学/弹道/特殊）直接比 0~100 的单项分：原始值单位不同，没法摆一列比。
            // 蓄力武器的单项分同样按"满蓄/最低蓄力"取平均，与主面板一致；明细数值仍取满蓄。
            var anyCharge = currentMin.Valid || otherMin.Valid;

            // 末列"得分差" = 单项分 × 权重 后左右相减 ⇒ 这一项到底给综合评分贡献了多少分。
            // 权重基本是 2%~15%，原始差值（DPS/秒数）看不出分量，光看差值列会误判"差得多厉害"。
            float RowDelta(Func<WeaponScoreReport, float> selector, float weight)
                => (SubScore(current, currentMin, selector) - SubScore(other, otherMin, selector)) * weight;

            // 权重随武器类型变（投掷物把 DPS/续航的权重并进单发）⇒ 左右两侧分别取自己的权重。
            // ⚠ 别写成 RowDelta 的重载：C# 不允许局部函数同名重载（CS0128）
            float RowDeltaByWeight(Func<WeaponScoreReport, float> selector,
                Func<WeaponScoreReport, float> weightOf)
                => SubScore(current, currentMin, selector) * weightOf(current)
                   - SubScore(other, otherMin, selector) * weightOf(other);

            // 隔行底色从第一行重新数（表头不参与，所以在这里清零而不是进方法时自动数）
            _compareRowIndex = 0;

            var currentOverall = AvgScore(current.Overall, currentMin);
            var otherOverall = AvgScore(other.Overall, otherMin);
            // 综合评分权重就是 1 ⇒ 末列与差值列同值（留着是为了让下面各项的得分差有个"合计"可对）
            DrawCompareRow("综合评分", currentOverall, otherOverall, "0.0", false, currentOverall - otherOverall);
            DrawCompareRow("爆发 DPS", current.BurstDps, other.BurstDps, "0.#", false,
                RowDelta(r => r.ScoreBurstDps, WeightBurstDps));
            DrawCompareRow("持续 DPS", current.SustainedDps, other.SustainedDps, "0.#", false,
                RowDelta(r => r.ScoreSustainDps, WeightSustainDps));
            DrawCompareRow("单发伤害", current.RealDamagePerTrigger, other.RealDamagePerTrigger, "0.#", false,
                RowDeltaByWeight(r => r.ScorePerTrigger, PerTriggerWeight));
            var anyHeat = current.HasHeat || other.HasHeat;
            DrawCompareRow("续航时长(s)", current.EnduranceSeconds, other.EnduranceSeconds, "0", false,
                RowDeltaByWeight(r => r.ScoreEndurance, r => r.IsThrowable ? 0f : WeightEndurance));
            DrawCompareRow(anyHeat ? "全弹总伤(整池)" : "全弹总伤", current.TotalDamage, other.TotalDamage, "0", false,
                RowDelta(r => r.ScoreTotalDamage, WeightTotalDamage));
            DrawCompareRow("穿甲等级", current.AP, other.AP, "0", false,
                RowDelta(r => r.ScoreAp, WeightAp));
            // 投掷物比"一颗清几只"，枪械比"10 秒击杀数"（两者单位不同，别混一列）
            var anyThrowable = current.IsThrowable || other.IsThrowable;
            DrawCompareRow(anyThrowable ? "单颗清杂(只)" : "10s 清杂击杀",
                anyThrowable ? current.ClearKillsPerThrow : current.ClearKillsIn10s,
                anyThrowable ? other.ClearKillsPerThrow : other.ClearKillsIn10s, "0.#", false,
                RowDelta(r => r.ScoreClear, WeightClear));
            // TTK 越低越好 ⇒ 差值着色要反过来（0 = 打不死，也按"最差"排）
            DrawCompareRow("中甲击杀耗时(s)", current.MediumTtk, other.MediumTtk, "0.#", true,
                RowDelta(r => r.ScoreAntiMedium, WeightAntiMedium));
            DrawCompareRow("重甲击杀耗时(s)", current.HeavyTtk, other.HeavyTtk, "0.#", true,
                RowDelta(r => r.ScoreAntiHeavy, WeightAntiHeavy));
            // 弱点命中率不是独立计分项（它是反中/反重的输入）⇒ 末列留空，别硬塞一个分
            DrawCompareRow("弱点命中率%", current.WeakPointPrecision * 100f, other.WeakPointPrecision * 100f, "0");
            // 破甲用"每次扣扳机的护甲破坏值"而不是 0~100 的分数：这个数是直接拿去和敌人护甲比大小的，
            // 换成分数还得反推锚点，比不出手感。蓄力枪取满蓄那一份，与明细行一致。
            DrawCompareRow("破甲值", current.ArmorDamage, other.ArmorDamage, "0", false,
                RowDelta(r => r.ScoreArmorBreak, WeightArmorBreak));
            DrawCompareRow("弱点加成%", current.WeaknessBonus * 100f, other.WeaknessBonus * 100f, "0", false,
                RowDelta(r => r.ScoreWeakness, WeightWeakness));
            // 空一行分开两段：上面是带单位的实测值（DPS / 秒数 / 数值），下面是换算成 0~100 的评分
            EditorGUILayout.Space(10);
            DrawCompareRow("范围伤害（分）", SubScore(current, currentMin, r => r.ScoreAoe),
                SubScore(other, otherMin, r => r.ScoreAoe), "0", false,
                RowDelta(r => r.ScoreAoe, WeightAoe));
            DrawCompareRow("人机工学（分）", SubScore(current, currentMin, r => r.ScoreHandling),
                SubScore(other, otherMin, r => r.ScoreHandling), "0", false,
                RowDelta(r => r.ScoreHandling, WeightHandling));
            DrawCompareRow("弹道射程（分）", SubScore(current, currentMin, r => r.ScoreBallistics),
                SubScore(other, otherMin, r => r.ScoreBallistics), "0", false,
                RowDelta(r => r.ScoreBallistics, WeightBallistics));
            DrawCompareRow("特殊标签（分）", SubScore(current, currentMin, r => r.ScoreSpecial),
                SubScore(other, otherMin, r => r.ScoreSpecial), "0", false,
                RowDelta(r => r.ScoreSpecial, WeightSpecial));
            DrawCompareRow("清杂能力（分）", SubScore(current, currentMin, r => r.ScoreClear),
                SubScore(other, otherMin, r => r.ScoreClear), "0", false,
                RowDelta(r => r.ScoreClear, WeightClear));
            DrawCompareRow("反中能力（分）", SubScore(current, currentMin, r => r.ScoreAntiMedium),
                SubScore(other, otherMin, r => r.ScoreAntiMedium), "0", false,
                RowDelta(r => r.ScoreAntiMedium, WeightAntiMedium));
            DrawCompareRow("反重能力（分）", SubScore(current, currentMin, r => r.ScoreAntiHeavy),
                SubScore(other, otherMin, r => r.ScoreAntiHeavy), "0", false,
                RowDelta(r => r.ScoreAntiHeavy, WeightAntiHeavy));
            DrawCompareRow("拆巢能力（分）", SubScore(current, currentMin, r => r.ScoreDemolish),
                SubScore(other, otherMin, r => r.ScoreDemolish), "0", false,
                RowDelta(r => r.ScoreDemolish, WeightDemolish));
            DrawCompareRow("控制能力（分）", SubScore(current, currentMin, r => r.ScoreControl),
                SubScore(other, otherMin, r => r.ScoreControl), "0", false,
                RowDelta(r => r.ScoreControl, WeightControl));

            EditorGUILayout.LabelField(
                "得分差 = 单项分 × 该行权重，左右相减 ⇒ 这一项给综合评分贡献了多少分（原始差值只是 DPS/秒数，看不出权重，判断\"差得算不算事\"看这一列）；" +
                "同一项在表里出现两次（原始值行 +（分）行）时两行得分差相同、权重 0 的行（全弹总伤、特殊标签）恒为 0 ⇒ 别把整列相加当成总分差。",
                EditorStyles.wordWrappedMiniLabel);

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

        /// <summary>单个 0~100 单项分：蓄力武器取满蓄与最低蓄力的均值（与主面板同口径）。</summary>
        private static float SubScore(WeaponScoreReport max, WeaponScoreReport min,
            Func<WeaponScoreReport, float> selector)
            => AvgSub(selector(max), selector(min), min.Valid);

        /// <summary>单发伤害的权重：投掷物不吃 DPS/续航，那三项的权重并进单发（合计 15%）。</summary>
        private static float PerTriggerWeight(WeaponScoreReport report)
            => report.IsThrowable
                ? WeightPerTrigger + WeightBurstDps + WeightSustainDps + WeightEndurance
                : WeightPerTrigger;

        /// <summary>
        /// 一行对照：差值 = 左（当前武器）- 右（对比武器）。
        /// <para>默认"越高越好" ⇒ &gt;0 绿；<paramref name="lowerIsBetter"/>（如击杀耗时）反之。</para>
        /// <para><paramref name="scoreDelta"/> = 这一项对综合评分的贡献差（单项分 × 权重，左-右）。
        /// 传 NaN（默认）表示该行不参与计分，末列画 "—" 占位，保持列对齐。</para>
        /// </summary>
        private void DrawCompareRow(string label, float current, float other, string format,
            bool lowerIsBetter = false, float scoreDelta = float.NaN)
        {
            // 隔行底色：奇数行铺一层淡白，偶数行不铺 —— 二十多行的长表不隔色很容易看串行
            var rowStyle = (_compareRowIndex++ & 1) == 0 ? _compareRowEvenStyle : _compareRowOddStyle;
            using (new EditorGUILayout.HorizontalScope(rowStyle))
            {
                GUILayout.Label(label, _compareCellStyle, GUILayout.Width(CompareLabelWidth));
                GUILayout.Label(current.ToString(format), _compareCellStyle, GUILayout.Width(CompareColWidth));
                GUILayout.Label(other.ToString(format), _compareCellStyle, GUILayout.Width(CompareColWidth));

                var oldColor = GUI.contentColor;
                var delta = current - other;
                GUI.contentColor = DeltaColor(delta, lowerIsBetter);
                GUILayout.Label(SignedText(delta, format), _compareDeltaStyle,
                    GUILayout.Width(CompareColWidth));

                // 末列是权重缩过的数：小权重(2%)差 10 分只有 0.2 ⇒ 固定两位小数，否则一片 0
                if (float.IsNaN(scoreDelta))
                {
                    GUI.contentColor = new Color(0.7f, 0.7f, 0.7f);
                    GUILayout.Label("—", _compareDeltaStyle, GUILayout.Width(CompareScoreWidth));
                }
                else
                {
                    GUI.contentColor = DeltaColor(scoreDelta, lowerIsBetter);
                    GUILayout.Label(SignedText(scoreDelta, "0.00"), _compareDeltaStyle,
                        GUILayout.Width(CompareScoreWidth));
                }

                GUI.contentColor = oldColor;
            }
        }

        /// <summary>差值着色：默认越高越好，<paramref name="lowerIsBetter"/> 反过来（越低越绿）。</summary>
        private static Color DeltaColor(float delta, bool lowerIsBetter)
        {
            if (Mathf.Approximately(delta, 0f)) return new Color(0.7f, 0.7f, 0.7f);
            var better = lowerIsBetter ? delta < 0f : delta > 0f;
            return better ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.95f, 0.5f, 0.45f);
        }

        /// <summary>带正负号的差值文本。<para>被格式吞掉的负零（"-0.00"）画出来像 bug ⇒ 去掉符号也不补 "+"。</para></summary>
        private static string SignedText(float value, string format)
        {
            var text = value.ToString(format);
            if (text.TrimStart('-', '0', '.').Length == 0) return text.TrimStart('-');
            return value > 0f ? "+" + text : text;
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
