using FPSGame.Attribute;
using UnityEditor;
using UnityEngine;



[CustomPropertyDrawer(typeof(MinMaxSliderAttribute))]
public class MinMaxSliderDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        MinMaxSliderAttribute attr = (MinMaxSliderAttribute)attribute;

        if (property.propertyType == SerializedPropertyType.Vector2)
        { 
            // 绘制标签
            position = EditorGUI.PrefixLabel(position, label);

            // 获取当前值
            Vector2 range = property.vector2Value;
            float min = range.x;
            float max = range.y;

            // 约束：min 不能超过 max
            min = Mathf.Clamp(min, attr.minLimit, max);
            max = Mathf.Clamp(max, min, attr.maxLimit);

            // 计算各区域的宽度（左侧输入框 + 滑块 + 右侧输入框）
            float inputWidth = 50f;      // 输入框宽度
            float spacing = 5f;          // 间距

            Rect minInputRect = new Rect(position.x, position.y, inputWidth, position.height);
            Rect sliderRect = new Rect(position.x + inputWidth + spacing, position.y,
                                        position.width - inputWidth * 2 - spacing * 2, position.height);
            Rect maxInputRect = new Rect(position.x + position.width - inputWidth, position.y,
                                          inputWidth, position.height);

            // 绘制左侧最小值输入框
            EditorGUI.BeginChangeCheck();
            float newMin = EditorGUI.FloatField(minInputRect, min);
            if (EditorGUI.EndChangeCheck())
            {
                newMin = Mathf.Clamp(newMin, attr.minLimit, max);
                min = newMin;
            }

            // 绘制中间滑块
            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(sliderRect, ref min, ref max, attr.minLimit, attr.maxLimit);
            if (EditorGUI.EndChangeCheck())
            {
                // 滑块已经自动约束了 min <= max
            }

            // 绘制右侧最大值输入框
            EditorGUI.BeginChangeCheck();
            float newMax = EditorGUI.FloatField(maxInputRect, max);
            if (EditorGUI.EndChangeCheck())
            {
                newMax = Mathf.Clamp(newMax, min, attr.maxLimit);
                max = newMax;
            }

            // 应用修改
            Vector2 newValue = new Vector2(
                Mathf.Round(min * 100f) / 100f,
                Mathf.Round(max * 100f) / 100f
            );
            property.vector2Value = newValue;
        }
        else
        {
            EditorGUI.HelpBox(position, "MinMaxSlider 只能用于 Vector2 字段", MessageType.Error);
        }
    }

}
