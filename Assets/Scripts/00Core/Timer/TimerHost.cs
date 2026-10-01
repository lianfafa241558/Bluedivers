using System;
using System.Collections;
using UnityEngine;

namespace FPSGame.Core
{
    /// <summary>
    /// 计时器 / 协程的**宿主入口原语**（2026-10-01 取代 <c>ServiceLocator.Flow</c> 的"调度"部分）。
    ///
    /// <para>▍为什么归 <c>00_Core</c>：**设施本来就在 Core** —— 宿主是 <see cref="ViewTimerController"/>
    /// （<c>00Core/Timer/ViewTimerController.cs</c>），它驱动的 <c>ViewTimerSystem</c> 也在同目录；
    /// 而创建宿主的 <c>GameRootBase.Awake</c>（<c>00Core/GameRootBase.cs</c>）本身就在 `00_Core`。
    /// ⇒ 与 <c>VfxPool</c> 同一条准入理由（"**设施与入口同层**"）。</para>
    ///
    /// <para>▍为什么必须静态入口（不能注入）：消费者是 prefab 实例化的单位/武器/任务组件（生成点分散），
    /// 且含**静态工具类**（<c>FpsHelper_Hit</c>）⇒ 注入不可行。</para>
    ///
    /// <para>▍写入保护：宿主与本类在**同一程序集**（00_Core）⇒ 只写 <c>internal set</c>，
    /// 连 <c>InternalsVisibleTo</c> 都不需要，外部层连 setter 都看不见。</para>
    ///
    /// <para>▍未就绪时（宿主 <c>Awake</c> 之前）全部空操作：<c>CreateTimer</c> 返回 null、协程不启动
    /// —— 等价于原 <c>NullFlowService</c> 的语义。</para>
    ///
    /// <para>▍取代了 `CoroutineSvc`：那个类会**自己再造一个常驻 GameObject** 当协程宿主，属于重复设施；
    /// 现在复用同一个宿主（本文档所在的宿主本来就是 <c>DontDestroyOnLoad</c> 的）。</para>
    ///
    /// <para>▍准入自查（Core 受控原语清单 = <c>LogicFrame</c> / <c>VfxPool</c> / <c>TimerHost</c>）：
    /// 必须满足"**设施与入口同层**"或"**宿主唯一且有明确制造者**"，并写清语义边界与退出条件
    /// —— 否则就是"把定位器换个地方长"。</para>
    ///
    /// <para>▍退出条件：等显式注入 / 装配根落地后，与定位器一起重估；但静态工具类消费者注定要留个静态入口。</para>
    /// </summary>
    public static class TimerHost
    {
        /// <summary>宿主（由 <c>GameRootBase.Awake</c> 发布）。同程序集 ⇒ <c>internal</c> 已足够。</summary>
        internal static ViewTimerController Host;

        /// <summary>创建一次性计时器（未就绪返回 null）。</summary>
        public static LogicTimer CreateTimer(Action cb, float waitTime, int counter = 1, Action endcb = null)
            => Host == null ? null : Host.CreateTimer(cb, waitTime, counter, endcb);

        /// <summary>创建一次性计时器（带计数回调版本）。</summary>
        public static LogicTimer CreateTimer(Action<int> cb, float waitTime, int counter = 1, Action endcb = null)
            => Host == null ? null : Host.CreateTimer(cb, waitTime, counter, endcb);

        /// <summary>创建循环计时器。</summary>
        public static LogicTimer CreatePerTimer(Action percb, float waitTime, Action endcb = null)
            => Host == null ? null : Host.CreatePerTimer(percb, waitTime, endcb);

        /// <summary>移除计时器。</summary>
        public static void RemoveTimer(LogicTimer timer)
        {
            if (timer != null) Host?.RemoveTimer(timer);
        }

        /// <summary>清空全部计时器。</summary>
        public static void ClearTimer() => Host?.ClearTimer();

        /// <summary>在常驻宿主上跑协程（取代 <c>CoroutineSvc</c>）。未就绪返回 null 且不启动。</summary>
        public static Coroutine RunCoroutine(IEnumerator routine)
            => (Host == null || routine == null) ? null : Host.StartCoroutine(routine);

        /// <summary>排查用：宿主是否已就绪。</summary>
        public static string Dump() => "TimerHost.Host = " + (Host == null ? "<none>" : Host.GetType().Name);
    }
}
