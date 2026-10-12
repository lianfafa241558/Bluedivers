# 网络层逐文件要点（**2026-10-09 结构，已重构为三层**）

> ⚠⚠ 本文档旧版描述的是"`NetTmp` 单目录 + 无 asmdef + 编译进 `Assembly-CSharp` + **未挂载到场景**"——
> **那套说法已全部作废**。网络层已按依赖方向拆成三层并接进了游戏主流程，见 §0。

## 0. 三层与程序集（先记住这张表）

| 层 | asmdef | 目录 | `references` | 约束 |
| --- | --- | --- | --- | --- |
| **传输 / 编解码** | `02_Net` | `Assets/Scripts/NetTmp/` | **`[]`**（只 Unity + BCL） | 看不见任何游戏程序集 ⇒ 不许引用 SO/枚举/游戏类型 |
| **业务网络层** | `07_NetGame` | `Assets/Scripts/07NetGame/` | **`[02_Net]`** | 看不见 `01_GameContract` / `04_Data` / `05_UnitCore` / `06_Gameplay` ⇒ **DTO 只能用基础类型**（枚举要当 `int` 传，见下方的 `TaskCfgDto`） |
| **消费方** | `09_Managers` / `10_UI` / `10_Effect` | — | 都含 `07_NetGame` | ⚠ **`10_UI` 现在看得见网络业务层** —— 旧的"UI 看不见网络层、所以必须绕道 `09_Managers`"的结论**已作废** |

**分层规则**：DTO/命令号/协议放 `07_NetGame`（只用基础类型）；队列与编解码放 `02_Net`；**游戏对象 ↔ 网络值的转换只能在 09/10 做**（`09Manager/Global/TeamNetBridge.cs`、`NetFriendBridge.cs`、`EnemyNetBridge.cs` 就是这三座桥）。

> 这套拆法的收益（也是拆的顺序）：让 `02_Net` 能"下沉"到任何地方复用 —— 会话类不再反向依赖游戏层的 `NetSvc/NetHostSvc`（旧版 `ClientSession` 直接调 `NetSvc.Instance.AddMsgQue`）。

## 1. 数据流与线程边界（最重要，重构后仍成立）

```
[主线程]   业务 → MessageCenter.Pack(cmdId, dto)      // 内部走 NetMsgCodec（AOT 安全）
[主线程]   NetSvc.SendMsg / NetHostSvc.SendToAll|SendToSession
[主线程]   KCPSession<NetMessage>.SendMsg → 库写 UDP（KCP 自带 update 循环）
─────────────── 网络 ───────────────
[传输线程] 库回调 KCPSession.OnReciveMsg(msg)
[传输线程] NetInbox.Enqueue(msg) / NetInbox.Enqueue(msg, sid)   // 静态队列 + lock
[主线程]   NetSvc.Update() / NetHostSvc.Update() 出队 → MessageCenter.Dispatch(msg) → 注册的 Action<T>
```

- 队列已从游戏层搬到传输层：**`NetTmp/Client/NetInbox.cs`**（`_client` / `_host` 两条 `Queue` + 一个 `_lock`；房主那条带 `Sid`）。
  ⚠ 旧的 `NetSvc.pkgque_lock` / `NetHostSvc.AddMsgQue` 已不再是队列所有者（类里可能仍留有薄的取件封装）。
- **任何 `OnReciveMsg` / `OnConnected` / `OnDisConnected` 都在库的线程池线程上** ⇒ 只能做纯数据操作（入队、`Debug.Log`、`DateTime`），**不能碰 Unity API、不能改游戏表**。

## 2. `02_Net`（`Assets/Scripts/NetTmp/`）逐文件

| 文件 | 职责 / 要点 |
| --- | --- |
| `Transport/NetClient.cs` | 成员端传输封装：持有 `KCPNet<ClientSession, NetMessage>`，暴露 `IsConnected`、`RttMs`、`CloseClient()`；心跳（`PingReq`）也在这一层。 |
| `Transport/NetServer.cs` | 房主端传输封装：`StartAsServer(0.0.0.0, port)`、会话表、`SendToAll`/`TryGetSession`、`CloseServer()`；`SocketException` 在此转成清晰报错。 |
| `Client/ClientSession.cs` | 成员端会话钩子：`Serializer => NetMessageSerializer.Instance`；`OnReciveMsg → NetInbox.Enqueue(msg)`。 |
| `Client/HostSession.cs` | 房主端会话钩子：`OnReciveMsg → NetInbox.Enqueue(msg, GetSessionID())`（**必须带 sid**）；另有 `PeerAddress`（反射读库私有 `m_remotePoint`，只为日志排查）；`OnConnected/OnDisConnected` **做了日志限流**，见 §7 的"反复握手"。 |
| `Client/NetInbox.cs` | 传输层入站队列（成员 / 房主两条）+ `TryDequeueClient/TryDequeueHost` + `ClearClient/ClearHost`。 |
| `Client/MessageCenter.cs` | 注册表 + 打包 + 分发：`Register<T>(cmdId, Action<T>)` / `Pack<T>` / `Dispatch` / `Unregister`。⚠ **`Pack`/`Dispatch` 现在走 `NetMsgCodec`**（不再是 `MessagePackSerializer`，见 SKILL 的 AOT 一节）；`Dispatch` 的 catch 打 **`e.ToString()`**（只打 `e.Message` 会丢掉处理器与行号）。 |
| `Services/NetCmdId.cs` | **传输层自有命令号**：`1001 PingReq` / `1002 PingRsp`（与游戏命令号分表，别往这里塞业务）。 |
| `Services/NetMsgCodec.cs` | AOT 安全的 MessagePack 读写器：只用 `MessagePackWriter/Reader`，`[MessagePackObject]` 类型按 **array 格式 + `[Key]` 升序**；反射成员（靠 `Assets/link.xml` 保全）。新增字段类型要在 `WriteValue/ReadValue` 补分支。 |
| `Services/Msg/PingMsg.cs` | 心跳 DTO。 |

## 3. `07_NetGame`（`Assets/Scripts/07NetGame/`）逐文件

| 文件 | 职责 / 要点 |
| --- | --- |
| `NetSvc.cs` | 成员端总入口（MonoBehaviour，单例）：`ConnectToRoom(room, cb)`、`JoinRoom/LeaveRoom/SetReady`、`SendMsg`、`Update()` 出队分发、`IsConnected`、`RttMs`。 |
| `NetHostSvc.cs` | 房主端总入口（48KB，最大）：`StartHost(HostRoomOptions)` / `StopHost` / 成员表 / 房间协议处理 / `SendToAll` / `SendToSession` / `ConfirmTask` / `NotifyTransition` / `SetHostReady` / 位姿下发节流（`poseBroadcastHz`）/ 场景 actor 快照应答。**所有会话级回调都走"入队 → 主线程处理"**。 |
| `NetRoomFlow.cs` | 业务编排层（41KB）：把"上行/下行"收敛成一套 `Send*` / `On*`（房主本地自派发 + 广播，成员发给房主）；对外暴露 `Instance`、`IsHost`、`SelfSid`、`CurrentMatchId` 与一堆静态事件（`OnPlayerList/OnTaskConfirm/OnTransition/OnArmamentSync/OnBoosterSync/OnWeaponSwitch/OnShoot/OnVital/OnAmmo/OnSceneActorReq/OnSceneActors/OnAirdropCall/OnSpeech/OnPlayerLeft/OnWaveStart/OnEnemyMove/OnEnemyHitUp/OnEnemyDamaged/OnGameOver/OnEvacuate/…`）。**业务层要收发都从它走，别直接找 NetSvc/NetHostSvc。** |
| `NetTransformFlow.cs` | 玩家位姿同步（成员上行 `PlayerTransformUp`、房主聚合广播 `TransformBatchSync`；对外 `OnPose` 事件）。 |
| `CmdId.cs` | **游戏业务命令号全表**（见 §5）。 |
| `RoomMeta.cs` | **库缺字段期间的唯一适配点**（房间名/地图名里的 `#T=` / `#` 约定解析，逐条标了 `TODO(库)`），见 §6。 |
| `Msg/RoomMsg.cs` | 房间 + 开局 + 玩家表现 + 敌人同步的 DTO（26KB，见 §5.2）。
  ⚠ DTO 上的 `[MessagePackObject]` / `[Key(n)]` **必须保留**（`NetMsgCodec` 反射读 `[Key]` 决定 array 顺序）⇒ `07_NetGame` 对 `MessagePack` 的依赖是**必需的**，别去"清理"；真正要禁的是 `MessagePackSerializer` 调用（全仓 0 处，只在注释里）。 |
| `Msg/BattleMsg.cs` | `PoseSnapshot` / `PoseBatchMsg`（位姿批次）。 |
| `Msg/SyncMsg.cs` | 局内世界状态：`GameOverMsg` / `EvacuateMsg` / `MissionUpdateMsg` / `FurnitureOperateMsg`（⚠ 已扩 `int State` + `string StateKey`，**StateKey 非空 = 状态同步**，接收端走 `ApplyRemoteState` 而**不**重放 `Operate()`）/ `MarkMsg` / `CallKaiMsg` / `WaveCenterMsg` / `SceneDestructibleMsg` / `KeyScreenStageMsg` / `PatrolSpawnMsg` / `OOPartSubmitMsg` / `KeyScreenInputMsg`。 |

## 4. 挂载点与三座桥

- **常驻组件**：`Assets/Resources/Prefabs/Manager/GameRoot.prefab` → 子物体 **`NetRoot`** 下挂 6 个：
  `NetSvc` / `NetHostSvc` / `NetRoomFlow` / `NetTransformFlow` / `TeamNetBridge` / `NetFriendBridge`。
  ⚠⚠ `GameRootBase.Awake/OnDestroy` 收集 `I_GlobaManager` **只扫「根物体 + 直接子物体」两级、不递归** ⇒ 常驻管理器挂**一级子物体**安全，挂更深会被**静默漏掉** Init/UnInit。
  （`NetManager` 名字带 Net 但与网络无关——它是 50Hz 逻辑帧宿主，已改名 `LogicFrameHost`，留在根上。）
- **三座桥**（`09_Managers`，负责"网络值 ↔ 游戏对象"，因为 07/02 都看不见游戏层）：
  | 桥 | 位置 | 职责 |
  | --- | --- | --- |
  | `TeamNetBridge` | `09Manager/Global/` | 名单/准备/资料/战备/强化/开局（`SetSeed` + `SetTask`）/转场 |
  | `NetFriendBridge` | `09Manager/Global/` | 盟友实体：创建/销毁/模型/武器/位姿/血盾/弹药/切枪/开火/喊话 |
  | `EnemyNetBridge` | `09Manager/Battle/` | 敌人：NetId、移动意图、命中上报、房主结算、死亡、伤害下行 |
- 场景/其它：`NetRoomFlow` 的房间列表 UI 在 `10UI/Comp/ServerListPanel.cs`；舰桥战备在 `BridgeSys` + `ArmamentWnd`。

## 5. 协议

### 5.1 阶段链（**命名按用户 2026-10-06 口径**）

```
Bridge（舰桥选任务）--房主 ConfirmTask 广播 TaskConfirmNtf--> Ready（**仍在舰桥，仍可进人**）
   --所有人就位--> Armament（仍在舰桥各自选战备）--全员准备--> Transition（房主 NotifyTransition 广播 TransitionNtf，
   各端**各自**加载战斗场景）--> Game
```
⚠ "开局"不是某一条消息：**配置**由 `TaskConfirmNtf` 定（含种子），**加载**由 `TransitionNtf` 触发。

### 5.2 命令号全表（`07NetGame/CmdId.cs`，2026-10-09）

| 命令号 | 常量 | 方向 / 用途 |
| --- | --- | --- |
| 1001 / 1002 | `NetCmdId.PingReq / PingRsp` | 传输层心跳（在 `NetTmp/Services/NetCmdId.cs`） |
| 4001 / 4002 / 4003 / 4004 / 4005 | `JoinRoomReq / JoinRoomRsp / LeaveRoomNtf / PlayerListSync / ReadyState` | 房间协议 |
| 4006 | `TaskConfirm` | 房主 → 全体：本局配置（`MapName/Difficulty/TaskIndex/ExtraDiff/Seed/PlayMode/TaskFingerprint/MatchId/Cfg`），进 Ready |
| 4007 | `PlayerProfileNtf` | 成员 → 房主：资料（角色/等级/武器/改装/战备/强化/准备） |
| 4008 / 4009 / 4010 / 4011 | `PlayerArmamentNtf/Sync`、`PlayerBoosterNtf/Sync` | 战备 / 全队强化（房主自己也走 Sync） |
| 4012 | `Transition` | 房主 → 全体：进 Transition（同时开始加载战斗） |
| 4013–4016 | `PlayerWeaponSwitchNtf/Sync`、`PlayerShootNtf/Sync` | 切枪 / 开火（**只做表现，不结算伤害**） |
| 4017–4020 | `PlayerVitalNtf/Sync`、`PlayerAmmoNtf/Sync` | 盟友血盾倒地 / 弹药系数 |
| 4021 / 4022 | `SceneActorReq / Ntf` | 场景单位（NPC）快照：成员请求、房主应答 |
| 4023 | `WaveStartSync` | 房主 → 全体：开波（时机 + 参数，成员按同一 `waveIndex` 复刻） |
| 4024 / 4025 / 4026 | `EnemyMoveSync` / `EnemyHitNtf` / `EnemyDiedSync` | 敌人移动意图 / 成员上报命中 / 房主广播死亡 |
| 4027 / 4028 | `AirdropCallNtf / Sync` | 局内呼叫战备 |
| 4029 | `PlayerLeftNtf` | 房主 → 全体：某成员离开。⚠ 与 `PlayerListSync` 分开：战斗期名单**冻结**（重排会让下标漂移），但"有人走了"必须传出去 ⇒ 只带 sid、不重排 |
| 4030 / 4031 | `SpeechNtf / Sync` | 角色喊话 |
| 4032 | `SceneUnitMoveSync` | 场景单位移动目标（键是 `Actor.Id`，不是 NetId） |
| 4033–4039 | `GameOverNtf` / `EvacuateNtf` / `MissionUpdateNtf` / `FurnitureOperateNtf` / `MarkNtf` / `CallKaiNtf` / `WaveCenterNtf` | 局内世界状态（见 `Msg/SyncMsg.cs`） |
| 4040 | `EnemyDamagedSync` | 房主 → 全体：房主打中的伤害下行（与 4025 成对，血量口径统一"房主权威"） |
| 4041 | `LoadCompleteNtf` | 成员 → 房主：本机战斗场景加载完成 |
| 4042 | `BattleStartSync` | 房主 → 全体：**全体加载完 ⇒ 一起开打**（各端凭它开 `IsStartBattle`）。⚠ 与 4012 分开：各端加载快慢差很多 |
| 4043 | `SceneDestructibleNtf` | 双向：场景可破坏物（油桶这类）被打掉。键 = `FNV1a(Actor.Id + 位置0.1m)`（同 `SyncId` 口径）；**只传"死了"不传伤害**；房主侧有账本、新人入房补发 |
| 4044 / 4045 | `PlayerLoadoutNtf / Sync` | 改装（档位 / 模组 / 载具）上报与广播 |
| 4046 | `KeyScreenStageNtf` | 双向：**按键终端(KeyScreen)阶段推进**（键 = 终端家具 `SyncId`）。⚠ **驱动端**上报（操作者那台 / 无人阶段房主）、其余端只应用（见 `KeyScreen.IsStageDriver`） |
| 4047 | `PatrolSpawnNtf` | 房主 → 全体：**巡逻队生成**（生成点 + 逐单位目标点）。构成靠"同落点派生流"，成员端 `CreatPatrolRemote` 复刻 |
| 4048 | `OOPartSubmitNtf` | 双向：容器类家具的一次提交（`SyncId` + 个数 + 主类型）⇒ 远端在盟友的**幽灵背包**(`FriendOOPartInventory`)上真跑 `BeginPress/StepPress` |
| 4049 | `KeyScreenInputNtf` | 双向：玩家对终端的**按键**（`SyncId` + `InputState` + 类型 0 单次/1 按住/2 松开）⇒ 非驱动端把输入换成远端按键、跑同一套阶段逻辑 |
| 5001 / 5002 | `PlayerTransformUp` / `TransformBatchSync` | 位姿：成员上行 / 房主聚合广播 |

### 5.3 DTO 约定

- `[MessagePackObject]` + 每字段 `[Key(n)]`；**新字段一律追加在末尾并取下一个 Key**（旧版端拿默认值），不要插在中间。
- 方向写进注释（照现有消息的风格）。
- **`07_NetGame` 看不见游戏层枚举/SO** ⇒ 网络 DTO 里**只准基础类型**；需要传枚举就用 `int`，转换在 09/10 的桥里做。
  例：`TaskCfgDto` 全字段是 `int/int[]/float/bool/string`（承载 `TaskCfg` 的内容：`main/extra/nestCount/seed/scale/terrain/enemyVariety/enable/name`）。
- ⚠ **`DamageMsg`（4025 / 4040 / 4043 三个号共用的"伤害·生死"DTO，9 字段）**：
  `Sid(0) / NetId(1) / Key(2) / Damage(3) / AttackerNetId(4) / Groups(5) / Part(6) / IsDeath(7) / CausedNetId(8)`。
  两个来源号**语义不同、别混**：`AttackerNetId` = **起源**（谁该记这笔账，统计口径，语义自上线未变）、
  `CausedNetId` = **直接来源**（炮台 / 地雷 / 补给舱本身，显示与 AI 口径）。
  接收端两者**各自**解析（`EnemyNetBridge.ResolveNetEmitter(号)` → 失败再 `ResolveRemoteUser(sid)` → 都失败 `NoSource`）；
  发送端 `attackerNetId` 取起源单位的 `NetId`、`causedNetId` 取直接来源单位的 `NetId`（玩家都没有号 ⇒ 都是 0 ⇒ 靠 sid 找盟友实例）。
  ⚠ `CausedNetId` 是 **2026-10-12 追加**的（旧端 `Skip()` 掉 ⇒ 显示退化成"开枪者"，能跑但显示不准）⇒ **破坏性协议变更，双端必须同版本换包**。

## 6. 房间字段的读取（`RoomMeta` = 取值薄适配层）

⚠ **2026-10-11 复核：旧写法作废。** 本文档之前这节讲"库缺字段 ⇒ 把难度/任务类型串进 `MapName`/`RoomName`（`#难度`、`#T=枚举|名字`）"——
那套约定**已经删掉了**（全仓搜 `ComposeRoomName` / `#T=` / `TODO(库)` 均 **0 命中**）。原因：**2026-10-08 库换代后这些已是 `LanRoomInfo` 的真字段**（`Difficulty` / `TaskMain` / `InGame` / `Source`，见 `references/kcpnet-api.md` §6）。

现在 `RoomMeta`（`07NetGame/RoomMeta.cs`，仍存在）只剩**取值薄适配**：

| 成员 | 作用 |
| --- | --- |
| `RoomMeta.RoomName(room)` / `MapName(room)` | 空引用保护 + 空串归一 |
| `RoomMeta.Difficulty(room)` | `room.Difficulty`，`< 0` = 广播里没带 ⇒ 返缺省 |
| `RoomMeta.TaskEnum(room)` / `TaskType(room)` | `room.TaskMain`（**两端一致的整数**）；名字用本地任务配置反查（⚠ 任务名不唯一，别只靠名字） |
| `RoomMeta.InGame(room)` | `room.InGame` ⇒ 房间列表"进行中"、且不给加入 |
| `RoomMeta.Source(room)` | `room.Source`（`RoomMeta.SourceEnum.LanBroadcast` 等）⇒ 判"局域网 / 服务器" |
| `RoomMeta.SourceEnum` | 来源枚举（房主侧 `StartHost` 写 `Source = (int)RoomMeta.SourceEnum.LanBroadcast`） |

要点：
- **唯一入口**：UI（`ServerListPanel` 等）一律经 `RoomMeta` 取值，**不自己判缺省**（面板里"取值"那一组方法全是转发）。房间字段要加/改，先动库的 `LanRoomInfo` 与 `FromJson`，再改 `RoomMeta`，UI 一行不用动。
- 房主侧：`NetHostSvc.StartHost` 直接写字段字面量；`GetBroadcastInfo()` 每轮刷 `InGame = _started`（把"最新值"喂给 `RoomInfoProvider`）；`ConfirmTask(..., int taskMain)` 就地刷 `RoomInfo.TaskMain`。
- `FromJson` **忽略未知 key、缺 key 给默认值** ⇒ 新旧版本的房主/成员可混联（`Difficulty < 0` 就是"对面没带这个字段"的表达）。


## 7. 联机踩坑（按症状查）

- **「只有两个客户端，房主的 Console 却一直在报成员进进出出」**：同一条 socket 被库**反复重握手**（`HostSession.OnConnected` 反复触发、sid 一串）⇒ 先看 `HostSession.PeerAddress`（反射读库的 `m_remotePoint`）确认对端是否同一个；`HostSession` 里已给这对回调**限流**（前 3 次 + 之后每 10s）。
- **「未入房就断线、循环重连」**：库的判活是"**收不到数据**就判掉线"（~15s），而"连上→入房"之间只有心跳一条报文 ⇒ 房主若对**未入房**会话不回 Pong，它会被自己的库判死并重连。**修法：`OnPingReq` 一律回 Pong**（不管有没有入房）。
- **「有人退房后，他的幽灵位姿还在下发 / 名单干净但位姿表还在涨」**：退房路径必须**立刻关会话 + 清四张表** —— `_players` / `_profiles` / `_poses` / `_lastSeen`（`_poses` 不清 ⇒ 该 sid 的位姿会继续跟着每批快照下发）。
- **「同一个人被提示离开两遍」**：主动踢人（`EvictIdleMembers`）会先 `HandleMemberLeft` 再 `CloseSession`，库回调 `OnSessionDisconnected` 又进来一次 ⇒ 用 `HashSet<uint> _selfClosed` + `CloseSessionOf(sid)`（先登记再关）去重；`StopHost` 要清这个集合。
- **「成员在转场/加载时被房主踢掉，回来又进不了房」**：`NetHostSvc.memberIdleTimeout` 默认 **8s**，比库自己的 15s 更严 ⇒ 客户端一卡帧（加载战斗）就超时被踢；而战斗中 `_joinClosed` / `_rosterFrozen` 会**拒绝重连**。三条路：抬高/关闭 idle 超时、转场期间不踢、支持重连复用身份。
- **「跨端 `TaskIndex` 指向的任务不一样」**：任务表 `TaskCfgs` 的行下标来自 `Resources.LoadAll` 的枚举顺序（编辑器 ↔ 打包版可不同）+ 每区域消费随机次数随地图不同 ⇒ **下标不是跨端标识**。修法两件：生成端**按 mapId 排序**（`TaskManager.OrderedMaps/MapIndex`）、本局配置**随 `TaskConfirmNtf` 下发内容**（`Cfg`，含"以内容为准、不查本地表"的 `SetTask(..., remoteCfg)` 分支），指纹因此从"硬校验"降级为"诊断告警"。
- **「后进房的人拿不到本局任务」**：`NetHostSvc._lastTaskConfirm` 会缓存整条广播，`OnJoinRoomReq` 里给新人**补发**（顺序在 `JoinRoomRsp` 之后，KCP 可靠有序）⇒ 所以那条消息必须**自足**（带种子 + 配置内容），而不是"让成员自己查表"。
- **`MarkNtf` 这类纯表现可以不可靠/乱序**（丢了无所谓）；**配置、名单、伤害、死亡必须可靠**（KCP 只有可靠有序通道 ⇒ 快照类消息要在应用层"丢旧 tick + 只发最新值"）。
