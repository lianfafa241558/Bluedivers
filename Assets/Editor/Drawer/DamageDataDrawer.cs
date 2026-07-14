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
        y = DrawProperty(property, "ChargeHeatScale", "满蓄热量倍率", position, y);
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
        return SectionHeaderHeight + 10 * (LineHeight + 2) + Padding;
    }

    #endregion

    #region 直接伤害

    private float DrawSection_DirectDamage(Rect position, SerializedProperty property, float y)
    {
        DrawSectionHeader(position, "直接伤害", ref y);
        var useCharge = property.FindPropertyRelative("UseCharge");

        EditorGUI.indentLevel++;
        y = DrawPropertyOrPaired(property, useCharge, "DamageDirect", "ChargeDamageScale", "直接伤害值", "满蓄伤害倍率", position, y);
        y = DrawSinglelineList(property, "DamageGroupDirect", "伤害成分", position, y);
        EditorGUI.indentLevel--;
        y += Padding;
        return y;
    }

    private float GetSectionHeight_DirectDamage(SerializedProperty property)
    {
        float h = SectionHeaderHeight;
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DamageDirect")) + 2;
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
        h += GetSinglelineListHeight(property, "DamageGroupExplosion");

        if (ev > 0)
        {
            h += LineHeight + 2; // ExplosionInnerRange + ExplosionRange (paired)
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DestructeRadius")) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ShockwaveRadius")) + 2;
            if (useCharge.boolValue)
                h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ChargeAOERangeScale")) + 2;
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
    /// 单行列表绘制(用于 List&lt;SKVP&lt;,&gt;&gt; 这类带 [Singleline] 元素的列表)
    /// 尽量贴近 Unity 原版 list: 头部=折叠+标签+Size 输入框; 底部=+/- 按钮; 元素单行内联
    /// </summary>
    protected float DrawSinglelineList(SerializedProperty property, string propName, string label, Rect position, float y, bool bold = false)
    {
        var listProp = property.FindPropertyRelative(propName);
        if (listProp == null) return y;

        float lineH = EditorGUIUtility.singleLineHeight;
        // 原版 list 头部右侧 Size 区域: "Size" 文本 + 整数输入框
        const float sizeLabelW = 40f;
        const float sizeFieldW = 40f;
        const float sizeGap = 4f;

        // ===== 头部: 折叠箭头 + 标签(左) | Size 标签 + 数量输入框(右) =====
        var headerRect = new Rect(position.x- sizeLabelW, y, position.width, lineH);

        // 右侧 Size 区域
        var sizeRect = new Rect(headerRect.x + headerRect.width , headerRect.y, sizeFieldW, lineH);

        // 折叠控件: 占满 头部 除掉 Size 区域 的部分(会自动按 indentLevel 缩进)
        var foldRect = new Rect(headerRect.x, headerRect.y, headerRect.width - sizeFieldW - sizeLabelW - sizeGap, lineH);

        GUIStyle foldStyle = bold
            ? new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold }
            : EditorStyles.foldout;

        // 用 PropertyField 的标准缩进方式绘制 foldout,保证与其他字段对齐
        int oldIndent = EditorGUI.indentLevel;
        // foldout 需要在缩进区域内,把 foldRect 限定在缩进后的可用宽度
        float indent = EditorGUI.IndentedRect(foldRect).x - foldRect.x;
        var foldDrawRect = new Rect(foldRect.x + indent, foldRect.y, foldRect.width - indent, lineH);

        bool expanded = EditorGUI.Foldout(foldDrawRect, listProp.isExpanded, new GUIContent(label), true, foldStyle);
        if (expanded != listProp.isExpanded) listProp.isExpanded = expanded;

        // Size 标签 + 输入框
        EditorGUI.indentLevel = 0;

        int newSize = EditorGUI.IntField(sizeRect, listProp.arraySize);
        if (newSize != listProp.arraySize && newSize >= 0) listProp.arraySize = newSize;
        EditorGUI.indentLevel = oldIndent;

        // 头部右键: 清空
        if (Event.current.type == EventType.ContextClick && headerRect.Contains(Event.current.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("清空数组"), false, () =>
            {
                listProp.ClearArray();
                listProp.serializedObject.ApplyModifiedProperties();
            });
            menu.ShowAsContext();
            Event.current.Use();
        }

        y += lineH + 2;

        if (!listProp.isExpanded) return y;

        // ===== 元素 =====
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            y = DrawSinglelineElement(position, element, i, listProp, y);
        }

        // ===== 底部 +/- 按钮(贴近原版 list 样式) =====
        y = DrawListFooter(position, listProp, y);

        y += Padding;
        return y;
    }

    /// <summary>底部 + / - 按钮,模仿 Unity 原版 list</summary>
    float DrawListFooter(Rect position, SerializedProperty listProp, float y)
    {
        float lineH = EditorGUIUtility.singleLineHeight;
        // 原版: 右下角 - 和 + 两个小方块,各 20x18
        const float btnW = 24f;
        const float btnGap = 2f;

        var footerRect = new Rect(position.x, y, position.width, lineH);

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;


        // "-" 在右, "+" 在 "-" 右侧(原版顺序)
        var minusRect = new Rect(footerRect.x + footerRect.width - btnW , footerRect.y, btnW, lineH);
        var plusRect = new Rect(footerRect.x + footerRect.width - btnW * 2 - btnGap, footerRect.y, btnW, lineH);

        if (GUI.Button(minusRect, "-", EditorStyles.miniButtonLeft))
        {
            if (listProp.arraySize > 0)
            {
                listProp.DeleteArrayElementAtIndex(listProp.arraySize - 1);
                listProp.serializedObject.ApplyModifiedProperties();
            }
        }
        if (GUI.Button(plusRect, "+", EditorStyles.miniButtonRight))
        {
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.indentLevel = oldIndent;
        return y + lineH + 2;
    }

    /// <summary>单行元素: "元素 i" + 各子字段(如 Key/Value)等宽平分</summary>
    float DrawSinglelineElement(Rect position, SerializedProperty element, int index, SerializedProperty listProp, float y)
    {
        float lineH = EditorGUIUtility.singleLineHeight;
        var rowRect = new Rect(position.x, y, position.width, lineH);

        var children = GetVisibleChildren(element);

        // 元素标签
        var elemLabel = new GUIContent($"元素 {index}");
        float labelW = EditorStyles.label.CalcSize(elemLabel).x + 64 ;
        EditorGUI.LabelField(new Rect(rowRect.x, rowRect.y, labelW, lineH), elemLabel);

        float x = rowRect.x + labelW;
        float w = rowRect.width - labelW;
        int visibleCount = 0;
        foreach (var c in children) if (c.name != "m_Script") visibleCount++;
        float fieldW = visibleCount > 0 ? w / visibleCount : w;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float origLabelW = EditorGUIUtility.labelWidth;

        foreach (var child in children)
        {
            if (child.name == "m_Script") continue;
            var cl = new GUIContent(child.displayName);
            float clw = EditorStyles.label.CalcSize(cl).x + 4;
            EditorGUIUtility.labelWidth = clw;
            var childRect = new Rect(x, rowRect.y, fieldW, lineH);
            EditorGUI.PropertyField(childRect, child, cl, false);
            x += fieldW;
        }

        EditorGUIUtility.labelWidth = origLabelW;
        EditorGUI.indentLevel = oldIndent;

        // 元素右键: 复制/删除
        if (Event.current.type == EventType.ContextClick && rowRect.Contains(Event.current.mousePosition))
        {
            int idx = index;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("复制"), false, () =>
            {
                listProp.InsertArrayElementAtIndex(idx);
                listProp.serializedObject.ApplyModifiedProperties();
            });
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                listProp.DeleteArrayElementAtIndex(idx);
                listProp.serializedObject.ApplyModifiedProperties();
            });
            menu.ShowAsContext();
            Event.current.Use();
        }

        return y + lineH + 2;
    }

    protected float GetSinglelineListHeight(SerializedProperty property, string propName)
    {
        var listProp = property.FindPropertyRelative(propName);
        if (listProp == null) return 0;
        float lineH = EditorGUIUtility.singleLineHeight;
        float h = lineH + 2; // 头部
        if (listProp.isExpanded)
        {
            h += listProp.arraySize * (lineH + 2); // 元素
            h += lineH + 2; // 底部 +/- 按钮
        }
        h += Padding;
        return h;
    }

    static List<SerializedProperty> GetVisibleChildren(SerializedProperty prop)
    {
        var list = new List<SerializedProperty>();
        var iter = prop.Copy();
        var end = prop.GetEndProperty();
        bool enter = true;
        while (iter.NextVisible(enter))
        {
            if (SerializedProperty.EqualContents(iter, end)) break;
            if (iter.name != "m_Script") list.Add(iter.Copy());
            enter = false;
        }
        return list;
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

        if (ev > 0)
        {
            h += LineHeight + 2; // ExplosionInnerRange + ExplosionRange (paired)
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("DestructeRadius")) + 2;
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("ShockwaveRadius")) + 2;
            h += GetSinglelineListHeight(property, "DamageGroupExplosion");
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
        h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("OnlyTerrain")) + 2;
        h += EditorGUI.GetPropertyHeight(useHole) + 2;

        if (useHole.boolValue)
        {
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("Hole")) + 2;
        }
        h += Padding;
        return h;
    }

    #endregion
}
