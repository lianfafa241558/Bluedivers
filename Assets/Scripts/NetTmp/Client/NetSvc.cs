using System.Collections.Generic;
using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{

/// <summary>
/// 【网络服务层】网络模块的"总入口"，对应旧项目的 NetSvc.cs。
///
/// ▍它的职责（新手重点）：
///   1. 创建 KCPNet 客户端并连接服务器
///   2. 提供 SendMsg() 供业务代码发送消息
///   3. 维护一个消息队列，把网络线程收到的消息转到主线程处理
///   4. 在 Update() 里取出队列中的消息，交给 MessageCenter 分发
///
/// ▍对比旧项目 NetSvc 的改进：
///   旧项目在 Update() 里用 40+ 行的巨型 switch 分发消息。
///   这里改成一行 MessageCenter.Dispatch(msg)，分发逻辑全部迁移到 MessageCenter，
///   各业务模块自己注册处理器，NetSvc 不再需要知道"每种消息该怎么处理"。
/// </summary>
public class NetSvc : MonoBehaviour
{
    /// <summary>单例实例（网络服务全局唯一）</summary>
    public static NetSvc Instance;

    /// <summary>
    /// KCP 客户端。泛型参数说明：
    ///   <ClientSession>：连接成功后使用的会话类（负责收/发/连接/断开回调）
    ///   <NetMessage>   ：这条连接收发消息的类型（我们的信封）
    /// </summary>
    private KCPNet<ClientSession, NetMessage> client;

    /// <summary>消息队列：网络线程收到的消息先进这里，主线程再取出处理（线程安全缓冲）</summary>
    private Queue<NetMessage> msgPackQue;

    /// <summary>是否已建立 KCP 连接（供业务层判断能否发送消息）</summary>
    public bool IsConnected => client != null && client.clientSession != null && client.clientSession.IsConnected();

    /// <summary>队列锁：保证多线程访问队列时安全（因为入队和出队在不同线程）</summary>
    public static readonly string pkgque_lock = "pkgque_lock";

    /// <summary>服务器地址和端口（示例值，实际从配置读取）。端口统一取 NetConfig.HostGamePort。</summary>
    private const string SRV_IP = "127.0.0.1";
    private const int SRV_PORT = NetConfig.HostGamePort;

    private void Awake()
    {
        Instance = this;
        // 局域网 P2P 模式：默认不自动连固定服务器，等成员"搜到房间按 C 回连房主"时才建立连接。
        // 如需直连已知地址（非 P2P），改成 InitSvc(true)。
        InitSvc(false);
    }

    /// <summary>
    /// 【初始化】创建消息队列。是否自动连接默认服务器由 connectDefault 控制。
    /// 在"局域网房间广播"模式下，成员不再连固定服务器，而是先发现房间再回连，
    /// 因此这里通常传 false，等找到房间后调用 ConnectToRoom()。
    /// </summary>
    /// <param name="connectDefault">是否立即连接默认服务器地址（SRV_IP:SRV_PORT）</param>
    public void InitSvc(bool connectDefault = true)
    {
        // 创建消息队列
        msgPackQue = new Queue<NetMessage>();

        if (connectDefault)
        {
            ConnectDefaultServer();
        }
    }

    /// <summary>
    /// 【连接默认服务器】用于"直连已知地址"的传统模式。
    /// 局域网房间广播模式下一般不会走到这里。
    /// </summary>
    public void ConnectDefaultServer()
    {
        Disconnect(); // 若已有连接，先断开再连
        ConnectTo((host) => host.StartAsClient(SRV_IP, SRV_PORT), (kcp) => kcp.ConnectServer(200, 5000));
    }

    /// <summary>
    /// 【回连房主】局域网房间广播找到房间后，用房间信息发起 KCP 回连。
    /// 这是"成员 → 房主"真正建立连接的核心入口。
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

        ConnectTo(
            (host) => host.StartAsClient(room.HostIp, room.HostPort),
            async (kcp) =>
            {
                // ConnectServer：每隔 interval ms 检测一次，最长等 maxintervalSum ms
                var result = await kcp.ConnectServer(200, 5000);
                Debug.Log($"[NetSvc] 回连房主 {room.HostIp}:{room.HostPort} → {(result.Success ? "成功" : result.Message)}");
                cb?.Invoke(result.Success);
            });
    }

    /// <summary>
    /// 【断开当前连接】清空客户端与消息队列，供换房间/退出时调用。
    /// </summary>
    public void Disconnect()
    {
        if (client != null)
        {
            client.CloseClient();
            client = null;
        }
        msgPackQue?.Clear();
    }

    // ==================== 房间协议：成员发起端 ====================
    // 这些方法把"房间消息"发往房主（经 NetHostSvc 处理）。
    // 房主回的消息（JoinRoomRsp/PlayerListSync/StartGameNtf）在业务层用
    // MessageCenter.Register 注册处理器接收（见 NetDemo）。

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

    /// <summary>
    /// 【离开房间】主动离开当前房间。
    /// </summary>
    public void LeaveRoom()
    {
        // 告知房主离开；房主会广播更新后的玩家列表给其他成员
        SendToHost(MessageCenter.Pack(CmdId.LeaveRoomNtf, new LeaveRoomNtf()));
    }

    /// <summary>
    /// 【准备/取消准备】切换准备状态上报给房主。
    /// </summary>
    public void SetReady(bool isReady)
    {
        SendToHost(MessageCenter.Pack(CmdId.ReadyState, new ReadyState { IsReady = isReady }));
    }

    /// <summary>向房主发送一条消息（成员端只有一条连接，直接走会话）</summary>
    private void SendToHost(NetMessage msg)
    {
        if (client != null && client.clientSession != null && client.clientSession.IsConnected())
        {
            client.clientSession.SendMsg(msg);
        }
        else
        {
            Debug.LogError("[NetSvc] 尚未连接房主，无法发送房间消息");
        }
    }

    /// <summary>
    /// 统一的"建连"流程：创建 KCP 客户端 → 启动 UDP 监听（startAction）→ 发起握手（connectAction）。
    /// 这样无论连默认服务器还是回连房主，都走同一套初始化逻辑。
    /// </summary>
    private void ConnectTo(
        System.Action<KCPNet<ClientSession, NetMessage>> startAction,
        System.Action<KCPNet<ClientSession, NetMessage>> connectAction)
    {
        // 创建 KCP 客户端（指定会话类型和消息类型）
        client = new KCPNet<ClientSession, NetMessage>();
        // 以客户端身份启动（StartAsClient：绑定本机 UDP，指向目标 IP:端口）
        startAction(client);
        // 异步发起真正的连接（connect 握手）
        connectAction(client);
    }

    /// <summary>
    /// 【发送消息】业务代码发送消息的统一入口。
    /// 旧项目对应：netSvc.SendMsg(new RTSMsg { cmd = ... })。
    /// 这里传入的是 MessageCenter 打包好的 NetMessage 信封。
    /// </summary>
    public void SendMsg(NetMessage msg, System.Action<bool> cb = null)
    {
        if (client != null && client.clientSession != null && client.clientSession.IsConnected())
        {
            // 连接正常：通过会话发送（KCPNet 会用 Serializer 自动序列化）
            client.clientSession.SendMsg(msg);
            cb?.Invoke(true);
        }
        else
        {
            Debug.LogError("服务器未连接");
            cb?.Invoke(false);
        }
    }

    /// <summary>
    /// 【入队】把网络线程收到的消息放进队列，等主线程处理。
    /// 由 ClientSession.OnReciveMsg 调用。
    /// </summary>
    public void AddMsgQue(NetMessage msg)
    {
        lock (pkgque_lock)
        {
            msgPackQue.Enqueue(msg);
        }
    }

    /// <summary>
    /// 【主线程轮询】每帧把队列里的消息取出来，交给 MessageCenter 分发。
    /// 这是"网络线程 → 主线程"的关键桥梁。
    /// 旧项目这里是一个 40+ 行的巨型 switch，现在压缩成一行分发调用。
    /// </summary>
    private void Update()
    {
        if (msgPackQue == null) return;

        // 循环取出队列里所有待处理的消息
        while (msgPackQue.Count > 0)
        {
            lock (pkgque_lock)
            {
                NetMessage msg = msgPackQue.Dequeue();
                // 核心：交给 MessageCenter 按 CmdId 查表分发（替代巨型 switch）
                MessageCenter.Dispatch(msg);
            }
        }
    }

    /// <summary>销毁时清理（断开连接、清空队列）</summary>
    private void OnDestroy()
    {
        Disconnect();
        msgPackQue = null;
        Instance = null;
    }
}
}
