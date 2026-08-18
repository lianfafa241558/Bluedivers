using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

/// <summary>
/// 已提交给凯伊(Kei)的欧帕兹列表 UI。
/// 用 Layout 列表式显示本任务中已提交给凯伊的各类型欧帕兹累计数量。
/// 数据源为 <see cref="TaskManager.SelectTaskData.collectProperty"/>（仅 Kei 交付时累加）。
/// 事件驱动刷新：Kei 交付时刷新并显示 HUD；停止变化 5 秒后淡出隐藏。
/// </summary>
public class KeiSubmitWnd : MonoBehaviour
{
    /// <summary>携带上限</summary>
    public const int MaxCapacity = 5;

    [SerializeField]
    [InspectorName("列表容器")]
    private Transform listRoot;

    [SerializeField]
    [InspectorName("行项预制件")]
    private GameObject itemPrefab;

    /// <summary>交付后停留时长（秒），随后淡出</summary>
    [SerializeField]
    [InspectorName("停留时长(秒)")]
    private float holdDuration = 5f;

    /// <summary>淡出时长（毫秒）</summary>
    [SerializeField]
    [InspectorName("淡出时长(毫秒)")]
    private int fadeOutMs = 500;

    private readonly List<GameObject> items = new();
    private Coroutine m_HideCoroutine;

    private void Start()
    {
        GlobalEventSub.OnKeiSubmit += OnKeiSubmit;
        if (listRoot == null) return;
        // 初始隐藏，等待首次交付再显示
        SetAlpha(transform, 0);
    }

    private void OnDestroy()
    {
        GlobalEventSub.OnKeiSubmit -= OnKeiSubmit;
    }

    private void OnKeiSubmit(OOPartEnum type, int count)
    {
        Refresh();
        ShowAndAutoHide();
    }

    /// <summary>显示 HUD 并重置 5 秒后淡出</summary>
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

    /// <summary>重建列表，与任务已提交采集量同步</summary>
    private void Refresh()
    {
        if (listRoot == null || !TaskManager.Instance || TaskManager.Instance.nowTask == null) return;

        var collect = TaskManager.Instance.nowTask.collectProperty;
        if (collect == null || collect.Count == 0)
        {
            ClearItems();
            return;
        }

        ClearItems();
        foreach (var kvp in collect)
        {
            OOPartEnum type = kvp.Key;
            int count = kvp.Value;
            if (count <= 0) continue;

            var go = Instantiate(itemPrefab, listRoot).transform;
            go.SetAsLastSibling();
            SetActive(go, true);

            SetSprite(go.GetChild(0), PropertyManager.Instance.GetIcon(type));
            SetText(go.GetChild(1), PropertyManager.Instance.GetName(type));
            SetText(go.GetChild(2), Tool.FillZero(count,2));
            //SetFill(go.GetChild(3), (count+0f)/MaxCapacity);
            items.Add(go.gameObject);
        }
    }

    private void ClearItems()
    {
        foreach (var item in items)
        {
            if (item != null) Destroy(item);
        }
        items.Clear();
    }
}
