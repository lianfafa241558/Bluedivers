---
name: bluedivers-unity
description: Bluedivers Unity 项目专用开发指南。该 skill 在处理 Bluedivers 项目的 C# 脚本编写、Unity 组件开发、模块架构设计、Photon PUN 网络功能、ScriptableObject 数据定义、UI 窗口开发等任务时使用。提供项目特定的架构约定、命名规范、模块依赖关系和常见开发工作流。当用户在 Bluedivers 项目中编写或修改 Unity C# 代码、新增脚本或模块、处理 Photon 网络逻辑时应触发此 skill。
---

# Bluedivers Unity 项目开发指南

## 项目环境

- **Unity**: 2022.3.62f2c1（2022 LTS），C# 9.0 / .NET Standard 2.1
- **渲染管线**: URP 14.0.12
- **网络**: Photon PUN（非 Unity Netcode / Mirror）
- **关键依赖**: TextMeshPro 3.0.9、Navigation 1.1.6、Timeline 1.7.7、ugui
- **游戏类型**: 3D FPS（命名空间含 `FpsGame`）
- **编码规范**: 见 `.codebuddy/rules/UnityCSharp编码规范.md`（自动加载），编码时必须遵循

## 架构约定

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


## Photon PUN 约定

- Photon 相关脚本位于 `Assets/Photon/`，使用独立的 asmdef。
- 运行时网络同步逻辑与本地逻辑解耦，便于离线测试。
- RPC 方法用 `[PunRPC]` 标记，方法名使用帕斯卡命名法。
- 跨网络同步字段需评估带宽，避免每帧同步大对象，优先事件驱动。

## 性能注意事项

- `Update`/`FixedUpdate`/`LateUpdate` 中禁止 `new` 分配、字符串拼接、`GetComponent`/`Find`。
- 组件引用在 `Awake`/`Start` 中缓存。
- 频繁创建/销毁的对象使用 `00Core/ObjectPool<T>` 对象池。
- 标签比较使用 `CompareTag`，大数组遍历用 `for` 而非 `foreach`。

## 已知问题与避坑

- **运行时误引 UnityEditor**：`00Core/ObjectPool.cs`（`using static UnityEditor.Progress;`）和 `02Data/` 下多个 `_SO` 脚本存在 `using UnityEditor`，会导致非 Editor 平台构建失败。新增代码严禁此问题，存量代码在接触时优先清理。
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
| `BattleManager` | `01Manager/Battle/` | 战斗流程管理 |
| `MissionController` | `01Manager/Battle/` | 任务系统管理 |
| `TickBehaviour` / `I_TickClass` | `00Core/Timer/` | 自定义 Tick 系统 |
| `FsmSystem` / `IState<T>` | `00Core/` | 泛型状态机框架 |
| `CustomAttribute` | `00Core/Attribute/` | 项目自定义特性 |

详细说明见 `references/module-guide.md`。
