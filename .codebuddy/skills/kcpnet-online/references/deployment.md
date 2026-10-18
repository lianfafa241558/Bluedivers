# KCPNet 部署 / 取证 / API 原文留档

> 从 `SKILL.md` 搬出（2026-10-18 瘦身）。**只在排障、重装、升级 DLL 时读**。

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
| `KCPNet<T,K>` | `ctor(bool isUnityEnvironment = true)`、`StartAsClient(ip,port)`、`StartAsServer(ip,port)`、`Task<ConnectResult> ConnectServer(int interval, int maxintervalSum = 5000)`、`SendToAll(K)`、`TryGetSession(uint, out T)`、`GetSessions()`、`CloseClient()` / `CloseServer()`、事件 `OnSessionConnected/OnSessionDisconnected/OnServerRestartDetected(Action<uint>)`、字段 `clientSession`、**`ClientId`**（持久会话标识，库 2026-10-09 版新增 ⇒ 见踩坑清单"两行 clientId"） | `T`=会话类型，`K`=消息类型（本项目为 `NetMessage`） |
| `KCPSession<T>` | 抽象属性 `Serializer`；虚方法 `OnConnected()` / `OnDisConnected()` / `OnReciveMsg(T)` / `OnUpdate(DateTime)`；`SendMsg(T)` / `SendMsg(byte[])`、`IsConnected()`、`GetSessionID()`、`InitSession(...)`、`CloseSession()`；属性 `AesKey`、`TimeoutMs`、`EnableTimeoutCheck`、`SetTimeout(long)`、`HeartbeatIntervalMs` | 必须继承并 override `Serializer` + 至少 `OnReciveMsg`。⚠ **库基类自己每 3s 双向发"心跳裸字节"**（带魔数、**不进 `MessageCenter`**），超时看 `_lastRecvTimeUtcTicks`（15s，任意 UDP 包都刷）⇒ **它替代不了应用层 `PingReq/Rsp`**（1001/1002 是 `RttMs` 的唯一来源，且判"谁是玩家"的那只时钟口径不同） |
| `IKCPMsgSerializer` | `byte[] Serialize(KCPMsg)`、`KCPMsg Deserialize(byte[])` | 信封级序列化器，项目直接用库里的 `NetMessageSerializer.Instance` |
| `KCPMsg` / `NetMessage` | `NetMessage : KCPMsg`，字段 `int CmdId`、`byte[] Data` | 项目自己的“信封”，`Data` 是 DTO 的 MessagePack 字节 |
| `NetConfig` | 常量 `HostGamePort = 17666`、`LanBroadcastPort = 29800`、`LanSelfBroadcastPort = 29801`、`HeartbeatIntervalMs = 3000`、`DefaultTimeoutMs = 15000` | 端口/心跳唯一来源，别在业务里硬编码。⚠ 它在**库内部**（工程里没有 .cs）⇒ 只能逐会话设 `TimeoutMs` 等属性，常量改不了 |
| `LanDiscoveryConfig` | 静态 `BroadcastPort=29800`、`ListenPort=29800`、`BroadcastAddress=255.255.255.255`、`AnnounceIntervalMs=2000`、`DiscoveryTimeoutMs=1500`、`RoomExpireMs=6000`、`BroadcastTtl=1`、`Magic="KCPLAN:"`、`AnnounceType/DiscType`；`Setup(...)` 全部参数可选 | 改广播参数的唯一入口（`Setup` 注入，不改库） |
| `LanRoomInfo` | 字段 `RoomName/HostIp/HostPort/PlayerCount/MaxPlayers/MapName/PasswordProtected/Version/PlayerNames[]` + **`int Difficulty(-1)` / `int TaskMain(-1)` / `bool InGame` / `int Source`**（共 **13** 个）、`ToJson()/FromJson()` | 房间模型，广播包与 UI 都用它。⚠ 2026-10-08 换代后**难度/任务/是否开局/来源已是真字段**，别再串字符串；`FromJson` **忽略未知 key、缺 key 给默认值** ⇒ 新旧房主可混联 |
| `LanBroadcaster` | `ctor(LanRoomInfo)`、属性 `RoomInfoProvider : Func<LanRoomInfo>`、`Start()/Stop()` | 房主侧周期性 ANNOUNCE + 应答 DISCOVER |
| `LanDiscoverer` | `ctor(int listenPort = -1)`、属性 `VersionFilter`、`AutoExpire`、`StartListening()`、`Task Scan()`、`List<LanRoomInfo> GetRooms()`、`Stop()`、`PurgeExpired()` | 成员侧发现器；`Scan()` 是异步的，不能立刻取结果 |
| `KCPTool` | `Log/ColorLog/WarnLog/ErrLog`、`Compress/DeCompress`、静态委托 `LogFunc/ColorLogFunc/...` | 库日志出口，`KCPLogColor` 有 None/Red/Green/Blue/Cyan/Magenta/Yellow |
| `KCPNet.Util.*` | `ClientSecurity.GenerateAesKey()/EncryptAesKeyWithRsa()`、`ServerSecurity.*`、`EncryptionHelper.Encrypt/Decrypt` | 握手 RSA→AES 加密，库内部使用，项目当前未显式配置 |
| `KCPNet.Config.*` | `RegionConfig`（ServerIP/Redis/DataBase/MaxPlayers…）、`ServerRegionConfig.Current` | 专用服务器/分区模式的配置，**本项目 P2P 房主模式未使用** |

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
