using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{
    /// <summary>
    /// 【成员端网络入口】游戏侧：持一个传输层 <see cref="NetClient"/>（02_Net），
    /// 自己只负责"房间协议"这类**游戏报文**的发送 + 每帧泵。
    ///
    /// <para>▍分工：连接/心跳/RTT/线程队列全在 <see cref="NetClient"/>（纯传输）；
    /// 本类只保留游戏动词（JoinRoom / LeaveRoom / SetReady）与业务发送入口 <see cref="SendMsg"/>。
    /// 这样"换一套传输实现"不需要动这里，也不需要在 prefab 上加组件（本类仍是
    /// <c>GameRoot.prefab</c> 上那个组件，类型名与 GUID 未变）。</para>
    /// </summary>
    public class NetSvc : MonoBehaviour
    {
        /// <summary>单例实例（网络服务全局唯一）</summary>
        public static NetSvc Instance;

        /// <summary>心跳间隔（秒）。<=0 关闭心跳。⚠ prefab（GameRoot）里的值会覆盖这里的默认值。</summary>
        [InspectorName("心跳间隔(秒)")]
        [Tooltip("成员定时发 Ping 给房主（房主回 Pong）：既保活（库内 15s 收不到数据会判掉线），也让房主能快速发现强退。<=0 关闭")]
        [SerializeField] private float heartbeatInterval = 2f;

        /// <summary>传输层成员连接（由本类创建并每帧泵）。</summary>
        private NetClient _client;

        /// <summary>是否已建立 KCP 连接（供业务层判断能否发送消息）</summary>
        public bool IsConnected => _client != null && _client.IsConnected;

        /// <summary>最近一次往返延迟（毫秒）；还没测到 = -1。</summary>
        public float RttMs => _client != null ? _client.RttMs : -1f;

        private void Awake()
        {
            Instance = this;
            // 局域网 P2P 模式：默认不自动连固定服务器，等成员"搜到房间"时才建立连接。
            _client = new NetClient(heartbeatInterval);
        }

        /// <summary>
        /// 【主线程轮询】心跳 + 分发入站消息（都在传输层 <see cref="NetClient.Pump"/> 里）。
        /// </summary>
        private void Update()
        {
            _client?.Pump();
        }

        // ==================== 连接（转发传输层） ====================

        /// <summary>
        /// 【回连房主】局域网房间广播找到房间后，用房间信息发起 KCP 回连。
        /// </summary>
        /// <param name="room">发现到的房间信息（用其 HostIp + HostPort 回连）</param>
        /// <param name="cb">连接结果回调（true=成功，false=失败），在异步连接结束后调用</param>
        public void ConnectToRoom(LanRoomInfo room, System.Action<bool> cb = null)
        {
            _client?.ConnectToRoom(room, cb);
        }

        /// <summary>【断开当前连接】供换房间/退出时调用。</summary>
        public void Disconnect()
        {
            _client?.Disconnect();
        }

        // ==================== 房间协议：成员发起端 ====================
        // 这些方法把"房间消息"发往房主（经 NetHostSvc 处理）。
        // 房主回的消息（JoinRoomRsp/PlayerListSync/TaskConfirmNtf）由业务层用
        // MessageCenter.Register 注册处理器接收。

        /// <summary>
        /// 【加入房间】回连成功后，向房主申请进房。
        /// </summary>
        /// <param name="playerName">玩家名</param>
        /// <param name="password">房间密码（无则传空）</param>
        public void JoinRoom(string playerName, string password = "")
        {
            SendToHost(MessageCenter.Pack(CmdId.JoinRoomReq, new JoinRoomReq
            {
                PlayerName = playerName,
                Password = password,
            }));
        }

        /// <summary>【离开房间】主动离开当前房间。</summary>
        public void LeaveRoom()
        {
            // 告知房主离开；房主会广播更新后的玩家列表给其他成员
            SendToHost(MessageCenter.Pack(CmdId.LeaveRoomNtf, new LeaveRoomNtf()));
        }

        /// <summary>【准备/取消准备】切换准备状态上报给房主。</summary>
        public void SetReady(bool isReady)
        {
            SendToHost(MessageCenter.Pack(CmdId.ReadyState, new ReadyState { IsReady = isReady }));
        }

        /// <summary>向房主发送一条消息（成员端只有一条连接，直接走传输层）</summary>
        private void SendToHost(NetMessage msg)
        {
            if (_client != null && _client.IsConnected)
            {
                _client.Send(msg);
            }
            else
            {
                Debug.LogError("[NetSvc] 尚未连接房主，无法发送房间消息");
            }
        }

        /// <summary>
        /// 【发送消息】业务代码发送消息的统一入口（走传输层 <see cref="NetClient"/>）。
        /// </summary>
        public void SendMsg(NetMessage msg, System.Action<bool> cb = null)
        {
            if (_client != null && _client.IsConnected)
            {
                _client.Send(msg);
                cb?.Invoke(true);
            }
            else
            {
                Debug.LogError("服务器未连接");
                cb?.Invoke(false);
            }
        }

        /// <summary>销毁时清理（退订心跳应答 + 断开连接）</summary>
        private void OnDestroy()
        {
            _client?.Dispose();
            _client = null;
            Instance = null;
        }
    }
}
