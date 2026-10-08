using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Game;
using UnityEngine;
using FPSGame.Utils;
using FPSGame.Gameplay;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;

/// <summary>
/// 玩家欧帕兹携带背包列表 UI。
/// 用 Layout 列表式显示玩家当前携带的各类型欧帕兹及数量（上限 5）。
/// 事件驱动刷新：仅在玩家拾取/交付时更新列表，并显示 HUD；
/// 停止变化 5 秒后淡出隐藏。
/// 列表机制（容器/行项/停留淡出）继承自 <see cref="HoldListHud"/>。
/// </summary>
[AddComponentMenu("UI/HUD/欧帕兹背包")]
public class OOPartBagWnd : HoldListHud
{
    private PlayerOOPartInventory m_Bag;

    //检视器调用
    public void Init() 
    {
        UnitEventBus.OnPlayerCreate += SetPlayer;
        HideAtStart();
    }

    private void SetPlayer(IActor actor)
    {
        m_Bag = actor.transform.GetComponent<PlayerOOPartInventory>();
        if (m_Bag != null) m_Bag.OnChanged += OnBagChanged;
    }

    private void OnDestroy()
    {
        UnitEventBus.OnPlayerCreate -= SetPlayer;
        if (m_Bag != null) m_Bag.OnChanged -= OnBagChanged;
    }

    /// <summary>背包变化：刷新列表并重新计时显示</summary>
    private void OnBagChanged(OOPartEnum type, int delta)
    {
        RefreshAndShow();
    }

    /// <summary>重建列表，与玩家当前携带量同步</summary>
    protected override void RefreshItems()
    {
        if (m_Bag == null || listRoot == null) return;

        ClearItems();
        foreach (var kvp in m_Bag.GetAll())
        {
            int count = kvp.Value;
            if (count <= 0) continue;

            var go = CreatItem();
            SetSprite(go.GetChild(0), PropertyManager.Instance.GetIcon(kvp.Key));
            SetText(go.GetChild(1), PropertyManager.Instance.GetName(kvp.Key));
            SetText(go.GetChild(2), Tool.FillZero(count, 2));
            SetFill(go.GetChild(3), (count + 0f) / MaxCapacity);
        }
    }

}
}
