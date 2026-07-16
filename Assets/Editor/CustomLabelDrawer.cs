
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.BaseTool;
using Unity.FPS.Game;

#if UNITY_EDITOR
using System.Reflection;
using System.Text.RegularExpressions;
#endif
using System;

#if UNITY_EDITOR
/// <summary>
/// 定义对带有 `CustomLabelAttribute` 特性的字段的面板内容的绘制行为。
/// </summary>
[CustomPropertyDrawer(typeof(CustomLabelAttribute))]
public class CustomLabelDrawer : PropertyDrawer
{
    private GUIContent _label = null;
    private bool reProperty = true;
    private Dictionary<string, string> customEnumNames = new Dictionary<string, string>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CustomLabelAttribute customLabel = (CustomLabelAttribute)attribute;
        /*
        if (!string.IsNullOrEmpty(customLabel.contField)) {
            var sourceProperty = property.serializedObject.FindProperty(customLabel.contField);

            if (sourceProperty == null || (sourceProperty.propertyType != SerializedPropertyType.Boolean&& sourceProperty.propertyType != SerializedPropertyType.Enum)) {
                EditorGUI.HelpBox(position, $"找不到控制的属性: {customLabel.contField}", MessageType.Error);
                return;
            }
            if (sourceProperty.propertyType == SerializedPropertyType.Boolean&&!sourceProperty.boolValue) {
                return;
            }
            if (sourceProperty.propertyType == SerializedPropertyType.Enum &&! customLabel.operate.Calculate(sourceProperty.enumValueIndex,customLabel.enumValue)) {
                return;
            }
        }*/
        if (!ShouldDisplay(property, customLabel)) return;

        reProperty = true;
        //判断是否为集合类型
        string displayName = property.displayName;
        //判断是否是集合内的项
        bool isElement = Regex.IsMatch(displayName, "Element \\d+");
        //判断是否为枚举类型
        bool isEnum = property != null && property.propertyType == SerializedPropertyType.Enum;

        if (_label == null)
        {
            GetPropertyHeight(property, label);
            string reName = "";
            //reName += property.propertyPath;
            //绘制自定义的名称
            string name = (attribute as CustomLabelAttribute).name;
            //如果是集合，那么修改其中的前缀
            if (isElement)
            {
                //如果是枚举类型就返回“元素X”，否则返回这个元素的“name”
                //这里我不知道如何获取集合中的序号，始终会返回 0，只能等后续有人改进了
                if (isEnum) name = "元素" + property.enumValueIndex;
                else name = displayName;
            }

            //reName = "" + customLabel.enumValue;
            _label = new GUIContent(name + reName);
        }

        if (isEnum)//如果是枚举，单独绘制
        {
            DrawEnum(position, property, _label);
            reProperty = false;
        }

        //if (isCollection&&!isElement)
        //{
        //    EditorGUI.PropertyField(position, property,new  GUIContent("数组"), true);
        //}
        /*

        //只要下面还有哪怕一个属性就不会报错，不知道为什么
        if (reProperty) EditorGUI.PropertyField(position, property, _label, true);
        */


        // 添加有效性检查
        if (reProperty && property != null)
        {
            try
            {
                EditorGUI.PropertyField(position, property, _label, true);
            }
            catch (InvalidOperationException)
            {
                // 如果发生异常，回退到简单绘制
                //EditorGUI.LabelField(position, _label, new GUIContent(property.displayName));
            }
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

    private bool ShouldDisplay(SerializedProperty property, CustomLabelAttribute attr)
    {
        if (string.IsNullOrEmpty(attr.contField)) return true;

        // 获取当前属性的父级对象
        var parentPath = property.propertyPath;
        //Debug.LogError("原始结构"+ parentPath);
        /*
        if (parentPath.Contains("Array")) // 处理数组元素的情况
        {

            var arrayIndex = parentPath.LastIndexOf(']');
            parentPath = parentPath.Substring(0, arrayIndex+1);
        }
        else// 处理普通嵌套情况
        {
            var lastDot = parentPath.LastIndexOf('.');
            parentPath = lastDot > 0 ? parentPath.Substring(0, lastDot) : "";
        }*/

        var lastDot = parentPath.LastIndexOf('.');
        parentPath = lastDot > 0 ? parentPath.Substring(0, lastDot) : "";
        // 构建完整控制属性路径
        //若父级路径为空（顶层属性），直接使用contField（如test2）；
        //否则拼接父路径（如MissionCfg.test2）

        var fullPath = string.IsNullOrEmpty(parentPath)
            ? attr.contField
            : $"{parentPath}.{attr.contField}";

        var controlProp = property.serializedObject.FindProperty(fullPath);
        if (controlProp == null)
        {
            Debug.LogError($"找不到控制属性: {fullPath}");
            return false;
        }

        switch (controlProp.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return Calculate(attr.operate,controlProp.boolValue?1:0, attr.enumValue);
            case SerializedPropertyType.Integer:
                return Calculate(attr.operate, controlProp.intValue, attr.enumValue);
            case SerializedPropertyType.Float:
                return Calculate(attr.operate,controlProp.floatValue, attr.enumValue);

            case SerializedPropertyType.Enum:
                bool havFlags = HasFlagsAttribute(controlProp);
                //int value = havFlags ? controlProp.intValue : controlProp.enumValueIndex;//GetEnumValue(controlProp);
                int value = controlProp.intValue;
                return Calculate(attr.operate, value, attr.enumValue);
        }
        return true;
    }
    public bool Calculate(CompareOperate operate, float source, float target)
    {
        return operate switch {
            CompareOperate.Equal => Mathf.Approximately(source, target),//这个是相似的方法
            CompareOperate.NotEqual => !Mathf.Approximately(source, target),
            CompareOperate.Less => source < target,
            CompareOperate.LessEqual => source <= target,
            CompareOperate.Greater => source > target,
            CompareOperate.GreaterEqual => source >= target,
            CompareOperate.Contain => (int)source != 0 && ((int)source & (int)target) == (int)target,
            CompareOperate.NotContain => (int)source != 0 && ((int)source & (int)target) == 0,
            _ => throw new System.ArgumentException("找不到操作符" + operate),
        };
        /*
            switch (operate)
            {
                case CompareOperate.Equal: return Mathf.Approximately(source, target);//这个是相似的方法
                case CompareOperate.NotEqual: return !Mathf.Approximately(source, target);
                case CompareOperate.Less: return source < target;
                case CompareOperate.LessEqual: return source <= target;
                case CompareOperate.Greater: return source > target;
                case CompareOperate.GreaterEqual: return source >= target;
                default: throw new System.ArgumentException("找不到操作符"+ operate);
            }*/
    }


    public void SetUpCustomEnumNames(SerializedProperty property, string[] enumNames)
    {


        object[] customAttributes = fieldInfo.GetCustomAttributes(typeof(CustomLabelAttribute), false);
        foreach (CustomLabelAttribute customAttribute in customAttributes)
        {
            Type enumType = fieldInfo.FieldType;

            foreach (string enumName in enumNames)
            {
                FieldInfo field = enumType.GetField(enumName);
                if (field == null) continue;
                CustomLabelAttribute[] attrs = (CustomLabelAttribute[])field.GetCustomAttributes(customAttribute.GetType(), false);

                if (!customEnumNames.ContainsKey(enumName))
                {
                    foreach (CustomLabelAttribute labelAttribute in attrs)
                    {
                        customEnumNames.Add(enumName, labelAttribute.name);
                    }
                }
            }
        }
    }

   


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        CustomLabelAttribute customLabel = (CustomLabelAttribute)attribute;
        if (!ShouldDisplay(property, customLabel)) return 0;
        /*
        if (!string.IsNullOrEmpty(customLabel.contField)) {
            var sourceProperty = property.serializedObject.FindProperty(customLabel.contField);
            if (sourceProperty.propertyType == SerializedPropertyType.Boolean && !sourceProperty.boolValue) {
                return 0;
            }
            if (sourceProperty.propertyType == SerializedPropertyType.Enum && !customLabel.operate.Calculate(sourceProperty.enumValueIndex, customLabel.enumValue))
            {
                return 0;
            }
        }*/




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

    //作者：MUXIGameStudio https://www.bilibili.com/read/cv9094952/

    /// <summary>
    /// 重新绘制枚举类型属性
    /// </summary>
    /// <param name="position">坐标</param>
    /// <param name="property">不知道</param>
    /// <param name="label">返回的label</param>
    
    /*
    private void DrawEnum(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginChangeCheck();
        Type type = fieldInfo.FieldType;
        //Debug.LogWarning("原始类型"+type);
        string[] names = property.enumNames;
        string[] values = new string[names.Length];

        while (!type.IsArray && typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
        {
            type = type.GenericTypeArguments[0];
        }

        while (type.IsArray)
        {
            type = type.GetElementType();
        }
        //Debug.LogWarning("最终类型" + type);
        for (int i = 0; i < names.Length; ++i)
        {
            FieldInfo info = type.GetField(names[i]);

            if (info != null)
            {
                //大概看懂了，获取这个枚举选项里面的自定义属性，如果没有就直接赋原值，有就赋第一个自定义属性的值
                var enumAttributes = (CustomLabelAttribute[])info.GetCustomAttributes(typeof(CustomLabelAttribute), false);
                values[i] = enumAttributes.Length == 0 ? names[i] : enumAttributes[0].name;//+" "+ i;
                
            }
            else
            {
                values[i] = "info不存在";
            }

        }

        int index = EditorGUI.Popup(position, label.text, property.enumValueIndex, values);
        if (EditorGUI.EndChangeCheck() && index != -1)
        {
            property.enumValueIndex = index;
        }
    }*/
    private void DrawEnum(Rect position, SerializedProperty property, GUIContent label) {
        // 新增Flags枚举检测逻辑
        Type enumType = fieldInfo.FieldType;
        bool isFlags = enumType.IsDefined(typeof(FlagsAttribute), false);

        // 原类型解析逻辑保持不变
        while (!enumType.IsArray && typeof(System.Collections.IEnumerable).IsAssignableFrom(enumType)) {
            enumType = enumType.GenericTypeArguments[0];
        }
        while (enumType.IsArray) {
            enumType = enumType.GetElementType();
        }

        // 生成显示名称（保留原自定义标签逻辑）
        string[] displayNames = new string[property.enumNames.Length];
        for (int i = 0; i < property.enumNames.Length; ++i) {
            FieldInfo field = enumType.GetField(property.enumNames[i]);
            displayNames[i] = field != null ?
                ((CustomLabelAttribute[])field.GetCustomAttributes(typeof(CustomLabelAttribute), false))
                    .FirstOrDefault()?.name ?? property.enumNames[i]
                : "Unknown";
        }

        EditorGUI.BeginChangeCheck();

        // 修改点：根据是否Flags选择不同控件
        int newValue = isFlags ?
            EditorGUI.MaskField(position, label, property.intValue, displayNames) :
            EditorGUI.Popup(position, label.text, property.enumValueIndex, displayNames);

        if (EditorGUI.EndChangeCheck()) {
            // 修改点：统一赋值逻辑
            if (isFlags) {
                property.intValue = newValue;  // Flags使用位掩码值
            }
            else {
                property.enumValueIndex = newValue;
            }
        }
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
            if ((Application.isPlaying&& attr.run)||(!Application.isPlaying && attr.editor))
            {

                if ((attribute as DisplayField).onlyRead)
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

[CustomPropertyDrawer(typeof(BoolShowAttribute))]
public class BoolShowAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        var condAttr = (BoolShowAttribute)attribute;
        var sourceProperty = property.serializedObject.FindProperty(condAttr.boolFieldName);

        if (sourceProperty == null || sourceProperty.propertyType != SerializedPropertyType.Boolean) {
            EditorGUI.HelpBox(position, $"找不到: {condAttr.boolFieldName}", MessageType.Error);
            return;
        }

        if (sourceProperty.boolValue== condAttr.value) {
            EditorGUI.PropertyField(position, property, label, true);
        }
        
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        return EditorGUI.GetPropertyHeight(property, label, true);
    }
}


[CustomPropertyDrawer(typeof(NullCheckAttribute))]
public class NullCheckDrawer : PropertyDrawer
{
    private static float lastWarningTime;
    private const float WarningInterval = 1f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.PropertyField(position, property, label);

        if (property.propertyType == SerializedPropertyType.ObjectReference &&
            property.objectReferenceValue == null &&
            EditorApplication.timeSinceStartup - lastWarningTime >= WarningInterval)
        {
            Debug.LogError($"空引用警告: {property.name} 未赋值",
                         property.serializedObject.targetObject);
            lastWarningTime = (float)EditorApplication.timeSinceStartup;
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
        if (attribute is CustomLabelAttribute customLabelAttr)
        {
            customLabel = new GUIContent(customLabelAttr.name);
        }
        // 绘制标签
        EditorGUI.LabelField(labelRect, label);

        // 检查 scaledValueProperty 是否为 null
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
        Debug.Log($"PEInt value changed in {targetObject.name}: {property.displayName}");

        // 应用修改
        property.serializedObject.ApplyModifiedProperties();
    }
}
*/


#endif

