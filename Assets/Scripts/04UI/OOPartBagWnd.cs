using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
using Utils;
using static WndTools.WndRootTool;

/// <summary>
/// 玩家欧帕兹携带背包列表 UI。
/// 用 Layout 列表式显示玩家当前携带的各类型欧帕兹及数量（上限10）。
/// 事件驱动刷新：仅在玩家拾取/交付时更新列表，并显示 HUD；
/// 停止变化 5 秒后淡出隐藏。
/// 挂载在常驻 HUD 上，配合 LayoutGroup 使用。
/// </summary>
public class OOPartBagWnd : MonoBehaviour
{
    [SerializeField]
    [InspectorName("列表容器")]
    private Transform listRoot;

    [SerializeField]
    [InspectorName("行项预制件")]
    private GameObject itemPrefab;

    /// <summary>拾取后停留时长（秒），随后淡出</summary>
    [SerializeField]
    [InspectorName("停留时长(秒)")]
    private float holdDuration = 5f;

    /// <summary>淡出时长（毫秒）</summary>
    [SerializeField]
    [InspectorName("淡出时长(毫秒)")]
    private int fadeOutMs = 500;

    private PlayerOOPartInventory m_Bag;
    private readonly List<GameObject> items = new();
    private Coroutine m_HideCoroutine;

    private PlayerOOPartInventory Bag
    {
        get => m_Bag;
    }
    //检视器调用
    public void Init() 
    {
        GlobalEventSub.OnPlayerCreate += SetPlayer;
        SetAlpha(transform, 0);
    }

    private void SetPlayer(I_Actor actor)
    {
        m_Bag = actor.transform.GetComponent<PlayerOOPartInventory>();
        if (m_Bag != null) m_Bag.OnChanged += OnBagChanged;
    }

    private void OnDestroy()
    {
        GlobalEventSub.OnPlayerCreate -= SetPlayer;
        if (m_Bag != null) m_Bag.OnChanged -= OnBagChanged;
    }

    /// <summary>背包变化：刷新列表并重新计时显示</summary>
    private void OnBagChanged(OOPartEnum type, int delta)
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

    /// <summary>重建列表，与玩家当前携带量同步</summary>
    private void Refresh()
    {
        Debug.LogError("刷新拾取列表");
        var bag = Bag;
        if (bag == null || listRoot == null) return;

        // 清空旧的
        foreach (var item in items)
        {
            if (item != null) Destroy(item);
        }
        items.Clear();

        foreach (var kvp in bag.GetAll())
        {
            OOPartEnum type = kvp.Key;
            int count = kvp.Value;
            if (count <= 0) continue;

            var go = Instantiate(itemPrefab, listRoot).transform;
            go.SetAsLastSibling();
            SetActive(go, true);

            SetSprite(go.GetChild(0), PropertyManager.Instance.GetIcon(type));
            SetText(go.GetChild(1), PropertyManager.Instance.GetName(type));
            SetText(go.GetChild(2), Tool.FillZero(count, 2));
            items.Add(go.gameObject);
        }
    }

}
