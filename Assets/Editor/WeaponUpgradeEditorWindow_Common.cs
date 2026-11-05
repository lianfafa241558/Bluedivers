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
            if (_stylesBuilt && _compareCellStyle != null && _compareCellStyle.fontSize == CompareCellFontSize)
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
            public float FireRate;          // 每秒扣扳机次数（不含真空期）
            public float ClearRate;         // 摊掉换弹/过热后的实际节奏（清杂看它）
            public float ClearKillsIn10s;   // 10 秒能击杀的 50 血杂兵数量（清杂的直接输入）
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
            public float RealDamageRatio;       // 有效伤害占名义总伤的比例（<1 = 混了破甲/控制成分）
            public float StatusPower;           // 每次扣扳机的异常状态成分值（控制能力）
            public bool IsBeamSplash;           // 光束把"作用范围"当命中半径（ProjectileLaser.useExplosionRange）
            public bool PenetratesUnits;        // 穿透单位（BulletFlag 或「穿透单位」专有属性）
            public float MultiTargetFactor;     // 一次节拍同时命中的目标数（普通子弹 = 1，只喂清杂）
            public float EffectiveDamagePerTrigger; // 单体有效伤害 × 目标数：只用于多目标展示
            public float HeavyArmorLevel;       // 反重假设的目标护甲等级
            public float HeavyDamagePerTrigger; // 对重型目标实际生效的单发伤害
            public float HeavyDamageFactor;     // 有效比例（AP 不足时 <1，AP 差 3 级 = 0）
            public float HeavyTtk;              // 打死 1000 血 4 甲单位要几秒（0 = 打不死）
            public float DerivativeDamage;      // 衍生物额外总伤（已并入 DamagePerTrigger）
            public float DerivativeArmorDamage; // 衍生物里的护甲破坏值
            public float DerivativeDuration;    // 衍生物存活时长
            public string DerivativeText;       // 衍生物来源文案

            // ---- 单项分（0~100）----
            public float ScoreBurstDps;
            public float ScoreSustainDps;
            public float ScorePerTrigger;
            public float ScoreMagazineDamage;
            public float ScoreTotalDamage;
            public float ScoreAp;
            public float ScoreArmorBreak;   // 破甲（直击/爆炸里的护甲破坏成分）
            public float ScoreControl;      // 控制（异常状态成分）
            public float ScoreHeavyOutput;     // 反重输入：TTK 分（0 = 打不死）
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
            public float ScoreAntiHeavy;    // 反重（穿甲 + 单发 + 弱点）
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
