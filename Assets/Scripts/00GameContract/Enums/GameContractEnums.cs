using UnityEngine;

namespace FPSGame.GameContract
{
    [System.Flags]//flag不能跳位，必须占位符
    public enum MissionTag
    {
        /// <summary>初始暴露</summary>
        [InspectorName("初始暴露")] StratDiscovered = 1 << 0,
        /// <summary>显示一个区域</summary>
        [InspectorName("显示一个区域")] IsArea = 1 << 1,
        /// <summary>跟随地图缩放</summary>
        [InspectorName("跟随地图缩放")] FollowAreaScale = 1 << 2,
        /// <summary>完成时隐藏图标</summary>
        [InspectorName("完成时隐藏图标")] CompleHide = 1 << 3,
        /// <summary>显示边框</summary>
        [InspectorName("显示边框")] DisplayFrame = 1 << 4,
        /// <summary>暴露后不隐藏</summary>
        [InspectorName("暴露后不隐藏")] OneDiscovered = 1 << 5,
        /// <summary>产生热度</summary>
        [InspectorName("产生热度")] HeatPoint = 1 << 6,

        [InspectorName("占位符")] placeholder3 = 1 << 7,
        [InspectorName("占位符")] placeholder4 = 1 << 8,
        /// <summary>完成时不播放音效</summary>
        [InspectorName("不播放音效")] NoAudio = 1 << 9,
        /// <summary>是否隐藏和子任务(在ui层)</summary>
        [InspectorName("是否隐藏和子任务(在ui层)")] hideAll = 1 << 10,
        /// <summary>是否隐藏自身(在uui层)</summary>
        [InspectorName("是否隐藏自身(在ui层)")] hideSelf = 1 << 11,
        /// <summary>显示进度条</summary>
        [InspectorName("显示进度条")] DisplayProgress = 1 << 12,
        /// <summary>激活</summary>
        [InspectorName("激活")] IsActive = 1 << 13,
        /// <summary>允许重复呼叫战备</summary>
        [InspectorName("允许重复呼叫战备")] RepeatCall = 1 << 14,
    }

    public enum UnitTier
    {
        /// <summary>无</summary>
        [InspectorName("无")] None = 0,
        /// <summary>小型单位</summary>
        [InspectorName("小型单位")] Small = 1,
        /// <summary>中型单位</summary>
        [InspectorName("中型单位")] Medium = 2,
        /// <summary>精英单位</summary>
        [InspectorName("精英单位")] Elite = 3,
        /// <summary>重型单位</summary>
        [InspectorName("重型单位")] Heavy = 4,
        /// <summary>巨型单位</summary>
        [InspectorName("巨型单位")] Giant = 5,
        /// <summary>警戒单位</summary>
        [InspectorName("警戒单位")] Alert = 6,
        /// <summary>特殊单位1</summary>
        [InspectorName("特殊单位1")] Special1 = 7,
        /// <summary>特殊单位2</summary>
        [InspectorName("特殊单位2")] Special2 = 8,
        /// <summary>首领</summary>
        [InspectorName("首领")] Boss = 9,
    }
}
