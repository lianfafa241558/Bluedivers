using System;
using System.Linq;
using Core;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RoleData_SO.speechGroups 的自定义检视器。
/// 参考 WeaponCfg.cs 的 WeaponCfgDrawer：按 SpeechTypeEnum 枚举逐项平铺，
/// 始终显示全部枚举项：已配置的显示 SoundGroup 对象引用（可删除），未配置的显示"添加"按钮。
/// 由于 DisplayDic 内部用 arr(List&lt;KVP&gt;) 序列化、dic 缓存，增删后需置 meetReset=true 触发重建。
/// </summary>
[CustomPropertyDrawer(typeof(DisplayDic<SpeechTypeEnum, SoundGroup_SO>))]
public class RoleSpeechGroupDrawer : PropertyDrawer
{
    private static readonly SpeechTypeEnum[] AllValues =
        (SpeechTypeEnum[])Enum.GetValues(typeof(SpeechTypeEnum));

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return lineHeight * (AllValues.Length + 1);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty arrProp = property.FindPropertyRelative("arr");
        SerializedProperty meetResetProp = property.FindPropertyRelative("meetReset");
        if (arrProp == null)
        {
            EditorGUI.LabelField(position, "找不到 arr 字段");
            EditorGUI.EndProperty();
            return;
        }

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float x = position.x;
        float w = position.width;
        float y = position.y;

        // ===== 标题行 =====
        Rect labelRect = new Rect(x, y, w - 120, lineHeight);
        Rect countRect = new Rect(x + 100, y, 100, lineHeight);
        Rect clearRect = new Rect(x + w - 95, y, 80, lineHeight);

        EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
        EditorGUI.LabelField(countRect, "长度: " + arrProp.arraySize);

        if (GUI.Button(clearRect, "清空"))
        {
            arrProp.ClearArray();
            if (meetResetProp != null) meetResetProp.boolValue = true;
            arrProp.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(arrProp.serializedObject.targetObject);
        }

        y += lineHeight + spacing;

        // ===== 逐项绘制 =====
        for (int vi = 0; vi < AllValues.Length; vi++)
        {
            SpeechTypeEnum type = AllValues[vi];

            bool found = false;
            for (int i = 0; i < arrProp.arraySize; i++)
            {
                SerializedProperty kvpProp = arrProp.GetArrayElementAtIndex(i);
                SerializedProperty keyProp = kvpProp.FindPropertyRelative("Key");
                if (keyProp != null && keyProp.enumValueIndex == vi)
                {
                    SerializedProperty valueProp = kvpProp.FindPropertyRelative("Value");
                    GUIContent typeLabel = new GUIContent(GetEnumString(type));

                    // 标签 + 对象引用 + 删除按钮
                    float labelW = GUI.skin.label.CalcSize(typeLabel).x + 6f;
                    float delBtnW = 50f;
                    float objW = w - labelW - delBtnW - 12f;
                    float curX = x;

                    Rect typeLabelRect = new Rect(curX, y, labelW, lineHeight);
                    EditorGUI.LabelField(typeLabelRect, typeLabel);
                    curX += labelW + 4f;

                    if (valueProp != null)
                    {
                        Rect objRect = new Rect(curX, y, objW, lineHeight);
                        EditorGUI.ObjectField(objRect, valueProp, GUIContent.none);
                        curX += objW + 4f;
                    }

                    Rect delRect = new Rect(curX, y, delBtnW, lineHeight);
                    if (GUI.Button(delRect, "删除"))
                    {
                        arrProp.DeleteArrayElementAtIndex(i);
                        if (meetResetProp != null) meetResetProp.boolValue = true;
                        arrProp.serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(arrProp.serializedObject.targetObject);
                        EditorGUI.EndProperty();
                        return;
                    }

                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // 未配置：标签（禁用）+ 添加按钮
                GUIContent typeLabel = new GUIContent(GetEnumString(type));
                float labelW = GUI.skin.label.CalcSize(typeLabel).x + 6f;
                float addBtnW = w - labelW - 12f;
                float curX = x;

                Rect typeLabelRect = new Rect(curX, y, labelW, lineHeight);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.LabelField(typeLabelRect, typeLabel);
                EditorGUI.EndDisabledGroup();
                curX += labelW + 4f;

                Rect addRect = new Rect(curX, y, addBtnW, lineHeight);
                if (GUI.Button(addRect, "添加"))
                {
                    // 记录待添加的枚举序号与目标，供选择窗回调时写入
                    int targetEnumIndex = vi;
                    var serializedObj = property.serializedObject;
                    string propertyPath = property.propertyPath;
                    UnityEngine.Object targetObj = serializedObj.targetObject;
                    string assetPath = AssetDatabase.GetAssetPath(targetObj);

                    // 仅列出该资产下挂载的 SoundGroup 子资源
                    var groups = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                        .OfType<SoundGroup_SO>().ToList();

                    // 用泛用选择弹窗（确认模式）打开
                    PopupWindow.Show(addRect, new SOPickerPopup<SoundGroup_SO>(
                        groups,
                        selectedGroup =>
                        {
                            if (selectedGroup == null) return;

                            var so = new SerializedObject(targetObj);
                            var speechProp = so.FindProperty(propertyPath);
                            if (speechProp == null) return;

                            SerializedProperty arrP = speechProp.FindPropertyRelative("arr");
                            SerializedProperty resetP = speechProp.FindPropertyRelative("meetReset");

                            int index = arrP.arraySize;
                            arrP.arraySize++;
                            SerializedProperty newKvp = arrP.GetArrayElementAtIndex(index);
                            SerializedProperty newKey = newKvp.FindPropertyRelative("Key");
                            if (newKey != null) newKey.enumValueIndex = targetEnumIndex;
                            SerializedProperty newValue = newKvp.FindPropertyRelative("Value");
                            if (newValue != null) newValue.objectReferenceValue = selectedGroup;
                            if (resetP != null) resetP.boolValue = true;
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(targetObj);
                        },
                        g => GetGroupIcon(),                                  // 图标
                        g => string.IsNullOrEmpty(g.groupName) ? g.name : g.groupName, // 名称
                        getType: null,                                        // 无类型行
                        confirmMode: true                                     // 需"确定/取消"确认
                    ));
                }
            }

            y += lineHeight + spacing;
        }

        EditorGUI.EndProperty();
    }

    private static Sprite _groupIcon;

    /// <summary>从 SoundGroup 类型的迷你缩略图构造一个图标 Sprite（缓存复用）</summary>
    private static Sprite GetGroupIcon()
    {
        if (_groupIcon == null)
        {
            Texture2D tex = AssetPreview.GetMiniTypeThumbnail(typeof(SoundGroup_SO));
            if (tex != null)
                _groupIcon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return _groupIcon;
    }

    /// <summary>取枚举的 InspectorName 中文标签，未标注则用枚举名</summary>
    private string GetEnumString(Enum value)
    {
        var fieldInfo = value.GetType().GetField(value.ToString());
        if (fieldInfo == null) return value.ToString();
        var attribute = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
        return attribute.Length > 0 ? ((InspectorNameAttribute)attribute[0]).displayName : value.ToString();
    }
}
