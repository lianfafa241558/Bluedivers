# -*- coding: utf-8 -*-
"""
把 2026-10-11 联机同步批次的经验落进 skill `kcpnet-online`（用户拍板）。

改动清单：
  SKILL.md
    1) §B 去掉已过期的"#T=/#难度 串房间名"约定（库已有真字段）
    2) 库 API 速查表 4 行：KCPNet.ClientId / KCPSession 心跳 / NetConfig 两个常量 / LanRoomInfo 13 字段
    3) §A 工作流：从"四步"扩成"六步 + 验证"
    4) 新增 §E「同步一条新东西」决策树 + 「诊断日志 NetSyncLog」一节
    5) 踩坑清单补 6 条（回环门共享 / 房主自派发 / 成员不得本地生成 / 本机视角 / 纯函数随机 / 两种编译假象）
  references/nettmp-layer.md
    6) 命令号表补 4041–4049；SyncMsg 的 DTO 清单补新名；§6 整节改写为"薄适配层"
  references/kcpnet-api.md
    7) ClientId / HeartbeatIntervalMs / NetConfig 常量 / LanRoomInfo 13 字段 / 来源日期

用法：python .codebuddy/plans/skill_kcpnet_update.py [--apply]
"""
import io
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

SKILL = '.codebuddy/skills/kcpnet-online/SKILL.md'
LAYER = '.codebuddy/skills/kcpnet-online/references/nettmp-layer.md'
API = '.codebuddy/skills/kcpnet-online/references/kcpnet-api.md'


class File:
    def __init__(self, rel):
        self.rel = rel
        self.path = os.path.join(ROOT, *rel.split('/'))
        raw = io.open(self.path, 'rb').read()
        self.bom = raw[:3] == b'\xef\xbb\xbf'
        if self.bom:
            raw = raw[3:]
        self.crlf = b'\r\n' in raw
        self.lines = raw.decode('utf-8').replace('\r\n', '\n').split('\n')
        self.orig = '\n'.join(self.lines)
        self.dirty = False

    def has(self, s):
        return s in self.orig

    def patch(self, key, anchor, new):
        a = anchor.split('\n')
        n = new.split('\n')
        hits = [i for i in range(len(self.lines) - len(a) + 1) if self.lines[i:i + len(a)] == a]
        assert len(hits) == 1, '[锚点失配] %s key=%s 命中=%d' % (self.rel, key, len(hits))
        i = hits[0]
        self.lines[i:i + len(a)] = n
        self.dirty = True

    def between(self, key, start_prefix, end_prefix, new):
        """替换 [start_prefix 行, end_prefix 行) 之间（end 行保留）。"""
        st = [i for i, l in enumerate(self.lines) if l.startswith(start_prefix)]
        assert len(st) == 1, '[起点失配] %s key=%s 命中=%d' % (self.rel, key, len(st))
        i = st[0]
        en = [j for j in range(i + 1, len(self.lines)) if self.lines[j].startswith(end_prefix)]
        assert en, '[终点失配] %s key=%s' % (self.rel, key)
        self.lines[i:en[0]] = new.split('\n')
        self.dirty = True

    def guard(self, key, unique, fn):
        """幂等：unique 已存在则跳过。"""
        if self.has(unique):
            return
        fn()

    def write(self):
        if not self.dirty:
            return False
        text = '\n'.join(self.lines)
        if self.crlf:
            text = text.replace('\n', '\r\n')
        raw = text.encode('utf-8')
        if self.bom:
            raw = b'\xef\xbb\xbf' + raw
        io.open(self.path, 'wb').write(raw)
        return True


FILES = {}


def F(rel):
    if rel not in FILES:
        FILES[rel] = File(rel)
    return FILES[rel]


# =====================================================================
# SKILL.md
# =====================================================================
s = F(SKILL)

# ---- 1) §B：删掉过期的 "#T=/#难度 串房间名" ----
s.guard('b.roommeta',
        '不用再串字符串',
        lambda: s.patch(
            'b.roommeta',
            '''        ⚠ 房间名/地图名里的 `#T=枚举|任务类型` 与 `#难度` 由 RoomMeta.ComposeRoomName/ComposeMapName 拼（库缺字段的临时约定）''',
            '''        ⚠ 房间字段**不用再串字符串**了：2026-10-08 库换代后 `LanRoomInfo` 已有真字段
        （`Difficulty` / `TaskMain` / `InGame` / `Source`，见 `references/kcpnet-api.md` §6），
        旧的 `RoomMeta.ComposeRoomName` / `#T=` 约定**已删除**（2026-10-11 复核：全仓 0 命中）；`RoomMeta` 现在只剩"取值薄适配"'''))

# ---- 2) API 速查表四行 ----
s.guard('t.clientid',
        '字段 `ClientId`',
        lambda: s.patch(
            't.clientid',
            '''| `KCPNet<T,K>` | `ctor(bool isUnityEnvironment = true)`、`StartAsClient(ip,port)`、`StartAsServer(ip,port)`、`Task<ConnectResult> ConnectServer(int interval, int maxintervalSum = 5000)`、`SendToAll(K)`、`TryGetSession(uint, out T)`、`GetSessions()`、`CloseClient()` / `CloseServer()`、事件 `OnSessionConnected/OnSessionDisconnected/OnServerRestartDetected(Action<uint>)`、字段 `clientSession` | `T`=会话类型，`K`=消息类型（本项目为 `NetMessage`） |''',
            '''| `KCPNet<T,K>` | `ctor(bool isUnityEnvironment = true)`、`StartAsClient(ip,port)`、`StartAsServer(ip,port)`、`Task<ConnectResult> ConnectServer(int interval, int maxintervalSum = 5000)`、`SendToAll(K)`、`TryGetSession(uint, out T)`、`GetSessions()`、`CloseClient()` / `CloseServer()`、事件 `OnSessionConnected/OnSessionDisconnected/OnServerRestartDetected(Action<uint>)`、字段 `clientSession`、**`ClientId`**（持久会话标识，库 2026-10-09 版新增 ⇒ 见踩坑清单"两行 clientId"） | `T`=会话类型，`K`=消息类型（本项目为 `NetMessage`） |'''))

s.guard('t.hb',
        '每 3s 双向发心跳裸字节',
        lambda: s.patch(
            't.hb',
            '''| `KCPSession<T>` | 抽象属性 `Serializer`；虚方法 `OnConnected()` / `OnDisConnected()` / `OnReciveMsg(T)` / `OnUpdate(DateTime)`；`SendMsg(T)` / `SendMsg(byte[])`、`IsConnected()`、`GetSessionID()`、`InitSession(...)`、`CloseSession()`；属性 `AesKey`、`TimeoutMs`、`EnableTimeoutCheck`、`SetTimeout(long)` | 必须继承并 override `Serializer` + 至少 `OnReciveMsg` |''',
            '''| `KCPSession<T>` | 抽象属性 `Serializer`；虚方法 `OnConnected()` / `OnDisConnected()` / `OnReciveMsg(T)` / `OnUpdate(DateTime)`；`SendMsg(T)` / `SendMsg(byte[])`、`IsConnected()`、`GetSessionID()`、`InitSession(...)`、`CloseSession()`；属性 `AesKey`、`TimeoutMs`、`EnableTimeoutCheck`、`SetTimeout(long)`、`HeartbeatIntervalMs` | 必须继承并 override `Serializer` + 至少 `OnReciveMsg`。⚠ **库基类自己每 3s 双向发"心跳裸字节"**（带魔数、**不进 `MessageCenter`**），超时看 `_lastRecvTimeUtcTicks`（15s，任意 UDP 包都刷）⇒ **它替代不了应用层 `PingReq/Rsp`**（1001/1002 是 `RttMs` 的唯一来源，且判"谁是玩家"的那只时钟口径不同） |'''))

s.guard('t.netcfg',
        'HeartbeatIntervalMs = 3000',
        lambda: s.patch(
            't.netcfg',
            '''| `NetConfig` | 常量 `HostGamePort = 17666`、`LanBroadcastPort = 29800`、`LanSelfBroadcastPort = 29801` | 端口唯一来源，别在业务里硬编码 |''',
            '''| `NetConfig` | 常量 `HostGamePort = 17666`、`LanBroadcastPort = 29800`、`LanSelfBroadcastPort = 29801`、`HeartbeatIntervalMs = 3000`、`DefaultTimeoutMs = 15000` | 端口/心跳唯一来源，别在业务里硬编码。⚠ 它在**库内部**（工程里没有 .cs）⇒ 只能逐会话设 `TimeoutMs` 等属性，常量改不了 |'''))

s.guard('t.roominfo',
        'PlayerNames[]` + `int Difficulty',
        lambda: s.patch(
            't.roominfo',
            '''| `LanRoomInfo` | 字段 `RoomName/HostIp/HostPort/PlayerCount/MaxPlayers/MapName/PasswordProtected/Version/PlayerNames[]`、`ToJson()/FromJson()` | 房间模型，广播包与 UI 都用它 |''',
            '''| `LanRoomInfo` | 字段 `RoomName/HostIp/HostPort/PlayerCount/MaxPlayers/MapName/PasswordProtected/Version/PlayerNames[]` + **`int Difficulty(-1)` / `int TaskMain(-1)` / `bool InGame` / `int Source`**（共 **13** 个）、`ToJson()/FromJson()` | 房间模型，广播包与 UI 都用它。⚠ 2026-10-08 换代后**难度/任务/是否开局/来源已是真字段**，别再串字符串；`FromJson` **忽略未知 key、缺 key 给默认值** ⇒ 新旧房主可混联 |'''))

# ---- 3) §A 工作流：四步 → 六步 ----
A_NEW = '''### A. 新增一条网络消息（六步 + 验证，固定顺序）

> 决策口径见下方 §E「同步一条新东西」。**别跳过第 4/5 步** —— 历史上一半的"两端不一致"都出在桥与回环门上。

1. `07NetGame/CmdId.cs` 加命令号常量，遵循分段（新模块另开千位段，编号不重复、不复用、不改旧值）。传输层控制报文另放 `NetTmp/Services/NetCmdId.cs`（1xxx）。
2. `07NetGame/Msg/` 下（可按模块新建文件，如 `SyncMsg.cs`）加 `[MessagePackObject]` 类，字段用 `[Key(0..n)]` 连续编号。新增字段**一律追加在末尾**并取下一个 Key，不要插在中间（对端拿默认值即向后兼容）。**字段只能用基础类型**（`07_NetGame` 看不见 `01_GameContract`/`04_Data`/`06_Gameplay`；要传枚举就传 `int`）。
3. **在 `NetRoomFlow` 里加三件套**（它是业务收发的唯一入口，别在别处注册业务消息）：
   `public static event Action<XxxMsg> OnXxx;` + `public void SendXxx(...)`（房主 `NetHostSvc.SendToAll`、成员 `NetSvc.SendMsg`）+ `private void HandleXxxNtf(XxxMsg m) => OnXxx?.Invoke(m);`；
   再到 `Awake()` `MessageCenter.Register<XxxMsg>(CmdId.Xxx, HandleXxxNtf)`、`OnDestroy()` `Unregister`（⚠ 同一个 CmdId **只能有一个注册者**）。
4. **在 09 侧写桥**（`Net*Bridge`）：订阅"本地产生的那个事件"→ 上报；订阅 `NetRoomFlow.OnXxx` → 应用。桥是**唯一**能同时看见游戏层与网络层的地方（05/06 看不见 07）。
5. **回环门**：应用远端时会派发本地事件（`Operate()` 之类）⇒ 用 `NetApplyGuard.Begin()/End()` 包住；**两条桥可能互相回环时，门必须共享**（别各持一个私有 bool）。
6. **过滤与自派发**：上报点先判 `flow == null || !flow.InRoom`（单机/不在房里直接 return）；接收端按 `Sid` 丢掉"自己发出去绕回来的那条"。⚠ **房主广播的任何状态都要再 `MessageCenter.Dispatch(msg)` 本地自派发一次**（`SendToAll` 按定义不含房主自己）。

**验证**（别只看 Console 0 error）：① `offline_compile.py <asm>…`；② 新增 `.cs` 让 Unity 编一次后**反射核验类型/成员真的存在**（防"假空转"，见踩坑清单）；③ 两端都开 `NetSyncLog` 跑一遍，对照 `·发` / `·收` 两侧日志。

'''

s.guard('a.workflow',
        '同步一条新东西',
        lambda: s.between('a.workflow',
                          '### A. 新增一条网络消息',
                          '### B. 局域网联机最小闭环',
                          A_NEW))

# ---- 4) 新增 §E 决策树 + 诊断日志节（插在踩坑清单之前）----
E_NEW = '''### E. 同步一条新东西（决策树，2026-10-11 补）

听到"XX 两端不一致"时按这五问走，**别上来就加消息**：

**① 键用哪个？** 跨端稳定 = "创建时确定 + 两端一致"；⚠ 一律在 `Awake`/生成时算一次并缓存 —— 会位移的实体不能每次现算。

| 对象 | 键 | 注意 |
| --- | --- | --- |
| 家具 / 终端 | `Furniture_Attached.SyncId = FNV1a(Id + 世界坐标0.1m)` | 依赖"`BaseObject` 配好 Id + 摆位两端一致"；量化 0.1m 吃掉浮点抖动 |
| 任务 | `MissionBase.netOrder` | 创建序赋值；场景模式按 0.1m 位置排序（`MissionController.AssignNetOrdersByPosition`） |
| 场景单位（NPC） | `Actor.Id` | 同名多实例会撞 ⇒ 成组刷的单位改用 NetId |
| 成组新生成的单位（波次 / 巡逻队） | **生成点**（+ 逐单位目标点） | 构成靠"按落点派生流"⇒ 同落点同构成，不必传 |
| 波次 / 敌人 | `Actor.NetId`（`Actor.Awake` 按 `type==Enemy` 分配、每局归零） | 0 = 非同步；⚠ 分配点必须在 `Awake`（放 `WaitSetPos` 会晚一帧） |
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

## 踩坑清单'''

s.guard('e.tree',
        '同步一条新东西（决策树',
        lambda: s.patch('e.tree', '## 踩坑清单', E_NEW))

# ---- 5) 踩坑清单补 6 条 ----
PITS = '''- **`Dispatch` 的异常要打全**：`Debug.LogError($"消息 {cmdId} 处理失败:{e}")` —— 只打 `e.Message` 会把"哪个处理器、哪一行"全丢掉（打包版实测过）。
- **应用远端消息会"派发本地事件"（回环）**（2026-10-11）：`ApplyRemoteOperate` 跑的还是本地 `Operate()` ⇒ 会触发本端桥订阅的 `FurnitureOperate` / `UnitEventBus` 等 ⇒ 不挡就是无限回环。⚠ 门要**跨桥共享**：终端阶段的应用会走到 `furn.Operate()`（`NetFurnitureBridge` 的订阅点），各持一个私有 bool 挡不住 ⇒ 统一 `NetApplyGuard`（计数式）。
- **`SendToAll` 按定义不含房主自己**：凡"房主广播的状态"，都要**再 `MessageCenter.Dispatch(msg)` 自派发一次**（名单、开局、巡逻队、可破坏物都踩过）。
- **成员端不得本地生成单位**：`Actor.Awake` 会吃 `NetId` 号段 ⇒ 与房主下发的单位撞号（错杀 / 错移）。生成类一律"成员闸门 + 房主广播生成事件 + 按同一生成点复刻"（见 §E ②）。
- **别拿"本机视角"做共享选择**：`最近玩家` / `本机玩家位置` 这类输入两端必然不同（撤离点选择踩过）⇒ 由**触发者 / 房主**定（见 §E ②）。
- **随机同步要纯函数**：`SeedUtil.Derive(seed, 细分键)`（`SeedStream` 枚举）；⚠ 一条 `System.Random(seed)` 流在"重试 / 两端调用次数不同"时**必然错位**（谜题、炼油都踩过）。
- **"改了内容却没编上"的两种假象**（2026-10-11）：① **假空转** = dll mtime 不动 / Console **也 0 error** / `refresh_unity` 照样返成功 —— 三个"通过"全是假象，**判据改反射**（`GetType`/`GetMethod` 是否为 null），解药 `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`（⚠ `external_changes_dirty:true` 时先 `AssetDatabase.Refresh(ForceUpdate)`）；② **假缺失** = `.cs.meta` 被写成等长全 `\\x00` ⇒ Unity 导不出 guid ⇒ 该 `.cs` 不进编译、报 `CS0103 名称"X"不存在` 而文件明明在 ⇒ 先查 `manage_asset get_info` 的 `guid`，**别改代码**。
- **改完一批联机代码，收尾做三件**：`offline_compile.py` 编一遍 → 让 Unity 编一次并**反射核验新成员** → 双端开 `NetSyncLog` 对照 `·发`/`·收`。'''

s.guard('pits.add',
        '两种假象',
        lambda: s.patch('pits.add',
                        '''- **`Dispatch` 的异常要打全**：`Debug.LogError($"消息 {cmdId} 处理失败:{e}")` —— 只打 `e.Message` 会把"哪个处理器、哪一行"全丢掉（打包版实测过）。''',
                        PITS))

# =====================================================================
# references/nettmp-layer.md
# =====================================================================
l = F(LAYER)

# ---- 6a) 命令号表 4041–4049 ----
l.guard('tbl.4041',
        'KeyScreenInputNtf',
        lambda: l.patch(
            'tbl.4041',
            '''| 4040 | `EnemyDamagedSync` | 房主 → 全体：房主打中的伤害下行（与 4025 成对，血量口径统一"房主权威"） |''',
            '''| 4040 | `EnemyDamagedSync` | 房主 → 全体：房主打中的伤害下行（与 4025 成对，血量口径统一"房主权威"） |
| 4041 | `LoadCompleteNtf` | 成员 → 房主：本机战斗场景加载完成 |
| 4042 | `BattleStartSync` | 房主 → 全体：**全体加载完 ⇒ 一起开打**（各端凭它开 `IsStartBattle`）。⚠ 与 4012 分开：各端加载快慢差很多 |
| 4043 | `SceneDestructibleNtf` | 双向：场景可破坏物（油桶这类）被打掉。键 = `FNV1a(Actor.Id + 位置0.1m)`（同 `SyncId` 口径）；**只传"死了"不传伤害**；房主侧有账本、新人入房补发 |
| 4044 / 4045 | `PlayerLoadoutNtf / Sync` | 改装（档位 / 模组 / 载具）上报与广播 |
| 4046 | `KeyScreenStageNtf` | 双向：**按键终端(KeyScreen)阶段推进**（键 = 终端家具 `SyncId`）。⚠ **驱动端**上报（操作者那台 / 无人阶段房主）、其余端只应用（见 `KeyScreen.IsStageDriver`） |
| 4047 | `PatrolSpawnNtf` | 房主 → 全体：**巡逻队生成**（生成点 + 逐单位目标点）。构成靠"同落点派生流"，成员端 `CreatPatrolRemote` 复刻 |
| 4048 | `OOPartSubmitNtf` | 双向：容器类家具的一次提交（`SyncId` + 个数 + 主类型）⇒ 远端在盟友的**幽灵背包**(`FriendOOPartInventory`)上真跑 `BeginPress/StepPress` |
| 4049 | `KeyScreenInputNtf` | 双向：玩家对终端的**按键**（`SyncId` + `InputState` + 类型 0 单次/1 按住/2 松开）⇒ 非驱动端把输入换成远端按键、跑同一套阶段逻辑 |'''))

# ---- 6b) SyncMsg 的 DTO 清单 ----
l.guard('syncmsg.dto',
        'KeyScreenStageMsg',
        lambda: l.patch(
            'syncmsg.dto',
            '''| `Msg/SyncMsg.cs` | 局内世界状态：`GameOverMsg` / `EvacuateMsg` / `MissionUpdateMsg` / `FurnitureOperateMsg` / `MarkMsg` / `CallKaiMsg` / `WaveCenterMsg`。 |''',
            '''| `Msg/SyncMsg.cs` | 局内世界状态：`GameOverMsg` / `EvacuateMsg` / `MissionUpdateMsg` / `FurnitureOperateMsg`（⚠ 已扩 `int State` + `string StateKey`，**StateKey 非空 = 状态同步**，接收端走 `ApplyRemoteState` 而**不**重放 `Operate()`）/ `MarkMsg` / `CallKaiMsg` / `WaveCenterMsg` / `SceneDestructibleMsg` / `KeyScreenStageMsg` / `PatrolSpawnMsg` / `OOPartSubmitMsg` / `KeyScreenInputMsg`。 |'''))

# ---- 6c) §6 整节改写 ----
L6 = '''## 6. 房间字段的读取（`RoomMeta` = 取值薄适配层）

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

'''
l.guard('sec6',
        '取值薄适配层',
        lambda: l.between('sec6',
                          '## 6. 库缺字段的临时约定',
                          '## 7. 联机踩坑',
                          L6))

# =====================================================================
# references/kcpnet-api.md
# =====================================================================
a = F(API)

a.guard('a.src',
        '2026-10-11 复核',
        lambda: a.patch('a.src',
                        '''> 来源：对 `Assets/Plugins/KCPNet/*.dll` 的反射/元数据解析结果（2026-09-30 实测）。''',
                        '''> 来源：对 `Assets/Plugins/KCPNet/*.dll` 的反射/元数据解析结果（2026-09-30 首测，**2026-10-11 复核并补入库 2026-10-08 / 10-09 两代新增成员**）。'''))

a.guard('a.clientid',
        '持久会话标识',
        lambda: a.patch('a.clientid',
                        '''| `clientSession` | `T` | 客户端模式下自己那条会话（`NetSvc` 直接读它） |''',
                        '''| `clientSession` | `T` | 客户端模式下自己那条会话（`NetSvc` 直接读它） |
| `ClientId` | `string` | **持久会话标识**（库 2026-10-09 版新增）：注入后，库的会话表按 `cid:` 复用而不是按 `ep:`（endpoint）⇒ 换端口/重连不会被当成"新的人"。⚠ 项目在 `NetTmp/Transport/NetClient.cs` 里注入（`private readonly string _clientId = Guid.NewGuid().ToString("N")` + `_client.ClientId = _clientId`）——**这两行被外部编辑覆盖过 2 次**，改完要核对字节 |'''))

a.guard('a.hb',
        'HeartbeatIntervalMs',
        lambda: a.patch('a.hb',
                        '''| `long TimeoutMs` / `bool EnableTimeoutCheck` / `void SetTimeout(long)` / `void RefreshLastRecvTime()` | 心跳超时控制 |''',
                        '''| `long TimeoutMs` / `bool EnableTimeoutCheck` / `void SetTimeout(long)` / `void RefreshLastRecvTime()` | 心跳超时控制 |
| `HeartbeatIntervalMs` | 库内部心跳间隔（默认 `NetConfig.HeartbeatIntervalMs = 3000`）：基类每 3s 双向发**心跳裸字节**（带魔数、`IsHeartbeat` 全等判定）⇒ **永远到不了 `OnReciveMsg` / `MessageCenter`**；超时判据是 `_lastRecvTimeUtcTicks`（15s，任意 UDP 包都刷） |'''))

a.guard('a.netcfg',
        'HeartbeatIntervalMs      = 3000',
        lambda: a.patch('a.netcfg',
                        '''public const int HostGamePort      = 17666;   // 房主 KCP 监听端口
public const int LanBroadcastPort  = 29800;   // 局域网广播/发现端口
public const int LanSelfBroadcastPort = 29801;// 同机双实例时成员侧用''',
                        '''public const int HostGamePort      = 17666;   // 房主 KCP 监听端口
public const int LanBroadcastPort  = 29800;   // 局域网广播/发现端口
public const int LanSelfBroadcastPort = 29801;// 同机双实例时成员侧用
public const int HeartbeatIntervalMs  = 3000; // 库内部心跳间隔（裸字节，不进 MessageCenter）
public const int DefaultTimeoutMs     = 15000;// 库内部会话超时（任意 UDP 包都刷 _lastRecvTimeUtcTicks）

// ⚠ 这几个常量在**库内部**（工程里没有对应 .cs）⇒ 改不了；能改的只有
//   逐会话属性：TimeoutMs / EnableTimeoutCheck / SetTimeout(long)。'''))

a.guard('a.roominfo',
        'public int    Difficulty',
        lambda: a.patch('a.roominfo',
                        '''public string RoomName;
public string HostIp;
public int    HostPort;        // 必须是房主 KCP 监听端口，否则回连不上
public int    PlayerCount;
public int    MaxPlayers;
public string MapName;
public bool   PasswordProtected;
public string Version;
public string[] PlayerNames;

public string ToJson();
public LanRoomInfo FromJson(string json);''',
                        '''public string RoomName;
public string HostIp;
public int    HostPort;        // 必须是房主 KCP 监听端口，否则回连不上
public int    PlayerCount;
public int    MaxPlayers;
public string MapName;
public bool   PasswordProtected;
public string Version;
public string[] PlayerNames;
// ↓ 库 2026-10-08 版新增的 4 个真字段（此前项目只能把它们串进房间名/地图名，那套约定已删除）
public int    Difficulty;      // -1 = 广播里没带（缺省）；JSON 键 difficulty
public int    TaskMain;        // 主任务枚举（两端一致的整数）；JSON 键 taskMain
public bool   InGame;          // 已进战斗 ⇒ 房间列表显示「进行中」且不给加入；JSON 键 inGame
public int    Source;          // 来源（局域网广播 / 服务器…）；JSON 键 source

public string ToJson();                  // 实例方法
public static LanRoomInfo FromJson(string json);   // ⚠ 忽略未知 key、缺 key 给默认值 ⇒ 新旧版本可混联'''))

a.guard('a.roominfo.note',
        '项目实测在用',
        lambda: a.patch('a.roominfo.note',
                        '''### `LanBroadcaster`（房主侧）''',
                        '''> ⚠ 项目实测在用的字段（代码证据）：`NetHostSvc.cs` 写 `RoomInfo.InGame`(:374) / `RoomInfo.TaskMain`(:859) / `RoomInfo.Difficulty`(:60 注释) / `Source = (int)RoomMeta.SourceEnum.LanBroadcast`(:294)；
> 读侧统一走 `RoomMeta`（`ServerListPanel.cs` 全量转发）与 `TaskManager`。⇒ 房间字段**不再需要字符串约定**。

### `LanBroadcaster`（房主侧）'''))

# =====================================================================
def main():
    apply = '--apply' in sys.argv
    changed = [f.rel for f in FILES.values() if f.dirty]
    print('=== 待写入：%d ===' % len(changed))
    for rel in changed:
        print('  M ' + rel)
    if not changed:
        print('没有需要改的东西（已打过补丁）。')
        return
    if not apply:
        print('\n[dry run] 校验全部通过。加 --apply 落盘。')
        return
    n = 0
    for f in FILES.values():
        if f.write():
            n += 1
    print('\n已落盘：%d 个文件。' % n)


if __name__ == '__main__':
    main()
