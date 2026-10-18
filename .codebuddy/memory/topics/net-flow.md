# 主题 · 联机 · 房间 / 开房流程 / 名单队伍 / 会话生命周期

> 由 `net.md` 拆出（2026-10-11）。预算 ≤ 8000 字符。同伴：`net.md`（传输/库/诊断）、`net-battle.md`（战斗同步）。
> 跨机「搜不到 / 连不上」排查 + 版本门槛已拆到 `net-connect.md`（2026-10-18）。
> ⚠ 原文（含每条实测）已下沉：`archive/topics-snapshot-20261011/net.md`。

## 1. 挂载与入口
- `NetSvc`/`NetHostSvc`/`NetRoomFlow`/`NetTransformFlow`（+ `TeamNetBridge`/`NetFriendBridge`）挂在 `GameRoot.prefab` 的**子物体 `NetRoot`** 下（2026-10-07 收拢：根上组件从 13 降到 7）；`GameRoot.unity` 有该 prefab 实例 ⇒ 常驻。
  ⚠⚠ **只能挂"根 或 一级子物"**：`GameRootBase.Awake/OnDestroy` 收集 `I_GlobaManager` **只扫两级、不递归**，挂更深会被**静默漏掉** Init/UnInit（2026-10-11 用 `NetRoot` 命中行复核过 skill 的说法，`net.md` 旧记载"挂在根 GameObject"**已过期**）。
- 只有 `NetSvc`/`NetHostSvc` 里 `new KCPNet<...>`（传输层），其它全是薄壳。

## 2. 开房 / 收人 / 阶段模型
- **公开房**：地图界面 `pubilc` 按钮**只**记 `SelectPlayMode=1` 并走与单人相同的 `ExpandCfg()`（调难度/选任务），不建服；服务器在 **`SelectMapWnd.ConfirmTask`（按"准备"）里 `CreateRoom(true)` 静默创建**（不弹提示；失败仍弹）⇒ 随后 `SetTask` + `NetHostSvc.ConfirmTask` 广播本局配置。
  ⇒「我是不是房主」的唯一判据 = **`NetHostSvc.RoomInfo != null`**（`StartHost` 设 / `StopHost` 清）。
- **收人窗口 = Ready**（选完任务 → 所有人就位）；**一进 Armament 就 `CloseJoin()` 关闸**。
- **阶段模型**（用户纠正过两次，以此为准）：`ConfirmTask`（原 `StartTask`，**只是"选完任务"**）→ `GameState = Ready`（**还在舰桥**）→ 全员就位 → **`Armament`（`1<<7 = 128`，仍在舰桥，各自选战备）** → 全员准备 → **`Transition` ⇒ 各端各自加载战斗场景**。
  触发点是大厅 `GameStateController`（guid `566bc32b…`，按 `GameStateEnum` 派发 UnityEvent）的 **`state: 8 = Transition` → `TransSceneController.StartLoad()`**（`Utnapishitim.unity`），**每个客户端各自触发**（`ArmamentWnd` 全员就绪 → 播 Exit → 动画事件 `FinishReady()` → `GameState = Transition`）。
- `CmdId.Transition = 4012` + `TransitionNtf{MatchId}`（房主权威"进战斗"）：房主侧 `TeamNetBridge.HandleGameStateChange` 监听 `GlobalEventSub.OnGameStateChange`，`entry == Transition` 且自己是房主且本局没发过（`_transitionSent`，回 `Ready` 复位）⇒ `NetHostSvc.NotifyTransition()`（广播 + **冻结名单** + `_started = true`）。
  成员侧 `HandleTransition` **只把 `GameRoot.GameState` 推到 `Transition`**（由大厅既有链路完成加载）；⚠ 成员**绝不能**自己再 `AsyncLoadScene`（双加载）。`GameRoot.GameState` setter 自带"同值不发事件" ⇒ 两条路径天然幂等。
- ⚠⚠ **"关闸"与"冻名单"必须分开**：`_joinClosed` 独立于 `_rosterFrozen` —— `ArmamentWnd` 的"全员就绪"判定要靠名单里的准备状态，在这里冻名单会把该阶段**冻死**。闸门在下一局 `ConfirmTask` 重开，`StartHost`/`StopHost` 都复位。
  冻结时机移到 **`NotifyTransition`**（Ready 期间仍可进人 ⇒ 公开房"后加入的人"才通）；`OnJoinRoomReq` 冻结时 `SendJoinFail("本局已开始")`，未冻结则**补发** `_lastTaskConfirm`（新人拿得到地图/任务/种子才进得了 Ready）。
- **开局加载闸门**：`LoadCompleteNtf = 4041`（成员→房主：本机战斗场景加载完成）+ `BattleStartSync = 4042`（房主→全体：全体加载完 ⇒ 一起开打，各端凭它把 `IsStartBattle` 打开）。⚠ 单独一条而不是复用 `Transition`（各端加载快慢差很多）。

## 3. 本局配置下发（"以内容为准"）
- `TaskConfirm = 4006` / `TaskConfirmNtf`（含 `TaskCfgDto Cfg` + `TaskFingerprint` + `MatchId` + `Seed` + `SceneName`）。
- `SetTask(..., TaskCfgDto remoteCfg = null)`：**有内容就以内容为准、不查本地表** ⇒ 本局配置就此冻结（后进者即使本地表没这个任务也能照打）。
- 指纹 `TaskFingerprint` 从**硬校验降级为诊断告警**（有内容时），否则后进者/跨窗口玩家被永久拒之门外；旧版房主（无内容）仍保留硬校验。
- ⚠ 命名纪律（踩过）：`StartGameNtf`/`NetHostSvc.StartGame`/`SelectMapWnd.StartTask` 已统一改名 **`TaskConfirmNtf`/`CmdId.TaskConfirm(4006)`/`ConfirmTask`**。

## 4. 玩家名单 / 队伍（`TeamManager` / `TeamNetBridge`）
- `RoomManager` 已改名 **`TeamManager`**（`09Manager/Global/TeamManager.cs`；`04Data/RoomState.cs` → `TeamState.cs`，GUID 保住）。新增 **`09Manager/Global/TeamNetBridge.cs`**（`I_GlobaManager`，挂 GameRoot）= 唯一映射点 + 开局复现。
- **职责口径（写死）**：`NetHostSvc/NetRoomFlow` 持"**谁在场**"（连接/会话/房间参数/资料表 `_profiles`）；`TeamManager` 持"**这一局谁上场 + 角色装备**"；只有 `BridgeSys.Send*` 发（联机走 `NetRoomFlow`、单机本地回环），只有 `Receive*` via `TeamNetBridge` 改 `TeamManager`。⛔ **不要合并**（09 与 02 跨层；`PlayerData` 与 `PlayerInfo` 还差一层映射）。
- **两套下标别混**：网络 `PlayerIndex` = 房主视角（房主固定 0）；本地 `TeamManager.players` = 自己视角（**自己永远 0**，`ArmamentWnd` 靠这条）。换算 `TeamNetBridge.IdOfSid`（房主 0⇒-1、成员 sid⇒-(sid+1)）。
- ⚠ **判"自己"必须与 `IsMySid` 同口径**（房主看 `sid==0`，成员看 `sid==SelfSid`）⇒ `LocalIndexOf` 命中即返回 `team.SelfIndex`。否则"房主转发回来的、**关于我自己的**同步"（战备 4007/4008、强化 4009/4010、以及将来任何东西）会在成员端被**静默丢弃**（症状：成员自己选的战备自己看不见、房主却看得见）。
- **准备状态三坑**（"成员点就绪：房主看得见、自己看不见"）：
  ① `HandlePlayerList` 的"通知 UI"循环**不能把 `IsSelfEntry` 全跳过**（房主自己跳过是对的，**成员自己必须放行**）；
  ② 收资料（`OnPlayerProfileNtf`）**绝不能照抄 `IsReady`**（`ToProfile` 不设该位 ⇒ 每次上报资料都把准备位重置成 false ⇒ 房主那边也跟着变未就绪、卡住"全员就绪"）⇒ `IsReady` **只能**由 `ReadyState` 改，收资料时从 `_players[sid].IsReady` 继承；
  ③ `OnReadyState` 里 `_profiles[sid].IsReady` 也要同步（`BuildProfileArray` 优先用它）。
- ⚠ `PlayerProfile` 必须**入房成功即上报**（`TeamNetBridge.HandleJoinResult` 订阅 `OnJoinResult` → `SendSelfProfile`）+ 房主在 `GameState → Ready` 也推一次自己的资料；否则盟友实体建出来却**没有身体**（`NetFriendBridge.AttachRoleModel` 要求 `RoleName` 非空，身体是运行时挂 `StudentModle/<RoleName>`）。`PlayerProfile`（02_Net）是 `PlayerData` 的平行 DTO，加字段两边都要加。
- ⚠ `BridgeSys` **只挂大厅 `Utnapishitim.unity`**（不在 GameRoot，且与 `ArmamentWnd` 同生共死）⇒ 战场里 `BridgeSys.Instance` 为 null，所有调用点判空。战备/强化/资料的**数据落点已从 `ArmamentWnd.Receive*` 上移到常驻 `TeamNetBridge`**（先写 `TeamManager.players[i]`，再 `BridgeSys.Instance?.Receive*` 叫醒界面），否则"不在舰桥时收到的同步"会被静默丢弃。
- 开房入口两处：地图界面 `pubilc` 按钮 + 设置界面 `stateWnd/PlayerStateSelf/FriendRoot` 的 3 个**空位**（`SettingWnd.CreateRoomFromEmptySlot`，空位节点运行时补 Button）。
- `PasswordWnd`（`WndType.Password` + `WndHub.Password`）已用于**房间密码**与**首次起名**。

## 5. 会话生命周期（人走了怎么办）
- 房主侧"**人走了**"唯一入口 = **`NetHostSvc.RemoveMemberAndNotify(sid)`**（三个调用点：断线/踢人、主动退房 `OnLeaveRoomNtf=4003`、空闲踢 `EvictIdleMembers`）。它做齐：清 `_players/_profiles/_poses` → `BroadcastPlayerList()`（战斗期冻结 ⇒ 空操作）→ `SendToAll(PlayerLeftNtf=4029)` → **`MessageCenter.Dispatch(msg)` 房主本地自派发**。
  ⚠ `SendToAll` 按定义**不含房主本地** ⇒ 漏这句房主就永远收不到"人走了"（与 `BroadcastPlayerList` 的自派发同款）。UI 不用改：`PlayerWnd` 每帧按 `ActorsManager.Players` 重排，实例一销毁下一帧行自动隐藏。
- ⚠⚠ 房主侧还要处理**库回调重复**：`EvictIdleMembers` 踢人后 `CloseSession()` 会让库再回调一次 `OnSessionDisconnected` ⇒ 同一个 sid 第二次进 `HandleMemberLeft`（日志没去重就"同一个人提示两遍"）。修法：`_selfClosed: HashSet<uint>` 登记"本端主动关的 sid" + `CloseSessionOf(sid)`（先登记再关）。
- ⚠ 空名单门：战斗中**不能重排名单**（`armamentRoot.GetChild(i)` ↔ `players[i]`、`task.BattleData` 按下标建、`AddBattleDataItem(playerIndex,…)` 按下标写 ⇒ 少一个人就统计错位）⇒ 成员 `HandlePlayerList` 开局后只按 id 更新字段、房主 `BroadcastPlayerList` 入口冻结（`_rosterFrozen`）。
- `NetRoomFlow.Leave()`（成员主动退房）：先 `SendToHost(LeaveRoomNtf)` 再 `Disconnect()`（**同步**把 `IsConnected` 置 false）⇒ **必须同时 `_wasInRoom = false`**，否则下一帧 `DetectHostLost` 把"主动退房"误报成"房主掉了"。⚠ 对房主无意义（房主退联机 = `StopHost`，目前无调用点）。

## 6. 房主掉线 ⇒ 本端回单机（无 host migration）
- **没有** host migration（无 `HostMigrat`/`BecomeHost`）；也**不重建单人房**。`NetRoomFlow.DetectHostLost` 只清标记（`CurrentRoom=null`/`SelfSid=0`/`SelfHostIndex=-1`/`_battleStarted=false`）+ 抛 `OnHostLost` ⇒ 各端**各自**退出房间、客机之间无直连（互相看不到对方，模型停在最后位姿）。要续联机只能回大厅重开/重加。
- **所有"等房主"的开关都要在 `OnHostLost` 复位**（漏一个就是静默的半瘫）：

  | 开关 | 复位点 | 不复位的后果 |
  |---|---|---|
  | `EnemyController.RemoteDrivenMovement` | `EnemyNetBridge.OnHostLost` | 敌人永远等下发 + 每帧刷"本端作废" |
  | `MissionBase.RemoteDriven` | `NetMissionBridge.OnHostLost` | 进度/完成/失败全被门住 ⇒ **这一局打不完** |
  | `NPCWalk.RemoteDriven` | `SceneUnitMoveSink.OnHostLost` | NPC 永远站着不动（不报错、不刷日志，最易漏） |
  | `KeyScreen.RemoteDriven` | `NetKeyScreenBridge.OnHostLost` | 终端永远不推进（2026-10-11 加） |

- `NetFriendBridge.HandleHostLost` = **`ClearFriends()`**（盟友全清）+ **丢名单缓存**（`_lastInfos/_lastProfiles = null`；否则回大厅/换场景时 `HandleSceneChanged` 会拿旧名单把这队盟友**重新建出来**）+ `_loadouts.Clear()`；`HandleSceneChanged` 另加门 `!flow.InRoom` 就丢缓存不重建。
  ⚠ 新增同类开关照单补（清单写在 `NetRoomFlow.OnHostLost` 的注释里）。

## 7. `RoomMeta`（房间字段唯一薄适配层）
`FPSGame.Net.RoomMeta` 只留 `Difficulty`（`out bool fromBroadcast` = `room.Difficulty >= 0`）/`TaskEnum`/`InGame`/`Source`/`RoomName`/`MapName`，职责 = 缺省值归一 + 空引用保护。房主侧 `NetHostSvc.StartHost` 直接写字面量（`Source=0`），`GetBroadcastInfo` 每轮刷 `InGame = _started`，`ConfirmTask(..., int taskMain)`（`taskType` 参数已删）就地刷 `RoomInfo.TaskMain`。
