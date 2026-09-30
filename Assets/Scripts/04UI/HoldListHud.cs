using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;

/// <summary>
/// 常驻 HUD 上「列表 + 变化后停留再淡出」的通用基类。
/// <para>
/// 统一提供：列表容器、行项预制件、停留时长、淡出时长四项配置，
/// 以及行项创建/清空、"数据变化 → 刷新并重置自动隐藏计时"的成套流程。
/// 子类只需实现 <see cref="RefreshItems"/>（各自的数据源与行项内容）。
/// </para>
/// <para>
/// 注意：配置字段名与子类原先一致，抽到基类后 Unity 仍按字段名匹配，prefab 上已配好的数据不受影响。
/// </para>
/// </summary>
public abstract class HoldListHud : MonoBehaviour
{
    /// <summary>列表容量上限（供行项填充条等显示使用）</summary>
    public const int MaxCapacity = 5;

    [SerializeField]
    [InspectorName("列表容器")]
    protected Transform listRoot;

    [SerializeField]
    [InspectorName("行项预制件")]
    protected GameObject itemPrefab;

    /// <summary>数据变化后停留时长（秒），随后淡出</summary>
    [SerializeField]
    [InspectorName("停留时长(秒)")]
    protected float holdDuration = 5f;

    /// <summary>淡出时长（毫秒）</summary>
    [SerializeField]
    [InspectorName("淡出时长(毫秒)")]
    protected int fadeOutMs = 500;

    protected readonly List<GameObject> items = new();
    private Coroutine m_HideCoroutine;

    /// <summary>初始状态：把 HUD 透明化（等待首次数据变化再显示）</summary>
    protected void HideAtStart()
    {
        SetAlpha(transform, 0);
    }

    /// <summary>数据变化时调用：刷新列表并重新计时显示</summary>
    protected void RefreshAndShow()
    {
        RefreshItems();
        ShowAndAutoHide();
    }

    /// <summary>子类实现：按各自数据源重建列表（用 <see cref="CreatItem"/> 创建行项）</summary>
    protected abstract void RefreshItems();

    /// <summary>显示 HUD 并重置自动隐藏计时</summary>
    private void ShowAndAutoHide()
    {
        if (m_HideCoroutine != null) StopCoroutine(m_HideCoroutine);

        SetAlpha(transform, 1f);
        SetActive(transform, true);
        m_HideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(holdDuration);
        SetAlpha(transform, 1f, 0f, fadeOutMs);
        SetActive(transform, false, fadeOutMs);
        m_HideCoroutine = null;
    }

    /// <summary>创建一个行项：置于列表末尾、激活并登记</summary>
    protected Transform CreatItem()
    {
        var go = Instantiate(itemPrefab, listRoot).transform;
        go.SetAsLastSibling();
        SetActive(go, true);
        items.Add(go.gameObject);
        return go;
    }

    /// <summary>清空当前所有行项</summary>
    protected void ClearItems()
    {
        foreach (var item in items)
        {
            if (item != null) Destroy(item);
        }
        items.Clear();
    }
}
}
