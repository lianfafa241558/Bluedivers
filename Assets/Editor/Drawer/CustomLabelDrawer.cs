using UnityEditor;
using UnityEngine;
using Core;

#if UNITY_EDITOR
using System.Reflection;
#endif
using System;

#if UNITY_EDITOR
/// <summary>
/// 瀹氫箟瀵瑰甫鏈?`CustomLabelAttribute` 鐗规€х殑瀛楁鐨勯潰鏉垮唴瀹圭殑缁樺埗琛屼负銆?/// </summary>
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
            // 妫€鏌ユ槸鍚﹀簲鐢ㄤ簡FlagsAttribute涓斾笉妫€鏌ョ户鎵块摼
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

        // 鑾峰彇褰撳墠灞炴€х殑鐖剁骇瀵硅薄
        var parentPath = property.propertyPath;

        var lastDot = parentPath.LastIndexOf('.');
        parentPath = lastDot > 0 ? parentPath.Substring(0, lastDot) : "";
        // 鏋勫缓瀹屾暣鎺у埗灞炴€ц矾寰?        //鑻ョ埗绾ц矾寰勪负绌猴紙椤跺眰灞炴€э級锛岀洿鎺ヤ娇鐢╟ontField锛堝test2锛夛紱
        //鍚﹀垯鎷兼帴鐖惰矾寰勶紙濡侻issionCfg.test2锛?
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
            CompareOperate.Equal => Mathf.Approximately(source, target),//杩欎釜鏄浉浼肩殑鏂规硶
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
        
        // 鑾峰彇scaledValue瀛楁
        SerializedProperty scaledValueProperty = property.FindPropertyRelative("scaledValue");

        // 璁＄畻鏍囩鍜岃緭鍏ユ鐨勪綅缃?        Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
        Rect fieldRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                                 position.width - EditorGUIUtility.labelWidth, position.height);

        // 澶勭悊CustomLabel
        GUIContent customLabel = label;
        // 缁樺埗鏍囩
        EditorGUI.LabelField(labelRect, label);

        // 妫€鏌?scaledValueProperty 鏄惁涓?null
        if (scaledValueProperty == null)
        {
            Debug.LogError("scaledValueProperty is null in PEIntDrawer");
            return;
        }


        // 缁樺埗鏁板瓧杈撳叆妗?        long oldValue = scaledValueProperty.longValue / PEMaths.PEInt.MULTIPLIER_FACTOR;
        string newValueStr = EditorGUI.TextField(fieldRect, oldValue.ToString());
        
        // 楠岃瘉杈撳叆鏄惁涓烘暟瀛?        if (long.TryParse(newValueStr, out long parsedValue))
        {
            // 鍊煎彂鐢熷彉鍖栨椂鏇存柊
            if (parsedValue != oldValue)
            {
                scaledValueProperty.longValue = parsedValue * PEMaths.PEInt.MULTIPLIER_FACTOR;
                //OnValueChanged(property);
            }
        }
        else if (!string.IsNullOrEmpty(newValueStr))
        {
            // 杈撳叆鏃犳晥鏃舵仮澶嶆棫鍊?            EditorGUI.LabelField(fieldRect, oldValue.ToString());
        }

        EditorGUI.EndProperty();
    }

    // 鍊煎彉鍖栨椂璋冪敤鐨勬柟娉?    private void OnValueChanged(SerializedProperty property)
    {
        // 鑾峰彇鎵€灞炵殑鑴氭湰瀵硅薄
        UnityEngine.Object targetObject = property.serializedObject.targetObject;
        Debug.Log($"PEInt value changed in {targetObject.name}: {property.DisplayFieldName}");

        // 搴旂敤淇敼
        property.serializedObject.ApplyModifiedProperties();
    }
}
*/


#endif