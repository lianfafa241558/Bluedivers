//  Project : UNITY FOLDOUT
//  Contacts : Pix - ask@pixeye.games

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.BaseTool;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pixeye.Unity
{
    public partial class EditorOverride
    {
      

        //===============================//
        // Inline 相关方法
        //===============================//

        private bool IsInlineField(SerializedProperty prop)
        {
            // 获取字段信息
            var targetType = target.GetType();
            var fieldInfo = targetType.GetField(prop.name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fieldInfo == null) return false;

            var fieldType = fieldInfo.FieldType;

            // 如果是数组或 List，检查元素类型
            if (fieldType.IsArray)
            {
                var elementType = fieldType.GetElementType();
                return Attribute.IsDefined(elementType, typeof(InlineAttribute));
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = fieldType.GetGenericArguments()[0];
                return Attribute.IsDefined(elementType, typeof(InlineAttribute));
            }

            // 普通类型直接检查
            return Attribute.IsDefined(fieldType, typeof(InlineAttribute));
        }



        /// <summary>
        /// 绘制内联对象（所有子字段在一行）
        /// </summary>
        private void DrawInlineObject(SerializedProperty prop)
        {
            // 获取所有子字段
            var childProps = GetChildProperties(prop);

            if (childProps.Count == 0)
            {
                string label = listLabels.TryGetValue(prop.name, out var cl) ? cl : null;
                if (label != null)
                    EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
                else
                    EditorGUILayout.PropertyField(prop, true);
                return;
            }

            // 紧凑布局
            EditorGUILayout.BeginHorizontal();

            // 显示字段名
            string fieldLabel = listLabels.TryGetValue(prop.name, out var customLabel)
                ? customLabel
                : ObjectNames.NicifyVariableName(prop.name);

            GUIStyle miniLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            miniLabelStyle.fontStyle = FontStyle.Bold;

            GUIContent labelContent = new GUIContent(fieldLabel);
            float labelWidth = miniLabelStyle.CalcSize(labelContent).x + 8;

            GUILayout.Label(labelContent, miniLabelStyle, GUILayout.Width(labelWidth));
            GUILayout.Space(2);

            // 计算每个字段的宽度
            float remainingWidth = EditorGUIUtility.currentViewWidth - labelWidth - 20;
            float childWidth = Mathf.Clamp(remainingWidth / childProps.Count, 40f, 120f);

            float originalLabelWidth = EditorGUIUtility.labelWidth;

            foreach (var child in childProps)
            {
                if (child.name == "m_Script") continue;

                string shortLabel = child.displayName;
                GUIContent shortContent = new GUIContent(shortLabel);
                float shortLabelWidth = EditorStyles.miniLabel.CalcSize(shortContent).x + 4;

                EditorGUIUtility.labelWidth = shortLabelWidth;
                EditorGUILayout.PropertyField(child, new GUIContent(shortLabel),
                    GUILayout.Width(childWidth), GUILayout.ExpandWidth(false));
            }

            EditorGUIUtility.labelWidth = originalLabelWidth;
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 获取 SerializedProperty 的所有可见子字段
        /// </summary>
        private List<SerializedProperty> GetChildProperties(SerializedProperty prop)
        {
            var children = new List<SerializedProperty>();
            var iterator = prop.Copy();
            var endProperty = prop.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(iterator, endProperty))
                    break;

                if (iterator.name == "m_Script")
                    continue;

                children.Add(iterator.Copy());
                enterChildren = false;
            }

            return children;
        }


        /// <summary>
        /// 绘制内联数组 - 完美模仿 Unity 原生样式
        /// </summary>
        /// <summary>
        /// 绘制内联数组 - 完美模仿 Unity 原生样式（精确布局版）
        /// </summary>
        private void DrawInlineArrayNative(SerializedProperty prop)
        {
            string fieldLabel = listLabels.TryGetValue(prop.name, out var customLabel)
                ? customLabel
                : ObjectNames.NicifyVariableName(prop.name);

            // ===== 获取原生折叠状态 =====
            string foldoutKey = $"{prop.propertyPath}_foldout";
            bool isExpanded = EditorPrefs.GetBool(foldoutKey, true);

            // ===== 绘制头部（完全原生样式） =====
            Rect headerRect = EditorGUILayout.GetControlRect();

            // 计算各区域位置
            float indentWidth = EditorGUI.indentLevel * 15f;
            float foldoutWidth = 30f;
            float labelWidth = EditorGUIUtility.labelWidth - indentWidth - foldoutWidth;
            float sizeWidth = 40f;
            float buttonWidth = 20f;
            float spacing = 2f;
            float totalWidth = headerRect.width - indentWidth;

            // 折叠按钮区域
            Rect foldoutRect = new Rect(headerRect.x + indentWidth, headerRect.y, foldoutWidth, headerRect.height);

            // 标签区域
            Rect labelRect = new Rect(foldoutRect.x + foldoutWidth, headerRect.y, labelWidth, headerRect.height);

            // 大小输入区域（右侧）
            Rect sizeRect = new Rect(headerRect.x + headerRect.width - sizeWidth - buttonWidth * 2 - spacing * 2,
                headerRect.y, sizeWidth, headerRect.height);

            // + 按钮
            Rect plusRect = new Rect(sizeRect.x + sizeWidth + spacing, headerRect.y, buttonWidth, headerRect.height);

            // - 按钮
            Rect minusRect = new Rect(plusRect.x + buttonWidth + spacing, headerRect.y, buttonWidth, headerRect.height);

            // 绘制折叠箭头（原生样式）
            isExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none, true);

            // 绘制标签
            EditorGUI.LabelField(labelRect, fieldLabel, EditorStyles.boldLabel);

            // 绘制大小
            int newSize = EditorGUI.IntField(sizeRect, prop.arraySize);
            if (newSize != prop.arraySize && newSize >= 0)
            {
                prop.arraySize = newSize;
            }

            // 绘制 +/- 按钮
            if (GUI.Button(plusRect, "+", EditorStyles.miniButton))
            {
                prop.arraySize++;
            }
            if (GUI.Button(minusRect, "-", EditorStyles.miniButton))
            {
                if (prop.arraySize > 0)
                    prop.arraySize--;
            }

            // 保存折叠状态
            EditorPrefs.SetBool(foldoutKey, isExpanded);

            // ===== 如果折叠，不显示元素 =====
            if (!isExpanded)
                return;

            // ===== 绘制每个元素 =====
            EditorGUI.indentLevel++;

            if (prop.arraySize == 0)
            {
                Rect emptyRect = EditorGUILayout.GetControlRect();
                emptyRect.x += EditorGUI.indentLevel * 15f;
                EditorGUI.LabelField(emptyRect, "Empty", EditorStyles.miniLabel);
            }
            else
            {
                bool elementIsInline = IsElementInline(prop);

                for (int i = 0; i < prop.arraySize; i++)
                {
                    var element = prop.GetArrayElementAtIndex(i);

                    if (elementIsInline && element.hasVisibleChildren)
                    {
                        var childProps = GetChildProperties(element);

                        if (childProps.Count > 0)
                        {
                            EditorGUILayout.BeginHorizontal();

                            // 元素索引（与原生样式一致）
                            string indexLabel = $"Element {i}";
                            float indexWidth = 70f;
                            if (i >= 10) indexWidth = 75f;
                            if (i >= 100) indexWidth = 80f;

                            // 计算缩进
                            float indentOffset = EditorGUI.indentLevel * 15f;

                            // 绘制索引
                            Rect indexRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(indexWidth));
                            indexRect.x += indentOffset;
                            EditorGUI.LabelField(indexRect, indexLabel);

                            // 计算剩余宽度
                            float remainingWidth = EditorGUIUtility.currentViewWidth - indexWidth - indentOffset - 40;
                            float childWidth = Mathf.Clamp(remainingWidth / childProps.Count, 40f, 150f);

                            float originalLabelWidth = EditorGUIUtility.labelWidth;

                            foreach (var child in childProps)
                            {
                                if (child.name == "m_Script") continue;

                                string shortLabel = child.displayName;
                                float shortLabelWidth = EditorStyles.label.CalcSize(new GUIContent(shortLabel)).x + 4;

                                EditorGUIUtility.labelWidth = shortLabelWidth;
                                EditorGUILayout.PropertyField(child, new GUIContent(shortLabel),
                                    GUILayout.Width(childWidth), GUILayout.ExpandWidth(false));
                            }

                            EditorGUIUtility.labelWidth = originalLabelWidth;
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(element, new GUIContent($"Element {i}"), true);
                        }
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(element, new GUIContent($"Element {i}"), true);
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// 检查数组/List 的元素是否支持 Inline
        /// </summary>
        private bool IsElementInline(SerializedProperty prop)
        {
            if (!prop.isArray)
                return false;

            // 获取元素类型
            var targetType = target.GetType();
            var fieldInfo = targetType.GetField(prop.name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fieldInfo == null) return false;

            var fieldType = fieldInfo.FieldType;

            if (fieldType.IsArray)
            {
                var elementType = fieldType.GetElementType();
                return Attribute.IsDefined(elementType, typeof(InlineAttribute));
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = fieldType.GetGenericArguments()[0];
                return Attribute.IsDefined(elementType, typeof(InlineAttribute));
            }

            return false;
        }

      
       
    }
}