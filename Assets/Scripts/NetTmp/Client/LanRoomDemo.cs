
// ============================================================================
// 局域网房间广播使用范例（LanRoomDemo）
// ----------------------------------------------------------------------------
// ▍它解决什么问题：
//   在"房主权威 + 局域网 P2P"模式下，成员要先"找到房间"，才能回连房主。
//   NetDemo 上面演示的是"直连已知服务器地址"，而这里演示"怎么在局域网里发现房间"。
//
// ▍对应 KCPNet 库的哪些类型：
//   房主端  → NetHostSvc（开服务器监听 + 广播房间）+ LanBroadcaster（底层广播）
//   成员端  → LanDiscoverer（广播发现请求 + 收集房间列表）
//   数据模型 → LanRoomInfo（房间信息：房间名/端口/人数/版本等）
//   协议常量 → LanDiscoveryConfig（广播端口 29800、魔数 "KCPLAN:" 等）
//
// ▍两步用法：
//   1. 房主：NetHostSvc.Instance.StartHost(房间名) → 开监听 + 广播房间
//   2. 成员：new LanDiscoverer() → StartListening() + Scan() → GetRooms() 拿到房间
//   拿到房间后，用 room.HostIp + room.HostPort 发起 KCP 回连（NetSvc.ConnectToRoom）。
//
// ▍完整闭环：
//   房主 StartHost → 广播房间 → 成员 Scan 发现 → 成员 ConnectToRoom 回连 → 建立 KCP 连接。
//   这是"房主权威 + 局域网 P2P"的最小可用闭环。
//
// ════════════════════════════════════════════════════════════════════════════
// ▍极简上手说明（不懂网络也能跑通）
// ════════════════════════════════════════════════════════════════════════════
//
// 【场景怎么挂】在 Unity 场景里放一个空 GameObject，挂上两个组件：
//     NetHostSvc（房主用）   +   NetSvc（成员用）   +   本组件 LanRoomDemo
//   （局域网联机时，房主那台机器用 NetHostSvc 开房，成员那台机器用 NetSvc 连。）
//
// 【按键流程（一台电脑自测也能跑，用两个"播放模式"或干脆同机测）】
//   房主那台：按 H 开房并广播  →  按 G 开始游戏（可选）
//   成员那台：按 R 搜房        →  按 C 连接第一个房间
//            → 按 I 加入房间   →  按 E 准备/取消准备  →  按 Q 离开房间
//
// 【一台电脑同时测房主+成员怎么办？】
//   因为房主和成员都要"监听端口"，同一台电脑会冲突。解决办法：
//   1. 用 Unity 开两个"播放模式"（或用两个项目），一个当房主、一个当成员；
//   2. 成员那边的 LanDiscoverer 用不同端口：new LanDiscoverer(29801)（见 StartMemberScan）。
//
// 【连不上/找不到房间？按顺序排查】
//   1. 两台电脑要连同一个 WiFi/局域网（不能一个连着路由器一个用手机热点）。
//   2. 房主那台的 Windows 防火墙：首次运行会弹窗"是否允许访问网络"，
//      一定要勾选"专用网络"并点"允许"。如果没弹窗，去"控制面板→防火墙→允许应用"
//      把游戏加进去。不放开，成员就永远连不上（这是最常见的原因）。
//   3. 确认按了 H（房主）后再按 R（成员），顺序别反。
//   4. 如果同一台电脑测，记得成员用 29801 端口（见上一条）。
//
// 【怎么在 Unity 客户端自由改端口/配置（注入式）】
//   LanDiscoveryConfig 是可注入的，在启动时调用一次 Setup 即可，不用改库代码：
//     LanDiscoveryConfig.Setup(broadcastPort: 39999);   // 把广播/发现端口改成 39999
//     LanDiscoveryConfig.Setup(broadcastAddress: IPAddress.Parse("192.168.1.255"));
//   ⚠️ 注意：广播端口这类配置要"所有玩家一致"才搜得到对方，所以一般建议保持默认；
//      改端口的主要用途是"避开别的程序占用的端口"或"局域网网段广播地址不通用时换地址"。
//   NetConfig 里还有 HostGamePort（房主连接端口），也能在 NetHostSvc 面板上改。
//
// ════════════════════════════════════════════════════════════════════════════
using KCPNet;
using UnityEngine;

namespace FPSGame.Net
{

public class LanRoomDemo : MonoBehaviour
{
    // ---------------- 演示所需的会话类型 ----------------
    // 房主：NetHostSvc（内部 KCPNet<HostSession, NetMessage> 以服务器身份运行）
    // 成员：NetSvc    （内部 KCPNet<ClientSession, NetMessage> 以客户端身份运行）
    // 局域网广播负责"发现"阶段，连上之后就走 KCP 传输，两者互补。

    private LanDiscoverer _member;  // 成员端发现器
    public bool isSame;

    // ---- 简易 UI 显示状态 ----
    private string _hostStatus = "未开房";
    private string _memberStatus = "未监听";
    private string _lastStatus = "";
    private System.Collections.Generic.List<LanRoomInfo> _roomList = new System.Collections.Generic.List<LanRoomInfo>();
    private string _playersText = "无";
    private bool _inRoom;          // 是否已成功加入房间

    /// <summary>关房到再次开房的最小间隔（秒）。UDP socket 关闭后端口需要时间释放，否则重绑会抛 SocketException。</summary>
    private const float HostRestartCooldown = 2f;
    /// <summary>上次关房时刻（Time.time）</summary>
    private float _lastHostStopTime = -999f;

    /// <summary>
    /// 本机开房时的房主名称（运行时时间戳生成，由 NetHostSvc 写入 RoomInfo.PlayerNames[0]）。
    /// 搜索时据此排除"自己开的房"。同机多个实例名称不同，不会互相误判。
    /// </summary>
    private string _selfHostName = null;

    private void Awake()
    {
        // 注册成员端"房间协议"处理器：接收房主回的消息
        MessageCenter.Register<JoinRoomRsp>(CmdId.JoinRoomRsp, OnJoinRoomRsp);
        MessageCenter.Register<PlayerListSync>(CmdId.PlayerListSync, OnPlayerListSync);
        MessageCenter.Register<StartGameNtf>(CmdId.StartGameNtf, OnStartGameNtf);
    }

    private void Update()
    {
        // ===== 房主操作 =====
        // 按 H：作为房主，创建房间并开始广播
        if (Input.GetKeyDown(KeyCode.H)) StartHost();

        // 按 J：作为房主，停止广播（离开房间）
        if (Input.GetKeyDown(KeyCode.J)) StopHost();

        // 按 G：作为房主，广播开始游戏（在成员都准备后）
        if (Input.GetKeyDown(KeyCode.G)) StartGame();

        // ===== 成员操作 =====
        // 按 R：作为成员，开始监听 + 扫描局域网房间
        if (Input.GetKeyDown(KeyCode.R)) StartMemberScan();

        // 按 T：作为成员，再扫一次（刷新房间列表）
        if (Input.GetKeyDown(KeyCode.T)) ScanAgain();

        // 按 S：作为成员，停止监听
        if (Input.GetKeyDown(KeyCode.S)) StopMember();

        // 按 C：作为成员，连接列表里的第一个房间（演示从房间信息回连）
        if (Input.GetKeyDown(KeyCode.C)) JoinFirstRoom();

        // 按 I：作为成员，向房主申请加入房间（回连成功后调用）
        if (Input.GetKeyDown(KeyCode.I)) DoJoinRoom();

        // 按 E：作为成员，切换准备状态
        if (Input.GetKeyDown(KeyCode.E)) ToggleReady();

        // 按 Q：作为成员，离开房间
        if (Input.GetKeyDown(KeyCode.Q)) LeaveRoom();
    }

    // ==================== 房主端：开房 + 广播 ====================
    private void StartHost()
    {
        if (NetHostSvc.Instance == null)
        {
            Debug.LogError("[LanRoomDemo] 请先在场景中挂 NetHostSvc 组件");
            return;
        }

        // 端口释放冷却：UDP socket 关闭后端口不会立刻释放，
        // 若在冷却期内立即开房，重绑同一端口会抛 SocketException。
        float remain = HostRestartCooldown - (Time.time - _lastHostStopTime);
        if (remain > 0)
        {
            _lastStatus = $"端口尚未释放，请稍候 {remain:F1}s 再开房（避免 SocketException）";
            Debug.LogWarning($"[LanRoomDemo] 距上次关房不足 {HostRestartCooldown}s，端口可能未释放，请稍候 {remain:F1}s 再按 H");
            return;
        }

        // 推荐方式：通过 NetHostSvc 开房。
        // 它会：启动 KCP 服务器监听(hostPort) + 构造房间信息 + 用 LanBroadcaster 广播。
        // StartHost(房间名, 地图名, 最大人数)
        NetHostSvc.Instance.StartHost("客厅大乱斗", "城市废墟", 4);
        // 记录自己开房时的房主名称（NetHostSvc 用时间戳生成），用于搜索时排除自己
        var selfNames = NetHostSvc.Instance.RoomInfo?.PlayerNames;
        _selfHostName = (selfNames != null && selfNames.Length > 0) ? selfNames[0] : null;
        _hostStatus = "已开房并广播（房间：客厅大乱斗 / 端口:" + NetHostSvc.Instance.RoomInfo?.HostPort + "）";
        _lastStatus = "房主已开房并广播，等待成员搜房回连";

        // ── 如果你想绕过 NetHostSvc，只单独用 LanBroadcaster 手动广播（更底层）──
        // var room = new LanRoomInfo
        // {
        //     RoomName = "客厅大乱斗",
        //     HostPort = 17666,   // ← 必须是房主 KCP 监听端口，成员才能回连
        //     MaxPlayers = 4,
        // };
        // _host = new LanBroadcaster(room);
        // _host.Start();
        Debug.Log("[LanRoomDemo] 房主已开房并广播到局域网");
    }

    private void StopHost()
    {
        NetHostSvc.Instance?.StopHost();
        _selfHostName = null; // 关房后不再排除自己
        _hostStatus = "已关房";
        _lastHostStopTime = Time.time; // 记录关房时刻，用于端口释放冷却
        _lastStatus = "房主已关房，等待端口释放...";
        Debug.Log("[LanRoomDemo] 房主已关房");
    }

    // ==================== 成员端：扫描局域网房间 ====================
    private void StartMemberScan()
    {
        if (_member != null) return;

        // ① 创建发现器（真实局域网不同机器用默认端口即可）
        //    同机测试时房主占了 29800，这里要换个端口，如 new LanDiscoverer(29801)。
        _member = new LanDiscoverer(isSame?NetConfig.LanSelfBroadcastPort:NetConfig.LanBroadcastPort);

        // 可选：只显示同版本房间
        // _member.VersionFilter = "1.0.0";

        // ② 开始监听（后台线程收集房主广播/回复）
        _member.StartListening();

        // ③ 主动扫一次
        ScanAgain();

        _memberStatus = "已开始监听局域网房间";
        Debug.Log("[LanRoomDemo] 成员已开始监听局域网房间");
    }

    private void ScanAgain()
    {
        if (_member == null)
        {
            _lastStatus = "尚未开始监听，请先按 R 开始搜房";
            return;
        }
        // 发送发现请求，等待房主应答（异步）
        _member.Scan();

        // 立即给用户反馈"正在扫描"，避免等 1.5 秒期间状态文本没变化
        _lastStatus = "正在扫描局域网房间，请稍候...";

        // 等约 1.5 秒后取房间列表（Scan 是异步的，这里简单延迟示意）
        // 真实项目可每帧轮询 GetRooms()，或等 Scan 完成再取。
        Invoke(nameof(PrintRooms), 1.5f);
    }

    private void PrintRooms()
    {
        int rawCount = _member != null ? _member.GetRooms().Count : 0;
        _roomList = GetFilteredRooms();

        if (rawCount > 0 && _roomList.Count == 0)
        {
            // 搜到了房间，但全被"排除自己"过滤掉了
            _lastStatus = $"扫描到 {rawCount} 个房间，但全部是本机自己的（已排除），暂无其他可连的房间";
        }
        else if (_roomList.Count == 0)
        {
            _lastStatus = "扫描到 0 个房间，请确认房主已按 H 开房，再按 T 重新扫描";
        }
        else
        {
            _lastStatus = $"扫描到 {_roomList.Count} 个房间（按 C 连接第一个）";
        }

        Debug.Log($"[LanRoomDemo] 扫描到 {rawCount} 个房间，排除自己后剩 {_roomList.Count} 个：");
        foreach (var r in _roomList)
        {
            Debug.Log($"  └ 「{r.RoomName}」 IP:{r.HostIp}:{r.HostPort} " +
                      $"人数:{r.PlayerCount}/{r.MaxPlayers} 地图:{r.MapName} 玩家 {string.Join(" ", r.PlayerNames ?? new string[0])}");
        }
    }

    /// <summary>
    /// 获取过滤后的房间列表：若本机开过房（_selfHostName 非空），
    /// 排除 PlayerNames 里包含本机房主名称的房间，避免误连自己开的房。
    /// 用唯一的时间戳名称判断，同机多个实例也不会互相误伤。
    /// </summary>
    private System.Collections.Generic.List<LanRoomInfo> GetFilteredRooms()
    {
        var raw = _member != null ? _member.GetRooms() : null;
        if (raw == null || raw.Count == 0) return new System.Collections.Generic.List<LanRoomInfo>();

        // 本机没开过房，或取不到自己的名称：不过滤，直接返回全部
        if (string.IsNullOrEmpty(_selfHostName)) return new System.Collections.Generic.List<LanRoomInfo>(raw);

        var filtered = new System.Collections.Generic.List<LanRoomInfo>();
        foreach (var r in raw)
        {
            bool isSelf = r.PlayerNames != null && System.Array.IndexOf(r.PlayerNames, _selfHostName) >= 0;
            if (isSelf) continue; // 排除自己开的房
            filtered.Add(r);
        }
        return filtered;
    }

    private void StopMember()
    {
        if (_member == null) return;
        _member.Stop();
        _member = null;
        _roomList.Clear(); // 停止监听后清空列表，避免显示过期房间
        _memberStatus = "已停止监听";
        Debug.Log("[LanRoomDemo] 成员已停止监听");
    }

    // ==================== 从房间信息回连房主 ====================
    // 按 C 连接列表里第一个房间
    private void JoinFirstRoom()
    {
        if (_member == null)
        {
            _lastStatus = "尚未开始监听，请先按 R 搜房";
            Debug.LogWarning("[LanRoomDemo] 尚未开始监听，请先按 R 搜房");
            return;
        }
        var rooms = GetFilteredRooms();
        if (rooms.Count == 0)
        {
            _lastStatus = "当前没有可用房间（可能只搜到自己的房间，已排除），先按 R 扫描";
            Debug.LogWarning("[LanRoomDemo] 当前没有可用的房间（可能只搜到自己的房间，已排除），先按 R 扫描");
            return;
        }
        JoinRoom(rooms[0]);
    }

    /// <summary>
    /// 【回连房主】这是"成员 → 房主"建立真正连接的核心。
    /// 广播只负责"发现"房间，连上之后的数据收发全走 KCP（复用 NetSvc 的客户端会话）。
    /// </summary>
    private void JoinRoom(LanRoomInfo room)
    {
        if (NetSvc.Instance == null)
        {
            _lastStatus = "场景中未挂 NetSvc 组件，无法回连";
            Debug.LogError("[LanRoomDemo] 场景中未挂 NetSvc 组件，无法回连");
            return;
        }
        Debug.Log($"[LanRoomDemo] 正在加入房间 {room.RoomName} @ {room.HostIp}:{room.HostPort}");
        _lastStatus = $"正在回连房主 {room.HostIp}:{room.HostPort}...";
        // 调用 NetSvc 的回连方法：内部会断开旧连接 → StartAsClient → ConnectServer 握手
        NetSvc.Instance.ConnectToRoom(room, (ok) =>
        {
            if (ok)
            {
                _lastStatus = $"已连上房主「{room.RoomName}」，按 I 申请加入";
                Debug.Log($"[LanRoomDemo] 已连上房主，按 I 申请加入房间");
            }
            else
            {
                _lastStatus = $"加入房间「{room.RoomName}」失败";
                Debug.LogError($"[LanRoomDemo] 加入房间「{room.RoomName}」失败");
            }
        });
    }

    // ==================== 房间协议：成员端交互 ====================
    /// <summary>按 I：向房主申请加入房间（需先回连成功）</summary>
    private void DoJoinRoom()
    {
        if (!EnsureMemberConnected("申请加入房间")) return;
        NetSvc.Instance.JoinRoom("玩家" + Random.Range(1000, 9999));
    }

    /// <summary>按 E：切换准备状态</summary>
    private void ToggleReady()
    {
        if (!EnsureMemberConnected("切换准备状态")) return;
        _ready = !_ready;
        NetSvc.Instance.SetReady(_ready);
        _lastStatus = $"已{(_ready ? "准备" : "取消准备")}";
        Debug.Log($"[LanRoomDemo] 已{(_ready ? "准备" : "取消准备")}");
    }
    private bool _ready;

    /// <summary>按 Q：离开房间</summary>
    private void LeaveRoom()
    {
        if (!EnsureMemberConnected("离开房间")) return;
        NetSvc.Instance.LeaveRoom();
        _ready = false;
        _inRoom = false;
        _lastStatus = "已离开房间";
    }

    /// <summary>
    /// 【连接检查】成员端的"入房/准备/离开"都必须先回连房主。
    /// NetSvc 未连接时 SendToHost 会静默吞掉消息，这里在 UI 上明确提示。
    /// </summary>
    private bool EnsureMemberConnected(string action)
    {
        if (NetSvc.Instance != null && NetSvc.Instance.IsConnected) return true;

        _lastStatus = $"尚未回连房主，无法{action}。请先按 C 连接房间";
        Debug.LogWarning($"[LanRoomDemo] 尚未回连房主，无法{action}");
        return false;
    }

    /// <summary>按 G：房主广播开始游戏</summary>
    private void StartGame()
    {
        NetHostSvc.Instance?.StartGame();
    }

    // ---- 房主回的消息处理器 ----
    private void OnJoinRoomRsp(JoinRoomRsp rsp)
    {
        if (rsp.ErrorCode == 0)
        {
            _inRoom = true;
            int count = rsp.Players?.Length ?? 0;
            _lastStatus = $"加入成功！当前玩家 {count} 人";
            _playersText = FormatPlayers(rsp.Players);
            Debug.Log($"[LanRoomDemo] 加入成功！当前玩家 {count} 人");
        }
        else
        {
            _lastStatus = $"加入被拒绝：{rsp.Reason}";
            Debug.LogError($"[LanRoomDemo] 加入被拒绝：{rsp.Reason}");
        }
    }

    private void OnPlayerListSync(PlayerListSync sync)
    {
        _playersText = FormatPlayers(sync.Players);
        var sb = new System.Text.StringBuilder("[LanRoomDemo] 玩家列表：");
        if (sync.Players != null)
        {
            foreach (var p in sync.Players)
            {
                sb.Append($"{(p.IsHost ? "👑" : "")}{p.PlayerName}{(p.IsReady ? "(准备)" : "(未准备)")} ");
            }
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>把玩家数组拼成便于 UI 显示的一行文字</summary>
    private string FormatPlayers(PlayerInfo[] players)
    {
        if (players == null || players.Length == 0) return "无";
        var parts = new System.Collections.Generic.List<string>();
        foreach (var p in players)
        {
            string mark = p.IsHost ? "👑" : "";
            string ready = p.IsReady ? "[准备]" : "[未准备]";
            parts.Add($"{mark}{p.PlayerName}{ready}");
        }
        return string.Join("  ", parts);
    }

    private void OnStartGameNtf(StartGameNtf ntf)
    {
        _lastStatus = $"房主开始游戏！地图 {ntf.MapName} —— 这里应切换到战斗场景";
        _ready = false;
        Debug.Log($"[LanRoomDemo] 房主开始游戏！地图 {ntf.MapName} —— 这里应切换到战斗场景");
    }

    // ==================== 简易 UI 显示 ====================
    // 直接画一个半透明面板，实时显示房主/成员/房间/玩家状态，方便观察是否成功。
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 440, 10, 430, Screen.height - 20));

        // 顶部操作提示
        GUILayout.Label("<size=16><b>【局域网房间联调】</b></size>");
        GUILayout.Label("房主：H=开房  J=关房  G=开局");
        GUILayout.Label("成员：R=搜房  T=再扫  S=停监听  C=连房  I=入房  E=准备  Q=离开");
        GUILayout.Space(6);

        // 状态面板
        GUI.backgroundColor = new Color(0, 0, 0, 0.6f);
        GUILayout.BeginVertical("box");
        GUILayout.Label($"<b>房主：</b>{_hostStatus}");
        GUILayout.Label($"<b>成员：</b>{_memberStatus}");
        GUILayout.Label($"<b>本机角色：</b>{(_inRoom ? "已入房" : "未入房")}  {(NetHostSvc.Instance != null ? "(含房主组件)" : "")}");
        GUILayout.Label($"<b>最新状态：</b>{_lastStatus}");
        GUILayout.EndVertical();
        GUILayout.Space(6);

        // 房间列表面板
        GUILayout.Label($"<b>发现的房间（{_roomList.Count}）：</b>");
        GUILayout.BeginVertical("box");
        if (_roomList.Count == 0)
        {
            GUILayout.Label("（暂无，按 R 扫描）");
        }
        else
        {
            foreach (var r in _roomList)
            {
                GUILayout.Label($"「{r.RoomName}」 {r.HostIp}:{r.HostPort} 人数:{r.PlayerCount}/{r.MaxPlayers} 地图:{r.MapName}");
            }
        }
        GUILayout.EndVertical();
        GUILayout.Space(6);

        // 玩家列表面板
        GUILayout.Label("<b>玩家列表：</b>");
        GUILayout.BeginVertical("box");
        GUILayout.Label(_playersText);
        GUILayout.EndVertical();

        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        // 反注册房间消息处理器
        MessageCenter.Unregister(CmdId.JoinRoomRsp);
        MessageCenter.Unregister(CmdId.PlayerListSync);
        MessageCenter.Unregister(CmdId.StartGameNtf);

        // 退出时清理，避免后台线程残留
        StopHost();
        StopMember();
    }
}
}
