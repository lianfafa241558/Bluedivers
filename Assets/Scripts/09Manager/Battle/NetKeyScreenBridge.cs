using FPSGame.Gameplay;
using FPSGame.Net;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · 按键终端(KeyScreen)阶段同步桥（09）：**一处收口终端的阶段推进**。
    ///
    /// <para>▍为什么需要：阶段推进原本纯本地（<c>KeyScreen.OnUpdateStage</c> 只在本地抛），而任务的
    /// **中间阶段标志**全靠它 —— 黑盒子的 <c>startDefense</c>、炼油的 Start/Repair、撤离的 Wait、
    /// <c>KeyScreenContGo</c> 的显隐与语音。两端阶段一分叉，就是"客机进了坚守、房主没进"（2026-10-11 用户实测）。</para>
    ///
    /// <para>▍驱动口径（判定唯一收口在 <c>KeyScreen.IsStageDriver</c>）：**只有驱动端上报** —— 有人操作时 =
    /// 操作者所在那台；无人操作（Load/Wait 自动阶段） = 房主。其余端只应用，不自行推进。</para>
    ///
    /// <para>▍键 = 终端家具的 <c>Furniture_Attached.SyncId</c>（与 4036 同口径 ⇒ 两端日志可直接对照）。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> Install/Uninstall（同 <see cref="NetFurnitureBridge"/> 模式）。</para>
    /// </summary>
    public static class NetKeyScreenBridge
    {
        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;

            // 成员端：终端阶段**不自行推进**，等驱动端（操作者那台 / 房主）下发
            var flow = NetRoomFlow.Instance;
            KeyScreen.RemoteDriven = flow != null && flow.SelfSid != 0u;

            KeyScreen.OnLocalStageChanged += OnLocalStageChanged;
            KeyScreen.OnLocalInput += OnLocalInput;
            NetRoomFlow.OnKeyScreenStage += OnRemoteStage;
            NetRoomFlow.OnKeyScreenInput += OnRemoteInput;
            NetRoomFlow.OnHostLost += OnHostLost;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            KeyScreen.OnLocalStageChanged -= OnLocalStageChanged;
            KeyScreen.OnLocalInput -= OnLocalInput;
            NetRoomFlow.OnKeyScreenStage -= OnRemoteStage;
            NetRoomFlow.OnKeyScreenInput -= OnRemoteInput;
            NetRoomFlow.OnHostLost -= OnHostLost;

            // ⚠ 必须复位：否则单机 / 下一局的终端永远不推进（同 MissionBase.RemoteDriven 那个坑）
            KeyScreen.RemoteDriven = false;
        }

        /// <summary>房主掉了 ⇒ 本端接管推进（否则这一局的终端就永久停在当前阶段）。</summary>
        static void OnHostLost()
        {
            KeyScreen.RemoteDriven = false;
        }

        #region 本端 -> 网络

        /// <summary>⚠ 只会被**驱动端**调到（<c>KeyScreen.SetStage</c> 里已经判过驱动门）。</summary>
        static void OnLocalStageChanged(int syncId, int stage)
        {
            if (syncId == 0) return;                 // 没有稳定键（终端上没身份源）：不参与同步
            if (NetApplyGuard.Applying) return;      // 正在应用远端：别再上报（防回环）

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            FPSGame.Utils.NetSyncLog.SyncLog("同步/终端/发",
                $"sync={syncId} → 阶段 {stage}（我是{(flow.IsHost ? "房主" : "成员")}）");
            flow.SendKeyScreenStage(syncId, flow.IsHost ? 0u : flow.SelfSid, stage);
        }

        /// <summary>【驱动端】把本机对终端的每一次按键传出去（非驱动端靠它复现整个终端）。
        /// <para>▍为什么只传按键而不传状态：非驱动端把输入换成远端按键后**跑的是同一套阶段逻辑**，
        /// UI / 判定 / 光标位置全部自然一致，不用再抽一堆"刷新 UI"的接口。</para></summary>
        static void OnLocalInput(int syncId, int key, int kind)
        {
            if (syncId == 0) return;
            if (NetApplyGuard.Applying) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;      // 单机 / 不在房里

            flow.SendKeyScreenInput(syncId, flow.IsHost ? 0u : flow.SelfSid, key, kind);
        }

        #endregion

        #region 网络 -> 本端

        static void OnRemoteStage(KeyScreenStageMsg m)
        {
            if (m == null || m.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost)
            {
                // 房主：转发全体（含发起者；发起者按 Sid 丢掉自己那条）—— 权威 Sid 原样带上
                flow.SendKeyScreenStage(m.SyncId, m.Sid, m.Stage);
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                              // 自己发的那条绕回来了：本地已经推进过
            }

            var furn = Furniture_Attached.FindBySyncId(m.SyncId);
            var screen = furn != null ? furn.GetComponentInChildren<KeyScreen>(true) : null;
            if (screen == null)
            {
                // ⚠ 不静默：要么两端 SyncId 不一致，要么本端压根没这个终端（同 NetFurnitureBridge 的排查口径）
                FPSGame.Utils.NetSyncLog.Warn("同步/终端/收",
                    $"sync={m.SyncId} 阶段{m.Stage}（发起 sid={m.Sid}）**本端找不到这个终端** ⇒ 丢弃");
                return;
            }

            FPSGame.Utils.NetSyncLog.SyncLog("同步/终端/收",
                $"{furn.name} → 阶段 {m.Stage}（发起 sid={m.Sid}）");

            // 共享门：应用阶段可能顺带触发 furn.Operate() ⇒ 会派发 FurnitureOperate（4036）⇒ 挡住它的上报
            NetApplyGuard.Begin();
            try
            {
                screen.ApplyRemoteStage(m.Stage);
            }
            finally
            {
                NetApplyGuard.End();
            }
        }

        /// <summary>【非驱动端】复现远端玩家的按键（房主转发全体；发起者按 Sid 丢自己那条）。</summary>
        static void OnRemoteInput(KeyScreenInputMsg m)
        {
            if (m == null || m.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost) flow.SendKeyScreenInput(m.SyncId, m.Sid, m.Key, m.Kind);
            else if (m.Sid == flow.SelfSid) return;

            var furn = Furniture_Attached.FindBySyncId(m.SyncId);
            var screen = furn != null ? furn.GetComponentInChildren<KeyScreen>(true) : null;
            if (screen == null) return;          // 找不到终端：静默（阶段同步那边已经会告警一次）

            screen.ApplyRemoteInput(m.Key, m.Kind);
        }

        #endregion
    }
}
