using UnityEditor;
using UnityEngine;
using Unity.FPS.Game;

[CustomPropertyDrawer(typeof(DamageData))]
public class DamageDataDrawer : PropertyDrawer
{
    private const float Padding = 4f;
    private const float HeaderHeight = 20f;
    private const float LineHeight = 18f;
    private const float Gap = 8f;
    private const float PairLabelWidth = 140f;

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
        y = DrawProperty(property, "UseCharge", "使用蓄力", position, y);
        y = DrawPropertyOrPaired(property, useCharge, "Speed", "ChargeSpeedScale", "投掷物的速度", "蓄力倍率", position, y);
        y = DrawPropertyOrPaired(property, useCharge, "Gravity", "ChargeGravityScale", "下坠速度", "蓄力倍率", position, y);
        y = DrawPropertyOrPaired(property, useCharge, "SoundRadius", "ChargeSoundScale", "发出的声音影响范围", "蓄力倍率", position, y);
        y = DrawPairedProperties(property, "MinRange", "安全引信(单位:M)", "MaxRange", "自爆引信(单位:M)", position, y);
        y = DrawProperty(property, "MaxLifeTime", "生命周期", position, y);
        y = DrawProperty(property, "InheritWeaponSpeed", "继承武器初速度", position, y);
        y = DrawProperty(property, "NoSource", "无源伤害", position, y);
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_Motion(SerializedProperty property)
    {
        return SectionHeaderHeight + 8 * (LineHeight + 2) + Padding;
    }

    #endregion

    #region 直接伤害

    private float DrawSection_DirectDamage(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "直接伤害", ref y);
        var useCharge = property.FindPropertyRelative("UseCharge");

        EditorGUI.indentLevel++;
        y = DrawPropertyOrPaired(property, useCharge, "DamageDirect", "ChargeDamageScale", "直接伤害值", "满蓄伤害倍率", position, y);
        y = DrawProperty(property, "DamageGroupDirect", "伤害成分", position, y);
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_DirectDamage(SerializedProperty property)
    {
        float h = SectionHeaderHeight;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DamageDirect")) + 2;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DamageGroupDirect")) + 2;
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
            y = DrawPairedProperties(property, "ExplosionInnerRange", "伤害内半径", "ExplosionRange", "伤害外半径", position, y);
            y = DrawProperty(property, "DestructeRadius", "地形破坏半径", position, y);
            y = DrawProperty(property, "ShockwaveRadius", "冲击波半径", position, y);
            if (useCharge.boolValue)
                y = DrawProperty(property, "ChargeAOERangeScale", "满蓄溅射范围倍率", position, y);
            y = DrawProperty(property, "DamageGroupExplosion", "伤害成分", position, y);
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

        if (ev > 0)
        {
            h += LineHeight + 2; // ExplosionInnerRange + ExplosionRange (paired)
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DestructeRadius")) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ShockwaveRadius")) + 2;
            if (useCharge.boolValue)
                h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ChargeAOERangeScale")) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DamageGroupExplosion")) + 2;
        }

        h += Padding;
        return h;
    }

    #endregion

    #region 碰撞

    private float DrawSection_Collision(Rect position, SerializedProperty property, float y)
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

    private float GetSectionHeight_Collision(SerializedProperty property)
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

    private float SectionHeaderHeight => EditorGUIUtility.singleLineHeight + 2;

    private void DrawSectionHeader(Rect position, string title, ref float y)
    {
        var rect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);
        y += EditorGUIUtility.singleLineHeight + 2;
    }

    /// <summary>并行绘制两个属性，左右各占一半，标签固定宽度 140</summary>
    private float DrawPairedProperties(SerializedProperty property, string prop1Name, string label1,
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

    private float DrawProperty(SerializedProperty property, string propName, string label, Rect position, float y)
    {
        var prop = property.FindPropertyRelative(propName);
        if (prop == null) return y;

        var rect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(prop));
        EditorGUI.PropertyField(rect, prop, new GUIContent(label), true);
        return y + rect.height + 2;
    }

    #endregion
}
