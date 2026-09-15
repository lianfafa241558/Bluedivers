using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.FPS.Game;

[CustomPropertyDrawer(typeof(DamageData))]
public class DamageDataDrawer : PropertyDrawer
{ 
    protected const float Padding = 4f;
    protected const float HeaderHeight = 20f;
    protected const float LineHeight = 18f;
    protected const float Gap = 8f;
    protected const float PairLabelWidth = 140f;

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
        y = DrawPropertyOrPaired(property, useCharge, "Speed", "ChargeSpeedScale", "投掷物的速度", "蓄力倍率", position, y);
        y = DrawPropertyOrPaired(property, useCharge, "Gravity", "ChargeGravityScale", "下坠速度", "蓄力倍率", position, y);
        y = DrawPropertyOrPaired(property, useCharge, "SoundRadius", "ChargeSoundScale", "发出的声音影响范围", "蓄力倍率", position, y);
        if (useCharge.boolValue)
        {
            // 未勾选蓄力时不显示这两个满蓄倍率
            y = DrawProperty(property, "ChargeHeatScale", "满蓄热量倍率", position, y);
            y = DrawProperty(property, "ChargeSpreadScale", "满蓄散布倍率", position, y);
        }
        y = DrawPairedProperties(property, "MinRange", "安全引信(单位:M)", "MaxRange", "自爆引信(单位:M)", position, y);
        y = DrawProperty(property, "MaxLifeTime", "生命周期", position, y);
        y = DrawProperty(property, "InheritWeaponSpeed", "继承武器初速度", position, y);
        y = DrawProperty(property, "NoSource", "无源伤害", position, y);
        y = DrawProperty(property, "WeaknessBonus", "弱点加成", position, y);
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_Motion(SerializedProperty property)
    {
        // 固定 11 行，勾选蓄力后额外增加"满蓄热量/散布倍率"2 行
        var useCharge = property.FindPropertyRelative("UseCharge");
        int rows = 11;
        if (useCharge != null && useCharge.boolValue) rows += 2;
        return SectionHeaderHeight + rows * (LineHeight + 2) + Padding;
    }

    #endregion

    #region 直接伤害

    private float DrawSection_DirectDamage(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "直接伤害", ref y);
        var useCharge = property.FindPropertyRelative("UseCharge");

        EditorGUI.indentLevel++;
        y = DrawPropertyOrPaired(property, useCharge, "DamageDirect", "ChargeDamageScale", "直接伤害值", "满蓄伤害倍率", position, y);
        y = DrawPropertyOrPaired(property, useCharge, "DirectAP", "DirectAPChargeScale", "直击穿甲等级", "满蓄直击穿甲倍率", position, y);
        y = DrawProperty(property, "demolishValue", "拆毁值", position, y);
        y = DrawSinglelineList(property, "DamageGroupDirect", "伤害成分", position, y);
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_DirectDamage(SerializedProperty property)
    {
        float h = SectionHeaderHeight;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DamageDirect")) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DirectAP")) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("demolishValue")) + 2;
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
        y = DrawPropertyOrPaired(property, useCharge, "DamageExplosion", "ChargeDamageScale", "爆炸伤害值", "满蓄倍率", position, y);

        if (explosionProp.floatValue > 0)
        {
            EditorGUI.indentLevel++;
            // 爆炸伤害为 0 时不显示爆炸穿甲等级(含满蓄倍率)
            y = DrawPropertyOrPaired(property, useCharge, "ExplosionAP", "ExplosionAPChargeScale", "爆炸穿甲等级", "满蓄爆炸穿甲倍率", position, y);
            y = DrawPairedProperties(property, "ExplosionInnerRange", "伤害内半径", "ExplosionRange", "伤害外半径", position, y);
            y = DrawProperty(property, "DestructeRadius", "地形破坏半径", position, y);
            y = DrawProperty(property, "ShockwaveRadius", "冲击波半径", position, y);
            y = DrawSinglelineList(property, "DamageGroupExplosion", "伤害成分", position, y, true);
            if (useCharge.boolValue)
                y = DrawProperty(property, "ChargeAOERangeScale", "满蓄溅射范围倍率", position, y);
            EditorGUI.indentLevel--;
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
        h += EditorGUI.GetPropertyHeight(explosionProp) + 2;

        // 爆炸伤害为 0 时只显示伤害值本身，其余行(穿甲/范围/成分等)一并隐藏
        if (ev > 0)
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ExplosionAP")) + 2;
            h += LineHeight + 2; // ExplosionInnerRange + ExplosionRange (paired)
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DestructeRadius")) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ShockwaveRadius")) + 2;
            h += GetSinglelineListHeight(property, "DamageGroupExplosion");
            if (useCharge.boolValue)
                h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ChargeAOERangeScale")) + 2;
        }

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
    /// 未开启蓄力 -> 正常单行绘制 prop1
    /// 开启蓄力 -> 并行绘制 prop1（左）和 prop2（右）
    /// </summary>
    private float DrawPropertyOrPaired(SerializedProperty property, SerializedProperty useCharge,
        string prop1Name, string prop2Name, string label1, string label2, Rect position, float y)
    {
        if (useCharge.boolValue)
            return DrawPairedProperties(property, prop1Name, label1, prop2Name, label2, position, y);
        else
            return DrawProperty(property, prop1Name, label1, position, y);
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
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_General(SerializedProperty property)
    {
        return SectionHeaderHeight + 2 * (LineHeight + 2) + Padding;
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
            y = DrawProperty(property, "ShockwaveRadius", "冲击波半径", position, y);
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
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ExplosionAP")) + 2;
            h += GetSinglelineListHeight(property, "DamageGroupExplosion");
        }

        h += Padding;
        return h;
    }

    #endregion
}
