using UnityEngine;

namespace FPSGame.GameContract
{
    // TODO: 本文件含两个互不相关的枚举，文件名与类型名都对不上。
    //       建议拆成 MissionTag.cs 与 UnitTier.cs（改名/移动请走 Unity，避免 GUID 与 meta 不同步）。

    /// <summary>
    /// 任务标签（位标志）。挂在 <c>MissionBase</c> 的 <c>missionTag</c> 上，UI 层据此决定任务条目/图标的显隐与表现。
    /// <para>⚠ 位标志<b>不能跳位</b>：每个已分配位都要保留，废弃的位只留下 <c>Placeholder*</c> 占位，禁止删除或重排。</para>
    /// </summary>
    [System.Flags]
    public enum MissionTag
    {
        /// <summary>初始暴露</summary>
        [InspectorName("初始暴露")] StratDiscovered = 1 << 0,
        /// <summary>显示一个区域</summary>
        [InspectorName("显示一个区域")] IsArea = 1 << 1,
        /// <summary>跟随地图缩放</summary>
        [InspectorName("跟随地图缩放")] FollowAreaScale = 1 << 2,
        /// <summary>完成时隐藏图标</summary>
        [InspectorName("完成时隐藏图标")] CompleteHide = 1 << 3,
        /// <summary>显示边框</summary>
        [InspectorName("显示边框")] DisplayFrame = 1 << 4,
        /// <summary>暴露后不隐藏</summary>
        [InspectorName("暴露后不隐藏")] OneDiscovered = 1 << 5,
        /// <summary>产生热度</summary>
        [InspectorName("产生热度")] HeatPoint = 1 << 6,

        /// <summary>占位符（位不可跳）</summary>
        [InspectorName("占位符")] Placeholder3 = 1 << 7,
        /// <summary>占位符（位不可跳）</summary>
        [InspectorName("占位符")] Placeholder4 = 1 << 8,

        /// <summary>完成时不播放音效</summary>
        [InspectorName("不播放音效")] NoAudio = 1 << 9,
        /// <summary>隐藏自身与子任务（UI 层）</summary>
        [InspectorName("隐藏含子任务")] HideAll = 1 << 10,
        /// <summary>只隐藏自身（UI 层）</summary>
        [InspectorName("隐藏自身")] HideSelf = 1 << 11,
        /// <summary>显示进度条</summary>
        [InspectorName("显示进度条")] DisplayProgress = 1 << 12,
        /// <summary>激活</summary>
        [InspectorName("激活")] IsActive = 1 << 13,
        /// <summary>允许重复呼叫战备</summary>
        [InspectorName("允许重复呼叫战备")] RepeatCall = 1 << 14,
    }

}
