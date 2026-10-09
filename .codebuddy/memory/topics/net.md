# 主题 · 联机（KCPNet，试验中）

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。

- 组成：`Assets/Plugins/KCPNet/{KCPNet,Kcp}.dll` + 自研适配层 `Assets/Scripts/NetTmp`
- 形态：房主权威 + 局域网 P2P（UDP 广播 → KCP 回连）
- 端口：**17666 / 29800 / 29801**
- 依赖：MessagePack 3.1.8 及传递依赖已装齐（`scripts/install_deps.py`，`--check` 可自检）
- ✅ **MessagePack 的 AOT（IL2CPP）问题已解**（2026-10-06）：原 `MessageCenter.Pack/Dispatch` 用 `MessagePackSerializer`（默认 `StandardResolver → DynamicObjectResolver`，靠 `System.Reflection.Emit` 现造 formatter）⇒ **打包版一发消息就 `PlatformNotSupportedException`**（编辑器走 Mono 完全看不出来）。现改走 **`NetTmp/Services/NetMsgCodec.cs`**：只用 `MessagePackWriter/Reader` 写读标准 MessagePack 字节，成员枚举与字段读写自己用反射做（**array 格式**、按 `[Key]` 升序；对端字段少 ⇒ 末尾留默认值，多 ⇒ `reader.Skip()`），不碰 Emit、不需预生成 formatter ⇒ **新增消息零额外工作**。实测 21/21 个 `[MessagePackObject]` 类型往返忠实（MCP `execute_code`）。⚠ 反射对裁剪器不可见 ⇒ 新增 `Assets/link.xml`（`preserve="all"` 保全 `02_Net` + `MessagePack.Annotations`）。
- ⚠ `MessagePack.GeneratedMessagePackResolver`（KCPNet.dll 自带）是 **internal** ⇒ 外部引用会 `CS0122`；KCPNet 自己显式传 options（DLL 里能搜到 `MessagePackSerializerOptions`）⇒ **库侧本来就 AOT 安全，只有我们的 DTO 踩了默认 resolver**。
- ⚠ MessagePack 3.1.8 的 nupkg **不含任何 analyzer/源生成器**（只有 `lib/*/MessagePack.dll`，`install_deps.py --dry-run` 实测）⇒ "mpc/源生成器预生成 resolver" 在这台机器上要额外装 .NET SDK 工具链，不划算；手写 formatter（21 个类型 ≈700 行）也被 `NetMsgCodec` 泛化掉了。
- ⚠ 插件加载失败看 `Editor.log` 里的 `Unable to resolve reference` / `will not be loaded due to errors`
- 扩展方式与排查清单见 skill `kcpnet-online`
- ⚠ `NetTmp` **有 asmdef**（`02_Net`，`references: []`）——skill 里"没有 asmdef、落在 Assembly-CSharp"的说法已过期
- ⚠ `10_UI` 能直接用 `KCPNet.*`（`KCPNet.dll` 在 `Assets/Plugins/` 下被全局自动引用，`10_UI.csproj` 已列 `KCPNet`）⇒ **房间发现不需要给 10_UI 加 `02_Net` 引用**
- `LanRoomInfo`（KCPNet.dll）**只有 9 个字段**：`RoomName / HostIp / HostPort / PlayerCount / MaxPlayers / MapName / PasswordProtected / Version / PlayerNames[]` —— **没有难度/人数上限之外的状态**，`MapName`/`RoomName`/`PlayerNames` 是仅有的自由串；`NetHostSvc.StartHost(roomName, mapName, maxPlayers, password)` 也不传难度 ⇒ 想广播额外字段只能改库，或约定塞进 `MapName`
- `LanDiscoverer(port)`：默认 `LanDiscoveryConfig.ListenPort`(29800)；**本机同时开房时 29800 被 `LanBroadcaster` 占用**，要降级到 `NetConfig.LanSelfBroadcastPort`(29801)，此时收不到 ANNOUNCE，只能靠周期 `Scan()` 维持列表；`RoomExpireMs=6000`、`AnnounceIntervalMs=2000`、`DiscoveryTimeoutMs=1500`
- `NetHostSvc` 广播的 `PlayerNames[0]` 是**合成名** `Host_<HHmmssfff>`（成员端用来排除自己开的房），不是真实玩家名
- 常量实测：`KCPNet.NetConfig` = `HostGamePort 17666` / `LanBroadcastPort 29800` / `LanSelfBroadcastPort 29801`；`LanDiscoveryConfig` = `ListenPort/BroadcastPort 29800`、`AnnounceIntervalMs 2000`、`DiscoveryTimeoutMs 1500`、`RoomExpireMs 6000`、`BroadcastTtl 1`、`BroadcastAddress 255.255.255.255`（可用 `Setup(...)` 注入覆盖）。`LanRoomInfo.ToJson()` 是**实例**方法、`FromJson(json)` 是**静态**方法 ⇒ 加字段时两处都要改，且 `FromJson` 缺 key 要给默认值（兼容旧版本房主）。
- ~~**房间列表的"库缺字段"补丁**（难度拼 `MapName` 的 `#难度`、任务类型拼 `RoomName` 的 `#T=枚举|名字`，2026-10-07 那套）~~ **2026-10-08 库改完后已整体删除**：
  `RoomMeta` 只剩 `Difficulty/TaskEnum/InGame/Source/RoomName/MapName` 六个薄取值；`ComposeRoomName`/`ComposeMapName`/`IsLanAddress`/`RoomMeta.TaskType`
  与 `TaskManager.NowTaskType`/`FindMainMission(string)` 全部删掉。任务**显示名**改由 UI 用枚举反查 `TaskManager.FindMainMission(MissionEnum)`（任务类型名不唯一，枚举才是精确键）。
- ⚠⚠ **心跳必须对"任何会话"都回，包括还没入房的**（2026-10-07 用户实测"战备界面等一会儿就反复断开/控制台刷串"）：
  库的会话超时判的是"**收不到数据**就判掉线"（`TimeoutMs` 默认 15s），而成员在"连上房主 → 入房成功"这段
  **只有心跳一条报文** ⇒ 房主若按"没入房就不回应"早退，那条连接**一个字节都收不到** ⇒ 必然被**它自己的库**判死 →
  客户端重连（同一 sid）→ 再判死 ⇒ 房主控制台每二十来秒一串 `成员断开 / 成员断连（未入房）`，
  **而真正在房间里的成员一直好着**（日志里只有它能"收到资料上报"）。修法：`NetHostSvc.OnPingReq` 删掉
  `_players.ContainsKey(sid)` 早退，一律回 Pong（关闸由入房校验负责，别靠不回心跳饿死对方）。
  配套：未入房会话的断开日志改成**每个 sid 只提示一次**（`_ghostLogged`）——那种连接本来没有游戏意义，循环重连会刷屏。
  ⚠ `EvictIdleMembers` 只遍历 `_players`（已入房）⇒ 从未入房的连接靠库自身超时收尾，这是对的。
- ⚠ **能发起连接的调用点只有两处**：`NetRoomFlow.Join`（UI）与 `LanRoomDemo`（演示，**没有任何场景引用它**）。
  排查"幽灵连接"时先看这两处 + 另一台机/另一个残留实例；⚠ 查场景里挂没挂某脚本要**按脚本 GUID 扫**（场景 YAML 里没有类名）。
- ⭐ **盟友姿态要带"俯仰"+ 战备/武器随机要播种（2026-10-07 用户实测二连）**：
  **俯仰**：`PoseSnapshot` 原本只带 yaw（注释写着"俯仰留给表现层推断"，**实测证明不行** —— 玩家抬头时身体只转 yaw、
  **武器跟着相机俯仰** ⇒ 别人看你永远平举枪，枪口方向也就错了）。现在加 `[Key(6)] Pitch`：
  发送端取 `BaseSelfController.CameraPitch`（新加的公开只读属性）；`NetTransformView` 把 pitch 一起插值/平滑并暴露
  `RenderedPitch`（**不写进身体旋转**，身体仍是 yaw-only）；`FriendController.Update` 每帧把它推给
  `FriendWeaponView.SetAimPitch`，后者转的是**武器挂点**（复刻玩家侧 `FirstPersonSocket.rotation = Euler(垂直角, 身体yaw, 0)`）。
  **随机播种**：`WeaponBaseController.GetShotDirectionWithinSpread` / `ShootFromMuzzle` 原来用**未播种的 `UnityEngine.Random`**
  ⇒ 轨道轰炸这类"各端各自执行一次"的战备两端各摇各的。现在加 `WeaponBaseController.RandomSeed`（0 = 旧行为）
  + 私有确定性 `System.Random`，由 `VFXAirdropEffect` 在生成战备武器时用
  `SeedUtil.Derive(SeedUtil.Derive(TaskState.Seed, SeedStream.Weapon), 战备ID)`（新增 `SeedStream.Weapon=13`）播种
  ⇒ 同一战备各端同一片火海。⚠ 只按 ID 派生（**不带落点**：落点各端各自算，当种子反而会分叉）；代价是重复呼叫图案相同。
- ⭐ **开火表现带"目标点"+ 呼叫战备同步（2026-10-07 落地，等实测）**：
  **目标点**：`PlayerShoot` 加 `HitX/Y/Z`（Key 5-7，0,0,0 = 旧版）；`NetRoomFlow.SendShoot(slot, dir, hit=default)`、
  `OnShoot` 改成 `Action<uint,int,Vector3,Vector3>`；开枪端取"相机中心射线"落点（`NetFriendBridge.LocalAimPoint`，300m 上限），
  接收端 `FriendWeaponView.PlayShoot(dir, aim)` **把方向重算成"枪口→目标点"**（表现弹是本地模拟的，只给方向时落点由本端地形/枪口偏移决定）。
  **呼叫战备**：`CmdId.AirdropCallNtf=4027`（成员→房主）+ `AirdropCallSync=4028`（房主→全体）+ `AirdropCallMsg{Sid,AirdropId,X/Y/Z}`；
  `NetFriendBridge` 挂 `BattleEventSub.OnAirdrop` 上报**自己放的**那一次（远端复现走 `BattleManager.ReleaseAirdrop` ⇒ `owner=null` ⇒ 天然不回声），
  收同步时 `IsSelf(sid)` 跳过自己、别人 `BattleManager.ReleaseAirdrop(point, id)` 就地复现；⚠ 房主**不在 Ntf 里本地复现**，
  靠转发回来的 Sync 走同一条应用路径。⚠ 复现那份不计"呼叫战备次数"（统计只在发起方本机）。
- ✅ **会话生命周期：房主侧"人走了"已收成唯一入口 `NetHostSvc.RemoveMemberAndNotify(sid)`（2026-10-09，修"战斗中队友强退、房主端模型与 HUD 行都不消失"）**：
  三个调用点（`HandleMemberLeft` 断线/踢人、`OnLeaveRoomNtf` 主动退房、`EvictIdleMembers` 空闲踢人）全部改调它；
  它做齐：清 `_players/_profiles/_poses` → `BroadcastPlayerList()`（战斗期冻结 ⇒ 空操作）→ `SendToAll(PlayerLeftNtf)`
  → **`MessageCenter.Dispatch(msg)` 房主本地自派发**（⚠ `SendToAll` 按定义**不含房主本地**，漏这句房主就永远收不到"人走了"；
  与 `BroadcastPlayerList` 的自派发同款手法）。UI 不用改：`PlayerWnd.UpdatePlayerStates` 每帧按 `ActorsManager.Players` 重排，
  实例一销毁下一帧行自动隐藏。顺带给 `MiniMapWnd` 补上漏的 `OnFriendLeave`（原来只订阅 `OnFriendCreate`，注释还留着"应该还有盟友离开游戏?"）。
  ⚠ 仍未做：`FriendSlotGroup` 靠轮询兜底（够用）；成员端"房主走了"未处理。
- ⚠ **（历史记录，2026-10-07）房主侧"人走了"的两个入口必须同一套清表**（用户实测"另一客户端强关后重复提示好几次、退房后没做处理"）：
  ①**主动退房** = `OnLeaveRoomNtf`（4003）：原实现**只删 `_players`**，`_profiles/_poses/_lastSeen` 全留 ⇒ `_poses` 会让该人的"幽灵位姿"**继续跟着每批快照广播**（`BuildProfileArray` 按 `_players` 遍历，所以 `_profiles` 只占内存）；且**不关会话**，客户端 `NetRoomFlow.Leave()` 自己 `Disconnect()` 只关本地 ⇒ 房主这条会话要等**库内超时**才回收，十几秒后再多报一条"成员断连（未入房）"⇒ 来回加入/退出几次就攒一串幽灵会话。
  ②**强杀/断网** = `EvictIdleMembers`（`memberIdleTimeout` 默认 8s，靠 `Update` 出队时刷新的 `_lastSeen`）：踢人后主动 `CloseSession()` ⇒ 库再回调一次 `OnSessionDisconnected` ⇒ **同一个 sid 第二次进 `HandleMemberLeft`**（原注释说"幂等"，但**日志没去重** ⇒ 用户看到同一个人提示两遍）。
  ⇒ 修法（`NetHostSvc.cs`）：`_selfClosed: HashSet<uint>` 登记"本端主动关的 sid" + `CloseSessionOf(sid)`（先登记再 `CloseSession`）；`HandleMemberLeft` 命中 `_selfClosed` 只静默清痕迹；`OnLeaveRoomNtf` 补齐三张表的清理并立刻关会话；else 分支（未入房）也要清 `_poses/_lastSeen`（⚠ `Update` 对**任何来源**的消息都记 `_lastSeen`，含未入房会话 ⇒ 不清就是每次连接留一条永久条目）；`StopHost` 一并清 `_selfClosed`。
- ⚠ **`sid → 本地下标` 有"两个自己"**（2026-10-07 打包端实测：`收到战备同步但找不到玩家 sid=…（忽略）`、症状是"成员自己选的战备自己看不见、房主却看得见"）：
  `TeamNetBridge.LocalIndexOf` 原先只特例了房主（`sid==0 && IsHost`），**漏了成员自己** —— 成员在 `TeamManager.players` 里占 `players[0]` 但它的 `id` 是**本地存档 UID**（`HandlePlayerList` 重建名单时把"自己"跳过，不写网络 id）⇒ `IdOfSid(自己sid) = -(sid+1)` 永远落空。
  ⇒ 判"自己"必须与 `IsMySid` 同一口径：房主看 `sid==0`，成员看 `sid==SelfSid`，命中即返回 `team.SelfIndex`。
  影响面：**任何房主转发回来的、关于我自己的同步**（战备 4007/4008、强化 4009/4010、以及将来的东西）都会在成员端被静默丢弃。
- ⚠⚠ **"准备状态"三坑（成员点就绪：房主看得见、自己看不见）**（2026-10-07 打包端实测）：
  ① `TeamNetBridge.HandlePlayerList` 的"通知 UI"循环把 **`IsSelfEntry` 的条目一律跳过** ⇒ 成员自己那份永远拿不到 `ReceivePlayerReady`（房主自己跳过是对的：它的就绪是本地行为且名单里恒 true；**成员自己必须放行**）⇒ 现在改成"只跳房主自己"，并用 `LocalIndexOf`（它认"成员自己"）取本地下标。
  ② `NetHostSvc.OnPlayerProfileNtf` 里 `_profiles[sid] = ntf.Profile` + 照抄 `IsReady`：而 `TeamNetBridge.ToProfile` **不设 IsReady** ⇒ 成员每次上报资料（切枪/战备/换角色都会触发）都把准备位重置成 false ⇒ 名单广播后**房主那边也跟着变未就绪**，还会卡住"全员就绪"。修法：`IsReady` **只能**由 `ReadyState` 改，收资料时从 `_players[sid].IsReady` 继承。
  ③ `OnReadyState` 里 `_profiles[sid].IsReady` 也要同步（`BuildProfileArray` 优先用 `_profiles[sid]`）—— 这条已在位，别删。
  ⇒ 排查口径：`NotifyReadyChanged` 用 `profile.IsReady` **优先于** `info.IsReady`，所以上面 ② 会让"房主端也看不见"；只修 ① 不够。
- ⭐ **战斗同步路线（2026-10-07 与用户讨论定调）**：现状是"两端各跑一份世界"，同种子只能让**起点**一致，三个分叉源 =
  ① **开波时机由各端本地进度驱动**（第一波之后就彻底错相）② 敌人 AI 看本地世界 ③ 伤害本地结算（盟友刻意无 `Damageable`）。
  **不做**帧同步/lockstep（`ProjectileStandard`/`Damageable` 走 PhysX + 动画/补间 ⇒ 跨机确定性拿不到）。
  路线 = **C3 玩家间命中**（小）→ **C1 波次权威**（小，房主广播 `WaveStart`，各端按同一条派生流本地生成；把"下一波"的触发从"本地清场"改成"收到房主事件"）
  → **B 敌人权威 + 快照/纠偏**（终局：真·同一场战斗）。
  **B 的关键设计（用户要求"尽量像 A 以降低主机压力"）** = **确定性影子模拟 + 意图下发 + 纠偏**：
  成员端也跑同一份敌人 AI，房主只发 ① `EnemyTarget{netId,targetSid}`（意图，变化时发）② 死亡/命中**事件** ③ `EnemyCorrect{netId,pos,hp}`（超阈值或 2s 兜底）
  ⇒ 带宽从"20Hz×每怪 20B"降到"只在变化/漂移时发"，观感接近帧同步。**前提**：血量/死亡以房主为唯一权威（成员只上报命中、不得自行扣血），且 AI 的随机源必须全部走种子流。
  带宽量化：30 怪 × 20B × 20Hz ≈ **12KB/s**/客户端（主机上行 ×成员数）⇒ 公网也够；再用**优先级轮询**（每帧只发预算内、按距离×重要性排序的实体）可与怪数**解耦**到 ~2~5KB/s。
  主机 CPU 才是真成本 ⇒ AI LOD（远处只挪位不寻路）+ 不做子弹权威（成员命中上报而非房主模拟每人的子弹）。
  **用户 2026-10-07 的四条细化（已采纳）**：
  ① **ID**：`Actor.IndexID` 是**进程内自增**（`GlobalIndexID++`，Awake 赋）且把玩家/盟友幽灵/道具都算进去 ⇒ 必然错位、**不能当网络 id**
  （工程早先已在 `NetFriendBridge` 快照注释下过同样结论）⇒ 新增 **`Actor.NetId`**（0=非同步单位），由**房主分配、随生成事件下发**（等价"原 numid 的意图，但来源改成房主"）。
  ② **移动**：钩子 = `EnemyController.SetNavDestination`（233-265，唯一漏斗 → `UnitEventSub.PathRequest` → `PathRequestManager.RequestPath` → `SetDestination`）⇒
  房主在这里发布 `EnemyMove{netId, dest}`，成员端**只应用不决策**且要**绕开本端去抖**（那套 `<1m + hasPath/pathPending + 重试节流` 是本地状态）。
  ③ **子弹**：各端本地模拟（用户意见）+ 命中上报、**房主结算血量/死亡**；命中带 `shotSeq` 防重传双扣。
  ④ **AI/技能随机**：要改的是**影响逻辑/位置**的（`EnemyMobile`/`EnemyNestBuild`/`UnitSkill_*`/`DieLoot`/`NPCWalk`/`MissionOilRefining`），
  **纯表现**的（`EnemyFXControllerUnit` 特效、武器散布）保持本地随机；用 `SeedUtil.Derive(Derive(seed, stream), netId)` 给每个实体建独立流（**必须用 NetId 当细分键**）。
- ⭐ **波次同步 = 两件事，分开做**（2026-10-07 起）：
  **(1a) 内容确定性（已做）**：`WaveBase` 原来的随机种子取自**全局静态流**（`new Random(RandomUtils.Range(0,1000))`）⇒ 两端必然抽到不同的波
  ⇒ 改成 `SeedUtil.Derive(TaskState.Seed, (int)SeedStream.Wave * 1000 + 波序)`（波序由 `WaveManager._waveSeq` 经 `WaveBase.PendingWaveIndex` 传入），无种子时保持原行为；
  同时修掉 `WaveBase.GetDropPoint` 里两处**漏到静态流**的抽取（`points.RandomTake()` → `points.RandomTake(random)`；`VectorUtils.GetRandomPointInCircle` 新增 `System.Random` 重载并改用它）。
  `RobotWave`/`ZergWave` 其余抽取都已走继承的 `random` ✅（已核对）。
  **(1b) 时序/参数权威（待做）**：开波时机仍是"各端本地进度驱动"⇒ 必须由房主广播 `WaveStart{ waveIndex, extraWave, tip, scale, range, center, points[] }`；
  ⚠ 关键认知：`WaveCreateParams.center` 常常是**本机玩家位置/最近玩家**（"追击"语义）⇒ 两端天生不同，**必须随事件下发**；
  ⚠ 成员端的 `centerGetter`（每 Tick 跟踪本机玩家）也得关掉，否则它的 center 会飘走。
- ⭐ **战斗同步 阶段 2/3/4 已落地（2026-10-07，全部编译+反射验证通过）**：
  **NetId**：`WaveManager.CreatUnit` 用每局自增计数写 `Actor.NetId`（`Actor.IndexID` 是进程内自增、还数着玩家/幽灵/道具 ⇒ 不能当网络 id）。
  ⚠ 因此"两端创建顺序一致"是硬前提（波次已确定性 ⇒ 成立）；**不经 `CreatUnit` 生成的单位 NetId=0 ⇒ 不参与同步**（`Effect/CreatEnemy`、`UnitSkill_Summoner` 的直接 Instantiate、任务召唤等）——**2026-10-08 已收口**，见下面那条 ⭐。
  **移动（意图式）**：钩子 = `EnemyController.SetNavDestination`（唯一漏斗）→ 房主发 `EnemyMoveSync(4024){NetId,目标点}`；
  成员端 `RemoteDrivenMovement=true` 时**本端 AI 决策一律作废**，只走 `ApplyRemoteDestination`（**绕开**本端那套 `<1m + hasPath + 重试节流` 的本地去抖）。两端各自本地算路径 ⇒ 位置接近。
  **命中/生死（权威在房主）**：成员 `_OnDamaged` → `EnemyHit(4025)` 上报；房主 `EnemyNetBridge.OnRemoteHit` 用 `DamagePacket`（⚠ `DamageGroups` 不能空，空则 `InflictDamage` 直接返回）结算，
  以 `EnemyController.ApplyingRemoteDamage` 防回声；某只怪死亡 ⇒ 房主 `EnemyDiedSync(4026)` ⇒ 成员 `Actor.Kill()` 干掉副本。
  **确定性随机**：`06Gameplay/AI/EnemyRandom.cs`（`SeedUtil.Derive(Derive(seed, SeedStream.Ai), NetId)`，按 NetId 缓存；无种子时也按实体分开，避免共用静态流被推进），
  `WaveManager.Awake` 里 `EnemyRandom.Clear()`；已改 `UnitSkill_Blink`/`EnemyNestBuild`/`EnemyMobile_AboState`。
  **仍未做**：移动纠偏/优先级轮询（会漂）、`DieLoot`/`NPCWalk`/`MissionOilRefining`/`UnitSkill_Summoner` 的随机、成员端血量非权威（只保证"死亡一致"）。
- ⭐⭐ **`NetId` 分配点已下沉到 `Actor.Awake`（2026-10-08，修"客机敌人全程原地罚站"）**：
  原唯一赋值点在 `WaveManager.CreatUnit`，而 **`ZergWave`/`RobotWave` 刷单位是裸 `Instantiate`**（`ZergWave.cs:100`、`RobotWave.cs:212/244`）⇒ 全部波次敌人 `NetId==0` ⇒
  ①房主 `SetNavDestination` 的 `NetId != 0` 门挡掉广播 ②成员端 `RemoteDrivenMovement` 又让本地决策整体早退 ⇒ **既无本地也无远端目标 = 永久静止**
  （死亡/命中同步同样被那道门废掉，同一处修复一并治好）。
  现：`ActorsManager` 持每局归零的 `_enemyNetIdSeq` + `NextEnemyNetId()`，**`Actor.Awake` 里 `type==Enemy` 就分号**（覆盖波次/巡逻队/巢穴/召唤/场景刷新等全部路径，新增刷怪方式不必再补）。
  ⚠ 必须在 `Awake`：放 `WaitSetPos` 的 `OnEnemyCreate` 会晚一帧，而波次单位创建后**同帧**就 `SetNavDestination`（房主要在那里广播）；
  ⚠ 判 `ActorsManager.Instance != null`（它由 `BattleManager.Init` 早于一切运行时刷怪创建；更早诞生的 Actor 分 0，防与归零后的号段撞号）。
  配套两条"别丢消息"：`EnemyController.ApplyRemoteDestination` 改为 **pending + `Update` 重试**（只拦 agent 被禁用的空投落地前阶段，其余仍交 `PathRequestManager` 兜底）；
  `EnemyNetBridge` 加 `_pendingMoves`，**目标点比副本先到就暂存**，`UnitEventSub.OnEnemyCreate` 时补发（房主只在目标变化时发 ⇒ 丢一条就没有第二条）。
  安全性实测：93 个带 `Actor` 的预制体里 `type=4(Enemy)` 恰好 35 个且全在 `Resources/Prefabs/Enemy/`（炮塔=8、地雷/场景物=16）⇒ 不会把非敌人物体算进号段。
- ⚠ **"开火同步"≠"命中/血量同步"**（2026-10-07 用户问"开火同步了，命中应该也同步，那血量是不是不用同步"，答案：不能省）：
  ① `FpsHelper.Hit(ProjectileHitData)`（`06Gameplay/Common/FpsHelper/FpsHelper_Hit.cs:148`）是**本地子弹碰撞**的伤害结算入口，调用者全是本地模拟事件
  （`ProjectileStandard.HitFX`、`DeployableMine.DoExplosion`、`SustainedEffect.ApplyEffect`、`AirdropPod.Hit`）—— 它读 `hitData.collider` 然后 `IDamageable.InflictDamage`；
  ② 4015/4016 的接收端只做枪口闪 + 枪响 + 枪械动画（`FriendWeaponView.PlayShoot` 刻意**不调 `HandleShoot`**，不生成子弹）⇒ 对面根本没有"他的子弹"，`Hit` 永远不会被远程开火触发；
  ③ 决定性的一条：**两端各自模拟一份世界**（只同步了玩家位姿 `PoseBatchMsg`，没有敌人/世界状态同步）⇒ 同一时刻两边的怪位置/血量/死活都不同，"由命中推血量"没有共同基准。
  ⇒ 玩家血量在**各自机器上本地权威**（敌人打他、护盾恢复、复活都是本地算），同步的**唯一目的是"给别人看"**（盟友血条/倒地姿态/将来"救起队友"）。
  成本极低（只在值变化时发 + 0.5s 对账，5 个 float）。若将来要做"同一场战斗"（队友能帮你打死你看到的怪），正确做法是**敌人由一方权威 + 敌人快照下行**，而不是同步每一发命中（RTT 与位姿分叉都会让它错）。
- ⚠ 若要给 `10_UI` 调 `NetSvc`/`NetHostSvc`：**必须给 `10_UI.asmdef` 加 `02_Net` 引用**（现 references 里只有 `KCPNet` 与其它项目程序集，**没有 `02_Net`**）；房间发现本身不需要（直接吃 `KCPNet.*`）。更好的做法是在 `02_Net` 加薄封装、UI 只跟接口通信。
- ⭐ **局内表现同步消息表**（2026-10-07 加，全部"成员→房主 Ntf / 房主→全体 Sync"同型双向，房主本地自派发 + `RelayToAll` 中继）：
  `4013/4014 = PlayerWeaponSwitch{ Sid, SlotIndex }`（切枪，槽位口径 = `PlayerWeaponsManager.SlotOf`）；
  `4015/4016 = PlayerShoot{ Sid, SlotIndex }`（开火表现：枪口闪/音效/枪械动画，**不结算伤害**）；
  `4017/4018 = PlayerVital{ Sid, Hp, HpMax, Shield, ShieldMax, Down }`（血/盾/倒地，传"值+上限"而不是比例）；
  `4019/4020 = PlayerAmmo{ Sid, Ratio }`（弹药系数，独立通道：每发子弹都变，与"变化即达"的血盾节奏不同；桥侧 10Hz + 变化判定）。
  消费/生产都在 09 的桥 `NetFriendBridge`（上行订阅本机 `PlayerWeaponsManager.OnSwitchedToWeapon/OnShoot` + 轮询血量；
  下行按 Sid 找盟友 → `FriendController.SetActiveWeaponSlot/PlayShoot/ApplyVital`；自己发的用 `NetRoomFlow.SelfSid` 丢掉）。
  ⚠ 盟友的**伤害仍由各自主机权威**：`PlayerFriend.prefab` 上刻意**没有 `Damageable`**，血量只由 4017/4018 镜像进它新增的 `HealthPlayer`。
  ⚠ 盟友离场**没有事件**（`FriendDead` 早被删）⇒ 任何"为盟友建的 UI"（如头顶血条）都要自己兜底清理，否则就是 `MissingReferenceException`。
- ⭐ **场景单位（NPC/`NPCWalk`）移动同步 = 路线 A（2026-10-08 落码 → 2026-10-09 改为自举，已可用）**：走**新通道 4032**（`SceneUnitMoveMsg{ Id=Actor.Id, X/Y/Z, Stop }`），**不复用 4024 的 NetId**——大厅会反复重建、NetId 的"每局归零+两端创建顺序一致"不成立。链路：房主 `NPCWalk` 决策 → `BattleEventSub.OnSceneUnitMove` → `09Manager/Global/SceneUnitMoveSink` 转发 → `NetRoomFlow.SendSceneUnitMove`；下行按 `Actor.Id` 找本端 NPC 应用。
  ⚠⚠ **原来的失败原因**：设计是"挂哪个场景就管哪个场景"，结果**全项目没有任何场景/预制体挂过 sink**（实测 0 命中，只有 `Utnapishitim.unity` 挂了 `NPCWalk`）⇒ `NPCWalk.RemoteDriven` 永远 false ⇒ 成员端照旧自己摇随机 ⇒ 两端 NPC 各走各的。
  修 = `SceneUnitMoveSink` 加 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap()`：没有就建一个 `DontDestroyOnLoad` 的（场景里手工挂了就让位）。常驻不串场景：它只订阅全局事件 + 扫静态 `ActorsManager.Actors`（`Actor.OnDestroy` 会摘）。
  ✅ 已核实 `Utnapishitim.unity` 里 4 个带 `NPCWalk` 的 GameObject 都带 `Actor` ⇒ 同步键 `Actor.Id` 成立（若某 NPC 没有 `Actor`，它的 `_unitId` 为空 ⇒ 不参与同步）。
  ⚠⚠ **但挂上 sink 后客机 NPC 会全员罚站 —— 真因（2026-10-09）**：sink 的 `Find` 扫的是 `ActorsManager.Actors`，而该表在 `ActorsManager.Awake` 里被 **`Actors = new()` 整个换新**
  ⇒ **场景里先于它 Awake 的 NPC 全被丢出表**（实测大厅 `Actors` 只剩 7 项：Yuuka/Nagisa/Cafe_Moe_Original/Modle/Kotama_Original/Player(Clone)/Friend_0，四个 NPC 一个都不在）
  ⇒ `Find` 永远失败、指令全堆在 `sink._pending`（实测 `_pending=3 键=[Aris][Hare][Mika]`）。**这是"路线 A 落码却从未跑通"的真正原因。**
  修：`NPCWalk` 自记在场表（`static s_all` + `OnEnable/OnDisable` + `UnitId` + `static FindById(id)`），`SceneUnitMoveSink.Find(id) => NPCWalk.FindById(id)`（顺带解决"玩家角色与 NPC 同名"）。
  ⚠ 05 层的根（`ActorsManager.Awake` 换表丢掉先注册者）**没修**，任何扫 `Actors` 的功能在大厅都会漏；本次只是绕过。
  ⚠ `Noa` 的 GameObject `activeInHierarchy=False` **是设计如此**（主机玩家选了 Noa 这个角色 ⇒ 客机端对应 NPC 被隐藏，用户 2026-10-09 确认）⇒ 它两端都不动、从不发布（日志里只有 Mika/Hare/Aris）；`OnDisable` 已把它移出自记表，无害。
  ✅ **2026-10-09 用户实测：NPC 位置同步已正常**（修完 `Find` 后），随即将 NPC 相关日志断掉（`NPCWalk`/`SceneUnitMoveSink` 里的每移动一条都删了，只留一次性/异常级）。
- ⚠ **"反复进房出房"（幽灵会话）已定性 = KCPNet 内部重握手，无害**（2026-10-09 用数据钉死）：
  房主侧 `[HostSession]` 打印的**所有**会话都是**同一个对端 `192.168.1.12:60476`**（= 客机那一台的那条 socket，`Get-NetUDPEndpoint` 查到该端口属客机进程），
  而客机自己只打过**一次** `[NetRoomFlow] 回连房主 … / [NetClient] 回连房主 → 成功`；客机 `SelfSid` 稳定、`IsConnected=True`、`RttMs=32`
  ⇒ 同一 socket 被库反复重握手 ⇒ 服务端每次新建一个 sid，旧的在库超时后自己关。**典型症状：名单里一串"尚未入房"的日志，隔一会儿自己断。**
  ⚠ 排除过"广播打的"：发现层端口（`LanBroadcastPort=29800/29801`）与 KCP 端口（`HostGamePort=17666`）不撞。
  ⚠ 也排除过"多进程"：进程快照里只有一个 `BlueDivers.exe`（就是房主）+ 一个编辑器（客机）+ 两个 AssetImportWorker。
  ⇒ 现状：`HostSession` 的会话日志**已限流**（前 3 次 + 之后每 10s 一条，带累计次数；回调在库线程上 ⇒ 用 `DateTime` 不能用 `Time.xxx`）。要根治得改库（无源码）。
- ✅ **远程弹道"穿过去、没命中"已修（2026-10-09）**：`PlayerShoot` 早就带了命中点（`HitX/Y/Z`，4015/4016），但接收端 `FriendWeaponView.PlayShoot` **只用它重算方向**、
  随后把命中点丢掉（`SpawnVisualBullet(muzzle, direction)`）。远端那颗"表现弹"是**真物理子弹**（`ProjectileStandard` 靠 `SphereCast` 自己撞），而两端敌人位置只是近似
  ⇒ 撞不到 ⇒ 一路飞到 `MaxRange` 消散，观感就是"从目标身上穿过去"。
  修：`SpawnVisualBullet(muzzle, direction, endPoint)` 把命中点传下去 → `ProjectileBase.SetVisualEndPoint`（`Shoot` 里复位，池化安全）
  → `ProjectileStandard.ReachedVisualEndPoint()`（**用"本帧位移"投影判定**，高速弹单帧数米也不会漏）→ 到点即 `OnHit(终点, -forward)` 按命中收尾。
  ⚠ `SpawnVisualBullet` 摘掉 `FpsHelper.Hit` 时**连带**摘掉了命中特效/音效/弹痕 ⇒ 抽出 `FpsHelper.PlayImpactFx(hitData)`（`Hit` 改为调它 ⇒ 本地零变化），表现弹补挂它。
  ⚠ 子类 `Update` 都调 `base.Update()`（`ProjectilePlayerStandard:91`/`PlayerLaser:28`/`PlayerHoming:71`/`LockStandard:32`）⇒ 判定不会被绕过（若将来新增不回调基类的子弹类型要留意）。
  ⚠ 注册/收信留在常驻 `NetRoomFlow`（否则场景不在时"未注册消息:4032"）；`NPCWalk.RemoteDriven` 语义 = **有桥在场且我是成员**（桥没了退回各自本地游荡，不会全体罚站）；⚠ 大厅**没有 `PathRequestManager`**（战场 `BattleManager` 才建）⇒ 不能用 `UnitEventSub.PathRequest`，只能直接 `SetDestination`；⚠ 大厅**没有 `ActorsManager` 实例** ⇒ 只能扫静态 `ActorsManager.Actors`（`Actor.Awake` 无条件登记）。
- ⚠ 「服务器 / 局域网」**没有数据来源**：`NetSvc.SRV_IP="127.0.0.1"` 是硬编码示例、`ConnectDefaultServer()` 无调用点，房间只来自 `LanDiscoverer` 广播 ⇒ 房间列表面板只能**按 `HostIp` 猜**（内网/环回 ⇒ 局域网，其余 ⇒ 服务器，详见 `ServerListPanel.RoomNetType`/`IsLanAddress`）。要让类型真实，得让房间数据自带「来源」字段。
- ⚠⚠ **`02_Net` 至今未挂进游戏**（2026-10-06 实测）：`NetSvc` / `NetHostSvc` / `LanRoomDemo` / `NetDemo` 在**所有场景与 prefab 里 0 命中**（只在各自 `.meta` 命中）⇒ 任何 UI 调 `NetSvc.Instance` 都是 null。联机改造第一步必须是"网络根节点引导"（推荐 `[RuntimeInitializeOnLoadMethod]` 建 `DontDestroyOnLoad("NetRoot")`，同 `WndHub.Bootstrap` 手法）。
- ⚠⚠ **KCPNet 无源码**（2026-10-06 探测 6 个根目录 × 深 5，`plans/find_kcp_source.py` 0 命中）⇒「给 `LanRoomInfo` 加 Difficulty/InGame/Source 再重出 DLL」需用户提供源码；退路 = 02_Net 自研 UDP 发现层（自带模型），回连仍可复用 `NetSvc.ConnectToRoom(new LanRoomInfo{HostIp,HostPort})`（实测该方法只用这两个字段）。库实测：`LanRoomInfo` public/not sealed/`[Serializable]`，恰好 9 个 public 字段，`ToJson`(实例)/`FromJson`(静态)（工具 `plans/dll_type_dump.py`）。
- 房间列表接入的完整计划 + Rooms 预制体实测层级（无 Status 节点、Server/Cancel 是 Rooms 兄弟节点、`Filter/placeholder` 只是 LayoutElement 占位）见 `.codebuddy/plans/联机_房间列表接入与KCPNet扩展_计划.md`；层级可复现脚本 `plans/rooms_hierarchy.py`。
- ✅ **已接入游戏**（2026-10-06）：`NetSvc`/`NetHostSvc`/`NetRoomFlow` 挂在 `Assets/Resources/Prefabs/Manager/GameRoot.prefab` 的**根 GameObject（名字就叫 GameRoot，脚本原本也在这个根上，不是子物体）**；`GameRoot.unity` 里有该 prefab 实例 ⇒ 常驻。`Assets/Scripts/10UI/10_UI.asmdef` 已加 `02_Net` 引用（`10_UI → 02_Net` 无环）。
- ⭐ **`FPSGame.Net.RoomMeta` = 房间字段的唯一薄适配层**（2026-10-08 库改完后收尾）：只留 `Difficulty`（`out bool fromBroadcast` = `room.Difficulty >= 0`）/`TaskEnum`/`InGame`/`Source`/`RoomName`/`MapName`，职责 = 缺省值归一 + 空引用保护。
  房主侧：`NetHostSvc.StartHost` 直接写字面量 `RoomName/MapName/Difficulty/TaskMain/InGame/Source`（`Source=0`），`GetBroadcastInfo` 每轮刷 `InGame = _started`，
  `ConfirmTask(..., int taskMain)`（**`taskType` 参数已删**）就地刷 `RoomInfo.TaskMain`；`HostRoomOptions.TaskType` 字段已删。
- ⭐ **库 DLL 换代（2026-10-08 实测，`KCPNet.LanRoomInfo` 9 → 13 字段）**：新增 `int Difficulty=-1 / int TaskMain=-1 / bool InGame / int Source`（**没有 `TaskType` 字符串**）；
  JSON 键 `difficulty/taskMain/inGame/source`，旧键名 `roomName/hostIp/hostPort/playerCount/maxPlayers/mapName/passwordProtected/version/playerNames` 未动。
  `FromJson` **忽略未知 key、缺 key 给默认值**（实测：只带 3 个键的旧包 ⇒ `diff=-1/task=-1/inGame=false/source=0`）⇒ 新旧房主可混联。
  `LanBroadcaster`/`LanDiscoverer` 无公开房间字段 ⇒ 库改动只落在 `LanRoomInfo` + 两个 JSON 方法。
  ⚠⚠ **换 DLL 后 Unity 里的旧程序集不会自动换**：本次 `AssetDatabase.ImportAsset(ForceUpdate)` 单独调用**无效**（反射仍是 9 字段），必须再 `refresh_unity(force, all, request)` 触发域重载才生效（判据 = `unity_reflect` 数出 13 字段）。
- ⭐ **库 DLL 再换代（2026-10-09，自动心跳 + 持久 clientId）**：新 `KCPNet.dll`（48128 B）新增 `KCPNet<T,K>.ClientId`（get/set）、`KCPSession<T>.{HeartbeatIntervalMs,TimeoutMs,EnableTimeoutCheck}`、`NetConfig.{HeartbeatIntervalMs=3000,DefaultTimeoutMs=15000}`。⚠ **`NetConfig` 在库内部（工程里没有 `NetConfig.cs`）** ⇒ 那两条常量改不了，要调只能逐会话设属性。
  **心跳** = 写在 `KCPSession` 基类、3s 双向互发 ⇒ `ClientSession`/`HostSession` 自动继承、业务零改动，且**不进 `MessageCenter`**（不污染游戏逻辑）。
  **去重** = 握手 `REQUEST_CONNECT:<clientId>` / 道别 `DISCONNECT:<clientId>`（不带该段仍兼容）；客户端要做的两行已落 `NetSvc.cs`：字段 `private readonly string _clientId = Guid.NewGuid().ToString("N")` + `ConnectTo()` 里 `client.ClientId = _clientId`（换端口重连复用同值 ⇒ 房主侧只留一条会话）。`NetHostSvc` 无需改（房主是服务器侧，不填 `ClientId`）。
  ⚠⚠ **库心跳不喂 `NetHostSvc._lastSeen`**（它只在 `Update` 出队、经 `MessageCenter.Dispatch` 时刷新）⇒ 应用层 2s `PingReq` 心跳**必须保留**，否则 `EvictIdleMembers`（8s）会把健康成员误踢。
  验证：`Library/ScriptAssemblies/02_Net.dll` 字节能搜到 `_clientId`、Console 0 error、离线编译 0 错误；`KCPNet` 反射出上面 5 个新成员。
- ⭐ **库心跳的真实机理（2026-10-09 反射 + IL 反汇编实测，别再靠猜）**：`KCPSession<T>` 内部有
  `static byte[] HeartbeatMarker` / `_lastHeartbeatSendTicks` / `_lastRecvTimeUtcTicks` / `_heartbeatLock` +
  私有 `TrySendHeartbeat(DateTime)` / `static IsHeartbeat(byte[])` / `RefreshLastRecvTime()`。
  - `TrySendHeartbeat` IL：`HeartbeatIntervalMs<=0` 直接 ret；用 `_lastHeartbeatSendTicks` 节流；把 `HeartbeatMarker.Clone()`（有 `AesKey` 先 `Encrypt`）**直接 `m_kcp.Send(...)`** ⇒ **发的是带魔数的裸字节，不是 `NetMessage`**。
  - `IsHeartbeat` IL：先比长度、再逐字节比 `HeartbeatMarker`（全等）⇒ 这是"进入反序列化之前的判据"，所以心跳**永远到不了 `OnReciveMsg`/`MessageCenter`**。
  - `UpdateAsync` 状态机 IL 顺序：`OnUpdate(now)` → `TrySendHeartbeat(now)` → `if(EnableTimeoutCheck)` 锁读 `_lastRecvTimeUtcTicks`，`(now.Ticks-last)/10000 > TimeoutMs` ⇒ `WarnLog` + **`CloseSession()`** 退循环 → 之后才是 KCP `Update/Recv`。
  - `ReciveData(byte[])`（UDP 入口）：`RefreshLastRecvTime(); m_kcp.Input(bytes)` ⇒ **任意 UDP 包到达都刷库时钟**；心跳只是"空闲时也保证有包可刷"。
  ⇒ 两条独立时钟：**库超时看 `_lastRecvTimeUtcTicks`（15s，库自己 `CloseSession`）**；**房主 `EvictIdleMembers` 看 `_lastSeen`（8s，只由业务消息出队刷新，`NetHostSvc.cs` Update 出队循环）**。判据与作用域都不同 ⇒ 库心跳**替代不了**应用层 Ping；且库未暴露"某会话最后活动时间"（`RefreshLastRecvTime` 私有、`OnUpdate` 不带 sid）⇒ 现状下应用层 Ping 是房主感知成员存活的**唯一通道**。`RttMs` 也依赖 Pong。
- `FPSGame.Net.NetRoomFlow`（新，挂 GameRoot）= 成员入房唯一入口：`Join(room, name, pwd, cb)` 内部「ConnectToRoom → JoinRoom」，**带超时（默认 10s）**、事件 `OnJoinResult/OnPlayerList/OnStartGame`、`Host(HostRoomOptions, out reason)`；`MessageCenter` 同命令号单处理器 ⇒ 它与 `LanRoomDemo`/`NetDemo` 不能同时挂。
- `HostRoomOptions`（`Services/Msg/RoomMsg.cs`）：`StartHost(HostRoomOptions)` 是推荐入口（旧 4 参签名保留给 demo）；`StartGameNtf` 已扩 `Difficulty/TaskIndex/ExtraDiff/Seed/PlayMode`（成员侧应用仍是 TODO）。
- `PasswordWnd`（`10UI/Wnd/PasswordWnd.cs` + `Resources/UI/Wnd/PasswordWnd.prefab`）：通用单行输入窗，`WndType.Password` + `WndHub.Password`；已用于**房间密码**（`ServerListPanel.Activate`）与**首次起名**（`FrontWnd.AskPlayerName`）；预制体由 TipWnd 复制改造，输入框是从 SelectMapWnd 的 `Filter/InputField (TMP)` 复制来的。
- 🔄 **「公开房」建服时机（2026-10-06 用户口径，已改）**：地图界面 `pubilc` 按钮**只**记 `SelectPlayMode=1` 并走与单人相同的 `ExpandCfg()`（调难度/选任务），**不再当场开房、不再顶出服务器列表**；服务器在 **`SelectMapWnd.ConfirmTask`（按下「准备」）里 `CreateRoom(true)` 静默创建**（不弹提示、不掀列表；失败仍弹），随后才 `SetTask` + `NetHostSvc.ConfirmTask` 广播本局配置。「我是不是房主」的唯一判据 = `NetHostSvc.RoomInfo != null`（`SelectMapWnd.IsHosting()`；`StartHost` 设 / `StopHost` 清）。**收人窗口 = Ready**（选完任务 → 所有人就位；一进 Armament 就 `CloseJoin()` 关闸）。
- ⭐⭐ **阶段模型（2026-10-06 用户纠正过我两次错误认知，以本条为准）**：`ConfirmTask`（原 `StartTask`，**只是"选完任务"**）只把 `GameState` 推到 **Ready —— 这时还在舰桥**；**所有人就位**才进 **Armament 阶段**（`GameStateEnum.Armament = 1<<7 = 128`，同样在舰桥，各自选战备）；**该阶段结束（全员准备）才进 `Transition` 并加载战斗场景**，触发点是大厅里的 `GameStateController`（guid `566bc32b…`，按 `GameStateEnum` 派发 UnityEvent）里 **`state: 8 = Transition` → `TransSceneController.StartLoad()`**（`Utnapishitim.unity:15614-15621`），**每个客户端各自触发**（`ArmamentWnd` 全员就绪 → 播 Exit → 动画事件 `FinishReady()` → `GameState = Transition`）。⇒ 建服放在 `ConfirmTask` 是对的：Ready 还在舰桥、成员有窗口搜房/加入（**进 Armament 即关闸**）。
- ✅ **三处衔接已修 + Armament 关闸（2026-10-06 落码，用户选方案 A 后追加）**：① 成员侧不再"收到配置就 `AsyncLoadScene`"（旧实现会让成员在 Ready 就跳进战场、房主还留在舰桥）⇒ `TeamNetBridge.HandleTaskConfirm` **只落配置**（`SetSeed` + `SetTask` + 收起选图窗），`_loadingBattleScene` 已删；② 名单冻结从 `ConfirmTask` 挪到 **`NotifyTransition`（进 Transition 才冻结）**，Ready 期间仍可进人（公开房"后加入的人"这才通）；③ `OnJoinRoomReq` 在冻结时 `SendJoinFail("本局已开始")`，未冻结则**补发** `_lastTaskConfirm`（新人拿得到地图/任务/种子才进得了 Ready）；④ **进 `Armament` 即关闸**（`NetHostSvc.CloseJoin()` → `OnJoinRoomReq` 回 `"人员已就位"`）：⚠ 关闸与冻名单**必须分开**（`_joinClosed` 独立于 `_rosterFrozen`）——`ArmamentWnd` 的"全员就绪"判定要靠名单里的准备状态，在这里冻名单会把该阶段冻死；闸门在下一局 `ConfirmTask` 重开，`StartHost`/`StopHost` 均复位。
- ⭐ **新增 `CmdId.Transition = 4012` + `TransitionNtf { MatchId }`**（房主权威"进战斗"）：房主侧 `TeamNetBridge.HandleGameStateChange` 监听 `GlobalEventSub.OnGameStateChange`，`entry == Transition` 且自己是房主且本局没发过（`_transitionSent`，回 `Ready` 复位）⇒ `NetHostSvc.NotifyTransition()`（广播 + 冻结名单 + `_started = true`）；成员侧 `HandleTransition` **只把 `GameRoot.GameState` 推到 `Transition`** ⇒ 由大厅既有的 `GameStateController(state: 8) → TransSceneController.StartLoad()` 完成加载。⚠ 成员**绝不能**自己再 `AsyncLoadScene`（双加载）；`GameRoot.GameState` setter 自带"同值不发事件" ⇒ 两条路径天然幂等。
- ⚠ **命名纪律（踩过，务必记）**：`StartGameNtf` / `NetHostSvc.StartGame` / `SelectMapWnd.StartTask` 已统一改名 **`TaskConfirmNtf` / `CmdId.TaskConfirm`（4006 未变）/ `ConfirmTask`**；`10_Effect/ShootMapPreView.cs` 的 `public void StartTask()` **是场景 UnityEvent 按方法名调的**（`Teach.unity:24319`、`BattleScene.unity:2392`）⇒ **批量改方法名前必须先 grep 场景/prefab 的 `m_MethodName`**（`.cs` 里搜不到调用点 ≠ 没人调）。
- ⚠ **传输层回调 = 后台线程**（2026-10-06 踩过）：`host.OnSessionConnected/OnSessionDisconnected`（`KCPNet.OnSessionConnected` 那类）和 `OnReciveMsg` 一样跑在 ThreadPool 线程 ⇒ **绝不能在回调里碰 Unity API / 游戏状态**。`NetHostSvc.OnMemberLeft` 原来直接 `BroadcastPlayerList()`（→ 桥 `ResSvc.LoadRes`）⇒ `UnityException: Load can only be called from the main thread`，且异常抛在**广播名单中途**⇒ 该清退的盟友留成幽灵。现改为"只入队 `_sessionEvents` → `Update()` 里 `ProcessSessionEvents()` 主线程处理"。同类改动要点：`NetFriendBridge.HandleRoster` 改成**先清退再新建**（防一次异常留幽灵）；`AttachRoleModel` 加 `fc.RoleId == roleName` 早退（名单同步很频繁，别再每次 `LoadRes`+`Instantiate`）。
- ⚠ **`PlayerProfile` 原先只在"换角色"时上报**（`BridgeRoleManager.SetPlayerRole`）⇒ 入房后双方资料都停在"只有名字、`RoleName` 空"的占位 ⇒ 盟友实体建出来却**没有身体**（`NetFriendBridge.AttachRoleModel` 要求 `RoleName` 非空；`PlayerFriend.prefab` 本身不含身体，身体是运行时挂 `StudentModle/<RoleName>`）。已修：`TeamNetBridge.HandleJoinResult`（订阅 `NetRoomFlow.OnJoinResult`，入房成功即 `SendSelfProfile()`）+ 房主在 `GameState → Ready` 时也推一次自己的资料（写进 `HostProfile`，否则成员端看房主是空壳）。排查关键词：`[NetFriendBridge] 创建盟友实例 sid=…`（实体建了）/ `找不到角色模型 …`（路径问题）。
- 开房入口两处：地图界面 `pubilc` 按钮（`SelectMapWnd.CreateRoom`）与设置界面 `stateWnd/PlayerStateSelf/FriendRoot` 的 3 个**空位**（`SettingWnd.CreateRoomFromEmptySlot`，空位节点 `PlayerState(n)/GameObject (1)/Image` 运行时补 Button）。
- ✅ **局内玩家表 ↔ 网络 已接通**（2026-10-06）：`RoomManager` 已改名 **`TeamManager`**（`09Manager/Global/TeamManager.cs`；`04Data/RoomState.cs` → `TeamState.cs`，GUID 保住；`Window` 字段改叫 `teamManager`；全仓无旧名残留）。新增 **`09Manager/Global/TeamNetBridge.cs`**（`I_GlobaManager`，挂 GameRoot）做唯一映射点 + 开局复现。
- **职责口径（写死）**：`NetHostSvc/NetRoomFlow` 持"**谁在场**"（连接/会话/房间参数/资料表 `_profiles`）；`TeamManager` 持"**这一局谁上场 + 角色装备**"；只有 `BridgeSys.Send*` 发（联机走 `NetRoomFlow`，单机仍本地回环）、只有 `Receive*` via `TeamNetBridge` 改 `TeamManager`。⛔ **不要合并 `TeamManager` 与 `NetHostSvc`**（09 与 02 跨层；`PlayerData` 与 `PlayerInfo` 还差一层映射）。
- **两套下标别混**：网络 `PlayerIndex` = 房主视角（房主固定 0）；本地 `TeamManager.players` = 自己视角（**自己永远 0**，`ArmamentWnd` 靠这条）⇒ 战备/强化同步都带 `Sid`，由 `TeamNetBridge.IdOfSid`（房主 0⇒-1、成员 sid⇒-(sid+1)）换算。`PlayerProfile`（02_Net）是 `PlayerData` 的平行 DTO，新增字段两边都要加。
- ⭐ **`BridgeSys` 是 Photon RPC 的接缝**（三条 Send 已改成联机走网络、单机本地回环）；**⚠ 它只挂在大厅场景 `Utnapishitim.unity`**（不在 GameRoot 常驻）⇒ 战场里 `BridgeSys.Instance` 为 null，所有调用点都要判空。`TeamManager.AddPlayer` 里的 `RandomUtils.InitRandom()` 已按联机要求删除（种子只由 `SetSeed`/`JoinPlayer(list,seed)` 定，`ReplacePlayers` 不碰种子）。
- 🔴🔴 **"种子同步"目前整体空转**（2026-10-06 发现）：`SelectMapWnd.cs:652` 传给 `host.StartGame(mapName, SelectTaskIndex, SelectTaskExtraDiff, 0, SelectPlayMode)` 的 **seed 就是 `0`**，而 `TeamManager.SetSeed(0)` 走 `RandomUtils.InitRandom()`（**时间随机**，`TeamManager.cs:116`）⇒ 两端静态流并没有真同步。**且用户记忆中的"加入房间同步种子"确实存在**（`TeamManager.SetSeed` ← `TeamNetBridge.cs:161` / `JoinPlayer(:121-128)`），但它**只喂 `RandomUtils`，完全没碰 `BattleRandom`**。⇒ 方案：新增单点权威种子 **`TaskState.Seed`**（`TaskManager.SetTask(...)` 加 `seed` 形参写入；`BattleManager.cs:111/164` 改读它建 `BattleRandom`；地形流/谜题流都从它派生；房主在 `SelectMapWnd.StartTask` 生成真种子替掉 0）⇒ `StartGameNtf.Seed`/`BattleRandom`/地形流/谜题流四者同源。
- ⭐ **A2 定稿（2026-10-06 第三轮，含对我自己结论的修正）**：`TaskManager.Update()`（`:156-171`）的**自动刷新具有"自愈"作用** —— `hasTriggeredThisMinute` 复位保证每个 **:00/:30 必刷一次**，且刷的是**当前桶**值 ⇒ **只要两端都在运行，跨过任一 :00/:30 就收敛到同一张 `TaskCfgs`**（所以"30 分钟窗口 + 两端在跑"其实基本可靠，我早前"约 1/3 会不一致"的估计作废）。⚠ 真正在制造不一致的是**当前 2 分钟版本**：`now.Minute/2*2` 让"启动时"算的值与":00/:30 刷新时"算的值不同。⇒ **A2 只需改 `TaskManager` 一个文件**：①窗口改回 30 分钟（加 `bool` 可切 2 分钟调试）②种子改 **UTC 桶** `DateTimeOffset.UtcNow.ToUnixTimeSeconds()/1800`（`DateTime.Now` 跨时区必崩；且旧公式 `Month*100+Day+Hour*100+window` **非单射**，会碰撞）③刷新条件从 `Minute==0||30` 改成**桶变化检测**（`bucket!=_lastBucket → _pendingRefresh=true`）**且仅在 `GameStateEnum.Bridge` 执行**（顺带修掉**休眠漏拍**：13:29 睡到 13:41 时原逻辑永不刷新）④`TaskCfgFingerprint` 兜底（覆盖时钟偏斜/时区设错/极端休眠）。**下发 TaskCfg 暂不需要**，留作报错后的升级路径。
- ⭐ **A5 结论（"按坐标哈希排序 `_initQueue` 能否同步自增 id"）⇒ 不能，也不建议做**：`Actor.IndexID` 在 `Instantiate` 时由**全局 static 计数器**分配（`Actor.cs:24/206`），且创建路径远不止 `_initQueue`（玩家 `RoleManagerBase.cs:34`、任务实体 `MissionController.cs:171`、敌人 `WaveManager.cs:255`、召唤物 `UnitSkill_Summoner.cs:23` 都直接 Instantiate）；**计数器跨局不清零** ⇒ 任一端历史上多建过一个 Actor 就永久错位。而 `IndexID` 已被当**本地身份键**用（`Actor.Equals`/`GetHashCode` `:109/113-116`）⇒ 强同步要求"两端历史 Actor 总数与顺序永远一致"，动态刷兵下不可能。**⇒ `IndexID` 保持本地，跨端身份用新的 `NetId`（房主分配）并存。** 排序+坐标哈希的正确用途是"保证生成物集合与参数一致"，注意：**先量化再哈希**（`RoundToInt(pos*100)`）、**重合要有 tie-breaker**、**必须先改成各自确定性流再排序**。
- ⭐ **程序集 GUID → asmdef 映射（2026-10-06 实测，别再猜）**：`e803c1ff`=00_Attributes、`82f5fbe1`=00_Core、`57eb3f01`=**01_GameContract**、`be60ed10`=**00_Utils**（`Assets/Scripts/00Tools/`，`RandomUtils.cs` 所在）、`5a18aab2`=04_Data、`3b918227`=05_UnitCore、`b8b5f059`=06_Gameplay、`8f290685`=08_Map、`8722b7a0`=09_Managers、`aebd9ea0`=10_UI、`6fba9e6b`=10_Effect、`0c95b8e2`=02_Net。⇒ `00_Utils` 被 08_Map/09_Managers/06_Gameplay/10_Effect/04_Data 全部引用 ⇒ **公共随机设施（`SeedUtil.Derive`）放 `00Tools` 一处定义全项目可用**；⚠ `02_Net` refs=[] 看不见它 ⇒ 指纹计算必须放 `09_Managers`。
- ✅ **"战斗中重建 `TaskCfgs` 是否安全"已澄清 = 安全**：`nowTask.taskCfg = TaskCfgs[mapIndex, taskIndex]`（`TaskManager.cs:319`）是**引用拷贝**，而 `CreatAllTask` 是 `TaskCfgs[i,u] = new TaskCfg{…}`（替换数组元素、**不改旧对象**）⇒ 正在跑的那局仍持旧对象 ⇒ "仅舰桥刷新"只是体验优化，不是正确性必需。
- ✅ **种子同步 / 初始化确定性 / 成员进战斗场景 已实现（2026-10-06）**：新增 `00Tools/SeedUtil.cs`（`SeedStream`+`SeedUtil.Derive(seed,purpose)`，纯整数混合、全项目可见）；`Constants.BattleSceneName="BattleScene"`；`TaskState.Seed`（唯一写入点 `TaskManager.SetTask`，经 `SyncTaskState` 发布）；`TaskManager` 桶机制（`CurrentBucket()=UtcNow/1800`，调试开关切 120s；`Update` 做**桶变化检测 + 仅 `GameStateEnum.Bridge` 执行** ⇒ 修掉休眠漏拍 + 战斗中不换表；`CreatAllTask(bucket)` 不再自取时间；`SetTask(…,int seed=0)` 回落 `taskCfg.seed` 并**归一非 0**；`TaskFingerprint(mapId,taskIndex)` 用 FNV 整数混合**不含任务名**）；`BattleManager.ResolveBattleSeed()` 供 `BattleRandom` 与地形；`GenerateNoiseTerrain` **两条派生流**（`Terrain`/`TerrainDecor`）+ 8 处换流（原 `DateTime 半小时` 种子与 `UnityEngine.Random` 全清）+ `PickRockCoverEntry` 加 `System.Random` 形参 + **NavMesh 等待不再"超时即放弃"**（改用例：`isDone` 一直等 + 失败 `LogError`）；`WaveManager:63` 换 `BattleRandom`；`CreatEnemy:69` 改判 `BattleState.IsStartBattle`；`KeyScreenControl` 谜题改**纯函数** `PuzzleRange(seed,PuzzleId,stage,index)`（`PuzzleId` = 世界坐标 0.1m 量化哈希；**用纯函数而非流是因为阶段可能被重试**）；`RoomMsg.StartGameNtf` 追加 `TaskFingerprint`/`MatchId`；`NetHostSvc` 加 `_rosterFrozen`（**不复用 `_started`**，`StartGame` 里"先放行→广播名单→再冻结"）+ `_matchId` 自增；`TeamNetBridge.HandleStartGame` 重写为"幂等→校验→指纹→`SetSeed`+`SetTask(seed)`→加载 BattleScene+`Creat(true)`"，`HandlePlayerList` 加"开局后只更新字段不重排"门（`ApplyListFieldsOnly`）；`SelectMapWnd.StartTask` 种子传 0 回落再读回广播。
- ⚠ **场景改名的坑（实测）**：`AssetDatabase.MoveAsset("Assets/Scene/TestScene.unity","Assets/Scene/BattleScene.unity")` + 同名目录一起改名，**GUID 保持不变**（`061f6d44…`）✓，但 **MoveAsset 只改内存**，磁盘 `ProjectSettings/EditorBuildSettings.asset` 的 `path` 仍是旧值 ⇒ 必须再 `AssetDatabase.SaveAssets()` + `EditorApplication.ExecuteMenuItem("File/Save Project")` 才落盘。
- ✅ **远程玩家（Friend）位姿同步已实现（2026-10-06）**：`CmdId` 新增 **5000 段**（`PlayerTransformUp=5001` 成员→房主 / `TransformBatchSync=5002` 房主→全体）；新增 `NetTmp/Services/Msg/BattleMsg.cs`（`PoseSnapshot`/`PoseBatchMsg`）、`NetTmp/Client/NetTransformFlow.cs`（编排：成员 20Hz 上行 → 房主**聚合** → 15Hz 批次下行 + `MessageCenter.Dispatch` 本地自派发，与 `BroadcastPlayerList` 同款）、`06Gameplay/Player/Controller/FriendController.cs`、`09Manager/Global/NetFriendBridge.cs`（名单增删 + 上行采集 + 下行应用 + 换场景按缓存名单重建 + 角色身体 `StudentModle/<RoleName>`）；`NetHostSvc` 加位姿聚合表 `_poses` 与 `BroadcastPoses`；`NetRoomFlow` 加 `CurrentMatchId`。**预制体**：新建 `Assets/Resources/Prefabs/BattleBase/PlayerFriend.prefab`（`Actor` **type=2(Friend)** + `FriendController` + `ModelSocket`/`AimPoint`，**无相机/无 AudioListener/无 PlayerController**）；`NetTransformFlow`+`NetFriendBridge`（含 `friendPrefab` 引用）已加到 `GameRoot.prefab`（场景实例自动同步）。
- ⚠ **盟友"闪现位移"的真因 ＝ 插值缓冲只有两个点**（2026-10-06 修，数值仿真证明）：`FriendController` 的 `RenderDelay(0.15s)` 远大于快照间隔（房主下行 15Hz = 67ms）⇒ 渲染时刻**永远落在最新一段的左端之前**，插值参数被夹到 0 ⇒ 退化成"每个包跳一下"（旧算法每帧最大位移 = 一包距离 **0.533m**；新算法 = 8/60 = **0.133m**，平均都 0.132 ⇒ 只改抖动不改总量）。现改为**多点缓冲**（`BufferSize=6` 的 `_bufPos/_bufYaw/_bufTime`）：`UpdateRenderPose` 先丢"整段都在渲染时刻之前"的老点、再在 `[t0,t1]` 内插值，断流则停在最后已知位姿。⚠ 两条别写错：① 缓冲容量必须 ≥ `RenderDelay/包间隔 + 2`（`Awake` 里 `Mathf.Max(4, BufferSize)` 兜底）；② **吸附判定要和"上一个快照"比**，旧代码和"当前 transform"比 ⇒ 渲染滞后 1~3m 会把正常跑动误判成传送、频繁清缓冲。
- ⚠ **强杀客户端 ⇒ 房主端盟友不清（已修）**：UDP 无连接，成员关进程后房主收不到"我走了"，库内 `TimeoutMs` 默认 15000 也要等 15s；而 `PingReqMsg/PingRspMsg`（`CmdId.PingReq/Rsp = 1001/1002`）**早就定义了但从没被使用**（没人心跳）⇒ 舰桥阶段两端都没业务报文时，健康的连接还可能被库的超时检查误杀。现补：`NetSvc` 每 2s `SendHeartbeat()`（并注册 `PingRsp` 算 `RttMs`）+ `NetHostSvc` 回 Pong、`Update()` 出队每条消息时刷新 `_lastSeen`、`EvictIdleMembers()`（`memberIdleTimeout=8s`）按"离开"清表 + 广播名单 + `CloseSession()`；⚠ 顺手补了 `_poses.Remove(sid)`（原来成员离开不清位姿表 ⇒ "幽灵位姿"会一直跟着批次下发）。
- 🔴 **远程玩家不能"运行时改造 Player 实例"**（实测两条硬理由）：①`Actor.Type` **只读**；②`Actor.WaitSetPos` 在 `Awake` 里**同步**派发 `UnitEventSub.PlayerCreate`（舰桥阶段 `IsMainStage()` 恒 true ⇒ 协程第一段不含 yield 直接跑完）⇒ 必须**预制体上**就设 `type=Friend`。且 `Player.prefab` 带 **3 相机（含 MainCamera）+ 2 AudioListener** ⇒ 复用会打架。⇒ 用"轻量专用预制体 + 运行时挂 `StudentModle` 身体"（学生模型根无 `Actor` ✓ 不会重复注册）。
- ⚠ **并行编辑警告（实测）**：执行期间 `10_Effect/CreatEnemy.cs` 被外部编辑器保存覆盖（改动被还原 + 多出一行未写完的 `PEVector3` 导致 `CS1002` 阻塞全工程）。⇒ 让 AI 改文件时不要同时打开同一文件编辑。：①**场景名不下发**，把 `"TestScene"` 统一改名 `"BattleScene"` —— 全仓只有 3 处命中：`EditorBuildSettings.asset:15`（**别手改**，Unity 重命名资产会自动更新 path）、`TransSceneController.cs:24`（唯一代码触点，抽常量 `BattleSceneName`）、`ModifyTerrain.cs:128`（仅注释）；资产侧重命名 `Assets/Scene/TestScene.unity` + **同名目录**（`Assets/Scene/TestScene/` 是按场景名约定的烘焙资产目录，含 `NavMesh-MapRoot.asset` 等，GUID 引用自动跟随）。②⚠ **"禁止进人就不会重排名单"不成立**：`NetHostSvc` 的 4 处名单广播里，`OnJoinRoomReq`/`OnReadyState`/`OnPlayerProfileNtf` 确实只在舰桥，但 **`OnMemberLeft`（`:274-286`）/`OnLeaveRoomNtf`（`:336-344`）在战斗中照样触发** ⇒ 列表会**少一个人**，破坏 `ArmamentWnd` 的 `armamentRoot.GetChild(i)`↔`players[i]` 假设，且 `task.BattleData` 是开局按 `players.Count` 建的（`TaskManager.cs:393-397`）、`AddBattleDataItem(playerIndex,…)` 按下标写 ⇒ 统计错位。⇒ **名单门要做**（成员 `HandlePlayerList` 在 `IsStartBattle` 后只按 id 更新字段；房主 `BroadcastPlayerList` 入口冻结）。③**用显式 `MatchId`**（房主自增）⇒ 免除"回大厅清键"（seed 会撞车），且是后续快照带局标识的基础。⚠ **冻结必须"每局放行"**：`StartGame()` 里"先放行→广播名单→再冻结"，**别复用 `_started`**（它还有 `RoomMeta.InGame` 语义）⇒ 用独立 `_rosterFrozen`。
- ⭐ **成员进战斗场景链路方案已出**：`.codebuddy/plans/联机_成员进战斗场景链路_方案.md`（第 0 步前置，对应 `NetRoomFlow.cs:299-308` 的 TODO）。房主参照链路：`SelectMapWnd.StartTask` → `SetTask`(→`GameState=Ready`) → **大厅里的 UnityEvent**（`Utnapishitim.unity:15599-15602`）调 `TransSceneController.StartLoad()`（`TransSceneController.cs:20`）→ `AsyncLoadScene("TestScene", ()=>BattleManager.Creat(true), true)`。成员要做的是同构三步：校验(含指纹) → `SetSeed` → `SetTask(…,seed)` → `AsyncLoadScene(ntf.SceneName, …)`。三个关键点：①场景名硬编码 `"TestScene"`（`:24`）⇒ 建议 `StartGameNtf` 追加 `SceneName`；②⚠⚠ **开局后必须停止 `ReplacePlayers` 重排名单**（`TeamNetBridge.HandlePlayerList:103` 整表替换+`Reindex`，而 `ArmamentWnd` 用 `armamentRoot.GetChild(i)`↔`players[i]`、`BattleManager`/`GameEndWnd` 按下标；战斗中 `PlayerListSync`（`NetHostSvc.cs:358/385` 准备/资料变更就广播）会让下标漂移）⇒ 加门 `if (BattleState.IsStartBattle) 只更新字段不重建`；③**名单就绪用 KCP 有序免费解决**：`NetHostSvc.StartGame()` 改成**先 `BroadcastPlayerList()` 再 `SendToAll(StartGameNtf)`**。另：重复收到 `StartGameNtf` 需幂等（否则两个 `BattleManager`），用 `ntf.Seed` 做 `_startedMatchKey`，⚠ 回大厅要清；`SetTask` 在大厅的副作用（`Ready` + `CreatCountDown` + `BridgeRoleManager.cs:76-80` 移人）与房主对称，先接受。
- ⭐ **改造方案已出**：`.codebuddy/plans/联机_种子同步与初始化确定性_改造方案.md`（Ⅰ `SeedUtil`/Ⅱ A2 TaskManager 三合一/Ⅲ A1 `TaskState.Seed` 单点/Ⅳ A3 地形派生流 + NavMesh 不超时放弃/Ⅴ `WaveManager:63`/Ⅵ `CreatEnemy:69`/Ⅶ 谜题/Ⅷ 指纹；共 11 处改动 10 个文件）。关键设计：`SetTask(…, int seed = 0)` 用默认值**避免改 5 个调用点**；`ApplyFractalNoiseToTerrain` 末尾追加 `int seed = 0` 同理；`TaskFingerprint` **只用整数 FNV 混合、不含 name**；`InitTerrain` 被 `BattleManager.cs:168` `yield return` ⇒ NavMesh 一旦"不超时放弃"就自动成为窗口结束的前置条件。
- ⭐ **A5 排序键定稿（2026-10-06）：用 `PEVector3`，别用浮点**。`PEVector3` 是定点结构（3×`PEInt`），`PEVector3(Vector3)` 构造会量化（`PEVector3.cs:86-91`）⇒ **天然抗浮点抖动**；库里已有权威整数出口 **`CoverLongArray()` → `long[3]{x.ScaledValue,y.ScaledValue,z.ScaledValue}`**（`:146-149`）⇒ 直接拿来做排序键。⚠ 别用 `PEVector3.GetHashCode()`（= `x+y+z`，`:166-169`，**加法可交换 ⇒ 碰撞极多**）；别用 `RawVector3Int`/`RawInt`（米级太粗）。prefab 身份**别用 `name.GetHashCode()`**（`string.GetHashCode()` 在 .NET Core/5+ **随机化**，跨进程不同）⇒ 用整数 prefabId（配置索引/枚举）或 FNV-1a；且必须用资产名而非带 `(Clone)` 的实例名。键 = `(Mix(CoverLongArray), prefabId, 层级路径哈希可选)`。
- ⭐ **A5 补充结论**：①**"顺序由代码固定"的不需要排序** —— `MissionController` 协程生成的任务点/兴趣点/场景点（`:171/309/326`）顺序天然确定；只需给 `CreatOOPart/CreateSupple/CreateBuilding` 这 3 类"场景预置组件自发 `EnqueueInit`"的对象排序。②❌ **不要重置 `GlobalIndexID`**：**大厅里有存活 Actor**（`RoleManagerBase.Start:34` Instantiate `Prefabs/BattleBase/Player` → 带 `Actor`；`BridgeRoleManager.Start:31-59` 还建展示模型）⇒ 重置后新 Actor 会与大厅那批**撞号**，而 `Actor.Equals`/`GetHashCode` 正按 `IndexID` 比较（`:109/113-116`）⇒ Dictionary/HashSet 互相顶掉。且无动机（用 `NetId` 后 `IndexID` 只需本地唯一）。③最终口径：静态布局一致 = 排序 + 确定性流；`IndexID` 跨端一致 = 做不到也不必要。
- ⭐ **A3 地形决策（用户否掉"走场景烘焙地形"）**：`InitSpecial` 只服务新手任务，正常流程是**创建式地图** ⇒ 地形必须同步。做法 = **从任务种子派生两条独立流**（`Derive(seed,id)=seed*31+id`）：`terrainRandom`（高度图）+ `decorRandom`（覆盖石/树/石块）；入口 `ApplyFractalNoiseToTerrain(...)`（`GenerateNoiseTerrain.cs:843-846`）**加 `int seed` 形参**，调用点 `BattleManager.InitTerrain()`（`:255-264`）。8 处要换：`:997-999`（原 DateTime 半小时种）、`:1056-1058`、`:1569`、`:1584/1585/1586`、`:1589`、`:1831/1832`、`:1857`、`:1911`；草 `:1659/1661/1662` 保留 `UnityEngine.Random`。⚠ 残留：NavMesh 烘焙（`:946/950`）跨端非逐位一致（输入一致时约厘米级）；`:951-955` 等待有 10s 超时 ⇒ 应把"NavMesh 完成"纳入窗口结束条件。
- ⭐ **A5 决策**：`CreatEnemy.cs:69` 开关条件 `GameState==Game` → **`BattleState.IsStartBattle`**。**谜题决策**：`KeyScreenControl` 4 处（`:65/69`、`:236-239`、`:333-334`、`:518`）改派生流，`puzzleId` 用世界坐标量化哈希/场景路径哈希（**不能用** `Furniture_Attached.NumberID`——静态自增各端不同）。
- 🔴 **随机源审计的 3 个地基级发现（2026-10-06，清单见 `.codebuddy/plans/联机_随机源审计清单.md`）**：①**`BattleRandom` 的种子根本没下发**——它吃 `taskCfg.seed`（`BattleManager.cs:111/164`），而该字段是本地在 `CreatAllTask` 用 `TaskRandom`（DateTime **2 分钟窗口**，`TaskManager.cs:180`）生成的，`SetTask`（`:382-402`）根本不碰 seed；下发的 `StartGameNtf.Seed` 只进 `TeamManager.SetSeed`→`RandomUtils` ⇒ **两端 `BattleRandom` 是两条线**。②**整张 `TaskCfgs` 也是本地时间生成的**（`TaskManager.cs:176-219`，含 `main/extra/nestCount/terrainType`）⇒ 同一个 `TaskIndex` 可能指向完全不同的任务，建议**随开局下发"选中项 TaskCfg"**。③**地形不能靠种子**：offset 种子=墙钟半小时窗口（`GenerateNoiseTerrain.cs:997/1056`）、植被/覆盖石=`UnityEngine.Random`，而**覆盖石参与 NavMesh 烘焙**（`:1818-1819/1862`）、树有碰撞体 ⇒ 推荐**联机统一走 `InitSpecial()` 的"场景烘焙地形 + 烘焙 NavMesh"路径**（`Teach.unity` 已是此形态）。
- ⚠ **窗口内唯一真实插队点**：`BattleManager.cs:205` `GameState=Game` → `:206` yield → `:208` `DrainInitQueue()`，间隙里 `CreatEnemy.Update`（`CreatEnemy.cs:69`）会先消费 `BattleRandom`；另 `WaveManager.cs:63` `cfg.templates.RandomTake()` 是**无参重载=静态流**却在抽"整场敌人构成的波次模板"（窗口内、必须改 `BattleRandom`）。
- ✅ **两条流要分开评估**：`BattleRandom`（每局独立实例）窗口内消费点少且集中、接近可控；真正不可控的是 **`RandomUtils` 静态流**（被音效 `SoundGroup_SO:68/74`、弹孔 `FpsHelper_Hit:342`、武器 `WeaponBaseController:456`、谜题 `KeyScreenControl:65-518` 等跨系统推进游标）。⇒ **必须一致的运行期随机（谜题序列）应派生独立流**（`new System.Random(hash(seed,puzzleId))`），这是最便宜的立刻见效改造。
- ⭐ **本局配置「以内容为准」+ 刷新门放宽（2026-10-06 实现，5 文件）**：①刷新门从"仅 Bridge"改为"**只在 `GameState == Game` 时不刷**"（`TaskManager.Update`）——放宽安全，因为 **`TaskCfg` 是 struct**、`SetTask` 里是**值拷贝**进 `nowTask`（`06Gameplay/Events/TaskData.cs:112`），而全仓 `TaskCfgs` 的消费点只有 `SelectMapWnd`（选图界面）+ TaskManager 自己，战斗/其它 UI 读的都是 `nowTask`。②`TaskConfirmNtf` 新增 `[Key(8)] TaskCfgDto Cfg`（**全 int/int[]/float/bool/string** —— `02_Net` references 为空，看不见 `TaskCfg`/`MissionEnum`）；`TaskManager.ToDto/FromDto` 做转换；`SetTask(..., TaskCfgDto remoteCfg = null)` **有内容就以内容为准、不查本地表** ⇒ 本局配置就此**冻结**：后进者即使本地表里已经没有这个任务也能照打（`NetHostSvc._lastTaskConfirm` 补发时自动带内容）。③`TeamNetBridge.HandleTaskConfirm` 的**指纹从硬校验降级为诊断告警**（有内容时），否则后进者/跨窗口玩家被永久拒之门外；旧版房主（无内容）仍保留硬校验。④`SelectMapWnd.ConfirmTask` 传 `TaskManager.ToDto(taskManager.nowTask.taskCfg)`。**连带收益：A2 跨窗口不一致对玩法不再致命。**
- ⭐ **为什么"同种子确定性"在本项目必然失败（2026-10-06 深挖，结论已写进计划 §1）**：**不是随机的问题，是驱动源的问题**。三层：**L1 随机**（全进程唯一静态流 `RandomUtils.cs:14`，被密码锁 `KeyScreenControl:65-69`/采集物/武器/掉落共同消费；`UnityEngine.Random` **完全没接种子**——武器散布 `WeaponBaseController:591` 等每次开火都消费；`CreatEnemy:80` 帧驱动）；**L2 时序**（`BaseSelfMoveableController.cs:536/587-611` 在定点量上做 `Time.deltaTime` 积分；AI 在 `Update`、逻辑帧在 `NetManager.Update` 两套时序）；**L3 引擎**（`PathRequestManager.cs:91` 只转发 `SetDestination`，真移动/避障在 Unity 内部 ⇒ 同目标点两台机器也有微差，微差→路径重算→**指数发散**）⇒ L3 几乎不可修。
- ⭐ **项目已有"确定性半成品"**（所以是"没走完"而非"走不通"）：50Hz 逻辑帧宿主 `NetManager`；**定点数学 `Assets/Plugins/PEMaths/PEMaths.dll`**（`PEInt`/`PEVector2/3`，`PEInt.RawFloat` 文档"转换完成后不可再参与逻辑运算"）；定点**已在玩法核心**：速度 `BaseSelfMoveableController.cs:107/766/772`、伤害 `FpsHelper_Hit.cs:59/203/225/373`、空间查询 `UnitQuery.FindUnits(PECircle)`、`Actor.LogicPos`；作者心迹 `WeatherSystem.cs:41`、`KeyScreenControl.cs:67`。**决策 = 走"档 B 局部确定性"**（只保留"只跑一次"的初始化层：地形/静态布局/任务点/建筑/采集物/初始波次/开局天气/密码锁），动态物全走快照；**档 A 全量 lockstep 的验收判据 = 同种子同输入 3000 帧后所有 `LogicPos` 逐位一致**，需重写 7 项，4 人局域网不值得。**半确定性比不确定性更危险**（"偶尔不一致"最难查）⇒ 局部确定性必须限制在边界清晰、只跑一次的区间。便宜的立刻见效改造：必须一致的用途**派生独立随机流**（`new System.Random(seed*31+purposeId)`），切断互相偷随机。
- ⭐ **`NetManager` 是现成的 50Hz 逻辑帧宿主，就是为联机准备的**（2026-10-06 发现）：`09Manager/Global/NetManager.cs:20`（挂 GameRoot、实现 `I_GlobaManager`/`ILogicFrameSink`），步长 `Constants.LoginFrame = 20ms`，宿主经 `LogicFrame.Sink` 发布给 `LogicBehaviour.LogicTick` 的 26 个子类。其注释原文："本类最初是为联机准备的……将来真做联机时只需把帧来源从本地时钟换成网络帧"。⇒ **战斗同步的 tick 复用它，不要新造一个。**
- ⭐ **战斗同步的选型已定（2026-10-06 出计划）**：**不做帧同步/确定性重演**，做「房主权威 + 事件驱动生命周期 + 10Hz 快照插值」。原因：`BattleRandom`（每局一实例，`BattleManager.cs:111/164`）与 `RandomUtils`（**全进程静态共享**，`RandomUtils.cs:14`）的消费时机由帧驱动（`CreatEnemy.cs:80` 每帧 `Bool`；`WaveBase.cs:50` 用静态随机二次造波次种子）⇒ 两端消费次数必然分叉。种子只能管"初始静态布局"。另：`Actor.IndexID`（`Actor.cs:24/206`）是各端自增计数器，**不能当网络 ID**，须房主分配 `NetId = (sid<<24)|seq`。
- **程序集方向（战斗同步相关，别搞反）**：`02_Net` refs=`[]` ⇒ 看不见 `05_UnitCore`/`06_Gameplay`（只能碰 `GameObject/Transform/基础类型`）；`09_Managers` 同时可见二者 + `02_Net` ⇒ **桥放 `09Manager/Global/NetBattleBridge.cs`**；而 `10_Effect`（`CreatEnemy/CreatOOPart/CreateSupple/CreateBuilding`）与 `06_Gameplay`（`EnemyNestBuild`/技能）**看不见 02_Net** ⇒ 「成员端跳过本地生成」的生成门**必须走 `01_GameContract` 接口**（同 `IBridgeArmamentSink` 模式）。
- 位置写回 API 的坑：`AIController.Pos` setter（`AIController.cs:79-85`）在 `EnemyController` 里被 override 成 `NavMeshAgent.Warp`（`:43-51`），**agent 被禁用/离网格时 Warp 返回 false 且不生效** ⇒ 远端实体应关 agent 后直写 transform。玩家侧用 `BaseSelfMoveableController.Move(pos, isTeleport:true)`（`:438-441`）→ `FpsHelper_Controller.Teleport`。远端实体要关的不止 AI：`EnemyController.Update`、`StateMachineFrame.Update`、`NavMeshAgent` 三处都会写 transform。KCP 只有可靠有序通道 ⇒ 快照必须在应用层丢旧 tick；高频快照建议手写 `byte[]` 而非 MessagePack（压缩成本；AOT 风险已由 `NetMsgCodec` 解决）。详见 `.codebuddy/plans/联机_关键物体位置同步_计划.md`。
- ✅ **`BridgeSys` 不挂 GameRoot 是对的**（2026-10-06 用户质疑后查证）：`BridgeSys` 与 `ArmamentWnd.prefab` 的实例**都只在 `Utnapishitim.unity`**（guid `fb33dc72…` / `19c30446…` 全仓交叉验证），三条链唯一触发点就是 `ArmamentWnd` 的点击，而 `ArmamentWnd.Init()` 又要写 `BridgeSys.Instance.armament` ⇒ 同生共死；挂常驻只会与其构成 `SingletonNet` 重复（重复实例会 Destroy 整个 GameObject）。**代价 = 跨场景调用点必须判空**。战备/强化的**数据落点已从 `ArmamentWnd.Receive*` 上移到常驻的 `TeamNetBridge`**（先写 `TeamManager.players[i]`，再 `BridgeSys.Instance?.Receive*` 叫醒界面），否则"不在舰桥时收到的同步"会被静默丢弃。
- ⭐ **三大事件总线「同步覆盖」审计结论（2026-10-08，只读调研，未改码）**：现有桥只有 5 处（`NetFriendBridge`/`EnemyNetBridge`/`TeamNetBridge`/`SceneUnitMoveSink`/`WaveManager`），覆盖「玩家表现 + 敌人 + 波次 + 场景单位 + 舰桥流程」；**「任务系统 / 战局结果 / 共享世界物件」三类完全没桥**，`CmdId` 也无对应消息号（只用到 4001~4032+5001/5002）。
  **BattleEventBus 缺**：`OnMissionStart/Completed/Fail/End/Update/StateChange`(65-87)、`OnMissionEntityShow`(90)、`OnEvacuate`(96)、`OnWipeFailCountdown/Cancel`(104/108)、`OnEndGame`(118)、`OnSubmitOOPart`(122)、`OnRevealAllMissions`(126)、`OnRequestAuthorize`/`OnAuthorizeAirdrop`(138/23)、`OnCallKai`(17)＝凯伊（详见 10-08 daily「凯伊有没有同步」）、`OnCancelAirdrop`（取消战备无消息）。
  **GlobalEventBus 缺**：`OnFurnitureOperate`(139,共享家具/密码锁/雷达站=任务推进触发点)、`OnOOPartCollect`(151,`BattleManager` 订阅被注释)、`OnKeiSubmit`(158)、`OnMark`(115,标记点位)、`OnPlayMeetSpeech`(165,只桥了 `OnPlayerSpeech` 而非这条)、`OnDaySwitch`(48,待评估)、`OnRequestGameState` 仅 Transition 已做。
  **UnitEventBus 缺**：`OnSpecUnitCreate/Dead`(84/87)、非敌人单位的 `OnUnitDeath`/`OnUnitKill`（敌人已由 4026 覆盖）、`OnNoise`(53,低优先)。
  已实现：`OnPlayerSpeech`、`OnEnemyMove/Hit/DiedRemote`、`OnSceneUnitMove`、`OnAirdrop`(=AirdropCall)、`OnPlayerCreate/FriendCreate/FriendRoleChanged/FriendLeave`、`OnPlayerDead/Revive`(Vital 镜像)、`OnSwitchRole`(profile)。
  建议优先级：**P0** 结算/团灭/撤离（`OnEndGame`/`OnWipeFail*`/`OnEvacuate`）；**P1** 任务组+Reveal；**P2** 共享世界物件（Furniture/OOPart/KeiSubmit）；**P3** 表现（Mark/Speech/Authorize/CallKai）。最小动作＝新增 `CmdId` 4033+ 段 + DTO + `NetMissionBridge`（09 层）。
- ⭐ **`02_Net` 已收窄为纯传输、游戏协议独立成 `07_NetGame`（2026-10-08 落地，编译+反射双证）**：
  `02_Net`(7 文件) = `ClientSession`/`HostSession`/`MessageCenter`/`NetMsgCodec`/`NetInbox`(新)/`NetCmdId`(新)/`Msg/PingMsg`；
  `07_NetGame`(8 文件, `references:["02_Net"]`) = `NetSvc`/`NetHostSvc`/`NetRoomFlow`/`NetTransformFlow`/`RoomMeta`/`CmdId`/`Msg/{RoomMsg,BattleMsg}`；删 `LoginMsg`/`ChatMsg`（全仓 0 消费方）。
  ⚠ 关键手法：原先 `ClientSession→NetSvc.Instance.AddMsgQue`、`HostSession→NetHostSvc.Instance.AddMsgQue`（会话回调游戏层）⇒ 会话若留 02、Service 进 07 就**成环**；改走 `02_Net/NetInbox`（线程安全入站队列，07 主线程泵）打断。
  ⚠ ping 属传输控制 ⇒ 迁 `NetCmdId`(1001/1002)，从 `CmdId` 移除；`CmdId` 其余值**全部不变**（保线兼容）。
  ⚠ 移动 `.cs` 用 MCP `AssetDatabase.MoveAsset`（保 GUID ⇒ `GameRoot.prefab` 上 `NetSvc/NetHostSvc/NetRoomFlow/NetTransformFlow` 组件引用不受影响；`find_dangling_guids.py` 脚本悬空=0）。
  ⚠ 桥**不搬**：`NetFriendBridge` 用 `ResSvc`(09) ⇒ 搬进 07 会 `09↔07` 成环，全留 09。
  ⚠ 遗留（二期）：socket 仍在 07 的 `NetSvc/NetHostSvc` ⇒ 可选把 `ClientNet`/`HostNet` 纯类下沉到 02；`LanRoomInfo` 由 KCPNet.dll 固定、字段含游戏语义（Difficulty/TaskMain/InGame）⇒ 传输层无法 100% 游戏无关。
- ⭐⭐ **二期落地（2026-10-08）：连接/会话也进 02 ⇒ `02_Net` = 传输全套**（编译+反射双证，**尚未实测联机**）：
  `02_Net` 新增两个纯类 —— `Transport/NetClient.cs`（`ConnectToRoom`/`Disconnect`/`Send`/`IsConnected`/心跳+RTT/`Pump`）与 `Transport/NetServer.cs`（`Start`/`Stop`/`SendToAll`/`SendTo`/`CloseSession`/`CurrentSid`/`OnConnected`/`OnDisconnected(sid,selfClosed)`/`LastSeen`/`Touch`/心跳应答/`Pump`）；共 **9 文件**、`references` 仍 `[]`。
  `07_NetGame` 的 `NetSvc`/`NetHostSvc` **保类名保 GUID 仍在 `GameRoot.prefab` 上**，只变成"持一个纯类 + 每帧 `Pump()`" ⇒ **零 prefab 改动**；`NetHostSvc` 只剩房间语义，`CurrentSid`/`SendToAll`/`SendToSession` 改委托。
  ⚠ 行为等价关键点：① 存活表 `_lastSeen` + "主动关闭登记表 `_selfClosed`" 搬进 `NetServer`，`OnDisconnected(sid, selfClosed)` 把"是不是我自己关的"带回 07（保日志去重口径）；② **空闲踢人仍在 07**（只有它知道"谁是玩家"）—— 07 遍历 `_players` 问 `NetServer.LastSeen(sid)`，再 `CloseSession`；③ 心跳应答（必须对任何会话都回）搬进 `NetServer`；④ `Update` 顺序仍是「会话事件 → 分发 → 踢人 → 位姿广播」。
  ⚠ 待办：**必须运行期实测**（开房/入房/名册准备/进战斗/强杀踢人/心跳不误杀）—— 本次只过了编译与反射。
- ⭐ **玩法层"未同步清单 + 同步方案"已定稿（2026-10-08）→ `.codebuddy/plans/联机_未同步清单与同步方案.md`**。
  一句话版：`CmdId` **4033 `GameOverNtf` / 4034 `EvacuateNtf` / 4035 `MissionUpdateNtf`(Key=`MissionBase.netOrder`) / 4036 `FurnitureOperateNtf`(键=`FNV1a(Id+位置量化)`) / 4037 `MarkNtf` / 4038 `CallKaiNtf`**（DTO 在 `07NetGame/Msg/SyncMsg.cs`，已编译通过、桥与玩法侧改动待做）。
  桥 = **每局静态桥**（同 `EnemyNetBridge`），安装点 `WaveManager.Awake` ⇒ 零 prefab 改动。
  三个易踩结论：① **hash 不产生唯一性** ⇒ 家具同 `Id` 多实例不能用 `Id`/`FNV(Id)`，要 `Id+位置`，且**键要创建时缓存**（家具会位移）；`NumberID` 不可用（`nowID` 自增不复位）。② 家具远端重放**不需要新事件总线**：`PlayerInputHandler/PlayerWeaponsManager` 的 `OnOperation` 自带 `user == gameObject` 过滤 ⇒ 远端传 Friend/null 即早退；`KeyScreenControl` 无过滤正是要处理的。③ `BattleManager.EndGame` 是"延迟加载场景" ⇒ 成员端二次调用会排**两个 timer**，必须加"本局只结束一次"门。
  不同步（已拍板）：团灭倒计时（本地判定）、战备授权（每人独立）、UI/输入/本机视角。
  追加：**4039 `WaveCenterMsg`** —— 波次 center 改"**房主 ~2Hz 周期下发**"（客户端只插值跟随）⇒ 判定只存在一处、翻边风险 0（否掉了"客户端本地算最近玩家 + eps"的方案）。创建位置核查：玩家出生点两端一致（`BattleManager` 侧 `Medivac` 固定变换 `TransformPoint(0,-4,6)`），盟友出生点不一致但被**首条位姿 `Teleport`** 吸附纠正。
  ⭐ **P0 已落地（2026-10-08）**：`09Manager/Battle/NetGameFlowBridge.cs`（静态桥，`WaveManager.Awake` Install ⇒ 零 prefab 改动）接 4033/4034；`BattleManager._gameOverRequested` 门（**只挡第二个 timer，`nowTask.result` 仍可补正**）；`NetRoomFlow` 加 `SendGameOver/SendEvacuate` + `OnGameOver/OnEvacuate`。编译+反射验证通过，**运行期实测待做**。
  ⚠ 通用坑（P0 已验证、后续照抄）：远端应用时桥会**直接派发本桥也订阅的玩法事件** ⇒ 必须有 `_applyingRemote` **回环门**，否则把远端的当成自己产生的再上报一次。
  ⭐ **P1/P2/P3 已全部落地（2026-10-08，编译+反射验证；运行期实测待做）**：
  - **P1 任务(4035)**：键 `MissionBase.netOrder`（`MissionController` 赋值；数据生成模式按 `missions.Count`，**场景模式按 0.1m 量化位置排序**因为 `FindObjectsByType` 顺序两端不保证）；消息**必须带 State**（完成有"进度/事件触发"两个出口）；成员端靠 `MissionBase.RemoteDriven` 门"不自判"（⚠ `Uninstall` 要复位）。桥 = `NetMissionBridge`。
  - **P2 家具(4036)**：键 `Furniture_Attached.SyncId = FNV1a(Id + 位置0.1m)`（`Awake` 算一次缓存；`Id` 不唯一、`NumberID` 各端自增 ⇒ 都不能用）；**一处收口** —— 远端重放 `ApplyRemoteOperate(remoteUser)` 跑同一条 `Operate()` ⇒ `OnOOPartCollect`/`OnSubmitOOPart`/`OnKeiSubmit` **不需单独同步**；⚠ 操作者只能传**远端单位**（`PlayerInputHandler/PlayerWeaponsManager.OnOperation` 靠 `user == gameObject` 过滤）。桥 = `NetFurnitureBridge`。
  - **P3 表现(4037/4038)**：桥 = `NetActionBridge`；`Mark` 只传点（目标实体是引用过不了网）；`CallKai` 的 `SpecUnitKei.OnCall` 不按 source 过滤。
  - ⭐ **纠正审计**：`OnPlayMeetSpeech` **不需要收口** —— `PlayerSpeechManager.OnMeetSpeech → Speech() → BattleEventBus.PlayerSpeech` 本来就是已同步的那条链（早前写的"只桥了 `OnPlayerSpeech`（部分缺口）"是错的）。
  - ⚠ 踩点：`MissionController.cs` 是 **CRLF**，其余 P1-P3 文件是 LF ⇒ 批量改写脚本必须做**行尾自适应**（否则 `HIT=0` 静默漏改）。
  - ⭐ **波次中心(4039) / 昼夜墙钟 / SyncedNavMover 也已落地（2026-10-08）**：
    - **4039 波次中心**：追击波（`centerGetter != null`）**只有房主判定**"离中心最近的玩家"（各端自己算会因位姿延迟**翻边**）；房主在 `centerGetter` 外**包一层**按 ~2Hz 下发（`CenterSendInterval=0.5s`），成员端 `SmoothRemoteCenter` 用 `MoveTowards`（24 m/s）插值跟随，**按 `WaveIndex` 丢过期包**。
    - **昼夜墙钟**：`TimeProgressionModule.Tick` **不再累加 `deltaTime`**，角度 = 纯函数 `AngleAt(state, WallClockSeconds())`（分段：前 2/3 白天 0→180°、后 1/3 夜晚 180→360°）⇒ 各端不再随运行时长漂移。⚠ 起点映射与旧版略有不同（最多差 60°）、且不再受 `timeScale` 影响；⚠ 前提是两端系统时钟对齐。它属 **`DayNightSystem` 独立 asmdef**。
    - **`SyncedNavMover`**（`06Gameplay/AI/`，`FPSGame.AI`）：把 `EnemyController` 里"缓存远端点 + agent 可用才落地 + 每帧重试 + 绕开本地去重/节流"抽成组件；落地方式**注入**（`applyHandler`，敌人注入 `TryRequestPath` 走 `UnitEventBus.PathRequest`）。**纯结构收敛、对外行为与消息不变**。⚠ `EnemyController` 是 `partial` ⇒ 动它的私有成员前必须先全仓搜一遍（本次已做）。
    - ⭐ **已接入三处**（2026-10-08）：`EnemyController`（注入 `TryRequestPath`）、`NPCWalk`（**故意不注入** —— 它不能走 `PathRequestManager`：那是战场 `BattleManager` 创建的，大厅里没人订阅 ⇒ 请求被静默丢弃）、`SpecUnitController`（凯伊；获得"缓存+重试"⇒ 修掉 4038 重放时"凯伊还没 Warp 上网格 ⇒ 这次呼叫就没了"）。默认落地判据含 `!isOnNavMesh` 就等（`SetDestination` 在网格外会报错）。

