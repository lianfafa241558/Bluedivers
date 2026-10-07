# 网络联机（房间列表 ↔ 网络层）设计计划

> 范围锚点：
> - 面板 `Assets/Scripts/10UI/Comp/ServerListPanel.cs`（asmdef **10_UI**）
> - 预制体 `Assets/Resources/UI/Wnd/SelectMapWnd.prefab` 的 `Rooms` 节点（默认 inactive）
> - 网络层 `Assets/Scripts/NetTmp/`（asmdef **02_Net**）+ `Assets/Plugins/KCPNet/*.dll`
>
> 本文件只出**计划**，不含已落码改动。日期 2026-10-06。

---

## 0. 现状事实（全部实测，带证据）

### 0.1 面板侧（已完整，但只到"列出来"为止）

| 事实 | 证据 |
| --- | --- |
| 发现/筛选/手风琴/去重刷新都已实现 | `ServerListPanel.cs:164-219`（`Open→StartListening+Scan+Refresh`，`Update` 按签名比对重建） |
| **点行只高亮，不发任何请求** | `ServerListPanel.cs:712-721` `Select(index)` 只改 `_selected` + 颜色 |
| 无 `SelectedRoom` / 无 `OnRoomActivated` | 字段区 `:126-158` |
| 难度是假数据 | `:748-768` `RoomDifficulty()` 先解 `MapName` 的 `#` 约定、否则回退 `TaskState.Difficulty`；3 组 `//TODO:【缺 KCP 能力】` 在 `:635-639 / :727-732 / :764-767 / :770-774` |
| 类型按地址猜 | `:776-799` `RoomNetType()` + `IsLanAddress()` |
| 开局状态无字段 | 无 `InGame` 相关代码 |
| 空/失败只 `Debug.Log` | `Refresh()` 末尾 `:507-510`；`StartListening()` 失败 `:375-377` 只 `LogWarning` |
| 面板不会过滤"自己开的房" | 无 `_selfHostName` 逻辑（`LanRoomDemo.cs:260-276` 才有） |
| 刷新签名未含难度/来源/开局 | `BuildSignature()` `:452-464` |

### 0.2 预制体实测层级（`Rooms` 子树，用 `.codebuddy/plans/rooms_hierarchy.py` 打印）

```
Rooms                       [inactive]    ← 挂 ServerListPanel(guid 469eb650…)
├─ Mask                     [inactive]    ← 全屏拉伸的遮罩（当前未使用）
├─ Panel
│  ├─ Filter
│  │  ├─ Title / Line
│  │  ├─ InputField (TMP)   ← searchInput
│  │  ├─ MapBtn/Title/{Text(1), Text(2)=mapValue, Arrow}  + 子模板项
│  │  ├─ DiffBtn/…=diffValue
│  │  ├─ PlayerCountBtn/…=countValue            + 2 个固定项
│  │  ├─ NetTypeBtn/…=netTypeValue              + 3 个 FilterDiffItem
│  │  ├─ placeholder        ← 只是 LayoutElement 占位，**没有 TMP 文本**
│  │  └─ Line (1)
│  └─ List
│     ├─ Header{Task/Diff/Team/Type}
│     ├─ Line
│     ├─ Scroll/Viewport/Content/Row   ← rowTemplate（Task/Diff/Team/Type 四列）
│     └─ Line (1)
├─ Server                   ← refreshBtn（锚 (1,0)，anchoredPos(-280,50)，200×40）
└─ Cancel                   ← closeBtn（锚 (1,0)，anchoredPos(-45,50)，200×40）
```

结论：
- **没有 `Status` 节点**（当初删掉了）⇒ 要做空/失败提示必须新增节点；`Filter/placeholder` 不能复用。
- `Server` / `Cancel` 是 `Rooms` 的**兄弟**节点（不在 `Panel` 下）⇒ 新增「加入」按钮同锚点放到 `(-515, 50)` 即可与它们并排。
- `Mask`（inactive，全屏拉伸）可当**模态遮罩**复用给密码输入框。

### 0.3 网络层

| 事实 | 证据 |
| --- | --- |
| `NetSvc.ConnectToRoom` / `JoinRoom` 已具备 | `NetSvc.cs:88-107`（只用到 `room.HostIp/HostPort`）、`:132-139` |
| `NetHostSvc.StartHost` **无难度参数** | `NetHostSvc.cs:103`、`RoomInfo` 构造 `:141-151` |
| `PlayerNames[0]` 是合成名 `Host_<HHmmssfff>` | `NetHostSvc.cs:140,150` |
| 房主名写死 `"房主"` | `BuildPlayerArray()` `:314-322` |
| 开局通知只带地图名 | `RoomMsg.cs:95-100` `StartGameNtf{MapName}` |
| `MessageCenter` 是"命令号→单处理器"字典 | `MessageCenter.cs:41,49-56,106-109`（`Register` 会**覆盖**） |
| **整个 02_Net 未接入游戏** | 全库 GUID 扫描：`NetSvc`(746a1262…) / `NetHostSvc`(eb726dec…) / `LanRoomDemo`(95976e88…) / `NetDemo` **在场景与预制体里 0 命中**，只在各自 `.meta` 命中 ⇒ 面板即便调 `NetSvc.Instance` 也拿到 null |
| `10_UI.asmdef` **没有** `02_Net` | `Assets/Scripts/10UI/10_UI.asmdef:4-24`（能直接吃 `KCPNet.*` 是因为插件全局自动引用） |
| `02_Net.asmdef.references = []` | `Assets/Scripts/NetTmp/02_Net.asmdef:4` |
| `02_Net.csproj` 存在 ⇒ 可离线编译 | 工作区根目录实测（`plans/offline_compile.py 02_Net 10_UI`） |

### 0.4 库侧（dnfile 静态实测 `Assets/Plugins/KCPNet/KCPNet.dll`）

- `KCPNet.LanRoomInfo`：**public / not sealed / [Serializable]**，字段恰好 **9 个且全是 public**：
  `RoomName / HostIp / HostPort / PlayerCount / MaxPlayers / MapName / PasswordProtected / Version / PlayerNames`
- 方法：`ToJson`（实例）、`FromJson`（静态），私有工具 `WriteString / WriteInt / WriteBool / WriteArray / ParseStringArray / Escape / Unescape / SplitTopLevel`
- ⚠⚠ **本机未找到 KCPNet 源码工程**：探测 `D:\Pro`、`D:\Project`、`D:\Game`、`D:\Work`、`D:\Code`、`C:\Users\Administrator\source`（深度 5）⇒ **0 命中**（脚本 `plans/find_kcp_source.py`）。
  `skills/kcpnet-online/SKILL.md:14` 也写明"无源码的托管 DLL，只能反射/反编译看 API"。
  ⇒ **「给 LanRoomInfo 加 Difficulty/InGame/Source 并重出 DLL」必须先拿到源码**，这是本计划唯一的硬阻塞项（见 §1）。

---

## 1. 唯一阻塞项：字段扩展走哪条路

`LanRoomInfo` 的难度/来源/开局三项，是面板 5 件交付里 3 件的共同前提。两条路二选一：

### 方案 A（交接所说的路线）：改库 → 重出 `KCPNet.dll`

前提：提供 KCPNet 源码工程路径（本机没有）。

改动（源码侧）：
```csharp
public int  Difficulty = -1;   // <0 = 未知/旧版房主
public bool InGame;
public int  Source;            // 0=LanBroadcast 1=ServerList（用 int 避免库反向依赖项目类型）
```
- `ToJson()` 里追加 `WriteInt(sb, "difficulty", Difficulty)` / `WriteBool(sb, "inGame", InGame)` / `WriteInt(sb, "source", Source)`
- `FromJson(json)` 里 **缺 key 给默认值**（`-1 / false / 0`）⇒ 兼容旧版房主
- 重出后覆盖 `Assets/Plugins/KCPNet/KCPNet.dll`（可带上 `.pdb`）→ `ImportAsset(ForceUpdate)`
- 验收：`plans/dll_type_dump.py KCPNet.LanRoomInfo` 应列出 **12** 个字段 + `ToJson/FromJson`；再做一次 `ToJson→FromJson` 往返（Unity `execute_code`）确认 3 个新字段能过、且**手工删掉 key 后仍能解析出默认值**

兼容性矩阵（要点）：
| 组合 | 结果 |
| --- | --- |
| 新客户端 ← 旧房主 | 缺 key ⇒ 默认值；难度显示"-"、来源回退按地址猜 |
| 旧客户端 ← 新房主 | 依赖库的 `FromJson` 是否容错：**容错 ⇒ 忽略未知 key 不崩**；若严格校验 ⇒ 必须同版本联机 |
| 新客户端 ← 新房主 | 全字段生效 |

> ⚠ `FromJson` 的严格程度**必须看源码确认**，它决定"能不能新旧混联"。

### 方案 B（无源码时的替代）：02_Net 自研发现层，完全不碰库

新增项目侧模型 + 收发器，`LanDiscoverer/LanBroadcaster` 不再使用：
```
Assets/Scripts/NetTmp/Client/RoomInfo.cs        // 项目侧房间模型（9 字段 + Difficulty/InGame/Source）
Assets/Scripts/NetTmp/Client/RoomAnnouncer.cs   // 房主：周期 UDP 广播 JSON（复用 NetConfig.LanBroadcastPort=29800）
Assets/Scripts/NetTmp/Client/RoomScanner.cs     // 成员：收 ANNOUNCE + 发 DISCOVER + 自实现过期(RoomExpireMs=6000)
```
- 回连仍走 `NetSvc.ConnectToRoom(new LanRoomInfo { HostIp = …, HostPort = … })`（实测该方法**只用这两个字段**，`NetSvc.cs:96-106`）
- 代价：自实现过期/版本过滤（约 40 行）+ 与库的 "KCPLAN:" 包互不干扰（各用各的 magic）
- 收益：字段完全自由；`10_UI` 可以不再直接依赖 `KCPNet.*` 的房间模型

**建议**：先按 §2 把与 A/B 无关的部分做完（面板只读增强、加入链路、引导），把 §1 的决策留给用户拍板 —— 路线不同只影响 §5 的"数据映射段"与 §4 的字段读取，不影响其余 80% 的工作量。

---

## 2. 分层与依赖

现状：`10_UI` 已经直接吃 `KCPNet.*`（插件自动引用），但它**看不到 `02_Net`**。

- 目标依赖：`10_UI → 02_Net → (KCPNet/MessagePack 插件)`，`02_Net.references` 保持 `[]`
- 合法性：编号 10 > 02 ⇒ "上层引用下层"，无环，符合规范
- 改动：`10_UI.asmdef` 的 `references` 追加 `"02_Net"`
- **不要**给 `02_Net` 加对 `04_Data`/`09_Managers` 的引用：玩家名等参数一律**由 UI 传入**（UI 侧读 `ArchivesData_SO.Current.playerName`），保持 02_Net 是最底层网络适配层

> UI 只跟一个薄封装打交道（§3.2 `NetRoomFlow`），不直接 `MessageCenter.Register`；将来若要多端共用（单人/联机同 UI），再把 `NetRoomFlow` 抽成 `01_GameContract` 的接口。本轮**不**抽（收益不可见、要动更低层）。

---

## 3. 网络层改造（02_Net）

### 3.1 引导：把网络根节点挂起来（**必须先做，否则面板拿到 null**）

新增 `Assets/Scripts/NetTmp/Client/NetBootstrap.cs`：
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
static void Boot()   // 场景里没有 NetSvc 时，建 DontDestroyOnLoad("NetRoot") 并挂 NetSvc + NetHostSvc
```
- 与 `WndHub.Bootstrap()`（`10UI/WndHub.cs:52-56`）同一套手法，零场景改动
- 保留一个 `[SerializeField] bool autoCreate` 之类的开关（或用静态字段），便于将来改回"手动挂场景"
- 备选：直接在 `Assets/Scene/GameRoot.unity` 上挂（`GameRoot` 本身就是 `DontDestroyOnLoad`，见 `GameRootBase.cs:31`）。**推荐自动引导**：不产生场景改动、多机/多窗口一致

### 3.2 `NetRoomFlow`：成员入房编排（新增 `Client/NetRoomFlow.cs`）

```csharp
public static class NetRoomFlow
{
    public static event Action<bool, string, PlayerInfo[]> OnJoinResult; // ok, reason, players
    public static event Action<PlayerInfo[]> OnPlayerList;
    public static event Action<string> OnStartGame;                      // mapName

    public static void Init();        // Register: JoinRoomRsp / PlayerListSync / StartGameNtf
    public static void Dispose();     // Unregister 三条（与 Init 成对）
    public static void Join(LanRoomInfo room, string playerName, string password, float timeoutSec = 8f);
    public static void Leave();       // NetSvc.LeaveRoom + Disconnect
}
```
要点：
- `Join` = 回连 → 成功再 `JoinRoom(name, pwd)`（对齐交接里的 `ok => ok && JoinRoom(...)`）
- **必须加超时**：`ConnectToRoom` 的回调可能永不回来（对端不在线/防火墙丢包），8s 未收到 `JoinRoomRsp` 就报"入房超时"
- 错误文案来自房主：`JoinRoomRsp.ErrorCode/Reason`（"房间已满"/"密码错误"，房主侧已实现 `NetHostSvc.cs:214-255`）
- 与 `LanRoomDemo`/`NetDemo` **不能同时启用**：`MessageCenter` 同命令号只留最后一个处理器（`MessageCenter.cs:49-56`）。这两个 demo 当前不在任何场景里（§0.3），保持现状即可

### 3.3 房主侧（`NetHostSvc` / `RoomMsg`）

| 改动 | 位置 | 说明 |
| --- | --- | --- |
| `StartHost(..., int difficulty = -1)` | `NetHostSvc.cs:103` | **追加在末位**，既有 3/4 参调用不受影响 |
| `RoomInfo.Difficulty = difficulty` | `:141-151` | 方案 A 才有此字段 |
| `InGame` 广播 | `:182-187` `GetBroadcastInfo()` | 新增私有 `bool _started`，`StartGame()`(`:283`) 里置 true |
| 房主项写真名 | `:314-322` `BuildPlayerArray()` | `"房主"` → 真实玩家名（队伍列/玩家列表要显示） |
| `PlayerNames` 追加真名 | `:150` | 在**末尾**追加，`[0]` 合成名保留（成员端 `IndexOf` 排除自己开的房靠它，别改 `[0]`） |
| `SelfHostName` 暴露 | `NetHostSvc` | 供面板过滤"自己开的房" |
| `StartGameNtf` 扩字段 | `RoomMsg.cs:95-100` | 追加 `Difficulty / TaskIndex / ExtraDiff[] / Seed`（`[Key(1..4)]` 追加在末尾）——否则成员无法复现房主选的难度/任务，见 §6 第 5 步 |

### 3.4 成员端（`NetSvc`）

- `ConnectToRoom` 已有超时参数（`:103` `ConnectServer(200, 5000)`），够用；`JoinRoom` 的超时在 `NetRoomFlow` 兜
- `SRV_IP="127.0.0.1"`(`:44`) 仍是死链（`ConnectDefaultServer` 无调用点）⇒ 本轮不动，但要在注释里保持"未启用"

---

## 4. 面板改造（`ServerListPanel.cs` + prefab）

### 4.1 数据契约（交接第一件）

- 新增字段/事件：
  - `public LanRoomInfo SelectedRoom { get; private set; }`（方案 B 则类型换成 `FPSGame.Net.RoomInfo`）
  - `public event Action<LanRoomInfo, string> OnRoomActivated;`（第二个参数 = 密码，无密码传 `""`）
- `Select(index)`（`:712-721`）：点已选中行 ⇒ 触发 `Activate`（空密码直接抛；`PasswordProtected` 则先弹密码框）
- 新增「加入」按钮 `Rooms/Join`：锚 `(1,0)`、`anchoredPos(-515,50)`、`200×40`（与 `Server`/`Cancel` 并排同尺寸），`SetButtonInteractable` 随 `SelectedRoom != null` 联动

### 4.2 密码输入（新增 `Rooms/Panel/PasswordBox`）

- 节点：半透明底 + `TMP_InputField`（复用 `Filter/InputField (TMP)` 的做法）+ 确定/取消 两个按钮；默认 inactive，`Rooms/Mask` 作为模态遮罩（打开时 `SetActive(true)`）
- 面板新增序列化字段：`pwdBox / pwdInput / pwdOk / pwdCancel`
- 流程：`Activate(room)` → `room.PasswordProtected ? 打开密码框 : OnRoomActivated(room, "")`；回车 = 确定、ESC = 关闭（面板 `Close()` 已有 ESC 链路，见 `SelectMapWnd.cs:263-268`）
- ⚠ 真实密码**不在广播里**（`LanRoomInfo` 只有 `PasswordProtected`）⇒ 只能让玩家手输，这是设计使然

### 4.3 难度（方案 A 后）

- `Fill()`(`:684-685`) 改读 `room.Difficulty`（`<0` 显示 `"-"`）
- `RebuildOptions()`(`:517-561`) / `PassFilter()`(`:629-639`) 改读 `room.Difficulty`；`<0` 的房间**不进难度下拉的计数**，也不会被具体难度筛中
- **删**：`ParseMapName`(`:737-742`) 的 `#` 解析、`RoomDifficulty()`(`:748-768`) 的 `TaskState.Difficulty` 回退、三组 `//TODO`（`:635-639 / :727-732 / :764-767 / :770-774`）。`ParseMapName` 若无其它调用点就整段删；有则退化成纯空值判断

### 4.4 来源（方案 A 后）

- `RoomNetType()`(`:776-779`) 改读 `room.Source`；`IsLanAddress()`(`:782-799`) **保留但降级**为 `Source` 未知时的回退（旧版房主）
- `NetTypeEnum`(`:39-46`) 保持不动

### 4.5 是否已开局

- `InGame` ⇒ 行「状态」列显示"进行中"（配色改 `ServerColor`/灰）；`InGame == true` 的行**不响应加入**
- `PassFilter` 的 `NotFullLabel`（`:642`）口径改为 `未满员 && !InGame`

### 4.6 空/失败提示（新增 `Rooms/Panel/Status`）

- 节点：`BottomLeft(48, 18)`、`800×24`、18 号字（`Panel` 底部 48px 留白内，不压 `List`）
- 面板新增 `[SerializeField] private Transform statusText;`
- 文案分支（替换 `:507-510` 的 `Debug.Log`）：
  | 情况 | 文案 |
  | --- | --- |
  | `_listenFailed` | "房间发现不可用：UDP 端口被占用或被防火墙拦截" |
  | 搜到 0 | "正在搜索局域网房间…" / "未搜索到房间" |
  | 搜到但筛后 0 | "已有 N 个房间，当前筛选条件下 0 个" |
  | 正常 | "共 N 个房间（局域网 X / 服务器 Y）" |
- 加入失败/超时的提示走 `WndHub.Tip.Creat`（`SelectMapWnd.cs:162-165` 已有用法）

### 4.7 其它必须同步的点

- `BuildSignature()`(`:452-464`) 把 `Difficulty / InGame / Source` 纳进签名，否则字段变了面板不刷新
- `OnDisable`(`:193-198`) 顺手关密码框、`_selected = -1`、`SelectedRoom = null`
- 预制体新增节点由谁做：**建议我出一版生成器**（沿用 `plans/BuildServerListPanel_*.cs.txt` 的 `PrefabUtility.LoadPrefabContents` 手法，改 `Rooms` 前先读旧层级做幂等），或用 MCP `execute_code` 直接改 prefab 并回填 `SerializedObject` —— 两者都需要用户点头（会改资产）

---

## 5. 窗口侧接线（`SelectMapWnd.cs`）

1. `FirstShowWnd()`：`serverPanel.OnVisibleChanged = SetServerPanelVisible;`(`:141`) 之后补
   ```csharp
   serverPanel.OnRoomActivated = OnRoomActivated;   // 新增
   ```
2. 新增 `OnRoomActivated(LanRoomInfo room, string pwd)`：
   `NetRoomFlow.Join(room, ArchivesData_SO.Current.playerName, pwd)`；结果回调里成功 ⇒ 进房 UI（或直接 `StartGameNtf` 待命），失败 ⇒ `WndHub.Tip.Creat(...)`
3. 开房入口（可选，第 4 步）：`taskPublic`(`:168-177`) 现在是"未完成的功能"占位 ⇒ 改成
   `NetHostSvc.Instance.StartHost(roomName, mapRoot.GetChild(SelectMapIndex).name, 4, password, difficulty: SelectTaskDiff)`
   `taskJoin`(`:159-167`) 改成"打开房间列表"（= 现在 `server` 按钮的行为）
4. `RefreshDisplay`/`ExpandCfg` 不动

---

## 6. 执行顺序（每步独立可验证）

| 步 | 内容 | 验证方式 |
| --- | --- | --- |
| **1** | 引导（§3.1）+ 面板只读增强（§4.6 状态文本、§4.7 签名/清理、自己开的房过滤） | 离线编译 `02_Net 10_UI`；Play 里能看到状态文案；同机开房+搜房时列表不出现自己的房 |
| **2** | 加入链路（§3.2 `NetRoomFlow` + §4.1 选中/加入回调 + §4.2 密码框 + §5.1/5.2 接线） | 同机双实例（成员用 29801）跑"搜到→点行→入房"；密码错/满员能拿到房主的 `Reason` 文案 |
| **3** | 字段扩展（§1 方案 A 或 B）+ 面板读真难度/来源/InGame + 删 `#` 约定与 3 处 TODO | 方案 A：`dll_type_dump.py` 出 12 字段 + JSON 往返/缺 key 默认值；面板两条不同难度的房能显示不同难度 |
| **4** | 开房入口（§3.3 难度参数 + §5.3）+ 房主真名/`SelfHostName` | 开房广播里 `Difficulty` 与选图界面一致；队伍列/玩家列表显示真名 |
| **5** | 开局同步（`StartGameNtf` 扩字段 → 成员 `TaskManager.SetTask` + `RoomManager.JoinPlayer`） | 房主开局 → 成员地图/难度/任务与房主一致 |

> 第 5 步已经越过"面板 ↔ 网络层"，属于真正的联机开局逻辑；本轮只给接口，是否接着做由用户定。

---

## 7. 验证手段（对齐既有约定）

- **离线编译**：`python .codebuddy/plans/offline_compile.py 02_Net 10_UI 09_Managers`（新增 `.cs` 未在 csproj 时用 `plans/tmp_compile_newfile.py`）
- **Unity 编译三证**（热区 3）：dll mtime 前进 + Console 0 error + `is_compiling:false`；新成员用 `execute_code` 反射核对（防"假空转"，必要时 `CleanBuildCache`）
- **库替换核验**：`plans/dll_type_dump.py KCPNet.LanRoomInfo`（字段/方法清单）
- **端到端**：两实例/两机跑 "开房 → 搜房 → 入房（含密码）→ 满员/密码错 → 离开"，核对面板状态文本 + `WndHub.Tip`

---

## 8. 风险 / 坑（提前记账）

1. **KCPNet 无源码**（§0.4）—— 唯一硬阻塞；方案 B 是完整的退路
2. `FromJson` 严格性未知 ⇒ 决定"新旧版本能否混联"（方案 A 必读源码）
3. **`02_Net` 当前完全没挂进游戏** —— 不做第 1 步，后面全是空转
4. UDP 端口：房主占 29800 ⇒ 同机自测成员必须 29801（收不到 ANNOUNCE，只能靠 3s 周期 `Scan()`）；防火墙要放行
5. `StopHost` 后端口 TIME_WAIT ⇒ 立刻重开抛 `SocketException`（`NetHostSvc.cs:117-129` 已 catch），业务层要复刻 `LanRoomDemo` 的 2s 冷却（`LanRoomDemo.cs:83-84,146-154`）
6. `MessageCenter` 同命令号**单处理器** ⇒ `LanRoomDemo`/`NetDemo` 与 `NetRoomFlow` 不能同时启用
7. `WndHub.Tip` 只能显示文字，**没有输入** ⇒ 密码必须在面板内解决
8. 面板 `Refresh()` 有两次 `Canvas.ForceUpdateCanvases`（`:501-503`）⇒ 新节点别塞进 `Content`（会参与列表重排）
9. 规范：新接口用 `I` 前缀；`const/static` 放类顶；`OnDisable/OnDestroy` 成对反注册（`NetRoomFlow.Dispose`、`ServerListPanel.OnDestroy`）

---

## 9. 待拍板

1. **KCPNet 源码能否提供** ⇒ 走方案 A（改库重出 DLL）还是方案 B（02_Net 自研发现层）？
2. asmdef：直接 `10_UI → 02_Net`（推荐，本轮）还是先抽 `01_GameContract` 接口（后置）？
3. 密码输入：面板内局部框（推荐，节点少）还是新建 `PasswordWnd` 窗口（要登记 `WndType` + 新预制体）？
4. 「加入」交互：二次点击行 +「加入」按钮（推荐，两者都留）还是只留按钮？
5. 本轮范围：只做第 1~2 步，还是连第 3~5 步一起排期？
6. 预制体节点（`Join` / `Status` / `PasswordBox`）由我出生成器/用 MCP 改，还是用户手动摆？

---

## 10. 实现进度（2026-10-06，用户已拍板后落地）

**用户拍板**：①挂 GameRoot ②改库暂缓、只留 `//TODO` ③NetDemo 只当学习样例 ④加 Status 节点
⑤新建可复用 `PasswordWnd`（房间密码 + 首次起名）⑥扩 DTO，开房入口 = 地图界面 `pubilc` 或设置界面 `stateWnd` 空玩家位
⑦直接 `10_UI → 02_Net` ⑧二次点击行 + 加入按钮都要 ⑨预制体由 MCP 改

### 代码

| 文件 | 改动 |
| --- | --- |
| `NetTmp/Client/RoomMeta.cs`（新） | 难度/来源/是否开局的**唯一适配点**；`#` 约定解析、按 IP 猜来源、`ComposeMapName` 都在这；三处 `TODO(库)` |
| `NetTmp/Client/NetRoomFlow.cs`（新） | 回连→入房→**超时**→事件；`Join/Leave/Host`；`OnJoinResult/OnPlayerList/OnStartGame` |
| `NetTmp/Services/Msg/RoomMsg.cs` | 新增 `HostRoomOptions`；`StartGameNtf` 扩 `Difficulty/TaskIndex/ExtraDiff/Seed/PlayMode` |
| `NetTmp/Client/NetHostSvc.cs` | `StartHost(HostRoomOptions)` 重载（旧签名保留）；难度/房主名/`_started`；`SelfHostName/HostPlayerName/Difficulty/IsStarted`；`StartGame(...)` 带全量配置；房主名写真名；`PlayerNames` 末尾追加真名 |
| `00GameContract/Enums/WndType.cs` | 加 `Password` |
| `10UI/WndHub.cs` | `WndHub.Password` + `TypeOf` 映射 |
| `10UI/Wnd/PasswordWnd.cs`（新） | 通用单行输入窗（标题/说明/占位/上限/是否密码/确认回调），队列式 |
| `10UI/Comp/ServerListPanel.cs` | `SelectedRoom` + `OnRoomActivated(room, pwd)`；加入按钮；二次点击行即加入；`Rooms/Panel/Status` 状态文案；难度/来源/开局改走 `RoomMeta`；签名纳入新字段 |
| `10UI/Wnd/SelectMapWnd.cs` | `serverPanel.OnRoomActivated` 接线；`pubilc`=开房、`join`=打开房间列表；`CreateRoom()`；`StartTask` 时房主广播 `StartGame` |
| `10UI/Wnd/SettingWnd.cs` | `stateWnd 空玩家位 → CreateRoomFromEmptySlot()`（按当前局地图/难度开房） |
| `10UI/Wnd/FrontWnd.cs` | 首次进入（`isNew`）先用 `PasswordWnd` 起名再进教学关 |
| `10UI/10_UI.asmdef` | `references` 加 `02_Net` |

### 资产（MCP 改）

- `Resources/Prefabs/Manager/GameRoot.prefab`：根节点挂 `NetSvc`/`NetHostSvc`/`NetRoomFlow`（`hostPort=17666`）；`CanvasRoot/WndUI` 下新增 `PasswordWnd` 实例（inactive）
- `Resources/UI/Wnd/PasswordWnd.prefab`（新，由 TipWnd 复制改造）：删 `cost`、加 `Input (TMP)`（从 SelectMapWnd 的 InputField 复制，y=84/h=44）、`title/desc/input/inputPlaceholder/optA/optB` 已回填
- `Resources/UI/Wnd/SelectMapWnd.prefab`：`Rooms/Join`（克隆 Server，锚 `(1,0)`、`(-515,50)`、200×40）+ `Rooms/Panel/Status`（克隆 TMP，`(48,18)` 760×24）；`joinBtn`/`statusText` 已回填
- `Resources/UI/Wnd/SettingWnd.prefab`：`emptySlotRoot = ExpandRoot/stateWnd/PlayerStateSelf/FriendRoot`（3 个空位）

### 验证

- 离线编译：`02_Net` / `01_GameContract` / `10_UI` 全 **0 错误**（工具 `plans/tmp_compile_files.py`，新增 asmdef 引用用 `--ref` 注入）
- Unity：`refresh_unity(force,all,request)` → Console **0 error**；反射实测 `FPSGame.UI.PasswordWnd` / `FPSGame.Net.RoomMeta` / `NetRoomFlow` / `ServerListPanel.joinBtn+statusText+OnRoomActivated` / `NetHostSvc.StartHost(HostRoomOptions)` / `StartGame(String,Int32,Int32[],Int32,Int32)` 全部存在
- 资产核对：GameRoot 三组件在位、PasswordWnd 实例在 `WndUI` 下、两条新节点与回填引用都在；`Utnapishitim.unity` 的 `SelectMapWnd` 实例**无 override** ⇒ 继承新回填

### 仍未做（下一步）

1. **库改动**（等用户）：`LanRoomInfo` 加 `Difficulty/InGame/Source` → 只改 `RoomMeta` 三处 + `NetHostSvc` 两处 `TODO(库)`
2. **成员侧复现开局**：`NetRoomFlow.OnStartGame` 目前只抛事件（订阅者待补）：加载场景 + `TaskManager.SetTask` + `RoomManager.JoinPlayer`
3. **房间大厅 UI**：现在开房后只是打开房间列表，没有"房间内准备/开始"界面
4. **房间列表不过滤"自己开的房"**：同机自测会看到自己的房（`NetHostSvc.SelfHostName` 已备好，面板加一行过滤即可）
5. 房间密码只支持"无密码/手输"，开房入口还没做"设密码"（`HostRoomOptions.Password` 已备好）
