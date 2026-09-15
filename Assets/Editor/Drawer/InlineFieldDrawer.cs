using System;
using System.Collections.Generic;
using System.Reflection;
using FPSGame.Attribute;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 单行内联列表的绘制样式（Rect 版，供 PropertyDrawer 使用）。
/// 默认值对应"Heading + 元素行 + 底部 +/-"的经典外观。
/// </summary>
public class InlineListStyle
{
    /// <summary>是否自绘头部（折叠箭头 + 标签 + Size 输入框）。false 表示标题由调用方自己画。</summary>
    public bool ShowHeader = true;

    /// <summary>头部是否可折叠。false = 始终展开（忽略 isExpanded）。</summary>
    public bool Collapsible = true;

    /// <summary>头部标签是否加粗。</summary>
    public bool BoldHeaderLabel;

    /// <summary>是否响应右键菜单（头部=复制/粘贴数组，元素行=复制/删除/清空）。</summary>
    public bool ShowContextMenu = true;

    /// <summary>是否显示 "元素 i" 前缀。</summary>
    public bool ShowElementLabel = true;

    /// <summary>"元素 i" 的格式。</summary>
    public string ElementLabelFormat = "元素 {0}";

    /// <summary>"元素 i" 标签的额外宽度（用于留白/对齐）。</summary>
    public float ElementLabelExtraWidth;

    /// <summary>底部 - / + 按钮。</summary>
    public bool ShowFooterButtons = true;

    /// <summary>非空则在底部画一个右对齐的添加按钮（文字即按钮文本）。</summary>
    public string AddButtonText;

    /// <summary>列表整体下方额外留白。</summary>
    public float ExtraBottomSpace;

    /// <summary>子字段显示名；null = 默认取 [InspectorName] &gt; displayName。</summary>
    public Func<SerializedProperty, string> ChildLabelProvider;

    /// <summary>子字段自定义绘制（收到的是该子字段的完整槽位 Rect）；null = 标签 + PropertyField。</summary>
    public Action<Rect, SerializedProperty> ChildDrawer;
}

/// <summary>
/// [Singleline] 单行内联字段/列表的**唯一**绘制实现，供全项目复用。
///
/// 背景：该能力原先散落成多份复制实现（EditorOverride 兜底 Inspector 的
/// <c>DrawInlineArrayNative</c>、DamageDataDrawer 的 <c>DrawSinglelineList</c>、
/// PatrolCfgDrawer/CampTemplateDrawer 里手写的 SKVP 行……）。本类将其收敛为：
///   · Layout 版：<see cref="DrawInlineListLayout"/> / <see cref="DrawInlineObjectLayout"/>
///     —— 给 Editor / EditorWindow（EditorGUILayout 流式布局）用；
///   · Rect 版：<see cref="DrawInlineListRect"/> / <see cref="GetInlineListHeight"/> /
///     <see cref="DrawElementRow"/>
///     —— 给 PropertyDrawer（EditorGUI 定式 Rect）用。
///
/// 注意：PropertyDrawer 是定式布局，不能用 Layout 版；Editor 面板是流式布局，
/// 建议用 Layout 版（自绘头部 + ReorderableList 支持拖拽重排）。
/// </summary>
public static class InlineFieldDrawer
{
    //===============================//
    // 缓存
    //===============================//

    /// <summary>Layout 版 ReorderableList 缓存（key = 目标实例 ID + propertyPath）。</summary>
    private static readonly Dictionary<string, ReorderableList> ListCache = new Dictionary<string, ReorderableList>();

    /// <summary>Layout 版元素行样式（"元素 i" 后紧跟等宽子字段）。</summary>
    private static readonly InlineListStyle LayoutRowStyle = new InlineListStyle
    {
        ShowHeader = false,
        ShowElementLabel = true,
        ElementLabelExtraWidth = 4f,
        ShowFooterButtons = false,
        ShowContextMenu = false, // 菜单在 ReorderableList 的 drawElementCallback 里统一处理
    };

    //===============================//
    // 1. 类型判定
    //===============================//

    /// <summary>该字段是否为单行内联字段（自身或元素类型带 [Singleline]）。</summary>
    public static bool IsInlineField(SerializedProperty prop, Object target = null)
    {
        var fieldType = ResolvePropertyType(prop, target);
        if (fieldType == null) return false;

        if (fieldType.IsArray)
            return IsSinglelineType(fieldType.GetElementType());

        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            return IsSinglelineType(fieldType.GetGenericArguments()[0]);

        return IsSinglelineType(fieldType);
    }

    /// <summary>类型（或其泛型定义 / 基类）是否标注了 [Singleline]。</summary>
    public static bool IsSinglelineType(Type type)
        => type != null && Attribute.IsDefined(type, typeof(SinglelineAttribute));

    //===============================//
    // 2. 反射工具（按 propertyPath 反解类型/字段）
    //===============================//

    /// <summary>由 SerializedProperty 反解其字段类型（支持 a.b、arr.Array.data[0].c 等路径）。</summary>
    public static Type ResolvePropertyType(SerializedProperty prop, Object target = null)
    {
        if (prop == null || prop.serializedObject == null) return null;
        var root = target != null ? target.GetType()
            : (prop.serializedObject.targetObject != null ? prop.serializedObject.targetObject.GetType() : null);
        return ResolvePropertyType(root, prop.propertyPath);
    }

    /// <summary>按 propertyPath 从根类型逐段下钻，返回末段字段类型。</summary>
    public static Type ResolvePropertyType(Type rootType, string propertyPath)
    {
        if (rootType == null || string.IsNullOrEmpty(propertyPath)) return null;

        Type current = rootType;
        foreach (string token in propertyPath.Split('.'))
        {
            if (current == null) return null;

            if (token == "Array")
            {
                // 数组/List 段：下钻到元素类型
                if (current.IsArray) current = current.GetElementType();
                else if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(List<>))
                    current = current.GetGenericArguments()[0];
                else return null;
                continue;
            }

            // data[0] 只是下标段，不改变类型
            if (token.StartsWith("data[", StringComparison.Ordinal)) continue;

            var field = FindField(current, token);
            if (field == null) return null;
            current = field.FieldType;
        }
        return current;
    }

    /// <summary>反解 SerializedProperty 对应的 FieldInfo（数组元素自身返回 null）。</summary>
    public static FieldInfo ResolveField(SerializedProperty prop)
    {
        if (prop == null || prop.serializedObject == null) return null;

        string path = prop.propertyPath;
        int lastDot = path.LastIndexOf('.');
        string name = lastDot >= 0 ? path.Substring(lastDot + 1) : path;
        if (name.StartsWith("data[", StringComparison.Ordinal)) return null;

        var rootType = RootType(prop);
        string parentPath = lastDot >= 0 ? path.Substring(0, lastDot) : "";
        // 顶层字段没有父路径，直接以根类型作为声明类型
        var parentType = string.IsNullOrEmpty(parentPath) ? rootType : ResolvePropertyType(rootType, parentPath);
        return FindField(parentType, name);
    }

    /// <summary>在类型及其基类链中查找字段（含私有）。</summary>
    public static FieldInfo FindField(Type type, string name)
    {
        while (type != null && type != typeof(object))
        {
            var field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
            type = type.BaseType;
        }
        return null;
    }

    private static Type RootType(SerializedProperty prop)
        => prop != null && prop.serializedObject != null && prop.serializedObject.targetObject != null
            ? prop.serializedObject.targetObject.GetType()
            : null;

    //===============================//
    // 3. 标签与子字段
    //===============================//

    /// <summary>字段显示名：[InspectorName] &gt; fallback &gt; NicifyVariableName。</summary>
    public static string GetFieldLabel(SerializedProperty prop, string fallback = null)
    {
        if (prop == null) return fallback;

        var field = ResolveField(prop);
        if (field != null)
        {
            var attr = Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) as InspectorNameAttribute;
            if (attr != null) return attr.displayName;
        }
        return fallback ?? ObjectNames.NicifyVariableName(prop.name);
    }

    /// <summary>子字段显示名：[InspectorName] &gt; displayName。</summary>
    public static string GetChildLabel(SerializedProperty child)
    {
        if (child == null) return "";
        var field = ResolveField(child);
        if (field != null)
        {
            var attr = Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) as InspectorNameAttribute;
            if (attr != null) return attr.displayName;
        }
        return child.displayName;
    }

    /// <summary>取对象的可见直接子字段（已过滤 m_Script）。</summary>
    public static List<SerializedProperty> GetVisibleChildren(SerializedProperty prop)
    {
        var children = new List<SerializedProperty>();
        if (prop == null) return children;

        var iterator = prop.Copy();
        var endProperty = prop.GetEndProperty();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            if (SerializedProperty.EqualContents(iterator, endProperty)) break;
            if (iterator.name == "m_Script") continue;
            children.Add(iterator.Copy());
            enterChildren = false;
        }
        return children;
    }

    //===============================//
    // 4. Layout 版（Editor / EditorWindow）
    //===============================//

    /// <summary>绘制内联单行对象：字段名 + 所有子字段横向平铺（Layout 版）。</summary>
    public static void DrawInlineObjectLayout(SerializedProperty prop, Object target = null, string label = null)
    {
        if (prop == null) return;

        var childProps = GetVisibleChildren(prop);
        if (childProps.Count == 0)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label ?? GetFieldLabel(prop)), true);
            return;
        }

        EditorGUILayout.BeginHorizontal();

        string fieldLabel = label ?? GetFieldLabel(prop);
        var labelContent = new GUIContent(fieldLabel);
        float labelWidth = EditorStyles.boldLabel.CalcSize(labelContent).x + 8;
        GUILayout.Label(labelContent, EditorStyles.label, GUILayout.Width(labelWidth));
        GUILayout.Space(2);

        float originalLabelWidth = EditorGUIUtility.labelWidth;
        foreach (var child in childProps)
        {
            string shortLabel = GetChildLabel(child);
            EditorGUIUtility.labelWidth = EditorStyles.miniLabel.CalcSize(new GUIContent(shortLabel)).x + 4;
            EditorGUILayout.PropertyField(child, new GUIContent(shortLabel), GUILayout.ExpandWidth(true));
        }
        EditorGUIUtility.labelWidth = originalLabelWidth;

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制内联数组/List（Layout 版）：自绘头部（折叠 + 标签 + Size 输入框）+ ReorderableList
    /// （支持拖拽重排、元素单行内联、元素/头部右键菜单）。
    /// </summary>
    public static void DrawInlineListLayout(SerializedProperty prop, Object target = null, string label = null)
    {
        if (prop == null) return;

        string fieldLabel = label ?? GetFieldLabel(prop);

        // ===== 头部（始终显示，不受折叠影响）=====
        Rect headerRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
        const float sizeWidth = 40f;

        Rect foldoutRect = new Rect(headerRect.x, headerRect.y, 30f, headerRect.height);
        Rect labelRect = new Rect(foldoutRect.x + 30f, headerRect.y, headerRect.width - 30f - sizeWidth, headerRect.height);
        Rect sizeRect = new Rect(headerRect.x + headerRect.width - sizeWidth, headerRect.y, sizeWidth, headerRect.height);

        // 悬停高亮（数量输入框有自己的高亮，排除）
        if (Event.current.type == EventType.Repaint
            && headerRect.Contains(Event.current.mousePosition)
            && !sizeRect.Contains(Event.current.mousePosition))
        {
            Color hoverColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.045f)
                : new Color(0f, 0f, 0f, 0.06f);
            EditorGUI.DrawRect(headerRect, hoverColor);
        }

        bool isExpanded = EditorGUI.Foldout(foldoutRect, prop.isExpanded, GUIContent.none, true);
        if (isExpanded != prop.isExpanded) prop.isExpanded = isExpanded;

        EditorGUI.LabelField(labelRect, fieldLabel, EditorStyles.boldLabel);

        // 头部右键：复制/粘贴整个数组
        if (Event.current.type == EventType.ContextClick && headerRect.Contains(Event.current.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("复制数组"), false, () => CopyArrayToClipboard(prop));
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

        int newSize = EditorGUI.IntField(sizeRect, prop.arraySize);
        if (newSize != prop.arraySize && newSize >= 0) prop.arraySize = newSize;

        // 整行点击切换折叠（放在 IntField/Foldout 之后，被消费的点击不会重复触发）
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0
            && headerRect.Contains(Event.current.mousePosition))
        {
            prop.isExpanded = !prop.isExpanded;
            Event.current.Use();
        }

        if (!prop.isExpanded) return;

        // ===== 列表体（ReorderableList，头部已自绘）=====
        string listKey = ListKey(prop);
        if (!ListCache.TryGetValue(listKey, out var list) || list == null
            || list.serializedProperty == null || list.serializedProperty.serializedObject != prop.serializedObject)
        {
            list = new ReorderableList(prop.serializedObject, prop, true, false, true, true)
            {
                drawHeaderCallback = rect => { /* 头部已手动绘制 */ },
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    if (index >= prop.arraySize) return;

                    // 元素右键上下文菜单
                    if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
                    {
                        var menu = new GenericMenu();
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

                    DrawElementRow(rect, prop, index, LayoutRowStyle);
                },
                elementHeight = EditorGUIUtility.singleLineHeight
            };
            ListCache[listKey] = list;
        }

        list.DoLayoutList();
    }

    /// <summary>按注册表补画字段上方的 DecoratorDrawer 型特性（自绘头部时 Unity 不会自动画）。</summary>
    public static void DrawDecorators(SerializedProperty prop, Object target)
    {
        if (prop == null || target == null) return;

        var fieldInfo = target.GetType().GetField(prop.name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fieldInfo == null) return;

        foreach (var attr in fieldInfo.GetCustomAttributes(typeof(PropertyAttribute), true))
        {
            var drawer = DecoratorDrawerCache.Bind(attr.GetType(), (PropertyAttribute)attr);
            if (drawer == null) continue;
            Rect rect = GUILayoutUtility.GetRect(0, drawer.GetHeight(), GUILayout.ExpandWidth(true));
            drawer.OnGUI(rect);
        }
    }

    /// <summary>
    /// DecoratorDrawer 注册表：按特性类型缓存 DecoratorDrawer 实例。
    /// Unity 没有公开"根据特性获取 DecoratorDrawer"的 API，这里在首次访问时扫描
    /// 所有 DecoratorDrawer 子类的 [CustomPropertyDrawer] 自建映射（纯公开 API）。
    /// 新的 Decorator 型特性只要写了 Drawer 类，即自动加入本机制。
    /// </summary>
    private static class DecoratorDrawerCache
    {
        private static readonly Dictionary<Type, DecoratorDrawer> Drawers = new Dictionary<Type, DecoratorDrawer>();
        private static bool _initialized;

        // 自建 Drawer 实例绕过了 Unity 的创建流程，Unity 不会注入 m_Attribute，
        // 必须在每次绘制前手动绑定实际特性实例，否则 Drawer 内取 attribute 会 NRE
        private static readonly FieldInfo AttributeField =
            typeof(DecoratorDrawer).GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);

        public static DecoratorDrawer Bind(Type attributeType, PropertyAttribute actualAttribute)
        {
            EnsureInit();
            if (Drawers.Count == 0) return null;
            if (!Drawers.TryGetValue(attributeType, out var drawer) || drawer == null) return null;
            AttributeField?.SetValue(drawer, actualAttribute);
            return drawer;
        }

        private static void EnsureInit()
        {
            if (_initialized) return;
            _initialized = true;
            if (AttributeField == null) return;

            foreach (var type in TypeCache.GetTypesDerivedFrom<DecoratorDrawer>())
            {
                // Unity 的特性类名为 CustomPropertyDrawer（无 Attribute 后缀），
                // 且其 GetHandledType() 为 internal，故通过公开的 GetCustomAttributesData() 读取构造参数
                foreach (var data in type.GetCustomAttributesData())
                {
                    if (data.AttributeType != typeof(CustomPropertyDrawer)) continue;

                    var attrType = data.ConstructorArguments.Count > 0
                        ? data.ConstructorArguments[0].Value as Type
                        : null;
                    if (attrType == null || Drawers.ContainsKey(attrType)) continue;

                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                        Drawers[attrType] = (DecoratorDrawer)Activator.CreateInstance(type);
                    break;
                }
            }
        }
    }

    //===============================//
    // 5. Rect 版（PropertyDrawer）
    //===============================//

    /// <summary>内联列表在 Rect 版下的总高度（与 DrawInlineListRect 严格对应）。</summary>
    public static float GetInlineListHeight(SerializedProperty listProp, InlineListStyle style = null)
    {
        if (listProp == null) return 0f;
        style ??= new InlineListStyle();

        float rowHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        float height = 0f;

        if (style.ShowHeader) height += rowHeight;

        if (IsExpanded(listProp, style))
        {
            height += listProp.arraySize * rowHeight;
            if (style.ShowFooterButtons) height += rowHeight;
            if (!string.IsNullOrEmpty(style.AddButtonText)) height += rowHeight;
        }

        height += style.ExtraBottomSpace;
        return height;
    }

    /// <summary>
    /// 绘制内联数组/List（Rect 版），返回绘制后的 y。
    /// 调用方需用 <see cref="GetInlineListHeight"/> 配套计算 GetPropertyHeight。
    /// </summary>
    public static float DrawInlineListRect(Rect position, SerializedProperty listProp, string label, float y, InlineListStyle style = null)
    {
        if (listProp == null) return y;
        style ??= new InlineListStyle();

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float rowHeight = lineHeight + EditorGUIUtility.standardVerticalSpacing;

        // ===== 头部 =====
        if (style.ShowHeader)
        {
            DrawHeaderRect(position, y, listProp, label, style);
            y += rowHeight;
        }

        if (!IsExpanded(listProp, style))
        {
            y += style.ExtraBottomSpace;
            return y;
        }

        // ===== 元素 =====
        for (int i = 0; i < listProp.arraySize; i++)
        {
            DrawElementRow(new Rect(position.x, y, position.width, lineHeight), listProp, i, style);
            y += rowHeight;
        }

        // ===== 底部按钮 =====
        if (style.ShowFooterButtons)
            y = DrawFooterButtons(position, y, listProp);

        if (!string.IsNullOrEmpty(style.AddButtonText))
            y = DrawAddButton(position, y, listProp, style.AddButtonText);

        y += style.ExtraBottomSpace;
        return y;
    }

    /// <summary>
    /// 绘制一行元素：可选"元素 i"前缀 + 各子字段等宽平铺内联。
    /// 元素行的右键菜单由 style.ShowContextMenu 控制。
    /// </summary>
    public static void DrawElementRow(Rect rowRect, SerializedProperty listProp, int index, InlineListStyle style)
    {
        if (listProp == null || index < 0 || index >= listProp.arraySize) return;
        style ??= new InlineListStyle();

        var element = listProp.GetArrayElementAtIndex(index);
        var children = GetVisibleChildren(element);

        int oldIndent = EditorGUI.indentLevel;
        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUI.indentLevel = 0;

        float x = rowRect.x;
        float width = rowRect.width;

        if (style.ShowElementLabel && children.Count > 0)
        {
            var elemLabel = new GUIContent(string.Format(style.ElementLabelFormat, index));
            float labelWidth = EditorStyles.label.CalcSize(elemLabel).x + style.ElementLabelExtraWidth;
            EditorGUI.LabelField(new Rect(x, rowRect.y, labelWidth, rowRect.height), elemLabel);
            x += labelWidth;
            width -= labelWidth;
        }

        if (children.Count == 0)
        {
            EditorGUI.PropertyField(new Rect(x, rowRect.y, width, rowRect.height), element, GUIContent.none);
        }
        else
        {
            float fieldWidth = width / children.Count;
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var slot = new Rect(x, rowRect.y, fieldWidth, rowRect.height);

                if (style.ChildDrawer != null)
                {
                    style.ChildDrawer(slot, child);
                }
                else
                {
                    string childLabel = style.ChildLabelProvider != null
                        ? style.ChildLabelProvider(child)
                        : GetChildLabel(child);
                    float childLabelWidth = EditorStyles.label.CalcSize(new GUIContent(childLabel)).x + 4f;
                    // 标签与字段分开绘制，避免 PrefixLabel 两行回退
                    EditorGUI.LabelField(new Rect(slot.x, slot.y, childLabelWidth, slot.height), childLabel);
                    EditorGUI.PropertyField(
                        new Rect(slot.x + childLabelWidth, slot.y, slot.width - childLabelWidth, slot.height),
                        child, GUIContent.none);
                }
                x += fieldWidth;
            }
        }

        EditorGUIUtility.labelWidth = originalLabelWidth;
        EditorGUI.indentLevel = oldIndent;

        // 元素右键菜单
        if (style.ShowContextMenu && Event.current.type == EventType.ContextClick
            && rowRect.Contains(Event.current.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("复制"), false, () =>
            {
                listProp.InsertArrayElementAtIndex(index);
                listProp.serializedObject.ApplyModifiedProperties();
            });
            menu.AddItem(new GUIContent("删除"), false, () =>
            {
                listProp.DeleteArrayElementAtIndex(index);
                listProp.serializedObject.ApplyModifiedProperties();
            });
            menu.ShowAsContext();
            Event.current.Use();
        }
    }

    /// <summary>底部 - / + 按钮（贴近 Unity 原版 list），返回绘制后的 y。</summary>
    public static float DrawFooterButtons(Rect position, float y, SerializedProperty listProp)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        const float buttonWidth = 24f;
        const float buttonGap = 2f;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var minusRect = new Rect(position.x + position.width - buttonWidth, y, buttonWidth, lineHeight);
        var plusRect = new Rect(position.x + position.width - buttonWidth * 2 - buttonGap, y, buttonWidth, lineHeight);

        if (GUI.Button(minusRect, "-", EditorStyles.miniButtonLeft) && listProp.arraySize > 0)
        {
            listProp.DeleteArrayElementAtIndex(listProp.arraySize - 1);
            listProp.serializedObject.ApplyModifiedProperties();
        }
        if (GUI.Button(plusRect, "+", EditorStyles.miniButtonRight))
        {
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.indentLevel = oldIndent;
        return y + lineHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    /// <summary>底部右对齐的"添加"按钮，返回绘制后的 y。</summary>
    public static float DrawAddButton(Rect position, float y, SerializedProperty listProp, string text)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        const float buttonWidth = 80f;

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        if (GUI.Button(new Rect(position.x + position.width - buttonWidth, y, buttonWidth, lineHeight), text))
        {
            listProp.arraySize++;
            listProp.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.indentLevel = oldIndent;
        return y + lineHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    private static bool IsExpanded(SerializedProperty listProp, InlineListStyle style)
        => !style.Collapsible || listProp.isExpanded;

    /// <summary>Rect 版头部：折叠箭头 + 标签 + 右侧 Size 输入框（保持与 Unity 原版 list 一致的观感）。</summary>
    private static void DrawHeaderRect(Rect position, float y, SerializedProperty listProp, string label, InlineListStyle style)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        const float sizeLabelWidth = 40f;
        const float sizeFieldWidth = 40f;
        const float sizeGap = 4f;

        var headerRect = new Rect(position.x - sizeLabelWidth, y, position.width, lineHeight);
        var sizeRect = new Rect(headerRect.x + headerRect.width, headerRect.y, sizeFieldWidth, lineHeight);
        var foldRect = new Rect(headerRect.x, headerRect.y,
            headerRect.width - sizeFieldWidth - sizeLabelWidth - sizeGap, lineHeight);

        int oldIndent = EditorGUI.indentLevel;
        bool expanded = IsExpanded(listProp, style);

        if (style.Collapsible)
        {
            GUIStyle foldStyle = style.BoldHeaderLabel
                ? new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold }
                : EditorStyles.foldout;

            // foldout 需要落在缩进区域内，保证与其他字段左对齐
            float indent = EditorGUI.IndentedRect(foldRect).x - foldRect.x;
            var foldDrawRect = new Rect(foldRect.x + indent, foldRect.y, foldRect.width - indent, lineHeight);

            expanded = EditorGUI.Foldout(foldDrawRect, listProp.isExpanded, new GUIContent(label), true, foldStyle);
            if (expanded != listProp.isExpanded) listProp.isExpanded = expanded;
        }
        else
        {
            EditorGUI.LabelField(foldRect, label,
                style.BoldHeaderLabel ? EditorStyles.boldLabel : EditorStyles.label);
        }

        EditorGUI.indentLevel = 0;
        int newSize = EditorGUI.IntField(sizeRect, listProp.arraySize);
        if (newSize != listProp.arraySize && newSize >= 0) listProp.arraySize = newSize;
        EditorGUI.indentLevel = oldIndent;

        // 头部右键：清空数组
        if (style.ShowContextMenu && Event.current.type == EventType.ContextClick
            && headerRect.Contains(Event.current.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("清空数组"), false, () =>
            {
                listProp.ClearArray();
                listProp.serializedObject.ApplyModifiedProperties();
            });
            menu.ShowAsContext();
            Event.current.Use();
        }
    }

    //===============================//
    // 6. 数组复制/粘贴（JsonUtility 剪贴板）
    //===============================//

    /// <summary>将数组内容按 JSON 写入系统剪贴板。</summary>
    public static void CopyArrayToClipboard(SerializedProperty prop)
    {
        if (prop == null) return;

        var items = new List<string>();
        for (int i = 0; i < prop.arraySize; i++)
            items.Add(SerializedPropertyToJson(prop.GetArrayElementAtIndex(i)));

        EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(new JsonArrayWrapper { items = items.ToArray() });
    }

    /// <summary>剪贴板中是否有可粘贴的数组数据。</summary>
    public static bool CanPasteArray(SerializedProperty prop)
    {
        if (string.IsNullOrEmpty(EditorGUIUtility.systemCopyBuffer)) return false;
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

    /// <summary>从剪贴板 JSON 覆盖写入数组。</summary>
    public static void PasteArrayFromClipboard(SerializedProperty prop)
    {
        if (prop == null) return;

        var wrapper = JsonUtility.FromJson<JsonArrayWrapper>(EditorGUIUtility.systemCopyBuffer);
        if (wrapper.items == null) return;

        prop.ClearArray();
        prop.arraySize = wrapper.items.Length;
        for (int i = 0; i < wrapper.items.Length; i++)
            JsonToSerializedProperty(prop.GetArrayElementAtIndex(i), wrapper.items[i]);
    }

    private static string SerializedPropertyToJson(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer: return JsonUtility.ToJson(new JsonValue<int> { value = prop.intValue });
            case SerializedPropertyType.Float: return JsonUtility.ToJson(new JsonValue<float> { value = prop.floatValue });
            case SerializedPropertyType.Boolean: return JsonUtility.ToJson(new JsonValue<bool> { value = prop.boolValue });
            case SerializedPropertyType.String: return JsonUtility.ToJson(new JsonValue<string> { value = prop.stringValue });
            case SerializedPropertyType.Color: return JsonUtility.ToJson(new JsonValue<Color> { value = prop.colorValue });
            case SerializedPropertyType.Vector2: return JsonUtility.ToJson(new JsonValue<Vector2> { value = prop.vector2Value });
            case SerializedPropertyType.Vector3: return JsonUtility.ToJson(new JsonValue<Vector3> { value = prop.vector3Value });
            case SerializedPropertyType.Vector4: return JsonUtility.ToJson(new JsonValue<Vector4> { value = prop.vector4Value });
            case SerializedPropertyType.Vector2Int: return JsonUtility.ToJson(new JsonValue<Vector2Int> { value = prop.vector2IntValue });
            case SerializedPropertyType.Vector3Int: return JsonUtility.ToJson(new JsonValue<Vector3Int> { value = prop.vector3IntValue });
            case SerializedPropertyType.Rect: return JsonUtility.ToJson(new JsonValue<Rect> { value = prop.rectValue });
            case SerializedPropertyType.RectInt: return JsonUtility.ToJson(new JsonValue<RectInt> { value = prop.rectIntValue });
            case SerializedPropertyType.Bounds: return JsonUtility.ToJson(new JsonValue<Bounds> { value = prop.boundsValue });
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
                    var entries = new List<JsonDictEntry>();
                    foreach (var child in GetVisibleChildren(prop))
                        entries.Add(new JsonDictEntry { key = child.name, value = SerializedPropertyToJson(child) });
                    return JsonUtility.ToJson(new JsonDictWrapper { entries = entries.ToArray() });
                }
        }
    }

    private static void JsonToSerializedProperty(SerializedProperty prop, string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer: prop.intValue = JsonUtility.FromJson<JsonValue<int>>(json).value; break;
            case SerializedPropertyType.Float: prop.floatValue = JsonUtility.FromJson<JsonValue<float>>(json).value; break;
            case SerializedPropertyType.Boolean: prop.boolValue = JsonUtility.FromJson<JsonValue<bool>>(json).value; break;
            case SerializedPropertyType.String: prop.stringValue = JsonUtility.FromJson<JsonValue<string>>(json).value; break;
            case SerializedPropertyType.Color: prop.colorValue = JsonUtility.FromJson<JsonValue<Color>>(json).value; break;
            case SerializedPropertyType.Vector2: prop.vector2Value = JsonUtility.FromJson<JsonValue<Vector2>>(json).value; break;
            case SerializedPropertyType.Vector3: prop.vector3Value = JsonUtility.FromJson<JsonValue<Vector3>>(json).value; break;
            case SerializedPropertyType.Vector4: prop.vector4Value = JsonUtility.FromJson<JsonValue<Vector4>>(json).value; break;
            case SerializedPropertyType.Vector2Int: prop.vector2IntValue = JsonUtility.FromJson<JsonValue<Vector2Int>>(json).value; break;
            case SerializedPropertyType.Vector3Int: prop.vector3IntValue = JsonUtility.FromJson<JsonValue<Vector3Int>>(json).value; break;
            case SerializedPropertyType.Rect: prop.rectValue = JsonUtility.FromJson<JsonValue<Rect>>(json).value; break;
            case SerializedPropertyType.RectInt: prop.rectIntValue = JsonUtility.FromJson<JsonValue<RectInt>>(json).value; break;
            case SerializedPropertyType.Bounds: prop.boundsValue = JsonUtility.FromJson<JsonValue<Bounds>>(json).value; break;
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
                    var wrapper = JsonUtility.FromJson<JsonDictWrapper>(json);
                    if (wrapper.entries == null) break;

                    var propCopy = prop.Copy();
                    var endProperty = prop.GetEndProperty();
                    bool enterChildren = true;
                    while (propCopy.NextVisible(enterChildren))
                    {
                        if (SerializedProperty.EqualContents(propCopy, endProperty)) break;
                        if (propCopy.name == "m_Script") continue;

                        for (int e = 0; e < wrapper.entries.Length; e++)
                        {
                            if (wrapper.entries[e].key != propCopy.name) continue;
                            JsonToSerializedProperty(propCopy, wrapper.entries[e].value);
                            break;
                        }
                        enterChildren = false;
                    }
                    break;
                }
        }
    }

    [Serializable]
    private struct JsonArrayWrapper { public string[] items; }
    [Serializable]
    private struct JsonValue<T> { public T value; }
    [Serializable]
    private struct JsonDictEntry { public string key; public string value; }
    [Serializable]
    private struct JsonDictWrapper { public JsonDictEntry[] entries; }

    //===============================//
    // 7. 内部工具
    //===============================//

    private static string ListKey(SerializedProperty prop)
    {
        var so = prop.serializedObject;
        int id = so != null && so.targetObject != null ? so.targetObject.GetInstanceID() : 0;
        return id + "|" + prop.propertyPath;
    }
}
