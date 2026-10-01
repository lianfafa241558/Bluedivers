# 主题 · 契约层 / 能力下沉 / 服务接缝 / 枚举与序列化

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。

## 契约层基本盘
- `01_GameContract`（41 cs）references 只有 `00_Core` + `00_Attribute`
- **准入「三进三出」**：进 = 服务接口 / 跨层封闭枚举 / 纯数据 DTO；出 = 单链消费者随生产层 / 表现概念拆查表 / 引擎编辑器设施
- **反向边界**：`GameObject`/`Sprite` 等引擎类型出现在服务/实体接口里是**本质**，不算污染

## 三种"能力下沉"手法（可复用）
1. **数据自持** — 纯数据 → `XxxData_SO.Current` 或 `FPSGame.Data.*State`。
   ⚠ **只适合"一处写、多处读"**；会变的值必须每个变更点 `SyncXxx()`；⚠ 静态字段**不随新实例归零** ⇒ 宿主 `Awake` 必须显式 `Reset()`。
   反例 `WaveCount`：真相是 `WaveManager.WaveCount => ticks.Count-1`（会**递减**），且 `ticks` 由 `00_Core` 的 `TickBehaviour` 增删（跨层写入）⇒ 快照必然漂移，只能留契约。
2. **Core 中性原语** — 需要返回值 / 消费者含静态类 ⇒ `00_Core` 加静态类 + `Sink`（`internal set` + `InternalsVisibleTo`）+ 窄接口 `IXxxSink`；宿主 `Init` 接管、`OnDestroy` 用 `ReferenceEquals` 归还。
   **准入**：满足"**设施与入口同层**"或"宿主唯一且有明确制造者"，并写清语义边界与退出条件。
3. **事件下沉** — 无返回值的命令类调用 → 静态事件总线。**层 = `min(发布者层, 订阅者层)`**；订阅方必须在 `if (Instance != this) return;` **之后**订阅、`OnDestroy` **成对退订**；"无订阅者 = 静默丢弃" ⇒ 调用点可顺带删掉存在性守卫。

## 已落地的 Core 受控原语（4 例，同款形态）
`LogicFrame`（逻辑帧）· `VfxPool`（池入口）· `TimerHost`（计时/协程）· `UnitQuery`（05_UnitCore 单位查询，`InternalsVisibleTo("09_Managers")`）

## `BattleHub.Current`（原 `ServiceLocator`，已降格改名）
- 落 `00GameContract/Services/BattleHub.cs`；`NullServices` 并入为 `internal sealed class NullBattleService`
- **四轮瘦身已收官：契约 19 → 5 成员，消费点 79 → ~21**
- ⚠ **接缝删不掉**：剩余 5 成员全是"需要返回值/回调" —— `CreatWave`(bool) / `ReleaseAirdrop`(`Action<GameObject>`) / `CreatPatrol`(`List<GameObject>`) / `WaveCount`(会递减，不适合快照) / `IsPresent`(存在性判断)。**它们是合理保留，不是欠债。**
- 判据：`Battle` 是**战斗系统门面**（非单一能力 ⇒ 只能按成员分而治之）；`06_Gameplay.asmdef` **不引用** `09_Managers` ⇒ 57 处编译期写不出 `BattleManager.Instance` ⇒ 槽是**结构刚需**
- 历史注释里 52 处 `ServiceLocator.X` **刻意保留**（记录沿革，不是活引用）
- 已删 9 槽与各自落法明细 → 见 `2026-10-01.md`

## 接缝类判据（通用）
- ⚠ **删槽 ≠ 删契约**：**"槽位"是查找机制、可以删；"契约"是类型，只要有注入链就必须留**。`Res` 槽删了但 `IResService` 必须留（`ResSvc` 实现 / `MissionController` 注入 / `MissionBase` 持有 / `IResConsumer.Inject` 接线口）
- ⚠ **"接口只开实测被调用的成员"**：判据 = 存在 `变量.成员` 形式的**接口**调用；上层直连实现的调用**不计**（据此摘掉过 `LoadSprite`）
- ⚠⚠ **契约替换铁律**：存在性判断 ≠ 状态判断（`X.Instance!=null` 换 `IsPresent`，**不能用状态位** —— 静默跳过、编译无错）⇒ 批量替换后**必须 Play 冒烟**
- ⚠ 消费者能直接引用实现类时（同层/上层），`BattleManager.Instance` 这类**合法且大量存在**；但"给 `IXxxService` 加静态 `Instance` 假装单例"不行（写保护做不出、丢 `Dump()`、契约层出现可变全局态）
- ⚠ 两个"删了必炸"：`Archive` 必须保留 `I_GlobaManager`（`GameRootBase.Awake` 靠 `GetComponents<I_GlobaManager>()` 装配）；`UIState.WindowState` 初值必须 `Game`（枚举 0 值是 `All`，取错锁死输入）
- ⚠ 遗留：19 处恒真 `?.`/`!= null` 死代码；`RoomManager.Init:61-62` 直接解引用 `ArchivesData_SO.Current` 无判空（靠装配顺序活着，脆弱）

## UI / 窗口（Wnd 槽收尾 · 2026-10-01）
- **`ServiceLocator` 已整体删除**（含 `Wnd` 槽），`IWindowService` 契约也删。原 `WndManager` 持有的 10 个具体窗口字段（`operationWnd`…`settingWnd`，属 UI 类型 ⇒ 反向依赖）已迁出。
- **当前架构**：
  - 玩法层开窗 = 发 `GlobalEventSub.OpenWnd(WndType)`（无返回值命令走事件，事件层 = `06_Gameplay`）
  - `WndManager`（09_Managers）订阅 → 转调 `WindowRegistry`（契约层委托插槽）
  - 窗口实例在 `WndHub`（10UI 的 `internal static class`）`map<WndType,Window>` 字典；`Window.Awake` 自注册 + `Scan(true)` 兜底（`Scanner` 经静态构造 + `[RuntimeInitializeOnLoadMethod]` 双保险）
  - `WndHub` 把能力（SetWndState / IsOpen / CreatNotice / CreatCountDown / ClearNotice）以**委托**登记进 `WindowRegistry`
  - `WindowState` 状态**数据自持**到 `04Data/UIState.cs`（玩法层输入门控读 `UIState.WindowState`，初值必须 `Game`，枚举 0 值是 `All`）
  - 弹提示 = `GlobalEventSub.Notice`（同链路）；倒计时 = `WindowRegistry.CountDown`
  - ⚠ `WndHub` 是 `internal`：现 `10_UI` 已是独立 asmdef ⇒ **编译期牙齿已生效**（`WndHub.cs:31-33` 注释写"在 Assembly-CSharp 里"已过时）
- **层判据**：`09_Managers` 不能引用 `10_UI` ⇒ 经契约层 `WindowRegistry` 委托中转切断反向依赖；`WindowRegistry` 的"退出条件（删类）"**未到**（`10_UI` 仍不可反向被 `09` 引用）
- **`WndType`（契约层枚举，原 `WndTypeEnum`）**：跨层开窗唯一钥匙；具体 UI 类型**只在 `WndHub`/`10UI`**；新增窗口**只改 `WndHub.TypeOf` 一处**映射（`WndType.cs` 注释写"WndManager.SetWndState 的映射"已过时）
- ⚠⚠ Unity 不为 `SetActive(false)` 对象调 `Awake` ⇒ 自注册死锁；兜底见上；**`Window` 子类禁定义 `Awake`**（替代 = `FirstShowWnd()`）
- 往老文件插 Unity 特性前先确认有 `using UnityEngine;`

## Wnd 槽收尾 · 剩余清单（待执行，尚未改代码）
1. **编译红（阻塞）**：`00Tools/WndRootTool.cs` 用 `TMPro` 但 `00_Utils.asmdef` 未引用 `Unity.TextMeshPro` ⇒ 2×CS0246。修：给 `00_Utils.asmdef` references 加 `"Unity.TextMeshPro"`。
2. ~~asmdef 悬空引用~~ ✅ **已执行（2026-10-01）**：从 10_UI/06_Gameplay/09_Managers/10_Effect 的 references 删 `00_WndTools`，从 10_UI 删 `04_UI`；asmdef 内 `00_WndTools`/`04_UI` 已归零，`.cs` 无残留引用（仅 `VehicleWeaponsManager.cs:3` 一行过时注释提及，非代码）。
3. **死代码链**：`WndManager.IsWndOpen`（0 调用）+ `WindowRegistry.GetOpen`/`IsOpen` + `WndHub.Bind()` 的 `IsOpen` 登记 → 整条删（无跨层消费者）。
4. **UI 层直连 `WndManager`**（合法但风格不统一，可选改事件）：`BridgeWnd:98`/`DeathUI:74`/`SettingWnd:582`（`CreatNotice`/`CreatCountDown`/`ClearNotice`）+ `10_Effect/VFXAirdropEffect:551`（`CreatNotice` → 可改 `GlobalEventSub.Notice`）。
5. **过时注释清理**：`WndHub.cs:31-33`（`04UI` 在 Assembly-CSharp → 已独立 `10_UI`）；`WndType.cs`（`WndManager.xxxWnd` 字段已删、"两处同步"应指 `WndHub.TypeOf`）；`WndManager.cs`（`01Manager`→`09Manager`、`04UI`→`10UI`、`SetWndState 的映射`→`WndHub.TypeOf`）。
6. **`WndManager` 死代码**：空 `Start()` 方法 + 注释掉的 `CreatSpeech` 块 → 删。
- ⚠ 玩法层残留 `WndManager` 引用**全是注释/被删代码**（`Furniture_General`/`Furniture_AttachedGeneral`/`MissionEvacuateStatic` 的注释；`WeaponPlayerController:266` 在 `/* */` 块；`InputManager:39` 已改 `UIState.WindowState`）→ 不需改。
- ⚠ **改 asmdef 会触发 Unity 重编译**（用户前台即时重编）→ 执行前须告知用户。

## 枚举与序列化约定（2026-10-01 定）
- ⚠ 本项目枚举**重度序列化**（`.asset`/`.prefab`/`.unity` 里按 **int** 存：`terrainType`/`sizeType`/`missionTag`/`tier`/`difficulty` …）⇒ **成员声明顺序就是取值**，新增只能**追加在末尾**，禁止中间插入/删除
- ⚠ 显式赋值的成员不会跟着漂移，隐式连续的会 ⇒ **同一枚举里别混用**（`MissionEnum` 是"0 起自增段 + 100/200/300 三段显式"的分段式，段内可加、段尾可加、跨段不可乱序）
- 纯排版整理的安全做法：写脚本与 `git HEAD` **逐枚举比对「成员名序列」**（工具 `.codebuddy/plans/tidy_contract_enums.py`，输出 `语义差异项` 必须为 0）
- 具体枚举所在位置：跨层封闭枚举放 `00GameContract/Enums/`；`00_Core/CoreEnums.cs` 是 Core 层枚举大本营（`WindowStateEnum`/`UnitTypeEnum`/`GameStateEnum`/`EnemyType`…）
- ⚠ `[InspectorName]` = `UnityEngine.InspectorName`（`FPSGame.Attribute` 里另有同名/近似特性，别混）
- ⚠ 资产 YAML 缺整行字段 = 脚本给类新增字段后**没有重存资产**；新字段加在**结构体/类末尾**最稳
