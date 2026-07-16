using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SoundItem))]
public class SoundItemDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 开始绘制，不显示默认的label
        EditorGUI.BeginProperty(position, label, property);

        // 计算各区域的位置
        float spacing = 5f;
        float clipWidth = position.width * 0.25f;
        float subtitleWidth = position.width * 0.75f - spacing;

        Rect clipRect = new Rect(position.x, position.y, clipWidth, position.height);
        Rect subtitleRect = new Rect(position.x + clipWidth + spacing, position.y, subtitleWidth, position.height);

        // 获取属性
        SerializedProperty clipProp = property.FindPropertyRelative("audioClip");
        SerializedProperty subtitleProp = property.FindPropertyRelative("subtitle");

        // 绘制字段
        EditorGUI.ObjectField(clipRect, clipProp, GUIContent.none);
        // 使用TextField替代PropertyField
        string newValue = EditorGUI.TextField(subtitleRect, GUIContent.none, subtitleProp.stringValue);
        if (newValue != subtitleProp.stringValue)
        {
            subtitleProp.stringValue = newValue;
        }

        EditorGUI.EndProperty();
    }
    /*
    // 设置每个元素的高度
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }*/
}