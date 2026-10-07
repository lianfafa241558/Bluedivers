using System.Collections.Generic;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 信息提示窗口：外部按 <b>string 键</b>驱动，一个键对应一条子项。
///
/// <para>▍语义（与调用方约定）：传入的 string **既是键、也是子项显示的文字**，键与子项一一对应：
/// <see cref="Add(string)"/> 添加一条（键没显示过就用 <see cref="_itemPrefab"/> 建一条并显示，已显示则只刷新文字）；
/// <see cref="Remove(string)"/> 移除一条（键不在显示中则什么都不做）。
/// 移除最后一条时窗口自动隐藏。</para>
///
/// <para>▍跨层调用（管理器 / 玩法层）：契约层 <c>WindowRegistry.Information.Add(string)</c> /
/// <c>WindowRegistry.Information.Remove(string)</c>（由 <c>WndHub</c> 登记，UI 未就绪时静默跳过）。
/// UI 层内部可直接 <c>WndHub.Information.Add(key)</c> / <c>WndHub.Information.Remove(key)</c>。</para>
/// </summary>
[AddComponentMenu("UI/窗口/信息提示")]
public class InformationWnd : Window
{
    /// <summary>一条信息子项的运行时句柄（实例 + 文本组件）</summary>
    private sealed class InfoItem
    {
        public Transform Root;
        public TMPro.TMP_Text Label;
    }

    [SerializeField]
    [InspectorName("子项预制体")]
    //[Tooltip("信息子项预制体（Resources/UI/Information/InformationItem.prefab）")]
    private GameObject _itemPrefab;

    [SerializeField]
    [InspectorName("子项容器")]
    //[Tooltip("子项挂载到哪个节点下；留空 = 本窗口根节点（根上的 VerticalLayoutGroup 负责排列）")]
    private Transform _listRoot;

    /// <summary>键 → 当前显示中的子项</summary>
    private readonly Dictionary<string, InfoItem> _items = new Dictionary<string, InfoItem>();

    /// <summary>当前显示中的信息条数</summary>
    public int Count => _items.Count;

    /// <summary>
    /// 添加一条信息并显示（键 = 子项显示的文字）。
    /// 该键已在显示中则不新建，只把那条的文字刷新成 <paramref name="key"/>。
    /// 这是给外部的主入口（契约层 <c>WindowRegistry.Information.Add</c> 转调的就是它）。
    /// </summary>
    public void Add(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        InfoItem item;
        if (_items.TryGetValue(key, out item))
        {
            if (item.Label) item.Label.text = key;
            return;
        }

        if (_itemPrefab == null)
        {
            Debug.LogError($"[InformationWnd] 未配置「子项预制体」，无法显示：{key}");
            return;
        }

        // 先开窗：首次显示时基类会回调 FirstShowWnd（清掉编辑器里留的预览子项）
        SetWndState(true);

        var root = Instantiate(_itemPrefab, _listRoot ? _listRoot : transform).transform;
        var label = root.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (label) label.text = key;

        _items.Add(key, new InfoItem { Root = root, Label = label });
    }

    /// <summary>
    /// 移除一条信息并销毁它的子项；该键不在显示中则什么都不做。
    /// 移除最后一条时窗口自动隐藏。
    /// 这是给外部的主入口（契约层 <c>WindowRegistry.Information.Remove</c> 转调的就是它）。
    /// </summary>
    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        InfoItem item;
        if (!_items.TryGetValue(key, out item)) return;

        _items.Remove(key);
        if (item.Root) Tool.Destroy(item.Root.gameObject);
        if (_items.Count == 0) SetWndState(false);
    }

    /// <summary>移除全部信息并隐藏窗口。</summary>
    public void Clear()
    {
        foreach (var item in _items.Values)
        {
            if (item.Root) Tool.Destroy(item.Root.gameObject);
        }
        _items.Clear();
        SetWndState(false);
    }

    /// <summary>该键当前是否显示中。</summary>
    public bool Contains(string key) => !string.IsNullOrEmpty(key) && _items.ContainsKey(key);

    protected override void FirstShowWnd()
    {
        // 编辑器里摆在窗口下的子项只是预览占位（运行时由「子项预制体」实例接管）⇒ 首次显示时收起
        var root = _listRoot ? _listRoot : transform;
        for (var i = 0; i < root.childCount; ++i) SetActive(root.GetChild(i), false);
    }

    protected override void ShowWnd() { }

    protected override void HideWnd() { }
}
}
