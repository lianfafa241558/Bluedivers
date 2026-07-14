# Bluedivers 模块详细指南

本文件为 SKILL.md 的补充参考，按需加载。

## 00Core/ — 核心框架

最底层模块，被所有上层模块引用。包含项目基础设施。

### 关键文件

- `ObjectPool.cs` — 泛型对象池 `ObjectPool<T>`，配合 `IRecyclable` 接口使用。提供 `_Add`/`_Pop`/`_Push` 委托定制行为。
  - 已知问题：存在 `using static UnityEditor.Progress;` 误引用，需清理。
- `GameRootBase.cs` — 游戏根基类，管理器注册与生命周期。
- `FsmSystem.cs` — 泛型有限状态机，`IState<T> where T : Enum` 定义状态接口。
- `DisplayDic.cs` — 可序列化字典实现。
- `ArchivesDataBase_SO.cs` — 存档数据库 SO。

### 子目录

- `Interfaces/` — 核心接口定义（`I_GlobaManager`、`I_Entity`、`IRecyclable`、`IPhysical`）
- `Timer/` — `TickBehaviour`、`I_TickClass` 自定义 Tick 系统
- `Attribute/` — `CustomAttribute` 项目自定义特性

## 00GameContract/ — 游戏契约层

共享接口与数据结构定义，被游戏逻辑层和核心层共同引用。

### 关键内容

- `Interface_Game.cs` — 游戏层核心接口：
  - `I_Entity` — 实体基础接口
  - `I_Actor : I_Entity` — 角色接口
  - `I_Damagable` — 可受伤接口
  - `I_MissionPoint : I_Entity` — 任务点接口
  - `I_Locatable` — 可定位接口
  - `VfxEffect` — 特效接口（注意：无 `I` 前缀，历史遗留）
  - 结构体：`UnitQueryGridNode`、`RuntimeSoundData`

## 00Tools/ — 工具类

### 子目录

- `Test/` — 测试与调试工具（`ExchangeTransformManager`、`DisplayBone`、`DrawLabelUtils`、`CopyUtils`），有独立 asmdef `00_Utils`
- `FpsHelper.cs` — FPS 显示工具
- `SingletonNet.cs` — 网络单例基类
- `LogicBehaviour.cs` — 逻辑行为基类，定义 `I_Login` 接口

## 01Manager/ — 全局管理器

### Global/ 子目录

- `GameRoot.cs` — 游戏根，管理器注册中心，游戏状态机（`GameStateEnum`），初始化协程 `InitGameState()`
- `ResSvc.cs` — 资源加载服务，含音频下载（`DownloadAudio` 协程）、延迟调用（`DelayedInvoke`）
- `CoroutineSvc.cs` — 协程管理服务，静态 `StartCoroutine`
- `ArchiveSvc.cs` — 存档服务，`SyncDefaultSettings` 协程
- `InputManager.cs` — 输入管理（含 `#if UNITY_EDITOR` 保护的编辑器逻辑）

### Battle/ 子目录

- `BattleManager.cs` — 战斗管理器，`Init()` 和 `InitTerrain()` 协程
- `MissionController.cs` — 任务系统，`InitializeAsync()`、`WaitForInitialization()`、`CreatMission()`、`InitAllMission()`、`InitInterestPoint()` 协程

## 02Data/ — ScriptableObject 数据

所有游戏配置数据 SO，文件名 `_SO` 后缀。

### 已有 SO

- `RoleData_SO.cs` — 角色数据
- `SoundGroup_SO.cs` — 音效组数据
- `NoticeTree_SO.cs` — 通知树数据
- 其他配置 SO

注意：多个 SO 脚本存在 `using UnityEditor` 误引用，需清理。

## 02Game/ — 游戏逻辑

最大的逻辑模块，按功能细分：

### 子目录结构

- `03Player/MainController/` — 玩家控制器（`PlayerController`、`VehicleController`，后者定义 `IDrivable` 接口）
- `AI/` — AI 系统
  - `Controller/` — `AIController`（定义 `I_AIController` 接口）
  - `StateMachine/` — `EnemyMobile` 等
  - `Skill/` — `UnitSkill_Base`（`m_Controller` 受保护字段）、`SympatheticDetonation`
  - `FxCont/` — 敌人特效控制（`EnemyControllerFX`、`EnemyFXControllerUnit`、`RendererSet`）
- `Game/` — 核心游戏对象
  - `Actor.cs` — 角色基类，`[InspectorName]` 中文化，`WaitSetPos` 协程
  - `Shared/` — 共享组件（`Health`、`Damageable`、`Weapon/` 武器系统）
  - `MissionView.cs` — 任务视图
  - `UnitQueryGridDebugger.cs` — 查询网格调试器（含 `#if UNITY_EDITOR`）
  - `DebugUtility.cs` — 调试工具（含 `#if UNITY_EDITOR`）
  - `PrefabReplacerOnInstance.cs` — 实例化时替换预制体
- `Gameplay/Projectile/` — 投射物（`ProjectileStandard`、`ProjectileDelayBomb`，含 `DelayedRelese` 协程）
- `05Interactable/` — 可交互对象（`Furniture_Base`、`Furniture_Attached` 定义 `IFurniture`、`KeyScreen`）
- `Interface/` — `IEquippable` 接口
- `Util/` — `GameMenuUtil`（含 `#if UNITY_EDITOR`）

## 04UI/ — UI 窗口与工具

### 子目录

- `Assembly/` — `04_UI` asmdef
- `WndTool/` — `00_WndTools` asmdef，窗口工具
- `UI/` — `CanvasController`（`_OnGameStart` 协程）
- 根目录窗口：`PlayerWnd`、`TipWnd`、`MissionCompleteWnd`（`CloseWndAfterDelay` 协程）、`GameEndWnd`（`DisplyLeft`/`DisplyRight`/`DisplyMiddle` 协程）

## 08Map/ — 地图生成

- `GenerateNoiseTerrain.cs` — 噪声地形生成，大量协程：`ApplyFractalNoiseToTerrain`、`GenerateBaseTerrain`、`ApplyErosionEffect`、`ApplyTextures`、`ApplyHeightsInChunks`、`ApplyAlphamapsInChunks`、`SpawnTrees`
- asmdef `08_Map`，rootNamespace `FpsGame.MapUtils`

## Effect/ — 特效

### 子目录

- `EffectComp/` — `05_EffectComp` asmdef，`Wreckage`（含 `#if UNITY_EDITOR`）
- `ModifyTerrain.cs` — 地形修改（`Modify` 协程）
- `DayNightCycle.cs` — 昼夜循环（`SetDayState` 协程）

## Feature/ — 渲染特性

- 雾效等渲染特性脚本

## Rendering/ — 渲染脚本

- `NormalizeMeshNormals.cs` — 网格法线归一化（含 `#if UNITY_EDITOR`）

## Photon/ — Photon PUN

- 独立 asmdef 程序集（多个）
- PUN 网络相关脚本与资源
- 不在 `Scripts/` 目录下，独立管理

## Editor/ — 编辑器脚本

- `Assets/Editor/` — 项目自定义编辑器扩展
- 独立 asmdef
- 运行时脚本不得引用此目录内容
