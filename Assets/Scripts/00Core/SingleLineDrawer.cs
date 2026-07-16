using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

/// <summary>
/// 抽象基类，继承它即可将多个字段压缩到一行显示。
/// 子类只需实现 Fields 属性，返回要显示的字段配置即可。
/// </summary>
#if UNITY_EDITOR
public abstract class SingleLineDrawer : PropertyDrawer
{
    /// <summary>
    /// 子类在此返回字段名 -> 可选标签的映射。
    /// 若标签为 null 或空，则不显示标签。
    /// </summary>
    protected abstract Dictionary<string, string> Fields { get; }

    private (string field, string label)[] _cachedFields;
    private bool _cached;

    private (string field, string label)[] GetFields()
    {
        if (!_cached)
        {
            var list = new List<(string, string)>();
            foreach (var kv in Fields)
            {
                list.Add((kv.Key, kv.Value));
            }
            _cachedFields = list.ToArray();
            _cached = true;
        }
        return _cachedFields;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 如果有默认 label（来自数组元素或字段名），在左侧绘制它
        float labelWidth = 0;
        if (label != null && !string.IsNullOrEmpty(label.text))
        {
            // 使用 EditorGUIUtility 的标准 label 宽度
            labelWidth = EditorGUIUtility.labelWidth;
            Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
            EditorGUI.PrefixLabel(labelRect, label);
        }

        var fields = GetFields();
        if (fields.Length == 0)
        {
            EditorGUI.EndProperty();
            return;
        }

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float spacing = 4f;
        float x = position.x + labelWidth;
        float availableWidth = position.width - labelWidth;

        // 计算总标签宽度
        float totalLabelWidth = 0;
        int labelCount = 0;
        foreach (var (field, lbl) in fields)
        {
            if (!string.IsNullOrEmpty(lbl))
            {
                totalLabelWidth += 24f;
                labelCount++;
            }
        }

        // 剩余宽度均分给每个字段
        float fieldWidth = (availableWidth - totalLabelWidth - spacing * (fields.Length - 1)) / fields.Length;
        if (fieldWidth < 10) fieldWidth = 10;

        foreach (var (field, lbl) in fields)
        {
            SerializedProperty prop = property.FindPropertyRelative(field);
            if (prop == null) continue;

            // 标签
            if (!string.IsNullOrEmpty(lbl))
            {
                Rect lblRect = new Rect(x, position.y, 24f, position.height);
                EditorGUI.LabelField(lblRect, lbl);
                x += 24f;
            }

            // 字段
            Rect fieldRect = new Rect(x, position.y, fieldWidth, position.height);
            EditorGUI.PropertyField(fieldRect, prop, GUIContent.none);
            x += fieldWidth + spacing;
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
#endif
