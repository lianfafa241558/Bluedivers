using UnityEditor;
using UnityEngine;
using Core;
using FPSGame.Attribute;


#if UNITY_EDITOR
using System.Reflection;
#endif
using System;

#if UNITY_EDITOR
/// <summary>
/// 定义对带有`CustomLabelAttribute`特性的字段的面板内容的绘制行为。
/// </summary>
[CustomPropertyDrawer(typeof(CompareAttribute))]
public class CustomLabelDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CompareAttribute customLabel = (CompareAttribute)attribute;
        if (!ShouldDisplayField(property, customLabel)) return;

        try
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
        catch (InvalidOperationException)
        {
        }

    }

    public static bool HasFlagsAttribute(SerializedProperty property)
    {

        // 获取目标对象的实际类型
        Type hostType = property.serializedObject.targetObject.GetType();
        // 通过反射获取字段或属性信息
        FieldInfo fieldInfo = hostType.GetField(property.propertyPath, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (fieldInfo != null)
        {
            // 检查是否应用了FlagsAttribute且不检查继承链
            return Attribute.IsDefined(fieldInfo.FieldType, typeof(FlagsAttribute), false);
        }

        PropertyInfo propInfo = hostType.GetProperty(property.propertyPath,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (propInfo != null)
        {
            return Attribute.IsDefined(propInfo.PropertyType, typeof(FlagsAttribute), false);
        }
        return false;
    }

    public static bool ShouldDisplayField(SerializedProperty property, CompareAttribute attr)
    {
        if (string.IsNullOrEmpty(attr.contField)) return true;

        // 获取当前属性的父级对象
        var parentPath = property.propertyPath;

        // [Compare] 加在数组/List 字段上时，Unity 会把它应用到每个数组元素上
        // （路径形如 SomeField.Array.data[i]），这里去掉数组索引段，
        // 让控制字段按"数组字段所在层级"定位（SomeField.Array → SomeField）
        var arrayIdx = parentPath.IndexOf(".Array");
        if (arrayIdx > 0)
        {
            parentPath = parentPath.Substring(0, arrayIdx);
        }

        var lastDot = parentPath.LastIndexOf('.');
        parentPath = lastDot > 0 ? parentPath.Substring(0, lastDot) : "";
        // 构建完整控制属性路径        //若父级路径为空（顶层属性），直接使用contField（如test2）；
        //否则拼接父路径（如MissionCfg.test2）
        var fullPath = string.IsNullOrEmpty(parentPath)
            ? attr.contField
            : $"{parentPath}.{attr.contField}";

        var controlProp = property.serializedObject.FindProperty(fullPath);
        if (controlProp == null)
        {
            Debug.LogError($"找不到控制属性 {fullPath}");
            return false;
        }

        switch (controlProp.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return Calculate(attr.operate, controlProp.boolValue ? 1 : 0, attr.enumValue);
            case SerializedPropertyType.Integer:
                return Calculate(attr.operate, controlProp.intValue, attr.enumValue);
            case SerializedPropertyType.Float:
                return Calculate(attr.operate, controlProp.floatValue, attr.enumValue);
            case SerializedPropertyType.Enum:
                int value = controlProp.intValue;
                return Calculate(attr.operate, value, attr.enumValue);
            default:
                // 其他类型根据是否不为 null 进行判定
                return Calculate(attr.operate, controlProp.objectReferenceValue != null ? 1 : 0, attr.enumValue);
        }
    }
    public static bool Calculate(CompareOperate operate, float source, float target)
    {
        return operate switch {
            CompareOperate.Equal => Mathf.Approximately(source, target),//使用近似比较
            CompareOperate.NotEqual => !Mathf.Approximately(source, target),
            CompareOperate.Less => source < target,
            CompareOperate.LessEqual => source <= target,
            CompareOperate.Greater => source > target,
            CompareOperate.GreaterEqual => source >= target,
            CompareOperate.Contain => (int)source != 0 && ((int)source & (int)target) == (int)target,
            CompareOperate.NotContain => (int)source != 0 && ((int)source & (int)target) == 0,
            _ => throw new System.ArgumentException("找不到操作符" + operate),
        };

    }


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        CompareAttribute customLabel = (CompareAttribute)attribute;
        if (!ShouldDisplayField(property, customLabel)) return 0;

        float baseHeight = base.GetPropertyHeight(property, label);
        if (property.isExpanded)
        {
            if (property.propertyType == SerializedPropertyType.Generic)
            {
                return baseHeight + EditorGUIUtility.singleLineHeight * property.CountInProperty();
            }
        }
        return baseHeight;
        
    }






}




[CustomPropertyDrawer(typeof(DisplayField))]
public class DisplayFieldDrawer : PropertyDrawer
{
    /// <summary>
    /// 本帧是否应该绘制：运行期看 run，编辑期看 editor。
    /// 高度与绘制必须用同一个判断，否则"不绘制"的那一侧仍会占位，表现成一段空白。
    /// </summary>
    private static bool ShouldDraw(DisplayField attr)
    {
        if (attr == null) return false;
        return Application.isPlaying ? attr.run : attr.editor;
    }

    /// <summary>
    /// 支持展示的属性类型。需要让别的类型也能用 [DisplayField] 时，往这里加一个 case 即可。
    /// </summary>
    private static bool IsSupportedType(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Float:
            case SerializedPropertyType.Boolean:
            case SerializedPropertyType.String:
            case SerializedPropertyType.Color:
            case SerializedPropertyType.ObjectReference:
            case SerializedPropertyType.Vector2:
            case SerializedPropertyType.Vector3:
                return true;
            default:
                return false;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!ShouldDraw(attribute as DisplayField) || !IsSupportedType(property)) return 0f;
        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = attribute as DisplayField;
        if (!ShouldDraw(attr) || !IsSupportedType(property)) return;

        //read 为 true 时只读展示，为 false 时允许在面板上直接改
        using (new EditorGUI.DisabledScope(attr.read))
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }
}

/*
[CustomPropertyDrawer(typeof(PEMaths.PEInt))]
public class PEIntDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        // 获取scaledValue字段
        SerializedProperty scaledValueProperty = property.FindPropertyRelative("scaledValue");

        // 计算标签和输入框的位置
        Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
        Rect fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                                 position.width - EditorGUIUtility.labelWidth, position.height);

        // 处理CustomLabel
        GUIContent customLabel = label;
        // 绘制标签
        EditorGUI.LabelField(labelRect, label);

        // 检查scaledValueProperty是否为null
        if (scaledValueProperty == null)
        {
            Debug.LogError("scaledValueProperty is null in PEIntDrawer");
            return;
        }


        // 绘制数字输入框
        long oldValue = scaledValueProperty.longValue / PEMaths.PEInt.MULTIPLIER_FACTOR;
        string newValueStr = EditorGUI.TextField(fieldRect, oldValue.ToString());
        
        // 验证输入是否为数字
        if (long.TryParse(newValueStr, out long parsedValue))
        {
            // 值发生变化时更新
            if (parsedValue != oldValue)
            {
                scaledValueProperty.longValue = parsedValue * PEMaths.PEInt.MULTIPLIER_FACTOR;
                //OnValueChanged(property);
            }
        }
        else if (!string.IsNullOrEmpty(newValueStr))
        {
            // 输入无效时恢复旧值
            EditorGUI.LabelField(fieldRect, oldValue.ToString());
        }

        EditorGUI.EndProperty();
    }

    // 值变化时调用的方法
    private void OnValueChanged(SerializedProperty property)
    {
        // 获取所属的脚本对象
        UnityEngine.Object targetObject = property.serializedObject.targetObject;
        Debug.Log($"PEInt value changed in {targetObject.name}: {property.DisplayFieldName}");

        // 应用修改
        property.serializedObject.ApplyModifiedProperties();
    }
}
*/


[CustomPropertyDrawer(typeof(DividerAttribute))]
public class DividerDrawer : DecoratorDrawer
{
    // DecoratorDrawer 是 Unity 原生的"装饰特性"机制（[Header]/[Space] 同款）：
    // 由 Unity 自动绘制在字段上方（含数组/List 头部、嵌套类内、任意 Inspector），
    // 不作用于数组元素，也不需要 EditorOverride 额外处理。

    public override float GetHeight()
    {
        DividerAttribute divider = (DividerAttribute)attribute;
        // 总高度 = 上下间距 + 线条高度
        return divider.spacing + divider.height;
    }

    public override void OnGUI(Rect position)
    {
        DividerAttribute divider = (DividerAttribute)attribute;

        // 计算分割线的绘制区域
        Rect rect = new Rect(
            position.x,
            position.y + divider.spacing / 2f,
            position.width,
            divider.height
        );

        // 获取实际颜色（如果用户未指定，则根据皮肤自动适配）
        Color lineColor = divider.color;
        if (lineColor == Color.gray) // 默认灰色的情况下自动适配
        {
            lineColor = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.6f, 0.6f, 0.6f);
        }

        // 绘制分割线
        EditorGUI.DrawRect(rect, lineColor);
    }
}
#endif