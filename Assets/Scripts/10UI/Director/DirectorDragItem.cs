using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPSGame.UI
{
/// <summary>
/// 导演面板列表行的拖拽源：把条目从面板拖到场景地面放置。
///
/// <para>▍为什么要手动禁用 ScrollRect：行自己实现了 <see cref="IBeginDragHandler"/>，
/// 拖拽事件会被行消费、不再冒泡 ⇒ 不关掉父级 ScrollRect 的话，行不会跟着滚、也看不出手感问题。
/// 滚轮滚动不经过拖拽链路，禁用期间照样可用。</para>
/// </summary>
[AddComponentMenu("UI/导演拖拽条目")]
public class DirectorDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>条目数据（<c>DirectorUnitEntry</c> 或 <c>AirdropData_SO</c>），由面板注入。</summary>
    public object Data;

    public Action<DirectorDragItem, Vector2> DragBegan;
    public Action<DirectorDragItem, Vector2> Dragging;
    public Action<DirectorDragItem, Vector2> DragEnded;

    private ScrollRect _scroll;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _scroll = GetComponentInParent<ScrollRect>();
        if (_scroll != null) _scroll.enabled = false;
        DragBegan?.Invoke(this, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Dragging?.Invoke(this, eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_scroll != null)
        {
            _scroll.enabled = true;
            _scroll = null;
        }
        DragEnded?.Invoke(this, eventData.position);
    }
}
}
