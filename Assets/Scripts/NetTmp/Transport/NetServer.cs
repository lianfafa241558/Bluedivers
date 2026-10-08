using System.Collections.Generic;
using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{
    /// <summary>
    /// 【房主侧连接】纯传输：socket / 会话表 / 广播 / 会话事件 / 心跳应答 / 存活时刻表。
    ///
    /// <para>▍分工：传输只回答"谁连着、谁还在发数据"；**谁是玩家、谁是队员一律不在这里判断**
    /// （那是 07 的 <c>NetHostSvc</c>）。所以"空闲踢人"由 07 决定候选人，本类只提供
    /// <see cref="LastSeen"/> / <see cref="Touch"/> / <see cref="CloseSession"/>。</para>
    ///
    /// <para>▍线程模型：KCP 的会话回调跑在 <b>ThreadPool 线程</b> ⇒ 本类只把 sid 入队，真正的
    /// 事件派发在 <see cref="Pump"/>（主线程）里做。回调里**绝不能**碰 Unity API / 游戏状态
    /// （2026-10-06 打包版实测：直接在回调里跑会在"广播名单中途"炸掉，留下幽灵盟友）。</para>
    ///
    /// <para>▍纯类，由 07 的 <c>NetHostSvc</c>（挂 <c>GameRoot.prefab</c>）持有并每帧调 <see cref="Pump"/>。</para>
    /// </summary>
    public class NetServer
    {
        private KCPNet<HostSession, NetMessage> _host;

        /// <summary>某个成员建立了会话（还没入房）。主线程回调。</summary>
        public event System.Action<uint> OnConnected;

        /// <summary>某个成员会话断开。主线程回调。
        /// <para>参数2 <c>selfClosed</c>：是否**本端主动关的**（离开房间 / 踢人）⇒ 调用方据此避免重复提示。</para></summary>
        public event System.Action<uint, bool> OnDisconnected;

        /// <summary>会话事件（由传输线程入队、主线程处理）。</summary>
        private struct SessionEvent
        {
            public uint Sid;
            public bool Joined;
        }

        private readonly Queue<SessionEvent> _sessionEvents = new Queue<SessionEvent>();
        private readonly object _lock = new object();

        /// <summary>sid → 最后一次收到该成员消息的时刻（<c>Time.unscaledTime</c>）。只在主线程读写。</summary>
        private readonly Dictionary<uint, float> _lastSeen = new Dictionary<uint, float>();

        /// <summary>**本端主动关掉**的会话 sid（离开房间 / 踢人）。库随后回调的断开事件据此不再重复提示。</summary>
        private readonly HashSet<uint> _selfClosed = new HashSet<uint>();

        /// <summary>当前正在主线程分发的这条消息来自哪个成员（<see cref="Pump"/> 出队时设置）。</summary>
        public uint CurrentSid { get; private set; }

        /// <summary>服务器是否在监听。</summary>
        public bool IsRunning => _host != null;

        /// <summary>
        /// 【开房】以服务器身份监听端口。
        /// </summary>
        /// <returns>成功 true；失败 false 且 <paramref name="error"/> 为底层原因（如端口未释放）</returns>
        public bool Start(string bindAddress, int port, out string error)
        {
            error = string.Empty;

            var host = new KCPNet<HostSession, NetMessage>();
            try
            {
                host.StartAsServer(bindAddress, port);
            }
            catch (System.Net.Sockets.SocketException e)
            {
                // ⚠️ UDP 端口关闭后不会立刻释放（TIME_WAIT）⇒ 刚关房立即重开同一端口会抛 SocketException。
                //    这里不抛，交给游戏层决定怎么提示（保持原来的 KCPTool 日志与异常文案）。
                error = e.Message;
                return false;
            }

            _host = host;
            _host.OnSessionConnected += HandleSessionConnected;
            _host.OnSessionDisconnected += HandleSessionDisconnected;

            // 心跳：必须对**任何会话**都回（含还没入房的）—— 见 OnPingReq 的说明
            MessageCenter.Register<PingReqMsg>(NetCmdId.PingReq, OnPingReq);
            return true;
        }

        /// <summary>【关房】停监听 + 退订心跳 + 清会话/存活/主动关闭登记表。</summary>
        public void Stop()
        {
            MessageCenter.Unregister(NetCmdId.PingReq);

            if (_host != null)
            {
                _host.OnSessionConnected -= HandleSessionConnected;
                _host.OnSessionDisconnected -= HandleSessionDisconnected;
                _host.CloseServer();
                _host = null;
            }

            lock (_lock) _sessionEvents.Clear();
            _lastSeen.Clear();
            _selfClosed.Clear();
            CurrentSid = 0;
        }

        /// <summary>广播给所有成员（不含房主本地）。</summary>
        public void SendToAll(NetMessage msg)
        {
            _host?.SendToAll(msg);
        }

        /// <summary>定向发送给指定成员（按 sid）。</summary>
        public void SendTo(NetMessage msg, uint sid)
        {
            if (_host != null && _host.TryGetSession(sid, out HostSession s) && s.IsConnected())
            {
                s.SendMsg(msg);
            }
        }

        /// <summary>主动关闭某个成员的会话（离开房间 / 空闲踢人）。
        /// <para>▍先登记 <see cref="_selfClosed"/>：<c>CloseSession</c> 会触发库的断开回调 ⇒ 又会走一次
        /// <see cref="OnDisconnected"/>，登记过调用方就能识别"这是我自己关的"。</para></summary>
        public void CloseSession(uint sid)
        {
            _selfClosed.Add(sid);
            if (_host != null && _host.TryGetSession(sid, out HostSession s)) s.CloseSession();
        }

        // ==================== 存活时刻表（供游戏层做"空闲踢人"判断） ====================

        /// <summary>该 sid 最后一次活跃的时刻；从没收到过消息 = -1。</summary>
        public float LastSeen(uint sid)
        {
            return _lastSeen.TryGetValue(sid, out float t) ? t : -1f;
        }

        /// <summary>把该 sid 的活跃时刻记为"现在"（刚入房、还没收到过任何消息时用）。</summary>
        public void Touch(uint sid)
        {
            _lastSeen[sid] = Time.unscaledTime;
        }

        // ==================== 主线程泵 ====================

        /// <summary>
        /// 【主线程泵】① 派发会话事件 → ② 抽出站队列并 <see cref="MessageCenter.Dispatch"/>（同时刷新活跃时刻）。
        /// </summary>
        public void Pump()
        {
            ProcessSessionEvents();

            while (NetInbox.TryDequeueHost(out NetInbox.Item hm))
            {
                // 记录当前消息来源，供房间协议处理器识别是哪个成员发的
                CurrentSid = hm.Sid;
                // 收到任何一条来自该成员的消息（心跳/位姿/准备…）都算"他还活着"
                if (hm.Sid != 0) _lastSeen[hm.Sid] = Time.unscaledTime;
                try
                {
                    MessageCenter.Dispatch(hm.Msg);
                }
                finally
                {
                    CurrentSid = 0;
                }
            }
        }

        /// <summary>主线程：按到达顺序派发会话事件（每帧在 <see cref="Pump"/> 里跑一次）。</summary>
        private void ProcessSessionEvents()
        {
            while (true)
            {
                SessionEvent ev;
                lock (_lock)
                {
                    if (_sessionEvents.Count == 0) return;
                    ev = _sessionEvents.Dequeue();
                }

                // ⚠ 出队之后在锁外处理：OnDisconnected 会走游戏的"广播名单 + 本地派发"，不该占着锁
                if (ev.Joined)
                {
                    OnConnected?.Invoke(ev.Sid);
                }
                else
                {
                    bool selfClosed = _selfClosed.Remove(ev.Sid);
                    _lastSeen.Remove(ev.Sid);
                    OnDisconnected?.Invoke(ev.Sid, selfClosed);
                }
            }
        }

        // ==================== 传输线程 → 只入队 ====================
        private void HandleSessionConnected(uint sid)
        {
            lock (_lock) _sessionEvents.Enqueue(new SessionEvent { Sid = sid, Joined = true });
        }

        private void HandleSessionDisconnected(uint sid)
        {
            lock (_lock) _sessionEvents.Enqueue(new SessionEvent { Sid = sid, Joined = false });
        }

        // ==================== 心跳应答 ====================
        /// <summary>【成员心跳】房主回一条 <see cref="PingRspMsg"/>（成员据此测 RTT，也让它的会话"有收到数据"）。
        ///
        /// <para>▍⚠⚠ **必须对"任何会话"都回，包括还没入房的**（2026-10-07 实测的"战备界面等一会儿就反复断开"就是它的锅）：
        /// 库的会话超时判的是"**收不到数据**就判掉线"（<c>TimeoutMs</c> 默认 15s），而成员从"连上房主"到"入房成功"
        /// 之间**只有心跳这一条报文**。若在这里加"没入房就早退"⇒ 那条连接**一个字节都收不到** ⇒ 15s 后必被它自己的库判死
        /// → 客户端重连 → 再被判死 ⇒ 房主控制台每二十来秒刷一串"成员断开 / 成员断连（未入房）"。</para>
        ///
        /// <para>关闸（Armament 之后不收人）由入房校验负责，**不该靠"不回心跳"来饿死对方**。</para>
        /// </summary>
        private void OnPingReq(PingReqMsg req)
        {
            if (req == null) return;

            uint sid = CurrentSid;
            if (sid == 0) return;                        // 没有来源会话 ⇒ 不该发生

            SendTo(MessageCenter.Pack(NetCmdId.PingRsp, new PingRspMsg
            {
                Id = req.Id,
                ServerTime = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond,
            }), sid);
        }
    }
}
