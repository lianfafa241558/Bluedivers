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

    private bool ShouldDisplayField(SerializedProperty property, CompareAttribute attr)
    {
        if (string.IsNullOrEmpty(attr.contField)) return true;

        // 获取当前属性的父级对象
        var parentPath = property.propertyPath;

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
    public bool Calculate(CompareOperate operate, float source, float target)
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

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return Application.isPlaying || !(attribute as DisplayField).run ? EditorGUI.GetPropertyHeight(property, label, true) : 0;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {

        // 确保属性是可序列化的
        if (property.propertyType == SerializedPropertyType.Integer ||
            property.propertyType == SerializedPropertyType.Float ||
            property.propertyType == SerializedPropertyType.Boolean ||
            property.propertyType == SerializedPropertyType.String ||
            property.propertyType == SerializedPropertyType.ObjectReference)
        {
            var attr = attribute as DisplayField;
            if ((Application.isPlaying&& attr.run) ||(!Application.isPlaying && attr.editor))
            {

                if ((attribute as DisplayField).read)
                {
                    GUI.enabled = false;
                    EditorGUI.PropertyField(position, property, label, true);
                    GUI.enabled = true;
                }
                else
                {
                    EditorGUI.PropertyField(position, property, label, true);
                }
            }
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


#endif