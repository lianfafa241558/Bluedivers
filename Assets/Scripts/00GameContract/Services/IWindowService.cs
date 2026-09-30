using System;
using FPSGame.Core;
using UnityEngine.Events;

namespace FPSGame.GameContract
{
    /// <summary>
    /// 窗口 / UI 服务契约（由 <c>WndManager</c> 实现并注册，2026-09-30 为 P5 加入）。
    ///
    /// <para>▍只开下层**实测用到**的成员：<c>WindowState</c>、<c>OnWindowStateChange</c>、<c>CreatNotice</c>、
    /// <c>SetWndState</c>（2026-09-30 补：<c>Furniture_General</c> 等家具要"打开某个窗口"）。</para>
    ///
    /// <para>▍<b>为什么窗口只用"动词 + 枚举"暴露</b>：<c>WndManager</c> 的窗口字段
    /// （<c>airdropConfigWnd</c>/<c>guideWnd</c>/<c>settingWnd</c>…）是 `04UI` 的**具体窗口类型**，
    /// 契约层根本无法命名；而且下层只需要"开一下"这个动作，给出句柄只会诱使耦合蔓延
    /// ⇒ 用 <see cref="WndTypeEnum"/> 当钥匙，见 <c>WndTypeEnum.cs</c>。
    /// （把窗口身份改为"自注册到注册表"留待 Phase 6 —— 届时 <c>WndManager</c> 才能删掉那 10 个字段。）</para>
    ///
    /// <para>⚠ 实现方注意：<c>WindowState</c> 是**静态属性**、<c>OnWindowStateChange</c> 是**静态事件**，
    /// 都无法隐式实现接口 ⇒ 需显式转发（事件用 <c>add => OnWindowStateChange += value;</c> 形式）。</para>
    /// </summary>
    public interface IWindowService
    {
        /// <summary>当前窗口状态（原 <c>WndManager.WindowState</c> 是静态属性）。</summary>
        WindowStateEnum WindowState { get; }

        /// <summary>窗口状态变化事件（实现方转发其静态事件；仅 oldState != value 时触发）。</summary>
        event UnityAction<WindowStateEnum, WindowStateEnum> OnWindowStateChange;

        /// <summary>弹一条 NPC 提示（<paramref name="func"/> 为持续条件，返回 false 即提前收起）。</summary>
        void CreatNotice(string role, string type, Func<bool> func = default, float vaildTime = -1);

        /// <summary>
        /// 开关某个窗口（语义 = 该窗口自身的 <c>SetWndState</c>：含首次打开的初始化与
        /// <c>GlobalEventSub.WndSwitch</c> 通知）。实现方负责把 <see cref="WndTypeEnum"/> 映射到具体窗口。
        /// </summary>
        void SetWndState(WndTypeEnum type, bool isActive = true);
    }
}
