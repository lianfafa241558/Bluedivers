using System.Collections.Generic;
using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{

/// <summary>
/// 【房主端网络服务层】房主权威模式下，房主这台机器的网络总入口。
/// 对应成员端的 NetSvc，但角色是"服务器/房主"。
///
/// ▍职责（新手重点）：
///   1. 以服务器身份 StartAsServer 监听端口，等待成员回连。
///   2. 维护成员会话列表 + 玩家信息（谁加入了、是否准备）。
///   3. 处理房间业务协议：加入/离开/准备/开始游戏（见 CmdId.Room*）。
///   4. 提供 SendToAll / SendToSession，实现房主权威的"转发/广播"。
///   5. 用消息队列把网络线程消息转到主线程，交给 MessageCenter 分发。
///   6. 联动 LanBroadcaster：把监听端口注入房间广播，成员才能发现并回连。
///
/// ▍房主权威模式的通信模型：
///   成员 --消息--> 房主：房主收到后决定逻辑（可改），再 SendToAll 转发给全体。
///   这样状态变更以房主为准，成员只渲染结果，天然防作弊、易同步。
///
/// ▍房间协议闭环（成员经 NetSvc 发起）：
///   成员 JoinRoomReq → 房主校验(满员/密码) → JoinRoomRsp(结果)
///   成功 → 房主广播 PlayerListSync 给全体
///   成员 ReadyState → 房主汇总 → 广播 PlayerListSync
///   房主认为可开局 → 广播 StartGameNtf
///
/// ▍使用方式（挂到房主机的 GameObject 上）：
///   NetHostSvc.Instance.StartHost(roomName, map, maxPlayers);  // 开房+广播
///   NetHostSvc.Instance.StopHost();                             // 关房
/// </summary>
public class NetHostSvc : MonoBehaviour
{
    /// <summary>单例实例（房主端全局唯一）</summary>
    public static NetHostSvc Instance;

    /// <summary>一条来自成员的待处理消息（NetMessage + 来源 sid）</summary>
    public struct HostMsg
    {
        public NetMessage Msg;
        public uint Sid;       // 来源成员的会话ID
    }

    /// <summary>
    /// KCP 服务器（房主）。泛型：
    ///   &lt;HostSession&gt;：代表一个成员的会话类（收/发/连接/断开回调）
    ///   &lt;NetMessage&gt;  ：这条连接收发消息的类型（我们的信封）
    /// </summary>
    private KCPNet<HostSession, NetMessage> host;

    /// <summary>房主 KCP 监听端口（成员回连用，也是广播进 HostPort 的值）。默认取 NetConfig.HostGamePort。</summary>
    [SerializeField] private int hostPort = NetConfig.HostGamePort;

    /// <summary>房主局域网广播器（把房间信息广播出去）</summary>
    private LanBroadcaster _broadcaster;

    /// <summary>消息队列：网络线程收到的消息先进这里，主线程再取出处理（线程安全缓冲）</summary>
    private Queue<HostMsg> msgPackQue;

    /// <summary>队列锁</summary>
    public static readonly string pkgque_lock = "pkgque_lock";

    /// <summary>当前房间信息（供广播用）</summary>
    public LanRoomInfo RoomInfo { get; private set; }

    /// <summary>房间最大人数（含房主）</summary>
    public int MaxPlayers { get; private set; } = 4;

    /// <summary>房间密码（空表示无密码）</summary>
    private string _roomPassword = "";

    /// <summary>本机作为房主时的自标识名称（运行时时间戳生成，用于成员端排除"自己开的房"）</summary>
    private string _hostSelfName = null;

    /// <summary>成员玩家信息表：sid -> PlayerInfo（不含房主自己）</summary>
    private readonly Dictionary<uint, PlayerInfo> _players = new Dictionary<uint, PlayerInfo>();

    /// <summary>当前在线成员数（不含房主）</summary>
    public int MemberCount => _players.Count;

    /// <summary>当前可加入的玩家总数（含房主自己）</summary>
    public int TotalPlayers => 1 + _players.Count;

    private void Awake()
    {
        Instance = this;
        msgPackQue = new Queue<HostMsg>();
        // 注册房间协议处理器（在主线程由 Update 分发时触发）
        MessageCenter.Register<JoinRoomReq>(CmdId.JoinRoomReq, OnJoinRoomReq);
        MessageCenter.Register<LeaveRoomNtf>(CmdId.LeaveRoomNtf, OnLeaveRoomNtf);
        MessageCenter.Register<ReadyState>(CmdId.ReadyState, OnReadyState);
    }

    /// <summary>
    /// 【开房】以服务器身份启动监听 + 广播房间。这是房主权威模式的入口。
    /// </summary>
    /// <param name="roomName">房间名</param>
    /// <param name="mapName">地图名（可选）</param>
    /// <param name="maxPlayers">最大人数（含房主）</param>
    /// <param name="password">房间密码（空表示无密码）</param>
    public void StartHost(string roomName, string mapName = "", int maxPlayers = 4, string password = "")
    {
        MaxPlayers = Mathf.Max(1, maxPlayers);
        _roomPassword = password ?? "";
        _players.Clear();

        // ① 创建 KCP 服务器（房主）
        host = new KCPNet<HostSession, NetMessage>();

        // 以"服务器"身份监听端口，等待成员回连。
        // 说明：P2P 模式下 DLL 直接放客户端，房主客户端内嵌一个迷你"服务器"，
        //       它只负责建连/转发，真正的游戏逻辑在房主本地的 Unity 主线程跑。
        // ⚠️ UDP 端口关闭后不会立刻释放（TIME_WAIT），若刚关房立即重开同一端口，
        //    会抛 SocketException。这里捕获并转成清晰报错，避免中断主循环。
        try
        {
            host.StartAsServer("0.0.0.0", hostPort);
        }
        catch (System.Net.Sockets.SocketException e)
        {
            host = null;
            _broadcaster?.Stop();
            _broadcaster = null;
            RoomInfo = null;
            KCPTool.ColorLog(KCPLogColor.Red, "开房失败，端口 {0} 可能尚未释放:{1}。请稍等片刻再开房。", hostPort, e.Message);
            throw new System.Exception($"[NetHostSvc] 开房失败，端口 {hostPort} 尚未释放（刚关房后需等待几秒）。原因:{e.Message}");
        }

        // 监听成员进出，用于更新房间人数/清理成员数据
        host.OnSessionConnected += OnMemberJoined;
        host.OnSessionDisconnected += OnMemberLeft;

        KCPTool.ColorLog(KCPLogColor.Green, "房主服务器已启动，监听端口 {0}", hostPort);

        // ② 构造房间信息并广播（成员发现房间后用它回连 hostPort）
        // 房主名称用"运行时时间戳"生成，每次开房都不同，成员端可据此精确排除"自己开的房"，
        // 同机多个实例也不会互相误判。
        _hostSelfName = $"Host_{System.DateTime.UtcNow.ToString("HHmmssfff")}";
        RoomInfo = new LanRoomInfo
        {
            RoomName = roomName,
            HostPort = hostPort,
            PlayerCount = 1,
            MaxPlayers = MaxPlayers,
            MapName = mapName,
            PasswordProtected = !string.IsNullOrEmpty(_roomPassword),
            Version = Application.version,
            PlayerNames = new[] { _hostSelfName },
        };

        // ③ 动态提供最新人数给广播（成员加入/离开时广播会自动更新）
        _broadcaster = new LanBroadcaster(GetBroadcastInfo());
        _broadcaster.Start();
    }

    /// <summary>
    /// 【关房】停止广播并关闭服务器监听。
    /// </summary>
    public void StopHost()
    {
        _broadcaster?.Stop();
        _broadcaster = null;

        if (host != null)
        {
            host.OnSessionConnected -= OnMemberJoined;
            host.OnSessionDisconnected -= OnMemberLeft;
            host.CloseServer();
            host = null;
        }
        RoomInfo = null;
        _hostSelfName = null;
        _players.Clear();
        KCPTool.ColorLog(KCPLogColor.Cyan, "房主服务器已停止");
    }

    /// <summary>
    /// 每次广播前调用，动态返回最新房间信息（人数会随成员进出变化）。
    /// </summary>
    private LanRoomInfo GetBroadcastInfo()
    {
        if (RoomInfo == null) return null;
        RoomInfo.PlayerCount = TotalPlayers; // 房主自己 + 已入房成员
        return RoomInfo;
    }

    // ==================== 成员进出（传输层事件） ====================
    /// <summary>成员建立 TCP/KCP 会话（此时还没 JoinRoom，只是连上了）</summary>
    private void OnMemberJoined(uint sid)
    {
        Debug.Log($"[NetHostSvc] 成员建立连接 sid={sid}（尚未入房）");
    }

    /// <summary>成员会话断开：若已入房，从房间移除并广播玩家列表</summary>
    private void OnMemberLeft(uint sid)
    {
        if (_players.Remove(sid))
        {
            Debug.Log($"[NetHostSvc] 成员离开 sid={sid}，剩余玩家 {TotalPlayers}");
            BroadcastPlayerList();
        }
        else
        {
            Debug.Log($"[NetHostSvc] 成员断连 sid={sid}（未入房）");
        }
    }

    // ==================== 房间业务协议处理 ====================
    /// <summary>
    /// 处理成员加入请求。校验满员/密码，成功则入房并广播玩家列表。
    /// </summary>
    private void OnJoinRoomReq(JoinRoomReq req)
    {
        uint sid = CurrentSid;
        if (sid == 0) return;

        // 满员检查（含房主自己）
        if (TotalPlayers >= MaxPlayers)
        {
            SendJoinFail(sid, "房间已满");
            return;
        }
        // 密码检查
        if (!string.IsNullOrEmpty(_roomPassword) && req.Password != _roomPassword)
        {
            SendJoinFail(sid, "密码错误");
            return;
        }

        // 加入成功：记录玩家信息
        var me = new PlayerInfo
        {
            Sid = sid,
            PlayerName = string.IsNullOrEmpty(req.PlayerName) ? $"玩家{sid}" : req.PlayerName,
            IsReady = false,
            IsHost = false,
        };
        _players[sid] = me;

        // 回给加入者：成功 + 自己信息 + 当前玩家列表（含房主）
        var rsp = new JoinRoomRsp
        {
            ErrorCode = 0,
            Self = me,
            Players = BuildPlayerArray(),
        };
        SendToSession(sid, MessageCenter.Pack(CmdId.JoinRoomRsp, rsp));

        Debug.Log($"[NetHostSvc] 玩家 {me.PlayerName} 加入房间，当前玩家 {TotalPlayers}/{MaxPlayers}");

        // 广播最新玩家列表给所有成员
        BroadcastPlayerList();
    }

    /// <summary>处理成员离开房间</summary>
    private void OnLeaveRoomNtf(LeaveRoomNtf ntf)
    {
        uint sid = CurrentSid;
        if (_players.Remove(sid))
        {
            Debug.Log($"[NetHostSvc] 玩家离开房间 sid={sid}，当前玩家 {TotalPlayers}/{MaxPlayers}");
            BroadcastPlayerList();
        }
    }

    /// <summary>处理成员准备状态切换</summary>
    private void OnReadyState(ReadyState st)
    {
        uint sid = CurrentSid;
        if (_players.TryGetValue(sid, out PlayerInfo p))
        {
            p.IsReady = st.IsReady;
            Debug.Log($"[NetHostSvc] 玩家 {p.PlayerName} 准备状态 → {st.IsReady}");
            BroadcastPlayerList();
        }
    }

    /// <summary>
    /// 【开战】房主主动广播开始游戏。可在所有玩家都准备后由业务层调用。
    /// </summary>
    public void StartGame(string mapName = "")
    {
        var ntf = new StartGameNtf { MapName = string.IsNullOrEmpty(mapName) ? RoomInfo?.MapName : mapName };
        SendToAll(MessageCenter.Pack(CmdId.StartGameNtf, ntf));
        Debug.Log("[NetHostSvc] 已广播开始游戏");
    }

    // ==================== 房间协议辅助 ====================
    /// <summary>当前正在主线程分发的这条消息来自哪个成员（由 Update 出队时设置）</summary>
    private uint CurrentSid { get; set; }

    /// <summary>向某个成员回"加入失败"</summary>
    private void SendJoinFail(uint sid, string reason)
    {
        SendToSession(sid, MessageCenter.Pack(CmdId.JoinRoomRsp, new JoinRoomRsp
        {
            ErrorCode = 1,
            Reason = reason,
        }));
    }

    /// <summary>把玩家列表广播给所有成员</summary>
    private void BroadcastPlayerList()
    {
        SendToAll(MessageCenter.Pack(CmdId.PlayerListSync, new PlayerListSync
        {
            Players = BuildPlayerArray(),
        }));
    }

    /// <summary>构造完整玩家数组（房主 + 所有成员），房主排第一个</summary>
    private PlayerInfo[] BuildPlayerArray()
    {
        var list = new List<PlayerInfo>(_players.Count + 1)
        {
            new PlayerInfo { Sid = 0, PlayerName = "房主", IsHost = true, IsReady = true },
        };
        list.AddRange(_players.Values);
        return list.ToArray();
    }

    // ==================== 发送 / 广播（房主权威核心） ====================
    /// <summary>广播给所有成员（不含房主本地）。</summary>
    public void SendToAll(NetMessage msg)
    {
        host?.SendToAll(msg);
    }

    /// <summary>定向发送给指定成员（按 sid）。房主权威下常用来做私密回复。</summary>
    public void SendToSession(uint sid, NetMessage msg)
    {
        if (host != null && host.TryGetSession(sid, out HostSession s) && s.IsConnected())
        {
            s.SendMsg(msg);
        }
    }

    // ==================== 消息队列：网络线程 → 主线程 ====================
    /// <summary>
    /// 【入队】由 HostSession.OnReciveMsg 调用，把网络线程消息转到主线程。
    /// 必须带上来源 sid。
    /// </summary>
    public void AddMsgQue(NetMessage msg, uint sid)
    {
        lock (pkgque_lock)
        {
            msgPackQue.Enqueue(new HostMsg { Msg = msg, Sid = sid });
        }
    }

    /// <summary>
    /// 【主线程轮询】每帧把队列里的消息取出来，交给 MessageCenter 分发。
    /// </summary>
    private void Update()
    {
        if (msgPackQue == null) return;
        while (msgPackQue.Count > 0)
        {
            HostMsg hm;
            lock (pkgque_lock)
            {
                hm = msgPackQue.Dequeue();
            }
            // 记录当前消息来源，供房间协议处理器识别是哪个成员发的
            CurrentSid = hm.Sid;
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

    /// <summary>销毁时清理</summary>
    private void OnDestroy()
    {
        MessageCenter.Unregister(CmdId.JoinRoomReq);
        MessageCenter.Unregister(CmdId.LeaveRoomNtf);
        MessageCenter.Unregister(CmdId.ReadyState);
        StopHost();
        msgPackQue = null;
        Instance = null;
    }
}
}
