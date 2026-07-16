using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpritePreviewAttribute))]
public class SpritePreviewDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var attr = attribute as SpritePreviewAttribute;
        return EditorGUIUtility.singleLineHeight * Mathf.Max(1, attr.height);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = attribute as SpritePreviewAttribute;
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float width = lineHeight * Mathf.Max(1, attr.width);
        float height = lineHeight * Mathf.Max(1, attr.height);
        // 左侧显示 label
        Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, lineHeight);
        EditorGUI.LabelField(labelRect, label);

        // 右侧显示 ObjectField
        float fieldX = position.x + EditorGUIUtility.labelWidth;
        float fieldY = position.y + (position.height - height) * 0.5f;
        Rect fieldRect = new Rect(fieldX, fieldY, width, height);

        EditorGUI.ObjectField(fieldRect, property, typeof(Sprite), GUIContent.none);
    }
}



[CustomPropertyDrawer(typeof(InlineAttribute))]
public class InlineDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 如果是数组元素（有父级且是数组），返回单行高度
        if (property.depth > 0 && property.serializedObject != null)
        {
            // 检查父级是否是数组
            var parent = property.GetParent();
            if (parent != null && parent.isArray)
            {
                return EditorGUIUtility.singleLineHeight;
            }
        }

        // 如果是顶级对象，也返回单行高度（由 EditorOverride 控制）
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 获取所有子字段
        var childProps = GetChildProperties(property);

        if (childProps.Count == 0)
        {
            // 没有子字段，正常显示
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // 如果是数组元素，使用内联布局
        bool isArrayElement = property.depth > 0 && IsParentArray(property);

        if (isArrayElement)
        {
            // 数组元素：不显示标签，直接显示所有子字段在一行
            DrawInlineChildren(position, childProps, label);
        }
        else
        {
            // 普通对象：显示标签 + 所有子字段在一行
            DrawInlineObject(position, property, childProps, label);
        }
    }

    private void DrawInlineChildren(Rect position, List<SerializedProperty> childProps, GUIContent label)
    {
        float spacing = 2f;
        float totalSpacing = spacing * (childProps.Count - 1);
        float fieldWidth = (position.width - totalSpacing) / childProps.Count;

        Rect currentRect = new Rect(position.x, position.y, fieldWidth, position.height);

        float originalLabelWidth = EditorGUIUtility.labelWidth;

        for (int i = 0; i < childProps.Count; i++)
        {
            var child = childProps[i];
            if (child.name == "m_Script") continue;

            string shortLabel = GetShortLabel(child.displayName);
            GUIContent shortContent = new GUIContent(shortLabel);
            float shortLabelWidth = EditorStyles.label.CalcSize(shortContent).x + 4;

            EditorGUIUtility.labelWidth = shortLabelWidth;
            EditorGUI.PropertyField(currentRect, child, new GUIContent(shortLabel));

            currentRect.x += fieldWidth + spacing;
        }

        EditorGUIUtility.labelWidth = originalLabelWidth;
    }

    private void DrawInlineObject(Rect position, SerializedProperty property, List<SerializedProperty> childProps, GUIContent label)
    {
        // 计算标签宽度
        float labelWidth = EditorGUIUtility.labelWidth;
        float fieldStartX = position.x + labelWidth;
        float availableWidth = position.width - labelWidth - 10;

        // 显示标签
        Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
        EditorGUI.LabelField(labelRect, label);

        // 绘制子字段
        float spacing = 2f;
        float totalSpacing = spacing * (childProps.Count - 1);
        float fieldWidth = (availableWidth - totalSpacing) / childProps.Count;
        fieldWidth = Mathf.Clamp(fieldWidth, 40f, 150f);

        Rect currentRect = new Rect(fieldStartX, position.y, fieldWidth, position.height);

        float originalLabelWidth = EditorGUIUtility.labelWidth;

        for (int i = 0; i < childProps.Count; i++)
        {
            var child = childProps[i];
            if (child.name == "m_Script") continue;

            string shortLabel = GetShortLabel(child.displayName);
            GUIContent shortContent = new GUIContent(shortLabel);
            float shortLabelWidth = EditorStyles.label.CalcSize(shortContent).x + 4;

            EditorGUIUtility.labelWidth = shortLabelWidth;
            EditorGUI.PropertyField(currentRect, child, new GUIContent(shortLabel));

            currentRect.x += fieldWidth + spacing;
        }

        EditorGUIUtility.labelWidth = originalLabelWidth;
    }

    private List<SerializedProperty> GetChildProperties(SerializedProperty prop)
    {
        var children = new List<SerializedProperty>();
        var iterator = prop.Copy();
        var endProperty = prop.GetEndProperty();

        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(iterator, endProperty))
                break;

            if (iterator.name == "m_Script")
                continue;

            children.Add(iterator.Copy());
            enterChildren = false;
        }

        return children;
    }

    private bool IsParentArray(SerializedProperty prop)
    {
        var parent = prop.GetParent();
        return parent != null && parent.isArray;
    }

    private string GetShortLabel(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return "";

        var abbreviations = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "health", "HP" },
            { "mana", "MP" },
            { "speed", "Spd" },
            { "damage", "Dmg" },
            { "defense", "Def" },
            { "attack", "Atk" },
            { "strength", "Str" },
            { "agility", "Agi" },
            { "intelligence", "Int" },
            { "wisdom", "Wis" },
            { "vitality", "Vit" },
            { "luck", "Luk" },
            { "x", "X" },
            { "y", "Y" },
            { "z", "Z" },
            { "width", "W" },
            { "height", "H" },
            { "name", "N" },
            { "value", "Val" },
            { "weight", "Wgt" },
            { "position", "Pos" },
            { "rotation", "Rot" },
            { "scale", "Scl" },
        };

        if (abbreviations.TryGetValue(fullName, out string abbr))
            return abbr;

        if (fullName.Length >= 2)
            return fullName.Substring(0, 2);

        return fullName.Substring(0, 1).ToUpper();
    }
}

// ===== 扩展方法：获取父级 SerializedProperty =====
public static class SerializedPropertyExtensions
{
    public static SerializedProperty GetParent(this SerializedProperty prop)
    {
        var path = prop.propertyPath;
        int lastDot = path.LastIndexOf('.');
        if (lastDot == -1)
            return null;

        var parentPath = path.Substring(0, lastDot);
        return prop.serializedObject.FindProperty(parentPath);
    }
}