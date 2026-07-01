using System;
using System.Collections.Generic;
using System.Reflection;
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
                return IsSingleLine(elementType);
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = fieldType.GetGenericArguments()[0];
                return IsSingleLine(elementType);
            }

            // 普通类型直接检查
            return IsSingleLine(fieldType);
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
                string label = InspectorNames.TryGetValue(prop.name, out var cl) ? cl : null;
                if (label != null)
                    EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
                else
                    EditorGUILayout.PropertyField(prop, true);
                return;
            }

            // 绱у噾甯冨眬
            EditorGUILayout.BeginHorizontal();

            // 显示字段名
            string fieldLabel = InspectorNames.TryGetValue(prop.name, out var customLabel)
                ? customLabel
                : ObjectNames.NicifyVariableName(prop.name);

            GUIContent labelContent = new GUIContent(fieldLabel);
            float labelWidth = EditorStyles.boldLabel.CalcSize(labelContent).x + 8;

            GUILayout.Label(labelContent, EditorStyles.label, GUILayout.Width(labelWidth));
            GUILayout.Space(2);

            // 计算每个字段的宽度
            float remainingWidth = EditorGUIUtility.currentViewWidth - labelWidth - 20;
            float childWidth = Mathf.Clamp(remainingWidth / childProps.Count, 40f, 120f);

            float originalLabelWidth = EditorGUIUtility.labelWidth;

            foreach (var child in childProps)
            {
                if (child.name == "m_Script") continue;

                string shortLabel = InspectorNames.TryGetValue(child.name, out var cl) ? cl : child.displayName;
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
        /// 绘制内联数组 - 使用 ReorderableList，支持拖拽重排、折叠、底部 +/- 按钮，元素内子字段单行显示
        /// </summary>
        private void DrawInlineArrayNative(SerializedProperty prop)
        {
            string fieldLabel = InspectorNames.TryGetValue(prop.name, out var customLabel)
                ? customLabel
                : ObjectNames.NicifyVariableName(prop.name);

            // ===== 手动绘制头部（始终显示，不受折叠影响）=====
            Rect headerRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);

            // 右侧大小输入区域宽度
            float sizeWidth = 40f;

            // 折叠箭头区域（带缩进）
            Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 30f, headerRect.height);
            // 标签区域：从箭头右侧到大小输入左侧
            Rect labelRect = new Rect(foldoutRect.x + 30f, headerRect.y,
                headerRect.width - 30f - sizeWidth, headerRect.height);

            bool isExpanded = EditorGUI.Foldout(foldoutRect, prop.isExpanded, GUIContent.none, true);
            if (isExpanded != prop.isExpanded)
                prop.isExpanded = isExpanded;

            EditorGUI.LabelField(labelRect, fieldLabel, EditorStyles.boldLabel);

            // 头部右键菜单：复制/粘贴整个数组
            if (Event.current.type == EventType.ContextClick && headerRect.Contains(Event.current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("复制数组"), false, () =>
                {
                    CopyArrayToClipboard(prop);
                });
                if (CanPasteArray(prop))
                    menu.AddItem(new GUIContent("粘贴数组"), false, () =>
                    {
                        PasteArrayFromClipboard(prop);
                        prop.serializedObject.ApplyModifiedProperties();
                    });
                else
                    menu.AddDisabledItem(new GUIContent("粘贴数组"));
                menu.ShowAsContext();
                Event.current.Use();
            }

            // 右侧大小输入
            Rect sizeRect = new Rect(headerRect.x + headerRect.width - sizeWidth,
                headerRect.y, sizeWidth, headerRect.height);
            int newSize = EditorGUI.IntField(sizeRect, prop.arraySize);
            if (newSize != prop.arraySize && newSize >= 0)
                prop.arraySize = newSize;

            // ===== 折叠则跳过列表体 =====
            if (!prop.isExpanded)
                return;

            // ===== 缓存或创建 ReorderableList（不带头部）=====
            string listKey = prop.propertyPath;
            if (!_inlineLists.TryGetValue(listKey, out var list) || list == null)
            {
                list = new UnityEditorInternal.ReorderableList(prop.serializedObject, prop, true, false, true, true)
                {
                    drawHeaderCallback = (rect) => { /* 头部已手动绘制，留空 */ },
                    drawElementCallback = (rect, index, isActive, isFocused) =>
                    {
                        var element = prop.GetArrayElementAtIndex(index);
                        var childProps = GetChildProperties(element);

                        // 右键上下文菜单
                        if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
                        {
                            GenericMenu menu = new GenericMenu();
                            menu.AddItem(new GUIContent("复制"), false, () =>
                            {
                                prop.InsertArrayElementAtIndex(index);
                                prop.serializedObject.ApplyModifiedProperties();
                            });
                            menu.AddItem(new GUIContent("删除"), false, () =>
                            {
                                prop.DeleteArrayElementAtIndex(index);
                                prop.serializedObject.ApplyModifiedProperties();
                            });
                            menu.AddItem(new GUIContent("清空数组"), false, () =>
                            {
                                prop.ClearArray();
                                prop.serializedObject.ApplyModifiedProperties();
                            });
                            menu.ShowAsContext();
                            Event.current.Use();
                        }

                        if (childProps.Count > 0)
                        {
                            int oldIndent = EditorGUI.indentLevel;
                            EditorGUI.indentLevel = 0;

                            // 缁樺埗 "Element X" 鏍囩
                            GUIContent labelContent = new GUIContent($"元素 {index}");
                            float labelWidth = EditorStyles.label.CalcSize(labelContent).x + 4;

                            Rect labelRect2 = new Rect(rect.x, rect.y, labelWidth, rect.height);
                            EditorGUI.LabelField(labelRect2, labelContent);

                            // 剩余区域按子字段数等宽平分分配
                            float inlineX = rect.x + labelWidth;
                            float inlineWidth = rect.width - labelWidth;
                            float childFieldWidth = inlineWidth / childProps.Count;

                            float originalLabelWidth = EditorGUIUtility.labelWidth;

                            // 通过反射获取数组元素类型的字段信息，用于读取 InspectorName 属性
                            var elementType = element.GetType(); // element 是 SerializedProperty，但 .GetType() 返回的是 SerializedProperty 类型
                            // 改用 targetType 和 field 路径来获取元素类型
                            //FieldInfo elementFieldInfo = null;
                            Type elementFieldType = null;
                            // 从 prop 的 propertyPath 推断数组元素类型
                            var propPath = prop.propertyPath;
                            int bracketIdx = propPath.IndexOf('[');
                            string arrayFieldName = bracketIdx >= 0 ? propPath.Substring(0, bracketIdx) : propPath;
                            var declaringType = target.GetType();
                            var arrayField = declaringType.GetField(arrayFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (arrayField != null)
                            {
                                Type arrayType = arrayField.FieldType;
                                if (arrayType.IsArray)
                                    elementFieldType = arrayType.GetElementType();
                                else if (arrayType.IsGenericType && arrayType.GetGenericTypeDefinition() == typeof(List<>))
                                    elementFieldType = arrayType.GetGenericArguments()[0];
                            }

                            foreach (var child in childProps)
                            {
                                if (child.name == "m_Script") continue;

                                string shortLabel;
                                if (elementFieldType != null)
                                {
                                    var childField = elementFieldType.GetField(child.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                    if (childField != null)
                                    {
                                        var attr = Attribute.GetCustomAttribute(childField, typeof(InspectorNameAttribute)) as InspectorNameAttribute;
                                        shortLabel = attr != null ? attr.displayName : child.displayName;
                                    }
                                    else
                                    {
                                        shortLabel = child.displayName;
                                    }
                                }
                                else
                                {
                                    shortLabel = child.displayName;
                                }

                                float shortLabelWidth = EditorStyles.label.CalcSize(new GUIContent(shortLabel)).x + 4;
                                EditorGUIUtility.labelWidth = shortLabelWidth;

                                Rect childRect = new Rect(inlineX, rect.y, childFieldWidth, rect.height);
                                EditorGUI.PropertyField(childRect, child, new GUIContent(shortLabel));

                                inlineX += childFieldWidth;
                            }

                            EditorGUIUtility.labelWidth = originalLabelWidth;
                            EditorGUI.indentLevel = oldIndent;
                        }
                        else
                        {
                            EditorGUI.PropertyField(rect, element, GUIContent.none);
                        }
                    },
                    elementHeight = EditorGUIUtility.singleLineHeight
                };

                _inlineLists[listKey] = list;
            }

            list.DoLayoutList();
        }

        /// <summary>
        /// ReorderableList 缓存，避免重复创建
        /// </summary>
        private Dictionary<string, UnityEditorInternal.ReorderableList> _inlineLists = new Dictionary<string, UnityEditorInternal.ReorderableList>();

        //===============================//
        // 数组拷贝/粘贴（JsonUtility）        //===============================//

        /// <summary>
        /// 将 SerializedProperty 数组内容拷贝到系统剪贴板（JSON 格式）        /// </summary>
        private void CopyArrayToClipboard(SerializedProperty prop)
        {
            var list = new List<string>();
            for (int i = 0; i < prop.arraySize; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                // 将每个元素转为 JSON
                string json = SerializedPropertyToJson(element);
                list.Add(json);
            }
            var wrapper = new JsonArrayWrapper { items = list.ToArray() };
            string fullJson = JsonUtility.ToJson(wrapper);
            EditorGUIUtility.systemCopyBuffer = fullJson;
        }

        /// <summary>
        /// 检查剪贴板中是否有可粘贴的数组数据
        /// </summary>
        private bool CanPasteArray(SerializedProperty prop)
        {
            if (string.IsNullOrEmpty(EditorGUIUtility.systemCopyBuffer))
                return false;
            try
            {
                var wrapper = JsonUtility.FromJson<JsonArrayWrapper>(EditorGUIUtility.systemCopyBuffer);
                return wrapper.items != null && wrapper.items.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 从系统剪贴板粘贴 JSON 数组数据到 SerializedProperty
        /// </summary>
        private void PasteArrayFromClipboard(SerializedProperty prop)
        {
            var wrapper = JsonUtility.FromJson<JsonArrayWrapper>(EditorGUIUtility.systemCopyBuffer);
            if (wrapper.items == null)
                return;

            prop.ClearArray();
            prop.arraySize = wrapper.items.Length;
            for (int i = 0; i < wrapper.items.Length; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                JsonToSerializedProperty(element, wrapper.items[i]);
            }
        }

        /// <summary>
        /// 将 SerializedProperty 的值序列化为 JSON 字符串        /// </summary>
        private string SerializedPropertyToJson(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:   return JsonUtility.ToJson(new JsonValue<int> { value = prop.intValue });
                case SerializedPropertyType.Float:     return JsonUtility.ToJson(new JsonValue<float> { value = prop.floatValue });
                case SerializedPropertyType.Boolean:   return JsonUtility.ToJson(new JsonValue<bool> { value = prop.boolValue });
                case SerializedPropertyType.String:    return JsonUtility.ToJson(new JsonValue<string> { value = prop.stringValue });
                case SerializedPropertyType.Color:     return JsonUtility.ToJson(new JsonValue<Color> { value = prop.colorValue });
                case SerializedPropertyType.Vector2:   return JsonUtility.ToJson(new JsonValue<Vector2> { value = prop.vector2Value });
                case SerializedPropertyType.Vector3:   return JsonUtility.ToJson(new JsonValue<Vector3> { value = prop.vector3Value });
                case SerializedPropertyType.Vector4:   return JsonUtility.ToJson(new JsonValue<Vector4> { value = prop.vector4Value });
                case SerializedPropertyType.Vector2Int: return JsonUtility.ToJson(new JsonValue<Vector2Int> { value = prop.vector2IntValue });
                case SerializedPropertyType.Vector3Int: return JsonUtility.ToJson(new JsonValue<Vector3Int> { value = prop.vector3IntValue });
                case SerializedPropertyType.Rect:      return JsonUtility.ToJson(new JsonValue<Rect> { value = prop.rectValue });
                case SerializedPropertyType.RectInt:   return JsonUtility.ToJson(new JsonValue<RectInt> { value = prop.rectIntValue });
                case SerializedPropertyType.Bounds:    return JsonUtility.ToJson(new JsonValue<Bounds> { value = prop.boundsValue });
                case SerializedPropertyType.BoundsInt: return JsonUtility.ToJson(new JsonValue<BoundsInt> { value = prop.boundsIntValue });
                case SerializedPropertyType.Quaternion: return JsonUtility.ToJson(new JsonValue<Quaternion> { value = prop.quaternionValue });
                case SerializedPropertyType.AnimationCurve: return JsonUtility.ToJson(new JsonValue<AnimationCurve> { value = prop.animationCurveValue });
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Character: return JsonUtility.ToJson(new JsonValue<int> { value = prop.intValue });
                case SerializedPropertyType.ObjectReference:
                {
                    string path = prop.objectReferenceValue ? AssetDatabase.GetAssetPath(prop.objectReferenceValue) : "";
                    return JsonUtility.ToJson(new JsonValue<string> { value = path });
                }
                // 复杂类型（struct/class with children）：递归序列化所有子字段
                default:
                {
                    var list = new List<JsonDictEntry>();
                    var childProps = GetChildProperties(prop);
                    foreach (var child in childProps)
                    {
                        if (child.name == "m_Script") continue;
                        list.Add(new JsonDictEntry { key = child.name, value = SerializedPropertyToJson(child) });
                    }
                    return JsonUtility.ToJson(new JsonDictWrapper2 { entries = list.ToArray() });
                }
            }
        }

        /// <summary>
        /// 从 JSON 字符串反序列化到 SerializedProperty
        /// </summary>
        private void JsonToSerializedProperty(SerializedProperty prop, string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:   prop.intValue = JsonUtility.FromJson<JsonValue<int>>(json).value; break;
                case SerializedPropertyType.Float:     prop.floatValue = JsonUtility.FromJson<JsonValue<float>>(json).value; break;
                case SerializedPropertyType.Boolean:   prop.boolValue = JsonUtility.FromJson<JsonValue<bool>>(json).value; break;
                case SerializedPropertyType.String:    prop.stringValue = JsonUtility.FromJson<JsonValue<string>>(json).value; break;
                case SerializedPropertyType.Color:     prop.colorValue = JsonUtility.FromJson<JsonValue<Color>>(json).value; break;
                case SerializedPropertyType.Vector2:   prop.vector2Value = JsonUtility.FromJson<JsonValue<Vector2>>(json).value; break;
                case SerializedPropertyType.Vector3:   prop.vector3Value = JsonUtility.FromJson<JsonValue<Vector3>>(json).value; break;
                case SerializedPropertyType.Vector4:   prop.vector4Value = JsonUtility.FromJson<JsonValue<Vector4>>(json).value; break;
                case SerializedPropertyType.Vector2Int: prop.vector2IntValue = JsonUtility.FromJson<JsonValue<Vector2Int>>(json).value; break;
                case SerializedPropertyType.Vector3Int: prop.vector3IntValue = JsonUtility.FromJson<JsonValue<Vector3Int>>(json).value; break;
                case SerializedPropertyType.Rect:      prop.rectValue = JsonUtility.FromJson<JsonValue<Rect>>(json).value; break;
                case SerializedPropertyType.RectInt:   prop.rectIntValue = JsonUtility.FromJson<JsonValue<RectInt>>(json).value; break;
                case SerializedPropertyType.Bounds:    prop.boundsValue = JsonUtility.FromJson<JsonValue<Bounds>>(json).value; break;
                case SerializedPropertyType.BoundsInt: prop.boundsIntValue = JsonUtility.FromJson<JsonValue<BoundsInt>>(json).value; break;
                case SerializedPropertyType.Quaternion: prop.quaternionValue = JsonUtility.FromJson<JsonValue<Quaternion>>(json).value; break;
                case SerializedPropertyType.AnimationCurve: prop.animationCurveValue = JsonUtility.FromJson<JsonValue<AnimationCurve>>(json).value; break;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Character: prop.intValue = JsonUtility.FromJson<JsonValue<int>>(json).value; break;
                case SerializedPropertyType.ObjectReference:
                {
                    string path = JsonUtility.FromJson<JsonValue<string>>(json).value;
                    if (!string.IsNullOrEmpty(path))
                        prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(path);
                    break;
                }
                // 复杂类型：递归反序列化子字段
                default:
                {
                    var wrapper = JsonUtility.FromJson<JsonDictWrapper2>(json);
                    if (wrapper.entries == null) break;
                    var propCopy = prop.Copy();
                    var endProperty = prop.GetEndProperty();
                    bool enterChildren = true;
                    while (propCopy.NextVisible(enterChildren))
                    {
                        if (SerializedProperty.EqualContents(propCopy, endProperty))
                            break;
                        if (propCopy.name == "m_Script") continue;
                        for (int e = 0; e < wrapper.entries.Length; e++)
                        {
                            if (wrapper.entries[e].key == propCopy.name)
                            {
                                JsonToSerializedProperty(propCopy, wrapper.entries[e].value);
                                break;
                            }
                        }
                        enterChildren = false;
                    }
                    break;
                }
            }
        }

        // JSON 序列化辅助类
        [Serializable]
        private struct JsonArrayWrapper { public string[] items; }
        [Serializable]
        private struct JsonValue<T> { public T value; }
        [Serializable]
        private struct JsonDictEntry { public string key; public string value; }
        [Serializable]
        private struct JsonDictWrapper2 { public JsonDictEntry[] entries; }


      

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


        private bool IsSingleLine(MemberInfo fieldType)
        {
            return Attribute.IsDefined(fieldType, typeof(SinglelineAttribute)); 
        }

    }
}