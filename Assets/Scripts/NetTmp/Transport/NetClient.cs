using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{
    /// <summary>
    /// 【成员侧连接】纯传输：建连 / 收发 / 心跳 / RTT / 主线程泵。
    ///
    /// <para>▍为什么是纯类而不是 MonoBehaviour：它只做"连接"这一件事，不该占用 GameObject；
    /// 由 07_NetGame 的 <c>NetSvc</c>（挂在 <c>GameRoot.prefab</c> 上）持有一个实例并在 <c>Update</c> 里调
    /// <see cref="Pump"/> ⇒ 加减传输实现不需要动 prefab。</para>
    /// <para>▍游戏语义（JoinRoom/LeaveRoom/SetReady 等）一律留在 07 的 <c>NetSvc</c>。</para>
    /// </summary>
    public class NetClient
    {
        private KCPNet<ClientSession, NetMessage> _client;

        /// <summary>是否已建立 KCP 连接（供业务层判断能否发送消息）。</summary>
        public bool IsConnected => _client != null && _client.clientSession != null && _client.clientSession.IsConnected();

        /// <summary>最近一次往返延迟（毫秒）；还没测到 = -1。</summary>
        public float RttMs { get; private set; } = -1f;

        private readonly float _heartbeatInterval;
        private float _heartbeatLeft;
        private uint _pingId;
        private uint _lastPingId;
        private long _lastPingSentMs;

        /// <param name="heartbeatInterval">心跳间隔（秒）；&lt;=0 关闭心跳</param>
        public NetClient(float heartbeatInterval)
        {
            _heartbeatInterval = heartbeatInterval;
            // 心跳应答：不注册的话 Dispatch 会报"未注册消息:1002"
            MessageCenter.Register<PingRspMsg>(NetCmdId.PingRsp, OnPingRsp);
        }

        /// <summary>
        /// 【回连房主】局域网房间广播找到房间后，用房间信息发起 KCP 回连。
        /// </summary>
        /// <param name="room">发现到的房间信息（用其 HostIp + HostPort 回连）</param>
        /// <param name="cb">连接结果回调（true=成功，false=失败），在异步连接结束后调用</param>
        public void ConnectToRoom(LanRoomInfo room, System.Action<bool> cb = null)
        {
            if (room == null)
            {
                cb?.Invoke(false);
                return;
            }

            Disconnect(); // 换房间/首次回连前，先清理旧连接

            _client = new KCPNet<ClientSession, NetMessage>();
            _client.StartAsClient(room.HostIp, room.HostPort);

            var kcp = _client;
            System.Action<KCPNet<ClientSession, NetMessage>> connectAction = async (c) =>
            {
                // ConnectServer：每隔 interval ms 检测一次，最长等 maxintervalSum ms
                var result = await c.ConnectServer(200, 5000);
                Debug.Log($"[NetClient] 回连房主 {room.HostIp}:{room.HostPort} → {(result.Success ? "成功" : result.Message)}");
                cb?.Invoke(result.Success);
            };
            connectAction(kcp);
        }

        /// <summary>【断开当前连接】清空客户端与入站队列，供换房间/退出时调用。</summary>
        public void Disconnect()
        {
            if (_client != null)
            {
                _client.CloseClient();
                _client = null;
            }
            NetInbox.ClearClient();
        }

        /// <summary>【发送】成员端只有一条连接，直接走会话。</summary>
        public void Send(NetMessage msg)
        {
            if (msg == null) return;
            if (_client != null && _client.clientSession != null && _client.clientSession.IsConnected())
            {
                _client.clientSession.SendMsg(msg);
            }
            else
            {
                Debug.LogError("[NetClient] 尚未连接房主，无法发送消息");
            }
        }

        /// <summary>【主线程泵】心跳 + 把传输层队列里的消息逐条交给 <see cref="MessageCenter"/> 分发。</summary>
        public void Pump()
        {
            SendHeartbeat();

            while (NetInbox.TryDequeueClient(out NetMessage msg))
            {
                MessageCenter.Dispatch(msg);
            }
        }

        /// <summary>
        /// 定时心跳（<see cref="NetCmdId.PingReq"/>，房主回 <see cref="NetCmdId.PingRsp"/>）。
        ///
        /// <para>▍为什么必须有：库的会话带"收不到数据就判掉线"的超时（<c>TimeoutMs</c> 默认 15000ms），
        /// 而舰桥阶段两端可能长时间没有任何业务报文（位姿同步只在战斗里跑）⇒ 没有心跳，健康连接也可能被
        /// 自己的库判定超时而断开。</para>
        /// </summary>
        private void SendHeartbeat()
        {
            if (_heartbeatInterval <= 0f || !IsConnected) return;

            _heartbeatLeft -= Time.unscaledDeltaTime;
            if (_heartbeatLeft > 0f) return;
            _heartbeatLeft = _heartbeatInterval;

            _lastPingId = ++_pingId;
            _lastPingSentMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
            Send(MessageCenter.Pack(NetCmdId.PingReq, new PingReqMsg
            {
                Id = _lastPingId,
                SendTime = _lastPingSentMs,
            }));
        }

        /// <summary>【房主的 Pong】算一次 RTT（<see cref="PingRspMsg"/> 只带回 Id，不回显发送时刻 ⇒ 靠本地记的发送时刻相减）。</summary>
        private void OnPingRsp(PingRspMsg rsp)
        {
            if (rsp == null || rsp.Id != _lastPingId) return;   // 过期/错配的应答直接忽略

            long nowMs = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond;
            RttMs = Mathf.Max(0f, nowMs - _lastPingSentMs);
        }

        /// <summary>销毁/换房间时清理（退订心跳应答 + 断开）。</summary>
        public void Dispose()
        {
            MessageCenter.Unregister(NetCmdId.PingRsp);
            Disconnect();
        }
    }
}
