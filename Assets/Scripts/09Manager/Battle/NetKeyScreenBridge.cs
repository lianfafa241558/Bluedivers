using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

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
    /// <para>▍键 = 终端家具的 <c>Furniture_Attached.SyncId</c>（与 4059 同口径 ⇒ 两端日志可直接对照）。</para>
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

            // 软键：本端这枚终端自己的 Id + 位置（撤离信标这类两端各生成一份的终端，SyncId 会因位置微差分叉）
            var local = Furniture_Attached.FindBySyncId(syncId);
            FPSGame.Utils.NetSyncLog.SyncLog("同步/终端/发",
                $"sync={syncId} → 阶段 {stage}（我是{(flow.IsHost ? "房主" : "成员")}）");
            flow.SendKeyScreenStage(syncId, flow.IsHost ? 0u : flow.SelfSid, stage,
                local != null ? local.Id : null, local != null ? local.transform.position : (Vector3?)null);
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

            var local = Furniture_Attached.FindBySyncId(syncId);
            flow.SendKeyScreenInput(syncId, flow.IsHost ? 0u : flow.SelfSid, key, kind,
                local != null ? local.Id : null, local != null ? local.transform.position : (Vector3?)null);
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
                flow.SendKeyScreenStage(m.SyncId, m.Sid, m.Stage, m.Id, new Vector3(m.X, m.Y, m.Z));
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                              // 自己发的那条绕回来了：本地已经推进过
            }

            // 精确键 → 软键（Id + 发送端位置取最近）：撤离信标这类两端各生成一份的终端靠它兜住
            var furn = Furniture_Attached.Resolve(m.SyncId, m.Id, new Vector3(m.X, m.Y, m.Z), "同步/终端/收");
            var screen = furn != null ? furn.GetComponentInChildren<KeyScreen>(true) : null;
            if (screen == null)
            {
                // ⚠ 不能直接丢：本端终端可能**还没落地**（空投信标两端各生成一份，舱体落体有先后）。
                //   一次性丢弃 = 客机永远停在旧阶段，而且驱动端那条阶段是一次性的（无人阶段推完就不再发）。
                QueueStage(m);
                return;
            }

            ApplyStage(furn, screen, m.Stage, m.Sid);
        }

        /// <summary>把阶段真正应用到本端终端（统一的执行口：立刻命中与挂起重试都走这里）。</summary>
        static void ApplyStage(Furniture_Attached furn, KeyScreen screen, int stage, uint sid)
        {
            FPSGame.Utils.NetSyncLog.SyncLog("同步/终端/收",
                $"{furn.name} → 阶段 {stage}（发起 sid={sid}）");

            // 共享门：应用阶段可能顺带触发 furn.Operate() ⇒ 会派发 FurnitureOperate（4059）⇒ 挡住它的上报
            NetApplyGuard.Begin();
            try
            {
                screen.ApplyRemoteStage(stage);
            }
            finally
            {
                NetApplyGuard.End();
            }
        }

        #region 挂起重试（终端还没生成）

        /// <summary>收端暂时找不到终端时挂起的阶段（同一终端只留最新一条）。</summary>
        private struct PendingStage
        {
            public int SyncId;
            public string Id;
            public Vector3 Pos;
            public int Stage;
            public float Deadline;
        }

        private static readonly System.Collections.Generic.List<PendingStage> _pending = new();

        /// <summary>挂起窗口(秒)：空投舱 <c>arriveTime</c> + 落地 + 生成物 Awake 一般远小于它。</summary>
        private const float PendingWindow = 15f;

        /// <summary>重试间隔(秒)：够密（终端一出现就补上），又不会每帧扫全表。</summary>
        private const float RetryInterval = 0.5f;

        static void QueueStage(KeyScreenStageMsg m)
        {
            var pos = new Vector3(m.X, m.Y, m.Z);
            for (int i = 0; i < _pending.Count; ++i)
            {
                if (_pending[i].SyncId != m.SyncId) continue;
                var item = _pending[i];              // 同一条终端又来新阶段：覆盖成最新，别排队重放旧阶段
                item.Stage = m.Stage;
                item.Deadline = Time.unscaledTime + PendingWindow;
                _pending[i] = item;
                return;
            }

            _pending.Add(new PendingStage
            {
                SyncId = m.SyncId,
                Id = m.Id,
                Pos = pos,
                Stage = m.Stage,
                Deadline = Time.unscaledTime + PendingWindow,
            });

            FPSGame.Utils.NetSyncLog.Error("同步/终端/收",
                $"sync={m.SyncId} 阶段{m.Stage}（软键 id={m.Id}）**本端还没有这个终端** ⇒ 挂起 {PendingWindow}s 内每 {RetryInterval}s 重试" +
                "（空投信标这类两端各生成一份的终端，落体先后会让消息先到）");

            if (_pending.Count == 1) ScheduleRetry();
        }

        static void ScheduleRetry()
        {
            // ⚠ 用 TimerHost（宿主 = ViewTimerController）：本类是静态类，没有自己的 Update
            //   宿主还没发布（极早期）⇒ 排不上就作废，别让挂起表永久留住条目
            if (FPSGame.Core.TimerHost.CreateTimer(RetryPending, RetryInterval) == null) _pending.Clear();
        }

        static void RetryPending()
        {
            float now = Time.unscaledTime;
            for (int i = _pending.Count - 1; i >= 0; --i)
            {
                PendingStage item = _pending[i];
                var furn = Furniture_Attached.Resolve(item.SyncId, item.Id, item.Pos, "同步/终端/收·重试");
                var screen = furn != null ? furn.GetComponentInChildren<KeyScreen>(true) : null;

                if (screen != null)
                {
                    _pending.RemoveAt(i);
                    FPSGame.Utils.NetSyncLog.SyncLog("同步/终端/收", $"挂起重试命中 {furn.name}（等了 {(PendingWindow - (item.Deadline - now)):F1}s）");
                    ApplyStage(furn, screen, item.Stage, 0u);
                }
                else if (now > item.Deadline)
                {
                    _pending.RemoveAt(i);
                    FPSGame.Utils.NetSyncLog.Error("同步/终端/收",
                        $"sync={item.SyncId}（软键 id={item.Id} 位置={item.Pos}）阶段{item.Stage}：{PendingWindow}s 内始终没有这个终端 ⇒ 放弃" +
                        "（本端没生成这件终端 / Id 与位置都对不上）");
                }
            }

            if (_pending.Count > 0) ScheduleRetry();
        }

        #endregion

        /// <summary>【非驱动端】复现远端玩家的按键（房主转发全体；发起者按 Sid 丢自己那条）。</summary>
        static void OnRemoteInput(KeyScreenInputMsg m)
        {
            if (m == null || m.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost) flow.SendKeyScreenInput(m.SyncId, m.Sid, m.Key, m.Kind, m.Id, new Vector3(m.X, m.Y, m.Z));
            else if (m.Sid == flow.SelfSid) return;

            var furn = Furniture_Attached.Resolve(m.SyncId, m.Id, new Vector3(m.X, m.Y, m.Z), "同步/终端/输入·收");
            var screen = furn != null ? furn.GetComponentInChildren<KeyScreen>(true) : null;
            if (screen == null) return;          // 找不到终端：静默（阶段同步那边已经会告警一次）

            screen.ApplyRemoteInput(m.Key, m.Kind);
        }

        #endregion
    }
}
