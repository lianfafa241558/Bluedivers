
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

        // 直接在同一行显示label和value字段
        EditorGUI.PropertyField(position, valueProp, label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 返回标准单行高度
        return EditorGUIUtility.singleLineHeight;
    }
}
//不知道为什么不能处理List<KVP>中的
[CustomPropertyDrawer(typeof(ArchSettingData))]
public class ArchSettingDataDrawer : PropertyDrawer
{
    private const float IndentWidth = 15f;
    private const float LineSpacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 直接平铺绘制，不做折叠
        DrawSingleProperty(position, property, label);

        EditorGUI.EndProperty();
    }

    private void DrawSingleProperty(Rect position, SerializedProperty property, GUIContent label)
    {
        // 标题行（不再折叠，直接显示）
        EditorGUI.LabelField(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            label
        );
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
            position.x + IndentWidth,
            position.y,
            position.width - IndentWidth,
            EditorGUIUtility.singleLineHeight
        );

        EditorGUI.PropertyField(currentPosition, titleProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

        // 绘制类型
        EditorGUI.PropertyField(currentPosition, typeProp);
        currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

        // 绘制值（根据类型显示不同控件）
        SettingBtnType type = (SettingBtnType)typeProp.enumValueIndex;
        SerializedProperty rawValueProp = valueProp.FindPropertyRelative("value");
        float curValue = float.TryParse(rawValueProp.stringValue, out float parsed) ? parsed : 0f;

        switch (type)
        {
            case SettingBtnType.Dropdown:
            {
                // 下拉框，选项为 showTexts 内容，value 存选中索引
                string[] options = GetStringArray(showTextsProp);
                if (options.Length == 0) options = new[] { "无选项" };
                int index = Mathf.Clamp((int)curValue, 0, options.Length - 1);
                int newIndex = EditorGUI.Popup(currentPosition, "显示值", index, options);
                if (newIndex != index)
                {
                    rawValueProp.stringValue = newIndex.ToString();
                }
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

                EditorGUI.PropertyField(currentPosition, showTextsProp, new GUIContent("显示文本列表"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                break;
            }
            case SettingBtnType.Slider:
            {
                // 滑动条，范围为 sliderRange
                Vector2Int range = sliderRangeProp.vector2IntValue;
                float newValue = EditorGUI.Slider(currentPosition, new GUIContent("显示值"), curValue, range.x, range.y);
                if (Mathf.Abs(newValue - curValue) > 0.0001f)
                {
                    rawValueProp.stringValue = newValue.ToString("F2");
                }
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;

                EditorGUI.PropertyField(currentPosition, sliderRangeProp, new GUIContent("滑动范围"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                EditorGUI.PropertyField(currentPosition, sliderSuffixProp, new GUIContent("滑动后缀"));
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                break;
            }
            case SettingBtnType.Toggle:
            {
                // bool 复选框，value 为 0/1
                bool curBool = curValue > 0f;
                bool newBool = EditorGUI.Toggle(currentPosition, new GUIContent("显示值"), curBool);
                if (newBool != curBool)
                {
                    rawValueProp.stringValue = newBool ? "1" : "0";
                }
                currentPosition.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                break;
            }
        }
    }

    private string[] GetStringArray(SerializedProperty arrayProp)
    {
        int count = arrayProp.arraySize;
        string[] result = new string[count];
        for (int i = 0; i < count; ++i)
        {
            result[i] = arrayProp.GetArrayElementAtIndex(i).stringValue;
        }
        return result;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight; // 标题行高度

        height += EditorGUIUtility.singleLineHeight * 3; // 基础字段：标题、类型、值
        height += LineSpacing * 3;

        SerializedProperty typeProp = property.FindPropertyRelative("type");
        SettingBtnType type = (SettingBtnType)typeProp.enumValueIndex;

        // 根据类型添加额外高度
        if (type == SettingBtnType.Dropdown)
        {
            var count = property.FindPropertyRelative("showTexts").CountInProperty() + 1;
            height += count * (EditorGUIUtility.singleLineHeight + LineSpacing);
        }
        else if (type == SettingBtnType.Slider)
        {
            height += 2 * (EditorGUIUtility.singleLineHeight + LineSpacing);
        }

        return height;
    }
}


#endif