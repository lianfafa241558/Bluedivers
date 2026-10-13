# 主题 · 联机（KCPNet）· 总览 / 传输 / 库 / 诊断

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。
> 同伴：`net-flow.md`（房间与开局流程 / 名单队伍 / 会话生命周期 / 房主掉线）、`net-battle.md`（战斗同步 / 种子确定性 / 消息号表）。
> ⚠ **完整历史（57k，含每条实测数据与 IL 反证）已下沉**：`archive/topics-snapshot-20261011/net.md` —— 翻旧结论去那里搜。

## 1. 组成 / 形态 / 端口
- 库：`Assets/Plugins/KCPNet/{KCPNet,Kcp}.dll`（**无源码**，改库只能找作者）+ 自研两层适配：
  - `02_Net`（**纯传输**，9 文件，`references:[]`）：`NetClient`/`NetServer`/`ClientSession`/`HostSession`/`MessageCenter`/`NetMsgCodec`/`NetInbox`/`NetCmdId`/`Msg/PingMsg`
  - `07_NetGame`（**游戏协议**，refs=02）：`NetSvc`/`NetHostSvc`/`NetRoomFlow`/`NetTransformFlow`/`RoomMeta`/`CmdId`/`Msg/{RoomMsg,BattleMsg,SyncMsg}`
  - 桥（`09Manager/**/Net*Bridge`）与玩法订阅方都在 **09**（09 同时看得见 05/06/07）
- 形态：**房主权威 + 局域网 P2P**（UDP 广播发现 → KCP 回连）。端口 **17666**(KCP) / **29800**(广播) / **29801**(本机自己开房时自广播降级)。
- ⚠ **`02_Net` refs=[]** ⇒ 看不见 05/06/07/09；DTO 字段**只能用 int/uint/float/bool/string/数组**（游戏枚举需先转 int）。`10_UI` 要用 `NetSvc` 得给它加 `02_Net` 引用；只用 `KCPNet.*` 则不必。
- 依赖：MessagePack 3.1.8（`scripts/install_deps.py --check` 自检）。
- 插件加载失败看 `Editor.log` 的 `Unable to resolve reference` / `will not be loaded due to errors`。

## 2. 消息编解码（AOT 关键）
- ⚠ **不能**用 `MessagePackSerializer` 默认 resolver（`DynamicObjectResolver` 靠 `Reflection.Emit`）⇒ **打包版一发消息就 `PlatformNotSupportedException`**（编辑器 Mono 看不出来）。
- 现走 `02_Net/.../NetMsgCodec.cs`：只手写 `MessagePackWriter/Reader` 读写**标准 MessagePack 字节**，成员枚举/字段读写自己反射（array 格式、按 `[Key]` 升序；对端字段少 ⇒ 末尾留默认值、多 ⇒ `reader.Skip()`）⇒ **新增消息零额外工作**。
- 配套 `Assets/link.xml`（`preserve="all"` 保全 `02_Net` + `MessagePack.Annotations`）—— 反射对裁剪器不可见。
- 库自带的 `MessagePack.GeneratedMessagePackResolver` 是 **internal**（外部引用 `CS0122`）；库侧自己传 options ⇒ 库本身 AOT 安全，只有我们的 DTO 会踩默认 resolver。

## 3. 库（KCPNet.dll）要点
- `LanRoomInfo` 现 **16 字段**：`RoomName/HostIp/HostPort/PlayerCount/MaxPlayers/MapName/PasswordProtected/Version/PlayerNames[]` + `int Difficulty(-1)/int TaskMain(-1)/bool InGame/int Source` + **2026-10-13 新增** `string HostName`（显示用真房主名）/`string RoomId`（开房生成的 GUID = 房间身份）/`int TaskIndex`（地图内任务下标）；JSON 键 `difficulty/taskMain/inGame/source/hostName/roomId/taskIndex`；`ToJson()` 实例方法、`FromJson(json)` 静态方法，**忽略未知 key、缺 key 给默认值** ⇒ 新旧房主可混联。`Difficulty < 0` 表示"广播里没有"。
- 常量：`NetConfig.HostGamePort=17666 / LanBroadcastPort=29800 / LanSelfBroadcastPort=29801 / HeartbeatIntervalMs=3000 / DefaultTimeoutMs=15000`；`LanDiscoveryConfig.ListenPort=29800 / AnnounceIntervalMs=2000 / DiscoveryTimeoutMs=1500 / RoomExpireMs=6000`。⚠ `NetConfig` 在**库内部**（工程里没有 .cs）⇒ 常量改不了，只能逐会话设属性。
- ⚠ 本机同时开房时 29800 被 `LanBroadcaster` 占用 ⇒ `LanDiscoverer` 降级 29801，此时收不到 ANNOUNCE，只能靠周期 `Scan()` 维持列表。
- ⚠ `PlayerNames[0]` 是**合成名** `Host_<HHmmssfff>`（成员端用它排除自己开的房），不是真实玩家名。
- ⚠ **换 DLL 后 Unity 里的旧程序集不会自动换**：`ImportAsset(ForceUpdate)` 单独调用无效，必须再 `refresh_unity(force,all,request)` 触发域重载；判据 = `unity_reflect` 数出的字段数变了。
- ⚠ "服务器/局域网"**没有数据来源**：`NetSvc.SRV_IP="127.0.0.1"` 是硬编码示例、`ConnectDefaultServer()` 无调用点 ⇒ 房间列表只能按 `HostIp` 猜（`ServerListPanel.RoomNetType`/`IsLanAddress`）。

### 3.0 发现层 `HostIp` 口径（2026-10-13 源码核对；报告 `.codebuddy/plans/KCPNet源码分析报告.md`）
- **库 0 个 `HostIp` 赋值点**（`LanBroadcaster.cs:95,147` 只 `info.ToJson()` 透传；`LanRoomInfo.cs:24` 默认 `""`）；**不挑网卡、不排 Loopback**（全库无 `NetworkInterface/Dns./AddressFamily`）⇒ 广播出口网卡 = OS 路由表（255.255.255.255 受限广播）。
- ⭐ **成员端每次都覆盖**：`LanDiscoverer` 收 ANNOUNCE 后 `info.HostIp = result.RemoteEndPoint.Address`（`LanDiscoverer.cs:154-156`），去重键也用 `sourceIp:HostPort`（`:163`）⇒ **房主侧给 `RoomInfo.HostIp` 赋值对成员无效**（2s 一次被冲掉）；也别指望"手工改 `GetRooms()` 里的值"（每 2s 换新对象）。
- 跨机时该 source IP **天然可达**（能收到这包就证明同域）；"HostIp=127.0.0.1"**只同机**成立、而同机也能连 ⇒ **排障别看"HostIp 值对不对"**，直接看 `NetRoomFlow.cs:398` 打出的 `<ip>:<port>`。
- ⚠ 残留唯一风险：多网卡时周期广播与 `RespondLoop` 单播应答共用 `Bind(IPAddress.Any)` 的同一 socket（`LanBroadcaster.cs:68,150`）⇒ 两者 source 可能是不同网卡 ⇒ 库按 `sourceIp:HostPort` 去重会出现**同一房间两行**（一行连不通）。我方 `ServerListPanel.QueryRooms():636-645` 无去重；`RoomKey():634` 仅供 `BuildSignature` ⇒ 要去重得在 `QueryRooms` 按 `PlayerNames[0] + HostPort` 分组。
- 报告 §0 提醒：若 DLL 早于 2026-10-10 的服务端会话重构，§3.2 幽灵会话结论不适用（自检判据见 §3.2）。

### 3.0b 库 2026-10-13 更新（DLL 已换；`LanRoomInfo` 16 字段 / 方法含 `IsFromExpectedPeer`・`ResolveAddress`・`EnableEncryption`）
- **⑤ 覆盖口径反转**：`LanDiscoverer` 现在**房主显式填了可解析 IP 就信任**，只有在 `HostIp` 为空/不可解析时才退回"收包 source"。⇒ 我方新增可选字段 `NetHostSvc.advertiseIp`（`[InspectorName("广播地址(可选)")]`，空 = 旧行为；非 IP 字面量 ⇒ 打 `LogWarning` 并当空串，**填错会让所有成员连不上**）。多网卡 / 虚拟局域网内测就靠它。
- **① 域名**（`StartAsClient` 走 `ResolveAddress`）/ **② 回包源校验放宽**（同 IP 不同端口会被接受并把 `remotePoint` 学习成实际来源 —— 端口映射 / 中转的前提）/ **③ 握手按 `NetConfig.HandshakeRetryIntervalMs=300` 重发** / **④ `EnableEncryption`（默认 false，旧行为不变）** —— 全在库内，我方零改动。
- **⑥ 去重键**：`LanDiscoverer` 优先用 `RoomId`；我方同步改为 `ServerListPanel.RoomIdentity` 首选 `RoomMeta.RoomId`、`NetRoomFlow.IsHostOfRoom` 首选 `host.RoomInfo.RoomId == room.RoomId`（老房主无该字段 ⇒ 自动退回旧判据）。
- ⚠ `TaskIndex` **目前没有消费点**：列表的任务三件套（图标/颜色/名）靠 `TaskMain` 枚举已精确，`TaskIndex` 只在"要显示具体任务名 / 双端校验同一份 `TaskCfg`"时才有用 ⇒ 已进广播 + 留了 `RoomMeta.TaskIndex` 读取口，别为它硬改 UI。

### 3.1 心跳 / 超时（两条独立时钟，别混）
- 库：`KCPSession` 基类每 3s 双向发**心跳裸字节**（带魔数，`IsHeartbeat` 全等判定）⇒ **永远到不了 `OnReciveMsg`/`MessageCenter`**；超时看 `_lastRecvTimeUtcTicks`（15s，任意 UDP 包都刷）⇒ 库自己 `CloseSession()`。
- 应用层：`_lastSeen`（`NetServer`）**只由业务消息出队刷新** ⇒ `EvictIdleMembers`（8s）判"谁是玩家"。
- ⇒ **两条时钟作用域不同 ⇒ 库心跳替代不了应用层 `PingReq/Rsp`**（1001/1002，`NetSvc` 每 2s，也是 `RttMs` 唯一来源）；库也没暴露"某会话最后活动时间"。
- ⚠⚠ **心跳必须对"任何会话"都回（含未入房的）**：库按"收不到数据"判死，而成员在"连上→入房成功"之间只有心跳一条报文 ⇒ 房主若"没入房就不回"，那条连接会被**它自己的库**判死 → 客户端重连 → 再判死 ⇒ 房主控制台每 ~20s 一串 `成员断开/（未入房）`，而真在房里的成员一直好着。
- 配套：未入房会话的断开日志**每个 sid 只提示一次**（`_ghostLogged`）；`EvictIdleMembers` 只遍历 `_players`（对的）。

### 3.2 幽灵会话（库侧缺陷，我们无责）
- 现象：房主侧同一对端反复"新建会话 + 关旧会话"（实测 ~5 次/秒），sid 在固定几个值间循环、`ep:` 键每秒重建。
- IL 实证：`BuildIdentityKey(clientId, ep)` ⇒ 有 clientId 用 `cid:`（**玩家那条稳**）、否则 `ep:`；`ParseClientId` 解析不出返回 null ⇒ 落回 `ep:` ⇒ **不带 clientId 的握手每来一次就新建会话（不幂等复用）**，每次关都触发 `OnDisConnected` 刷屏。
- 诉求（可直接转发作者）：① 会话表按 identityKey/endpoint **幂等复用**；② 重复/重传的 `REQUEST_CONNECT` 不建新会话；③ 未完成握手的会话及时回收。
- ✅ **2026-10-13 源码核对：三条诉求在 10-10 版已全部实现** —— 未知 sid 数据包**直接丢弃不建会话**、`HandleConnectRequest` 只复用不替换（`KCPNet.cs:323-339,162-196`）；握手期 8s 短超时回收（`KCPNet.cs:207-231`）。**自检判据**（日志文案）：`会话ID:{0}（身份 {1}）添加到会话字典` = 新版 / `会话ID:{0}添加到会话字典` = 旧版。⚠ 若仍复现幽灵会话 ⇒ 说明工程里的 DLL 还是旧版，需替换。
- ⚠ 仍未钉死：**谁在发"不带 clientId 的握手"**（对端老包 / 库自重试 / KCP 分包）—— 需 Play 中逐秒采样 `sidToKey` 看 `cid:` 是否也在换。
- ⚠ **持久 clientId 的两行**（库 2026-10-09 版新增 `KCPNet<T,K>.ClientId`）：写在 `02_Net/Transport/NetClient.cs`（`private readonly string _clientId = Guid.NewGuid().ToString("N")` + 在 `new KCPNet<...>` 之后、`StartAsClient` 之前设 `_client.ClientId`）。⚠ 这两行**被覆盖过 2 次**（拆分改名 + 外部编辑器缓冲区存回）⇒ 改完必须用 `02_Net.dll` **字节含 `_clientId`** 收尾核对，别只信编辑返回值。

## 4. 诊断日志（`00Tools/NetSyncLog.cs`）
- 三路 `Bullet / Ai / Sync` + `Warn`（异常级，便于筛）+ `AiLog(key,interval)` 限流版（每帧都走的调用点必须用它，否则 Console 被刷爆）。
- ⚠ 实测默认值：`Enabled = true`（**两端都开**）；静默用 `-nonetsynclog` / `NETSYNC_LOG` / `PlayerPrefs["NetSyncLog"]`。**正式发布前应改成"只认启动参数"**。
- 读日志：编辑器 = Console（MCP 可按 `filter_text` 筛）；打包端 = `Player.log`（启动会打 `Application.consoleLogPath`）。
- 常用 tag：`家具/操作·发|收`、`家具/键`、`终端/发|收`、`巡逻/发|收`、`容器/发|收`、`可破坏物·发|收`、`同步/家具/状态·发`。
- 通用手法：**"找不到对应实体"这类静默 return 必须留 `Warn` + 打整张键表**（否则现象就是"这个东西怎么都不同步"）。

## 5. 通用判据（传输 / 表现层）
- KCP 是**可靠有序**通道 ⇒ 增量消息不必防乱序；但**快照类要在应用层丢旧 tick**（迟到包会把状态拽回去）。
- ⚠ **传输层回调跑在 ThreadPool 线程**（`OnSessionConnected/Disconnected`、`OnReciveMsg`）⇒ 回调里**绝不能碰 Unity API / 游戏状态**（`Load can only be called from the main thread`）；必须"只入队 → 主线程 `Update` 里处理"（`NetInbox` + `ProcessSessionEvents`）。
- `MessageCenter` 是**按命令号单处理器**的静态字典 ⇒ 同一命令号只能有一个注册者（`NetRoomFlow` 与 demo 类不能同时挂）。
- **能发起连接的调用点只有两处**：`NetRoomFlow.Join`（UI）与 `LanRoomDemo`（演示，无场景引用）⇒ 排查"幽灵连接"先看这两处 + 另一台机/残留实例；⚠ 查"场景里挂没挂某脚本"要**按脚本 GUID 扫**（场景 YAML 里没有类名）。
- ⚠ 改方法名前先 grep 场景/prefab 的 `m_MethodName`（UnityEvent 按方法名调，`.cs` 里搜不到调用点 ≠ 没人调）。
