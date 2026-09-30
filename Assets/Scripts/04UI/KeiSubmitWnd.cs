using FPSGame.Core;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.UI
{
using static FPSGame.WndTools.WndRootTool;
using FPSGame.Managers;
    using FPSGame.GameContract;
    using FPSGame.Gameplay;

    /// <summary>
    /// 已提交给凯伊(Kei)的欧帕兹列表 UI。
    /// 用 Layout 列表式显示本任务中已提交给凯伊的各类型欧帕兹累计数量。
    /// 数据源为 <see cref="TaskManager.SelectTaskData.collectProperty"/>（仅 Kei 交付时累加）。
    /// 事件驱动刷新：Kei 交付时刷新并显示 HUD；停止变化 5 秒后淡出隐藏。
    /// 列表机制（容器/行项/停留淡出）继承自 <see cref="HoldListHud"/>。
    /// </summary>
    [AddComponentMenu("UI/HUD/已交付欧帕兹")]
public class KeiSubmitWnd : HoldListHud
{
    private void Start()
    {
        GlobalEventSub.OnKeiSubmit += OnKeiSubmit;
        if (listRoot == null) return;
        // 初始隐藏，等待首次交付再显示
        HideAtStart();
    }

    private void OnDestroy()
    {
        GlobalEventSub.OnKeiSubmit -= OnKeiSubmit;
    }

    private void OnKeiSubmit(OOPartEnum type, int count)
    {
        RefreshAndShow();
    }

    /// <summary>重建列表，与任务已提交采集量同步</summary>
    protected override void RefreshItems()
    {
        if (listRoot == null || !TaskManager.Instance || TaskManager.Instance.nowTask == null) return;

        ClearItems();
        var collect = TaskManager.Instance.nowTask.collectProperty;
        if (collect == null) return;

        foreach (var kvp in collect)
        {
            int count = kvp.Value;
            if (count <= 0) continue;

            var go = CreatItem();
            SetSprite(go.GetChild(0), PropertyManager.Instance.GetIcon(kvp.Key));
            SetText(go.GetChild(1), PropertyManager.Instance.GetName(kvp.Key));
            SetText(go.GetChild(2), Tool.FillZero(count,2));
            //SetFill(go.GetChild(3), (count+0f)/MaxCapacity);
        }
    }
}
}
