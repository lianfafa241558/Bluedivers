using System;
using FPSGame.Core;

// 注册方是 UI 层的 `WndHub`（10_UI）⇒ 只放给它写；管理器/玩法/表现层**只能读**。
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("10_UI")]

namespace FPSGame.GameContract
{
    /// <summary>
    /// 窗口注册表（2026-10-01 建，用于**切断 `01Manager → 04UI` 的反向依赖**）。
    ///
    /// <para>▍它解决什么：<c>WndManager</c> 原先持有 10 个**具体窗口字段**
    /// （<c>selectRoleWnd</c>/<c>guideWnd</c>/<c>settingWnd</c>…，都是 `04UI` 类型）⇒
    /// 「管理器 → UI」的反向依赖，`09_Managers` 与 `10_UI` 都切不出来。
    /// 本类用**委托**当插槽（不存窗口对象、不出现任何 UI 类型），UI 层把自己登记进来，
    /// 管理器经它转调 ⇒ 契约层对"窗口"只剩**动词**。</para>
    ///
    /// <para>▍谁登记：UI 层的 <c>WndHub</c>（`04UI/WndHub.cs`）在窗口自注册时填充这些插槽，
    /// 并把 <see cref="Scanner"/> 设成"重新扫描场景里全部窗口（含未激活）"的委托 —— 这样
    /// **窗口从未被打开过也能被打开**（窗口平时是关着的，`Awake`/`Init` 可能还没跑）。</para>
    ///
    /// <para>▍退出条件：等 `10_UI` 成集且改为显式装配后，本类应被删除。
    /// （⚠ 别再拿 <see cref="BattleHub"/> 当"同命参照"—— 它是"需要返回值/回调"的战斗能力接缝，属**合理保留**、不是过渡物。）</para>
    /// </summary>
    public static class WindowRegistry
    {
        /// <summary>(窗口, 是否打开) → 是否命中；未命中返回 false（调用方静默跳过）。</summary>
        public static Func<WndType, bool, bool> SetWndState { get; internal set; }

        /// <summary>(窗口) → 是否已打开；未登记返回 false。</summary>
        public static Func<WndType, bool> IsOpen { get; internal set; }

        /// <summary>(角色, 语音组, 持续条件, 有效时长) → 弹一条 NPC 提示。</summary>
        public static Action<string, string, Func<bool>, float> CreatNotice { get; internal set; }

        /// <summary>(倒计时读数提供者, 样式) → 启动倒计时窗。</summary>
        public static Action<Func<int>, CountDownTypeEnum> CreatCountDown { get; internal set; }

        /// <summary>清掉当前 NPC 提示。</summary>
        public static Action ClearNotice { get; internal set; }

        /// <summary>
        /// UI 层登记的"重新扫描"委托（扫场景里**全部**窗口，含未激活）。
        /// 由 <see cref="EnsureScanned"/> 在插槽为空时调用一次。
        /// </summary>
        public static Action Scanner { get; internal set; }

        private static bool scanned;

        /// <summary>插槽还没被填过就请 UI 层扫一次（幂等，最多一次）。</summary>
        public static void EnsureScanned()
        {
            if (scanned || SetWndState != null) return;
            scanned = true;
            if (Scanner != null) Scanner();
        }

        /// <summary>按枚举开关窗口。命中返回 true；未登记（或 UI 尚未就绪）返回 false。</summary>
        public static bool SetState(WndType type, bool isActive)
        {
            EnsureScanned();
            return SetWndState != null && SetWndState(type, isActive);
        }

        /// <summary>窗口是否已打开。</summary>
        public static bool GetOpen(WndType type)
        {
            EnsureScanned();
            return IsOpen != null && IsOpen(type);
        }

        /// <summary>弹 NPC 提示（未登记时静默跳过）。</summary>
        public static void Notice(string role, string type, Func<bool> func, float vaildTime)
        {
            EnsureScanned();
            if (CreatNotice != null) CreatNotice(role, type, func, vaildTime);
        }

        /// <summary>启动倒计时窗（未登记时静默跳过）。</summary>
        public static void CountDown(Func<int> provider, CountDownTypeEnum type)
        {
            EnsureScanned();
            if (CreatCountDown != null) CreatCountDown(provider, type);
        }

        /// <summary>清掉 NPC 提示。</summary>
        public static void Clear()
        {
            EnsureScanned();
            if (ClearNotice != null) ClearNotice();
        }

        /// <summary>登记状态摘要（排查用）。</summary>
        public static string Dump()
        {
            return "WindowRegistry: SetWndState=" + (SetWndState != null)
                 + " IsOpen=" + (IsOpen != null)
                 + " CreatNotice=" + (CreatNotice != null)
                 + " CreatCountDown=" + (CreatCountDown != null)
                 + " ClearNotice=" + (ClearNotice != null)
                 + " Scanner=" + (Scanner != null)
                 + " scanned=" + scanned;
        }
    }
}
