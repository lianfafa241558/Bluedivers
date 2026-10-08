# 联机 · NetTmp 拆分为「传输层 02_Net」+「游戏协议层 07_NetGame」

> 日期：2026-10-08　状态：执行中
> 目标：让 `02_Net` **只负责传输**（会话 / 线格式 / 分发 / 线程队列），零游戏语义；全部游戏协议与编排归 `07_NetGame`。

## 1. 边界判据（一句话）

> 传输层只认 `(int cmdId, byte[] / 泛型 payload)` 与 `sid`。
> 凡出现「玩家 / 房间 / 准备 / 战局 / 位姿 / 战备 / 任务」的语义，一律属 `07_NetGame`。

`sid` 只是"会话号"，不是"玩家"。

## 2. 实测依赖（拆分前）

| 程序集 | refs | 备注 |
|---|---|---|
| `02_Net` | `[]` | 无 asmdef 依赖 |
| `05_UnitCore` / `06_Gameplay` | 不含 `02_Net` | 玩法层看不到 net |
| `09_Managers` | 含 `02_Net`（按 name） | 7 个文件用 `FPSGame.Net` |
| `10_UI` | 含 `02_Net`（按 name） | 3 个文件用 `FPSGame.Net` |

- 全仓 `CmdId.` 在 NetTmp 之外 **0 命中** ⇒ 拆 `CmdId` 不影响外部。
- 全仓 `MessageCenter/NetMsgCodec/ClientSession/HostSession` 在 NetTmp 之外 **0 命中** ⇒ 09/10 无需保留 `02_Net` 引用。
- 唯一跨层耦合：`ClientSession → NetSvc.Instance.AddMsgQue`、`HostSession → NetHostSvc.Instance.AddMsgQue`
  ⇒ 若 Service 进 07、Session 留 02 就会成环 ⇒ 用 `NetInbox`（02 自有队列）打断。

## 3. 目标结构

### `Assets/Scripts/NetTmp/`（asmdef `02_Net`，refs 仍为 `[]`）— 纯传输内核

| 文件 | 说明 |
|---|---|
| `Client/ClientSession.cs` | KCP 会话（客户端侧），收包入 `NetInbox` |
| `Client/HostSession.cs` | KCP 会话（房主侧），收包带 sid 入 `NetInbox` |
| `Client/MessageCenter.cs` | `cmdId → handler` 分发 + `Pack` |
| `Client/NetInbox.cs` | **新增**：线程安全入站队列（网络线程写 / 主线程取） |
| `Services/NetMsgCodec.cs` | AOT 安全线格式编解码 |
| `Services/NetCmdId.cs` | **新增**：传输控制命令号（`PingReq=1001 / PingRsp=1002`） |
| `Services/Msg/PingMsg.cs` | 心跳 DTO |

### `Assets/Scripts/07NetGame/`（asmdef `07_NetGame`，refs `["02_Net"]`）— 游戏协议 + 编排

| 文件 | 说明 |
|---|---|
| `NetSvc.cs` | 客户端连接管理 + 心跳 + 泵 `NetInbox`（原 `NetTmp/Client/`） |
| `NetHostSvc.cs` | 房主 socket + 名册/准备/关闸/冻结 + 转发（原 `NetTmp/Client/`） |
| `NetRoomFlow.cs` | 房间/局内编排（19 条命令号） |
| `NetTransformFlow.cs` | 位姿聚合 |
| `RoomMeta.cs` | 房间元数据适配 |
| `CmdId.cs` | 游戏命令号（去掉 ping；删 login/chat） |
| `Msg/RoomMsg.cs` | 房间/舰桥/战局 DTO |
| `Msg/BattleMsg.cs` | 位姿/命中/血盾/开波/战备 DTO |

### 删除

- `NetTmp/Services/Msg/LoginMsg.cs`、`ChatMsg.cs` + `CmdId` 的 2001/2002/3001/3002 段（无消费方）。

## 4. asmdef 改动

| asmdef | 现在 | 改成 |
|---|---|---|
| `02_Net.asmdef` | `references: []` | 不动 |
| `07_NetGame.asmdef`（新建） | — | `references: ["02_Net"]` |
| `09_Managers.asmdef` | `"02_Net"` | `"07_NetGame"` |
| `10_UI.asmdef` | `"02_Net"` | `"07_NetGame"` |

无环：`07 → 02`；`09 → 07`；`10 → {09, 07}`；`02/05/06` 不指向 07 ⇒ DAG。

## 5. 执行步骤

1. 新建 `Assets/Scripts/07NetGame/`（含 `Msg/` 子目录）+ `07_NetGame.asmdef`。
2. 新增 `NetInbox.cs`、`NetCmdId.cs`；改 `ClientSession` / `HostSession` 走 `NetInbox`。
3. `NetSvc` / `NetHostSvc`：删自带队列与 `AddMsgQue`，`Update` 改抽 `NetInbox`；心跳改 `NetCmdId`。
4. `CmdId` 去 ping、删 login/chat。
5. 用 `AssetDatabase.MoveAsset` 搬 8 个游戏文件（保 GUID）。
6. 改 `09_Managers` / `10_UI` 的 asmdef 引用。
7. `refresh_unity(force, all, request)` → Console 0 error → `offline_compile.py 02_Net 07_NetGame 09_Managers 10_UI` → 反射核对。

## 6. 约束与代价

1. **桥不搬**：`NetFriendBridge`/`EnemyNetBridge`/`TeamNetBridge`/`SceneUnitMoveSink` 留在 `09_Managers`
   —— `NetFriendBridge` 用 `ResSvc`（09 层）⇒ 搬进 07 会 `09 ↔ 07` 成环。
2. **`LanRoomInfo` 由 KCPNet.dll 固定**，字段含 `Difficulty/TaskMain/InGame`（游戏语义）⇒ 只能把"写这些字段"的代码上移，字段本身留在库。传输层无法 100% 游戏无关，这是外部依赖的硬边界。
3. 移动 `.cs` **必须** `AssetDatabase.MoveAsset`（文件系统 move 会让 Unity 重分配 GUID ⇒ Missing Script）。
4. **二期可选**：把 socket 归属（`ClientNet`/`HostNet` 纯类）也下沉到 02，使 `NetSvc`/`NetHostSvc` 只剩游戏编排。

---

## 7. 二期方案（把「连接/会话/存活」也下沉到 02）——✅ 2026-10-08 已执行（编译+反射通过；**运行期实测待做**）

目标：让 `02_Net` 真正负责传输**全套**（会话 + 线格式 + 分发 + 线程队列 + **连接管理**）；`07_NetGame` 只剩「游戏协议 + 房间语义 + 编排」。

### 7.1 关键设计决策

1. **不新增 MonoBehaviour ⇒ 零 prefab 改动。** `NetSvc` / `NetHostSvc` 是 `GameRoot.prefab` 上的组件 ⇒ 二期**保留两个类名与 GUID**（仍留在 07），只把内部实现委托给 02 的**纯类** `NetClient` / `NetServer`。这样不用碰 prefab、不会有 Missing Script。
2. **新类都是纯 C# 类**（非 MonoBehaviour）⇒ 由现成的 `NetSvc.Update()` / `NetHostSvc.Update()` 调 `Pump()`，不需要往 prefab 上加组件。

### 7.2 02 新增两个纯类

| 类 | 职责（从 07 搬下来的） |
|---|---|
| `02_Net/Transport/NetClient.cs` | 持 `KCPNet<ClientSession, NetMessage>`；`Connect(ip, port, cb)` / `Disconnect()` / `Send(NetMessage)` / `IsConnected`；**心跳 + RTT**（用 02 的 `NetCmdId` + `PingMsg`）；`Pump()` = 抽 `NetInbox.TryDequeueClient` → `MessageCenter.Dispatch` |
| `02_Net/Transport/NetServer.cs` | 持 `KCPNet<HostSession, NetMessage>`；`Start(port, out reason)` / `Stop()`；`SendToAll` / `SendTo(NetMessage, uint sid)` / `TryGetSession(sid)` / `CloseSession(sid)`；`CurrentSid`；**会话事件**（传输线程 → `_sessionEvents` → 主线程 Pump 派发）`event OnConnected/OnDisconnected`；**存活判定** `_lastSeen` + 空闲踢人 `event OnIdle(uint sid)`；**心跳应答**（`NetCmdId.PingReq` → 回 Pong，必须对任何会话都回）；`Pump()` = 处理会话事件 → 抽 host 队列（设 `CurrentSid` + 刷 `_lastSeen`）→ `Dispatch` → 踢空闲 |

> 注意分工：02 只判「这个 sid 没消息了」（`OnIdle`），**不碰名册**；名册清理/广播仍由 07 做。

### 7.3 07 侧瘦身

- `NetSvc`（MonoBehaviour，保名保 GUID）：持 `NetClient`；`Update()` → `_client.Pump()`；`SendMsg` → `_client.Send`；**只留游戏动词** `JoinRoom` / `LeaveRoom` / `SetReady`（`CmdId` 游戏号 + 游戏 DTO，`Pack` 后交给 `_client.Send`）。
- `NetHostSvc`（MonoBehaviour，保名保 GUID）：持 `NetServer`；`Update()` → `_server.Pump()`；**只留房间语义**：`RoomInfo` / `_broadcaster` / 名册 `_players`·`_profiles`·`_poses` / 准备 / `_joinClosed`·`_rosterFrozen` / `ConfirmTask` / `NotifyTransition` / `BroadcastPlayerList` / `BuildProfileArray` / `RelayToAll` / `BroadcastPoses`。原 `OnMemberJoined` / `OnMemberLeft` 改成订阅 `_server.OnConnected/OnDisconnected`；`EvictIdleMembers` 改成订阅 `_server.OnIdle`；`CloseSessionOf` → `_server.CloseSession`。

### 7.4 明确「不动」

- `NetRoomFlow` / `NetTransformFlow` / `RoomMeta` / `CmdId` / `RoomMsg` / `BattleMsg` 全部不动（本就是游戏协议）。
- `09_Managers` 的 4 个桥全部不动。

### 7.5 步骤（沿用一期手法）

1. 新增 `02_Net/Transport/NetClient.cs`、`NetServer.cs`（纯类）——一次性编译通过。
2. `NetSvc` 改造（小，~80 行）：内部改持 `NetClient`，删自身 KCP/心跳/队列代码。
3. `NetHostSvc` 改造（**最重，~300 行搬迁 + ~120 行改写**）：内部改持 `NetServer`，删会话/队列/心跳/踢人实现，改订阅事件。
4. `offline_compile.py 02_Net 07_NetGame 09_Managers 10_UI` → 0 error。
5. `refresh_unity` + Console 0 error + 反射核对（`NetClient`/`NetServer` ∈ 02；`NetSvc`/`NetHostSvc` ∉ 02）。
6. **运行期冒烟（必做）**：开房 → 入房 → 名册/准备 → 进战斗 → 强杀客户端看踢人 → 心跳不误杀。判据沿用 2026-10-07 那批（强退清人、未入房连接不刷屏、名单不重排）。

### 7.6 风险与取舍

- **风险集中在第 3 步**：`NetHostSvc` 是行为最密的文件（名册冻结 / 关闸 / 踢人 / 心跳 / 位姿聚合），建议**单开一次会话**做，改完必须实测。
- **收益纯属架构纯度**：二期不加任何功能；一期其实已满足「02 零游戏语义」。
- **更小的替代方案**：若不需要 socket 进 02，只做「07 内部再分家」——把会话/踢人拆成 `07_NetGame/HostSessionMgr.cs`。文件变小、风险更低，但"连接在 07"这一点不变。
