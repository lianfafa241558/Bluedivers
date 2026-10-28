using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FPSGame.Game;
using FPSGame.Weapon;

namespace FPSGame.EditorExt
{

[CustomPropertyDrawer(typeof(DamageData))]
public class DamageDataDrawer : PropertyDrawer
{ 
    protected const float Padding = 4f;
    protected const float HeaderHeight = 20f;
    protected const float LineHeight = 18f;
    protected const float Gap = 8f;
    protected const float PairLabelWidth = 140f;
    private const float BoxInnerTop = 3f;
    private const float BoxInnerBottom = 5f;
    private const string LeftBoxTitle = "基础";
    private const string RightBoxTitle = "满蓄";
    // 编译标记：仅用于在编译产物里确认此版本已加载，勿删
    private const string CompileMarker = "SplitBoxesV3";

    /// <summary>单行内联列表样式（绘制实现在 InlineFieldDrawer，本类只选样式）</summary>
    protected static readonly InlineListStyle SinglelineStyle = new InlineListStyle
    {
        ElementLabelExtraWidth = 64f,
        ExtraBottomSpace = Padding,
    };

    /// <summary>同上，头部标签加粗（爆炸伤害区使用）</summary>
    protected static readonly InlineListStyle SinglelineStyleBold = new InlineListStyle
    {
        BoldHeaderLabel = true,
        ElementLabelExtraWidth = 64f,
        ExtraBottomSpace = Padding,
    };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var headerRect = new Rect(position.x, position.y, position.width, HeaderHeight);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = headerRect.y + HeaderHeight + Padding;

            y = DrawSection_Motion(position, property, y);
            y = DrawSection_DirectDamage(position, property, y);
            y = DrawSection_ExplosionDamage(position, property, y);
            y = DrawSection_Collision(position, property, y);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return HeaderHeight;

        float height = HeaderHeight + Padding;
        height += GetSectionHeight_Motion(property);
        height += GetSectionHeight_DirectDamage(property);
        height += GetSectionHeight_ExplosionDamage(property);
        height += GetSectionHeight_Collision(property);
        return height;
    }

    #region 运动

    private float DrawSection_Motion(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "运动", ref y);
        var useCharge = property.FindPropertyRelative("UseCharge");

        EditorGUI.indentLevel++;
        y = DrawProperty(property, "BulletPrefab", "子弹", position, y);
        y = DrawProperty(property, "LandingPrefab", "落点预制体(开火预测落点)", position, y);
        y = DrawProperty(property, "UseCharge", "使用蓄力", position, y);

        if (useCharge.boolValue)
        {
            // 勾选蓄力后其余运动字段进左右框体：左=原字段，右=同行蓄力倍率(无对应则空行)
            y = DrawSplitBoxes(position, y, BuildChargeRows_Motion(property));
        }
        else
        {
            y = DrawProperty(property, "Speed", "投掷物的速度", position, y);
            y = DrawProperty(property, "Gravity", "下坠速度", position, y);
            y = DrawProperty(property, "SoundRadius", "发出的声音影响范围", position, y);
            // 逻辑层噪声半径(AI 听觉用)；表现层音效距离是上面的 SoundRadius，两者分开
            y = DrawProperty(property, "ImpactSoundRadius", "命中噪声半径(逻辑层)", position, y);
            y = DrawProperty(property, "MinRange", "安全引信(单位:M)", position, y);
            y = DrawProperty(property, "MaxRange", "自爆引信(单位:M)", position, y);
            y = DrawProperty(property, "MaxLifeTime", "生命周期", position, y);
            y = DrawProperty(property, "InheritWeaponSpeed", "继承武器初速度", position, y);
            y = DrawProperty(property, "NoSource", "无源伤害", position, y);
            y = DrawProperty(property, "WeaknessBonus", "弱点加成", position, y);
        }
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_Motion(SerializedProperty property)
    {
        var useCharge = property.FindPropertyRelative("UseCharge");
        float h = SectionHeaderHeight;
        h += RowHeight(property, "BulletPrefab");
        h += RowHeight(property, "LandingPrefab");
        h += RowHeight(property, "UseCharge");

        if (useCharge.boolValue)
        {
            h += GetSplitBoxesHeight(BuildChargeRows_Motion(property));
        }
        else
        {
            h += RowHeight(property, "Speed");
            h += RowHeight(property, "Gravity");
            h += RowHeight(property, "SoundRadius");
            h += RowHeight(property, "ImpactSoundRadius");
            h += RowHeight(property, "MinRange");
            h += RowHeight(property, "MaxRange");
            h += RowHeight(property, "MaxLifeTime");
            h += RowHeight(property, "InheritWeaponSpeed");
            h += RowHeight(property, "NoSource");
            h += RowHeight(property, "WeaknessBonus");
        }
        h += Padding;
        return h;
    }

    #endregion

    #region 直接伤害

    private float DrawSection_DirectDamage(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "直接伤害", ref y);
        var useCharge = property.FindPropertyRelative("UseCharge");

        EditorGUI.indentLevel++;
        if (useCharge.boolValue)
        {
            // 勾选蓄力后进左右框体：左=原字段，右=同行满蓄倍率(无对应则空行)
            y = DrawSplitBoxes(position, y, BuildChargeRows_Direct(property));
        }
        else
        {
            y = DrawProperty(property, "DamageDirect", "直接伤害值", position, y);
            y = DrawProperty(property, "DirectAP", "直击穿甲等级", position, y);
            y = DrawProperty(property, "demolishValue", "拆毁值", position, y);
        }
        // 伤害成分列表无蓄力对应项，保持整行宽绘制
        y = DrawSinglelineList(property, "DamageGroupDirect", "伤害成分", position, y);
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_DirectDamage(SerializedProperty property)
    {
        var useCharge = property.FindPropertyRelative("UseCharge");
        float h = SectionHeaderHeight;
        if (useCharge.boolValue)
        {
            h += GetSplitBoxesHeight(BuildChargeRows_Direct(property));
        }
        else
        {
            h += RowHeight(property, "DamageDirect");
            h += RowHeight(property, "DirectAP");
            h += RowHeight(property, "demolishValue");
        }
        h += GetSinglelineListHeight(property, "DamageGroupDirect");
        h += Padding;
        return h;
    }

    #endregion

    #region 爆炸伤害

    private float DrawSection_ExplosionDamage(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "爆炸伤害", ref y);
        var useCharge = property.FindPropertyRelative("UseCharge");
        var explosionProp = property.FindPropertyRelative("DamageExplosion");

        EditorGUI.indentLevel++;
        if (useCharge.boolValue)
        {
            // 勾选蓄力后进左右框体：左=原字段，右=同行满蓄倍率(无对应则空行)
            y = DrawSplitBoxes(position, y, BuildChargeRows_Explosion(property));
        }
        else
        {
            y = DrawProperty(property, "DamageExplosion", "爆炸伤害值", position, y);

            if (explosionProp.floatValue > 0)
            {
                EditorGUI.indentLevel++;
                // 爆炸伤害为 0 时不显示穿甲/范围等子字段
                y = DrawProperty(property, "ExplosionAP", "爆炸穿甲等级", position, y);
                y = DrawProperty(property, "ExplosionInnerRange", "伤害内半径", position, y);
                y = DrawProperty(property, "ExplosionRange", "伤害外半径", position, y);
                y = DrawProperty(property, "DestructeRadius", "地形破坏半径", position, y);
                y = DrawProperty(property, "ShockwaveRadius", "冲击波半径(范围)", position, y);
                // 力度与半径已解耦；半径为 0 表示没有冲击波 ⇒ 力度无意义，整行不画
                if (property.FindPropertyRelative("ShockwaveRadius").floatValue > 0)
                    y = DrawProperty(property, "ShockwaveForce", "冲击波力度(冲量)", position, y);
                EditorGUI.indentLevel--;
            }
        }

        if (explosionProp.floatValue > 0)
        {
            // 伤害成分列表无蓄力对应项，保持整行宽绘制
            y = DrawSinglelineList(property, "DamageGroupExplosion", "伤害成分", position, y, true);
        }

        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_ExplosionDamage(SerializedProperty property)
    {
        var explosionProp = property.FindPropertyRelative("DamageExplosion");
        var useCharge = property.FindPropertyRelative("UseCharge");
        float ev = explosionProp.floatValue;

        float h = SectionHeaderHeight;
        if (useCharge.boolValue)
        {
            h += GetSplitBoxesHeight(BuildChargeRows_Explosion(property));
        }
        else
        {
            h += RowHeight(property, "DamageExplosion");
            // 爆炸伤害为 0 时只显示伤害值本身，其余行(穿甲/范围等)一并隐藏
            if (ev > 0)
            {
                h += RowHeight(property, "ExplosionAP");
                h += RowHeight(property, "ExplosionInnerRange");
                h += RowHeight(property, "ExplosionRange");
                h += RowHeight(property, "DestructeRadius");
                h += RowHeight(property, "ShockwaveRadius");
                if (property.FindPropertyRelative("ShockwaveRadius").floatValue > 0)
                    h += RowHeight(property, "ShockwaveForce");
            }
        }

        if (ev > 0)
            h += GetSinglelineListHeight(property, "DamageGroupExplosion");

        h += Padding;
        return h;
    }

    #endregion

    #region 碰撞

    protected float DrawSection_Collision(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "碰撞", ref y);

        EditorGUI.indentLevel++;
        y = DrawProperty(property, "UseCollisionDirection", "特效使用碰撞点的朝向", position, y);
        y = DrawProperty(property, "ImpactVfxSpawnOffset", "特效沿法线偏移量", position, y);
        y = DrawProperty(property, "ImpactVfx", "碰撞特效", position, y);
        y = DrawProperty(property, "ImpactSfx", "碰撞音效", position, y);
        y = DrawProperty(property, "OnlyTerrain", "只附着到地面", position, y);
        var useHole = property.FindPropertyRelative("UseHole");
        y = DrawProperty(property, "UseHole", "创建弹坑", position, y);
        if (useHole.boolValue)
        {
            EditorGUI.indentLevel++;
            y = DrawProperty(property, "Hole", "弹坑/不填使用默认", position, y);

            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    protected float GetSectionHeight_Collision(SerializedProperty property)
    {
        var useHole = property.FindPropertyRelative("UseHole");
        float h = SectionHeaderHeight;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("UseCollisionDirection")) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ImpactVfxSpawnOffset")) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ImpactVfx")) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ImpactSfx")) + 2;
        h += EditorGUI.GetPropertyHeight(useHole) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("OnlyTerrain")) + 2;

        if (useHole.boolValue)
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("Hole")) + 2;
        }
        h += Padding;
        return h;
    }

    #endregion

    #region 辅助

    protected float SectionHeaderHeight => EditorGUIUtility.singleLineHeight + 2;

    protected void DrawSectionHeader(Rect position, string title, ref float y)
    {
        var rect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);
        y += EditorGUIUtility.singleLineHeight + 2;
    }

    /// <summary>并行绘制两个属性，左右各占一半，标签固定宽度 140</summary>
    protected float DrawPairedProperties(SerializedProperty property, string prop1Name, string label1,
        string prop2Name, string label2, Rect position, float y)
    {
        var prop1 = property.FindPropertyRelative(prop1Name);
        var prop2 = property.FindPropertyRelative(prop2Name);
        if (prop1 == null || prop2 == null) return y;

        float halfWidth = (position.width - Gap) * 0.5f;
        float inputWidth = halfWidth - PairLabelWidth;

        var rect1 = new Rect(position.x, y, halfWidth, LineHeight);
        EditorGUI.LabelField(new Rect(rect1.x, rect1.y, PairLabelWidth, LineHeight), label1);
        EditorGUI.PropertyField(new Rect(rect1.x + PairLabelWidth, rect1.y, inputWidth, LineHeight),
            prop1, GUIContent.none, true);

        var rect2 = new Rect(position.x + halfWidth + Gap, y, halfWidth, LineHeight);
        EditorGUI.LabelField(new Rect(rect2.x, rect2.y, PairLabelWidth, LineHeight), label2);
        EditorGUI.PropertyField(new Rect(rect2.x + PairLabelWidth, rect2.y, inputWidth, LineHeight),
            prop2, GUIContent.none, true);

        return y + LineHeight + 2;
    }

    /// <summary>
    /// 左右框体中的一行：左=原字段，右=同行蓄力字段；任一侧为 null 即该侧空行。
    /// 绘制与测高共用同一份行数据，保证 GetPropertyHeight 与 OnGUI 严格一致。
    /// </summary>
    private sealed class SplitRow
    {
        public SerializedProperty Left;
        public string LeftLabel;
        public string LeftTip;
        public SerializedProperty Right;
        public string RightLabel;
        public string RightTip;
        public float Height;

        public static SplitRow Pair(SerializedProperty left, string leftLabel, string leftTip,
            SerializedProperty right, string rightLabel, string rightTip)
        {
            return new SplitRow
            {
                Left = left,
                LeftLabel = leftLabel,
                LeftTip = leftTip,
                Right = right,
                RightLabel = rightLabel,
                RightTip = rightTip,
                Height = Mathf.Max(PropHeight(left), PropHeight(right)),
            };
        }

        public static SplitRow LeftOnly(SerializedProperty left, string label, string tip)
        {
            return new SplitRow { Left = left, LeftLabel = label, LeftTip = tip, Height = PropHeight(left) };
        }

        public static SplitRow RightOnly(SerializedProperty right, string label, string tip)
        {
            return new SplitRow { Right = right, RightLabel = label, RightTip = tip, Height = PropHeight(right) };
        }

        private static float PropHeight(SerializedProperty prop) =>
            prop == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(prop);
    }

    /// <summary>勾选蓄力后"运动"区剩余字段进左右框体(标签精简、完整说明放 tooltip)</summary>
    private static List<SplitRow> BuildChargeRows_Motion(SerializedProperty property)
    {
        SerializedProperty P(string name) => property.FindPropertyRelative(name);

        return new List<SplitRow>
        {
            SplitRow.Pair(P("Speed"), "速度", "投掷物的速度", P("ChargeSpeedScale"), "速度倍率", "满蓄子弹速度倍率"),
            SplitRow.Pair(P("Gravity"), "下坠速度", null, P("ChargeGravityScale"), "重力倍率", "满蓄子弹重力倍率"),
            SplitRow.Pair(P("SoundRadius"), "声音范围", "发出的声音影响范围", P("ChargeSoundScale"), "声音倍率", "满蓄声音范围倍率"),
            SplitRow.LeftOnly(P("ImpactSoundRadius"), "命中噪声", "命中点的逻辑层噪声半径(AI 听觉)"),
            SplitRow.RightOnly(P("ChargeHeatScale"), "热量倍率", "满蓄热量倍率"),
            SplitRow.RightOnly(P("ChargeSpreadScale"), "散布倍率", "满蓄散布倍率"),
            SplitRow.LeftOnly(P("MinRange"), "安全引信", "安全引信(单位:M)"),
            SplitRow.LeftOnly(P("MaxRange"), "自爆引信", "自爆引信(单位:M)"),
            SplitRow.LeftOnly(P("MaxLifeTime"), "生命周期", null),
            SplitRow.LeftOnly(P("InheritWeaponSpeed"), "继承初速", "继承武器初速度"),
            SplitRow.LeftOnly(P("NoSource"), "无源伤害", null),
            SplitRow.LeftOnly(P("WeaknessBonus"), "弱点加成", null),
        };
    }

    /// <summary>勾选蓄力后"直接伤害"区进左右框体</summary>
    private static List<SplitRow> BuildChargeRows_Direct(SerializedProperty property)
    {
        SerializedProperty P(string name) => property.FindPropertyRelative(name);

        return new List<SplitRow>
        {
            SplitRow.Pair(P("DamageDirect"), "直击伤害", "直接伤害值", P("ChargeDamageScale"), "伤害倍率", "满蓄伤害倍率"),
            SplitRow.Pair(P("DirectAP"), "直击穿甲", "直击穿甲等级", P("DirectAPChargeScale"), "穿甲倍率", "满蓄直击穿甲倍率"),
            SplitRow.LeftOnly(P("demolishValue"), "拆毁值", null),
        };
    }

    /// <summary>勾选蓄力后"爆炸伤害"区进左右框体(爆炸伤害为 0 时子字段与旧版一致隐藏)</summary>
    private static List<SplitRow> BuildChargeRows_Explosion(SerializedProperty property)
    {
        SerializedProperty P(string name) => property.FindPropertyRelative(name);

        var rows = new List<SplitRow>
        {
            SplitRow.Pair(P("DamageExplosion"), "爆炸伤害", "爆炸伤害值", P("ChargeDamageScale"), "伤害倍率", "满蓄伤害倍率"),
        };

        if (property.FindPropertyRelative("DamageExplosion").floatValue > 0)
        {
            rows.Add(SplitRow.Pair(P("ExplosionAP"), "爆炸穿甲", "爆炸穿甲等级", P("ExplosionAPChargeScale"), "穿甲倍率", "满蓄爆炸穿甲倍率"));
            rows.Add(SplitRow.LeftOnly(P("ExplosionInnerRange"), "伤害内半径", null));
            // 溅射倍率同时缩放内/外/破坏/冲击波半径 ⇒ 与半径组同排，别掉到框底单独一行
            rows.Add(SplitRow.Pair(P("ExplosionRange"), "伤害外半径", null, P("ChargeAOERangeScale"), "溅射倍率", "满蓄溅射范围倍率"));
            rows.Add(SplitRow.LeftOnly(P("DestructeRadius"), "破坏半径", "地形破坏半径"));
            rows.Add(SplitRow.LeftOnly(P("ShockwaveRadius"), "冲击波半径", "冲击波作用范围"));
            // 半径为 0 表示没有冲击波 ⇒ 力度无意义，整行不画
            if (P("ShockwaveRadius").floatValue > 0)
                rows.Add(SplitRow.LeftOnly(P("ShockwaveForce"), "冲击波力度", "冲量：Δv = 力度×距离衰减 / 重量"));
        }
        return rows;
    }

    /// <summary>左右框体占用的总高度(含框体下方 2px 间隔)，与 DrawSplitBoxes 返回值一致</summary>
    private static float GetSplitBoxesHeight(List<SplitRow> rows)
    {
        return GetSplitBoxHeight(rows) + 2f;
    }

    private static float GetSplitBoxHeight(List<SplitRow> rows)
    {
        float h = BoxInnerTop + EditorGUIUtility.singleLineHeight + 2f;
        foreach (var row in rows)
            h += row.Height + 2f;
        return h + BoxInnerBottom;
    }

    /// <summary>绘制左右两个框体：左=原字段("基础")，右=同行蓄力字段("满蓄")，无对应字段的行留空</summary>
    private static float DrawSplitBoxes(Rect position, float y, List<SplitRow> rows)
    {
        float boxHeight = GetSplitBoxHeight(rows);
        float halfWidth = (position.width - Gap) * 0.5f;
        var leftBox = new Rect(position.x, y, halfWidth, boxHeight);
        var rightBox = new Rect(position.x + halfWidth + Gap, y, halfWidth, boxHeight);
        GUI.Box(leftBox, GUIContent.none, EditorStyles.helpBox);
        GUI.Box(rightBox, GUIContent.none, EditorStyles.helpBox);

        // 标签宽按字符类别保守估宽(见 LabelRenderWidth)，框内取最长标签对齐，最多占框体六成
        float labelWidth = Mathf.Min(MeasureSplitLabelWidth(rows), halfWidth * 0.62f);

        float titleH = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(new Rect(leftBox.x + 4f, y + BoxInnerTop, halfWidth - 8f, titleH), LeftBoxTitle, EditorStyles.miniBoldLabel);
        EditorGUI.LabelField(new Rect(rightBox.x + 4f, y + BoxInnerTop, halfWidth - 8f, titleH), RightBoxTitle, EditorStyles.miniBoldLabel);

        float iy = y + BoxInnerTop + titleH + 2f;
        foreach (var row in rows)
        {
            DrawSplitSide(leftBox.x, iy, halfWidth, labelWidth, row.Left, row.LeftLabel, row.LeftTip);
            DrawSplitSide(rightBox.x, iy, halfWidth, labelWidth, row.Right, row.RightLabel, row.RightTip);
            iy += row.Height + 2f;
        }
        return y + boxHeight + 2f;
    }

    /// <summary>取框内所有标签估宽最大值(含 6px 缓冲)，绘制与测高共用</summary>
    private static float MeasureSplitLabelWidth(List<SplitRow> rows)
    {
        float w = 0f;
        foreach (var row in rows)
        {
            w = Mathf.Max(w, LabelRenderWidth(row.LeftLabel));
            w = Mathf.Max(w, LabelRenderWidth(row.RightLabel));
        }
        return w + 6f;
    }

    /// <summary>
    /// 按字符类别保守估标签渲染宽。
    /// 陷阱：编辑器主字体(LegacyRuntime)对 CJK 的 HasCharacter/CalcSize 度量与实际回退渲染宽度不一致，
    /// 用 CalcSize 会截断中文字段名，故 CJK 直接按最大可能字宽 22px 估(字号 12 时实测回退渲染约 16~20px)。
    /// </summary>
    private static float LabelRenderWidth(string label)
    {
        if (string.IsNullOrEmpty(label)) return 0f;

        float w = 0f;
        foreach (char c in label)
        {
            bool isCjk = (c >= 0x2E80 && c <= 0x9FFF)   // CJK 部首/汉字
                || (c >= 0xF900 && c <= 0xFAFF)          // CJK 兼容表意
                || (c >= 0xFF00 && c <= 0xFFEF);         // 全角符号
            w += isCjk ? 22f : 8f;
        }
        return w;
    }

    private static void DrawSplitSide(float x, float y, float width, float labelWidth, SerializedProperty prop, string label, string tip)
    {
        if (prop == null) return; // 空行：该侧无对应字段

        var labelRect = new Rect(x + 4f, y, labelWidth, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(labelRect, string.IsNullOrEmpty(tip) ? new GUIContent(label) : new GUIContent(label, tip));
        var fieldRect = new Rect(x + 4f + labelWidth, y, width - 8f - labelWidth, EditorGUI.GetPropertyHeight(prop));
        EditorGUI.PropertyField(fieldRect, prop, GUIContent.none, true);
    }

    /// <summary>单行字段高度(含行距 2px)，测高用</summary>
    private static float RowHeight(SerializedProperty property, string propName)
    {
        var prop = property.FindPropertyRelative(propName);
        return prop == null ? 0f : EditorGUI.GetPropertyHeight(prop) + 2f;
    }

    #endregion

    #region Helper

    protected float DrawProperty(SerializedProperty property, string propName, string label, Rect position, float y)
    {
        var prop = property.FindPropertyRelative(propName);
        if (prop == null) return y;

        var rect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(prop));
        EditorGUI.PropertyField(rect, prop, new GUIContent(label), true);
        return y + rect.height + 2;
    }

    /// <summary>
    /// 单行列表绘制(用于 List&lt;SKVP&lt;,&gt;&gt; 这类带 [Singleline] 元素的列表)。
    /// 实现已统一收敛到 <see cref="InlineFieldDrawer"/>，这里只提供本 Drawer 使用的样式。
    /// </summary>
    protected float DrawSinglelineList(SerializedProperty property, string propName, string label, Rect position, float y, bool bold = false)
    {
        var listProp = property.FindPropertyRelative(propName);
        if (listProp == null) return y;

        return InlineFieldDrawer.DrawInlineListRect(position, listProp, label, y,
            bold ? SinglelineStyleBold : SinglelineStyle);
    }

    protected float GetSinglelineListHeight(SerializedProperty property, string propName)
    {
        var listProp = property.FindPropertyRelative(propName);
        if (listProp == null) return 0;

        return InlineFieldDrawer.GetInlineListHeight(listProp, SinglelineStyle);
    }

    #endregion
}

/// <summary>SustainedDamageData 持续效果专用伤害配置检视器(无运动/直击/蓄力)</summary>
[CustomPropertyDrawer(typeof(SustainedDamageData))]
public class SustainedDamageDataDrawer : DamageDataDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var headerRect = new Rect(position.x, position.y, position.width, HeaderHeight);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = headerRect.y + HeaderHeight + Padding;

            y = DrawSection_General(position, property, y);
            y = DrawSection_Explosion(position, property, y);
            y = DrawSection_Collision(position, property, y);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return HeaderHeight;

        float height = HeaderHeight + Padding;
        height += GetSectionHeight_General(property);
        height += GetSectionHeight_Explosion(property);
        height += GetSectionHeight_Collision(property);
        return height;
    }

    #region 通用

    private float DrawSection_General(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "通用", ref y);

        EditorGUI.indentLevel++;
        y = DrawProperty(property, "NoSource", "无源伤害", position, y);
        y = DrawProperty(property, "SoundRadius", "发出的声音影响范围", position, y);
        // 逻辑层噪声半径(AI 听觉用)；表现层音效距离是上面的 SoundRadius，两者分开
        y = DrawProperty(property, "ImpactSoundRadius", "命中噪声半径(逻辑层)", position, y);
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_General(SerializedProperty property)
    {
        // 固定 3 行(无源伤害 / 声音影响范围 / 命中噪声半径)，别漏改
        return SectionHeaderHeight + 3 * (LineHeight + 2) + Padding;
    }

    #endregion

    #region 爆炸伤害

    private float DrawSection_Explosion(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "爆炸伤害", ref y);
        var explosionProp = property.FindPropertyRelative("DamageExplosion");

        EditorGUI.indentLevel++;

        y = DrawProperty(property, "demolishValue", "拆毁值", position, y);
        y = DrawProperty(property, "ExplosionAP", "穿甲等级", position, y);
        y = DrawProperty(property, "DamageExplosion", "爆炸伤害值", position, y);


        if (explosionProp.floatValue > 0)
        {
            EditorGUI.indentLevel++;
            y = DrawPairedProperties(property, "ExplosionInnerRange", "伤害内半径", "ExplosionRange", "伤害外半径", position, y);
            y = DrawProperty(property, "DestructeRadius", "地形破坏半径", position, y);
            y = DrawProperty(property, "ShockwaveRadius", "冲击波半径(范围)", position, y);
            // 半径为 0 表示没有冲击波 ⇒ 力度无意义，整行不画
            if (property.FindPropertyRelative("ShockwaveRadius").floatValue > 0)
                y = DrawProperty(property, "ShockwaveForce", "冲击波力度(冲量)", position, y);
            y = DrawSinglelineList(property, "DamageGroupExplosion", "伤害成分", position, y, true);
            EditorGUI.indentLevel--; 
        }

        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_Explosion(SerializedProperty property)
    {
        var explosionProp = property.FindPropertyRelative("DamageExplosion");
        float ev = explosionProp.floatValue;

        float h = SectionHeaderHeight;
        h += EditorGUI.GetPropertyHeight(explosionProp) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("demolishValue")) + 2;

        if (ev > 0)
        {
            h += LineHeight + 2; // ExplosionInnerRange + ExplosionRange (paired)
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DestructeRadius")) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ShockwaveRadius")) + 2;
            if (property.FindPropertyRelative("ShockwaveRadius").floatValue > 0)
                h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ShockwaveForce")) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ExplosionAP")) + 2;
            h += GetSinglelineListHeight(property, "DamageGroupExplosion");
        }

        h += Padding;
        return h;
    }

    #endregion
}
}
