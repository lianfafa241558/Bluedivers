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
/// ▍房间协议闭环（成员经 NetSvc 发起；阶段链 = Bridge 选任务 → Ready → Armament → Transition → Game）：
///   成员 JoinRoomReq → 房主校验(满员 / 密码 / 是否已进战斗) → JoinRoomRsp(结果) [+ 补发本局配置]
///   成功 → 房主广播 PlayerListSync 给全体
///   成员 ReadyState → 房主汇总 → 广播 PlayerListSync
///   房主在 Bridge 选完任务 → 广播 TaskConfirmNtf（双方进 Ready；**收人窗口只有 Ready**）
///   所有人就位 → Armament：`CloseJoin()` 关闸（不再收人，但名单继续同步——准备状态要靠它）
///   全员准备 → 房主广播 TransitionNtf（同时开始加载战斗）+ **冻结名单**
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
    [InspectorName("监听端口")]
    [SerializeField] private int hostPort = NetConfig.HostGamePort;

    /// <summary>房主局域网广播器（把房间信息广播出去）</summary>
    private LanBroadcaster _broadcaster;

    /// <summary>消息队列：网络线程收到的消息先进这里，主线程再取出处理（线程安全缓冲）</summary>
    private Queue<HostMsg> msgPackQue;

    /// <summary>会话事件（成员建立连接 / 断开）。</summary>
    private struct SessionEvent
    {
        public uint Sid;
        public bool Joined;
    }

    /// <summary>
    /// 会话事件队列 —— 由**传输线程**入队、**主线程**处理。
    ///
    /// <para>▍为什么必须过一遍队列：<c>host.OnSessionConnected/OnSessionDisconnected</c> 是 KCP
    /// <c>KCPSession.UpdateAsync</c> 的续体，跑在 **ThreadPool 线程**上（不是主线程）；而
    /// "成员离开 → <c>BroadcastPlayerList()</c> → 09 侧桥 <c>NetFriendBridge.HandleRoster</c> →
    /// <c>ResSvc.LoadRes</c> / <c>Instantiate</c>" 全程都是 Unity API ⇒ 直接在回调里跑会抛
    /// <c>UnityException: Load can only be called from the main thread</c>（2026-10-06 打包版实测），
    /// 而且它会在<b>广播名单中途</b>炸掉，导致该清退的盟友留在场上变成幽灵。</para>
    /// </summary>
    private readonly Queue<SessionEvent> _sessionEvents = new Queue<SessionEvent>();

    /// <summary>队列锁</summary>
    public static readonly string pkgque_lock = "pkgque_lock";

    /// <summary>当前房间信息（供广播用）</summary>
    public LanRoomInfo RoomInfo { get; private set; }

    /// <summary>房间最大人数（含房主）</summary>
    public int MaxPlayers { get; private set; } = 4;

    /// <summary>房间密码（空表示无密码）</summary>
    private string _roomPassword = "";

    /// <summary>本房难度（<c>DifficultyEnum</c> 的 int；-1 = 未指定）。随广播下发（<c>LanRoomInfo.Difficulty</c>），
    /// 同时 <c>TaskConfirmNtf</c> 也带一份给成员复现难度。</summary>
    private int _difficulty = -1;

    /// <summary>本房**主任务类型枚举值**（<c>MissionEnum</c> 的 int；-1 = 未知/还没选任务）。随广播下发，
    /// 房间列表据此**精确**取任务图标/颜色（任务类型名不唯一，只靠名字会取错）。
    /// ⚠ 本类在 <c>02_Net</c>、看不见 <c>MissionEnum</c>（asmdef references 为空）⇒ 只存 <c>int</c>。</summary>
    private int _taskMain = -1;

    /// <summary>房主玩家名（**显示用**，与"排除自己开的房"的合成名 <see cref="_hostSelfName"/> 区分开）。</summary>
    private string _hostName = "房主";

    /// <summary>本房是否已进战斗（= 已过 <see cref="NotifyTransition"/>；此时不再收人）。
    /// <para>随广播下发（<c>LanRoomInfo.InGame</c>）⇒ 成员端能筛掉"没满员但已进游戏"的房间。</para></summary>
    private bool _started;

    /// <summary>本机作为房主时的自标识名称（运行时时间戳生成，用于成员端排除"自己开的房"）</summary>
    private string _hostSelfName = null;

    /// <summary>成员玩家信息表：sid -> PlayerInfo（不含房主自己）</summary>
    private readonly Dictionary<uint, PlayerInfo> _players = new Dictionary<uint, PlayerInfo>();

    /// <summary>成员资料表：sid -> <see cref="PlayerProfile"/>（角色/武器/战备/强化，由成员自己上报）。</summary>
    private readonly Dictionary<uint, PlayerProfile> _profiles = new Dictionary<uint, PlayerProfile>();

    /// <summary>房主自己的资料（角色/武器/战备/强化）。由 09 侧的桥在资料变化时写入，见 <see cref="SetLocalProfile"/>。</summary>
    public PlayerProfile HostProfile { get; private set; }

    /// <summary>房主自己的就绪状态。⚠ **不写死 true**：转场要求"所有玩家都就绪"，房主与成员同一口径
    /// （2026-10-07 用户口径）—— 写死会让成员永远看到房主"已就绪"，房主点取消也没人知道。</summary>
    private bool _hostReady;

    /// <summary>房主自己的当前就绪状态（成员端靠名单里的这份）。</summary>
    public bool HostReady => _hostReady;

    /// <summary>【房主本地】设置房主自己的就绪并广播。与成员走 <c>ReadyState → OnReadyState</c> 是同一口径。</summary>
    public void SetHostReady(bool ready)
    {
        if (_hostReady == ready) return;
        _hostReady = ready;
        if (RoomInfo != null) BroadcastPlayerList();
    }

    // ==================== 位姿聚合（房主权威） ====================

    /// <summary>
    /// 【下行频率】房主把所有实体的最新位姿按这个频率聚合广播一次。
    /// <para>▍为什么不"收到就转发"：那只在有变化时发包，人数一多包数就爆炸；
    /// 固定频率聚合后，报文条数与实体数解耦（一条批次带全部）。</para>
    /// <para>⚠ 改这个值要同步接收端的 <c>NetTransformView.RenderDelay</c>（约 2 个快照间隔）与 <c>BufferSize</c>
    /// （缓冲要覆盖住延迟）；20Hz 下每快照每实体 ~25B，带宽可忽略。⚠ prefab（`GameRoot.prefab`）里的值会覆盖这里。</para>
    /// </summary>
    [InspectorName("位姿下行频率(Hz)")]
    [SerializeField] private float poseBroadcastHz = 20f;

    /// <summary>sid → 该实体最新位姿（房主自己 + 全部成员）。</summary>
    private readonly Dictionary<uint, PoseSnapshot> _poses = new Dictionary<uint, PoseSnapshot>();

    /// <summary>
    /// 【空闲踢人】超过这么久没收到某个成员的任何消息（含心跳），就按"他离开了"处理。
    /// <para>▍为什么需要：成员被<b>强杀</b>（关进程/断网）时 UDP 是无连接的，房主收不到"我走了"，
    /// 库内的会话超时（<c>TimeoutMs=15000</c>）也要等 15 秒 ⇒ 这段时间里该成员的盟友实例一直挂在场上。
    /// 心跳间隔见 <c>NetSvc.heartbeatInterval</c>（默认 2s），这里给 4 次余量。</para>
    /// </summary>
    [InspectorName("成员空闲超时(秒)")]
    [Tooltip("超过这么久收不到该成员的任何消息（含心跳）就踢出房间；<=0 关闭该机制")]
    [SerializeField] private float memberIdleTimeout = 8f;

    /// <summary>sid → 最后一次收到该成员消息的时刻（<c>Time.unscaledTime</c>）。
    /// <para>⚠ 只在主线程读写（写入点在 <see cref="Update"/> 出队时，读取点在 <see cref="EvictIdleMembers"/>）⇒ 不需要锁。</para></summary>
    private readonly Dictionary<uint, float> _lastSeen = new Dictionary<uint, float>();

    /// <summary>踢人时的暂存表（避免遍历 <see cref="_players"/> 时修改它，也避免每帧产生垃圾）</summary>
    private readonly List<uint> _idleBuffer = new List<uint>();

    /// <summary>**本端主动关掉**的会话 sid（离开房间 / 空闲踢人）。库随后回调的断开事件只会再清一遍痕迹，
    /// 不再重复提示 —— 否则同一个人会先"成员离开"、隔一帧又"成员断连（未入房）"（2026-10-07 用户实测）。</summary>
    private readonly HashSet<uint> _selfClosed = new HashSet<uint>();

    /// <summary>已经提示过"未入房的会话断了"的 sid（同一连接循环重连时只提示一次，别刷屏）。
    /// 数量受"曾经连上过的连接数"限制，且开房/关房时清空。</summary>
    private readonly HashSet<uint> _ghostLogged = new HashSet<uint>();

    /// <summary>当前局号（由 <see cref="SetLocalPose"/> 带入，随批次下发；0 = 大厅）</summary>
    private int _poseMatchId;

    private float _poseSendLeft;

    /// <summary>【房主本地】写入房主自己的位姿（由 09 侧的桥每帧调用）。</summary>
    public void SetLocalPose(PoseSnapshot snap, int matchId)
    {
        if (snap == null) return;
        snap.Sid = 0;                       // 房主的权威标识恒为 0
        _poseMatchId = matchId;
        _poses[0] = snap;
    }

    /// <summary>【成员上行】收到某个成员的位姿。<c>Sid</c> 一律以当前会话为准（不采信 DTO 里带的）。</summary>
    private void OnPoseUp(PoseBatchMsg batch)
    {
        uint sid = CurrentSid;
        if (sid == 0 || batch == null || batch.Items == null) return;
        if (!_players.ContainsKey(sid)) return;     // 还没入房就上报 → 忽略

        // 单条上行：只取第一项，并按会话 sid 覆写身份
        var snap = batch.Items[0];
        if (snap == null) return;
        snap.Sid = sid;
        _poseMatchId = batch.MatchId;
        _poses[sid] = snap;
    }

    /// <summary>【下行】按固定频率聚合广播，并**本地自派发一次**（房主自己的桥也要处理成员的位姿）。</summary>
    private void BroadcastPoses()
    {
        if (_poses.Count == 0) return;
        if (RoomInfo == null) return;               // 没开房（单机）不广播

        _poseSendLeft -= Time.deltaTime;
        if (_poseSendLeft > 0f) return;
        _poseSendLeft = 1f / Mathf.Max(1f, poseBroadcastHz);

        var items = new PoseSnapshot[_poses.Count];
        _poses.Values.CopyTo(items, 0);

        var msg = MessageCenter.Pack(CmdId.TransformBatchSync,
            new PoseBatchMsg { MatchId = _poseMatchId, Items = items });
        SendToAll(msg);
        MessageCenter.Dispatch(msg);   // 房主本地自派发（与 BroadcastPlayerList 同款）
    }

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
        // 舰桥准备（战备/强化/资料）：成员上行到房主，房主校验后转发（见 RelayToAll）
        MessageCenter.Register<PlayerProfileNtf>(CmdId.PlayerProfileNtf, OnPlayerProfileNtf);
        MessageCenter.Register<PlayerArmamentNtf>(CmdId.PlayerArmamentNtf, OnPlayerArmamentNtf);
        // 局内战备呼叫：成员上行 → 房主转发给全体（房主自己那条不走这里，见 NetRoomFlow.SendAirdropCall）
        MessageCenter.Register<AirdropCallMsg>(CmdId.AirdropCallNtf, OnAirdropCallNtf);
        // 角色喊话：成员上行 → 房主转发给全体
        MessageCenter.Register<PlayerSpeechMsg>(CmdId.SpeechNtf, OnSpeechNtf);
        MessageCenter.Register<PlayerBoosterNtf>(CmdId.PlayerBoosterNtf, OnPlayerBoosterNtf);
        // 局内表现同步（切枪 / 开火）：成员上行 → 房主转发给全体
        MessageCenter.Register<PlayerWeaponSwitch>(CmdId.PlayerWeaponSwitchNtf, OnPlayerWeaponSwitchNtf);
        MessageCenter.Register<PlayerShoot>(CmdId.PlayerShootNtf, OnPlayerShootNtf);
        MessageCenter.Register<PlayerVital>(CmdId.PlayerVitalNtf, OnPlayerVitalNtf);
        MessageCenter.Register<PlayerAmmo>(CmdId.PlayerAmmoNtf, OnPlayerAmmoNtf);
        // 位姿同步：成员上行 → 房主聚合（下行在 Update 里按固定频率广播）
        MessageCenter.Register<PoseBatchMsg>(CmdId.PlayerTransformUp, OnPoseUp);
        // 心跳：成员定时发 Ping，房主回 Pong。
        // ① 让成员端"收不到数据就判掉线"的库内检查不会误杀健康的空转连接（舰桥阶段两端本来没业务报文）；
        // ② 顺带成为"这个成员还活着吗"的依据（见 EvictIdleMembers）。
        MessageCenter.Register<PingReqMsg>(CmdId.PingReq, OnPingReq);
    }

    /// <summary>
    /// 【开房】以服务器身份启动监听 + 广播房间（旧签名的兼容重载，<c>LanRoomDemo</c> 在用）。
    /// 新代码请用 <see cref="StartHost(HostRoomOptions)"/> —— 它能带难度与房主名。
    /// </summary>
    public void StartHost(string roomName, string mapName = "", int maxPlayers = 4, string password = "")
    {
        StartHost(new HostRoomOptions
        {
            RoomName = roomName,
            MapName = mapName,
            MaxPlayers = maxPlayers,
            Password = password,
            Difficulty = -1,
            HostName = "",
        });
    }

    /// <summary>
    /// 【开房】以服务器身份启动监听 + 广播房间。这是房主权威模式的入口。
    /// </summary>
    /// <param name="options">开房参数（房间名 / 地图 / 人数 / 密码 / 难度 / 房主名）</param>
    public void StartHost(HostRoomOptions options)
    {
        MaxPlayers = Mathf.Max(1, options.MaxPlayers);
        _roomPassword = options.Password ?? "";
        _difficulty = options.Difficulty;
        _taskMain = options.TaskMain;
        _hostName = string.IsNullOrEmpty(options.HostName) ? "房主" : options.HostName;
        _started = false;
        _rosterFrozen = false;        // 新房间：名单未冻结、入房闸门打开
        _joinClosed = false;
        _lastTaskConfirm = null;      // 也没有"上一局配置"可补发
        _matchId = 0;
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
            RoomName = options.RoomName,
            HostPort = hostPort,
            PlayerCount = 1,
            MaxPlayers = MaxPlayers,
            MapName = options.MapName,
            Difficulty = _difficulty,
            TaskMain = _taskMain,
            InGame = false,
            // 当前唯一的房间来源就是局域网广播（服务器列表尚未实现）⇒ 恒定 0
            Source = (int)RoomMeta.SourceEnum.LanBroadcast,
            PasswordProtected = !string.IsNullOrEmpty(_roomPassword),
            Version = Application.version,
            // ⚠ PlayerNames[0] 必须是这个**合成名**（成员端靠 IndexOf 排除自己开的房），
            //   真名追加在末尾，不影响那条排除逻辑。
            PlayerNames = BuildHostPlayerNames(),
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
        _hostName = "房主";
        _difficulty = -1;
        _taskMain = -1;
        _started = false;
        _rosterFrozen = false;
        _joinClosed = false;
        _hostReady = false;      // 新房：房主也是"未就绪"，等他点
        _lastTaskConfirm = null;
        _players.Clear();
        _profiles.Clear();
        _poses.Clear();          // 关房：位姿表一并清掉（否则下一房会带出旧位姿）
        _lastSeen.Clear();       // 心跳计时也一并清（新房里重新计）
        _selfClosed.Clear();     // 主动关会话的登记表同理（新房的 sid 从零开始）
        _ghostLogged.Clear();    // 同上
        HostProfile = null;
        KCPTool.ColorLog(KCPLogColor.Cyan, "房主服务器已停止");
    }

    // ==================== 房间信息（供 UI 读） ====================

    /// <summary>本机房主的自标识名（成员端用它排除"自己开的房"）。未开房时为 null。</summary>
    public string SelfHostName => _hostSelfName;

    /// <summary>房主玩家名（显示用）。</summary>
    public string HostPlayerName => _hostName;

    /// <summary>本房难度（-1 = 未指定）。</summary>
    public int Difficulty => _difficulty;

    /// <summary>本房是否已进战斗（Transition 之后不再收人）。</summary>
    public bool IsStarted => _started;

    /// <summary>
    /// 广播用的玩家名数组：<c>[0]</c> = 排除自己开的房用的合成名，末尾追加真房主名。
    /// <para>⚠ 别把真名挪到 <c>[0]</c>：成员端 <c>Array.IndexOf(PlayerNames, selfHostName)</c> 靠第一位。</para>
    /// </summary>
    private string[] BuildHostPlayerNames()
    {
        return string.IsNullOrEmpty(_hostName)
            ? new[] { _hostSelfName }
            : new[] { _hostSelfName, _hostName };
    }

    /// <summary>
    /// 每次广播前调用，动态返回最新房间信息（人数 / 是否已开局会变）。
    /// </summary>
    private LanRoomInfo GetBroadcastInfo()
    {
        if (RoomInfo == null) return null;
        RoomInfo.PlayerCount = TotalPlayers; // 房主自己 + 已入房成员
        RoomInfo.InGame = _started;          // 已进战斗 ⇒ 房间列表显示「进行中」且不给加入
        return RoomInfo;
    }

    // ==================== 成员进出（传输层事件 → 只入队） ====================
    /// <summary>成员建立 TCP/KCP 会话（此时还没 JoinRoom，只是连上了）。
    /// <para>⚠ 本方法跑在**传输线程**（见 <see cref="_sessionEvents"/>）⇒ 这里只入队，别动任何游戏状态。</para></summary>
    private void OnMemberJoined(uint sid)
    {
        lock (pkgque_lock)
        {
            _sessionEvents.Enqueue(new SessionEvent { Sid = sid, Joined = true });
        }
    }

    /// <summary>成员会话断开。
    /// <para>⚠ 同 <see cref="OnMemberJoined"/>：传输线程回调，只入队；真正的清理在主线程做。</para></summary>
    private void OnMemberLeft(uint sid)
    {
        lock (pkgque_lock)
        {
            _sessionEvents.Enqueue(new SessionEvent { Sid = sid, Joined = false });
        }
    }

    // ==================== 成员进出（主线程处理） ====================
    /// <summary>主线程：按到达顺序处理会话事件（每帧在 <see cref="Update"/> 里跑一次）。</summary>
    private void ProcessSessionEvents()
    {
        while (true)
        {
            SessionEvent ev;
            lock (pkgque_lock)
            {
                if (_sessionEvents.Count == 0) return;
                ev = _sessionEvents.Dequeue();
            }

            // ⚠ 出队之后在锁外处理：BroadcastPlayerList 会走网络发送 + 本地派发，不该占着锁
            if (ev.Joined) Debug.Log($"[NetHostSvc] 成员建立连接 sid={ev.Sid}（尚未入房）");
            else HandleMemberLeft(ev.Sid);
        }
    }

    /// <summary>成员会话断开（主线程）：若已入房，从房间移除并广播玩家列表</summary>
    private void HandleMemberLeft(uint sid)
    {
        bool selfClosed = _selfClosed.Remove(sid);   // 本端主动关的（离开房间 / 踢人）⇒ 上面那步已经处理过，别重复

        if (_players.Remove(sid))
        {
            _profiles.Remove(sid);   // 资料跟人走，人走了资料也删（否则 BuildProfileArray 会漏出僵尸项）
            _poses.Remove(sid);      // 位姿表同样要清：不清的话这条"幽灵位姿"会跟着每一批快照一直下发
            _lastSeen.Remove(sid);
            Debug.Log($"[NetHostSvc] 成员离开 sid={sid}，剩余玩家 {TotalPlayers}");
            BroadcastPlayerList();   // ⚠ 战斗期名单**冻结**，这一句会被跳过（下面那条"离开"消息才是关键）
            // ★ 无论名单冻不冻结都要告诉各端"这个人走了"（只带 sid、不重排）：
            //   否则战斗中强退的盟友模型会一直留在别人屏幕上（2026-10-07 实测）。
            SendToAll(MessageCenter.Pack(CmdId.PlayerLeftNtf, new PlayerLeftMsg { Sid = sid }));
            return;
        }

        // 未入房的会话（连上没 JoinRoom / 离房后滞留 / 客户端被强杀）：没有房间状态要广播，但**也要清** ——
        // Update 对"任何来源的消息"都记过 _lastSeen（含未入房会话）⇒ 不清就是每来一次连接留一条永久条目。
        _poses.Remove(sid);
        _lastSeen.Remove(sid);
        // ⚠ 日志只报**第一次**：没入过房的连接本来就没有任何游戏意义，循环重连时会刷屏
        //   （2026-10-07 用户实测：每二十来秒一串，分不清到底有没有人被踢）。
        if (!selfClosed && _ghostLogged.Add(sid)) Debug.Log($"[NetHostSvc] 成员断连 sid={sid}（未入房，未参与游戏，后续同类不再提示）");
    }

    /// <summary>主动关闭某个成员的会话（离开房间 / 空闲踢人）。
    /// <para>▍为什么要先登记 <see cref="_selfClosed"/>：<c>CloseSession</c> 会触发库的断开回调 ⇒ 又会进
    /// <see cref="HandleMemberLeft"/>，登记过就只静默清痕迹。</para></summary>
    private void CloseSessionOf(uint sid)
    {
        _selfClosed.Add(sid);
        if (host != null && host.TryGetSession(sid, out HostSession s)) s.CloseSession();
    }

    // ==================== 心跳 / 空闲踢人 ====================
    /// <summary>【成员心跳】房主回一条 <see cref="PingRspMsg"/>（成员据此测 RTT，也让它的会话"有收到数据"）。
    ///
    /// <para>▍⚠⚠ **必须对"任何会话"都回，包括还没入房的**（2026-10-07 实测的"战备界面等一会儿就反复断开"就是它的锅）：
    /// 库的会话超时判的是"**收不到数据**就判掉线"（<c>TimeoutMs</c> 默认 15s），而成员从"连上房主"到"入房成功"
    /// 之间**只有心跳这一条报文**。旧实现在这里加了 <c>_players.ContainsKey(sid)</c> 的早退 ⇒ 未入房的连接
    /// **一个字节都收不到** ⇒ 15s 后必然被**它自己的库**判死 → 客户端重连（新会话又是同一 sid）→ 再被判死 ⇒
    /// 房主控制台每二十来秒刷一串 <c>成员断开 / 成员断连（未入房）</c>，而真正在房间里的那个成员一直好着。</para>
    ///
    /// <para>关闸（<see cref="CloseJoin"/>，Armament 之后不收人）由入房校验负责，**不该靠"不回心跳"来饿死对方**。</para>
    /// </summary>
    private void OnPingReq(PingReqMsg req)
    {
        if (req == null) return;

        uint sid = CurrentSid;
        if (sid == 0) return;                        // 没有来源会话 ⇒ 不该发生

        SendToSession(sid, MessageCenter.Pack(CmdId.PingRsp, new PingRspMsg
        {
            Id = req.Id,
            ServerTime = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond,
        }));
    }

    /// <summary>
    /// 【空闲踢人】成员被强杀 / 断网时，房主收不到任何"我走了"的通知（UDP 无连接），
    /// 会话会一直挂着、盟友实例也就一直留在场上。这里用"最后一次收到该成员消息的时刻"兜底：
    /// 超时即按"他离开了"处理（清表 + 广播名单 ⇒ 各端桥会清掉盟友实例），并主动关掉该会话。
    ///
    /// <para>▍为什么本帧就能判定：<see cref="Update"/> 出队每条消息时都会刷新 <see cref="_lastSeen"/>，
    /// 心跳（<c>PingReqMsg</c>）与战斗中的位姿上行都是"活着的证据"。</para>
    /// </summary>
    private void EvictIdleMembers()
    {
        if (memberIdleTimeout <= 0f || _players.Count == 0) return;

        float now = Time.unscaledTime;
        _idleBuffer.Clear();
        foreach (var kv in _players)
        {
            // 刚入房、还没收到过任何消息：先记当前时刻，从下一帧起算超时
            if (!_lastSeen.TryGetValue(kv.Key, out float t))
            {
                _lastSeen[kv.Key] = now;
                continue;
            }
            if (now - t > memberIdleTimeout) _idleBuffer.Add(kv.Key);
        }

        for (int i = 0; i < _idleBuffer.Count; ++i)
        {
            uint sid = _idleBuffer[i];
            Debug.LogWarning($"[NetHostSvc] 成员 sid={sid} 已 {memberIdleTimeout:0.#}s 无任何消息（疑似强退/断网）⇒ 按离开处理");
            HandleMemberLeft(sid);
            // 主动收尾会话：让库把该会话的重传缓冲/句柄释放掉，并触发一次 OnSessionDisconnected
            //（那时 _players 里已经没有他了，会走"未入房"分支；已在 CloseSessionOf 里登记 ⇒ 不再重复提示）
            CloseSessionOf(sid);
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

        // 收人窗口只有 **Ready**（选完任务 → 所有人就位）：① 进 Armament 就不再收人；
        // ② 进 Transition（名单已冻结、正在加载战斗）更不可能。进来只会卡在舰桥，
        // 还会破坏"players 下标 = 名单下标"的假设。
        if (_rosterFrozen)
        {
            SendJoinFail(sid, "本局已开始");
            return;
        }
        if (_joinClosed)
        {
            SendJoinFail(sid, "人员已就位");
            return;
        }

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

        // 本局配置已经确认过（房主在 Bridge 选完任务就广播了）⇒ 给新成员补发一份，
        // 否则他拿不到地图 / 任务 / 种子，进不了 Ready。顺序放在 JoinRoomRsp 之后（KCP 可靠有序）。
        if (_lastTaskConfirm != null)
        {
            SendToSession(sid, MessageCenter.Pack(CmdId.TaskConfirm, _lastTaskConfirm));
        }

        Debug.Log($"[NetHostSvc] 玩家 {me.PlayerName} 加入房间，当前玩家 {TotalPlayers}/{MaxPlayers}");

        // 广播最新玩家列表给所有成员
        BroadcastPlayerList();
    }

    /// <summary>处理成员离开房间（成员主动退房：先发这条通知，随后自己断开连接）</summary>
    private void OnLeaveRoomNtf(LeaveRoomNtf ntf)
    {
        uint sid = CurrentSid;
        if (_players.Remove(sid))
        {
            // ⚠ 与 HandleMemberLeft 同一套清理，别只删 _players：留下 _poses 会让这个人的"幽灵位姿"
            //   跟着每一批快照继续下发，_profiles/_lastSeen 则是纯泄漏。
            _profiles.Remove(sid);
            _poses.Remove(sid);
            _lastSeen.Remove(sid);
            Debug.Log($"[NetHostSvc] 玩家离开房间 sid={sid}，当前玩家 {TotalPlayers}/{MaxPlayers}");
            BroadcastPlayerList();
        }

        // ★ 主动离开 = 断开连接：立刻回收会话。否则它会以"未入房"的身份滞留到库超时，
        //   十几秒后再多报一条"成员断连（未入房）"——客户端来回加入/退出几次就攒出一串幽灵会话（2026-10-07 实测）。
        CloseSessionOf(sid);
    }

    /// <summary>处理成员准备状态切换</summary>
    private void OnReadyState(ReadyState st)
    {
        uint sid = CurrentSid;
        if (_players.TryGetValue(sid, out PlayerInfo p))
        {
            p.IsReady = st.IsReady;

            // 资料里的准备位也要跟着变（ArmamentWnd 是从资料里读准备状态的）
            if (_profiles.TryGetValue(sid, out PlayerProfile prof) && prof != null) prof.IsReady = st.IsReady;

            Debug.Log($"[NetHostSvc] 玩家 {p.PlayerName} 准备状态 → {st.IsReady}");
            BroadcastPlayerList();
        }
    }

    // ==================== 舰桥准备：资料 / 战备 / 强化（房主权威转发） ====================

    /// <summary>
    /// 成员上报自己的资料（角色/等级/武器/战备/强化）⇒ 合并进表并广播。
    /// ⚠ 成员只能改**自己**那份：<c>sid</c> 一律以当前会话为准，不采信 DTO 里带的 Sid。
    /// </summary>
    private void OnPlayerProfileNtf(PlayerProfileNtf ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null || ntf.Profile == null) return;
        if (!_players.ContainsKey(sid)) return;   // 还没入房就上报 → 忽略

        ntf.Profile.Sid = sid;
        ntf.Profile.IsHost = false;
        // ★ 准备状态**只能**由 ReadyState 改：资料上报里那位是成员侧 DTO 的默认值 false，
        //   照抄下来会让"点完就绪后再上报一次资料（切枪/战备/换角色都会触发）"把它重置成未就绪，
        //   名单广播给全体后连房主那边也跟着变（2026-10-07 实测）。
        ntf.Profile.IsReady = _players[sid] != null && _players[sid].IsReady;
        _profiles[sid] = ntf.Profile;

        if (_players[sid] != null && !string.IsNullOrEmpty(ntf.Profile.Name)) _players[sid].PlayerName = ntf.Profile.Name;

        Debug.Log($"[NetHostSvc] 收到资料上报 sid={sid} 角色={ntf.Profile.RoleName}");
        BroadcastPlayerList();
    }

    /// <summary>成员选择战备 → 以房主权威的索引转发给全体（房主本地也走一遍）。</summary>
    private void OnPlayerArmamentNtf(PlayerArmamentNtf ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null) return;

        int index = IndexOfSession(sid);
        if (index < 0) return;

        RelayToAll(CmdId.PlayerArmamentSync, new PlayerArmamentSync
        {
            PlayerIndex = index,
            AirdropId = ntf.AirdropId,
            SlotIndex = ntf.SlotIndex,
            Sid = sid,               // 权威标识：各端按它映射自己的本地下标
        });
    }

    /// <summary>成员呼叫战备 → 转发给全体。
    /// <para>⚠ 房主**不在这里本地复现**：转发出去的同步消息会回到房主自己（<see cref="NetRoomFlow.OnAirdropCall"/>），
    /// 那条路的判据是"不是自己发的就复现"⇒ 房主恰好是"别人发的" ⇒ 复现一次，正好。
    /// 发起方（成员）收到自己那条会被 <c>IsSelf(sid)</c> 丢掉 —— 他本地已经真放过一次了。</para>
    /// </summary>
    private void OnAirdropCallNtf(AirdropCallMsg ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null || ntf.AirdropId <= 0) return;

        RelayToAll(CmdId.AirdropCallSync, new AirdropCallMsg
        {
            Sid = sid,                  // 权威标识：各端据此丢掉"自己发的那条"
            AirdropId = ntf.AirdropId,
            X = ntf.X, Y = ntf.Y, Z = ntf.Z,
            Yaw = ntf.Yaw,              // 信标朝向（轰炸的炮位方向靠它，别漏）
        });
    }

    /// <summary>成员喊话 → 转发给全体（房主本地不复现：转回来的同步消息里 sid≠0 ⇒ 房主会当"别人喊的"播一次，正好）。</summary>
    private void OnSpeechNtf(PlayerSpeechMsg ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null) return;

        RelayToAll(CmdId.SpeechSync, new PlayerSpeechMsg { Sid = sid, Speech = ntf.Speech });
    }

    /// <summary>成员选择全队强化 → 转发给全体。</summary>
    private void OnPlayerBoosterNtf(PlayerBoosterNtf ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null) return;

        int index = IndexOfSession(sid);
        if (index < 0) return;

        RelayToAll(CmdId.PlayerBoosterSync, new PlayerBoosterSync
        {
            PlayerIndex = index,
            BoosterId = ntf.BoosterId,
            Sid = sid,
        });
    }

    /// <summary>成员切枪 → 以房主权威的 sid 转发给全体（房主本地也走一遍）。</summary>
    private void OnPlayerWeaponSwitchNtf(PlayerWeaponSwitch ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null) return;

        RelayToAll(CmdId.PlayerWeaponSwitchSync, new PlayerWeaponSwitch
        {
            Sid = sid,               // 权威标识：各端按它找对应的盟友实体
            SlotIndex = ntf.SlotIndex,
        });
    }

    /// <summary>成员开火 → 转发给全体（表现：枪口闪光 / 音效 / 枪械动画 / **弹道方向**）。
    /// <para>⚠ 方向必须原样带上：接收端只同步了 yaw，没有它就只能水平打。</para></summary>
    private void OnPlayerShootNtf(PlayerShoot ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null) return;

        RelayToAll(CmdId.PlayerShootSync, new PlayerShoot
        {
            Sid = sid,
            SlotIndex = ntf.SlotIndex,
            DirX = ntf.DirX,
            DirY = ntf.DirY,
            DirZ = ntf.DirZ,
        });
    }

    /// <summary>成员生命状态 → 转发给全体（血/盾/倒地，只做表现）。</summary>
    private void OnPlayerVitalNtf(PlayerVital ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null) return;

        RelayToAll(CmdId.PlayerVitalSync, new PlayerVital
        {
            Sid = sid,               // 权威标识：各端按它找对应的盟友实体
            Hp = ntf.Hp,
            HpMax = ntf.HpMax,
            Shield = ntf.Shield,
            ShieldMax = ntf.ShieldMax,
            Down = ntf.Down,
        });
    }

    /// <summary>成员弹药系数 → 转发给全体（只做 HUD 显示）。</summary>
    private void OnPlayerAmmoNtf(PlayerAmmo ntf)
    {
        uint sid = CurrentSid;
        if (sid == 0 || ntf == null) return;

        RelayToAll(CmdId.PlayerAmmoSync, new PlayerAmmo
        {
            Sid = sid,
            Ratio = ntf.Ratio,
        });
    }

    /// <summary>
    /// 【本局配置确认】房主在 Bridge **选完任务**时广播（调用点：<c>SelectMapWnd.ConfirmTask</c>）。
    ///
    /// <para>▍⚠ 这**不是"开局"**：发完这条，房主与成员都还在舰桥的 **Ready** 阶段等人
    /// （阶段链：Bridge 选任务 → Ready → 所有人就位 → Armament 选战备 → Transition 加载战斗 → Game）。
    /// 所以名单**不冻结**（Ready/Armament 期间仍允许加入；冻结见 <see cref="NotifyTransition"/>）。
    /// 同时把配置存进 <c>_lastTaskConfirm</c>，供新人入房时补发。</para>
    /// </summary>
    /// <param name="mapName">地图名（空 = 用本房记录的地图，会自动剥掉 "#难度" 后缀）</param>
    /// <param name="taskIndex">任务下标（-1 = 未指定）</param>
    /// <param name="extraDiff">额外难度（null = 全 0）</param>
    /// <param name="seed">随机种子（同局一致；0 = 由成员各自随机）</param>
    /// <param name="playMode">0=加入 1=公开 2=单人</param>
    /// <param name="taskFingerprint">房主"选中项 TaskCfg"的指纹（0 = 不校验；见 TaskManager.TaskFingerprint）</param>
    /// <param name="cfg">★**选中项 TaskCfg 的内容**（null = 不带 ⇒ 成员只能按本地表下标取，跨窗口会错位）。
    /// <para>▍由 09 侧的 <c>TaskManager.ToDto</c> 转换而来（02_Net 看不见 <c>TaskCfg</c>/<c>MissionEnum</c>）。
    /// 带上它之后，本局配置就"以内容为准"：后进者即使本地任务表里已经没有这个任务，也能照打
    /// —— 这正是 <see cref="_lastTaskConfirm"/> 补发机制要保证的语义。</para></param>
    /// <param name="taskMain">★ 本局**主任务类型枚举值**（<c>MissionEnum</c> 的 int；-1 = 不带；取 <c>TaskManager.NowTaskMain</c>）。
    /// <para>▍为什么要在这里收：房间**可能建得比选任务早** —— 「公开房」流程是按下准备时才建服
    /// （<c>SelectMapWnd.ConfirmTask</c> 里先 <c>CreateRoom</c> 再 <c>SetTask</c>），建服那一刻本房任务还是
    /// 空的/上一局的。确认任务才是权威时刻 ⇒ 在这里就地把广播里的主任务枚举刷新，房间列表一个广播周期内就跟着变。
    /// ⚠ 用 <c>int</c> 而非 <c>MissionEnum</c> 是因为 02_Net 看不见该枚举。</para></param>
    public void ConfirmTask(string mapName = "", int taskIndex = -1, int[] extraDiff = null,
        int seed = 0, int playMode = 2, int taskFingerprint = 0, TaskCfgDto cfg = null,
        int taskMain = -1)
    {
        _rosterFrozen = false;   // 新一局放行名单（上一局进 Transition 时冻结过）
        _joinClosed = false;     // 同时重开入房（收人窗口 = Ready，进 Armament 会再关闸）
        _started = false;
        BroadcastPlayerList();   // 进 Ready 前给全体推一次最新名册

        var ntf = new TaskConfirmNtf
        {
            MapName = string.IsNullOrEmpty(mapName) ? RoomMeta.MapName(RoomInfo) : mapName,
            Difficulty = _difficulty,
            TaskIndex = taskIndex,
            ExtraDiff = extraDiff ?? new int[4],
            Seed = seed,
            PlayMode = playMode,
            TaskFingerprint = taskFingerprint,
            Cfg = cfg,
            MatchId = ++_matchId,
        };
        _lastTaskConfirm = ntf;   // 供 Ready/Armament 期间入房的新人补发（**含配置内容** ⇒ 后进者按内容复现）

        // ★ 任务已定 ⇒ 刷新广播里的主任务枚举（房间列表据此精确取任务图标/颜色）。RoomInfo 是**同一个对象**、
        //   广播每次读它现取（GetBroadcastInfo）⇒ 这里改完，下一次广播就带上了。
        //   （公开房流程建服早于选任务，所以这一步不是可选项 —— 见方法参数说明。）
        if (taskMain >= 0)
        {
            _taskMain = taskMain;
            if (RoomInfo != null) RoomInfo.TaskMain = _taskMain;
        }

        SendToAll(MessageCenter.Pack(CmdId.TaskConfirm, ntf));
        Debug.Log($"[NetHostSvc] 已广播本局配置（局号 {ntf.MatchId}，地图 {ntf.MapName}，难度 {ntf.Difficulty}，" +
                  $"任务 {ntf.TaskIndex}，种子 {ntf.Seed}，指纹 {ntf.TaskFingerprint}，" +
                  $"配置内容 {(cfg != null && cfg.Main >= 0 ? "已带" : "无")}）——仍在舰桥 Ready，名单未冻结");
    }

    /// <summary>
    /// 【关闸】进入 <c>Armament</c> 阶段（所有人就位、开始选战备）时由 09 侧的桥调用 ⇒ 之后不再收人。
    /// <para>▍⚠ 只关"入房"，**不动名单广播**：<c>ArmamentWnd</c> 的"全员就绪 → 进 Transition"判定
    /// 就靠 <c>PlayerListSync</c> 里同步的准备状态，若在这里冻名单，准备状态会被一起冻住、
    /// 这个阶段就再也走不完了（所以关闸与冻名单是两件事，别合并）。</para>
    /// <para>▍重新放行：下一局 <see cref="ConfirmTask"/>（Bridge 选完任务 ⇒ 回到 Ready）。</para>
    /// </summary>
    public void CloseJoin()
    {
        if (_joinClosed) return;

        _joinClosed = true;
        Debug.Log("[NetHostSvc] 已进入 Armament（人员已就位）⇒ 关闭入房，不再接受新成员");
    }

    /// <summary>
    /// 【进 Transition】房主在 Armament 阶段"所有人都准备"、即将加载战斗场景时调用
    /// （调用点：09 侧的 <c>TeamNetBridge</c> 监听到 <c>GameState → Transition</c>）。
    /// <list type="number">
    ///   <item>广播 <see cref="TransitionNtf"/> ⇒ 成员把自己的 <c>GameState</c> 也推到 <c>Transition</c>
    ///         （加载动作走各自大厅既有的链，见该 DTO 的注释）；</item>
    ///   <item><b>冻结名单</b>：从这一刻起不再收人、不再广播名单
    ///         （战斗中重排会让 <c>players</c> 下标漂移，而 ArmamentWnd / BattleData 都按下标与数量假设）。</item>
    /// </list>
    /// </summary>
    public void NotifyTransition()
    {
        if (RoomInfo == null) return;   // 单机 / 没开房：什么都不用做

        _rosterFrozen = true;
        _started = true;

        SendToAll(MessageCenter.Pack(CmdId.Transition, new TransitionNtf { MatchId = _matchId }));
        Debug.Log($"[NetHostSvc] 已广播进入战斗（局号 {_matchId}），名单已冻结");
    }

    // ==================== 房间协议辅助 ====================
    /// <summary>当前正在主线程分发的这条消息来自哪个成员（由 Update 出队时设置）。
    /// <para>⚠ 公开读：<c>NetRoomFlow</c> 的路由处理器要靠它知道"这条请求是谁发的"（同一个派发口径）。</para></summary>
    public uint CurrentSid { get; private set; }

    /// <summary>向某个成员回"加入失败"</summary>
    private void SendJoinFail(uint sid, string reason)
    {
        SendToSession(sid, MessageCenter.Pack(CmdId.JoinRoomRsp, new JoinRoomRsp
        {
            ErrorCode = 1,
            Reason = reason,
        }));
    }

    /// <summary>
    /// 进战斗（Transition）后名册冻结 —— 战斗中不再广播名单，避免 <c>players</c> 下标漂移。
    /// <para>▍为什么不复用 <c>_started</c>：它还给 <c>RoomMeta.InGame</c>（房间列表"是否已开局"）用，
    /// 临时置 false 会被别的逻辑读到。</para>
    /// <para>▍置位点：<see cref="NotifyTransition"/>；放行点：<see cref="ConfirmTask"/> 开头（新一局）。</para>
    /// </summary>
    private bool _rosterFrozen;

    /// <summary>入房闸门：进 <c>Armament</c>（人员已就位）后关闭，<see cref="ConfirmTask"/>（新一局）放行。
    /// <para>▍为什么不复用 <see cref="_rosterFrozen"/>：删名（关闸）与冻名单是两件事 —— 关闸只该拦入房，
    /// 名单必须继续广播，否则 ArmamentWnd 收不到"谁已就绪"，全员就绪判定永远不成立。</para></summary>
    private bool _joinClosed;

    /// <summary>最近一次广播出去的"本局配置"，供 Ready/Armament 期间入房的新人补发（见 <see cref="OnJoinRoomReq"/>）。</summary>
    private TaskConfirmNtf _lastTaskConfirm;

    /// <summary>本局局号（每次 <see cref="ConfirmTask"/> 自增；0 保留给"未指定"）。</summary>
    private int _matchId;

    /// <summary>
    /// 把玩家列表（+详细资料）广播给所有成员。
    /// <para>⚠ 房主自己也要收：<c>SendToAll</c> 只发成员，所以这里额外 <c>MessageCenter.Dispatch</c> 一次
    /// —— 09 侧的桥（<c>TeamNetBridge</c>）订阅同一条消息，房主本地的 <c>TeamManager</c> 才能跟着更新。</para>
    /// </summary>
    private void BroadcastPlayerList()
    {
        // 进战斗后名册冻结：有人掉线也不重排（否则 players 下标会漂移，
        // 而 ArmamentWnd 的 armamentRoot.GetChild(i) ↔ players[i]、task.BattleData 的长度都按下标/数量假设）
        if (_rosterFrozen) return;

        var sync = new PlayerListSync
        {
            Players = BuildPlayerArray(),
            Profiles = BuildProfileArray(),
        };
        var msg = MessageCenter.Pack(CmdId.PlayerListSync, sync);
        SendToAll(msg);
        MessageCenter.Dispatch(msg);   // 房主本地自派发
    }

    /// <summary>
    /// 构造与 <see cref="BuildPlayerArray"/> **同序**的资料数组（房主在最前）。
    /// 成员还没上报资料时给一份"只有名字"的占位（角色名空 ⇒ 消费方要靠 <c>HasProfile</c> 判空，
    /// 别拿空 RoleName 去 <c>CreatPrefab</c>）。
    /// </summary>
    private PlayerProfile[] BuildProfileArray()
    {
        // 房主那一份：就绪只认房主自己的操作（见 _hostReady），别写死 true
        var host = HostProfile ?? new PlayerProfile { Sid = 0, Name = _hostName, IsHost = true };
        host.Sid = 0;
        host.IsHost = true;
        if (string.IsNullOrEmpty(host.Name)) host.Name = _hostName;
        host.IsReady = _hostReady;

        var list = new List<PlayerProfile>(_players.Count + 1) { host };

        foreach (var kv in _players)
        {
            PlayerProfile p;
            if (!_profiles.TryGetValue(kv.Key, out p) || p == null)
            {
                p = new PlayerProfile
                {
                    Sid = kv.Key,
                    Name = kv.Value != null ? kv.Value.PlayerName : null,
                    IsReady = kv.Value != null && kv.Value.IsReady,
                    IsHost = false,
                };
            }
            list.Add(p);
        }
        return list.ToArray();
    }

    /// <summary>sid → 索引（与 <see cref="BuildPlayerArray"/> 同序：房主 0，成员按字典顺序）。</summary>
    public int IndexOfSession(uint sid)
    {
        int i = 1;
        foreach (var kv in _players)
        {
            if (kv.Key == sid) return i;
            ++i;
        }
        return -1;
    }

    /// <summary>
    /// 【房主本地资料上报】房主自己的角色/武器/战备/强化变化时由 09 侧的桥调用 ⇒ 广播给成员。
    /// </summary>
    public void SetLocalProfile(PlayerProfile profile)
    {
        if (profile == null) return;
        profile.Sid = 0;
        profile.IsHost = true;
        profile.IsReady = _hostReady;   // ⚠ 就绪只由 SetHostReady/点击决定，别在这里写死 true
        HostProfile = profile;
        if (RoomInfo != null) BroadcastPlayerList();
    }

    /// <summary>
    /// 【权威转发】房主把一条同步消息发给所有成员，**并本地自派发一次**（房主自己要处理同样的消息）。
    /// ⚠ 用 <c>MessageCenter.Dispatch</c> 直接投递（而不是排队）：调用点已经在主线程的消息处理里了。
    /// </summary>
    private void RelayToAll<T>(int cmdId, T dto) where T : class
    {
        var msg = MessageCenter.Pack(cmdId, dto);
        SendToAll(msg);
        MessageCenter.Dispatch(msg);
    }

    /// <summary>构造完整玩家数组（房主 + 所有成员），房主排第一个</summary>
    private PlayerInfo[] BuildPlayerArray()
    {
        var list = new List<PlayerInfo>(_players.Count + 1)
        {
            // 房主名用真名（房间列表的"队伍列" / 玩家列表都显示它；不是广播里那个合成名）
            new PlayerInfo { Sid = 0, PlayerName = _hostName, IsHost = true, IsReady = _hostReady },
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

        // ⚠ 先处理会话事件（连接/断开），再做消息派发：它们按到达顺序入队，
        //   而且"离开 → 广播名单"必须跑在主线程（见 _sessionEvents 的注释）。
        ProcessSessionEvents();

        while (msgPackQue.Count > 0)
        {
            HostMsg hm;
            lock (pkgque_lock)
            {
                hm = msgPackQue.Dequeue();
            }
            // 记录当前消息来源，供房间协议处理器识别是哪个成员发的
            CurrentSid = hm.Sid;
            // 收到任何一条来自该成员的消息（心跳/位姿/准备…）都算"他还活着"（见 EvictIdleMembers）
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

        // ⚠ 放在 BroadcastPoses 之前：刚被判离线的成员不该再出现在这一批快照里
        EvictIdleMembers();

        BroadcastPoses();   // 位姿聚合下行（固定频率，见 poseBroadcastHz）
    }

    /// <summary>销毁时清理</summary>
    private void OnDestroy()
    {
        MessageCenter.Unregister(CmdId.JoinRoomReq);
        MessageCenter.Unregister(CmdId.LeaveRoomNtf);
        MessageCenter.Unregister(CmdId.ReadyState);
        MessageCenter.Unregister(CmdId.PlayerProfileNtf);
        MessageCenter.Unregister(CmdId.PlayerArmamentNtf);
        MessageCenter.Unregister(CmdId.PlayerBoosterNtf);
        MessageCenter.Unregister(CmdId.PlayerWeaponSwitchNtf);
        MessageCenter.Unregister(CmdId.PlayerShootNtf);
        MessageCenter.Unregister(CmdId.PlayerVitalNtf);
        MessageCenter.Unregister(CmdId.PlayerAmmoNtf);
        MessageCenter.Unregister(CmdId.PlayerTransformUp);
        MessageCenter.Unregister(CmdId.PingReq);
        StopHost();
        msgPackQue = null;
        Instance = null;
    }
}
}
