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
    /// 样式构建与内部数据结构（评分报告 / 列表项）。
    /// </summary>
    public partial class WeaponUpgradeEditorWindow
    {
        // ========== 样式构建 ==========
        private void BuildStyles()
        {
            // ⚠ 不能只信 _stylesBuilt：Unity 域热重载会把缓存的 GUIStyle 字段还原成"默认空样式"
            //   （字号 0、黑字、clipping=Overflow），而 bool 标志本身照样是 true
            //   ⇒ 只看标志就会一直拿坏样式画，表现为整片黑字、行高塌掉。这里用哨兵样式自检，坏了重建。
            //   ⚠ 新加的样式也要进哨兵：只判 _compareCellStyle 时，隔行底色样式坏了不会被重建
            if (_stylesBuilt && _compareCellStyle != null && _compareCellStyle.fontSize == CompareCellFontSize
                && _compareRowOddStyle != null)
            {
                return;
            }

            _stylesBuilt = true;

            _cardStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(1, 1, 1, 1),
                normal = { background = MakeTex(1, 1, new Color(0.22f, 0.22f, 0.24f, 0.9f)) },
            };

            _cardSelectedStyle = new GUIStyle(_cardStyle)
            {
                normal = { background = MakeTex(1, 1, new Color(0.25f, 0.45f, 0.75f, 0.8f)) },
            };

            _activeCardStyle = new GUIStyle(_cardStyle)
            {
                normal = { background = MakeTex(1, 1, new Color(0.3f, 0.55f, 0.28f, 0.8f)) },
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.75f, 0.85f) },
            };

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.9f, 0.9f, 0.7f) },
            };

            _scoreBigStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 34,
                alignment = TextAnchor.MiddleLeft,
            };

            // 差值表用 label/boldLabel 打底而不是 miniLabel：mini 系列带 fixedHeight，放大字号会被裁字。
            // ⚠ 必须显式写 textColor：克隆出来的 EditorStyles 文本色是黑，在深色皮肤下会糊成一片，
            //   而且黑底乘 GUI.contentColor 永远是黑 ⇒ 差值的红绿也出不来。
            // clipping 用 Clip：武器名比列宽长时直接截断，别让它压到右边的列上。
            _compareHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = CompareCellFontSize,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.86f, 0.86f, 0.86f) },
            };

            _compareCellStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = CompareCellFontSize,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.82f, 0.82f, 0.82f) },
            };

            _compareDeltaStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = CompareDeltaFontSize,
                clipping = TextClipping.Clip,
                // 纯白：颜色全靠 GUI.contentColor 乘出来，底色带灰会把红绿压暗
                normal = { textColor = Color.white },
            };

            // 对比表隔行底色：HorizontalScope 是"先画样式背景、再画子控件" ⇒ 直接把背景塞进
            // 行容器样式，天然就是行底色。别改成"量完 rect 再 DrawRect"：DrawRect 只在 Repaint 生效，
            // 画在标签之后会盖住文字，画在之前又只有上一帧量得到的 rect（拖动窗口时会闪）。
            // ⚠ 必须克隆 GUIStyle.none（padding/margin 全 0）：列宽是硬编码的 684 贴着内容宽，
            //   带上边距或内边距会把最后一列顶出面板。
            _compareRowEvenStyle = new GUIStyle(GUIStyle.none);
            _compareRowOddStyle = new GUIStyle(GUIStyle.none)
            {
                normal = { background = MakeTex(1, 1, new Color(1f, 1f, 1f, 0.05f)) },
            };
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ========== 内部数据结构 ==========
        /// <summary>评分中间结果：数值既用于面板显示，也是评分锚点的输入。</summary>
        private struct WeaponScoreReport
        {
            public bool Valid;

            // ---- 原始统计值 ----
            public float DirectDamage;
            public bool UseCharge;          // DamageData.UseCharge：这一发吃不吃蓄力倍率
            public float ChargeRatio;       // 0 = 最低蓄力，1 = 满蓄
            public float ChargeScale;       // 当前比例下的直击/爆炸倍率
            public float ChargeRangeScale;  // 当前比例下的爆炸半径倍率
            public float DamagePerProjectile;
            public float DamagePerTrigger;
            public int ProjectilesPerTrigger;
            public int Muzzles;
            public int ShootCost;
            public float Interval;
            public float DamageDelay;       // 扣扳机到伤害真正落地之间的延迟（手雷 = 引信 MaxLifeTime，其余 0）
            public float FireRate;          // 每秒扣扳机次数（不含真空期）
            public float ClearRate;         // 摊掉换弹/过热后的实际节奏（清杂看它）
            public float ClearKillsIn10s;   // 10 秒能击杀的 50 血杂兵数量（枪械清杂的直接输入）
            public float ClearKillsPerThrow;    // 投掷物清杂：一颗能清几只（不吃备弹与节奏）
            public float ClearDamagePerTrigger; // 对"1 甲杂兵"的有效单发（已按护甲折算）
            public float TrailKills;            // 清杂里"尾迹持续伤害"单独折算的击杀数
            public float TrailRange;            // 尾迹的实际命中半径（尾迹档爆炸半径 + 尾迹范围加成）
            public float DerivativeRealDamage;  // 衍生物里真正扣血的部分（尾迹按时间轴折算要用）
            public float Reload;
            public float Magazine;
            public float Ammo;
            public float MagazineShots;     // 一匣能扣几次扳机
            public bool IsThrowable;        // 投掷物：不参与 DPS 计分，改按单发伤害评
            public bool HasHeat;            // 热量武器：节奏由热量决定，不是弹匣
            public float HeatPerShot;       // prefab 配的每发热量（含蓄力倍率）
            public float EffectiveHeat;     // 扣掉连射途中散掉的热量后，真正攒下的热量
            public float CoolSpeed;         // 散热速度（判断"会不会过热"要显示它）
            public float BurstShots;        // 一轮可连射发数（热量满 100 前）；0 = 永远不过热
            public float OverheatDuration;  // 过热锁定时间
            public float CoolDelay;         // 散热延迟（停火后多久才开始散热）
            public int LockLayers;          // 锁定类：满层数
            public float LockInterval;      // 锁定类：每层耗时（秒）
            public float LockTime;          // 锁定类：每发摊销的锁定耗时（= LockInterval，已计入 Interval）
            public float HeatCycleShots;    // 实战每轮发数（过热流 = BurstShots；主动散热流 = BurstShots-1）
            public float HeatCyclePause;    // 每轮停顿秒数（过热锁定 / 主动散热耗时）
            public bool HeatStyleVent;      // true = 主动停火散热比打满过热更快，节奏按它算
            public int HeatPauses;          // 打空一匣期间的过热次数
            public float DumpTime;          // 打空一匣耗时（含过热停顿）
            public float BurstDps;
            public float SustainedDps;
            public float MagazineDamage;    // 热量武器 = 到过热为止的一轮总伤；普通武器 = 整匣
            public float PoolDamage;        // 整池（弹匣打光）总伤，持续 DPS 用
            public float TotalDamage;
            public int AP;
            public float WeaknessBonus;
            public float ExplosionDamage;
            public float ExplosionRange;        // 满蓄后的实际半径
            public float BaseExplosionRange;    // prefab 里配的原始半径
            public float Spread;
            public float RecoilForce;
            public float BulletSpeed;
            public float EffectiveRange;
            public int UniqueTagCount;
            public int SpecialModCount;
            public string UniqueTagText;
            public float DemolishValue;     // 拆毁值（秒杀巢穴的门槛）
            public float DirectDestruction;     // 直击的护甲破坏系数（Destruction 成分）
            public float ExplosionDestruction;  // 爆炸的护甲破坏系数
            public float ArmorDamage;           // 每次扣扳机的护甲破坏值（含衍生物）
            public float HealthDamagePerProjectile; // 每颗弹丸真正扣血的伤害（不含破甲/纯控制成分）
            public float RealDamagePerTrigger;  // 每次扣扳机真正扣血的伤害（含衍生物）
            public float RealDamageRatio;       // 实际进血量 ÷ 面板单发（<1 = 混了破甲/控制成分；>1 = 多类型各结算一次）
            public float StatusPower;           // 每次扣扳机的异常状态成分值（控制能力）
            public bool IsBeamSplash;           // 光束把"作用范围"当命中半径（ProjectileLaser.useExplosionRange）
            public bool PenetratesUnits;        // 穿透单位（BulletFlag / 「穿透单位」专有属性 / 尾迹沿线）
            public bool TrailsUnits;            // 尾迹类：每拍沿飞行线段整条打穿（说明文案用）
            public float MultiTargetFactor;     // 一次节拍同时命中的目标数（普通子弹 = 1，只喂清杂）
            public float EffectiveDamagePerTrigger; // 单体有效伤害 × 目标数：只用于多目标展示
            public float HeavyArmorLevel;       // 反重假设的目标护甲等级
            public float HeavyDamagePerTrigger; // 对重型目标躯干实际生效的单发伤害
            public float HeavyDamageFactor;     // 有效比例（AP 不足时 <1，AP 差 3 级 = 0）
            public float HeavyTtk;              // 打死 1000 血 4 甲 + 350 血 4 甲头部（弱点）要几秒（0 = 打不死）
            public float WeakPointPrecision;    // 命中弱点（头部）的比例：人机工学换算（分散布+后坐），0~0.9
            public float FirstShotPrecision;    // 首发命中率：只看散布（后坐力要打完一发才顶起来），0~0.9
            public float MediumDamagePerTrigger; // 反中：按命中率混合后的等效单发（躯干 3 甲 / 头 1 甲）
            public float MediumTtk;             // 打死 500 血 3 甲 + 200 血 1 甲头部（弱点）要几秒（0 = 打不死）
            public float DerivativeDamage;      // 衍生物额外总伤（已并入 DamagePerTrigger）
            public float DerivativeArmorDamage; // 衍生物里的护甲破坏值
            public float DerivativeDuration;    // 衍生物存活时长
            public string DerivativeText;       // 衍生物来源文案
            public int LoadoutCount;            // 已选改装项数（评分页的模拟升级/模组；0 = 原始数据）

            // ---- 单项分（0~100）----
            public float ScoreBurstDps;
            public float ScoreSustainDps;
            public float ScorePerTrigger;
            public float ScoreMagazineDamage;   // 一轮总伤分（仅参考/对比，不单独计分）
            public float ScoreTotalDamage;      // 整池总伤分（并入续航的 30%）
            public float EnduranceSeconds;      // 续航：整池弹药打完的实际秒数
            public float ScoreEndurance;        // 续航分 = 时长 70% + 整池总伤 30%
            public float ScoreAp;
            public float ScoreArmorBreak;   // 破甲（直击/爆炸里的护甲破坏成分）
            public float ScoreControl;      // 控制（异常状态成分）
            public float ScoreHeavyOutput;     // 反重输入：TTK 分（0 = 打不死）
            public float ScoreMediumOutput;    // 反中输入：TTK 分（0 = 打不死）
            public float ScoreWeakness;
            public float ScoreAoe;
            public float ScoreSpread;
            public float ScoreRecoil;
            public float ScoreHandling;     // 人机工学 = 散布 50% + 后坐力 50%
            public float ScoreSpeed;
            public float ScoreRange;
            public float ScoreBallistics;
            public float ScoreSpecial;
            public float ScoreClear;        // 清杂（持续 + 弹匣 + 范围）
            public float ScoreAntiHeavy;    // 反重（打靶输出 80% + 破甲 20%）
            public float ScoreAntiMedium;   // 反中（500 血 3 甲 + 200 血 1 甲头部弱点）
            public float ScoreDemolish;     // 拆巢（拆毁值）
            public float Overall;
        }

        private class WeaponAssetEntry
        {
            public GameObject Prefab;
            public WeaponPlayerController Controller;
            public string DisplayName;
            public Sprite Icon;
            public string TypeName;
            public int TypeEnum;
        }
    }
}
