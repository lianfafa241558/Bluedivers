using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>数据 Tab 模块的泛型抽象基类，封装所有公共样板逻辑</summary>
/// <typeparam name="TData">数据资产类型（ScriptableObject 或 GameObject 等 UnityEngine.Object 子类）</typeparam>
public abstract class DataTabModule<TData> : IDataTab 
    where TData : Object
{
    protected readonly DataEditorWindow Host;
    protected readonly List<TData> Items = new List<TData>();
    protected TData Selected;

    /// <summary>可选的 label 缓存，供需要反射计算标签的子类使用（如 Mission）</summary>
    protected readonly Dictionary<TData, string> LabelCache = new Dictionary<TData, string>();

    protected DataTabModule(DataEditorWindow host)
    {
        Host = host;
    }

    public abstract TabType TabType { get; }
    public abstract string DisplayName { get; }
    public int Count => Items.Count;
    public bool HasSelection => Selected != null;
    public string SearchFilter { get; set; } = "";
    public Vector2 LeftScroll { get; set; }
    public Vector2 RightScroll { get; set; }

    // —— 数据加载扩展点 ——
    protected abstract string RootPath { get; }
    protected abstract string TypeName { get; }
    /// <summary>是否包含子目录搜索，默认 false</summary>
    protected virtual bool IncludeSubDirs => false;
    /// <summary>单项加载完成后的钩子（如填充 LabelCache）</summary>
    protected virtual void OnItemLoaded(TData data) { }
    /// <summary>列表排序比较器，返回 null 表示不排序</summary>
    protected abstract Comparison<TData> SortComparison { get; }

    // —— 子类需实现的"差异点" ——
    protected abstract List<TData> FilterItems(List<TData> items);
    protected abstract void DrawListItemContent(TData data, bool isSelected);
    protected abstract string GetEmptyMessage();
    protected abstract string GetSelectedTitle();

    /// <summary>右侧标题样式，默认粗体；子类可重写以应用颜色</summary>
    protected virtual GUIStyle GetSelectedTitleStyle() => EditorStyles.boldLabel;

    /// <summary>右侧标题下方的额外信息（如资产路径、主线/支线标识）</summary>
    protected virtual void DrawRightPanelExtra() { }

    // —— 公共逻辑（只写一次） ——

    public void Refresh()
    {
        LabelCache.Clear();
        RefreshData();
    }

    /// <summary>数据加载模板：路径校验 → 搜索资产 → 加载 → 钩子 → 排序</summary>
    protected virtual void RefreshData()
    {
        Items.Clear();
        if (!AssetDatabase.IsValidFolder(RootPath))
        {
            Debug.LogError($"路径不存在: {RootPath}");
            return;
        }

        string[] searchPaths = GetSearchPaths();
        string[] guids = AssetDatabase.FindAssets(TypeName, searchPaths);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TData data = AssetDatabase.LoadAssetAtPath<TData>(path);
            if (data == null) continue;
            Items.Add(data);
            OnItemLoaded(data);
        }

        var comparison = SortComparison;
        if (comparison != null)
            Items.Sort(comparison);
    }

    /// <summary>根据 IncludeSubDirs 构造搜索路径数组</summary>
    private string[] GetSearchPaths()
    {
        if (!IncludeSubDirs)
            return new[] { RootPath };

        string[] subDirs = AssetDatabase.GetSubFolders(RootPath);
        return new[] { RootPath }.Concat(subDirs).ToArray();
    }

    public void DrawLeftPanel(Action drawFooter)
    {
        float panelWidth = Mathf.Max(Host.position.width * 0.35f, 200);
        EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth));
        GUILayout.Label($"共 {Items.Count} 项", EditorStyles.boldLabel);
        LeftScroll = EditorGUILayout.BeginScrollView(LeftScroll, GUI.skin.box, GUILayout.ExpandHeight(true));

        var filtered = FilterItems(Items);
        foreach (var item in filtered)
            DrawItem(item);

        EditorGUILayout.EndScrollView();

        // Footer toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
        if (GUILayout.Button("刷新列表", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
        {
            Host.DestroyCachedEditor();
            Refresh();
        }
        drawFooter?.Invoke();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    /// <summary>绘制一个列表项：内容 + 点击选中 + 高亮（统一模板）</summary>
    private void DrawItem(TData data)
    {
        bool isSelected = IsSelected(data);
        DrawListItemContent(data, isSelected);

        var rowRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
        {
            OnSelect(data);
            Event.current.Use();
        }
        Host.DrawSelectionHighlight(rowRect, isSelected);
    }

    public void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

        if (!HasSelection || !Host.HasCachedEditor)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.HelpBox(GetEmptyMessage(), MessageType.Info);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            return;
        }

        RightScroll = EditorGUILayout.BeginScrollView(RightScroll);
        GUILayout.Label(GetSelectedTitle(), GetSelectedTitleStyle());
        DrawRightPanelExtra();
        EditorGUILayout.Space(5);

        Host.DrawCachedInspector();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    public void MoveSelection(int direction)
    {
        var filtered = FilterItems(Items);
        if (filtered.Count == 0) return;

        int idx = filtered.IndexOf(Selected);
        int newIdx;
        if (idx < 0)
            newIdx = direction > 0 ? 0 : filtered.Count - 1;
        else
            newIdx = Mathf.Clamp(idx + direction, 0, filtered.Count - 1);

        if (newIdx != idx)
            OnSelect(filtered[newIdx]);
    }

    public string GetSelectedAssetPath()
        => Selected != null ? AssetDatabase.GetAssetPath(Selected) : null;

    public Object GetSelectedData() => Selected;

    /// <summary>返回用于创建 Inspector Editor 的目标对象，默认即数据本身</summary>
    protected virtual Object GetEditorTarget(TData data) => data;

    /// <summary>选中某项：更新选中、创建缓存 editor、重置焦点、重绘</summary>
    protected void OnSelect(TData data)
    {
        Selected = data;
        Host.SetCachedEditor(GetEditorTarget(data));
        DataEditorWindow.ResetFocus();
        Host.Repaint();
    }

    /// <summary>判断指定项是否为当前选中项（引用相等）</summary>
    protected bool IsSelected(TData data) => Selected == data;
}
