using System.Collections.Generic;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 消息窗：滚动显示系统播报（玩家倒地 / 发现任务点 / 加入·退出任务 …），到点自动消失、空则自隐。
///
/// <para>▍驱动：玩法层写 <c>GameMsg.*</c>（→ <c>GlobalEventBus.Msg</c> → <c>WndManager</c> → 契约层
/// <c>WindowRegistry.Msg</c> → 本窗 <see cref="Push"/>）；UI 层内可直接 <c>WndHub.Msg.Push(text)</c>。</para>
/// <para>▍文本走 TMP 富文本：颜色标签由调用方（<c>GameMsg</c>）给，本窗只负责排队与销毁。</para>
/// </summary>
[AddComponentMenu("UI/窗口/消息")]
public class MsgWnd : Window
{
    /// <summary>一条消息的运行时句柄。</summary>
    private sealed class Item
    {
        public Transform Root;
        /// <summary>到点时间（<c>Time.time</c>）即移除。</summary>
        public float Expire;
    }

    [SerializeField]
    [InspectorName("消息行模板")]
    [Tooltip("一条消息的克隆源；运行时保持关闭（VerticalLayoutGroup 忽略 inactive，不会占位）")]
    private Transform _itemTemplate;

    [SerializeField]
    [InspectorName("消息容器")]
    [Tooltip("行挂到哪个节点下；留空 = 本节点（根上的 VerticalLayoutGroup 负责排列）")]
    private Transform _listRoot;

    [SerializeField]
    [InspectorName("停留时间(秒)")]
    private float _lifeTime = 6f;

    [SerializeField]
    [InspectorName("最多同时显示条数")]
    private int _maxCount = 8;

    /// <summary>显示中的消息（按加入顺序）。</summary>
    private readonly List<Item> _items = new List<Item>();

    /// <summary>推一条消息（外部主入口，契约层 <c>WindowRegistry.Msg</c> 转调的就是它）。空串静默忽略。</summary>
    public void Push(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (_itemTemplate == null)
        {
            Debug.LogWarning($"[MsgWnd] 未配置「消息行模板」，消息被丢弃：{text}", this);
            return;
        }

        SetWndState(true);   // 先开窗：首次显示时基类会回调 FirstShowWnd

        var root = _listRoot ? _listRoot : transform;
        var row = Instantiate(_itemTemplate, root);
        // ⚠ 从 inactive 模板克隆出来的行也是关的（会继承关闭状态）⇒ 必须显式打开，否则列表看着是空的
        row.gameObject.SetActive(true);
        SetText(row, text);

        _items.Add(new Item { Root = row, Expire = Time.time + Mathf.Max(0.1f, _lifeTime) });
        while (_maxCount > 1 && _items.Count > _maxCount) RemoveAt(0);   // 超上限挤掉最旧的

        Refresh();
    }

    /// <summary>清空全部消息（不隐藏窗口）。</summary>
    public void Clear()
    {
        for (var i = _items.Count - 1; i >= 0; --i) RemoveAt(i);
    }

    private void Update()
    {
        var now = Time.time;
        for (var i = _items.Count - 1; i >= 0; --i)
        {
            if (now >= _items[i].Expire) RemoveAt(i);
        }
        if (_items.Count == 0 && State) SetWndState(false);   // 空则自隐
    }

    private void RemoveAt(int index)
    {
        var item = _items[index];
        _items.RemoveAt(index);
        if (item.Root) Tool.Destroy(item.Root.gameObject);
    }

    /// <summary>
    /// 列表改动后刷一次：末尾那次 <c>Canvas.ForceUpdateCanvases</c> 不能省
    /// （重排会把图元标脏，不刷则新克隆的行要等下一帧才画出来）。
    /// </summary>
    private void Refresh()
    {
        Canvas.ForceUpdateCanvases();
        RefreshLayoutImmediate(_listRoot ? _listRoot : transform);
        Canvas.ForceUpdateCanvases();
    }

    protected override void FirstShowWnd()
    {
        // 行模板只是克隆源：确保它是关的，否则会以"预览行"的身份占在列表里
        if (_itemTemplate) SetActive(_itemTemplate, false);
    }

    protected override void ShowWnd() { }

    protected override void HideWnd() => Clear();
}
}
