---
name: kcpnet-online
description: Bluedivers 项目的 KCPNet 联机网络层（预编译库 Assets/Plugins/KCPNet + 自研适配层 Assets/Scripts/NetTmp）专用指南。当需要部署/安装/升级 KCPNet.dll 与 Kcp.dll、安装或自检 NetTmp 需要的 NuGet 依赖（MessagePack 及其传递依赖，scripts/install_deps.py 与 --check）、排查 NetTmp 下 “未能找到类型或命名空间名 MessagePackObject” 之类 CS0246 编译报错、排查 `Assembly '...' will not be loaded due to errors` / `Unable to resolve reference` 这类程序集加载失败、新增或修改网络消息（CmdId 常量 + [MessagePackObject] DTO + MessageCenter.Register/Pack/Dispatch）、编写或调试 NetSvc / NetHostSvc / ClientSession / HostSession、实现局域网开房与搜房回连（LanDiscoverer / LanBroadcaster / LanRoomInfo / LanDiscoveryConfig）、排查开房 SocketException 或端口占用/防火墙/同机双实例问题、以及需要确认 KCPNet 库对外 API 与默认端口、**排查"某玩法在两端状态不一致 / 没同步 / 键对不上 / 回环"这类同步问题时触发**。
---

# KCPNet 联机层（Bluedivers）

> ⚠ **2026-10-18 瘦身**：实现细节与长尾坑移入 `references/`（**不随 skill 注入**），本文件只留判据/口径/工作流。

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

## 现状（2026-10-09）与两条安装点硬约束

- **编译 / 加载 / 挂载 / 接入主流程都已通**：DLL 在 `Assets/Plugins/KCPNet/`（Any Platform）、MessagePack 依赖闭包已补齐、
  运行时加载 `Kcp 2.0.0.0` / `KCPNet 1.0.0.0` / `MessagePack 3.1.8.0`，适配层落 **`02_Net.dll`**（2026-10-09 起**不再在 `Assembly-CSharp`**）。
  各条证据（Console / 反射 / 字节扫描）→ `references/deployment.md`。
- **挂载**：`GameRoot.prefab` 的子物体 `NetRoot` 下 6 个组件（`NetSvc` / `NetHostSvc` / `NetRoomFlow` / `NetTransformFlow` / `TeamNetBridge` / `NetFriendBridge`）。
- ⚠⚠ **两条安装点硬约束**：① `GameRootBase.Awake/OnDestroy` 收集 `I_GlobaManager` **只扫根 + 一级子物体、不递归** ⇒ 挂更深会被**静默漏掉** Init/UnInit；
  ② `NetManager`（现 `LogicFrameHost`）**与网络无关**（50Hz 逻辑帧宿主，26 个 `LogicBehaviour` 靠它跑）⇒ 别把它和网络组件一起挪。
- 任何 KCP 改造前先 `read_console` 确认当前状态；剩下的是"把还没同步的玩法补齐"。

## 部署清单（从零到可编译）

1. **放库**：`KCPNet.dll` + `Kcp.dll` 放 `Assets/Plugins/KCPNet/`（默认 Any Platform，不设 `defineConstraints`、不放 `Editor/`）。
2. **补依赖闭包**：`python .../scripts/install_deps.py`（`--check` 只读自检）。⚠ 只装 **6 个**（MessagePack / Annotations / Bcl.AsyncInterfaces /
  NET.StringTools / Collections.Immutable / CompilerServices.Unsafe），**不要**再放 `System.Memory`/`System.Buffers` 等 Unity 自带 BCL（会"同名重复"）。
  逐 DLL 版本、为什么需要、Nupkg 取哪个 target → `references/deployment.md`。
3. **刷新**：`refresh_unity(mode=force, scope=all, compile=request)` → `read_console` 看 `CS0246` 残留。
4. **验证四连**（缺一不可，只看 Console 会漏"程序集加载失败"）：① `read_console(types=["error"])` = 0；② 反射 `GetAssemblies()` 见 `KCPNet`/`MessagePack`；
  ③ 真实序列化往返；④ 报 `Assembly '…' will not be loaded` ⇒ 去 `Editor.log` 看紧随其后的 `Unable to resolve reference 'X'`。
5. **挂载**：只有**新建**常驻管理器才动 prefab，且只能挂**根或一级子物体**。
6. **AOT（IL2CPP）已修好，别再重走**：MessagePack 的 `DynamicObjectResolver`（`Reflection.Emit`）在打包版必抛 `PlatformNotSupportedException`，
  **编辑器（Mono）完全看不出来** ⇒ 适配层已改走 `NetMsgCodec`（`MessagePackWriter/Reader`，array 格式按 `[Key]` 升序）⇒ 新增消息仍只需"CmdId + `[MessagePackObject]` + `[Key(n)]`"。
  ⚠ 红线：保留 `Assets/link.xml`（否则打包后反序列化全是默认值）、新类型要在 `WriteValue/ReadValue` 补分支、别引 `GeneratedMessagePackResolver`（`internal`）。原理与备选路 → `references/deployment.md`。

## 库 API 速查（完整签名 / 依赖矩阵 → `references/kcpnet-api.md`）

- **端口与心跳的唯一来源 `NetConfig`**（⚠ 它在**库内部**，工程里没有 .cs ⇒ 常量改不了，只能逐会话设 `TimeoutMs`）：
  `HostGamePort=17666`、`LanBroadcastPort=29800`、`LanSelfBroadcastPort=29801`、`HeartbeatIntervalMs=3000`、`DefaultTimeoutMs=15000`。
- **`KCPSession<T>` 的心跳坑**：库基类自己每 3s 发「心跳裸字节」（带魔数、**不进 `MessageCenter`**，超时 15s）⇒ **替代不了应用层 `PingReq/Rsp`**
  （1001/1002 才是 `RttMs` 唯一来源）。`KCPNet<T,K>` 的 ctor 是 `KCPNet(bool isUnityEnvironment = true)`（可选参数，别误判成要传参）。
- 其余签名（`KCPNet<T,K>` 成员、`LanDiscoveryConfig`/`LanRoomInfo`/`LanBroadcaster`/`LanDiscoverer`、信封与日志出口）→ `references/kcpnet-api.md`。

## 适配层结构与职责（2026-10-09 分层后）

| 程序集 / 目录 | 放什么 | 代表文件 |
| --- | --- | --- |
| `02_Net` / `Assets/Scripts/NetTmp/` | 传输、入站队列、会话钩子、信封与编解码、传输层命令号 | `Transport/NetClient`、`Transport/NetServer`、`Client/{ClientSession, HostSession, NetInbox, MessageCenter}`、`Services/{NetCmdId, NetMsgCodec}` |
| `07_NetGame` / `Assets/Scripts/07NetGame/` | 业务入口、业务编排门面、位姿流、业务命令号、DTO、库缺字段适配 | `NetSvc`、`NetHostSvc`、`NetRoomFlow`、`NetTransformFlow`、`CmdId`、`RoomMeta`、`Msg/{RoomMsg, BattleMsg, SyncMsg}` |
| `09_Managers`（三座桥） | 网络值 ↔ 游戏对象 | `Global/TeamNetBridge`、`Global/NetFriendBridge`、`Battle/EnemyNetBridge` |

更细的逐文件要点、**命令号全表（4001–4040 / 5001–5002 / 传输层 1001–1002）**、阶段链、DTO 约定与 `RoomMeta` 字符串约定见 `references/nettmp-layer.md`。

⚠ 分层之后写代码的落位规则：**新消息/DTO 放 `07_NetGame`（只用基础类型）**；**需要碰 `SO`/枚举/`GameObject` 的转换放 `09_Managers` 的桥里**；**别在 `02_Net` 里引用任何游戏层类型**。

## 主要工作流

### A. 新增一条网络消息（六步 + 验证，固定顺序）

> 决策口径见下方 §E「同步一条新东西」。**别跳过第 4/5 步** —— 历史上一半的"两端不一致"都出在桥与回环门上。

1. `07NetGame/CmdId.cs` 加命令号常量，遵循分段（新模块另开千位段，编号不重复、不复用、不改旧值）。传输层控制报文另放 `NetTmp/Services/NetCmdId.cs`（1xxx）。
2. `07NetGame/Msg/` 下（可按模块新建文件，如 `SyncMsg.cs`）加 `[MessagePackObject]` 类，字段用 `[Key(0..n)]` 连续编号。新增字段**一律追加在末尾**并取下一个 Key，不要插在中间（对端拿默认值即向后兼容）。**字段只能用基础类型**（`07_NetGame` 看不见 `01_GameContract`/`04_Data`/`06_Gameplay`；要传枚举就传 `int`）。
3. **在 `NetRoomFlow` 里加三件套**（它是业务收发的唯一入口，别在别处注册业务消息）：
   `public static event Action<XxxMsg> OnXxx;` + `public void SendXxx(...)`（房主 `NetHostSvc.SendToAll`、成员 `NetSvc.SendMsg`）+ `private void HandleXxxNtf(XxxMsg m) => OnXxx?.Invoke(m);`；
   再到 `Awake()` `MessageCenter.Register<XxxMsg>(CmdId.Xxx, HandleXxxNtf)`、`OnDestroy()` `Unregister`（⚠ 同一个 CmdId **只能有一个注册者**）。
4. **在 09 侧写桥**（`Net*Bridge`）：订阅"本地产生的那个事件"→ 上报；订阅 `NetRoomFlow.OnXxx` → 应用。桥是**唯一**能同时看见游戏层与网络层的地方（05/06 看不见 07）。
5. **回环门**：应用远端时会派发本地事件（`Operate()` 之类）⇒ 用 `NetApplyGuard.Begin()/End()` 包住；**两条桥可能互相回环时，门必须共享**（别各持一个私有 bool）。
6. **过滤与自派发**：上报点先判 `flow == null || !flow.InRoom`（单机/不在房里直接 return）；接收端按 `Sid` 丢掉"自己发出去绕回来的那条"。⚠ **房主广播的任何状态都要再 `MessageCenter.Dispatch(msg)` 本地自派发一次**（`SendToAll` 按定义不含房主自己）。
   房主端若要"**识别这条消息来自哪个成员**"，在处理器里读 **`CurrentSid`**（`Update()` 出队时写入、`Dispatch` 后清 0），**不要**自己从 DTO 里带 sid。

**验证**（别只看 Console 0 error）：① `offline_compile.py <asm>…`；② 新增 `.cs` 让 Unity 编一次后**反射核验类型/成员真的存在**（防"假空转"，见踩坑清单）；③ 两端都开 `NetSyncLog` 跑一遍，对照 `·发` / `·收` 两侧日志。


### B. 局域网联机最小闭环

```
房主：NetHostSvc.StartHost(HostRoomOptions{ RoomName, MapName, MaxPlayers, Password, Difficulty, HostName, TaskType, TaskMain })
        → StartAsServer("0.0.0.0", hostPort) + LanBroadcaster.Start()（广播含 HostPort）
        ⚠ 房间字段**不用再串字符串**了：2026-10-08 库换代后 `LanRoomInfo` 已有真字段
        （`Difficulty` / `TaskMain` / `InGame` / `Source`，见 `references/kcpnet-api.md` §6），
        旧的 `RoomMeta.ComposeRoomName` / `#T=` 约定**已删除**（2026-10-11 复核：全仓 0 命中）；`RoomMeta` 现在只剩"取值薄适配"
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

库无源码 ⇒ **换版本必须重新核对签名，不要凭记忆改代码**：

1. **Unity 在跑 → 用 Unity MCP 反射（首选）**：`unity_reflect(action="search"|"get_type"|"get_member", …)`
   / `execute_code` 读 `NetConfig` 常量。
2. **Unity 没跑 → `scripts/inspect_dll.py --refs-only`**（打依赖矩阵，缺包就靠它）/ 不带参数打 `TypeDef` 清单（签名与默认值不在静态解析范围内）。

升级后重点确认三件：① `KCPNet.dll` 是否仍只引用 `MessagePack 3.1.8`（版本变了就得换 DLL）；② `NetConfig`/`LanDiscoveryConfig` 的静态默认值；
③ `KCPNet<T,K>` 的 ctor 与 `ConnectServer` 签名。命令与本轮实测记录 → `references/deployment.md`。

### E. 同步一条新东西（决策树，2026-10-11 补）

听到"XX 两端不一致"时按这五问走，**别上来就加消息**：

> ⚠ **2026-10-12 起实现已换代（五问判据不变，但别照旧代码抄）**：号空间统一 + 三档取号（战斗前派生号 / 战斗后敌人自增号 / 房主生成段 `0x2000_0000`），房主唯一创建统一走 **4052 `HostSpawnNtf` + `INetSpawner`/`SpawnerRegistry`/`WaveManager.CreatUnit`**（`4051`/`4047` 已停发、常量保留）。实现细节 → `memory/topics/net-netid.md`・`net-spawn.md`・`net-damage.md`・`net-vehicle*.md`。

**① 键用哪个？** 跨端稳定 = "创建时确定 + 两端一致"；⚠ 一律在 `Awake`/生成时算一次并缓存 —— 会位移的实体不能每次现算。

| 对象 | 键 | 注意 |
| --- | --- | --- |
| 家具 / 终端 | `Furniture_Attached.SyncId = FNV1a(Id + 世界坐标0.1m)` | 依赖"`BaseObject` 配好 Id + 摆位两端一致"；量化 0.1m 吃掉浮点抖动 |
| 任务 | `MissionBase.netOrder` | 创建序赋值；场景模式按 0.1m 位置排序（`MissionController.AssignNetOrdersByPosition`） |
| 场景单位（NPC） | `Actor.Id` | 同名多实例会撞 ⇒ 成组刷的单位改用 NetId |
| 成组新生成的单位（波次 / 巡逻队） | **生成点**（+ 逐单位目标点） | 构成靠"按落点派生流"⇒ 同落点同构成，不必传 |
| 波次 / 敌人 | `Actor.NetId`（`Actor.Awake` 取号、每局归零） | 0 = 非同步；⚠ 分配点必须在 `Awake`（放 `WaitSetPos` 会晚一帧）；⚠ 2026-10-12 起已改**三档判据**（战斗前派生号 / 战斗后自增 / 房主生成段），见 `net-netid.md` |
| ⛔ **不能当键** | `Actor.IndexID`（进程内自增、跨局不清零）、`Furniture_NumberID`（各端自增）、`Resources.LoadAll` 下标 | 两端必然不同 |

**② 谁权威？**
- **房主**：生成（波次 / 巡逻队 / 空投）、生死、任务结果、加载闸门、位姿聚合、共享选择（选哪个撤离点）。
- **操作者那台**：玩家点出来的东西（终端阶段 4046 / 终端按键 4049）。口径 = "**有人操作时只有操作者所在的那台推；无人阶段（Load/Wait）才归房主**"；判定收口在 `KeyScreen.IsStageDriver()`，别在别处再判一遍。
- ⛔ **成员端绝对不能"本地生成"**：`Actor.Awake` 会吃 `NetId` 号段 ⇒ 与房主下发的单位**撞号**（后续 `EnemyMoveSync`/`EnemyDiedSync` 作用到别的单位上）。生成类一律"成员闸门（return 空）+ 房主广播生成事件 + 成员按**同一生成点**复刻"（`WaveManager.CreatWave` / `CreatPatrol` / `CreatPatrolRemote`）。
- ⛔ **别拿"本机视角"做共享选择**：`最近玩家` / `本机玩家位置` / `本机背包` 这类输入两端必然不同（撤离点选择就踩过）⇒ 换成"**触发者位置 / 房主位置**"。

**③ 同步"操作重放"还是"结果状态"？**
- 效果写在**交互入口**（`Operate()`）里的 ⇒ 走"重放 `Operate()`"（4036 的路子，一处收口）。
- 效果写在**过程**里的（`IStepPress.StepPress` 逐个扣背包 + `OnSubmit`）⇒ 重放 `Operate()` **只能收尾**；两条出路：① 送"结果状态"（如 `held` / `_destroyed` 账本）；② 给远端造一个可扣的**替身** —— `FriendOOPartInventory`（幽灵背包）⇒ 真跑 `BeginPress/StepPress`（4048 的做法）。

**④ 应用远端会不会触发本地事件（回环）？**
会就必须进回环门，且**门要跨桥共享**：`ApplyRemoteStage` → `furn.Operate()` → `GlobalEventBus.FurnitureOperate` → 又回到 `NetFurnitureBridge` 的上报点。
⇒ 统一用 `NetApplyGuard.Begin()/End()`（**计数式**，`internal static`，`Applying` 供各桥读）。

**⑤ 有没有非幂等副作用？**（改地形高度图、重算包围盒、累加计数）
⇒ 远端重放时**必须跳过**：`Furniture_Attached.ApplyRemoteOperate` 期间 `IsRemoteReplay == true`，`Furniture_Pipe.Place()` 靠它挡掉 `ModifyHeightMap`（两端各挖一次会**叠加**）。

**降级手段（还没做同步时的"治乱"）**：随机类优先改成**纯函数** `SeedUtil.Derive(seed, 细分键)`，而不是一条 `System.Random(seed)` 流（两端消费次数不同就错位）；单机（`TaskState.Seed == 0`）保留原随机路径。

## 诊断日志（`00Tools/NetSyncLog.cs` —— 排查联机的第一工具）

- 三路分管 `Bullet` / `Ai` / `Sync`，另有 `Warn`（"不正常"用 Warning，便于 Console 筛）与**限流重载** `AiLog(tag, msg, key, interval)` —— **每帧都会走的调用点必须用它**，否则"一只怪一行/帧"直接把 Console 刷爆。
- ⚠ 实测默认 `Enabled = true`（**两端都开**）。静默：启动参数 `-nonetsynclog` / 环境变量 `NETSYNC_LOG` / `PlayerPrefs["NetSyncLog"]`。**正式发布前应改成"只认启动参数"**（否则常年把 `Player.log` 刷爆）。
- 放 `00_Utils`（05/06/09/10 都引用它）；⚠ **`07_NetGame` 只引用 `02_Net`、看不见它** ⇒ 传输层的日志点要放在 09 的桥里。
- 读日志：编辑器 = Console（MCP `read_console(filter_text="…")`）；打包端 = `Player.log`（启动时会打 `Application.consoleLogPath`）。
- 常用 tag：`家具/操作·发|收`、`家具/键`、`家具/状态·发|收`、`终端/发|收`、`巡逻/发|收`、`容器/发|收`、`可破坏物·发|收`。
- ⭐ **口径**：凡是"**找不到对应实体**"这类静默 `return`，必须留 `Warn` + **打整张键表**（否则现象就是"这个东西怎么都不同步"，且完全无从下手）。
- **两端对照法**：同一条消息在两端各留一条 `·发` / `·收`（带"键 + 值 + 发起 sid"）⇒ 一眼分清"键不一致 / 本端没生成 / 根本没收到"。

## 踩坑清单
> 长尾细节（MessagePack 版本写法 / `Assembly 'X' will not be loaded` 连锁 / API 兼容级别 Std2.0 / BCL 重复 / `dll.meta` 的 `Editor: enabled: 0` /
> 改已有 DLL 后重导入 / 反复重握手 / 未入房会话循环重连 / `memberIdleTimeout` 8s 比库 15s 更严 / 后进房补发配置 / `Dispatch` 异常要打全 / PowerShell 三个坑）
> → `references/pitfalls.md`（**只有真踩到再去读**）。下面留的是每次改联机代码都要过的判据。



- **会话钩子 / 连接回调都可能不在主线程**（2026-10-06 踩过）：`OnReciveMsg`、`OnConnected`、`OnDisConnected`，以及 `host.OnSessionConnected/OnSessionDisconnected` 这类 KCP `UpdateAsync` 续体，**全都在网络 / ThreadPool 线程**上跑 ⇒ 绝不能在里面碰 Unity API（`Debug.Log` 之外）或游戏状态（`_players` 之类的表也别改，有数据竞争）。`NetHostSvc.OnMemberLeft` 原来就是直接 `BroadcastPlayerList()` → 09 侧桥 `ResSvc.LoadRes` ⇒ `UnityException: Load can only be called from the main thread`；更阴的是它抛在**广播名单中途**，导致该清退的盟友留在场上变幽灵。
  现行模式：会话级事件只入 `Queue<SessionEvent> _sessionEvents`（`lock (pkgque_lock)`），由 `Update()` 开头的 `ProcessSessionEvents()` 在**主线程**按到达顺序处理（出队后在**锁外**处理，别占着锁做网络发送）。**新增任何会话级回调都照这个模式。**
  `MessageCenter.Dispatch` 里 `try/catch` 是为了兜住单个消息的处理异常，别删。
- **房主端会话必须带 sid**：`HostSession.OnReciveMsg` 丢的是 `NetInbox.Enqueue(msg, GetSessionID())`，漏了 sid 就无法定向回复/维护成员表。
- **消息未注册就报错**：`Dispatch` 查不到命令号会 `Debug.LogError($"未注册消息:{msg.CmdId}")`，看到它先查 `Register` 是否在 `Awake` 里漏调用、或 `Unregister` 是否提前执行。
- **开房 `SocketException`**：UDP 端口关后**不会立即释放**，刚 `StopHost()` 就 `StartHost()` 必抛 ⇒ 开房入口要加"关房后 N 秒内不许再开"的冷却（~2s），别用重试死循环。`NetHostSvc` 已 catch 成清晰报错。
- **搜不到房间**：按 `memory/topics/net-connect.md` 的顺序查（防火墙入站放行 → 客户端隔离 → 虚拟网卡 → 同机换 29801 → 版本门槛）；**别去查 `HostIp` 的值**（它由库按收包 source IP 填）。
- **`Scan()` 异步**：扫描后有 `DiscoveryTimeoutMs`(=1500ms) 量级的等待，Demo 用 `Invoke(nameof(PrintRooms), 1.5f)` 延后取结果；生产代码应轮询 `GetRooms()` 或按完成回调取，别在 `Scan()` 下一行就取列表。
- **排除自己开的房**：`NetHostSvc` 用时间戳生成 `_hostSelfName` 写进 `RoomInfo.PlayerNames[0]`，成员侧拿它过滤自己广播出来的房间（同机多实例也不会互相误判）。
- **网络代码要放进对的程序集**（2026-10-09 分层后）：`02_Net`（`NetTmp`）`references=[]` ⇒ 只放传输/编解码，**不能引用任何游戏层类型**；业务消息/DTO 放 `07_NetGame`（只引用 `02_Net`）⇒ **字段只能用基础类型**（要传枚举就传 `int`，例：`TaskCfgDto`）；需要碰 `SO`/枚举/`GameObject` 的转换放 `09_Managers` 的桥里（`TeamNetBridge`/`NetFriendBridge`/`EnemyNetBridge`）。
  预编译插件（无 asmdef 的 DLL）默认被**所有** asmdef 自动引用，通常不必显式写；只有给 asmdef 勾了 `overrideReferences` 才需要手动加。
- **退房/踢人要"立刻关会话 + 清四张表"**：`_players` / `_profiles` / `_poses` / `_lastSeen`。⚠ `_poses` 不清 ⇒ 该 sid 的**幽灵位姿会继续跟着每批快照下发**。踢人路径还要防"提示两遍"：`EvictIdleMembers` 先本地清再 `CloseSession`，库又会回调 `OnSessionDisconnected` ⇒ 用 `HashSet<uint> _selfClosed` + `CloseSessionOf(sid)`（先登记再关）去重，`StopHost` 清集合。
- **跨端的"下标"都不可信**：任务表 `TaskCfgs` 的行下标来自 `Resources.LoadAll` 枚举顺序（编辑器 ↔ 打包版可不同）⇒ 生成端按 `mapId` 排序（`TaskManager.OrderedMaps/MapIndex`）**且**本局配置随 `TaskConfirmNtf.Cfg`（`TaskCfgDto`）**下发内容**，成员侧 `SetTask(..., remoteCfg)` 以内容为准、不查本地表；端到端指纹因此从"硬校验"降级为**诊断告警**（否则跨窗口/后进房的玩家会被永久拒之门外）。
- **应用远端消息会"派发本地事件"（回环）**（2026-10-11）：`ApplyRemoteOperate` 跑的还是本地 `Operate()` ⇒ 会触发本端桥订阅的 `FurnitureOperate` / `UnitEventBus` 等 ⇒ 不挡就是无限回环。⚠ 门要**跨桥共享**：终端阶段的应用会走到 `furn.Operate()`（`NetFurnitureBridge` 的订阅点），各持一个私有 bool 挡不住 ⇒ 统一 `NetApplyGuard`（计数式）。
- **`SendToAll` 按定义不含房主自己**：凡"房主广播的状态"，都要**再 `MessageCenter.Dispatch(msg)` 自派发一次**（名单、开局、巡逻队、可破坏物都踩过）。
- **成员端不得本地生成单位**：`Actor.Awake` 会吃 `NetId` 号段 ⇒ 与房主下发的单位撞号（错杀 / 错移）。生成类一律"成员闸门 + 房主广播生成事件 + 按同一生成点复刻"（见 §E ②）。
- **别拿"本机视角"做共享选择**：`最近玩家` / `本机玩家位置` 这类输入两端必然不同（撤离点选择踩过）⇒ 由**触发者 / 房主**定（见 §E ②）。
- **随机同步要纯函数**：`SeedUtil.Derive(seed, 细分键)`（`SeedStream` 枚举）；⚠ 一条 `System.Random(seed)` 流在"重试 / 两端调用次数不同"时**必然错位**（谜题、炼油都踩过）。
- **"改了却没编上"的两种假象**（假空转 / 假缺失 meta）：判据与解药见 `memory/topics/tooling.md §编译验证`；**决定性判据永远是反射**（`GetType`/`GetMethod` 是否为 null），别只看 Console 的 0 error。
- **改完一批联机代码，收尾做三件**：`offline_compile.py` 编一遍 → 让 Unity 编一次并**反射核验新成员** → 双端开 `NetSyncLog` 对照 `·发`/`·收`。

## 工具约定（本项目）

- **脚本一律用 Python**（本机已装 Python 3.12 + pip，`dnfile` 已装），不要再写 `.ps1`：小工具/批量文件操作/校验脚本都走 Python，本 skill 的 `scripts/` 就是范例。
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
