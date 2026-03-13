
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using static ArchivesData_SO;
#endif


#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(ArchivesFloat))]
public class ArchivesFloatDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 获取value属性
        SerializedProperty valueProp = property.FindPropertyRelative("value");

        // 直接在同一个行显示label和value字段
        EditorGUI.PropertyField(position, valueProp, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 返回标准单行高度
        return EditorGUIUtility.singleLineHeight;
    }
}
//不知道为什么不能处理list<KVP>中的
[CustomPropertyDrawer(typeof(ArchSettingData))]
public class ArchSettingDataDrawer : PropertyDrawer
{
    private const float FoldoutIndent = 15f;
    private const float LineSpacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 处理数组元素的情况（当PropertyDrawer用于List中的元素时）
        if (property.propertyType == SerializedPropertyType.Generic && property.isArray)
        {
            // 如果是数组元素，则直接绘制该元素
            DrawSingleProperty(position, property, label);
        }
        else
        {
            // 普通字段的绘制逻辑
            DrawSingleProperty(position, property, label);
        }

        EditorGUI.EndProperty();
    }

    private void DrawSingleProperty(Rect position, SerializedProperty property, GUIContent label)
    {
        // 设置折叠状态
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            label,
            true
        );

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            position.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 获取所有序列化属性
            SerializedProperty titleProp = property.FindPropertyRelative("titile");
            SerializedProperty typeProp = property.FindPropertyRelative("type");
            SerializedProperty valueProp = property.FindPropertyRelative("value");
            SerializedProperty showTextsProp = property.FindPropertyRelative("showTexts");
            SerializedProperty sliderRangeProp = property.FindPropertyRelative("sliderRange");
            SerializedProperty sliderSuffixProp = property.FindPropertyRelative("sliderSuffix");

            // 绘制标题（带缩进）
            Rect currentPosition = new Rect(
                position.x + FoldoutIndent,
                position.y,
                position.width - FoldoutIndent,
                EditorGUIUtility.singleLineHeight
            );

            EditorGUI.PropertyField(currentPosition, titleProp);
            currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 绘制类型
            EditorGUI.PropertyField(currentPosition, typeProp);
            currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 绘制值
            EditorGUI.PropertyField(currentPosition, valueProp);
            currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

            // 根据类型显示不同的字段
            SettingBtnType type = (SettingBtnType)typeProp.enumValueIndex;

            if (type == SettingBtnType.Dropdown)
            {
                EditorGUI.PropertyField(currentPosition, showTextsProp, new GUIContent("显示文本列表"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
            }
            else if (type == SettingBtnType.Slider)
            {
                EditorGUI.PropertyField(currentPosition, sliderRangeProp, new GUIContent("滑动范围"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                EditorGUI.PropertyField(currentPosition, sliderSuffixProp, new GUIContent("滑动后缀"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
            }

            EditorGUI.indentLevel--;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight; // 折叠行高度

        if (property.isExpanded)
        {
            height += EditorGUIUtility.singleLineHeight * 3; // 基础字段：标题、类型、值
            height += LineSpacing * 3;

            SerializedProperty typeProp = property.FindPropertyRelative("type");
            SettingBtnType type = (SettingBtnType)typeProp.enumValueIndex;
            
            // 根据类型添加额外高度
            if (type == SettingBtnType.Dropdown)
            {
                var count = property.FindPropertyRelative("showTexts").CountInProperty()+1;
                height += count*( (EditorGUIUtility.singleLineHeight) + LineSpacing);

            }
            else if (type == SettingBtnType.Slider)
            {
                height +=2*(EditorGUIUtility.singleLineHeight + LineSpacing);
            }
        }

        return height;
    }
}


#endif