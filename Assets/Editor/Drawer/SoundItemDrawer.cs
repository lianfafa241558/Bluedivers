using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SoundItem))]
public class SoundItemDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 寮€濮嬬粯鍒讹紝涓嶆樉绀洪粯璁ょ殑label
        EditorGUI.BeginProperty(position, label, property);

        // 璁＄畻鍚勫尯鍩熺殑浣嶇疆
        float spacing = 5f;
        float clipWidth = position.width * 0.25f;
        float subtitleWidth = position.width * 0.75f - spacing;

        Rect clipRect = new Rect(position.x, position.y, clipWidth, position.height);
        Rect subtitleRect = new Rect(position.x + clipWidth + spacing, position.y, subtitleWidth, position.height);

        // 鑾峰彇灞炴€?
        SerializedProperty clipProp = property.FindPropertyRelative("audioClip");
        SerializedProperty subtitleProp = property.FindPropertyRelative("subtitle");

        // 缁樺埗瀛楁
        EditorGUI.ObjectField(clipRect, clipProp, GUIContent.none);
        // 浣跨敤TextField鏇夸唬PropertyField
        string newValue = EditorGUI.TextField(subtitleRect, GUIContent.none, subtitleProp.stringValue);
        if (newValue != subtitleProp.stringValue)
        {
            subtitleProp.stringValue = newValue;
        }

        EditorGUI.EndProperty();
    }
    /*
    // 璁剧疆姣忎釜鍏冪礌鐨勯珮搴?
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }*/
}