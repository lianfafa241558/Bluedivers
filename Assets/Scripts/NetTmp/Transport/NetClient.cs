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

        /// <summary>
        /// 【客户端身份】进程内固定的身份 ID（只读 ⇒ 换端口重连复用同一个值）。
        /// 房主侧按它认人 ⇒ 同一玩家反复重连只保留一条会话；不注入则退回库的"按 ip:端口 去重"
        /// （换端口就认不出是同一个人）。必须在 <see cref="ConnectToRoom"/> 里注入 client.ClientId。
        /// </summary>
        private readonly string _clientId = System.Guid.NewGuid().ToString("N");

        /// <summary>是否已建立 KCP 连接（供业务层判断能否发送消息）。</summary>
        public bool IsConnected => _client != null && _client.clientSession != null && _client.clientSession.IsConnected();

        /// <summary>最近一次往返延迟（毫秒）；还没测到 = -1。</summary>
        public float RttMs { get; private set; } = -1f;

        private readonly float _heartbeatInterval;
        private readonly float _hostSilentSeconds;
        private float _heartbeatLeft;
        private uint _pingId;
        private uint _lastPingId;
        private long _lastPingSentMs;

        /// <summary>"心跳发出去、还在等应答"的截止时刻（<c>Time.unscaledTime</c>）；<c>0</c> = 不在等。
        /// <para>▍为什么要有它：房主进程被**强杀**（关窗口/杀进程/拔网线）时 **UDP 没有 FIN**，
        /// KCP 库里那条会话不一定会（及时）翻成断开 ⇒ 客机唯一的判据 <c>IsConnected</c> 一直是 true
        /// ⇒ <c>NetRoomFlow.DetectHostLost</c> 永不触发 ⇒ 房主的盟友实例（模型）与 <c>PlayerWnd</c>
        /// 的队友行会**永远挂在客机屏幕上**（2026-10-14 实测）。</para>
        /// <para>▍判据：连着呢，但从"发出 Ping"起这么久没等到**任何**应答 ⇒ 认定房主已消失。</para></summary>
        private float _pongDeadline;

        /// <param name="heartbeatInterval">心跳间隔（秒）；&lt;=0 关闭心跳</param>
        /// <param name="hostSilentSeconds">心跳静默判定阈值（秒）；&lt;=0 关闭该看门狗。
        /// 应显著大于 <paramref name="heartbeatInterval"/>（默认 2s 心跳 + 5s 阈值 = 容忍连续丢 2 个应答）。</param>
        public NetClient(float heartbeatInterval, float hostSilentSeconds = 5f)
        {
            _heartbeatInterval = heartbeatInterval;
            _hostSilentSeconds = hostSilentSeconds;
            // 心跳应答：不注册的话 Dispatch 会报"未注册消息:1002"
            MessageCenter.Register<PingRspMsg>(NetCmdId.PingRsp, OnPingRsp);
        }

        /// <summary>【看门狗】连着呢，但已经超过阈值没收到房主的任何应答 ⇒ 判定"房主已消失"。
        /// <para>▍消费方：<c>NetRoomFlow.DetectHostLost</c>（把它当"没连着"处理 ⇒ 走 <c>OnHostLost</c> 收尾）。
        /// 这是"强关房主"能被客机发现的关键 —— 光靠 <see cref="IsConnected"/> 不够（见 <see cref="_pongDeadline"/>）。</para></summary>
        public bool IsHostSilent =>
            _hostSilentSeconds > 0f && IsConnected && _pongDeadline > 0f && Time.unscaledTime > _pongDeadline;

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
            // 注入本机身份：随握手上报 ⇒ 房主按 clientId 去重（换端口重连也认得出是同一个人）
            _client.ClientId = _clientId;
            // TODO(加密)：公网直连要开就**两端一起开** —— 在这里（StartAsClient 之前）写
            //   `_client.EnableEncryption = true;`，房主侧同款见 NetServer.Start。
            //   库默认 false = 明文（局域网够用）；只开一端会握手不上（走的是 RSA 换 AES 那套）。
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

            // ★ 发出去了就开始倒计时（收到任何应答会把它清掉）⇒ 房主被强杀时靠它发现
            _pongDeadline = Time.unscaledTime + _hostSilentSeconds;
        }

        /// <summary>【房主的 Pong】算一次 RTT（<see cref="PingRspMsg"/> 只带回 Id，不回显发送时刻 ⇒ 靠本地记的发送时刻相减）。</summary>
        private void OnPingRsp(PingRspMsg rsp)
        {
            if (rsp == null) return;

            // ★ 收到**任何**应答都证明房主还活着 ⇒ 先把看门狗撤掉（放在 Id 校验之前：过期应答也算"活着"的证据）
            _pongDeadline = 0f;

            if (rsp.Id != _lastPingId) return;   // 过期/错配的应答不参与 RTT 计算

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
