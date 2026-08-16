using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.Game
{
    /// <summary>
    /// ModifyAttrData 的 PropertyDrawer：
    /// 将四个字段绘制为单行，并根据 type 的值过滤 modifier 下拉框
    /// </summary>
    [CustomPropertyDrawer(typeof(ModifyAttrData))]
    public class ModifyAttrDataPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProp = property.FindPropertyRelative("name");
            var typeProp = property.FindPropertyRelative("type");
            var modifierProp = property.FindPropertyRelative("modifier");
            var valueProp = property.FindPropertyRelative("value");

            if (typeProp == null || modifierProp == null || valueProp == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // 用 intValue 而非 enumValueIndex：底层值才匹配枚举定义值
            bool isSpecial = (WeaponAttrType)typeProp.intValue == WeaponAttrType.Special;
            float fieldWidth = isSpecial ? position.width / 4f : position.width / 3f;
            float originalLabelWidth = EditorGUIUtility.labelWidth;
            float x = position.x;

            // name 字段（仅 type==Special 时显示，改为下拉框）
            if (isSpecial)
            {
                var nameLabel = new GUIContent(GetShortLabel(property, "name"));
                Rect nameRect = new Rect(x, position.y, fieldWidth, position.height);
                DrawNameDropdown(nameRect, nameProp, property, nameLabel);
                x += fieldWidth;
            }

            // type 字段
            Rect typeRect = new Rect(x, position.y, fieldWidth, position.height);
            var typeLabel = new GUIContent(GetShortLabel(property, "type"));
            EditorGUIUtility.labelWidth = CalcLabelWidth(typeLabel);
            EditorGUI.PropertyField(typeRect, typeProp, typeLabel);
            x += fieldWidth;

            // modifier 字段（根据 type 过滤）
            Rect modRect = new Rect(x, position.y, fieldWidth, position.height);
            var modLabel = new GUIContent(GetShortLabel(property, "modifier"));
            DrawFilteredModifier(modRect, modifierProp, typeProp, modLabel);
            x += fieldWidth;

            // value 字段
            Rect valRect = new Rect(x, position.y, position.xMax - x, position.height);
            var valLabel = new GUIContent(GetShortLabel(property, "value"));
            EditorGUIUtility.labelWidth = CalcLabelWidth(valLabel);
            EditorGUI.PropertyField(valRect, valueProp, valLabel);

            EditorGUIUtility.labelWidth = originalLabelWidth;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        // ========== modifier 过滤逻辑 ==========

        /// <summary>
        /// 根据 typeProp 的值过滤 modifier 下拉框
        /// </summary>
        private static void DrawFilteredModifier(Rect position, SerializedProperty modifierProp,
            SerializedProperty typeProp, GUIContent label)
        {
            if (typeProp == null)
            {
                EditorGUI.PropertyField(position, modifierProp, label ?? GUIContent.none);
                return;
            }

            // 用 intValue（底层值）而非 enumValueIndex（声明位置索引）
            WeaponAttrType attrType = (WeaponAttrType)typeProp.intValue;

            if (!WeaponAttributeFactory.attributeConfigs.TryGetValue(attrType, out var config))
            {
                EditorGUI.PropertyField(position, modifierProp, label ?? GUIContent.none);
                return;
            }

            ModifierType allowedModifier = config.Item3;

            List<string> filteredNames = new();
            List<int> filteredIntValues = new();

            foreach (ModifierType val in Enum.GetValues(typeof(ModifierType)))
            {
                if (val == ModifierType.All) continue;
                if (allowedModifier == ModifierType.All || allowedModifier.HasFlag(val))
                {
                    filteredNames.Add(val.ToString());
                    filteredIntValues.Add((int)val);
                }
            }

            if (filteredNames.Count == 0)
            {
                EditorGUI.PropertyField(position, modifierProp, label ?? GUIContent.none);
                return;
            }

            int currentIntValue = modifierProp.intValue;
            int currentIndex = filteredIntValues.IndexOf(currentIntValue);
            if (currentIndex < 0)
            {
                currentIndex = 0;
                modifierProp.intValue = filteredIntValues[0];
            }

            // 绘制标签
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                float labelW = CalcLabelWidth(label);
                var lblRect = new Rect(position.x, position.y, labelW, position.height);
                var popupRect = new Rect(position.x + labelW, position.y, position.width - labelW, position.height);
                EditorGUI.LabelField(lblRect, label);
                int newIndex = EditorGUI.Popup(popupRect, currentIndex, filteredNames.ToArray());
                if (newIndex >= 0 && newIndex < filteredIntValues.Count)
                    modifierProp.intValue = filteredIntValues[newIndex];
            }
            else
            {
                int newIndex = EditorGUI.Popup(position, currentIndex, filteredNames.ToArray());
                if (newIndex >= 0 && newIndex < filteredIntValues.Count)
                    modifierProp.intValue = filteredIntValues[newIndex];
            }
        }

        private static string GetShortLabel(SerializedProperty property, string childName)
        {
            var targetType = property.serializedObject.targetObject.GetType();
            var field = targetType.GetField(property.name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                var childField = field.FieldType.GetField(childName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (childField != null)
                {
                    var attr = Attribute.GetCustomAttribute(childField, typeof(InspectorNameAttribute)) as InspectorNameAttribute;
                    if (attr != null) return attr.displayName;
                }
            }
            return ObjectNames.NicifyVariableName(childName);
        }

        private static float CalcLabelWidth(GUIContent label)
        {
            return EditorStyles.label.CalcSize(label).x + 4;
        }

        private static void DrawLabeledField(Rect position, SerializedProperty prop, GUIContent label)
        {
            float labelW = CalcLabelWidth(label);
            var lblRect = new Rect(position.x, position.y, labelW, position.height);
            var fldRect = new Rect(position.x + labelW, position.y, position.width - labelW, position.height);
            EditorGUI.LabelField(lblRect, label);
            EditorGUI.PropertyField(fldRect, prop, GUIContent.none);
        }

        /// <summary>
        /// 将 name 字段绘制为下拉框，选项来自武器的 m_UniqueAttr
        /// ModifyAttrData 存在于 WeaponUpgradeData_SO / WeaponModuleData_SO 中，
        /// 需要向上查找到武器的 WeaponPlayerController 组件上的 m_UniqueAttr
        /// </summary>
        private static void DrawNameDropdown(Rect position, SerializedProperty nameProp,
            SerializedProperty property, GUIContent label)
        {
            var so = property.serializedObject;
            var targetObj = so.targetObject;
            if (targetObj == null)
            {
                EditorGUI.PropertyField(position, nameProp, label);
                return;
            }

            // 收集名称列表
            var names = new List<string> { "" };

            // 通过 Selection 或遍历 Resources 查找引用此 SO 的 WeaponPlayerController
            // 简单方案：遍历当前 Selection 的武器，找到包含此 SO 的武器
            var weaponSO = FindWeaponSerializedObject(so, targetObj);
            if (weaponSO != null)
            {
                var uniqueAttrProp = weaponSO.FindProperty("m_UniqueAttr");
                if (uniqueAttrProp != null)
                {
                    for (var i = 0; i < uniqueAttrProp.arraySize; i++)
                    {
                        var elem = uniqueAttrProp.GetArrayElementAtIndex(i);
                        var n = elem.FindPropertyRelative("name");
                        if (n != null && !string.IsNullOrEmpty(n.stringValue))
                            names.Add(n.stringValue);
                    }
                }
            }

            // 如果没找到，也尝试从 targetObj 本身读取（模块 SO 可能有）
            if (names.Count == 1)
            {
                var uniqueAttrProp = so.FindProperty("m_UniqueAttr");
                if (uniqueAttrProp != null)
                {
                    for (var i = 0; i < uniqueAttrProp.arraySize; i++)
                    {
                        var elem = uniqueAttrProp.GetArrayElementAtIndex(i);
                        var n = elem.FindPropertyRelative("name");
                        if (n != null && !string.IsNullOrEmpty(n.stringValue))
                            names.Add(n.stringValue);
                    }
                }
            }

            var currentName = nameProp.stringValue;
            var currentIndex = names.IndexOf(currentName);
            if (currentIndex < 0) currentIndex = 0;

            float labelW = CalcLabelWidth(label);
            var lblRect = new Rect(position.x, position.y, labelW, position.height);
            var popupRect = new Rect(position.x + labelW, position.y, position.width - labelW, position.height);

            EditorGUI.LabelField(lblRect, label);
            var newIndex = EditorGUI.Popup(popupRect, currentIndex, names.ToArray());
            if (newIndex != currentIndex)
                nameProp.stringValue = names[newIndex];
        }

        /// <summary>
        /// 从选中的武器预设中查找引用此 SO 的 WeaponPlayerController
        /// </summary>
        private static SerializedObject FindWeaponSerializedObject(SerializedObject so, UnityEngine.Object targetObj)
        {
            var instanceId = targetObj.GetInstanceID();
            if (_soToWeaponCache.TryGetValue(instanceId, out var cached))
                return cached;

            var prefabs = Resources.LoadAll<GameObject>("Weapons");
            foreach (var prefab in prefabs)
            {
                var controller = prefab.GetComponent<WeaponPlayerController>();
                if (controller == null) continue;

                var cso = new SerializedObject(controller);

                // 检查 Modules
                var modulesProp = cso.FindProperty("Modules");
                if (modulesProp != null)
                {
                    for (var i = 0; i < modulesProp.arraySize; i++)
                    {
                        if (modulesProp.GetArrayElementAtIndex(i).objectReferenceValue == targetObj)
                        {
                            _soToWeaponCache[instanceId] = cso;
                            return cso;
                        }
                    }
                }

                // 检查 Upgrade
                var upgradeProp = cso.FindProperty("Upgrade");
                if (upgradeProp != null)
                {
                    for (var i = 0; i < upgradeProp.arraySize; i++)
                    {
                        var levelElem = upgradeProp.GetArrayElementAtIndex(i);
                        var valueProp = levelElem.FindPropertyRelative("Value");
                        if (valueProp == null) continue;
                        for (var j = 0; j < valueProp.arraySize; j++)
                        {
                            if (valueProp.GetArrayElementAtIndex(j).objectReferenceValue == targetObj)
                            {
                                _soToWeaponCache[instanceId] = cso;
                                return cso;
                            }
                        }
                    }
                }
            }

            _soToWeaponCache[instanceId] = null;
            return null;
        }

        private static readonly Dictionary<int, SerializedObject> _soToWeaponCache = new();
    }
}
