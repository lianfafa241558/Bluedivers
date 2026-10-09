---
name: kcpnet-online
description: Bluedivers 项目的 KCPNet 联机网络层（预编译库 Assets/Plugins/KCPNet + 自研适配层 Assets/Scripts/NetTmp）专用指南。当需要部署/安装/升级 KCPNet.dll 与 Kcp.dll、安装或自检 NetTmp 需要的 NuGet 依赖（MessagePack 及其传递依赖，scripts/install_deps.py 与 --check）、排查 NetTmp 下 “未能找到类型或命名空间名 MessagePackObject” 之类 CS0246 编译报错、排查 `Assembly '...' will not be loaded due to errors` / `Unable to resolve reference` 这类程序集加载失败、新增或修改网络消息（CmdId 常量 + [MessagePackObject] DTO + MessageCenter.Register/Pack/Dispatch）、编写或调试 NetSvc / NetHostSvc / ClientSession / HostSession、实现局域网开房与搜房回连（LanDiscoverer / LanBroadcaster / LanRoomInfo / LanDiscoveryConfig）、排查开房 SocketException 或端口占用/防火墙/同机双实例问题、以及需要确认 KCPNet 库对外 API 与默认端口时触发。
---

# KCPNet 联机层（Bluedivers）

## Overview

Bluedivers 的联机能力由**四层**构成（⚠ **2026-10-09 已按依赖方向重构**：旧的"`NetTmp` 单目录 / 无 asmdef / 未挂载到场景"那套说法**全部作废**）：

| 层 | 位置 | 形态 | 能否改 |
| --- | --- | --- | --- |
| **预编译库 KCPNet** | `Assets/Plugins/KCPNet/KCPNet.dll` + `Kcp.dll` | 无源码的托管 DLL（netstandard2.0） | 否，只能反射/反编译看 API |
| **传输 / 编解码**（asmdef `02_Net`） | `Assets/Scripts/NetTmp/`（`Transport/` + `Client/` + `Services/`） | 项目源码；`references = []` | 是；**看不见任何游戏程序集** |
| **业务网络层**（asmdef `07_NetGame`） | `Assets/Scripts/07NetGame/`（`NetSvc`/`NetHostSvc`/`NetRoomFlow`/`NetTransformFlow`/`CmdId`/`RoomMeta`/`Msg/`） | 项目源码；`references = [02_Net]` | 是；**看不见 01/04/05/06** ⇒ DTO 只能用基础类型 |
| **消费方 + 三座桥** | `09_Managers`（`TeamNetBridge`/`NetFriendBridge`/`EnemyNetBridge`）、`10_UI`、`10_Effect` | `references` 含 `07_NetGame` | 是；**游戏对象 ↔ 网络值的转换只在这一层** |

- 库负责：UDP 收发、KCP 可靠传输（丢包重传/乱序重组）、会话（`KCPSession<T>`）与服务器（`KCPNet<T,K>`）抽象、局域网房间广播发现、RSA→AES 握手密钥。
- `02_Net` 负责：传输封装（`Transport/NetClient`、`Transport/NetServer`）、**入站队列**（`Client/NetInbox`）、会话钩子（`ClientSession`/`HostSession`）、信封与编解码（`MessageCenter` + `NetMsgCodec`）、传输层命令号（`NetCmdId`）。
- `07_NetGame` 负责：业务入口（`NetSvc` 成员 / `NetHostSvc` 房主）、**业务编排门面**（`NetRoomFlow`：所有收发的唯一入口）、位姿流（`NetTransformFlow`）、业务命令号（`CmdId`）、DTO（`Msg/RoomMsg|BattleMsg|SyncMsg`）、库缺字段适配（`RoomMeta`）。
- 通信模型：**房主权威 + 局域网 P2P**。房主客户端内嵌一个迷你“服务器”（`StartAsServer`）监听端口，成员先经 UDP 广播发现房间，再用 `StartAsClient` 回连房主。
- ⚠ 两个易踩的结论变更：① **`10_UI` 现在引用了 `07_NetGame`** ⇒ 旧的"UI 看不见网络层、必须绕道 09_Managers"已不成立（但"游戏对象 ↔ 网络值"仍应留在 09 的桥里）；② **`02_Net` 的 `references` 才是空的那个**，`07_NetGame` 引用 `02_Net`。

数据流（记住这条链就掌握了 80%）：

```
业务代码
  → MessageCenter.Pack(cmdId, dto)        // 内部走 NetMsgCodec（AOT 安全）→ NetMessage{CmdId, Data}
  → NetSvc.SendMsg(NetMessage)            // 成员端；房主端用 NetHostSvc.SendToAll/SendToSession
  → KCPSession<T>.SendMsg                 // 库：KCP 可靠发送
────────────────────── 网络 ──────────────────────
  → KCPSession<T>.OnReciveMsg(NetMessage) // 库回调（**传输线程**，绝不能碰 Unity API）
  → NetInbox.Enqueue(msg) / Enqueue(msg, sid)   // 加锁入队（NetTmp/Client/NetInbox.cs；房主端额外带 sid）
  → NetSvc.Update() / NetHostSvc.Update() 出队（主线程）
  → MessageCenter.Dispatch(msg)           // 查 CmdId 表 → NetMsgCodec 反序列化 → 调注册回调
```

## 当前部署状态（**2026-10-09 更新**：已挂载、已接入主流程、分层重构完成）

| 项 | 状态 | 证据 |
| --- | --- | --- |
| `KCPNet.dll` / `Kcp.dll` 放置 | ✅ `Assets/Plugins/KCPNet/`，PluginImporter 为 Any Platform（`platformData` 中 `Any.enabled: 1`） | `Assets/Plugins/KCPNet/*.meta` |
| **MessagePack 依赖闭包** | ✅ 已补齐（6 个 DLL，见下节），此前缺失导致全工程编译失败 | 装前：Console 大量 `CS0246 未能找到类型或命名空间名“MessagePackObject”`；装后：`read_console(types=["error"])` 返回 **0 条** |
| 程序集是否加载成功 | ✅ Unity 运行时加载 `Kcp 2.0.0.0`、`KCPNet 1.0.0.0`、`MessagePack 3.1.8.0`、`MessagePack.Annotations 3.1.8.0`；并实测 `LoginReqMsg` 序列化往返成功（14 字节） | `execute_code` 打印 `AppDomain.CurrentDomain.GetAssemblies()` |
| 适配层编译 | ✅ 引用 `KCPNet` + `MessagePack` 的是 **`02_Net.dll`**（`NetTmp`）—— ⚠ 2026-10-09 起网络代码**不再落在 `Assembly-CSharp`**，别再只看它 | 字节扫描 `Library/ScriptAssemblies/*.dll`（`install_deps.py --check` 已改成扫 02_Net / 07_NetGame / Assembly-CSharp 三个） |
| 挂载到场景 | ✅ **挂在 `GameRoot.prefab` 的子物体 `NetRoot` 下**：`NetSvc`/`NetHostSvc`/`NetRoomFlow`/`NetTransformFlow`/`TeamNetBridge`/`NetFriendBridge` 六个组件（2026-10-07 收拢，根节点从 13 个组件降到 7 个） | MCP 回读 prefab 层级 + 反射确认 |
| 被既有游戏逻辑引用 | ✅ 已接进主流程：大厅房间列表（`ServerListPanel`）、舰桥选任务/战备（`SelectMapWnd`/`BridgeSys`/`ArmamentWnd`）、开局（`TaskConfirmNtf`）、战斗（波次/敌人/位姿/命中/伤害）、盟友实体与 HUD | `09Manager/Global/{TeamNetBridge,NetFriendBridge}.cs`、`09Manager/Battle/EnemyNetBridge.cs` |

结论（2026-10-09）：**编译、加载、挂载、接入主流程都已通**；剩下的活是"把还没同步的玩法补齐"（见 `.codebuddy/plans/联机_未同步清单与同步方案.md`）。

⚠ 两条**安装点**硬约束（改挂载方式前务必先看）：
1. `GameRootBase.Awake/OnDestroy` 收集 `I_GlobaManager` **只扫「根物体 + 直接子物体」两级、不递归** ⇒ 常驻管理器挂**一级子物体**安全，挂更深会被**静默漏掉** Init/UnInit。
2. `NetManager` 名字带 Net，但它**与网络无关**（50Hz 逻辑帧宿主，已改名 `LogicFrameHost`，26 个 `LogicBehaviour` 靠它跑）⇒ 别把它和网络组件一起挪。

任何"KCP 相关改造"开始前，先用 `read_console` 确认当前状态。

## 部署清单（从零到可编译）

1. **放库**：`KCPNet.dll` + `Kcp.dll`（+ 可选 `KCPNet.pdb`、`log4net.config`，非必需）放同一目录，路径用 `Assets/Plugins/KCPNet/`。两个 DLL 保持默认 `Any Platform`，不要设 `defineConstraints`，也不要放进 `Editor/` 目录。
2. **补依赖闭包**（一次性命令，Python；默认目标就是下面这张表的完整 6 个）：

   ```bash
   python "d:\Pro\Bluedivers\.codebuddy\skills\kcpnet-online\scripts\install_deps.py"           # 装全部 6 个
   python "d:\Pro\Bluedivers\.codebuddy\skills\kcpnet-online\scripts\install_deps.py" --check   # 只读自检：装没装 / 还缺什么
   ```

   `--check` 会依次报告：6 个 DLL 是否存在 + 每个 `*.dll.meta` 的 Any Platform 标志、是否误放了 Unity 自带的同名程序集、`02_Net.dll` / `07_NetGame.dll` / `Assembly-CSharp.dll` 是否已引用 `KCPNet`/`MessagePack`、以及 **Editor.log 里最近一次域重载**的未解析引用（历史失败会被识别为历史，不会误报）。

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
5. **挂载**：不需要你手动挂 —— 已在 `GameRoot.prefab` 的子物体 `NetRoot` 下（6 个组件，见"当前部署状态"）。只有**新建**常驻管理器时才需要动 prefab，且必须挂**根或一级子物体**。
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

## 适配层结构与职责（2026-10-09 分层后）

| 程序集 / 目录 | 放什么 | 代表文件 |
| --- | --- | --- |
| `02_Net` / `Assets/Scripts/NetTmp/` | 传输、入站队列、会话钩子、信封与编解码、传输层命令号 | `Transport/NetClient`、`Transport/NetServer`、`Client/{ClientSession, HostSession, NetInbox, MessageCenter}`、`Services/{NetCmdId, NetMsgCodec}` |
| `07_NetGame` / `Assets/Scripts/07NetGame/` | 业务入口、业务编排门面、位姿流、业务命令号、DTO、库缺字段适配 | `NetSvc`、`NetHostSvc`、`NetRoomFlow`、`NetTransformFlow`、`CmdId`、`RoomMeta`、`Msg/{RoomMsg, BattleMsg, SyncMsg}` |
| `09_Managers`（三座桥） | 网络值 ↔ 游戏对象 | `Global/TeamNetBridge`、`Global/NetFriendBridge`、`Battle/EnemyNetBridge` |

更细的逐文件要点、**命令号全表（4001–4040 / 5001–5002 / 传输层 1001–1002）**、阶段链、DTO 约定与 `RoomMeta` 字符串约定见 `references/nettmp-layer.md`。

⚠ 分层之后写代码的落位规则：**新消息/DTO 放 `07_NetGame`（只用基础类型）**；**需要碰 `SO`/枚举/`GameObject` 的转换放 `09_Managers` 的桥里**；**别在 `02_Net` 里引用任何游戏层类型**。

## 主要工作流

### A. 新增一条网络消息（四步，固定顺序）

1. `07NetGame/CmdId.cs` 加命令号常量，遵循分段（新模块另开千位段，编号不重复、不复用、不改旧值）。传输层控制报文另放 `NetTmp/Services/NetCmdId.cs`（1xxx）。
2. `07NetGame/Msg/` 下（可按模块新建文件，如 `SyncMsg.cs`）加 `[MessagePackObject]` 类，字段用 `[Key(0..n)]` 连续编号。新增字段**追加在末尾**并取下一个 Key，不要插在中间。**字段只能用基础类型**（`07_NetGame` 看不见 `01_GameContract`/`04_Data`/`06_Gameplay`）。
3. 在 `Awake()` 中注册处理器：`MessageCenter.Register<XxxMsg>(CmdId.Xxx, OnXxx);`，并在 `OnDestroy()` 中 `MessageCenter.Unregister(CmdId.Xxx);`（房主侧协议处理则由 `NetHostSvc` 注册）。
4. 发送：**业务侧优先走 `NetRoomFlow`**（它把"房主本地自派发 + 广播 / 成员发给房主"收敛成一套 `Send*`/`On*`）；底层才是
   成员端 `NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.Xxx, dto))`、房主端 `NetHostSvc.Instance.SendToAll(...)` 或 `SendToSession(sid, ...)`）。
   房主端若要“识别这条消息来自哪个成员”，在处理器中读 `CurrentSid`（`Update()` 出队时写入，`Dispatch` 后清 0），**不要**自己从 DTO 里带 sid。

### B. 局域网联机最小闭环

```
房主：NetHostSvc.StartHost(HostRoomOptions{ RoomName, MapName, MaxPlayers, Password, Difficulty, HostName, TaskType, TaskMain })
        → StartAsServer("0.0.0.0", hostPort) + LanBroadcaster.Start()（广播含 HostPort）
        ⚠ 房间名/地图名里的 `#T=枚举|任务类型` 与 `#难度` 由 RoomMeta.ComposeRoomName/ComposeMapName 拼（库缺字段的临时约定）
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
- 同机双实例测试：房主占 29800，成员把**发现器**挂到 29801 —— `new LanDiscoverer(NetConfig.LanSelfBroadcastPort)`（或 `LanDiscoveryConfig.Setup(listenPort: ...)`）；广播端口仍须一致。

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

升级后要重点确认的三件事：① `KCPNet.dll` 是否仍只引用 `MessagePack 3.1.8`（版本变了就要换 DLL）；② `NetConfig` / `LanDiscoveryConfig` 的静态默认值有没有变；③ `KCPNet<T,K>` 的构造与 `ConnectServer` 签名——适配层（`02_Net`）里 `new KCPNet<...>()` 之所以合法，是因为库的 ctor 是 `KCPNet(bool isUnityEnvironment = true)`，**可选参数**，别误判成应该传参。

## 踩坑清单

- **会话钩子 / 连接回调都可能不在主线程**（2026-10-06 踩过）：`OnReciveMsg`、`OnConnected`、`OnDisConnected`，以及 `host.OnSessionConnected/OnSessionDisconnected` 这类 KCP `UpdateAsync` 续体，**全都在网络 / ThreadPool 线程**上跑 ⇒ 绝不能在里面碰 Unity API（`Debug.Log` 之外）或游戏状态（`_players` 之类的表也别改，有数据竞争）。`NetHostSvc.OnMemberLeft` 原来就是直接 `BroadcastPlayerList()` → 09 侧桥 `ResSvc.LoadRes` ⇒ `UnityException: Load can only be called from the main thread`；更阴的是它抛在**广播名单中途**，导致该清退的盟友留在场上变幽灵。
  现行模式：会话级事件只入 `Queue<SessionEvent> _sessionEvents`（`lock (pkgque_lock)`），由 `Update()` 开头的 `ProcessSessionEvents()` 在**主线程**按到达顺序处理（出队后在**锁外**处理，别占着锁做网络发送）。**新增任何会话级回调都照这个模式。**
  `MessageCenter.Dispatch` 里 `try/catch` 是为了兜住单个消息的处理异常，别删。
- **房主端会话必须带 sid**：`HostSession.OnReciveMsg` 丢的是 `NetInbox.Enqueue(msg, GetSessionID())`，漏了 sid 就无法定向回复/维护成员表。
- **消息未注册就报错**：`Dispatch` 查不到命令号会 `Debug.LogError($"未注册消息:{msg.CmdId}")`，看到它先查 `Register` 是否在 `Awake` 里漏调用、或 `Unregister` 是否提前执行。
- **MessagePack 版本写法**：`MessageCenter.Dispatch` 用的是非泛型 `MessagePackSerializer.Deserialize(Type, ReadOnlyMemory<byte>)`，参数顺序 `Type` 在前，`byte[]` 隐式转 `ReadOnlyMemory<byte>`，**不需要 `ref`**（3.1.8 的正确写法）。
- **开房 `SocketException`**：UDP 端口关闭后不会立即释放，刚 `StopHost()` 就 `StartHost()` 会抛。`NetHostSvc` 已 catch 转成清晰报错；**开房入口自己要加"关房后 N 秒内不许再开"的冷却**（原 Demo 的 `HostRestartCooldown`=2s 做法，Demo 已删），别用重试死循环。
- **搜不到房间的排查顺序**：① 两台机器同一局域网；② 房主那台 Windows 防火墙首次弹窗必须勾“专用网络→允许”（最常见原因）；③ 顺序是先房主开房再成员搜房；④ 同机测试成员换 29801；⑤ 版本过滤 `VersionFilter` / `AutoExpire` 是否把房间滤掉。
- **`Scan()` 异步**：扫描后有 `DiscoveryTimeoutMs`(=1500ms) 量级的等待，Demo 用 `Invoke(nameof(PrintRooms), 1.5f)` 延后取结果；生产代码应轮询 `GetRooms()` 或按完成回调取，别在 `Scan()` 下一行就取列表。
- **排除自己开的房**：`NetHostSvc` 用时间戳生成 `_hostSelfName` 写进 `RoomInfo.PlayerNames[0]`，成员侧拿它过滤自己广播出来的房间（同机多实例也不会互相误判）。
- **`*.dll.meta` 里 `Editor: enabled: 0` 是无害的**：那是 Unity “Any Platform 托管插件”的标准写法（`Any.enabled: 1` 才是关键），不代表编辑器不能用。
- **`Assembly 'X' will not be loaded due to errors:` 怎么查**：Console 只给标题，真原因（`Unable to resolve reference 'Y'`）在 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 里紧跟其后几行。这类错误会**连锁**：`MessagePack.dll` 加载失败 ⇒ 引用它的 `KCPNet.dll` 失败 ⇒ 引用 KCPNet 的 `Assembly-CSharp.dll` 失败（伴随 `Assembly-CSharp-Editor.dll` 失败）。修好最底层那一个即可全通。
- **API 兼容级别是 .NET Standard 2.0（不是 2.1）**：`PlayerSettings.GetApiCompatibilityLevel(Standalone) == NET_Standard_2_0`（`ProjectSettings.asset` 里 `apiCompatibilityLevel: 6`），所以必须取 NuGet 的 `lib/netstandard2.0/` 资产；`lib/netstandard2.1/`（MessagePack 也有，依赖更少）在 Std2.0 profile 下不可用。改 API 级别属于全局变更，别顺手改。
- **别重复提供 Unity 自带的 BCL**：Unity 自带 `System.Memory 4.0.99.0`，能满足对 `4.0.1.2/4.0.1.0` 的引用；同理 `System.Buffers`/`System.Numerics.Vectors`/`System.Reflection.Emit*`/`System.Threading.Tasks.Extensions` 都不用补。多塞会触发“同名预编译程序集重复”。
- **网络代码要放进对的程序集**（2026-10-09 分层后）：`02_Net`（`NetTmp`）`references=[]` ⇒ 只放传输/编解码，**不能引用任何游戏层类型**；业务消息/DTO 放 `07_NetGame`（只引用 `02_Net`）⇒ **字段只能用基础类型**（要传枚举就传 `int`，例：`TaskCfgDto`）；需要碰 `SO`/枚举/`GameObject` 的转换放 `09_Managers` 的桥里（`TeamNetBridge`/`NetFriendBridge`/`EnemyNetBridge`）。
  预编译插件（无 asmdef 的 DLL）默认被**所有** asmdef 自动引用，通常不必显式写；只有给 asmdef 勾了 `overrideReferences` 才需要手动加。
- **改已有 DLL 文件后**：Unity 可能不重新导入，用 `AssetDatabase.ImportAsset(path, ForceUpdate)` 或 `refresh_unity`；**新建** `.cs` 必须先 `refresh_unity(mode=force, scope=assets)`。
- **「同一条 socket 被库反复重握手」**（2026-10-09）：房主 Console 看起来像"一直有人进进出出"、sid 一串，其实只有一个客机。查法：`HostSession.PeerAddress`（反射读库私有 `m_remotePoint`）看对端是不是同一个；`HostSession` 已给 `OnConnected/OnDisConnected` **限流**（前 3 次 + 之后每 10s，带累计次数）。
- **「未入房会话循环重连」**：库按"**收不到数据**判掉线"（~15s），而"连上 → 入房"之间只有心跳一条报文 ⇒ 房主对**未入房**会话必须也回 Pong（`OnPingReq` 不要先判 `_players` 里有没有），否则它被自己的库判死并重连。
- **退房/踢人要"立刻关会话 + 清四张表"**：`_players` / `_profiles` / `_poses` / `_lastSeen`。⚠ `_poses` 不清 ⇒ 该 sid 的**幽灵位姿会继续跟着每批快照下发**。踢人路径还要防"提示两遍"：`EvictIdleMembers` 先本地清再 `CloseSession`，库又会回调 `OnSessionDisconnected` ⇒ 用 `HashSet<uint> _selfClosed` + `CloseSessionOf(sid)`（先登记再关）去重，`StopHost` 清集合。
- **`memberIdleTimeout` 默认 8s，比库自己的 15s 更严** ⇒ 客户端一卡帧（尤其中途加载战斗场景）就超时被踢；而战斗期 `_joinClosed`/`_rosterFrozen` 会**拒绝重连** ⇒ 表现是"没关游戏却掉线且回不来"。要动就三选一：抬高超时、转场期间不踢、支持重连复用身份。
- **跨端的"下标"都不可信**：任务表 `TaskCfgs` 的行下标来自 `Resources.LoadAll` 枚举顺序（编辑器 ↔ 打包版可不同）⇒ 生成端按 `mapId` 排序（`TaskManager.OrderedMaps/MapIndex`）**且**本局配置随 `TaskConfirmNtf.Cfg`（`TaskCfgDto`）**下发内容**，成员侧 `SetTask(..., remoteCfg)` 以内容为准、不查本地表；端到端指纹因此从"硬校验"降级为**诊断告警**（否则跨窗口/后进房的玩家会被永久拒之门外）。
- **那条"后进房的人也能拿到本局配置"靠的是补发**：`NetHostSvc._lastTaskConfirm` 缓存**整条**广播，`OnJoinRoomReq` 里给新人补发（在 `JoinRoomRsp` 之后，KCP 可靠有序）⇒ 消息必须**自足**（种子 + 配置内容），别设计成"让成员自己查表"。
- **`Dispatch` 的异常要打全**：`Debug.LogError($"消息 {cmdId} 处理失败:{e}")` —— 只打 `e.Message` 会把"哪个处理器、哪一行"全丢掉（打包版实测过）。

## 工具约定（本项目）

- **脚本一律用 Python**（本机已装 Python 3.12 + pip，`dnfile` 已装），不要再写 `.ps1`：小工具/批量文件操作/校验脚本都走 Python，本 skill 的 `scripts/` 就是范例。
- 不得不用 PowerShell 时注意两个坑：① 命令里的 `$var` 会被吞掉，复杂逻辑先写文件再 `-File` 执行；② `.ps1` 里出现中文会被 PowerShell 5.1 按 ANSI 解码，可能吐出引号字符导致“缺少字符串终止符”——脚本保持纯 ASCII。
- ⚠ 本机 PowerShell 另有 **Safe delete bulk-guard**：`Remove-Item -Recurse` 会被拦（报 `Safe delete could not verify this bulk deletion. Nothing was deleted.`）⇒ **删文件/目录别用 `Remove-Item -Recurse`**，走工具（`delete_file`）或 `del` 单个文件。
- ⚠ PowerShell 的 `Select-String -Path "**/*.cs"` **不支持递归通配**，会静默给出"0 命中"的**假阴性** ⇒ 递归扫描一律用 `Get-ChildItem -Recurse -Filter *.cs | Select-String …`（或直接用本 skill 的 Python 脚本）。
- 「看/改 Unity 里的真实状态」优先 Unity MCP（`read_console` / `unity_reflect` / `execute_code`），它比任何外部反射脚本都权威。
- ⚠ `execute_code` 默认用 **CodeDom（返回里 `"compiler":"codedom"`）= C# 6** ⇒ 探针代码**别用**本地函数、`is T x` 模式匹配等 C# 7+ 语法；写错只会报含糊的 `Unexpected symbol '('`。要 C# 7+ 得传 `compiler: "roslyn"`（依赖 Microsoft.CodeAnalysis）。
- **程序集/脚本编译的"到底编没编"判据（比 `refresh_unity` 的返回值可靠）**：比较 `.cs` 与 `Library/ScriptAssemblies/<asm>.dll` 的 **mtime**，以及 `EditorApplication.isCompiling`；`refresh_unity` 常**报成功却没编译**（`external_changes_dirty:false`、`is_focused:false`），此时用
  `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)` 推；偶发 `isCompiling` **长时间卡住**（非 Play、Console 无错）⇒ 等编辑器恢复焦点或手动 Ctrl+R。
- **离线编译**（不依赖 Unity 在跑，按 asmdef 逐个编）：`python d:\Pro\Bluedivers\.codebuddy\plans\offline_compile.py <程序集名…>`（如 `02_Net 07_NetGame 09_Managers`）。
  ⚠ 它取的是 Unity 生成的 `.csproj`，**新建的 `.cs` 不在列表里会报一串假错**（"找不到类型"）⇒ 新建文件后先让 Unity 编译一次再跑它，或用它自带的追加机制。

## 验证手段

- 编译与运行期报错：Unity MCP `read_console`（先 `set_active_instance` 钉到 Bluedivers 实例）。
- 程序集是否真的加载 / 序列化是否可用：Unity MCP `execute_code`（打印 `AppDomain.CurrentDomain.GetAssemblies()`；做一次 `MessagePackSerializer` 往返）。
- 库 API 事实核对：Unity MCP `unity_reflect`（首选）或 `scripts/inspect_dll.py`（离线静态解析）。
- 依赖是否装齐（“还缺什么 / 要不要我手动装”）一条命令：`python scripts/install_deps.py --check`。
- 依赖矩阵（“这个 DLL 到底引用了谁”）：`scripts/inspect_dll.py --refs-only`，或 `install_deps.py` 打印的 nupkg 内容。
- 端到端联调（**用游戏自己的 UI，Demo 已删**）：一端在大厅「公开/开房」（`SelectMapWnd`/`SettingWnd` → `NetRoomFlow.Host`），另一端开 `ServerListPanel`（大厅 Server 按钮）搜房 → 加入 → 就位 → 房主选任务 `ConfirmTask` → 战备 → 转场。
  日志锚点：`[NetHostSvc] 房主服务器已启动` / `玩家 X 加入房间` / `已广播本局配置（局号 N…）`、`[TeamNetBridge] 已确认本局配置`、`[NetFriendBridge] 创建盟友实例 sid=…`。
- 排查"两端状态不一致"的第一现场：`NetRoomFlow` 的事件有没有被订阅方漏掉 + `MessageCenter` 有没有 `未注册消息:N` 的 LogError。
- **AOT 地雷自检（一条命令，必须 0 致命命中）**：

  ```bash
  python "d:\Pro\Bluedivers\.codebuddy\skills\kcpnet-online\scripts\check_aot_unsafe.py"
  ```

  扫全仓 `.cs` 且**区分注释与真代码**（字符串字面量里的 `//` 不会误判），查三类：
  ① **致命** = `MessagePackSerializer` / `DynamicObjectResolver` / `MessagePack.Resolvers` / `GeneratedMessagePackResolver` 的**真调用**（IL2CPP 必抛 `PlatformNotSupportedException`）；
  ② **分层违规** = `MessagePackWriter` / `MessagePackReader` 出现在 `NetTmp`（传输层）之外；
  ③ 参考 = 动态代码生成相关（`AssemblyBuilder`/`ILGenerator`/`Reflection.Emit`，不判失败）。
  退出码 0/1（可直接接 CI），`-v` 连"只在注释里出现"的也列出来；`--root X` 可换扫描根。
  ⚠⚠ 判据是"**代码里有没有调用**"，不是"字符串出现过"：`install_deps.py --check` 的字节扫描对 **`07_NetGame.dll` 报 `MessagePack=True` 是正常的** —— 那来自 DTO 上的 `[MessagePackObject]` / `[Key(n)]` 特性（特性类型在 MessagePack 程序集里，必然产生 TypeRef），而**这些特性必须保留**：`NetMsgCodec` 靠**反射读 `[Key]`** 决定 array 字段顺序与"追加在末尾"的兼容语义。⇒ **别去清 `07_NetGame` 对 MessagePack 的依赖**（`07_NetGame.asmdef` 里本来也没显式列它，`overrideReferences:false` 下插件对所有 asmdef 自动可见）。
