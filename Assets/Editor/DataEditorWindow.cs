using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class DataEditorWindow : EditorWindow
{
    private TabType _currentTab = TabType.Mission;

    private readonly Dictionary<TabType, IDataTab> _tabs = new Dictionary<TabType, IDataTab>();

    // Shared cached editor
    private UnityEditor.Editor _cachedEditor;

    [MenuItem("Tools/数据编辑器")]
    private static void Open()
    {
        var window = GetWindow<DataEditorWindow>();
        window.titleContent = new GUIContent("数据编辑器");
        window.Show();
    }

    private IDataTab Current => _tabs[_currentTab];

    private void OnEnable()
    {
        RegisterTabs();
        RefreshAll();
    }

    private void OnDisable()
    {
        _cachedEditor = null;
    }

    /// <summary>注册所有 Tab 模块</summary>
    private void RegisterTabs()
    {
        _tabs.Clear();
        AddTab(new MissionTabModule(this));
        AddTab(new CampTabModule(this));
        AddTab(new AirdropTabModule(this));
        AddTab(new MapTabModule(this));
        AddTab(new UpdateTabModule(this));
        AddTab(new WeaponModuleTabModule(this));
        AddTab(new WeaponUpgradeTabModule(this));
        AddTab(new AboStateTabModule(this));
        AddTab(new WeaponTabModule(this));
        AddTab(new RoleTabModule(this));
        AddTab(new BoosterTabModule(this));
    }

    private void AddTab(IDataTab tab) => _tabs[tab.TabType] = tab;

    private void RefreshAll()
    {
        DestroyCachedEditor();
        foreach (var tab in _tabs.Values)
            tab.Refresh();
    }

    /// <summary>重置 GUI 焦点，避免切换项后输入框仍保持焦点</summary>
    public static void ResetFocus()
    {
        GUIUtility.hotControl = 0;
        GUIUtility.keyboardControl = 0;
        EditorGUIUtility.editingTextField = false;
    }

    #region Cached Editor Management

    public bool HasCachedEditor => _cachedEditor != null;

    public void SetCachedEditor(Object target)
    {
        DestroyCachedEditor();
        _cachedEditor = target != null ? UnityEditor.Editor.CreateEditor(target) : null;
    }

    public void DestroyCachedEditor()
    {
        if (_cachedEditor == null) return;

        var editor = _cachedEditor;
        _cachedEditor = null;

        // 检查 serializedObject 是否仍有效，防止销毁已释放对象导致异常
        if (editor.serializedObject != null && editor.target != null)
        {
            try { DestroyImmediate(editor); }
            catch (Exception) { /* 忽略销毁时的异常 */ }
        }
    }

    /// <summary>绘制缓存 editor 的 inspector，并处理修改保存</summary>
    public void DrawCachedInspector()
    {
        EditorGUI.BeginChangeCheck();
        _cachedEditor.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck() && _cachedEditor.serializedObject != null && _cachedEditor.serializedObject.hasModifiedProperties)
        {
            _cachedEditor.serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>绘制选中项高亮背景</summary>
    public void DrawSelectionHighlight(Rect rowRect, bool isSelected)
    {
        if (isSelected)
            EditorGUI.DrawRect(new Rect(0, rowRect.y, position.width * 0.35f, rowRect.height), new Color(0.3f, 0.5f, 1f, 0.2f));
    }

    #endregion

    #region GUI

    private void OnGUI()
    {
        HandleKeyboardNavigation();

        DrawToolbar();
        DrawTabs();

        EditorGUILayout.BeginHorizontal();
        Current.DrawLeftPanel(DrawLocateButton);
        DrawResizeHandle();
        Current.DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            RefreshAll();

        GUILayout.Space(10);
        Current.SearchFilter = EditorGUILayout.TextField(Current.SearchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);

        foreach (var tab in _tabs.Values)
            DrawTabButton(tab.DisplayName, tab.TabType);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTabButton(string label, TabType tab)
    {
        bool selected = _currentTab == tab;
        if (GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(100)) != selected)
        {
            _currentTab = tab;
            DestroyCachedEditor();
            ResetFocus();
        }
    }

    #endregion

    #region Keyboard Navigation

    /// <summary>处理键盘导航：WS/↑↓移动选择项，AD/←→切换Tab</summary>
    private void HandleKeyboardNavigation()
    {
        Event e = Event.current;
        if (e.type != EventType.KeyDown) return;
        // 文本输入框正在编辑时不拦截按键，避免影响输入
        if (EditorGUIUtility.editingTextField) return;

        bool handled = false;
        if (e.keyCode == KeyCode.W || e.keyCode == KeyCode.UpArrow)
        {
            Current.MoveSelection(-1);
            handled = true;
        }
        else if (e.keyCode == KeyCode.S || e.keyCode == KeyCode.DownArrow)
        {
            Current.MoveSelection(1);
            handled = true;
        }
        else if (e.keyCode == KeyCode.A || e.keyCode == KeyCode.LeftArrow)
        {
            CycleTab(-1);
            handled = true;
        }
        else if (e.keyCode == KeyCode.D || e.keyCode == KeyCode.RightArrow)
        {
            CycleTab(1);
            handled = true;
        }

        if (handled)
            e.Use();
    }

    /// <summary>循环切换 TabType（左上角的类型选择）</summary>
    private void CycleTab(int direction)
    {
        var values = (TabType[])Enum.GetValues(typeof(TabType));
        int count = values.Length;
        int current = Array.IndexOf(values, _currentTab);
        int next = (current + direction + count) % count;
        _currentTab = values[next];
        DestroyCachedEditor();
        ResetFocus();
        Repaint();
    }

    #endregion

    #region Helpers

    private void PingSelectedFile()
    {
        string path = Current.GetSelectedAssetPath();
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("提示", "请先选择一个数据项", "确定");
            return;
        }
        Object fileObj = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (fileObj != null)
        {
            EditorGUIUtility.PingObject(fileObj);
            Selection.activeObject = fileObj;
        }
    }

    private void DrawLocateButton()
    {
        if (GUILayout.Button("定位到选中文件", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            PingSelectedFile();
    }

    private void DrawResizeHandle()
    {
        GUILayout.Box("", GUILayout.Width(5), GUILayout.ExpandHeight(true));
        EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.ResizeHorizontal);
    }

    /// <summary>创建一个在所有状态下保持颜色的 GUIStyle</summary>
    public static GUIStyle ColoredLabel(GUIStyle baseStyle, Color color)
    {
        var style = new GUIStyle(baseStyle);
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        style.onNormal.textColor = color;
        style.onHover.textColor = color;
        style.onActive.textColor = color;
        style.onFocused.textColor = color;
        return style;
    }

    public static string GetEnumLabel(MissionEnum value)
    {
        var fieldInfo = typeof(MissionEnum).GetField(value.ToString());
        if (fieldInfo == null) return value.ToString();

        var customAttrs = fieldInfo.GetCustomAttributes(false);
        foreach (var attr in customAttrs)
        {
            Type attrType = attr.GetType();
            if (attrType.Name == "CustomLabelAttribute")
            {
                var nameProp = attrType.GetField("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp != null)
                {
                    string name = nameProp.GetValue(attr) as string;
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
        }

        var inspectorAttr = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
        if (inspectorAttr.Length > 0)
            return (inspectorAttr[0] as InspectorNameAttribute)?.displayName ?? value.ToString();

        return value.ToString();
    }

    public static string GetInspectorName<TEnum>(TEnum value) where TEnum : Enum
    {
        var fieldInfo = typeof(TEnum).GetField(value.ToString());
        if (fieldInfo == null) return value.ToString();

        var inspectorAttr = fieldInfo.GetCustomAttributes(typeof(InspectorNameAttribute), false);
        if (inspectorAttr.Length > 0)
            return (inspectorAttr[0] as InspectorNameAttribute)?.displayName ?? value.ToString();

        return value.ToString();
    }

    #endregion
}
