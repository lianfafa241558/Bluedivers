# NetTmp 适配层逐文件要点

> 目录：`Assets/Scripts/NetTmp/`（`Client/` 7 个脚本 + `Services/` 1 个常量表 + 4 个消息文件）。
> 该目录**没有 asmdef**，因此编译进 `Assembly-CSharp`；除自身外无任何工程代码引用它。
> 文件夹名 `NetTmp`（Tmp = temporary），属于试验性质模块——改造时按“新代码”，不要假定它已接入游戏主流程。

## 1. 数据流与线程边界（最重要）

```
[主线程] 业务 → MessageCenter.Pack → NetSvc.SendMsg / NetHostSvc.SendToAll
[主线程] KCPSession.SendMsg → 库写入 UDP（KCP 内部有独立 update 循环）
[后台线程] UDP 收包 → KCPSession.OnReciveMsg(msg)     ← 不能在里做 Unity 操作
[后台线程] NetSvc.AddMsgQue(msg) / NetHostSvc.AddMsgQue(msg, sid)  ← lock (pkgque_lock)
[主线程] 组件 Update() 出队 → MessageCenter.Dispatch(msg) → 注册的 Action<T>
```

`AddMsgQue` 与 `Update` 出队都持 `lock (NetSvc.pkgque_lock)`（房主端是 `NetHostSvc.pkgque_lock`，同一个字符串常量 "pkgque_lock"）。

## 2. Client/NetSvc.cs —— 成员端总入口

| 成员 | 说明 |
| --- | --- |
| `static NetSvc Instance` | 单例，`Awake` 赋值，`OnDestroy` 置 null |
| `KCPNet<ClientSession, NetMessage> client` | 成员端只有一条连接 |
| `bool IsConnected` | `client != null && client.clientSession != null && client.clientSession.IsConnected()` |
| `Awake()` | `InitSvc(false)` —— **默认不自动连固定服务器**（P2P 模式，等搜到房间再回连） |
| `InitSvc(bool connectDefault = true)` | 建消息队列 + 可选 `ConnectDefaultServer()` |
| `ConnectDefaultServer()` | 直连模式：`StartAsClient(SRV_IP, SRV_PORT)` + `ConnectServer(200, 5000)`；`SRV_IP = "127.0.0.1"`、`SRV_PORT = NetConfig.HostGamePort` |
| `ConnectToRoom(LanRoomInfo room, Action<bool> cb = null)` | **回连房主唯一入口**：`Disconnect()` → `StartAsClient(room.HostIp, room.HostPort)` → `await ConnectServer(200, 5000)` → `cb(result.Success)` |
| `Disconnect()` | `client.CloseClient()` + 清空队列 |
| `JoinRoom(name, pwd)` / `LeaveRoom()` / `SetReady(bool)` | 房间协议发起端，全部经 `SendToHost` |
| `SendMsg(NetMessage, Action<bool> cb)` | 业务发送统一入口；未连接时 `Debug.LogError("服务器未连接")` 并回调 false |
| `SendToHost(NetMessage)`（private） | 未连接时 `LogError("[NetSvc] 尚未连接房主，无法发送房间消息")` |
| `AddMsgQue(NetMessage)` | 由 `ClientSession.OnReciveMsg` 调用 |
| `Update()` | 出队 + `MessageCenter.Dispatch`（旧项目这里是 40+ 行巨型 switch，本项目改成一行） |

`ConnectTo`（private）是建连模板方法：`new KCPNet<ClientSession, NetMessage>()` → `startAction(绑定UDP)` → `connectAction(握手)`。

## 3. Client/NetHostSvc.cs —— 房主端总入口

| 成员 | 说明 |
| --- | --- |
| `static NetHostSvc Instance` | 单例 |
| `struct HostMsg { NetMessage Msg; uint Sid; }` | 队列元素，**必须带来源 sid** |
| `KCPNet<HostSession, NetMessage> host` | 服务器实例；每个连进来的成员对应一个 `HostSession` |
| `[SerializeField] int hostPort = NetConfig.HostGamePort` | 面板可改监听端口 |
| `LanBroadcaster _broadcaster` | 房间广播 |
| `LanRoomInfo RoomInfo { get; private set; }` | 当前房间（广播与 UI 共用） |
| `int MaxPlayers`、`int MemberCount`、`int TotalPlayers` | 人数（`TotalPlayers = 1 + 成员数`，含房主自己） |
| `Dictionary<uint, PlayerInfo> _players` | sid → 玩家信息（不含房主） |
| `_hostSelfName` | 房主名，用 `Host_{HHmmssfff}` 时间戳生成，写进 `RoomInfo.PlayerNames[0]`，供成员**排除自己开的房** |
| `Awake()` | 注册三个房间协议处理器：`JoinRoomReq` / `LeaveRoomNtf` / `ReadyState` |
| `StartHost(roomName, mapName = "", maxPlayers = 4, password = "")` | 创建 `KCPNet<HostSession, NetMessage>()` → `try { host.StartAsServer("0.0.0.0", hostPort) } catch (SocketException)`（转成清晰报错并回滚）→ 订阅 `OnSessionConnected/OnSessionDisconnected` → 构造 `RoomInfo` → `new LanBroadcaster(GetBroadcastInfo()).Start()` |
| `StopHost()` | 退订事件 → `CloseServer()` → 停广播 → 清房间与成员表 |
| `GetBroadcastInfo()` | `LanBroadcaster` 每轮广播前回调，刷新 `RoomInfo.PlayerCount = TotalPlayers` 后返回同一引用 |
| `OnMemberJoined(sid)` / `OnMemberLeft(sid)` | 传输层事件；离开时若在 `_players` 中则移除并广播玩家列表 |
| `OnJoinRoomReq(JoinRoomReq)` | 读 `CurrentSid` → 满员校验（`TotalPlayers >= MaxPlayers`）→ 密码校验 → 记录 `PlayerInfo` → 回 `JoinRoomRsp`（`ErrorCode=0` + `Self` + `Players`）→ `BroadcastPlayerList()` |
| `OnLeaveRoomNtf` / `OnReadyState` | 更新成员表并广播玩家列表 |
| `StartGame(mapName = "")` | 广播 `StartGameNtf` |
| `SendToAll(NetMessage)` / `SendToSession(uint sid, NetMessage)` | 房主权威广播/定向（定向用 `TryGetSession` + `IsConnected()`） |
| `AddMsgQue(NetMessage, uint sid)` / `Update()` | 出队时写 `CurrentSid`，`Dispatch` 后 `finally { CurrentSid = 0; }` |
| `OnDestroy()` | `MessageCenter.Unregister` 三条协议 + `StopHost()` |

⚠ `StartHost` 里 **`SocketException` 会被 catch 后重新 `throw`**（先清理 `host`/广播/`RoomInfo` 再抛），调用方需自行 try/catch，不要在捕获后立即重试开房，会再次失败。

## 4. 两个会话钩子

`ClientSession : KCPSession<NetMessage>`

```csharp
protected override IKCPMsgSerializer Serializer => NetMessageSerializer.Instance;
protected override void OnConnected()    { Debug.Log("[ClientSession] 连接服务器成功"); }
protected override void OnDisConnected() { Debug.Log("[ClientSession] 断开服务器连接"); }
protected override void OnReciveMsg(NetMessage msg) { NetSvc.Instance.AddMsgQue(msg); }
protected override void OnUpdate(DateTime now) { }
```

`HostSession : KCPSession<NetMessage>`

```csharp
public string PlayerName = "";                     // 预留给房主管理成员，当前未使用
protected override IKCPMsgSerializer Serializer => NetMessageSerializer.Instance;
protected override void OnReciveMsg(NetMessage msg)
    => NetHostSvc.Instance.AddMsgQue(msg, GetSessionID());   // ← 必须带 sid
```

两者 `OnUpdate` 都留空。新增会话类型（例如“专用服务器端”）照抄这两个文件重写 `Serializer` + `OnReciveMsg` 即可。

## 5. Client/MessageCenter.cs —— 分发核心

```csharp
private sealed class Handler { public Type MsgType; public Action<object> Callback; }
private static readonly Dictionary<int, Handler> _handlers = new();

public static void Register<T>(int cmdId, Action<T> handler) where T : class
    // 存 typeof(T) 与 obj => handler(obj as T)，同一 cmdId 重复注册会覆盖
public static NetMessage Pack<T>(int cmdId, T msg) where T : class
    // new NetMessage { CmdId = cmdId, Data = MessagePackSerializer.Serialize(msg) }
public static void Dispatch(NetMessage msg)
    // 查表 → 未注册则 LogError($"未注册消息:{msg.CmdId}")
    // → MessagePackSerializer.Deserialize(handler.MsgType, msg.Data)
    // → handler.Callback(body) 包在 try/catch 里，异常只 LogError 不中断循环
public static void Unregister(int cmdId)
```

关键细节：

- 非泛型反序列化写法为 `MessagePackSerializer.Deserialize(Type, ReadOnlyMemory<byte>)`，**Type 在前**，`msg.Data`（`byte[]`）隐式转 `ReadOnlyMemory<byte>`，**不要加 `ref`**（MessagePack 3.1.8 的签名）。
- `Dispatch` 的 `try/catch` 是逐条消息的隔离网：单条坏包不会打断 `NetSvc.Update()` 的 `while` 循环。
- 静态表**没有清空机制**，模块切换场景时务必 `Unregister`，否则残留处理器会在新场景里被间接触发。

## 6. Services/ 数据定义

`CmdId.cs` 分段：

| 段 | 命令号 | 消息 |
| --- | --- | --- |
| 通用 | `1001 PingReq` / `1002 PingRsp` | — |
| 账号 | `2001 LoginReq` / `2002 LoginRsp` | `LoginReqMsg` / `LoginRspMsg` |
| 聊天 | `3001 ChatSend` / `3002 ChatBroadcast` | `ChatSendMsg` / `ChatBroadcastMsg` |
| 房间 | `4001 JoinRoomReq` / `4002 JoinRoomRsp` / `4003 LeaveRoomNtf` / `4004 PlayerListSync` / `4005 ReadyState` / `4006 StartGameNtf` | `RoomMsg.cs` 内 6 个 DTO + `PlayerInfo` |

DTO 约定：`[MessagePackObject]` + 每字段 `[Key(n)]` 连续编号；字段 `public`（MessagePack 需要）；`PlayerInfo` 的 `Sid/PlayerName/IsReady/IsHost` 对应 `[Key(0..3)]`。

消息类里存在“方向注释”：`JoinRoomReq`(成员→房主)、`JoinRoomRsp`(房主→成员)、`LeaveRoomNtf`(成员→房主)、`PlayerListSync`(房主→全体)、`ReadyState`(成员→房主)、`StartGameNtf`(房主→全体)。新增消息请照此写明方向。

## 7. 两个 Demo

### NetDemo.cs（直连模式联调）

- `Awake` 注册：`LoginRsp` / `PingRsp` / `ChatBroadcast`。
- 按键：`L` 发登录、`B` 发聊天；`Update` 内每 3 秒自动发 `PingReq`（带 `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`，`OnPingRsp` 里算 RTT）。
- 所有发送前走 `EnsureConnected(action)`：未连接时写 UI 提示 + `LogWarning` 并返回 false。
- 注意注释：**不用 `C` 键**，因为 `LanRoomDemo` 已占用 C（避免同场景按键冲突）。
- 说明直连模式需要外部先调 `NetSvc.ConnectDefaultServer()`（`InitSvc(false)` 不自动连）。

### LanRoomDemo.cs（局域网房间联调）

- `Awake` 注册：`JoinRoomRsp` / `PlayerListSync` / `StartGameNtf`。
- 按键：房主 `H` 开房、`J` 关房、`G` 开局；成员 `R` 搜房、`T` 再扫、`S` 停监听、`C` 连第一个房间、`I` 申请入房、`E` 准备/取消、`Q` 离开。
- `HostRestartCooldown = 2f`：`StopHost` 记录 `_lastHostStopTime`，冷却期内按 H 直接拒绝并提示（规避 UDP 端口未释放导致的 `SocketException`）。
- `StartMemberScan` 用 `new LanDiscoverer(isSame ? NetConfig.LanSelfBroadcastPort : NetConfig.LanBroadcastPort)`，`isSame` 开关用于**同机双实例测试**。
- `ScanAgain` 里 `_member.Scan()` 是异步的，用 `Invoke(nameof(PrintRooms), 1.5f)` 延后取结果。
- `GetFilteredRooms()` 通过 `_selfHostName`（取自 `NetHostSvc.Instance.RoomInfo.PlayerNames[0]`）排除本机自己开的房；`PrintRooms` 会区分“扫到 N 个但全是自己的”与“扫到 0 个”两种提示。
- `EnsureMemberConnected(action)`：入房/准备/离开前必须已回连房主。
- `OnGUI` 右侧面板显示：房主状态、成员状态、本机角色、最新状态、房间列表、玩家列表（`FormatPlayers` 用 👑 标房主、(准备)/(未准备)）。

## 8. 场景使用方式（当前未挂载）

推荐 GameObject 组合：

| 机器 | 组件 |
| --- | --- |
| 房主 | `NetHostSvc`（+ 可选 `NetSvc` 让房主也能作为本地客户端收发） |
| 成员 | `NetSvc` |
| 两端联调 | 再加 `LanRoomDemo`（房间流程 UI）、`NetDemo`（登录/Ping/聊天 UI） |

两个 Demo 的 `OnDestroy` 都会 `Unregister` 自己注册的命令号并停掉广播/发现器，避免后台线程残留。
