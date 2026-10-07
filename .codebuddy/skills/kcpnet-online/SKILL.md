---
name: kcpnet-online
description: Bluedivers 项目的 KCPNet 联机网络层（预编译库 Assets/Plugins/KCPNet + 自研适配层 Assets/Scripts/NetTmp）专用指南。当需要部署/安装/升级 KCPNet.dll 与 Kcp.dll、安装或自检 NetTmp 需要的 NuGet 依赖（MessagePack 及其传递依赖，scripts/install_deps.py 与 --check）、排查 NetTmp 下 “未能找到类型或命名空间名 MessagePackObject” 之类 CS0246 编译报错、排查 `Assembly '...' will not be loaded due to errors` / `Unable to resolve reference` 这类程序集加载失败、新增或修改网络消息（CmdId 常量 + [MessagePackObject] DTO + MessageCenter.Register/Pack/Dispatch）、编写或调试 NetSvc / NetHostSvc / ClientSession / HostSession、实现局域网开房与搜房回连（LanDiscoverer / LanBroadcaster / LanRoomInfo / LanDiscoveryConfig）、排查开房 SocketException 或端口占用/防火墙/同机双实例问题、以及需要确认 KCPNet 库对外 API 与默认端口时触发。
---

# KCPNet 联机层（Bluedivers）

## Overview

Bluedivers 的联机能力由**两层**构成，理解这两层的边界是本 skill 的核心：

| 层 | 位置 | 形态 | 能否改 |
| --- | --- | --- | --- |
| **预编译库 KCPNet** | `Assets/Plugins/KCPNet/KCPNet.dll` + `Kcp.dll` | 无源码的托管 DLL（netstandard2.0） | 否，只能反射/反编译看 API |
| **项目适配层** | `Assets/Scripts/NetTmp/`（`Client/` + `Services/`） | 项目自己的 C# 源码 | 是，所有业务改造在这里 |

- 库负责：UDP 收发、KCP 可靠传输（丢包重传/乱序重组）、会话（`KCPSession<T>`）与服务器（`KCPNet<T,K>`）抽象、局域网房间广播发现、RSA→AES 握手密钥。
- 适配层负责：消息信封与序列化（`NetMessage` + `MessagePack`）、命令号表（`CmdId`）、按命令号分发的注册表（`MessageCenter`）、成员端入口（`NetSvc`）、房主端入口（`NetHostSvc`）、两个会话钩子（`ClientSession` / `HostSession`）以及两个 Demo。
- 通信模型：**房主权威 + 局域网 P2P**。房主客户端内嵌一个迷你“服务器”（`StartAsServer`）监听端口，成员先经 UDP 广播发现房间，再用 `StartAsClient` 回连房主。

数据流（记住这条链就掌握了 80%）：

```
业务代码
  → MessageCenter.Pack(cmdId, dto)        // MessagePack 序列化 dto → NetMessage{CmdId, Data}
  → NetSvc.SendMsg(NetMessage)            // 成员端；房主端用 NetHostSvc.SendToAll/SendToSession
  → KCPSession<T>.SendMsg                 // 库：KCP 可靠发送
────────────────────── 网络 ──────────────────────
  → KCPSession<T>.OnReciveMsg(NetMessage) // 库回调（可能不在主线程！）
  → NetSvc.AddMsgQue / NetHostSvc.AddMsgQue   // 加锁入队（房主端额外带 sid）
  → Mono Update() 出队（主线程）
  → MessageCenter.Dispatch(msg)           // 查 CmdId 表 → 反序列化 → 调注册回调
```

## 当前部署状态（2026-09-30 实测：依赖已装、工程可编译可运行）

| 项 | 状态 | 证据 |
| --- | --- | --- |
| `KCPNet.dll` / `Kcp.dll` 放置 | ✅ `Assets/Plugins/KCPNet/`，PluginImporter 为 Any Platform（`platformData` 中 `Any.enabled: 1`） | `Assets/Plugins/KCPNet/*.meta` |
| **MessagePack 依赖闭包** | ✅ 已补齐（6 个 DLL，见下节），此前缺失导致全工程编译失败 | 装前：Console 大量 `CS0246 未能找到类型或命名空间名“MessagePackObject”`；装后：`read_console(types=["error"])` 返回 **0 条** |
| 程序集是否加载成功 | ✅ Unity 运行时加载 `Kcp 2.0.0.0`、`KCPNet 1.0.0.0`、`MessagePack 3.1.8.0`、`MessagePack.Annotations 3.1.8.0`；并实测 `LoginReqMsg` 序列化往返成功（14 字节） | `execute_code` 打印 `AppDomain.CurrentDomain.GetAssemblies()` |
| 适配层编译 | ✅ `Library/ScriptAssemblies/Assembly-CSharp.dll` 已引用 `KCPNet` + `MessagePack` | DLL 引用表字节扫描 / 重新编译时间戳 |
| 适配层挂载到场景 | ❌ 仍未挂载：无任何场景/预制体引用 `NetSvc`/`NetHostSvc`/`LanRoomDemo`/`NetDemo` 的脚本 GUID | 全库 GUID 扫描无命中 |
| 被既有游戏逻辑引用 | ❌ 除 `NetTmp` 自身外，`Assets/Scripts` 下无其他脚本引用 `NetSvc`/`NetHostSvc`/`MessageCenter`/`CmdId` | 仍是孤立试验模块（文件夹名 `NetTmp` = 临时） |

结论：**编译与库加载已通**，剩下两件事是「挂到场景」与「接进游戏主流程」。任何“KCP 相关改造”开始前，先用 `read_console` 确认当前状态。

## 部署清单（从零到可编译）

1. **放库**：`KCPNet.dll` + `Kcp.dll`（+ 可选 `KCPNet.pdb`、`log4net.config`，非必需）放同一目录，路径用 `Assets/Plugins/KCPNet/`。两个 DLL 保持默认 `Any Platform`，不要设 `defineConstraints`，也不要放进 `Editor/` 目录。
2. **补依赖闭包**（一次性命令，Python；默认目标就是下面这张表的完整 6 个）：

   ```bash
   python "d:\Pro\Bluedivers\.codebuddy\skills\kcpnet-online\scripts\install_deps.py"           # 装全部 6 个
   python "d:\Pro\Bluedivers\.codebuddy\skills\kcpnet-online\scripts\install_deps.py" --check   # 只读自检：装没装 / 还缺什么
   ```

   `--check` 会依次报告：6 个 DLL 是否存在 + 每个 `*.dll.meta` 的 Any Platform 标志、是否误放了 Unity 自带的同名程序集、`Assembly-CSharp.dll` 是否已引用 `KCPNet`/`MessagePack`、以及 **Editor.log 里最近一次域重载**的未解析引用（历史失败会被识别为历史，不会误报）。

   最终落地在 `Assets/Plugins/KCPNet/` 的 6 个 DLL 及其必要性：

   | DLL | 版本 | 为什么需要 |
   | --- | --- | --- |
   | `MessagePack.dll` | 3.1.8.0（取 `lib/netstandard2.0/`） | 适配层所有 DTO 的序列化 |
   | `MessagePack.Annotations.dll` | 3.1.8.0 | `[MessagePackObject]`/`[Key]` |
   | `Microsoft.Bcl.AsyncInterfaces.dll` | 8.0.0 | `MessagePack.dll` 的 AssemblyRef |
   | `Microsoft.NET.StringTools.dll` | 17.11.4（程序集版本 1.0.0.0） | 同上 |
   | `System.Collections.Immutable.dll` | 8.0.0 | 同上 |
   | `System.Runtime.CompilerServices.Unsafe.dll` | 6.0.0 | 同上 |

   ⚠ **不要**再放 `System.Memory.dll` / `System.Buffers.dll`：Unity 自带 `System.Memory 4.0.99.0`，实测能正确满足 `KCPNet.dll`(需要 4.0.1.2) / `Kcp.dll`(需要 4.0.1.0) 的引用；自己塞一份极易触发“同名程序集重复”。
   同理，`MessagePack.dll` 还引用 `System.Numerics.Vectors` / `System.Reflection.Emit*` / `System.Threading.Tasks.Extensions`，这些都由 Unity 的 profile 提供，**不必**手工补。

   放置位置：与 KCPNet 同目录。**不要**给 `Plugins/` 加 `.asmdef`，保持预编译插件默认行为。
3. **刷新**：新增 DLL 属于 Assets 变更。用 Unity MCP 先 `refresh_unity(mode=force, scope=all, compile=request)`，再 `read_console` 看是否有 `CS0246` 残留。
4. **验证四连**（缺一不可，只看 Console 会漏掉“程序集加载失败”）：
   1. `read_console(types=["error"])` → 0 条；
   2. 程序集真的加载了：`execute_code` 打印 `AppDomain.CurrentDomain.GetAssemblies()` 里能看到 `KCPNet`/`MessagePack`；
   3. 做一次真实序列化往返（`execute_code` 里 `MessagePackSerializer.Serialize/Deserialize<LoginReqMsg>`）；
   4. 若仍报 `Assembly '...' will not be loaded due to errors`，**去 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 看紧接着的那几行 `Unable to resolve reference 'X'`**（Console 面板只显示标题行，原因在里面）。
5. **挂载**：新建/指定一个常驻 GameObject，按用途挂组件——房主机挂 `NetHostSvc`，成员机挂 `NetSvc`，联调再挂 `LanRoomDemo`（房间流程）与 `NetDemo`（登录/Ping/聊天）。两个 Demo 会自动 `MessageCenter.Register`，可用于验收。
6. **AOT（IL2CPP）—— 这个坑已经踩过并修好了（2026-10-06），别再重走**：`ProjectSettings` 的 Standalone `scriptingBackend` 是 **IL2CPP(1)**，而 MessagePack 默认走 `DynamicObjectResolver`（`System.Reflection.Emit`，IL2CPP 不支持）。症状：**打包版一发消息就抛**
   `PlatformNotSupportedException: Operation is not supported on this platform` at `AssemblyBuilder.DefineDynamicAssembly` ← `DynamicObjectResolver.FormatterCache<T>..cctor`；
   **编辑器（Mono）完全看不出来**。修法 = 适配层不再用 `MessagePackSerializer`，改用 **`NetTmp/Services/NetMsgCodec.cs`**（只用 `MessagePackWriter/Reader` 写读标准 MessagePack 字节，成员枚举 + 字段读写自己用反射做；`[MessagePackObject]` 类型写 **array 格式**、按 `[Key]` 升序）。⇒ **新增消息仍然只需"CmdId 常量 + `[MessagePackObject]` + `[Key(n)]` 两步**，不必写 formatter。三条注意事项：
   ① 反射用法对 IL2CPP 托管裁剪**不可见** ⇒ 必须保留 `Assets/link.xml`（`preserve="all"` 保全 `02_Net` 与 `MessagePack.Annotations`），否则打包后"反序列化出来全是默认值"；
   ② 字段类型只支持 `NetMsgCodec.WriteValue/ReadValue` 里列出的那些（string/int/uint/long/float/bool/short/byte/double/enum/数组/嵌套 DTO），新类型要在那两处补分支（会在编辑器里当场抛 `NotSupportedException`，不静默写坏）；
   ③ **不要**试图引用 `MessagePack.GeneratedMessagePackResolver`（KCPNet.dll 自带的那份是 `internal`，外部引用报 `CS0122`）；KCPNet 自己显式传 options（DLL 里能搜到 `MessagePackSerializerOptions`）⇒ 库侧本来就安全。
   备选路（本次没走，因为要多装工具链）：MessagePack 3.1.8 的 nupkg **不含 analyzer/源生成器**，想用 mpc/源生成器预生成 resolver 得自己装 .NET SDK；或者把 Standalone 切 Mono（能立刻绕过，但放弃 IL2CPP）。

## 库 API 速查（仅列常用，完整签名见 `references/kcpnet-api.md`）

| 类型 | 关键成员 | 说明 |
| --- | --- | --- |
| `KCPNet<T,K>` | `ctor(bool isUnityEnvironment = true)`、`StartAsClient(ip,port)`、`StartAsServer(ip,port)`、`Task<ConnectResult> ConnectServer(int interval, int maxintervalSum = 5000)`、`SendToAll(K)`、`TryGetSession(uint, out T)`、`GetSessions()`、`CloseClient()` / `CloseServer()`、事件 `OnSessionConnected/OnSessionDisconnected/OnServerRestartDetected(Action<uint>)`、字段 `clientSession` | `T`=会话类型，`K`=消息类型（本项目为 `NetMessage`） |
| `KCPSession<T>` | 抽象属性 `Serializer`；虚方法 `OnConnected()` / `OnDisConnected()` / `OnReciveMsg(T)` / `OnUpdate(DateTime)`；`SendMsg(T)` / `SendMsg(byte[])`、`IsConnected()`、`GetSessionID()`、`InitSession(...)`、`CloseSession()`；属性 `AesKey`、`TimeoutMs`、`EnableTimeoutCheck`、`SetTimeout(long)` | 必须继承并 override `Serializer` + 至少 `OnReciveMsg` |
| `IKCPMsgSerializer` | `byte[] Serialize(KCPMsg)`、`KCPMsg Deserialize(byte[])` | 信封级序列化器，项目直接用库里的 `NetMessageSerializer.Instance` |
| `KCPMsg` / `NetMessage` | `NetMessage : KCPMsg`，字段 `int CmdId`、`byte[] Data` | 项目自己的“信封”，`Data` 是 DTO 的 MessagePack 字节 |
| `NetConfig` | 常量 `HostGamePort = 17666`、`LanBroadcastPort = 29800`、`LanSelfBroadcastPort = 29801` | 端口唯一来源，别在业务里硬编码 |
| `LanDiscoveryConfig` | 静态 `BroadcastPort=29800`、`ListenPort=29800`、`BroadcastAddress=255.255.255.255`、`AnnounceIntervalMs=2000`、`DiscoveryTimeoutMs=1500`、`RoomExpireMs=6000`、`BroadcastTtl=1`、`Magic="KCPLAN:"`、`AnnounceType/DiscType`；`Setup(...)` 全部参数可选 | 改广播参数的唯一入口（`Setup` 注入，不改库） |
| `LanRoomInfo` | 字段 `RoomName/HostIp/HostPort/PlayerCount/MaxPlayers/MapName/PasswordProtected/Version/PlayerNames[]`、`ToJson()/FromJson()` | 房间模型，广播包与 UI 都用它 |
| `LanBroadcaster` | `ctor(LanRoomInfo)`、属性 `RoomInfoProvider : Func<LanRoomInfo>`、`Start()/Stop()` | 房主侧周期性 ANNOUNCE + 应答 DISCOVER |
| `LanDiscoverer` | `ctor(int listenPort = -1)`、属性 `VersionFilter`、`AutoExpire`、`StartListening()`、`Task Scan()`、`List<LanRoomInfo> GetRooms()`、`Stop()`、`PurgeExpired()` | 成员侧发现器；`Scan()` 是异步的，不能立刻取结果 |
| `KCPTool` | `Log/ColorLog/WarnLog/ErrLog`、`Compress/DeCompress`、静态委托 `LogFunc/ColorLogFunc/...` | 库日志出口，`KCPLogColor` 有 None/Red/Green/Blue/Cyan/Magenta/Yellow |
| `KCPNet.Util.*` | `ClientSecurity.GenerateAesKey()/EncryptAesKeyWithRsa()`、`ServerSecurity.*`、`EncryptionHelper.Encrypt/Decrypt` | 握手 RSA→AES 加密，库内部使用，项目当前未显式配置 |
| `KCPNet.Config.*` | `RegionConfig`（ServerIP/Redis/DataBase/MaxPlayers…）、`ServerRegionConfig.Current` | 专用服务器/分区模式的配置，**本项目 P2P 房主模式未使用** |

## 适配层（NetTmp）结构与职责

| 文件 | 职责 | 对外入口 |
| --- | --- | --- |
| `Client/NetSvc.cs` | 成员端总入口：建连（`ConnectDefaultServer` / `ConnectToRoom`）、发送（`SendMsg`）、收消息入队、`Update()` 里派发 | `NetSvc.Instance`、`IsConnected`、`JoinRoom/LeaveRoom/SetReady` |
| `Client/NetHostSvc.cs` | 房主端总入口：`StartHost/StopHost`、成员表、房间协议处理、`SendToAll/SendToSession`、联动 `LanBroadcaster` | `NetHostSvc.Instance`、`RoomInfo`、`ConfirmTask`、`NotifyTransition` |
| `Client/ClientSession.cs` | 成员端会话钩子：`Serializer`、`OnReciveMsg → NetSvc.Instance.AddMsgQue` | 由库回调 |
| `Client/HostSession.cs` | 房主端会话钩子：`OnReciveMsg → NetHostSvc.Instance.AddMsgQue(msg, GetSessionID())`（**必须带 sid**） | 由库回调 |
| `Client/MessageCenter.cs` | 静态注册表：`Register<T>(cmdId, Action<T>)`、`Pack<T>(cmdId, T)`、`Dispatch(NetMessage)`、`Unregister(cmdId)` | 全局静态 |
| `Services/CmdId.cs` | 命令号常量分段表（1000 通用 / 2000 账号 / 3000 聊天 / 4000 房间） | 常量 |
| `Services/Msg/*.cs` | `[MessagePackObject]` DTO：`PingMsg`、`LoginMsg`、`ChatMsg`、`RoomMsg`（`PlayerInfo/JoinRoomReq/JoinRoomRsp/LeaveRoomNtf/PlayerListSync/ReadyState/TaskConfirmNtf/TransitionNtf` + 资料与战备若干） | 数据 |
| `Client/NetDemo.cs` | 直连模式联调 Demo（L 登录 / B 聊天 / 每 3 秒 Ping） | 组件 |
| `Client/LanRoomDemo.cs` | 局域网房间联调 Demo（房主 `H/J/G`，成员 `R/T/S/C/I/E/Q`）+ OnGUI 状态面板 | 组件 |

更细的逐文件要点与调用链见 `references/nettmp-layer.md`。

## 主要工作流

### A. 新增一条网络消息（四步，固定顺序）

1. `Services/CmdId.cs` 加命令号常量，遵循分段（新模块另开千位段，编号不重复、不复用、不改旧值）。
2. `Services/Msg/` 下（可按模块新建文件，如 `BattleMsg.cs`）加 `[MessagePackObject]` 类，字段用 `[Key(0..n)]` 连续编号。新增字段**追加在末尾**并取下一个 Key，不要插在中间。
3. 在 `Awake()` 中注册处理器：`MessageCenter.Register<XxxMsg>(CmdId.Xxx, OnXxx);`，并在 `OnDestroy()` 中 `MessageCenter.Unregister(CmdId.Xxx);`（房主侧协议处理则由 `NetHostSvc` 注册）。
4. 发送：成员端 `NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.Xxx, dto))`；房主端 `NetHostSvc.Instance.SendToAll(...)` 或 `SendToSession(sid, ...)`。
   房主端若要“识别这条消息来自哪个成员”，在处理器中读 `CurrentSid`（`Update()` 出队时写入，`Dispatch` 后清 0），**不要**自己从 DTO 里带 sid。

### B. 局域网联机最小闭环

```
房主：NetHostSvc.StartHost(房间名, 地图, 人数, 密码)
        → StartAsServer("0.0.0.0", hostPort) + LanBroadcaster.Start()（广播含 HostPort）
成员：new LanDiscoverer(端口) → StartListening() → Scan() → 等 1.5s → GetRooms()
        → NetSvc.ConnectToRoom(room, cb)  // StartAsClient + ConnectServer 握手
        → NetSvc.JoinRoom(name, pwd) / SetReady(true) / LeaveRoom()
房主：OnJoinRoomReq 校验(满员/密码/是否已进战斗) → JoinRoomRsp [+ 补发 TaskConfirmNtf] + BroadcastPlayerList
阶段链（⚠ 用户 2026-10-06 口径，命名都按它）：Bridge 选任务 →（房主 ConfirmTask 广播 TaskConfirmNtf）**Ready —— 仍在舰桥，仍可进人**
        → 所有人就位 → Armament（仍在舰桥各自选战备）→ 全员准备 → **Transition（房主 NotifyTransition 广播 TransitionNtf，各端各自加载战斗场景）** → Game
```

### C. 改端口 / 改广播参数

- 房主监听端口：`NetHostSvc` 的 `[SerializeField] hostPort`（默认 `NetConfig.HostGamePort` = 17666）。
- 广播参数：启动时调用一次 `LanDiscoveryConfig.Setup(broadcastPort: 39999, broadcastAddress: IPAddress.Parse("192.168.1.255"))` 等，全部参数可选。
- ⚠ 广播端口必须**所有玩家一致**才能互相发现，默认值一般不要改；改端口主要用于避开被占用端口。
- 同机双实例测试：房主占 29800，成员改用 29801（`NetConfig.LanSelfBroadcastPort`，Demo 里由 `isSame` 开关切换）。

### D. 升级/替换库 DLL 后核对 API

库无源码，换版本后**必须重新核对签名**，不要凭记忆改代码。两条路，按场景选：

1. **Unity 在跑 → 用 Unity MCP 原生反射（首选，权威且零额外依赖）**：

   ```
   unity_reflect(action="search", query="Lan", scope="project")          # 找类型
   unity_reflect(action="get_type", class_name="KCPNet.KCPNet`2")        # 成员摘要
   unity_reflect(action="get_member", class_name="KCPNet.KCPNet`2", member_name="ConnectServer")
   execute_code(code="return typeof(KCPNet.NetConfig).GetField(\"HostGamePort\").GetRawConstantValue();")
   ```

2. **Unity 没跑 / 只想看依赖矩阵 → Python 静态解析（不加载程序集，缺依赖也能用）**：

   ```bash
   python "d:\Pro\Bluedivers\.codebuddy\skills\kcpnet-online\scripts\inspect_dll.py" --refs-only
   python "d:\Pro\Bluedivers\.codebuddy\skills\kcpnet-online\scripts\inspect_dll.py"   # + 类型清单
   ```

   `--refs-only` 打印每个 DLL 的 `AssemblyRef`（依赖矩阵，判断“还缺哪个包”就靠它）；不带参数再打印 `TypeDef` 清单。签名/可选参数默认值不在静态解析范围内，用上面的 MCP 路径取。

升级后要重点确认的三件事：① `KCPNet.dll` 是否仍只引用 `MessagePack 3.1.8`（版本变了就要换 DLL）；② `NetConfig` / `LanDiscoveryConfig` 的静态默认值有没有变；③ `KCPNet<T,K>` 的构造与 `ConnectServer` 签名——`Assembly-CSharp` 里 `new KCPNet<...>()` 之所以合法，是因为库的 ctor 是 `KCPNet(bool isUnityEnvironment = true)`，**可选参数**，别误判成应该传参。

## 踩坑清单

- **会话钩子 / 连接回调都可能不在主线程**（2026-10-06 踩过）：`OnReciveMsg`、`OnConnected`、`OnDisConnected`，以及 `host.OnSessionConnected/OnSessionDisconnected` 这类 KCP `UpdateAsync` 续体，**全都在网络 / ThreadPool 线程**上跑 ⇒ 绝不能在里面碰 Unity API（`Debug.Log` 之外）或游戏状态（`_players` 之类的表也别改，有数据竞争）。`NetHostSvc.OnMemberLeft` 原来就是直接 `BroadcastPlayerList()` → 09 侧桥 `ResSvc.LoadRes` ⇒ `UnityException: Load can only be called from the main thread`；更阴的是它抛在**广播名单中途**，导致该清退的盟友留在场上变幽灵。
  现行模式：会话级事件只入 `Queue<SessionEvent> _sessionEvents`（`lock (pkgque_lock)`），由 `Update()` 开头的 `ProcessSessionEvents()` 在**主线程**按到达顺序处理（出队后在**锁外**处理，别占着锁做网络发送）。**新增任何会话级回调都照这个模式。**
  `MessageCenter.Dispatch` 里 `try/catch` 是为了兜住单个消息的处理异常，别删。
- **房主端会话必须带 sid**：`HostSession.OnReciveMsg` 丢的是 `AddMsgQue(msg, GetSessionID())`，漏了 sid 就无法定向回复/维护成员表。
- **消息未注册就报错**：`Dispatch` 查不到命令号会 `Debug.LogError($"未注册消息:{msg.CmdId}")`，看到它先查 `Register` 是否在 `Awake` 里漏调用、或 `Unregister` 是否提前执行。
- **MessagePack 版本写法**：`MessageCenter.Dispatch` 用的是非泛型 `MessagePackSerializer.Deserialize(Type, ReadOnlyMemory<byte>)`，参数顺序 `Type` 在前，`byte[]` 隐式转 `ReadOnlyMemory<byte>`，**不需要 `ref`**（3.1.8 的正确写法）。
- **开房 `SocketException`**：UDP 端口关闭后不会立即释放，刚 `StopHost()` 就 `StartHost()` 会抛。`NetHostSvc` 已 catch 转成清晰报错，`LanRoomDemo` 另有 2 秒冷却（`HostRestartCooldown`）。业务层应复刻这个冷却而不是重试死循环。
- **搜不到房间的排查顺序**：① 两台机器同一局域网；② 房主那台 Windows 防火墙首次弹窗必须勾“专用网络→允许”（最常见原因）；③ 顺序是先房主开房再成员搜房；④ 同机测试成员换 29801；⑤ 版本过滤 `VersionFilter` / `AutoExpire` 是否把房间滤掉。
- **`Scan()` 异步**：扫描后有 `DiscoveryTimeoutMs`(=1500ms) 量级的等待，Demo 用 `Invoke(nameof(PrintRooms), 1.5f)` 延后取结果；生产代码应轮询 `GetRooms()` 或按完成回调取，别在 `Scan()` 下一行就取列表。
- **排除自己开的房**：`NetHostSvc` 用时间戳生成 `_hostSelfName` 写进 `RoomInfo.PlayerNames[0]`，成员侧拿它过滤自己广播出来的房间（同机多实例也不会互相误判）。
- **`*.dll.meta` 里 `Editor: enabled: 0` 是无害的**：那是 Unity “Any Platform 托管插件”的标准写法（`Any.enabled: 1` 才是关键），不代表编辑器不能用。
- **`Assembly 'X' will not be loaded due to errors:` 怎么查**：Console 只给标题，真原因（`Unable to resolve reference 'Y'`）在 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 里紧跟其后几行。这类错误会**连锁**：`MessagePack.dll` 加载失败 ⇒ 引用它的 `KCPNet.dll` 失败 ⇒ 引用 KCPNet 的 `Assembly-CSharp.dll` 失败（伴随 `Assembly-CSharp-Editor.dll` 失败）。修好最底层那一个即可全通。
- **API 兼容级别是 .NET Standard 2.0（不是 2.1）**：`PlayerSettings.GetApiCompatibilityLevel(Standalone) == NET_Standard_2_0`（`ProjectSettings.asset` 里 `apiCompatibilityLevel: 6`），所以必须取 NuGet 的 `lib/netstandard2.0/` 资产；`lib/netstandard2.1/`（MessagePack 也有，依赖更少）在 Std2.0 profile 下不可用。改 API 级别属于全局变更，别顺手改。
- **别重复提供 Unity 自带的 BCL**：Unity 自带 `System.Memory 4.0.99.0`，能满足对 `4.0.1.2/4.0.1.0` 的引用；同理 `System.Buffers`/`System.Numerics.Vectors`/`System.Reflection.Emit*`/`System.Threading.Tasks.Extensions` 都不用补。多塞会触发“同名预编译程序集重复”。
- **别把 KCP 代码放进带 asmdef 的目录就直接用**：`NetTmp` 目前**没有 asmdef**，所以落在 `Assembly-CSharp`；一旦给它加 asmdef（或把它挪进 `01Manager`/`02Game` 等有 asmdef 的模块），**预编译 DLL 需要在 asmdef 里显式引用**，且 `08_Map` 这类程序集看不见 `Assembly-CSharp`，跨层只能传 `GameObject[]`/基础类型。
- **改已有 DLL 文件后**：Unity 可能不重新导入，用 `AssetDatabase.ImportAsset(path, ForceUpdate)` 或 `refresh_unity`；**新建** `.cs` 必须先 `refresh_unity(mode=force, scope=assets)`。

## 工具约定（本项目）

- **脚本一律用 Python**（本机已装 Python 3.12 + pip，`dnfile` 已装），不要再写 `.ps1`：小工具/批量文件操作/校验脚本都走 Python，本 skill 的 `scripts/` 就是范例。
- 不得不用 PowerShell 时注意两个坑：① 命令里的 `$var` 会被吞掉，复杂逻辑先写文件再 `-File` 执行；② `.ps1` 里出现中文会被 PowerShell 5.1 按 ANSI 解码，可能吐出引号字符导致“缺少字符串终止符”——脚本保持纯 ASCII。
- 「看/改 Unity 里的真实状态」优先 Unity MCP（`read_console` / `unity_reflect` / `execute_code`），它比任何外部反射脚本都权威。

## 验证手段

- 编译与运行期报错：Unity MCP `read_console`（先 `set_active_instance` 钉到 Bluedivers 实例）。
- 程序集是否真的加载 / 序列化是否可用：Unity MCP `execute_code`（打印 `AppDomain.CurrentDomain.GetAssemblies()`；做一次 `MessagePackSerializer` 往返）。
- 库 API 事实核对：Unity MCP `unity_reflect`（首选）或 `scripts/inspect_dll.py`（离线静态解析）。
- 依赖是否装齐（“还缺什么 / 要不要我手动装”）一条命令：`python scripts/install_deps.py --check`。
- 依赖矩阵（“这个 DLL 到底引用了谁”）：`scripts/inspect_dll.py --refs-only`，或 `install_deps.py` 打印的 nupkg 内容。
- 端到端联调：场景挂 `NetHostSvc` + `NetSvc` + `LanRoomDemo`，按 Demo 按键跑一遍“开房→搜房→回连→入房→准备→开局”，OnGUI 面板会显示房主/成员/房间/玩家四块状态。
