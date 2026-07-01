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
        // 宸︿晶鏄剧ず label
        Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, lineHeight);
        EditorGUI.LabelField(labelRect, label);

        // 鍙充晶鏄剧ず ObjectField
        float fieldX = position.x + EditorGUIUtility.labelWidth;
        float fieldY = position.y + (position.height - height) * 0.5f;
        Rect fieldRect = new Rect(fieldX, fieldY, width, height);

        EditorGUI.ObjectField(fieldRect, property, typeof(Sprite), GUIContent.none);
    }
}


