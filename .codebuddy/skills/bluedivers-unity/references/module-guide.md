# Bluedivers 模块详细指南

本文件为 SKILL.md 的补充参考，按需加载。

## 00Core/ — 核心框架

最底层模块，被所有上层模块引用。包含项目基础设施。

### 关键文件

- `ObjectPool.cs` — 泛型对象池 `ObjectPool<T>`，配合 `IRecyclable` 接口使用。提供 `_Add`/`_Pop`/`_Push` 委托定制行为。
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

注意：`using UnityEditor` 已于 2026-08-07 添加 `#if UNITY_EDITOR` 守卫。

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

---

# 项目全景总结

> 2026-08 调研产出，2026-08-07 修正。定位：单机版 PvE 第三人称/第一人称**射击割草** demo，对标《绝地潜兵2》+《深岩银河》融合。

## 已实现系统（扎实的核心战斗层）

| 系统 | 实现 |
|------|------|
| 伤害模型 | `I_Damagable`/`Damageable`：弱点部位、多部位护甲、关联护甲（`TransferDamageable`）、护甲破坏/流血、护盾/生命、抗性 |
| 属性系统 | `GameAttribute`/`UnitAttributeFactory`：Modifier 叠加、`OnFinalValueChange`、双属性链（直接/爆炸） |
| 武器系统 | `WeaponController` 体系 30+ 脚本：弹匣/过热（`OverheatBehavior`）/蓄力/充能、多伤害类型（枪/爆炸/破坏/真伤/毒/燃烧/冻结/电/眩晕/恐怖/辐射/黑客）、升级/模块（`WeaponUpgradeController`）、补给弹药 |
| 投射物 | `ProjectileBase`、`Gameplay/Projectile/`（标准/延迟炸弹/制导炮击 `GuidedShelling`） |
| 玩家 | 第一/第三人称切换、冲刺、倒地呼叫、死亡/复活无敌、后坐力、多武器管理、载具（`VehicleController`/`IDrivable`）、装备（`Jetpack`/`ShieldBag`） |
| AI | `AIController`/`EnemyController`/`DetectionModule`/状态机（`EnemyMobile`）/技能（`UnitSkill_Base`：闪烁/呼叫增援/冲撞/召唤师）、BOSS 单位 |
| 波次 | `WaveManager`（已拆分为 `WaveManager.cs` + `ZergWave.cs` + `RobotWave.cs`）：人口值权重随机刷怪，Zerg 虫潮 / RobotWave 机器人（飞鹰运兵、人口 16/船），多难度倍率，守卫战 |
| 单位检索 | `UnitQueryGrid` 空间网格 + `UnitQueryGridDebugger` |
| 地形 | 绝地潜兵式平坦地形，`GenerateNoiseTerrain`：7 种地形预设、fBm 分形噪声、侵蚀后处理、分块提交、树/草植被、NavMesh 异步构建。**不做洞穴/矿洞体素系统** |
| 任务 | `MissionBase`/`MissionController`：主/支/巢穴/子/撤离五类、`MissionTag` flags、战备授权范围、任务进度/提示/图标 |
| 天气/昼夜 | `DayNightCycle`、白天/夜间敌袭切换（`OnDaySwitch`） |
| 战备 | **全类别已实现**：`AirdropController`/`AirdropWnd`（4 槽位战备、轨道打击/照明弹/探照灯/飞鹰/SOS 等） |
| 战略大地图 | `MapData_SO`（模板）+ `SelectMapWnd`（展示）+ `ArchivesData_SO`（存值），单机值；暂无游戏胜利修改解放度 |
| 撤离 | 类似深岩银河机制：所有人上船才起飞（不放弃任何人），无撤离失败惩罚 |
| 框架 | 事件驱动（`GlobalEventSub`/`BattleEventSub` static event）、管理器注册中心（`I_GlobaManager`）、双层定时器、固定逻辑帧 `LogicBehaviour`、固定点数 `PEMaths`、对象池、FSM、存档、音频、UI 窗口管理 |

## 尚未实现（对标两款游戏）

**对标《绝地潜兵2》缺失：**
1. **重型部署类战备**：缺机甲（Exosuit）、哨戒机枪、炮塔部署。战备类别全但缺重型部署。
2. **大战略/任务链**：无解放度进度推进、无任务链。
3. **联机**：当前单机版 demo，KCPNet 未完成暂不迁移。目标轻服务器（仅房间列表）+ 本地存储。

**对标《深岩银河》缺失：**
1. **洞穴系统**：地图是平坦地形（绝地潜兵式），**不做** 3D 洞穴/矿洞/挖掘。
2. **矿物货运链**：规划中——给 `SpecUnitKei` 加 furniture 交互 + 玩家 OOPart 拾取组件（有携带上限），必须交给 Kei。

**通用缺失：**
1. 玩法循环打磨："任务→战备→结算→强化"的 Roguelite/成长闭环是否完整需确认。

## 架构改进建议（按优先级）

1. **统一确定性随机源**：`BattleManager.BattleRandom` 与 `WaveManager` 的 `System.Random` 收口到单一确定性随机源。
2. **解耦 `I_Damagable`/`I_Entity` 与 Unity 类型**：`InflictDamage` 混用 `GameObject`/`Vector3`/`PEVector3`/`Transform`。定义纯逻辑 DTO（`DamagePacket`/`SpawnCommand`）。
3. **统一命名空间 + asmdef rootNamespace**：仅 `00_Core`/`08_Map` 设了 rootNamespace。
4. **重写 SingletonNet RPC 封装**：`nameof(action)` 取到参数名而非目标方法名，未真正跑通。
5. **God Class 审查**：`BattleManager`（350+）、`PlayerController`（550）、`WeaponPlayerController`。
6. **`UnitQueryGrid.FindUnits` 返回 `List` 每帧分配**：改结构体迭代器/池化。
