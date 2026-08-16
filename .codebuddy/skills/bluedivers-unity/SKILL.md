---
name: bluedivers-unity
description: Bluedivers Unity 项目专用开发指南。该 skill 在处理 Bluedivers 项目的 C# 脚本编写、Unity 组件开发、模块架构设计、ScriptableObject 数据定义、UI 窗口开发等任务时使用。提供项目特定的架构约定、命名规范、模块依赖关系、游戏系统全景与常见开发工作流。当用户在 Bluedivers 项目中编写或修改 Unity C# 代码、新增脚本或模块时触发。
---

# Bluedivers Unity 项目开发指南

## 项目环境

- **Unity**: 2022.3.62f2c1（2022 LTS），C# 9.0 / .NET Standard 2.1
- **渲染管线**: URP 14.0.12
- **网络**: Photon PUN 为旧方案（已弃用）；KCPNet 自研网络库尚未完成，当前为**单机版 demo**，暂不做联机
- **关键依赖**: TextMeshPro 3.0.9、Navigation 1.1.6、Timeline 1.7.7、ugui
- **游戏类型**: 单机版 PvE 第三人称/第一人称**射击割草** demo（对标《绝地潜兵2》+《深岩银河》融合）
- **逻辑数学**: `PEMaths`（Photon Engine 固定点数库，PEInt/PEVector），用于逻辑层确定性计算
- **编码规范**: 见 `.codebuddy/rules/UnityCSharp编码规范.md`（自动加载），编码时必须遵循

## 项目定位

以《绝地潜兵2》为蓝本、融合《深岩银河》元素的**单机版 PvE 射击割草** demo。
- **绝地潜兵元素**：战备空投/轨道打击/飞鹰空袭/SOS呼叫/探照灯（**全类别已实现**）、主/副/特殊任务、战略大地图（`MapData_SO` + `SelectMapWnd` + `ArchivesData_SO`）、平坦地形生成、撤离、欧帕兹收集、倒地呼叫队友、NPC 语音播报、昼夜循环夜间敌袭
- **深岩银河元素**：撤离机制（所有人上船才起飞，不放弃任何人）、采矿采集、巢穴破坏任务、波次防守
- 代码大量沿用 Unity 官方 FPS Sample 命名空间（`Unity.FPS.Game` 等），由 FPS Sample 二次开发而来

完整系统全景、已实现/未实现对标、架构改进建议见 `references/module-guide.md` 末尾的「项目全景总结」章节。

## 架构约定

### 解耦与事件
- **降低耦合度**：避免组件之间直接互相获取引用（如 `GetComponent<PlayerController>()`）。跨模块/跨层通知优先使用事件机制。
- 全局事件（非战斗相关）在 `GlobalEventSub` 中定义（`Assets/Scripts/01Manager/Global/GlobalEventSub.cs`）；战斗相关事件在 `BattleEventSub` 中定义。
- 新增跨多组件通知的功能时，优先在 `GlobalEventSub` 或 `BattleEventSub` 中加 `static event` 和对应的 `static` 触发方法。

### 数字前缀模块划分

脚本位于 `Assets/Scripts/`，按数字前缀编号划分模块，编号体现程序集依赖顺序。上层模块（编号大）可引用下层模块（编号小），反之禁止：

| 编号 | 目录 | 职责 |
|------|------|------|
| 00 | `00Core/` | 核心框架：单例基类、对象池 `ObjectPool<T>`、FSM、定时器、接口、自定义特性 |
| 00 | `00GameContract/` | 游戏契约层：共享接口（`I_Entity`/`I_Actor`/`I_Damagable`）、数据结构 |
| 00 | `00Tools/` | 工具类：`FpsHelper`、`SingletonNet`、测试工具 |
| 01 | `01Manager/` | 全局管理器：`GameRoot`、`ResSvc`、`BattleManager`、`MissionController`、`CoroutineSvc` |
| 02 | `02Data/` | ScriptableObject 数据定义（`_SO` 后缀） |
| 02 | `02Game/` | 游戏逻辑：Player、AI、Gameplay、Interactable、Mission |
| 04 | `04UI/` | UI 窗口与工具 |
| 08 | `08Map/` | 地图生成（地形噪声、树木生成） |
| - | `Effect/` | 特效与视觉效果 |
| - | `Feature/` | 渲染特性（雾等） |
| - | `Rendering/` | 渲染脚本 |

详细的模块说明和关键类清单见 `references/module-guide.md`。

### 程序集定义（asmdef）

项目使用 8 个 asmdef 分层管理，新增脚本须放入对应模块的 asmdef 范围内。跨模块引用须通过 asmdef 显式声明依赖，且只能引用编号更小的模块。

## 命名约定速查

| 类型 | 约定 | 示例 |
|------|------|------|
| 接口 | `I_` 带下划线前缀 | `I_Entity`、`I_Actor`、`I_Damagable` |
| ScriptableObject | `_SO` 后缀 | `RoleData_SO`、`SoundGroup_SO` |
| 私有/受保护字段 | `_` 加驼峰 | `_controller`、`_health` |
| Inspector 字段 | `[SerializeField] private` + `[InspectorName("中文")]` | 见下方示例 |
| 协程方法 | 动词开头，不用 `Coroutine` 后缀 | `InitGameState`、`WaitSetPos` |
| 枚举 | 帕斯卡命名，不加 `Enum` 后缀（新代码） | `ActorState`、`GameState` |

Inspector 字段暴露示例：
```csharp
[SerializeField] [InspectorName("状态")] private ActorState _actorState = ActorState.Normal;
```

## 开发工作流

### 新增运行时脚本

1. 根据职责确定所属模块编号（参照上方表格），放入对应目录。
2. 确认该目录是否在某个 asmdef 范围内，若是则自动归属该程序集。
3. 文件名与主类名完全一致，编码 UTF-8 with BOM。
4. 私有字段用 `_` 前缀，Inspector 暴露用 `[SerializeField] private`。
5. 运行时脚本**不得** `using UnityEditor`，编辑器专属代码用 `#if UNITY_EDITOR` 包裹。
6. 遵循 `.codebuddy/rules/UnityCSharp编码规范.md` 中的全部规则。

### 新增 ScriptableObject 数据

1. 脚本放入 `02Data/`，文件名以 `_SO` 结尾。
2. 使用 `[CreateAssetMenu]` 特性便于在编辑器中创建实例。
3. 运行时通过 `ResSvc` 加载，不要在运行时修改 SO 资产文件。
4. 如需在 Inspector 中使用 `UnityEditor` API（如 `EditorGUI`），必须放在 `Editor/` 目录下并用 `#if UNITY_EDITOR` 保护。

### 新增 UI 窗口

1. 脚本放入 `04UI/`，遵循 `04_UI` 或 `00_WndTools` 程序集。
2. 窗口类通常继承项目中的窗口基类（查看 `04UI/` 下现有窗口类确定基类）。
3. 异步逻辑（如延迟关闭、动画等待）使用协程，不使用 `async/await`。

### 新增管理器

1. 放入 `01Manager/`，按子领域分目录（`Global/`、`Battle/`）。
2. 实现核心接口 `I_GlobaManager`（注意带下划线前缀），在 `GameRoot` 中注册。
3. 初始化逻辑放在 `Awake`/`Start` 或 `Init` 协程中。

### 新增协程

1. 方法名以动词开头（如 `InitGameState`、`DownloadAudio`），不加 `Coroutine` 后缀。
2. 通过 `CoroutineSvc.StartCoroutine` 或 `MonoBehaviour.StartCoroutine` 启动。


## 网络层（暂不做联机）

> 当前为**单机版 demo**，暂不做联机。Photon PUN 已弃用，KCPNet 自研网络库尚未完成。目标网络架构：轻服务器（仅房间列表）+ 本地存储。

### 现有网络基础设施（保留备用）
- `00Tools/SingletonNet.cs` — 网络单例基类（旧封装 `MonoBehaviourPunCallbacks` 的 RPC 重载）
- `01Manager/Global/NetManager.cs` — 继承 `SingletonNet<NetManager>`，驱动 `I_Login` 固定逻辑帧 `LogicTick`
- `00Tools/LogicBehaviour.cs` — 固定逻辑帧基类
- `PEMaths`（PEInt/PEVector 定点数）— 逻辑层确定性计算，避免浮点同步不一致

### 业务代码中的 Photon 依赖点（未来迁移时参考）
| 文件 | 依赖情况 |
|------|---------|
| `00Tools/SingletonNet.cs` | RPC 封装基类 |
| `01Manager/Global/NetManager.cs` | 继承 SingletonNet |
| `01Manager/Bridge/BridgeSys.cs` | `SendPlayerSelectArmament`/`SendPlayerReady` 两个 RPC |
| `04UI/BridgeWnd.cs` | 仅 using Photon，无实际调用 |
| `04UI/ArmamentWnd.cs` | 仅 using Photon，无实际调用 |

### 未来网络开发约定
- 战斗逻辑（AI 出生、随机种子、伤害）走**确定性逻辑帧**，用确定性随机源。
- `I_Actor`/`I_Entity` 已有 `LogicPos`/`Logic3Pos` 定点坐标，可直接作为同步载体。
- 网络传输只走定点数/ID，Unity 类型（GameObject/Vector3）仅在表现层恢复。

## 性能注意事项

- `Update`/`FixedUpdate`/`LateUpdate` 等高频方法中禁止 `new` 分配、字符串拼接、`GetComponent`/`FindObjectOfType`/`Find`，引用在 `Awake`/`Start` 缓存。
- 组件引用在 `Awake`/`Start` 中缓存。
- 频繁创建/销毁的对象使用 `00Core/ObjectPool<T>` 对象池。
- 标签比较使用 `CompareTag`，大数组遍历用 `for` 而非 `foreach`。

## 已知问题与避坑

- ~~运行时误引 UnityEditor~~：已于 2026-08-07 修复（`EnemyMobile.cs`、`RoleData_SO.cs`、`SoundGroup_SO.cs`、`TaskManager.cs` 添加 `#if UNITY_EDITOR` 守卫）。
- **SingletonNet 的 RPC 封装有误**：`RPC(nameof(action), ...)` 中 `nameof(action)` 取到的是局部参数名 `"action"` 而非委托目标方法名（第 63 行才用 `action.Method.Name`），说明该 RPC 封装未真正跑通。
- **确定性随机源未统一收口**：`BattleRandom`（`BattleManager`）与 `WaveManager` 各自的 `System.Random` 并存。将来做联机时需统一收口到单一确定性随机源。
- **接口命名不统一**：历史代码中少量接口使用标准 `I_` 前缀，新接口必须使用 `I` 前缀。
- **字段暴露方式不统一**：存量 `public` 字段较多，新代码优先 `[SerializeField] private`，存量逐步迁移。
- **枚举后缀不统一**：部分枚举带 `Enum` 后缀（`GameStateEnum`），新代码加后缀。

## 关键基础设施

开发前了解以下已有设施，避免重复造轮子：

| 设施 | 位置 | 用途 |
|------|------|------|
| `ObjectPool<T>` | `00Core/` | 泛型对象池，实现 `IRecyclable` 接口配合使用 |
| `GameRootBase` / `GameRoot` | `00Core/` → `01Manager/Global/` | 游戏入口与全局管理器注册中心 |
| `ResSvc` | `01Manager/Global/` | 资源加载服务（含音频下载） |
| `CoroutineSvc` | `01Manager/Global/` | 协程管理服务 |
| `ArchiveSvc` | `01Manager/Global/` | 存档服务 |
| `InputManager` | `01Manager/Global/` | 输入管理 |
| `BattleManager` | `01Manager/Battle/` | 战斗流程管理（含 `BattleRandom` 确定性随机源） |
| `MissionController` | `01Manager/Battle/` | 任务系统管理 |
| `TickBehaviour` / `I_TickClass` | `00Core/Timer/` | 自定义 Tick 系统 |
| `LogicTimerSystem` / `ViewTimerSystem` | `00Core/Timer/` | 双层定时器（固定逻辑帧 20ms + 表现帧） |
| `FsmSystem` / `IState<T>` | `00Core/` | 泛型状态机框架 |
| `CustomAttribute` | `00Core/Attribute/` | 项目自定义特性 |
| `LogicBehaviour` / `I_Login` | `00Tools/` | 固定逻辑帧基类（帧同步基础） |
| `SingletonNet` / `NetManager` | `00Tools/` → `01Manager/Global/` | 网络单例 + 逻辑帧驱动（当前单机版，暂不联机） |
| `WaveManager` | `01Manager/Bridge/` | 波次刷怪调度（已拆分为 `WaveManager.cs` + `ZergWave.cs` + `RobotWave.cs`） |
| `UnitQueryGrid` | `02Game/` | 空间网格优化单位检索（割草大数量敌人） |
| `PEMaths` | `Assets/Scripts/Lib/PEMaths.xml` | 固定点数数学库（PEInt/PEVector），逻辑层确定性计算 |
| `GenerateNoiseTerrain` | `08Map/` | fBm 噪声地形生成（分块提交、树木植被、NavMesh） |
| `GameAttribute` / `UnitAttributeFactory` | `02Game/` | 属性系统：Modifier 叠加、双属性链（直接/爆炸）、OnFinalValueChange |
| `WeaponController` 体系 | `02Game/Game/Shared/Weapon/` | 30+ 武器脚本：弹匣/过热/蓄力/充能、多种伤害类型、武器升级模块 |
| `SOPickerPopup<T>` | `Assets/Editor/SOPickerPopup.cs` | **泛用 SO 选择弹窗框架**（PopupWindowContent）。做"可搜索的列表选择器"时**优先复用**，不要再自造：数据由委托注入（icon/name/type/color/frame），`confirmMode` 控制"单击即选"或"确定/取消确认"。用法见下 |

### 复用约定：SO 选择弹窗（SOPickerPopup）

需要"从一堆数据里选一个并回调"的编辑器 UI 时（如给某字段选 SO/预制体、从子资源里挑一个），**直接复用** `SOPickerPopup<T>`（`Assets/Editor/SOPickerPopup.cs`，全局命名空间），不要重复实现搜索列表弹窗。

- 注入数据 + 显示委托，用 `PopupWindow.Show(rect, ...)` 锚定在某控件上弹出：
  ```csharp
  PopupWindow.Show(activatorRect, new SOPickerPopup<MySO>(
      items,                                  // List<MySO>
      so => { /* 选中回调 */ },
      so => so.icon,                          // 图标(Func<T,Sprite>)，可 null
      so => so.name,                          // 名称
      so => so.typeName,                      // 次级类型行，可 null(不显示)
      getTypeColor: so => so.color,           // 可选：类型文字颜色
      getFrame: so => (path, color),          // 可选：图标边框
      confirmMode: true                       // true=单击选中+确定/取消确认; 默认 false=单击即回调
  ));
  ```
- `confirmMode=false`（默认）：单击条目立即回调并关闭（武器升级/模组选择器用此模式）。
- `confirmMode=true`：单击仅选中高亮，双击或"确定"确认，可"取消"（RoleData_SO.speechGroups 的语音子资源选择用此模式）。
- 过滤/子资源筛选由调用方注入数据前完成（如 `AssetDatabase.LoadAllAssetsAtPath(path).OfType<SoundGroup_SO>()`），框架只负责展示与搜索。

现有调用方：`WeaponUpgradeEditorWindow.cs`（`ShowUpgradePicker`/`ShowModulePicker`）、`RoleSpeechGroupDrawer.cs`（speechGroups 添加）。

详细说明见 `references/module-guide.md`。
