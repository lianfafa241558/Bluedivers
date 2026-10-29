using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FPSGame.Weapon;

namespace FPSGame.Game
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
                var nameLabel = new GUIContent(GetShortLabel("name"));
                Rect nameRect = new Rect(x, position.y, fieldWidth, position.height);
                DrawNameDropdown(nameRect, nameProp, property, nameLabel);
                x += fieldWidth;
            }

            // type 字段
            Rect typeRect = new Rect(x, position.y, fieldWidth, position.height);
            var typeLabel = new GUIContent(GetShortLabel("type"));
            EditorGUIUtility.labelWidth = CalcLabelWidth(typeLabel);
            EditorGUI.PropertyField(typeRect, typeProp, typeLabel);
            x += fieldWidth;

            // modifier 字段（根据 type 过滤）
            Rect modRect = new Rect(x, position.y, fieldWidth, position.height);
            var modLabel = new GUIContent(GetShortLabel("modifier"));
            DrawFilteredModifier(modRect, modifierProp, typeProp, modLabel);
            x += fieldWidth;

            // value 字段
            Rect valRect = new Rect(x, position.y, position.xMax - x, position.height);
            var valLabel = new GUIContent(GetShortLabel("value"));
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

        /// <summary>
        /// 子字段中文名。property 是列表元素（名字是 "data"），拿它去 targetObject 上找同名字段必然落空，
        /// 标签就会退化成 "Type"/"Value" ⇒ 直接按元素类型 ModifyAttrData 取 InspectorName。
        /// </summary>
        private static string GetShortLabel(string childName)
        {
            var childField = typeof(ModifyAttrData).GetField(childName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (childField != null)
            {
                // ⚠ 必须全限定 System.Attribute：本文件在 FPSGame.Game 命名空间内，
                //   裸写 Attribute 会被解析为外层命名空间的 FPSGame.Attribute（同名子命名空间遮蔽）
                var attr = System.Attribute.GetCustomAttribute(childField, typeof(InspectorNameAttribute)) as InspectorNameAttribute;
                if (attr != null) return attr.displayName;
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
        /// （ModifyAttrData 同时被 WeaponUpgradeData_SO / WeaponModuleData_SO 使用）
        /// </summary>
        private static void DrawNameDropdown(Rect position, SerializedProperty nameProp,
            SerializedProperty property, GUIContent label)
        {
            var targetObj = property.serializedObject.targetObject;
            if (targetObj == null)
            {
                EditorGUI.PropertyField(position, nameProp, label);
                return;
            }

            var names = GetUniqueAttrNames(targetObj);

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
        /// 候选名优先取「引用本 SO 的那把武器」的 m_UniqueAttr；找不到引用武器（新建模组还没挂进 Modules）
        /// 就退回全部武器的并集 —— 否则下拉只剩一个空项，看着像"专有属性白加了"。
        /// </summary>
        private static List<string> GetUniqueAttrNames(UnityEngine.Object target)
        {
            EnsureCacheHook();

            var id = target.GetInstanceID();
            if (_nameCache.TryGetValue(id, out var cached) && cached.Version == _cacheVersion && cached.Names != null)
                return cached.Names;

            var names = new List<string> { "" };
            var seen = new HashSet<string>();

            var owner = FindReferencingWeapon(target);
            if (owner != null)
            {
                AppendUniqueNames(owner, names, seen);
            }
            else
            {
                foreach (var path in GetAllWeaponPrefabPaths())
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    foreach (var controller in prefab.GetComponentsInChildren<WeaponPlayerController>(true))
                        AppendUniqueNames(controller, names, seen);
                }
            }

            _nameCache[id] = new CachedNames { Version = _cacheVersion, Names = names };
            return names;
        }

        private static void AppendUniqueNames(WeaponPlayerController controller, List<string> names, HashSet<string> seen)
        {
            if (controller == null) return;

            var uniqueAttrProp = new SerializedObject(controller).FindProperty("m_UniqueAttr");
            if (uniqueAttrProp == null) return;

            for (var i = 0; i < uniqueAttrProp.arraySize; i++)
            {
                var n = uniqueAttrProp.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (n != null && !string.IsNullOrEmpty(n.stringValue) && seen.Add(n.stringValue))
                    names.Add(n.stringValue);
            }
        }

        /// <summary>
        /// 找出把 target 挂在 Modules / Upgrade 里的武器（返回组件而不是 SerializedObject，
        /// 旧实现把"没找到"也缓存成 null，之后即使挂上了引用也永远查不到）。
        /// </summary>
        private static WeaponPlayerController FindReferencingWeapon(UnityEngine.Object target)
        {
            foreach (var path in GetAllWeaponPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // 组件不一定挂根节点，用递归取全部
                foreach (var controller in prefab.GetComponentsInChildren<WeaponPlayerController>(true))
                {
                    var cso = new SerializedObject(controller);
                    if (ContainsRef(cso.FindProperty("Modules"), target))
                        return controller;

                    var upgradeProp = cso.FindProperty("Upgrade");
                    for (var i = 0; upgradeProp != null && i < upgradeProp.arraySize; i++)
                    {
                        var valueProp = upgradeProp.GetArrayElementAtIndex(i).FindPropertyRelative("Value");
                        if (ContainsRef(valueProp, target))
                            return controller;
                    }
                }
            }

            return null;
        }

        private static bool ContainsRef(SerializedProperty arrayProp, UnityEngine.Object target)
        {
            if (arrayProp == null) return false;
            for (var i = 0; i < arrayProp.arraySize; i++)
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == target) return true;
            return false;
        }

        private static List<string> GetAllWeaponPrefabPaths()
        {
            if (_weaponPrefabPaths != null) return _weaponPrefabPaths;

            _weaponPrefabPaths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { WeaponPrefabRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) _weaponPrefabPaths.Add(path);
            }
            return _weaponPrefabPaths;
        }

        /// <summary>
        /// 资产/引用一变就作废缓存：不然在 prefab 里新加专有属性、或把模组挂进 Modules 之后，
        /// 已经打开过的 SO 依然拿旧结果，必须手动重编译才刷新。
        /// </summary>
        private static void EnsureCacheHook()
        {
            if (_cacheHooked) return;
            _cacheHooked = true;
            EditorApplication.projectChanged += () =>
            {
                _cacheVersion++;
                _nameCache.Clear();
                _weaponPrefabPaths = null;
            };
        }

        private const string WeaponPrefabRoot = "Assets/Resources/Weapons";

        private struct CachedNames
        {
            public int Version;
            public List<string> Names;
        }

        private static readonly Dictionary<int, CachedNames> _nameCache = new();
        private static List<string> _weaponPrefabPaths;
        private static int _cacheVersion;
        private static bool _cacheHooked;
    }
}
