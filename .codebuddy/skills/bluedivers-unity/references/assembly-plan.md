# Bluedivers 工程全景与 asmdef 拆分方案

> 生成：2026-09-30。数据来源 = 全目录代码盘点 + 全量类型引用矩阵（`.codebuddy/plans/AsmDepScan.ps1`）。
> 重跑命令：`powershell -NoProfile -ExecutionPolicy Bypass -File .codebuddy/plans/AsmDepScan.ps1 > .codebuddy\plans\dep_matrix.txt`
> ⚠ 矩阵是**文本级**统计（含注释/字符串），只用于判断耦合**量级与方向**；是否能编译由 asmdef 的 GUID 引用决定，两者冲突时以编译为准（已抽检确认 `00_Utils→08_Map`、`DayNightSystem→02Data/01Manager`、`Rendering→DayNightSystem` 等均为注释噪声）。

---

## 0. 一页速览

| 项 | 数值 |
|---|---|
| 脚本总量 | ~~436~~ ⇒ **497**（`Assets/Scripts`）+ **47**（`Assets/Editor`） |
| 自有 asmdef | ~~13~~ ⇒ **28**（+ 第三方 `MackySoft`/`RootMotion`/`NavMeshComponents`/`DynamicBone` 等） |
| 落在 `Assembly-CSharp` 的 | ~~≈330 个 cs~~ ⇒ **0** —— `Assembly-CSharp` 与 `Assembly-CSharp-Editor` **都已不存在**（实测 `CompilationPipeline` 的预定义程序集数 = 0） |
| 契约层现状 | `01_GameContract` **40 cs**，分 `Services/`(11) `Entities/`(12) `Data/`(6) `Enums/`(9) + 根 2（`ServiceLocator`/`WindowRegistry`）；**无 MonoBehaviour、无表现概念** |
| 编译现状 | Console **0 error**；`Missing types` warning = **0**（`[SerializeReference]` 命名空间失配 22 处 / 16 prefab 已修） |
| 现有程序集引用图 | `00_Core→00_Attribute`；`01_GameContract→00_Core,00_Attribute`；`00_Utils→00_Core,01_GameContract,00_Attribute`；`08_Map→00_Attribute,00_Utils,00_Core`；`00_WndTools→00_Core,00_Utils`；`04_UI→00_Core,00_Utils,00_WndTools`；`05_EffectComp→01_GameContract,00_Utils,00_Core`；`DayNightSystem→00_Attribute`（+包） |
| 旧 asmdef 引用（GUID） | `e803c1ff…`=00_Attribute、`82f5fbe1…`=00_Core、`57eb3f01…`=01_GameContract、`be60ed10…`=00_Utils、`3cd45ab0…`=04_UI、`6640f3d1…`=00_WndTools、`8f290685…`=FpsGame.MapUtils(08Map)、`b8b27062…`=DayNightSystem、`b94ecfde…`=05_EffectComp、`55929e21…`=NavMeshComponents；外链 `6055be8e…`=TMP、`8c4dd219…`=Unity.AI.Navigation、`49b49c76…`=MackySoft |

**一句话诊断**：不是"缺 asmdef"，而是 `01Manager ↔ 02Game`（含 `02Data`、`04UI`、`Effect`）构成 **一个 342 文件的强连通分量**（✅ **已于 2026-10-01 完全拆解**，见第 5 节各 Phase 状态），任何"只加 asmdef 不改代码"的做法都会立刻被编译器拒绝。必须先按下面的顺序**消除回边**，再逐块建程序集。

---

## 1. 精要全景（模块 → 职责 → 现状）

### 1.1 基础层（已 asmdef 化，健康）

| 目录 | 程序集 | 核心内容 |
|---|---|---|
| `00Attribute/` | `00_Attribute` | `[DisplayField]` `[Foldout]` `[Compare]` 等项目特性（注意：**特性只对字段有效**） |
| `00Core/` | `00_Core` | `Singleton<>`/`GameRootBase`、`ObjectPool<T>`、`BaseMono/BaseObject`、`TickBehaviour`+双层定时器、`I_Entity/IPhysical/IRecyclable`、`DamageTypeEnum/ActorState/GameStateEnum/WindowStateEnum/Constants` 等**全项目共享枚举** |
| `00GameContract/` | `01_GameContract` | `I_Actor/I_Health/I_Damageable/IVfxEffect/I_MissionPoint`、`TargetData`、`DamagePacket`、`UnitQueryGridNode`、`NoiseData`、`MissionTag/UnitTier`、`LayerDefinition` |
| `00Tools/Test/` | `00_Utils` | ⚠ 名字叫 Test，实为基础工具程序集：`Utils.Tool`（39KB）、`TerrainUtils`/`TerrainMainUtils`（地形/NavMesh，46KB）、`TransformUtils/VectorUtils/RandomUtils/IEnumerableUtils(WeightTake)/MpbController/DrawLabelUtils` |
| `00Tools/`根 | **Assembly-CSharp** | `FpsHelper`（32KB，**同时依赖 08Map 与 02Game** ⇒ 拆分痛点）、`LogicBehaviour`（依赖 GameRoot/NetManager）、`SingletonNet`、`ObjectIsValid`（无辜，可下沉） |
| `08Map/` | `FpsGame.MapUtils` | `MapRoot`、`GenerateNoiseTerrain`、`TerrainClearer`、`TreeDestructor/RockCoverDestructor`（零管理器依赖，健康） |
| `DayNightSystem/` | `DayNightSystem` | `DayNightBrain`+Modules、`WeatherAtmosphereController`（氛围桥） |
| `Effect/EffectComp/` | `05_EffectComp` | 纯渲染/地形辅助 18 个组件 |
| `04UI/Assembly/` | `04_UI` | `ButtonEnterDetector/DynamicBar/WheelUI` 等**纯控件**（零管理器依赖） |
| `04UI/WndTool/` | `00_WndTools` | `WndRootTool`（SetText/SetAlpha/动画）、`LinkColor`、`UIKeepAspectRatio` |
| `Plugins/` | `NavMeshComponents` | 第三方；`DynamicBone` 4 个 cs 无 asmdef |
| `NetTmp/` | **Assembly-CSharp（本轮新增）** | `Client/`（`NetSvc`/`NetHostSvc`/`ClientSession`/`HostSession`/`MessageCenter`/`LanRoomDemo`/`NetDemo`）+ `Services/`（`CmdId` + `Msg/{Chat,Login,Ping,Room}Msg`）；依赖 `KCPNet.dll`（`Assets/Plugins/KCPNet/`，DLL 预编译、默认 auto-referenced）。**对外零引用**（唯一边 `Client→Services`）⇒ 最安全的第一个 asmdef |

### 1.2 巨型程序集内部（Assembly-CSharp，按当前耦合排布）

| 目录 | 规模 | 职责 | 关键上行依赖 |
|---|---|---|---|
| `01Manager/Global/` | 12 | `GameRoot`/`ResSvc`/`WndManager`/`TaskManager`/`RoomManager`/`ArchiveSvc`/`PropertyManager`/`InputManager`/`NetManager`/`CoroutineSvc`/`RoleManagerBase` + **`GlobalEventSub`** | → 02Data(47)、02Game(≈17)、**04UI(15)**、08Map、DayNight(4) |
| `01Manager/Battle/` | 17 | `BattleManager`（战斗总控/单位网格/团灭判负）、**`BattleEventSub`**、`WaveManager/ZergWave/RobotWave`、`PatrolContriller`、`PathRequestManager`、`MissionController`、`AirdropController`、`VFXManager`、`WeatherSystem` | → 02Game/Game(40)、Mission(38)、Managers(31)、02Data(31)、Effect(22)、DayNight(12)、08Map(8) |
| `01Manager/Bridge/` | 2 | `BridgeSys`（舰桥选战备/就绪）、`BridgeRoleManager` | → **04UI(`ArmamentWnd`)**、02Data(12) |
| `02Data/` | 20 | 15 个 `_SO` + `Variant/`（4 个变体 SO + 基类） | ⚠ **反向依赖上层**：`RoleData_SO`(02Game `WeaponPlayerController`+`WeaponTypeEnum`)、`WeaponModuleData_SO`/`WeaponUpgradeData_SO`(02Game `ModifyAttrData` + 01Manager `ResSvc`)、`MissionData_SO`(02Game `MissionBase`)、`MapData_SO`(08Map `TerrainType`) |
| `02Game/Game/` | 17 | **`Actor`**（单位身份本体）、**`UnitQueryGrid`**(+`TargetCfg`)、`MissionView`、`InterestPoint`、`GameConstants`、`MinMax*`、`MeshCombiner`、空标记组件 | → `GlobalEventSub`/`BattleEventSub`、`BattleManager` |
| `02Game/Game/Managers/` | 2 | **`ActorsManager`**（单位注册表）、**`AudioSvc`**（被 `WndManager` 反向调用！） | → `ResSvc`、`GameRoot` |
| `02Game/Game/Shared/` | 12 | `Health`(abstract)+`HealthPlayer/Enemy/Other/Shield/SpecUnit`、**`Damageable`**、**`ProjectileBase`**(+`ProjectileHitData`)、`ShieldRebuild`、`TransferDamageable`、`AutoDeath` | → `BattleManager`(10)、`TaskManager`、`BattleEventSub`、`AbaStateData_SO` |
| `02Game/Game/Shared/Weapon/` | 20 | `WeaponBaseController`、`WeaponController` 体系、`DamageData`/`SustainedDamageData`、`WeaponAttributeFactory`、`WeaponCfg`（含死文件 `WeaponCfgOld.cs`/`WeaponAttributeFactoryOld.cs`） | → `BattleManager`、`WndManager`、`ResSvc`、`TaskManager` |
| `02Game/Game/Mission/` | 22 | `MissionBase`(abstract TickBehaviour)+`Main/Sub/Extra/Evacuate` 四组任务 + `MissionDestroyActor/WaitSub/OperationFurn/CompleteKeyScreen` | → `BattleManager`(19)、`TaskManager`、`WndManager`、`ResSvc`、**05Interactable(23+13)** |
| `02Game/AI/` | 44 | `Controller`（`AIController` 基类 + `IUnit/I_AIController` + `EnemyController` 三分部）、`StateMachine`（`EnemyMobile`/`EnemyTurret`/`GuardDog`…）、`DetectionModule`（视/听）、`Skill`（`UnitSkill_*`）、`FxCont`（**纯表现**）、`Other` | → `BattleManager`、`BattleEventSub`、`GlobalEventSub` |
| `02Game/03Player/` | 16 | `PlayerController`（三分部）、`BaseSelfController/BaseSelfMoveableController`、`PlayerInputHandler`、`PlayerWeaponsManager`、`EquipController`、`PlayerOOPartInventory`、`VehicleController/EmplacementController` | → `WndManager`(68)、`GlobalEventSub`(97)、`BattleEventSub`、`GameRoot`、`ResSvc` |
| `02Game/05Interactable/` | 23 | `Furniture_Attached`(=`IFurniture`)+`FurnitureFlag`、各 `Furniture_*` 叶子（Door/Pipe/KeiSubmit/HandEquip/Artillery/Supply…）、`KeyScreen`+流程、`OOPart` | → `GlobalEventSub`(70)、`TaskManager`、`WndManager`、03Player(28) |
| `02Game/06Npc/` | 4 | `GuideController`、`Furniture_NPCChat`、`NPCWalk`、`SpecUnitController` | → `GlobalEventSub`、`SoundGroup_SO` |
| `02Game/Gameplay/` | 23 | `Bag/`（`BagBase`+`HandEquip`+`Jetpack/ShieldBag/WeaponBag`）、`Projectile/`（`ProjectileStandard/Laser/Melee`、`AirdropPod`、`DeployableMine`）、`SPEffect/`（`SustainedEffect/GroundFire/Trample`） | → `BattleManager`(少量)、`GameContract` |
| `02Game/Interface/` | 2 | `IEquippable`+`EquippableFlagEnum`、`ISubmittableHandItem` | 纯契约，**应下沉** |
| `04UI/`根 + `UI/` | 46 | `Window` 基类 + ≈30 个 `*Wnd`、`CanvasController`、准星/血条/任务条/字幕 | → `WndManager`(229!)、`BattleManager`(96)、02Data(95)、02Game(≈110) |
| `Effect/`根 + `VFX/` | 28 | `CreatBuilding/CreatEnemy/CreateSupple/CreatOOPart/ModifyTerrain/MedivacController/TransSceneController/DayNightCycle/FreeCameraController/LimitedLife`… | → `GlobalEventSub`(55)、`BattleManager`(18)、`TaskManager`、`ActorsManager` |
| `Rendering/`（含 `Snow/`） | 7 | **`02_Rendering`（2026-09-30 已切出）**：`SnowRendererFeature`+`SnowController`、体积云、描边、雾前层 RF、`SnowVolume` | → `00_Utils`(`TerrainUtils.Main`)、`DayNightSystem`(`WeatherAtmosphereController.*`)、URP/Core RP；反向被 `Assembly-CSharp` 调 `SnowController` |

---

## 2. 耦合诊断（量化）

### 2.1 三个真实环路（编译器不允许 asmdef 化的根因）

**环 1 —— `01Manager ↔ 02Game`（主干，最大）**

| 方向 | 代表调用点 | 次数（文本级） |
|---|---|---|
| 02Game → 01Manager | `BattleManager.Instance`(110)、`GlobalEventSub`(143)、`BattleEventSub`(135)、`TaskManager`(64)、`WndManager`(68)、`ResSvc`(48)、`GameRoot`(77)、`ArchiveSvc`(42) | `03Player→Global 97`、`Mission→Battle 19`、`Shared→Global 26`、`Shared/Weapon→Battle 12` |
| 01Manager → 02Game | `BattleManager`：`new ActorsManager()`、`Actor`、`UnitQueryGrid`、`UnitQueryGridDebugger`、`PlayerController`；`BattleEventSub`：`Actor`/`MissionBase` 事件签名；`MissionController`：`MissionBase` 字段+`Instantiate`；`WaveManager/ZergWave/RobotWave/PatrolContriller`：`Actor`/`ActorsManager`/`EnemyController`；`VFXManager`：`ProjectileBase` 池；`WndManager`：`AudioSvc`；`TaskManager/RoleManagerBase`：`CampaignCfg`/`PlayerController` | `Battle→Game 40`、`Battle→Mission 38`、`Battle→Managers 31`、`Battle→Effect 22`、`Global→02Game≈17` |

**环 2 —— `02Data ↔ 上层`**：`02Data→01Manager/Global 18`（`ResSvc`）、`02Data→02Game/Shared/Weapon 6`（`ModifyAttrData`）、`02Data→02Game/Mission 1`（`MissionBase`）、`02Data→08Map 1`（`TerrainType`）；反向 `Global→02Data 47`、`Battle→02Data 31`、`04UI→02Data 95`。

**环 3 —— `Effect / 04UI / Plugins ↔ 01Manager`**：`Effect→Global 55`+`→Battle 18`，`Battle→Effect 22`（`VFXManager` 池 `ProjectileBase`/`VFXAirdropEffect`）；`04UI→Global 229`+`→Battle 96`，`Global→04UI 15`（`WndManager` 持窗口引用）+`Bridge→ArmamentWnd 6`。

### 2.2 最烫的类型（跨单元引用次数 Top 20，用于判断"该下沉什么"）

`I_Actor 179`、`Actor 164`、`GlobalEventSub 143`、`DamageTypeEnum 138`、`Tool 137`、`BattleEventSub 135`、`WindowStateEnum 117`、`InputManager 116`、`BattleManager 110`、`GameStateEnum 106`、`Constants 96`、`ActorsManager 84`、`GameRoot 77`、`TerrainUtils 77`、`DisplayField 75`、`AudioSvc 72`、`DetectionModule 71`、`WndManager 68`、`WeaponPlayerController 68`、`TaskManager 64`、`MissionBase 61`、`FpsHelper 60`、`IHealth 60`、`IEquippable 54`。

> 读法：`I_Actor/Actor/ActorsManager/DetectionModule/IEquippable/MissionBase/DamageData` 是高内聚"内核"，`GlobalEventSub/BattleEventSub/WndManager/TaskManager/ResSvc/GameRoot` 是"服务 + 事件"，前者要**下沉**，后者要**接口化**。

### 2.3 非耦合类问题清单（与拆分并行收拾）

| 问题 | 位置 | 处置 |
|---|---|---|
| 运行时目录里混 Editor 脚本 | `02Game/AI/StateMachine/Editor/TurretDrawer.cs`（独立 Editor 文件夹） | 建 editor-only asmdef 或移到 `Assets/Editor/` |
| **未守卫的 `UnityEditor`** | `05Interactable/OOPart.cs:4`（`UnityEditor.ShaderGraph.Internal`）、`Game/MissionView.cs:9`（`using static UnityEditor.Progress`） | 删除或 `#if UNITY_EDITOR`。⚠ 这类会让**非 Editor 平台构建直接失败** |
| 整文件死代码 | `Game/Shared/Weapon/WeaponCfgOld.cs`、`WeaponAttributeFactoryOld.cs`（整体 `/* */`）、`EnemyControllerDebug.cs` | 删除 |
| 纯 Editor 逻辑占运行时代码位 | `02Game/Util/GameMenuUtil.cs`（整文件 `#if UNITY_EDITOR`） | 移到 `Assets/Editor/` |
| 命名空间错拼/割裂 | `SympatheticDetonation.cs` = `UFPSGame.AI`；同目录混用 `Unity.FPS.Game`/`Unity.FPS.Gameplay`/`FPSGame.*`/全局 | 新代码统一 `FPSGame.*`；存量就地改名（不改类型名则不动 prefab） |
| `rootNamespace` 缺失 | 11 个 asmdef 里仅 `00_Core`/`00_Attribute` 填了 | 逐个补齐（不影响编译，只影响新文件模板） |
| 契约藏在实现文件里 | `IFurniture`+`FurnitureFlag`（`Furniture_Attached.cs`）、`TargetCfg`（`UnitQueryGrid.cs`）、`ProjectileHitData`（`ProjectileBase.cs`）、`IDamageData`（`DamageData.cs`）、`ModifyAttrData`（`WeaponUpgradeController.cs`）、`IVehicleUIController`（`04UI/UI/VehicleUI.cs`）、`DifficultyEnum`（`TaskManager.cs`）、`WeaponTypeEnum`（`RoleData_SO.cs`）、`TerrainType`（`08Map`）、`EnemyVarietyType`（`00_Core`） | 抽成独立文件下沉 `01_GameContract`（见 §4-C） |

---

## 3. 目标程序集架构（分层自下而上）

> ⚠ **2026-09-30 行业视角复审后：玩法层不再拆成 5 个程序集，改为合并为 1~2 个；UI/Effect 也走向合并。先读 §8 再按本表执行**（本表保留原始分析，§8 给出修正后的最终清单）。
>
> 层号小 = 被依赖方。**同一层内不允许互引**（需要互引就说明该合并或该抽契约）。

| 层 | 程序集（目标名） | 来源目录 | 允许引用 |
|---|---|---|---|
| 0 | `00_Attribute` | 00Attribute | — |
| 1 | `00_Core` | 00Core | 0 |
| 2 | `01_GameContract` | 00GameContract（**扩容为唯一契约层**） | 0,1 |
| 3 | `00_Utils` | 00Tools（根 + Test，**先把 `FpsHelper` 拆干净**） | 0,1,2 |
| 3 | `02_Net` | NetTmp | 0,1,2（+ KCPNet.dll） |
| 3 | `DayNightSystem`（已有） | DayNightSystem | 0,1,2（+ Fog / URP Core RP） |
| 4 | `02_Rendering` ✅已建 | Rendering（含 `Snow/`，`Feature/` 已并入） | 0,1,2 + `00_Utils` + `DayNightSystem` + URP/Core RP |
| 4 | `03_EventBus` | 由 `GlobalEventSub`/`BattleEventSub` **契约化拆出** | 0,1,2 |
| 4 | `03_Audio` | `AudioSvc` + `SoundGroup_SO` + 音频 DTO | 1,2,3 |
| 4 | `04_Data` ✅已建 | `02Data/`（14 个纯数据 SO；**`RoleData_SO`/`ArchivesData_SO`/`MissionData_SO`/`MissionMainData_SO`/`WeaponModuleData_SO`/`WeaponUpgradeData_SO` 已迁到 `02Game/Game/Data/`，待 P5 归入游戏层**） | 0,1,2 + `00_Utils` |
| 4 | `04_Map` | 08Map / FpsGame.MapUtils | 0,1,2,3 |
| 4 | `04_UI.Wnd`（现 `00_WndTools`） | 04UI/WndTool | 0,1,2,3 |
| 4 | `04_UI.Controls`（现 `04_UI`） | 04UI/Assembly | 0,1,2,3,4.UI.Wnd（+TMP） |
| 5 | `05_UnitCore` ✅**已建（2026-09-30）** | `Assets/Scripts/05UnitCore/`（19 cs：`Actor`、`UnitQueryGrid`+`UnitQueryGridDebugger`、`UnitEventSub`、`ActorsManager`、`Health`(+`HealthEnemy/Other/Player/Shield/SpecUnit`+`Health_AboState`)、`Damageable`、`TransferDamageable`、`AutoDeath`、`ShieldBehaviour`、`ShieldRebuild`、`MinMaxParameters`、`GameConstants`；⚠ `ProjectileBase` **不在此层，归 `06_Weapon`**） | `00_Attribute`+`00_Core`+`01_GameContract`+`00_Utils`+`03_Audio`+`04_Data` |
| 6 | `06_Weapon` | `02Game/Game/Shared/Weapon/`（`DamageData`/`Weapon*Controller`/`WeaponAttributeFactory`） | 0..5 |
| 6 | `06_AI` | `02Game/AI/**`（含 FxCont/DetectionModule/Skill/StateMachine/Controller） | 0..5 |
| 6 | `06_RenderingFx`（现 `05_EffectComp`） | Effect/EffectComp | 0..4 |
| 7 | `07_Player` | `02Game/03Player/**` | 0..6 |
| 7 | `07_Mission` | `02Game/Game/Mission/**` + `MissionView`/`InterestPoint` | 0..6 |
| 8 | `08_Interactable` | `02Game/05Interactable/**` + `02Game/06Npc/**` + `Gameplay/**` | 0..7 |
| 9 | `09_Managers` | `01Manager/**`（实现 §4-A 的契约接口） | 0..8 |
| 10 | `10_UI` | `04UI` 根 + `04UI/UI` | 0..9 |
| 10 | `10_Effect` | `Effect/`根 + `Effect/VFX` | 0..9 |
| 11 | `EditorTools` / `Editor.DataEditor` / `Editor.Drawer` | `Assets/Editor/**`（`Editor/DataEditorWindow.cs`、`DataTabs/`、`Drawer/`、`SOPickerPopup`、`Tool/`） | 全部运行时程序集（`includePlatforms:[Editor]`） |

**新 asmdef 统一置 `autoReferenced: true` 无必要** —— 建议全部 `autoReferenced: false` + 显式 `references`，否则任何新脚本仍能"凭空"用到上层类型，等于白拆。**只对 Editor 程序集保持 `true`。**

---

## 4. 打破环路的 6 个手法（每个都给了落点）

### A. 服务定位器 + 契约接口（解决"低层调 Manager"）
在 `01_GameContract` 加：
```csharp
public interface IBattleService { void Authorize(int id, bool on); /* …按实际用到的方法逐个加 */ }
public interface ITaskService   { /* nowTask 数据只读视图 */ }
public interface IResService    { T Load<T>(string path) where T : Object; }
public interface IWndService    { void CreatNotice(...); }
public interface IGameRootService { GameStateEnum GameState { get; } float CreateTimer(...); }
public static class ServiceLocator { public static IBattleService Battle; public static ITaskService Task; … }
```
各 Manager 在 `Awake/Init` 里 `ServiceLocator.Battle = this;`。
调用方把 `BattleManager.Instance.XXX` 改成 `ServiceLocator.Battle.XXX`。
- 覆盖量：`BattleManager`(110) / `TaskManager`(64) / `ResSvc`(48) / `WndManager`(68) / `GameRoot`(77) / `ArchiveSvc`(42)。
- 代价：接口要按**实际调用到的成员**逐个搬（不要一次设计全量 API）；可用 `// TODO 下沉` 分批。

### B. 事件总线契约化下沉（解决"事件中枢被上层类型污染"）
`GlobalEventSub`/`BattleEventSub` 现在把 `Actor`/`MissionBase`/`PlayerController`/`IFurniture`/`AirdropData_SO` 写进签名，导致它们必须和 02Game 同程序集。处置：
1. 签名换契约：`Action<Actor>`→`Action<I_Actor>`、`Action<MissionBase>`→`Action<I_Mission>`、`Action<PlayerController>`→`Action<I_Actor>` 或 `Action<GameObject>`、`Action<AirdropData_SO>`→`Action<int>`/`Action<IAirdropData>`。
2. 完成后再整体移入 `03_EventBus`（只引 0/1/2）。
3. ⚠ 与项目既有约定一致：**能用实例事件就别塞全局订阅**（如 `Furniture_HandEquip.OnPicked`、`Furniture_KeiSubmit` 的 `ISubmittableHandItem.NotifySubmitTo`），拆分层时优先把"只有一两个订阅者"的事件改成实例事件。

### C. 契约抽取清单（把实体移出实现文件 → `01_GameContract`）

| 契约 | 现在藏在 | 目标 |
|---|---|---|
| `IFurniture`+`FurnitureFlag` | `Furniture_Attached.cs` | GameContract |
| `TargetCfg` | `UnitQueryGrid.cs` | GameContract |
| `ProjectileHitData` | `ProjectileBase.cs` | GameContract |
| `IDamageData` | `DamageData.cs` | GameContract |
| `IEquippable`+`EquippableFlagEnum`、`ISubmittableHandItem` | `02Game/Interface/` | GameContract |
| `IStepPress` | `05Interactable/IStepPress.cs` | GameContract |
| `IUnit`、`I_AIController`（瘦身：去掉 `WeaponBaseController` 字段） | `AI/Controller/AIController.cs` | GameContract |
| `IVehicleUIController` | `04UI/UI/VehicleUI.cs` | GameContract（**解开 02Game→04UI 的硬依赖**） |
| `ModifyAttrData` | `WeaponUpgradeController.cs` | GameContract |
| `DifficultyEnum`、`WeaponTypeEnum`、`TerrainType`、`EnemyVarietyType` | `TaskManager.cs` / `RoleData_SO.cs` / `08Map` / `00_Core` | GameContract（枚举是跨层数据，不是实现） |
| `I_Mission` | 新抽（`MissionBase` 的只读面：`Tag/Type/NowProgress/MaxProgress/Entity/IsComplete`） | GameContract |

### D. 数据层去逻辑（解决 `02Data → 上层`）—— 实际采用「分层搬迁」而非「逐字段改造」
> 2026-09-30 落地结论：**改字段类型（`MonoBehaviour`→`GameObject`）要连带改调用点 + 重配资产，成本与风险远高于收益**。实际采用两步：
> ① **可下沉的跨层枚举/结构** → 抽到 `01_GameContract`（`OOPartEnum`/`MissionEnum`/`SizeType`/`WeatherType`/`TerrainType`）；
> ② **整份 SO 引用了逻辑类型** → 把该 SO **搬出数据层**（`02Data/` → `02Game/Game/Data/`），等 P5 游戏层成集后它自然归位。
> 判据：**SO 里出现 MonoBehaviour 字段或服务调用 ⇒ 搬走；只出现基础类型/枚举/其他 SO ⇒ 留下**。

| 破口（原计划） | 实际处理（2026-09-30） |
|---|---|
| `RoleData_SO.weapons: DisplayDic<WeaponTypeEnum,List<WeaponPlayerController>>` | **搬走** `RoleData_SO` 到 `02Game/Game/Data/`（字段与调用点未动，零资产改配） |
| `WeaponModuleData_SO`/`WeaponUpgradeData_SO: List<ModifyAttrData>` | **搬走**两个 SO（避免为 `ModifyAttrData` 连带下沉 `WeaponAttrType`/`ModifierType`） |
| `WeaponModuleData_SO: ResSvc.Instance.LoadSprite` | 随 SO 搬走；将来若要让 SO 回数据层，再改成 `Sprite` 引用或静态委托注入 |
| `MissionData_SO.controller: MissionBase` | **搬走** `MissionData_SO`+`MissionMainData_SO`（`Instantiate(go)+GetComponent<I_Mission>` 的改造留到 P5） |
| `MapData_SO` 用 `TerrainType`(08Map) | ✅ `TerrainType` 下沉 `01_GameContract`（同批还下沉了 `OOPartEnum`/`MissionEnum`/`SizeType`/`WeatherType`） |
| `ArchivesData_SO` 调 `GlobalEventSub.OnGainExp` | **搬走** `ArchivesData_SO`（另：它 `Resources.Load<RoleData_SO>`，与 `RoleData_SO` 同层更方便） |

### E. 管理器装配权反转（解决 `01Manager → 具体玩法类型`）
- `BattleManager`：`new ActorsManager()` → 改为对 `IUnitRegistry` 接口编程；`Actor`/`UnitQueryGrid` 下沉 `05_UnitCore`（网格只吃 `I_Actor` 数据）。
- `BattleEventSub`/`GlobalEventSub`：见 §4-B。
- `MissionController`：`MissionBase` → `I_Mission`（§4-C）。
- `WaveManager/ZergWave/RobotWave/PatrolContriller`：`Actor`→`I_Actor`、`EnemyController`→`I_AIController`。
- `VFXManager`：`ProjectileBase`/`VFXAirdropEffect` 池 → 池契约接口 `IVfxPoolItem`（GameContract）。
- `WndManager.AudioSvc` → 下沉 `03_Audio`（同级不互引）。
- **窗口跨层开关 → "动词 + 枚举"**（2026-09-30 设计、**2026-10-01 已落地**；编译 0 error + 反射已验证。**原稿"下沉 `Window` 抽象基类"与"窗口自注册表"两版均作废**，理由见末尾）
  - **实测前提**：`Window` 基类（`04UI/Window.cs`）已有 `public virtual void SetWndState(bool isActive = true)`（内部 `SetActive` + `GlobalEventSub.WndSwitch` + 首次显示时 `firstInit` 懒取 6 个管理器）与 `public bool State => gameObject.activeSelf`。**跨层的具体窗口字段消费者全仓只有 `Furniture_General.cs`（7 处 / 6 种窗口）**；另有 `01Manager/Bridge/BridgeRoleManager.cs:68`（同层，合法，保留不动）。`WndManager` 的 10 个字段全是 `public` + Inspector 序列化的 `04UI` 具体类型。
  - **已落地的形状（只加"动词"，不下沉任何 UI 类型）**：
    1. **`01_GameContract/WndTypeEnum.cs`（新）**：`None/Operation/Tip/Notice/CountDown/SelectMap/SelectRole/Guide/Vehicle/AirdropConfig/Setting`（**列全 10 个窗口**，不只当前用到的 6 个）。归属判据：被 `WndManager`(01Manager)、`Window` 子类(04UI)、玩法层**三者共见 ⇒ 放三者共见的最低层＝契约层**（不跟着 `WindowStateEnum` 放 `00Core`，那是历史原因）。
    2. `IWindowService` **只加 1 个动词**：`void SetWndState(WndTypeEnum type, bool isActive = true)`；**没加 `IsWndOpen`**（当前无消费者，守"只开真正用到的成员"）。`NullServices.NullWindowService` 同步补空实现 ✓（**规则：往契约加成员必须同步加空对象**）。
    3. `WndManager.SetWndState(type, isActive)` = **`switch` 映射现有 10 个字段**后调 `wnd.SetWndState(isActive)`（映射不到用 `if (wnd)` 静默跳过）⇒ **具体窗口类型只出现在 `WndManager` 一个文件里**。
    4. `Furniture_General` 7 处 → `GameContract.ServiceLocator.Wnd.SetWndState(GameContract.WndTypeEnum.X);`，删掉 `private static WndManager wndManager => WndManager.Instance;`，并在原位留了说明注释 ⇒ **玩法侧 UI/管理器直连真实为 0** ✓
  - ⚠⚠ **为什么"窗口自注册表"推迟到 Phase 6（本次没做）**：**Unity 不会为 `SetActive(false)` 的对象调用 `Awake`**，而窗口平时正是关着的 ⇒ `Window.Awake` 里自注册在"首次打开前"根本没执行 ⇒ **死锁**（要注册表才能打开，要打开才注册）。可靠的注册表方案必须先解决"谁在窗口未激活时扫一次"（UI 层加 `GetComponentsInChildren<Window>(true)` 的 bootstrap，或 `RuntimeInitializeOnLoadMethod` 扫描器），并处理"销毁反注册"（26 个子类里 10 个已 override `OnDestroy`，**必须先确认它们都调了 `base.OnDestroy()`**）。⇒ **先落"switch 映射"：零生命周期假设、零 prefab 改动，就把跨层依赖切断；删 `WndManager` 那 10 个字段留 Phase 6**（届时只改实现，调用点零改动）。
  - **为什么不用"下沉 `Window` 抽象基类"**：① 仍让 `WndManager`(01Manager) 依赖 `04UI` 类型 ⇒ `01Manager → 04UI 15` 这条边留着；② 基类 `firstInit` 懒取 6 个管理器 ⇒ 下沉要连带改 6 处；③ 玩法层拿到句柄会自然去用 `Window` 的其它成员（耦合蔓延）。**动词化 = 最小暴露面**。
  - **本次保留**：`CreatTip`/`CreatCountDown`/`CreatNotice`/`ClearNotice` 仍需具体类型（`tipWnd.Creat`/`countDownWnd.StartDown`/`noticeWnd.Creat`）⇒ 那 3 个字段留着；彻底删 10 个字段时把这一族上移进 UI 层。
  - **验收（已通过）**：`failed=False` + Console 0 error；反射 = `IWindowService` 含 `SetWndState(WndTypeEnum,Boolean)`、`WndTypeEnum` 在 `01_GameContract`（11 个值）、`WndManager implements IWindowService=True`、`ServiceLocator.Wnd` 默认 `NullWindowService` ✓。
  - ⚠ **维护点：新增窗口只改 `04UI/WndHub.TypeOf` 一处**（把类型映射到 `WndTypeEnum`）；`Window.Awake` 会自动登记 ✓。
  - ✅ **2026-10-01：Phase 6 的"去字段"部分已完成**（`01Manager` 的 `04UI` 引用 **17 → 0**）：
    1. **`00GameContract/WindowRegistry.cs`（新）**：**委托插槽** —— `SetWndState`/`IsOpen`/`CreatNotice`/`CreatCountDown`/`ClearNotice`/`Scanner` + `EnsureScanned/SetState/GetOpen/Notice/CountDown/Clear/Dump`。契约里**不出现任何 UI 类型**，管理器只调动词。
    2. **`00GameContract/IBridgeArmamentSink`**（同文件）：`BridgeSys` 原先 `public ArmamentWnd armament;` 并调它的 3 个方法 ⇒ 改为对接口编程（实现方 `ArmamentWnd`，登记点不变）。
    3. **`04UI/WndHub.cs`（新）**：UI 侧窗口中心（`Tip`/`Notice`/`CountDown`/`Operation` + `Dictionary<WndTypeEnum,Window>`）；`Bind()` 把能力登记进 `WindowRegistry`；`TypeOf()` 用 `is` 映射；**`Scan()` 扫 `FindObjectsOfType<Window>(true)`**；**`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 启动钩子**保证扫描器一定可用。
    4. **`04UI/Window.cs`**：加 `protected virtual void Awake() => WndHub.SelfRegister(this);` + `OnDestroy` 里 `SelfUnregister` ⇒ **26 个子类一次性自注册**（含原来靠 Inspector 字段的 `TipWnd`/`NoticeWnd`/`CountDownWnd`/`OperationWnd`）。⚠ 已核对：26 个子类**无一**自带 `Awake`/`OnEnable` ⇒ 安全；**今后子类不要定义 `Awake`**（会顶掉基类注册）。
    5. **`WndManager`**：删 10 个字段 + `CreatTip`（参数 `TipWndInfo` 属 UI、调用方全在 UI ⇒ 直接用 `WndHub.Tip.Creat`）；其余动词转调 `WindowRegistry` ⇒ **`WndManager` 里零 UI 类型**。
    6. **调用点批量改写**（`.codebuddy/plans/p6_wnd_sweep.py`，11 文件）：删 12 行窗口自注册、`CreatTip` 7 处 → `WndHub.Tip.Creat`、`operationWnd` → `WndHub.Operation`、`BridgeRoleManager` 改 `SetWndState`、`BridgeSys.armament` 改接口类型。
  - ⚠⚠ **本次解决的关键坑**：**未激活的窗口不跑 `Awake`** ⇒ 自注册会漏。四层兜底：① `Window.Awake` 自注册；② `WndHub.Scan()` 扫 `includeInactive:true`；③ `WindowRegistry.EnsureScanned()` 在插槽为空时回调 `Scanner`，且 `SetWndState` 闭包 miss 时**再扫一次**；④ 启动钩子保证 `Scanner` 非空。⇒ **"第一次打开某个从未打开过的窗口"可用** ✓
  - ⚠ 语义改进：原 `wndManager.selectRoleWnd.SetWndState(true)` 在字段未赋值时是 **NRE**，现静默跳过（同 P3 的 null 语义改进）。
  - **`WndHub` 取值形态（2026-10-01 评审改进）**：**单一数据源** —— `Dictionary<WndTypeEnum, Window>` 是唯一真相；UI 内部用 **`Get<T>(WndTypeEnum)` / `Get<T>()`**（泛型、免强转、miss 时自动 `Scan`）；`Tip`/`Notice`/`CountDown`/`Operation` 由**可变字段改为只读属性**（内部走 `Get<T>`）⇒ 无"两份真相"、少 8 行簿记、调用点零改动。
    - ⚠ **纪律：`Get<T>` 只能在 `04UI` 内使用**（它要求写出具体窗口类型）；跨层用契约动词。
    - 已否决：字典键改 `System.Type`（丢"管理器只认枚举"的能力 ✗）、`Window.WndType` 抽象化（动 26 个子类 ✗）。
- `BridgeSys → ArmamentWnd` → 改发事件（`GlobalEventSub.OnXxx`），UI 侧订阅。

### F. 反向通知改事件（Effect 层）
`Effect/CreatBuilding`、`CreatEnemy`、`CreateSupple`、`MedivacController`、`TransSceneController`、`ApplayMapTextureVariant` 里的 `BattleManager.EnqueueInit/TaskManager/ActorsManager/ResSvc` 调用：**表现层不该驱动流程**。
- 需要"入场后初始化"→ 订阅 `03_EventBus` 的 `OnBattleReady`/`OnUnitCreate`；
- 需要读配置 → `ServiceLocator.Task`；
- 需要加载 → `ServiceLocator.Res`。
拆完后 `Effect` 才可能落在 `09_Managers` 之上。

---

## 5. 分阶段执行清单

> 原则：**每阶段结束必须能编译 + Play 冒烟**；阶段内先改代码再建 asmdef（asmdef 是"锁"，不是"锤"）。

### Phase 0 · 清理 —— ✅ 已完成（2026-09-30，编译 0 error）
| 项 | 结果 |
|---|---|
| 删死代码 | ✅ `WeaponCfgOld.cs`、`WeaponAttributeFactoryOld.cs`（+`.meta`）已删；删前已核对：整文件 `/* */`、**无任何 asset/code 引用**（按 `.cs.meta` 的 guid 全文搜 prefab/unity/asset 为零） |
| UnityEditor 泄漏 | ✅ `05Interactable/OOPart.cs:4`（`UnityEditor.ShaderGraph.Internal`）与 `Game/MissionView.cs:9`（`using static UnityEditor.Progress`）已删。⚠ 全仓扫描后确认：**其余 13 处 `using UnityEditor` 全在 `#if UNITY_EDITOR` 内**，无第二个真泄漏 |
| Editor 脚本归位 | ✅ `02Game/Util/GameMenuUtil.cs`（+`.meta`）→ `Assets/Editor/`（原本在运行时代码目录里，虽被 `#if UNITY_EDITOR` 全包但位置误导） |
| ~~`TurretDrawer.cs` 移出运行时目录~~ | ❌ **方案有误，无需处理**：它在 `02Game/AI/StateMachine/Editor/`，Unity 的 `Editor` 文件夹规则已把它编进 `Assembly-CSharp-Editor`（实测：`Assembly-CSharp.dll` 里搜不到 `TurretDrawer`，`Assembly-CSharp-Editor.dll` 里有）。真正的隐患只在"02Game 将来加 asmdef"时才会出现——那时该文件夹需要自己的 editor-only asmdef |
| 命名空间拼写 | ✅ `SympatheticDetonation.cs` 的 `UFPSGame.AI` → `FPSGame.AI`（全仓唯一出现处，无引用方） |
| `rootNamespace` 补齐 | ✅ `00_Utils`→`Utils`、`04_UI`→`FPSGame.UI`、`00_WndTools`→`WndTools`、`08_Map`→`FpsGame.MapUtils`、`DayNightSystem`→`FPSGame.DayNightSystem`、`05_EffectComp`→`EffectComp`（`01_GameContract`/`NavMeshComponents` 未动） |

### Phase 1 · 切出零依赖块 —— ✅ **已完成（2026-10-01）**
> 零依赖块全部切出并成集：`00_Utils`(30) / `02_Rendering`(8) / `03_Audio`(1) / `FpsGame.MapUtils`(6) / `00_WndTools`(3) / `04_UI`(5) / `05_EffectComp`(18) / `DayNightSystem`(8) / `DynamicBone`(4)。
**① `Rendering/` + `Feature/Snow/` → `02_Rendering` —— ✅ 已完成（编译通过、Console 0 error）**
- 目录调整：`Feature/Snow/SnowVolume.cs` 并入 `Rendering/Snow/`（+`.meta` 一起搬，**保留 guid ⇒ VolumeProfile 等资产引用不断**），空的 `Feature/` 与 `Feature.meta` 已删除。
- 交付：`Assets/Scripts/Rendering/02_Rendering.asmdef`（`src=7`：6 个 Rendering + `Snow/SnowVolume`；`autoReferenced: true`）。
- 最终 `references`（⚠ **与我初版方案不符，已修正**）：
  | 引用 | 为什么需要 |
  |---|---|
  | `GUID:be60ed10355013d4da9800d95ff2b0be` = `00_Utils` | `SnowRendererFeature.cs:353` 用 `TerrainUtils.Main` |
  | `GUID:b8b270621298133458487c8e89886c8f` = `DayNightSystem` | `DrawVolumetricCloud.cs:164/205/206/243/244/246` 用 `WeatherAtmosphereController.*` |
  | `Unity.RenderPipelines.Core.Runtime` | `VolumeComponent`/`VolumeManager`/`IPostProcessComponent` |
  | `Unity.RenderPipelines.Universal.Runtime` | `ScriptableRendererFeature`/`ScriptableRenderPass` |
- **教训（写方案时踩的）**：我最初用 `\bUtils\.` 去扫依赖，**匹配不到 `TerrainUtils.Main`**（`\b` 在 `TerrainUtils` 内部失效），`WeatherAtmosphereController` 也不在扫的词表里 ⇒ 首版 asmdef 漏引用、编译报 7 条 `CS0103`。**以后扫依赖一律用"本项目已声明类型名全集"去匹配（就是 `AsmDepScan.ps1` 的做法），不要手工列关键词。**
- 反向确认无环：`DayNightSystem → 02_Rendering`（3 次）与 `02_Rendering → 01Manager/Battle`（1 次）均为**注释噪声**；`01Manager/Battle → 02_Rendering`(6)、`00Tools → 02_Rendering`(1)、`01Manager/Bridge → 02_Rendering`(1) 是 `Assembly-CSharp` 里的真实调用（`SnowController`），靠 `autoReferenced: true` 解析 ✓。
- **层级修正**：`02_Rendering` 不是"零依赖叶子"，它**必须在 `DayNightSystem`、`00_Utils` 之上**。

**② `NetTmp/` → `02_Net` —— ⬜ 未做**（12 cs，对外零引用，唯一边 `Client→Services`；依赖 `Assets/Plugins/KCPNet/KCPNet.dll`）。
**③ `Plugins/DynamicBone` → 可选程序集 —— ⬜ 未做**（不改也行）。

### ⚠ 过渡期铁律（**双向**，2026-09-30 补第 2 条）
**① `autoReferenced` 必须为 `true`**：`Assembly-CSharp`（≈340 cs 的巨型程序集）**无法显式添加 references**，只能靠"自动引用所有 `autoReferenced:true` 的程序集"来看到新程序集的类型。所以在 `Assembly-CSharp` 被拆空之前，**每个新 asmdef 都必须 `autoReferenced: true`**（与 §7-5 的终态目标相反，见该条修订）。拆到 Phase 7（`Assembly-CSharp` 基本清空）后才统一切成 `false` + 显式 `references`。

**② ⚠⚠ 反向不成立：asmdef 程序集永远看不到 `Assembly-CSharp` 的类型** —— 这是 Unity 的**硬约束**（不是配置问题，`references` 里写什么都加不上）。推论：**新程序集要用到的每一样东西，必须"已经"在某个 asmdef 或插件 dll 里**。
- **这就是 P4 的真阻塞**（我最初的 6 项扫查没抓到它）：`Actor`/`Health`/`Damageable`/`UnitQueryGrid` 调 `x.IsValid()`，而 `IsValid` 扩展声明在 `00Tools/ObjectIsValid.cs`＝**`00Tools/` 根（Assembly-CSharp）** ⇒ `05_UnitCore` 编译报 4 条 `CS1061`。
- 另一面：这类"看着能用、一进 asmdef 就消失"的依赖，**恰恰说明拆分是有效的**（它印证了 `00Tools/` 根必须成集）。
- 两个**扫描盲区**（本次实测，务必记住）：
  1. **扩展方法**：调用点写的是 `x.IsValid()`，**不出现类名** ⇒ 任何"按类型名匹配"的扫描都抓不到。需要额外扫**扩展方法的定义与调用形态**（`static X M(this ...)` 的所有定义 → 再反查调用）。
  2. **`internal` 成员**：跨不了程序集，也是一类隐形依赖。

**③ ⚠⚠ 搬 Unity 资产必须用 `AssetDatabase.MoveAsset(old, new)`，不要只靠文件系统 `Move-Item` + `.meta`**（2026-10-01 血的教训）
- **原理**：Unity 在资产库未及时刷新时，会把"新路径的 `.meta` 声称拥有旧路径的 guid"判为**冲突**（控制台 `GUID [...] conflicts with: <old path> (current owner)`），于是**给新路径重新分配一个 guid**，而旧 guid 仍被 prefab/场景/数据资产引用 ⇒ **Missing Script / 数据资产丢脚本**。
- **实测规模**：本系列 asmdef 拆分累计 **14 个文件 guid 变化**，其中 **7 个真损坏**（`MissionData_SO` 被 26 个数据资产引用、`WeaponUpgradeData_SO` 68 个、`MissionMainData_SO` 15、`WeaponModuleData_SO` 20、`RoleData_SO` 5、`ArchivesData_SO` 1、`InputManager` 被 `InputManager.prefab` + `GameEnd.unity` 引用）。
- **审计/修复工具**：`.codebuddy/plans/p53_guid_repair.py` —— 用 `git show HEAD:<old path>.meta` 的**原 guid** 与工作区同名文件的新 guid 比对，`--apply` 把旧 guid **写回新位置的 `.meta`** ⇒ 刷新后旧 guid 指到新路径 ✓（实测 7 个全修好，资产重新解析出 `MissionData_SO`/`WeaponUpgradeData_SO` 等类型）。
- **规矩**：**每批搬迁后跑一次**审计（纯文件分析、秒级）；`p53_guid_check.py` 只作辅助（它看不见 `Packages/` 的 meta ⇒ 会把包内脚本 `StandaloneInputModule`/`EventSystem` 之类误报为缺失）。
- **另一条**：**抽取代码块到新文件时 using 不会跟着走**（`AirdropData.cs` 抽出后 `CS0246`：`InspectorName`/`I_Actor`）⇒ 抽取脚本要把原文件的 using 头一并带过去。

**④ ⚠⚠ 契约替换的铁律：「存在性判断」≠「状态判断」**（2026-10-01 `ModifyTerrain` 实拍回归）
- 把 `X.Instance != null` 这类**存在性守卫**换成服务成员时，**必须用"服务已就位"语义**（如新增 `IBattleService.IsPresent`：实现方恒 `true`、空对象 `false`），**不能**用 `IsStartBattle`/`IsTeamWiped` 这类**状态位**。
- **为什么危险**：`ServiceLocator` 的槽位被 `NullServices` 兜底成**永不 null** ⇒ 状态位在"服务还没注册"时返回中性值（false/0/空表），于是 `if (X.IsStartBattle)` 这类守卫**静默为假** ⇒ **整段逻辑被跳过**，而**编译 0 error、无任何日志** —— 只能靠**运行时冒烟**发现（本次就是用户进 Play 才看到 `ModifyTerrain` 不清地形物体）。
- **排查手法（决定性）**：`git show HEAD:<原路径>` 把原始判断式对出来逐条比语义。本次比对：`InterestPoint`（原本 `!Instance.IsStartBattle` ✓ 等价）、`MissionView`（原本 `!Instance || !Instance.IsStartBattle` ✓ 等价）、**`ModifyTerrain`（原本 `Instance != null` ⇒ 只有这处真错）**。
- **推论**：**批量契约替换之后必须做运行时冒烟**；"编译 + 反射通过"不足以证明等价。

#### §5.1 架构收尾（定位器 → 显式注入）：已完成 / 剩余（2026-10-01 实测）
**✅ 已完成（本轮，编译 0 error）**：把 `ServiceLocator` 与 `WindowRegistry` 的槽位 setter 收成 **`internal set`**，并用
`[assembly: InternalsVisibleTo("09_Managers")]`（ServiceLocator，注册方=各服务实现）与 `("10_UI")`（WindowRegistry，注册方=`WndHub`）
**只放给注册方写** ⇒ 下层（`05_UnitCore`/`06_Gameplay`/`10_UI`/`10_Effect`…）**只能读，编译期保证**（"谁都能改全局状态"这一最危险的面已关闭）。
另加 **`ServiceLocator.Dump()`**（打印各槽当前实现类型，未注册会看到 `Null*Service`）便于排查启动顺序问题。

**⏳ 剩余（多轮专项，实测规模）**：`ServiceLocator.*` 共 **~180 个消费点**：

| 消费方 | 点数 | 显式注入的难点 |
|---|---|---|
| `06Gameplay/02Game` | 129 | 大量 MonoBehaviour / 静态上下文 ⇒ 必须先有**装配根**（工厂或 Init 注入） |
| `06Gameplay/Common` | 18 | 同上 |
| `01Manager/*` | 10 | 同集内可直接引具体管理器（**低价值**，可顺手回退） |
| **`05_UnitCore`** | **9**（`Damageable 3`、`Health_AboState 2`、`HealthEnemy`/`Actor`/`HealthPlayer`/`HealthShield` 各 1） | 这些是 **prefab 实例化的 MonoBehaviour** ⇒ 注入只能经"生成单位的装配点"（`BattleManager.CreatUnit`/对象池） |
| 其它 | ~10 | `Res 6`、`Net 6`、`Room 4`、`Archive 3`、`Path 2` |

- `WindowRegistry` 消费点 **15**（`WndManager` 的 5 个动词 + `WndHub` 登记）；要删除它需把 `IWindowService` 拆成
  "**状态**（`WindowState`/事件，由管理器实现）" + "**UI 能力**（弹提示/倒计时/开关窗口，由 UI 实现）"两个契约，
  影响 `ServiceLocator.Wnd` 的 ~25 个调用点。
- **建议路线（每轮一个服务，编译 + Play 冒烟后再删槽）**：① `Room`/`Path`/`Archive`/`Net`（约 15 点，装根最简单）
  ② `Res`/`Task`/`Vfx` ③ `Flow`（30 点）④ `Battle`（73 点，最后）⑤ 删除 `ServiceLocator`；`WindowRegistry` 随 `Wnd` 一起收。

##### ⚠ 阶段① 实测可行性（2026-10-01，**结论：`Net` 无法低成本显式注入**）
四个服务的消费点**实际只有 9 处**（比预估的 15 少）：
| 服务 | 消费点 | 消费方性质 → 能否注入 |
|---|---|---|
| `IRoomService` | `EnemyController:305`、`MissionOilRefining:51` | 单位/任务组件（**prefab 实例化**）⇒ 只能在"生成它们的装配点"注入 |
| `IPathService` | `EnemyController:259` | 同上 |
| `IArchiveService` | `PlayerInputHandler:436`、`PlayerController:152` | 玩家 prefab ⇒ 只能在其实例化处注入 |
| **`INetService`** | **`LogicBehaviour:20/22/31/37`（4 处）** | **是 `LogicBehaviour` 基类**（26 个子类共用）⇒ 注入必须落到**每一个创建者** ✗ **不可行** |

⇒ **两条路，需拍板**：
- **A（建议）：把 `ServiceLocator` 认定为"已声明的、被强制的接缝"，架构收尾到此为止。** 依据：① asmdef 已经把"下层引上层类型"变成**编译错误** ⇒ 耦合目标已达成；② 定位器已硬化（`internal set` + `InternalsVisibleTo`，下层只能读）；③ 对 prefab 实例化的 MonoBehaviour，DI 天然退化成"要么定位器、要么改所有创建点"，而后者在 Unity 工程里收益极低。
- **B：继续迁移**（只对 5 处 `Archive`/`Room`/`Path` 做注入，`Net` 留定位器）⇒ ⚠ 会**同时存在两套机制**（定位器 + 注入字段），一致性反而更差，不推荐。

**✅ 同批完成（2026-10-01，编译 0 error）**：`Assets/Editor/ProjectEditors.asmdef`（41 cs，Editor-only）+ `Assets/Shader/Editor/ShaderEditors.asmdef`（3 cs）⇒ **`Assembly-CSharp-Editor` 44 → 0**。
**✅ 后续收尾（2026-10-01 实测）**：`Assembly-CSharp` 里残留的那个第三方 WarFX 脚本也已消灭 ⇒ **预定义程序集总计 = 0**（`CompilationPipeline.GetAssemblies()` 实测 `Assembly-CSharp` / `Assembly-CSharp-Editor` **都不存在**）、**自有 asmdef 28 个**、`Assets/Scripts` 497 cs + `Assets/Editor` 47 cs。⇒ `autoReferenced:false` 的前置已满足（但其收益现在很小：asmdef→asmdef 本来就要求显式 references）。

### Phase 2 · 数据层下行 —— ✅ **2-A / 2-B 均已完成（2026-10-01，编译 0 error）**
> **2-A**：`02Data` → 独立程序集 `04_Data`（14 cs）。
> **2-B**：契约抽取主体完成 —— `01_GameContract` 现 **40 cs**（分 `Services/`11 `Entities/`12 `Data/`6 `Enums/`9 + 根 2），asmdef references 只有 `00_Core` + `00_Attribute`（方向干净、无环）。

#### Phase 2-A：把 `02Data` 变成独立程序集 `04_Data`（已完成）
做法**与原方案不同**：没有去改 SO 的字段类型（那要动 `PlayerController`/`SelectRoleWnd`/资产），而是**先把"引用逻辑类型的 SO 簇"整体搬出 `02Data/`**，再给剩下的纯数据 SO 建 asmdef。

1. **5 个跨层枚举下沉 `01_GameContract/`**（脚本按行号精确抽取，不手抄；`sanity check` 不匹配就 abort）：
   | 类型 | 原声明点 | 新位置 | 命名空间处理 |
   |---|---|---|---|
   | `OOPartEnum` | `01Manager/Global/PropertyManager.cs:44` | `00GameContract/OOPartEnum.cs` | 原为全局 ⇒ **原样保留**（零调用点改动） |
   | `MissionEnum` | `01Manager/Global/TaskManager.cs:512`（155 个成员，含显式值 100~204/300） | `00GameContract/MissionEnum.cs` | 同上 |
   | `SizeType` | `TaskManager.cs:698` | `00GameContract/SizeType.cs` | 同上 |
   | `WeatherType` | `01Manager/Battle/WeatherType.cs`（整文件） | `00GameContract/WeatherType.cs` | 整文件 `Move-Item`（含 `.meta`） |
   | `TerrainType` | `08Map/GenerateNoiseTerrain.cs:11`（在 `namespace FpsGame.MapUtils` 内） | `00GameContract/TerrainType.cs` | 改为**全局命名空间**（全仓无 `FpsGame.MapUtils.TerrainType` 限定写法）⇒ 零调用点改动 |
   ⚠ 连带：`08_Map.asmdef` 必须补 `GUID:57eb3f01c587cb542babeb6b0981d9fd`(`01_GameContract`)，否则 `GenerateNoiseTerrain` 里 31 处 `TerrainType` 失联。
   ⚠ 枚举**成员顺序/显式值绝对不能变**（资产按整数序列化）⇒ 所以用行号抽取而不是凭记忆重打。
2. **6 个"引用逻辑类型"的 SO 迁出 `02Data/` → 新目录 `02Game/Game/Data/`**（仅 `Move-Item` 文件+`.meta`，**零代码、零资产改动**，GUID 不变故资产引用不断）：
   `RoleData_SO`（持 `List<WeaponPlayerController>`）、`ArchivesData_SO`（调 `GlobalEventSub.OnGainExp`，且 `Resources.Load<RoleData_SO>`）、`MissionData_SO`（持 `MissionBase controller`）、`MissionMainData_SO`（`: MissionData_SO`）、`WeaponModuleData_SO`（`List<ModifyAttrData>` + `ResSvc.Instance.LoadSprite`）、`WeaponUpgradeData_SO`（`List<ModifyAttrData>` + `OOPartEnum`）。
   迁移判据（脚本自动 abort）：`02Data` 剩余文件若仍引用这 6 个类型则拒绝迁移。
3. **`02Data/04_Data.asmdef`**：`autoReferenced: true`，`references = 00_Attribute + 00_Core + 01_GameContract + 00_Utils`，`src=14`。
4. **验收（实测）**：`scriptCompilationFailed=False`；`read_console(types=["error"])` 0 条；`CompilationPipeline.GetAssemblies()` → `[04_Data] src=14 refs=00_Attribute 00_Core 01_GameContract 00_Utils`；`P2DataLeak.ps1` 复查 `02Data` 出边只剩 `00_Core(18) / 01_GameContract(12) / 00_Attribute(4)` —— **全在下层**。
5. **迁移后 `02Data` 保留的 14 个文件**：`AboStateData_SO`/`AirdropData_SO`/`Booster_SO`/`CampData_SO`/`MapData_SO`/`NoticeTree_SO`/`SoundGroup_SO`/`UpdateData_SO`/`VehicleData_SO` + `Variant/`×5。

⚠⚠ **本次最贵的教训：类型名扫描看不见「扩展方法」**。`SoundGroup_SO.cs:63` 的 `clips.RandomTake()`（`RandomTake` 声明在 `00Tools/Test/RandomUtils.cs`，属 `00_Utils`）在 `P2DataLeak.ps1` 里**完全不可见**（它不是类型名），asmdef 一建好就报 `CS1061`。⇒ 结论：**静态扫描只能给候选集，asmdef 的引用清单最终必须由编译器确认**；给新程序集建 asmdef 时，先把"可能的工具程序集"（`00_Utils` 这类）一并引用上，再看编译器是否报 `CS0246/CS1061`。相关工具脚本：`.codebuddy/plans/P2DataLeak.ps1`（类型出边）、`.codebuddy/plans/P2ExtractEnums.ps1`（行号精确抽取枚举）。

#### Phase 2-B：契约抽取 —— ✅ 主体完成（2026-09-30，编译 0 error，`01_GameContract` `src=16`）

**已下沉 7 项**（全部保持**原命名空间** ⇒ 零调用点改动；新文件都在 `00GameContract/`）：

| 契约 | 来源 | 手法 |
|---|---|---|
| `IEquippable` + `EquippableFlagEnum` | `02Game/Interface/IEquippable.cs` | 整文件搬（全局命名空间） |
| `ISubmittableHandItem` | `02Game/Interface/ISubmittableHandItem.cs` | 整文件搬（全局） |
| `IStepPress` | `02Game/05Interactable/IStepPress.cs` | 整文件搬（全局）；⚠ 顺手把 XML 里指向上层的 `<see cref="PlayerOperationController"/>`/`<see cref="IFurniture.Handle"/>` 改成纯文本，避免 CS1574 |
| `FurnitureFlag` + `IFurniture` | `Furniture_Attached.cs:13-61` | 行号抽取 → 新文件 `FurnitureContract.cs`（保留 `namespace FPSGame.Furn`）；`Furniture_Attached.cs` 334→285 行 |
| `IVehicleUIController` | `04UI/UI/VehicleUI.cs:11-25` | 行号抽取（全局）；**解开 `02Game/Gameplay/Bag → 04UI` 的硬依赖** |
| `TargetCfg` | `02Game/Game/UnitQueryGrid.cs:269-278` | 行号抽取（全局） |
| `IDamageData` | `02Game/Game/Shared/Weapon/DamageData.cs:12-46` | 行号抽取 → 新文件保留 `namespace Unity.FPS.Game`（数据类留在原文件） |

**有意延后的 3 项（附理由，别当成漏做）**：
- ⏸ `ProjectileHitData`（`ProjectileBase.cs:110`）：含字段 `WeaponBaseController weapon`（02Game 的 MonoBehaviour）⇒ 要下沉必须先把该字段降级成接口/`GameObject`。**它本来就该和 `ProjectileBase` 一起进 P4 的 `05_UnitCore`**，随那时一起处理更合适。
- ⏸ `ModifyAttrData`（`WeaponUpgradeController.cs:502`）+ `ModifierType`/`WeaponAttrType`（`WeaponAttributeFactory.cs:137/192`，实测是**命名空间级**声明、可安全抽）：它的下游 `WeaponModuleData_SO`/`WeaponUpgradeData_SO` **已随 P2-A 迁出数据层**，⇒ 下沉的"解锁作用"已消失，属纯洁癖改造，风险（波及属性抽屉与 `[Compare]` 表达式）不值当。**降级为 P5 可选**。
- ⏸ `IUnit`/`I_AIController` 瘦身（`AIController.cs:14/26`）：现有成员引用 `WeaponBaseController`/`DetectionModule` ⇒ 必须瘦身成只含 `I_Actor` 能表达的成员，是**改契约 + 改实现**的活。归入 **P4 `06_AI` 拆分**一起做。

**验证**：`scriptCompilationFailed=False`；`read_console(types=["error"])` 0 条；`[01_GameContract] src=16`（5 个下沉枚举 + 7 个新契约 + 原有 4 个）；全量矩阵复查 **未引入任何新环**（`01_GameContract` 的出边只有 `00_Core`）。
⚠ 手法的通用教训：**抽取"藏在实现文件里的契约"必须用行号精确抽取**（`.codebuddy/plans/P2BExtractContracts*.ps1`），且脚本要带 sanity check + 拒绝迁移的前置检查；`Cut-Range` 之类的 helper 别用 `return ,$array`（会把内容喷到 stdout）。
候选与声明点（2026-09-30 实测）：
| 契约 | 现在藏在 | 下沉可行性 |
|---|---|---|
| `IEquippable`+`EquippableFlagEnum` | `02Game/Interface/IEquippable.cs`（688 B） | ✅ 几乎零风险（整文件搬） |
| `ISubmittableHandItem` | `02Game/Interface/ISubmittableHandItem.cs`（596 B） | ✅ 同上 |
| `IStepPress` | `02Game/05Interactable/IStepPress.cs`（1.3 KB） | ✅ 先核对它引用的成员类型是否都在契约层 |
| `IFurniture`+`FurnitureFlag` | `02Game/05Interactable/Furniture_Attached.cs:14/36` | ⚠ 需拆文件；先核对 `IFurniture` 成员签名里有没有 02Game 类型 |
| `TargetCfg` | `02Game/Game/UnitQueryGrid.cs:270` | ⚠ 同时 `UnitQueryGrid` 自身要下沉到 `05_UnitCore` 才有意义 |
| `ProjectileHitData` | `02Game/Game/Shared/ProjectileBase.cs:87` | ⚠ 需连同 `ProjectileBase` 的定位一起定（P4） |
| `IDamageData` | `02Game/Game/Shared/Weapon/DamageData.cs:15` | ⚠ 该文件 19.5 KB，`IDamageData` 与数据类混装，拆时注意 `DamageDataDrawer` 只认字段名 |
| `ModifyAttrData` | `02Game/Game/Shared/Weapon/WeaponUpgradeController.cs:502` | ⚠ 它含 `WeaponAttrType`/`ModifierType`（在 `WeaponAttributeFactory.cs:137/192`）⇒ 下沉要连这两个枚举一起，会波及抽屉与 `[Compare]` 表达式 |
| `IUnit`/`I_AIController` | `02Game/AI/Controller/AIController.cs:14/26` | ⚠ `I_AIController` 现有成员引用 `WeaponBaseController`/`DetectionModule` 等 ⇒ **必须先瘦身成只含 `I_Actor` 能表达的成员** |
| `IVehicleUIController` | `04UI/UI/VehicleUI.cs:11` | ✅ 下沉后解开 `02Game/Gameplay/Bag → 04UI` 的硬依赖 |

> 剩余 3 项（`ProjectileHitData` / `ModifyAttrData`+属性枚举 / `IUnit`+`I_AIController`）的处理时机见上方"有意延后"说明 —— **P2 到此收尾**，下一步是 Phase 3（ServiceLocator + 事件总线下沉）。

### Phase 3 · 服务定位器 + 事件总线下沉（最大收益块）—— ✅ **已完成（2026-10-01，编译 0 error）**
> `ServiceLocator`（10 槽：`Task/Battle/Flow/Vfx/Net/Res/Archive/Wnd/Room/Path`）+ `NullServices` + 三条事件总线（`UnitEventSub` / `GlobalEventSub` / `BattleEventSub`）均已下沉到位；**槽位 setter 已收成 `internal set` + `InternalsVisibleTo`**（下层只读，编译期保证）。
> ⚠ 遗留决策见 §5.1：`ServiceLocator` 是否继续换成显式注入（**建议 A：认定为受控接缝、到此为止**，待拍板）。以下保留 S0~S4 执行记录。
**③ `AudioSvc` → `03_Audio` —— ✅ 已完成（2026-09-30，编译 0 error）**
- `AudioSvc.cs` 从 `02Game/Game/Managers/` 移到新目录 **`Assets/Scripts/03Audio/`**（+`.meta` 保留 guid），建 `03_Audio.asmdef`（refs = `00_Core` + `00_Utils`，`src=1`）。
- 反转掉的 4 处上行依赖（`AudioSvc` 原先 4 处直接依赖 01Manager）：
  | 原依赖 | 反转方式 |
  |---|---|
  | `GlobalEventSub.OnGameStateChange += InGameStateChange` | 改 **public 方法** + 由 `AudioEventBridge`（01Manager，新文件）转发 |
  | `GlobalEventSub.OnSettingCange += OnSettingCange` | 同上 ⇒ 改 public 方法 + 转发 |
  | `ResSvc.Instance.LoadAudio`（`PathToCilp` 重写） | 新增 `public static Func<string,bool,AudioClip> ClipLoader`（桥上注入 `ResSvc.LoadAudio`） |
  | `GameRoot.CreateTimer`（`Suppressed` 内 3 处） | 新增 `public static Func<Action,float,int,LogicTimer> TimerRequest`（桥上注入 `GameRoot.CreateTimer`）—— **计时语义逐字保持**（`0.05s×20` 压低、`time` 秒后 → `0.05s×1` 恢复） |
- 新文件 **`01Manager/Global/AudioEventBridge.cs`**：`[RuntimeInitializeOnLoadMethod]` 里一次性挂两个事件转发 + 注入 `ClipLoader`/`TimerRequest`；⚠ 全进程只订阅一次（比原先"每个 AudioSvc 实例都在 Awake 订阅一次"更安全）。
- ⚠ 注意：`GameRoot.CreateTimer` **虽声明在 `00Core/GameRootBase.cs:67`（00_Core）**，但调用必须写 `GameRoot.CreateTimer`（`GameRoot` 在 01Manager）⇒ 下层**仍然不能直接调**，只能用委托反转。
- 验收：`failed=False`、Console 0 error、`[03_Audio] src=1 refs=00_Core 00_Utils`、`[Assembly-CSharp] src=314 含AudioSvc.cs=False`。

**④ `03_EventBus` —— 🚧 S0/S1/S3 已完成（2026-09-30，编译 0 error），S2/S4 待做。⚠ 下面 1~3 条已被 S0 的实测结论**修订**，最新方案见本节末尾「S0~S4 执行记录」**
1. **前置补契约**（缺了就别想下沉）：把 `SpeechTypeEnum` 从 `RoleData_SO.cs` 抽到 `01_GameContract`（与 `WeaponTypeEnum` 同批）、新建 `I_Mission`（`MissionBase` 的只读面）、定位 `RuntimeSoundData` 并下沉（若在 02Game）。
2. **签名契约化（分批编译，每批 3~5 个事件）**，`GlobalEventSub` 的 18 个事件里只有 4 个需要改：
   | 事件 | 现签名 | 处置 |
   |---|---|---|
   | `OnSwitchRole` | `Action<PlayerController>` | → `Action<I_Actor>` |
   | `OnFriendCreate` | `Action<Actor>` | → `Action<I_Actor>`（订阅方需要具体成员时 `is Actor a` 转换） |
   | `OnSelectRolePreview` | `Action<RoleData_SO>` | → `Action<GameObject>`/`ScriptableObject` |
   | `OnPlayMeetSpeech` | `Action<GameObject,SpeechTypeEnum>` | 枚举下沉后即干净 |
   其余 14 个（`GameStateEnum`/`float`/`string`/`bool`/`GameObject`/`IFurniture`/`OOPartEnum`/`I_Actor`）**本来就干净** ⇒ 直接下移即可。另需清 `using static AirdropController;`（01Manager/Battle）与 `using Unity.FPS.Game;`/`FpsGame.Mission;`。
   ⚠ `BattleEventSub`（≈135 处引用）需**同样做一次分类**（我从记忆里已知：`MissionBase` 出现在 6 个签名、`Actor` 占多数、`NoiseData` 已干净）——**执行前先把它整份读完分类**，不要凭猜测改。
3. **分流**：订阅者 ≤2 的事件改成**实例事件**（符合项目既有约定"少塞 `BattleEventSub`"），缩小总线面积。
4. **下移 + 建集**：文件移到 `Assets/Scripts/0xEventBus/`，建 `03_EventBus.asmdef`（refs = `00_Attribute` + `00_Core` + `01_GameContract`，**若签名里保留 Data 类型则需 `04_Data` 并在其下**⇒届时把 Data 排在总线之前，Data 不引用总线，可安全换序）。
5. **验收**：`03_EventBus` 出边只有 `00_Core`/`01_GameContract`(+`04_Data`)；`01Manager → 02Game` 回边从 135+ 降到个位数；`AsmDepScan.ps1` 复查无新环。

> **为什么即使选"不分家"也必须做 ④**：`05_UnitCore`（`Actor`/`Health`/`Damageable`）是总线**最大的生产者**（`Actor.cs` 一家就调 `BattleEventSub` 34 次）⇒ 想让 `05_UnitCore` 独立成集，总线必须在它**下面**。所以 ③④ 是 `05_UnitCore` 的必要前置。
> 而 ①②（`ServiceLocator` + 6 个服务接口）在"不分家"方案里**可以缩减**为只做 `05_UnitCore` 真正需要的那几个（`Health`/`Damageable` → `TaskManager`/`BattleManager`）。

#### S0~S4 执行记录（2026-09-30 实测，**取代上面的第 1~3 条**）

**S0 ✅ 事件分类（两个总线共 48 个事件）**
- **本次最关键的判据**：**一个事件该放哪一层 = 该事件的 `min(发布者层, 订阅者层)`** —— 静态总线类必须能被"发布方"和"订阅方"两侧都引用。
- 据此分类（逐事件实测发布者/订阅者所在目录）：
  | 归属 | 事件 | 依据 |
  |---|---|---|
  | **必须下移**（min ≤ `05_UnitCore`） | 单位类 12 个：`OnUnitPosChange`/`OnUnitDeath`/`OnUnitKill`/`OnUnitHit`/`OnEnemyCreate`/`OnEnemyDead`/`OnSpecUnitCreate`/`OnSpecUnitDead`/`OnFriendCreate`/`OnPlayerCreate`/`OnPlayerDead`/`OnPlayerRevive`；噪声表现类 2 个：`OnNoise`/`OnBulletHit` | 发布者在 `Actor`/`Health`/`Damageable`/`ActorsManager`/`FpsHelper`（都将进 `05_UnitCore`） |
  | **留在上层总线** | 任务 7 个、空投 5 个、`OnWipeFailCountdown/Cancel`、`OnCallKai`、`OnEvacuate`、`OnMark`、`OnGainExp`、`OnSwitchRole`、`OnSelectRolePreview`、`OnFurnitureOperate`、`OnViewSwitch`、`OnOOPartCollect`、`OnKeiSubmit`、`OnPlayMeetSpeech`、`OnActorSpeech`、`OnGameStateChange`、`OnTimeScaleChange`、`OnSceneChange`、`OnWndSwitch`、`OnDaySwitch`、`OnSettingCange` | 发布者与订阅者**都在** managers/gameplay/UI（例：空投＝发布者 `AirdropController`(01Manager)、订阅者 `AirdropWnd`(UI)） |
- ⇒ **结论：不是"整体下移一个总线"，而是"按 min 层拆成两个总线"**。这直接推翻了"必须抽 `I_Mission`"的假设。

**S1 ✅ 契约缺口（只做了 1 件，另 3 件经核查不需要）**
| 原计划 | 实测结论 |
|---|---|
| `SpeechTypeEnum` 下沉 | ✅ 从 `RoleData_SO.cs:82-200` 抽到 `00GameContract/SpeechTypeEnum.cs`（**保留 `namespace Unity.FPS.Game` ⇒ 零调用点改动**），源文件 201→82 行 |
| 新建 `I_Mission` | ❌ **不做**。任务事件留上层总线 ⇒ 总线保留 `MissionBase` 即可。抽它会波及：`m_ObjectivesDictionnary` 的键类型、`mission.parent` 的类型、`==null` 语义（接口引用判不出 Unity 已销毁对象，得改 `IsValidMono()`）、`mission.data.reward`（`data` 是 02Game/Game/Data 的 `MissionData_SO`，契约层看不见）⇒ 代价 ≫ 收益 |
| `RuntimeSoundData` 下沉 | ✅ **已在 `04_Data`**（`02Data/SoundGroup_SO.cs:426`）⇒ 无需动 |
| （新）`NoiseData` | ✅ **不用抽**：声明就在 `BattleEventSub.cs` 末尾（`:175`），随文件搬即可（只用 `GameObject`+`PEVector3`+`PEInt`） |
| （新）`AirdropData`+`AirdropState` | ❌ **判归上层，不搬**：`AirdropData`(`AirdropController.cs:398`) 含逻辑且字段是 `AirdropData_SO`(04_Data)，只适合搬去 `04_Data`；但空投事件属上层总线 ⇒ 搬它零收益 |

**S2 ✅ 结论：不需要做（方案已在 S4 里被更省的做法取代）**
原计划要把下移总线里 10 个事件的 `Action<Actor>` 改成 `Action<I_Actor>`（连带 20 个订阅处理函数要改签名 + 加 cast）。**改为：让下移总线与 `Actor` 类同处一个程序集**（都放 `02Game/Game/`，将来一起进 `05_UnitCore`）⇒ 签名里直接用具体类型 `Actor`，**12 个事件与全部订阅方一个字都不用改**，避免 20 处无谓改动与 cast 风险。这也是"事件层 = min(发布者层, 订阅者层)"判据的直接推论：与其把签名抽象化，不如把总线搬到与发布者同层。

**S3 ✅ 完成（结论与原设想相反，但这是实测结论）**
- ✅ **删死事件**：`OnFriendDead`/`OnFriendRevive` 全仓 `+=`/`-=` **零命中**（只有 `Actor.cs:324` 在发布）⇒ 连发布调用一起删（`BattleEventSub.cs` 留注释说明）。
- ❌ **不做"≤2 订阅者 → 实例事件"的批量转换**。判据 + 实证：
  1. **判据**：只有同时满足 (a) 发布者层 ≤ 订阅者层（否则给 UI 制造对 managers 的向上依赖）(b) 订阅者**已持有**发布者实例 (c) 发布者是**单实例**（否则 UI 面对 N 个实例无法订阅）—— 才值得转。
  2. 实测满足 (a)(b)(c) 的只有两组：`OnAuthorizeAirdrop`/`OnInputAirdrop`（发布者 `AirdropController`，订阅者 `AirdropWnd` 已有 `controller` 字段）与 `OnWipeFailCountdown`/`OnWipeFailCancel`（发布者 `BattleManager` 单例，订阅者 `DeathUI` 已在用 `BattleManager.Instance`）。
  3. 但这两组**都属上层总线** ⇒ 转换**不减少任何跨层签名**，只有微小的维护面积收益，却要承担订阅生命周期风险（`AirdropWnd.controller` 是 `ResetAirdrop(cont)` 后才赋值，订阅时机要重排；`DeathUI` 订阅时 `BattleManager.Instance` 可能还是 null）⇒ **决定不转**。
  4. 典型**不可转**例子（写成反面教材，避免以后误判）：`OnUnitPosChange`（发布者 `Actor` 有 N 个实例 ✗）、`OnUnitKill`（发布者是"正在死的那个单位" ✗ UI 只认玩家）、`OnWndSwitch`（发布者 `WndManager` 在订阅者 `PlayerInputHandler` **之上**，转实例即制造向上依赖 ✗ —— 这正是总线存在的意义）。

**S4 ✅ 已完成（2026-09-30，编译 0 error，反射验证通过）**
- **新建 `Assets/Scripts/02Game/Game/UnitEventSub.cs`（下层总线，与 `Actor.cs` 同目录 ⇒ 将来随 `05_UnitCore` 一起下沉）**：
  - 14 个事件：`OnUnitPosChange`/`OnUnitDeath`/`OnUnitKill`/`OnUnitHit`/`OnBulletHit`/`OnNoise`/`OnEnemyCreate`/`OnEnemyDead`/`OnSpecUnitCreate`/`OnSpecUnitDead`/`OnPlayerDead`/`OnPlayerRevive`（原 `BattleEventSub`）+ `OnPlayerCreate`/`OnFriendCreate`（原 `GlobalEventSub`，**发布者都是 `Actor.cs`** 故必须同层）；
  - `NoiseData` 结构随 `OnNoise` 一起搬入（它原本就声明在 `BattleEventSub.cs` 末尾，不用单独抽）；
  - 签名**保持具体类型 `Actor`**（见 S2 结论）；⚠ 新文件必须 `using Unity.FPS.Game;`（`Actor` 在这个命名空间，漏了会报 18 条 `CS0246`）。
- **上层总线**留在原位：`GlobalEventSub`（14 事件）+ `BattleEventSub`（15 事件：空投/任务/撤离/团灭/呼叫凯伊），后续随 `09_Managers`/`06_Gameplay` 归位。
- 调用点迁移：脚本按"事件名白名单 + 词边界"批量改类名前缀，**两次共 97 处**（`BattleEventSub.单位类事件`→`UnitEventSub.*` 69 处/19 文件；`GlobalEventSub.PlayerCreate|FriendCreate`→`UnitEventSub.*` 28 处/10 文件）。⚠ 批量重写文件后要用 `git diff --stat` 抽查 diff 行数是否与替换数成正比（验证编码没被翻）。
- **验收（反射读已加载程序集，比读源码可靠）**：`GlobalEventSub` 14 事件 / `UnitEventSub` **14 事件** / `BattleEventSub` 15 事件；`scriptCompilationFailed=False`、Console 0 error。
- **对 P4 的意义**：`05_UnitCore` 提取时，`UnitEventSub.cs` 只是"跟着 `Actor.cs` 走"，不再有签名改造或跨层事件残留 ⇒ **P4 的前置障碍已清空**。

**①② `ServiceLocator` + 服务接口（最小集）—— ✅ 已完成（2026-09-30，编译 0 error）**
> 原计划是"6 个服务接口覆盖 300+ 处单例调用"。实际先做**最小集**：按"`05_UnitCore` 到底需要什么"倒推，只开 3 个成员。

- **倒推过程（可复用的做法）**：把 `05_UnitCore` 的 17 个候选文件（`Actor`/`UnitQueryGrid`/`UnitEventSub`/`ActorsManager`/`Health*`/`Damageable`/`Shield*`/`TransferDamageable`/`AutoDeath`/`MinMaxParameters`）逐个扫 `.Instance` + 上层类型名，得到**全部上行依赖只有 3 个成员**：
  | 消费点 | 原写法 | 现在 |
  |---|---|---|
  | `Damageable.Awake:115-116`、`HealthEnemy.Awake:19-20` | `TaskManager.Instance.nowTask.ExtraDifficulty[3]` / `.difficulty` | `ServiceLocator.Task` + `ITaskService`（2 个成员） |
  | `Damageable:266,271`、`HealthPlayer:36`、`HealthShield:33` | `BattleManager.Instance.HaveBooster(BoosterType.X)` | `ServiceLocator.Battle` + `IBattleService`（1 个成员） |
  | `ShieldBehaviour:66` | `AudioSvc.CreatSource` | **无需处理**：`AudioSvc` 已在 `03_Audio`（下层）✓ |
- **交付物**：
  1. `00GameContract/ServiceLocator.cs`（新）——只含 `Task`/`Battle` 两个属性；注释里写明**边界（不要用它藏随便什么数据）与退出条件（`09_Managers` 成集或改显式注入后删除）**。
  2. `00GameContract/Interface_Manager.cs` —— 该文件原本是**空的 `namespace GameContract {}` 占位**，正好填入 `ITaskService`/`IBattleService`（注释里写明"只开下层真正用到的成员"）。
  3. 枚举下沉（保留原有全局命名空间 ⇒ 零调用点改动）：`DifficultyEnum` 从 `TaskManager.cs:513-523` 抽出、`BoosterType` 从 `Booster_SO.cs:3-43` 抽出（⚠ 它原本在 `02Data`＝`04_Data`，契约层直接用它会造成**层级反转**，必须先下沉）。
  4. **数据自持**（不是服务）：`ResSvc.aboStateDic` 的存储下沉到 `AboStateData_SO.Dic`（`04_Data`），`ResSvc` 保留同名**只读转发属性**兼容旧调用点；`Health`/`Health_AboState` 的 6 个读点改读 `AboStateData_SO.Dic`。⇒ 判据：**"数据"下沉到数据层自持，"能力"才进服务定位器**。
  5. 注册：`TaskManager.Init()` 与 `BattleManager.Awake()` 各一行 `ServiceLocator.X = this;`；两个管理器分别显式/隐式实现接口（`TaskManager` 用**显式实现**，避免污染它的公开面）。
- **null 语义（有意改进）**：原代码在 `nowTask` 为 null 时是 **NRE**；现在 `TaskManager` 的显式实现返回中性值（`ExtraDifficulty` 全 0、`Difficulty` = `Normal`），消费方用 `ServiceLocator.Battle != null && ...` / `taskSvc != null ? ... : 0`。⇒ 契约里写明"实现方不得返回 null"。
- **验收**：`failed=False`、Console 0 error、`[01_GameContract] src=20`（+`DifficultyEnum`/`BoosterType`/`ServiceLocator`）、`[04_Data] src=14` 不变；**17 个 UnitCore 候选文件的上行依赖扫描结果为空**。
- **遗留（等对应 Phase 需要时再开）**：`IResService`(除 abo 缓存外还有 `LoadAudio`/`LoadSprite`)、`IWndService`、`IGameRootService`、`IArchiveService` 尚未开；`ServiceLocator` 本身是过渡设施。

**P4 前置（`05_UnitCore` 的上行依赖）—— ✅ 2026-09-30 全部清零（编译 0 error）**
> 教训先行：**"扫上行依赖"要用两层漏斗**。第一遍我用 `.Instance` + 关键词扫 19 个候选文件，只看到 3 处；第二遍改用**"项目已声明类型名全集"**扫，才补齐 `IUnit`/`UnitAttrType`/`VFXManager`/`FpsHelper` 4 处 ⇒ 共 6 处真阻塞。**永远别用手工关键词清单。

| 阻塞（原始位置） | 处理方式 |
|---|---|
| `Actor.cs:238` → `FpsHelper.IsMainStage()`（`00Tools` 根＝Assembly-CSharp） | 新增 **`IFlowService`**（`GameState` + `IsMainStage`），由**状态所有者 `GameRoot`** 实现、并在 `Awake` 里**单例判定之后**注册；`Actor` 加私有 `IsMainStage()` 转调。⚠ **不能并进 `IBattleService`**：Bridge/Ready 场景没有 `BattleManager`，而这段等待逻辑恰恰要在这些状态下返回 true |
| `Health_AboState:193,205` → `VFXManager.Creat/Release` | 新增 **`IVfxService`**（只开 `GameObject` 这一对入口；`ProjectileBase` 重载属武器层，不进契约），`VFXManager` **显式实现**（转发到既有静态入口）并在 `Init()` 注册 |
| `Health_AboState:36,50,191` → `IUnit` + `UnitAttrType` + `GameAttribute` | **接口隔离**（不是下沉类型族）：新增 **`IUnitScale { float VisualScale { get; } }`**，由 `AIController`/`BaseSelfController` 显式实现（`((IUnit)this).GetAttribute(UnitAttrType.Size)?.FinalValue.RawFloat ?? 1f`），`Health_AboState` 只取体型。**不做** `GameAttribute` 一族下沉 —— 那要连带 `AttrTag`/`ModifierType`/`WeaponAttrType`/`UnitAttrType`/`IUnit` 5 个类型 + 561 行文件，且与 P2 延后的 `ModifyAttrData` 下沉是同一件事 |
| `ResSvc.aboStateDic`（**静态字典**，6 个读点在 UnitCore） | **数据自持**：存储下沉为 `AboStateData_SO.Dic`（`04_Data`），`ResSvc` 保留**只读转发属性**兼容旧调用点 |
- **`05_UnitCore.asmdef` 的 references（实测推导，可直接用）**：`00_Attribute` + `00_Core` + `01_GameContract` + `00_Utils` + `03_Audio` + `04_Data`。
- **P4 本体的第一步仍是"收敛目录"**（asmdef 是文件夹级）：内核文件散在 `02Game/Game/`（与 `MissionView`/`InterestPoint`/`MeshCombiner*`/`PrefabReplacer*`/`DebugUtility` 混放）与 `02Game/Game/Shared/`（与 `ProjectileBase` 混放 —— 后者带 `ProjectileHitData.weapon: WeaponBaseController` ⇒ 归 `06_Weapon`）。

### Phase 4 · 单位内核 —— ✅ **已完成（2026-09-30，编译 0 error）**
**① 目录收敛**：新建 `Assets/Scripts/05UnitCore/`，把 **19 个内核文件**（含 `.meta`，**保 guid ⇒ prefab/serialized 引用不断**）从两处搬入：
- 从 `02Game/Game/`：`Actor`、`UnitQueryGrid`、`UnitQueryGridDebugger`、`UnitEventSub`、`MinMaxParameters`、`GameConstants`
- 从 `02Game/Game/Shared/`：`Health`、`HealthEnemy`、`HealthOther`、`HealthPlayer`、`HealthShield`、`HealthSpecUnit`、`Health_AboState`、`Damageable`、`ShieldBehaviour`、`ShieldRebuild`、`TransferDamageable`、`AutoDeath`
- 从 `02Game/Game/Managers/`：`ActorsManager`（该目录随后为空，已连同 `.meta` 删除）
- **刻意留在外面**：`ProjectileBase.cs`（带 `ProjectileHitData.weapon: WeaponBaseController` ⇒ `06_Weapon`）、`MissionView`/`InterestPoint`/`MeshCombiner*`/`PrefabReplacer*`/`DebugUtility`/`CampaignCfg`/`ConstantRotation`/`Ignore*`（`07_Mission`/工具层）
- ⚠ **partial 类必须同程序集**：搬前先扫 `partial class <X>`，确认 `Health` 的两半（`Health.cs` + `Health_AboState.cs`）都在集合内 ✓

**② asmdef**：`Assets/Scripts/05UnitCore/05_UnitCore.asmdef`，`autoReferenced: true`，`src=19`。
**权威 references**（`CompilationPipeline.GetAssemblies()` 实测）：`00_Attribute` + `00_Core` + `01_GameContract` + `00_Utils` + `03_Audio` + `04_Data`（+ 自动带入的 `UnityEditor.UI`/`UnityEngine.UI`，无害）。

**③ 过程中的真阻塞与修复**（详见 §5 过渡期铁律 ②）：
- 编译报 4 条 `CS1061: 未包含 IsValid 的定义` ⇒ 根因：`IsValid` 扩展在 `00Tools/ObjectIsValid.cs`＝**Assembly-CSharp**，asmdef 看不到。
- 修复：把 `00Tools/ObjectIsValid.cs`（**全局命名空间纯工具**，只依赖 `System`/`Core.Interface`/`UnityEngine`）下沉进 `00Tools/Test/`（＝`00_Utils`），**同时删掉 `00Tools/Test/Tool.cs` 里那 2 个重复的 `internal static bool IsValid`**（否则同命名空间下两份可用扩展 ⇒ `CS0121` 二义性）。
- ⚠ 由此 `00Tools/` 根只剩 `FpsHelper*`(6)/`LogicBehaviour`/`SingletonNet`/`TechnicalDebt`（都依赖上层，等 Phase 5/6）。

**④ 验收（全部通过）**：`failed=False`、Console **0 error**、`[05_UnitCore] src=19`、程序集已加载；**反射验证 `Assembly-CSharp` 里 19 个内核类型残留 = `[]`**；依赖矩阵显示方向正确（`04UI 79`、`01Manager/Battle 54`、AI/Player/Interactable/Mission/Effect 等**上层 → UnitCore**；UnitCore → 只有那 6 个下层 asmdef）。

**⑤ 后续（P4 未尽项，属 Phase 6/§4-E）**：把 `BattleManager` 里的网格字段改为契约（不直接持有 `UnitQueryGrid`/`Actor` 具体类型）；本阶段**未动** `01Manager` 一行代码。

### Phase 5 · 玩法层 —— ✅ **已完成（2026-10-01，编译 0 error）**
> `P5-0`/`P5-1`/`P5-2`/`P5-3`/`P5-4` **全部落地**，`06_Gameplay` 已切出（**186 cs**）。
> 以下保留当年的侦察结论与执行记录（**它们是"为什么必须合并成一个程序集"的决策依据**）。

**侦察工具**：`.codebuddy/plans/p5_recon.py`（Python，符合"脚本优先 Python"约定）。相比旧 PS 矩阵有 3 处修正：① 先剥注释；② **额外收集扩展方法名**（调用点 `x.M()` 不出现类名 ⇒ 纯类型名扫描抓不到，P4 就栽在这）；③ 候选层按**文件目录路径**匹配、且跳过 `x.Set(`/`.Compare(` 这类**成员调用**噪声。
⚠ 数据仍是文本级：长名（`BattleManager`/`WeaponBaseController`/`KeyScreen`…）可靠，短名（`Set`/`Direction` 之类）仍有少量噪声。

#### 5.1 结论一：**玩法层不能逐个切，必须合并成一个 `06_Gameplay`**
候选层**互引是双向稠密图**（实测 `from -> to` 次数）：

| 互引 | 次数 | 反向 | 判定 |
|---|---|---|---|
| `08_Interactable → 07_Player` | 25 | `07_Player → 08_Interactable` 3 | ⛔ 双向 |
| `07_Player → 06_Weapon` | 13 | `06_Weapon → 07_Player` 3 | ⛔ 双向 |
| `07_Player → 06_AI` | 7 | `06_AI → 07_Player` 1 | ⛔ 双向 |
| `08_Interactable → 06_Weapon` | 14 | `06_Weapon → 08_Interactable` 2 | ⛔ 双向 |
| `06_AI → 06_Weapon` | 27 | 无 | ✅ **唯一干净的单向大边**（AI 依赖武器 ⇒ 若要分层，只能 AI 在武器之上） |
| `07_Mission → 08_Interactable` | 15 | (`08 → Mission` 无) | ✅ 单向（但原计划要"反转"它，合并后**不必反转**） |

⇒ 与 §8.3 的行业复审结论一致：**P5 = 切 1 个程序集（`06_Gameplay`），不是 5 个**。合并后 `Mission → Interactable` 的"反转 23+13 次"这一大难点**自然消失**。

#### 5.2 结论二：每层的"阻塞项"大头都是 `01Manager/*` 与同批玩法自身
| 候选层 | cs | 下游 asmdef 边 | ⚠阻塞引用 | 阻塞 Top |
|---|---|---|---|---|
| `06_Weapon`(含 `Gameplay/Projectile`) | 29 | `01_GameContract 24`, `00_Core 17`, `00_Utils 13`, `00_Attribute 5`, `05_UnitCore 5`, `03_Audio 2` | **59** | `FpsHelper 7`、`ProjectileBase/HitData/IgnoreHitDetection 13`、`VFXManager 6`、`BattleManager 4`、`LimitedLife 4`、`PlayerWeaponsManager 3` |
| `06_AI` | 44 | `00_Core 31`, `01_GameContract 27`, `05_UnitCore 18`, `00_Utils 14`, `03_Audio 9`, `00_Attribute 5`, `04_Data 2` | **65** | `WeaponBaseController 8`、`ModifierType 7`、`FpsHelper 7`、`BattleManager 6`、`WeaponEnemyController 5`、`VFXManager 5`、`PathRequestManager 2`、`BattleEventSub 2` |
| `07_Player` | 16 | `01_GameContract 24`, `00_Core 18`, `00_Utils 10`, `05_UnitCore 8`, `00_Attribute 3`, `04_Data 3`, `03_Audio 2`, `00_WndTools 1` | **57** | `GlobalEventSub 5`、`GameRoot 4`、`WndManager 4`、`ArchiveSvc 3`、`UnitAttrType 3`、`BattleEventSub 3` |
| `07_Mission` | 30 | `01_GameContract 24`, `00_Core 18`, `05_UnitCore 15`, `00_Utils 8`, `04_Data 3`, `03_Audio 3`, `00_Attribute 3` | **108** | `BattleManager 13`、`KeyScreen 8`、`BattleEventSub 7`、`WaveCreateParams 7`、`GlobalEventSub/ResSvc/GameRoot 各 4`、`MedivacController 3`、`ProcedureType 3` |
| `08_Interactable` | 38 | `01_GameContract 46`, `00_Utils 15`, `00_Core 12`, `00_Attribute 11`, `05_UnitCore 8`, `04_Data 5`, `03_Audio 4`, `00_WndTools 4` | **91** | `EquipController 8`、`PlayerController 6`、`BattleManager 5`、`WeaponController 4`、`FpsHelper 4`、`DebugUtility 3`、`IDrivable 1` |

#### 5.3 结论三：两个**必须先做**的前提

**前提 1 · `00Tools` 根的 manager 引用必须先清零 —— ✅ 已完成（P5-0）**
- 玩法层用 `FpsHelper*` 共 **49 次**：`02Game/AI 17`、`02Game/Gameplay 13`、`01Manager/Battle 6`、`Effect 4`、`02Game/03Player 3`、`05Interactable 2`、`04UI 2`、`06Npc 1`、`Game 1`；✅ **下层零使用** ⇒ `FpsHelper` 只需与玩法层同层即可被发现。
- ⚠ **方案修正（2026-09-30 实测后改口）**：原计划"把 `00Tools` 根做成下层的 `05_Facade`"**不成立**，两个原因：
  1. 6 个 `FpsHelper*.cs` 是**同一个 `public static partial class FpsHelper`** ⇒ **必须同程序集**，拆不开；
  2. 它本体要吃 `ProjectileHitData`/`ProjectileBase`/`ProjectileMelee`（武器层）⇒ 不可能落在武器层之下。
  ⇒ **正确做法：`FpsHelper*`（+ `LogicBehaviour`）随玩法层一起进 `06_Gameplay`**，只需把对 **manager** 的引用消掉（manager 永远在玩法层之上，这部分无论怎么做都得消）。
- `Plugins/DynamicBone`（4 cs，无 asmdef）**仍是阻塞**：`08_Interactable` 用它的 `Direction` 2 次 ⇒ 要么给它加 asmdef，要么让调用点走 `GetComponent`/事件。

**前提 2 · 管理器契约要扩容（实测清单，别再靠猜）**

| 目标 | 玩法层引用次数 | 建议处理 |
|---|---|---|
| `BattleManager` | 30（Mission 13 + AI 6 + Interactable 5 + Weapon 4 + Player 2） | 扩 `IBattleService`（只加真正用到的成员） |
| `VFXManager` | 11（Weapon 6 + AI 5） | 扩 `IVfxService`（武器侧要 `Creat(ProjectileBase)` 重载） |
| `GameRoot` | 11 | 扩 `IFlowService` / 或把 `CreateTimer` 一族搬进 `00_Core`（老坑） |
| `GlobalEventSub` | 19 | **不做契约**：见下 |
| `BattleEventSub` | 12 | **不做契约**：见下 |
| `WndManager` | 9 | 新 `IWndService`（或按 §4-E 改 `WindowRegistry`） |
| `AirdropController` + `AirdropData` | 12 | `AirdropData` 是 DTO ⇒ 下沉；`AirdropController` 的 `using static` 用法要先消 |
| `WaveCreateParams` | 8 | DTO ⇒ 下沉契约/`04_Data` |
| `ResSvc` / `TaskManager` / `ArchiveSvc` | 4 / 4 / 3 | 扩 `IResService` / `ITaskService` / 新 `IArchiveService` |
| `InputManager`+`InputState`、`PathRequestManager`、`RoomManager`、`NetManager`、`TaskItem`、`SelectTaskData` | 各 1~2 | 少量成员，按需开 |

- **`GlobalEventSub`/`BattleEventSub` 别做成契约**：它们是 static 事件总线，按 §5「总线层 = `min(发布者层, 订阅者层)`」⇒ **直接搬进 `06_Gameplay`** 最省。实测它们的签名带 `RoleData_SO`（`Game/Data`）、`MissionBase`、`AirdropData`（`01Manager/Battle`）、`RuntimeSoundData`（`03Audio`）⇒ 契约化要改一堆类型签名，搬迁则是零改动。上层（`01Manager`/`04UI`/`Effect`）通过 `autoReferenced` 照样能用 ✓

**前提 3（小）· 表现层外溢**：玩法层引 `Effect` 的 `LimitedLife 4`、`ChargeView 1`、`MedivacController 3`、`MedivacState 3`、`FlareGun 2` ⇒ `LimitedLife` 像通用组件（可下沉），其余要么契约化、要么随"表现层"一起上移。

#### 5.4 修订后的 P5 执行顺序
| 步骤 | 内容 | 说明 |
|---|---|---|
| **P5-0** ✅**已完成（2026-09-30，编译 0 error）** | 消掉 `00Tools` 根对 manager 的 7 处引用（详见下方 P5-0 记录）；`Plugins/DynamicBone` 是否成集另定 | 玩法层能用 `FpsHelper` 的前提 |
| **P5-1** ✅**已完成（2026-10-01，编译 0 error）** | 契约扩容（`IBattleService`/`IVfxService`/`IWindowService`/`IResService`/`IArchiveService`/`IFlowService`…）+ DTO 下沉（`AirdropData`/`WaveCreateParams`/`TaskItem`/`SelectTaskData`）；**玩法侧 manager/UI 直连 146 → 0** | 按 §5.3 表逐项，每加 2~3 个成员编译一次 |
| **P5-2** ✅**已完成** | `GlobalEventSub`/`BattleEventSub` 搬进 `06Gameplay/`（零签名改动） | — |
| **P5-3** ✅**已完成（2026-10-01，编译 0 error）** | **`06_Gameplay` 已切出**：`02Game/{Game,AI,03Player,05Interactable,06Npc,Gameplay,Util}` 整目录搬入 `06Gameplay/02Game/`（**169 cs**）+ 既有 `06Gameplay/`（16 cs：`GlobalEventSub`/`BattleEventSub`/`TaskData`/`Common/`/`Airdrop/`）⇒ **src=185**；`Assembly-CSharp` 300→**115** | references = `00_Attribute`/`00_Core`/`01_GameContract`/`00_Utils`/`05_UnitCore`/`03_Audio`/`04_Data`/`00_WndTools`/`02_Rendering`/**`FpsGame.MapUtils`**/`RootMotion`/`Unity.Burst`/`Unity.TextMeshPro` |
| **P5-4** ✅**已完成（2026-10-01，编译 0 error）** | `10_Effect` 已切出（**23 cs**）。⚠ 当年标"⏸ 被 Phase 6 阻塞"的原因仍值得记住：`Effect/`（根 21 + `VFX/` 2）的**外部引用只有 `01Manager/*` 共 34 处**，且**全是"向上引管理器"**（合法）—— 切不了的唯一原因是**管理器还在 `Assembly-CSharp`**（asmdef 看不到）⇒ **先切 `09_Managers`**，之后 `Effect` 只需加一个 asmdef。 | 先做前置、再切 `10_Effect` 的判据见下方 P5-4 记录 |

#### P5-3 执行进展（2026-10-01）
**工具**：`.codebuddy/plans/p53_scan.py`（单一大候选集的统计：下游 asmdef 边 + 阻塞项 + 阻塞类型）与 `.codebuddy/plans/p53_ext.py`（**外部阻塞明细**：剥注释 + 收扩展方法名 + 输出 `文件:行: 类型 [所属单元] 代码`）。
⚠ **明细必须逐条看代码列**：本次识别出的假阳性包括字符串字面量（`["GuideWnd"]`/`"MiniMapWnd"`/`"Direction"`）、枚举/常量成员（`WeaponTypeEnum.FlareGun` 14 处、`Constants.HealBag` 2 处、`ProcedureType.Direction`）。

**✅ 已完成（编译 0 error）**：
1. `00Tools` 根：`FpsHelper*`(6) + `LogicBehaviour` → `06Gameplay/Common/` ⇒ 根只剩 `SingletonNet`/`TechnicalDebt`。⚠ **`SingletonNet` 被 `BridgeSys`/`NetManager` 用 ⇒ 切 `09_Managers` 前必须给它 asmdef**。
2. **`InputManager`（含 `InputState` 枚举，`InputManager.cs:135`）从 `01Manager/Global/` → `06Gameplay/Common/`** ⇒ 一举清掉 **100 处**阻塞（`InputManager 50`+`InputState 50`；`03Player 29`/`05Interactable 20`/`Effect 11`/`01Manager 15`/`04UI` 多数）。唯一上行依赖 `NowWindowState => WndManager.WindowState` 改 `GameContract.ServiceLocator.Wnd.WindowState`（语义一致：未就绪返回 `Game` ✓）。
   - **判据（可复用）**：`InputManager` 只是 `00_Core/InputManagerBase<InputState,WindowStateEnum>` 的**薄子类**、唯一上层依赖可契约化、且**所有消费者都在玩法层及以上（下层零使用）** ⇒ **搬（改 1 行）远胜契约化（改 100 处调用点）**。

**🚧 剩余真实阻塞 ≈ 49 处**（噪声已剔）：
| 阻塞 | 处数 | 处理方案 |
|---|---|---|
| `AirdropData`（**嵌在 `AirdropController` 里**，`01Manager/Battle`） | 15 | **DTO 下沉契约层**（文件作用域＝解嵌套，同 `TaskData.cs` 套路）⇒ 8 个文件里的 `AirdropController.AirdropData` 改成裸名 |
| `AirdropController` | 10 | 其 `WaitRelease` 走契约；`using static AirdropController;`(2) 随 `AirdropData` 下沉删掉 |
| `Effect` 5 类型 | 24 | `LimitedLife 9`、`MedivacController 7`+`MedivacState 3`（嵌套枚举）、`ChargeView 4`（`WeaponController.ChargeVfx` 序列化字段）、`ModifyTerrain 1` ⇒ 倾向**搬进玩法层**（谁用谁拥有；搬文件保 GUID ⇒ prefab 不断），**逐个先确认其自身无上行依赖** |
| `Plugins/DynamicBone` `Direction` | 5 | 逐条看**是噪声**（`bg.Find("Direction")`、`case ProcedureType.Direction`）；若真有引用再给插件加 asmdef |
| `04UI` 窗口名 | 4 | **全是家具 Id 字符串** ⇒ 噪声 ✓ |

**`06_Gameplay.asmdef` 的 references（实测）**：`01_GameContract 233`、`00_Core 112`、`00_Utils 64`、`05_UnitCore 58`、`00_Attribute 27`、`03_Audio 21`、`04_Data 18`、`00_WndTools 5`、`02_Rendering 1`、`08_Map 1`。
⚠ `00_WndTools` 是 `04UI/WndTool` 的 asmdef ⇒ **成集前要确认它不反向引用玩法层**（否则成环）。

**✅ 已完成第二批（2026-10-01，编译 0 error + 反射验证）**
1. **`AirdropData`/`AirdropState` 解嵌套下沉玩法层**（脚本 `.codebuddy/plans/p53_airdrop.py`：行区间原样抽取 + 去缩进 + 全局改引用，15 个文件）。`AirdropController.cs` 582→398 行；新建 `06Gameplay/Airdrop/AirdropData.cs`。
   - **为什么**不能进契约层 **：`AirdropData` 有字段 `AirdropData_SO cfg`，而 `AirdropData_SO` 在 `02Data`＝`04_Data`，**`04_Data` 反过来引用 `01_GameContract`** ⇒ 契约再引 `04_Data` 就成环。玩法层在两者之上 ✓。
   - ⚠ **抽取代码块必须连 using 一起搬**（本次漏了 ⇒ `CS0246`：`InspectorName`/`I_Actor`）。
2. **`WaitRelease` 的处理（取舍记录）**：它是 `AirdropData` 类型的静态状态，被玩法层与上层同时读写，且**要被原样传进 `BattleEventSub.CancelAirdrop(go, data)`** ⇒ **契约投影不可行**（契约命名不了玩法层类型）。⇒ 新建玩法层静态持有者 `AirdropReleaseState.WaitRelease`；`AirdropController` 保留**同名转发属性** ⇒ 管理器/表现层调用点零改动。
3. **`Effect` 4 个类型搬进 `06Gameplay/Common/`**（谁用谁拥有）：`LimitedLife`(deps=`00_Utils`/`00_Core`)、`ChargeView`(仅 UnityEngine)、`MedivacController`(+嵌套 `MedivacState`)、`ModifyTerrain`(`00_Core`/`08_Map`/`00_Attribute`)。
4. **搬进来暴露出 2 个文件的上行依赖，已契约化**：`MedivacController` 的 `TaskManager.Instance.nowTask`(= `IsValid`/`Countdown`/`EnterTransition`) 与 `BattleManager.Instance.HaveBooster`、`ModifyTerrain` 的 `if (BattleManager.Instance)` ⇒ `ITaskService` **+3 窄投影**（`HasTask`/`Countdown{get;set;}`/`EnterTransition`）+ `ServiceLocator.Battle.HaveBooster`/`IsStartBattle`（ⓘ `NullServices` 同步补齐 ✓，脚本 `p53_medivac.py`）。
5. **⚠⚠ 见 §5 新铁律：搬 Unity 资产必须用 `AssetDatabase.MoveAsset`**——本批又踩了一次（Unity 给新路径**重新分配 guid**，旧 guid 仍被 prefab/数据资产引用 ⇒ Missing Script）。用 `.codebuddy/plans/p53_guid_repair.py` 审计出**全系列 14 个文件 guid 变化、7 个真损坏**（`MissionData_SO` 26 处、`WeaponUpgradeData_SO` 68 处、`InputManager` 被 prefab+场景引用…）并**全部修好**（旧 guid 写回新 `.meta`，刷新后往返 OK ✓）。

**➡ 下一步（P5-3 收尾）**：176 个文件搬入 `06Gameplay/`（按子系统建子目录）→ 挂 `06_Gameplay.asmdef`（references 见上）→ 编译修错。⚠ 搬运**必须**用 `AssetDatabase.MoveAsset`，或搬完**立即**跑 guid 审计。

#### P5-4 记录（2026-10-01）：先做前置，`10_Effect` 本身要等 Phase 6
**工具**：`.codebuddy/plans/p54_ext.py`（Effect 版外部阻塞扫描）与 `p6_ext.py`（`01Manager` 版）。⚠ 脚本里的候选前缀**不要带结尾斜杠**（`"Effect"` ✓ / `"Effect/"` ✗ 匹配不到）；单元名按 asmdef **文件名**取（所以 `08_Map` 的名字是 `08_Map`，与 `references` 里要写的 `FpsGame.MapUtils` 不是一回事 ⇒ 读数时注意别把合法的 `ASM:08_Map` 当阻塞）。

**实测结论：`Effect` 的外部引用 = `01Manager/*` 34 处（全部合法向上引用）** ⇒ `10_Effect` 的唯一阻塞是"管理器未成集" ⇒ **P5-4 的正确顺序 = 先 Phase 6 的 `09_Managers`**。

**`01Manager` 的阻塞扫描结果（`09_Managers` 的前置，共 28 处，去噪后 19 处）**：
| 组 | 处数 | 性质 | 处理 |
|---|---|---|---|
| `04UI` 窗口类型 | **17**（`WndManager` 10 个字段 + `OperationWnd/TipWnd/NoticeWnd/CountDownWnd` + `TipWndInfo`/`NoticeData`/`Window` + `BridgeSys.armament: ArmamentWnd`） | 真阻塞（管理器 → UI） | **Phase 6 的窗口注册表**（见 §4-E：`WindowRegistry` + 删字段 + `CreatTip` 一族上移/委托化 + `BridgeSys` 改事件）。⚠ "窗口未激活不跑 `Awake`"的解法：UI 层放 bootstrap 扫 `GetComponentsInChildren<Window>(true)` |
| `Effect` 反向 | 真 1（`BattleManager:454` 用 `VFXAirdropEffect` 池） | 真阻塞（管理器 → 表现层） | 池契约 `IVfxPoolItem`（§4-E） |
| `00Tools/SingletonNet` | 2 | 真阻塞（插件根无 asmdef） | ✅ **已解决**：`MoveAsset` 搬进 `00Tools/Test/`（`00_Utils`，只依赖 `Utils`） |
| `04UI/CountDownTypeEnum` | 2 | 真阻塞（枚举住在 UI） | ✅ **已解决**：下沉 `00Core/CountDownTypeEnum.cs`（`00_Core`，**全局命名空间不变 ⇒ 零调用点改动**，同 `DifficultyEnum`/`BoosterType` 手法） |
| `ASM:08_Map`（`MapRoot`/`GenerateNoiseTerrain`） | 7 | **误报**（`08_Map` 是下层 ⇒ 合法） | 无需处理 |

⚠ **噪声识别（本次再验证）**：`Constants.HealBag`（6 处）、`WeaponTypeEnum.FlareGun`（1）、`PropertyManager.CreatOOPart()`（**方法名**与 `Effect/CreatOOPart` 类同名，1 处）**全是同名成员噪声**，不是类型引用。

**顺手修复**：guid 审计又抓出 1 处**历史遗留**损坏 —— `PhoenixEagleController.cs`（更早从 `Assets/Art/Modle/PhoenixEagle/` 搬到 `02Game/AI/Controller/` 时 guid 变了）被 3 个资产引用 ⇒ 已用 `p53_guid_repair.py --apply` 修回 ✓。

**验收**：`failed=False`、Console 0 error；反射确认 `CountDownTypeEnum -> 00_Core` ✓、`SingletonNet -> 00_Utils` ✓。

#### P5-0 执行记录（2026-09-30，✅ 编译 0 error）

**目标**：`00Tools` 根（`FpsHelper*`6/`LogicBehaviour`/`SingletonNet`/`TechnicalDebt`）对 manager 的 **10 处引用**清零 —— 因为 manager 在玩法层之上，这部分无论 P5 怎么做都必须消。

| # | 原引用 | 处理 |
|---|---|---|
| 1 | `FpsHelper.cs:42-44` `GameRoot.GameState == Game/Ready/Bridge` | → `ServiceLocator.Flow.IsMainStage`（复用 P4 建的 `IFlowService`） |
| 2 | `FpsHelper_Extension.cs:53-54` `TaskManager.Instance.nowTask.ExtraDifficulty[0]/difficulty` | → `ServiceLocator.Task`（复用 P3 的 `ITaskService` 两个成员） |
| 3 | `FpsHelper_Hit.cs:80,196,289,350,357` `BattleManager.Instance.IsValid()/HaveBooster/FindUnits` | → `ServiceLocator.Battle`；**给 `IBattleService` 加 `FindUnits(IPERange, TargetCfg, Func<I_Actor,bool>)` 两个重载**（签名完全可契约化：`IPERange` 来自 `PEMaths.dll` 插件 ✓、`TargetCfg` 已在契约 ✓） |
| 4 | `FpsHelper_Hit.cs:278` `GameRoot.Instance.StartCoroutine(...)` | → **`IFlowService` 加 `Coroutine RunCoroutine(IEnumerator)`**，由 `GameRoot` 实现（下层拿不到 `GameRoot.Instance` 这个老坑，用服务一次性解决） |
| 5 | `FpsHelper_Hit.cs:311,317,330` `VFXManager.Creat(...)` ×3 | GameObject 两者 → `ServiceLocator.Vfx`；**`ProjectileBase` 重载 → `IVfxService` 加 `Component Creat(Component template, Vector3, Quaternion)`**（下层不能在契约里点名具体玩法类型，用 `Component` 表达） |
| 6 | `LogicBehaviour.cs:21-39` `GameRoot.Instance(.IsLocal)` / `NetManager.Instance?.Add/Remove(this)` | → **`IFlowService` 加 `bool IsLocal`**；**新增 `INetService`（`Add/Remove(I_Login)`，`NetManager` 的两个公开方法天然满足 ⇒ 零改动）+ `ServiceLocator.Net`** |
| 7 | `I_Login`（原声明在 `00Tools/LogicBehaviour.cs`） | **下沉到契约层**（新文件 `00GameContract/Interface_Login.cs`），否则 `INetService` 无法引用它 |

- **验收**：Console **0 error**；`01_GameContract.dll` mtime 更新；复跑 `p5_recon.py PREREQ` ⇒ `00Tools` 根阻塞引用 **10 → 3**，残留 3 处**全是** `ProjectileHitData 1`/`ProjectileBase 1`/`ProjectileMelee 1`（武器层，将随 `FpsHelper` 一起进 `06_Gameplay`，无需契约）；`00Tools/*.cs` 里 `GameRoot/TaskManager/NetManager/BattleManager/VFXManager` 关键词**已全部消失**。
- **附带收益**：`IFlowService` 现在同时解决了"下层查游戏状态 + 跑协程 + 查 IsLocal"三件事（`Actor` 与 `FpsHelper` 共用），`IVfxService` 覆盖了 VFX 池的两种形态。
- ⚠ **仍待处理（P5-1）**：`VFXManager.Creat(GameObject)` 在**其他玩法/AI 文件**里还有调用（`FxControllerBase:176`、`SympatheticDetonation:80` 等）—— 那些文件在 `06_Gameplay` 落地时必须改 `ServiceLocator.Vfx`；`Effect` 侧同理。

#### P5-1a 执行记录（2026-09-30，✅ 编译通过 + 反射验证）

**① 事件总线先搬走（= 把 P5-2 提前做掉，零调用点改动）**
`GlobalEventSub.cs`（14 事件）+ `BattleEventSub.cs`（15 事件）从 `01Manager/Global`、`01Manager/Battle` → 新建的 **`Assets/Scripts/06Gameplay/`**（未来 `06_Gameplay` 的落点）。依据仍是「**总线层 = min(发布者层, 订阅者层)**」：实测玩法层引 `GlobalEventSub 19` / `BattleEventSub 12`，**下层零使用**。⚠ 它们签名里带 `RoleData_SO`/`MissionBase`/`AirdropData`/`RuntimeSoundData`/`using static AirdropController` ⇒ 契约化要改一堆类型，**搬家是零改动**。

**② DTO 下沉（只搬"仅依赖 UnityEngine"的）** —— 用 Python 按**行区间精确抽取**（不手抄，保空白/编码）：
| 原位置 | 内容 | 结果 |
|---|---|---|
| `BattleManager.cs:559-621` | `WaveCreateParams`(struct) + **`WaveUtil`**(3 个扩展方法) | → `00GameContract/WaveCreateParams.cs`（**全局命名空间保持不变 ⇒ 零调用点改动**） |
| `TaskManager.cs:526-536` | `GameResult`(enum) | → 同上 |
- ⚠ **`WaveUtil` 必须跟着走**：否则玩法层用不了 `.Set(center)`/`.Scale(s)`——扩展方法的调用点**不出现类名**，是最容易漏的一类依赖（P4 的 `IsValid` 同款坑）。
- ⚠ 顺手修：`TaskManager.cs` 尾部因下沉残留的**孤立 XML 注释**（会报 CS1587 类告警）已清。

**③ 契约扩容（按「成员级实测面」开，不按类开）**
| 接口 | 新增成员 | 依据（玩法层实测调用次数） |
|---|---|---|
| `IBattleService` | `IsStartBattle`/`IsTeamWiped`/`ReinforcementCount`/`BattleRandom`、`EnqueueInit`、`CreatUnit`、`CreatWave`、`ReleaseAirdrop`×2、`Authorize`、`AddBattleDataItem`、`EndGame`、`SubmitOOPart`（+ P5-0 的 `FindUnits`×2、`HaveBooster`） | `FindUnits 12`、`Authorize 12`、`BattleRandom 8`、`CreatWave 7`、`AddBattleDataItem 5`、`ReleaseAirdrop 3`、`EnqueueInit 3`、`EndGame 2`、`CreatUnit 1`、`SubmitOOPart 1` |
| `IFlowService` | `CreateTimer`×2、`CreatePerTimer` | `GameState 14`、`CreateTimer 5`、`CreatePerTimer 4`（`StartCoroutine 1` 由 P5-0 的 `RunCoroutine` 覆盖） |
- ⚠ **必须显式实现**（字段 / 静态方法无法隐式实现接口）：`BattleManager` 的 `IsStartBattle`（**字段**）与 `EnqueueInit`（**静态**）；`GameRoot` 的 3 个定时器（**静态**在 `GameRootBase`）。
- **反射验收**：`01_GameContract` 内含 `WaveCreateParams`/`WaveUtil`/`GameResult` ✓；`IBattleService` = 12 方法 + 4 属性、`IFlowService` = 4 方法 + 3 属性、`INetService` 2、`IVfxService` 3、`I_Login` 4 ✓；`ServiceLocator` 槽位 = `Task, Battle, Flow, Vfx, Net` ✓。

**④ ⚠ 实测结论：这几类 DTO **下沉不到契约层**（必须落在 `06_Gameplay`）**
| DTO | 为什么进不了契约（tier 2） | 结论 |
|---|---|---|
| `SelectTaskData` + `TaskItem` + `TaskCfg`（都嵌在 `TaskManager.cs` 内） | 字段带 `MissionData_SO`/`MissionMainData_SO`（在 `02Game/Game/Data`＝**玩法层**）；`TaskCfg` 的属性甚至直接调 **`TaskManager.Instance.Missions`** | ⇒ 只能落在"两侧共见"的 **`06_Gameplay`** |
| `AirdropData`（嵌在 `AirdropController.cs:398`） | 字段带 `AirdropData_SO`（`02Data`＝tier 4，**高于**契约 tier 2） | 同上 |
- ⇒ **`ITaskService.nowTask`（玩法层 18 处）不能用契约暴露**（`nowTask` 的类型就是 `SelectTaskData`）。**建议走"数据自持"**（与 P3 的 `AboStateData_SO.Dic` 完全同构）：DTO 家族放进 `06_Gameplay` + 一个玩法侧持有者，由 `TaskManager`（上层）写入，玩法层直接读。
- **判据固化**：能进契约 = 只依赖 `00_Core`/`00_Attribute`/`01_GameContract` + UnityEngine + PEMaths；**一旦沾 `_SO` 或玩法类型，就只能落在更高层的"共见"程序集**。

**⑤ 仍待处理**：`IResService`(`CreatPrefab 4`/`LoadSprite`/`AsyncLoadScene`/`AsyncContinueLoadScene`)、`IWndService`(`OnWindowStateChange 6`/`CreatNotice 5`/`WindowState 3` + 6 个具体窗口字段)、`IArchiveService`(`Archive 5`/`GetSetting 5`)、`RoomManager`/`PathRequestManager`/`PropertyManager` 的契约与注册；以及**调用点批量替换**（`BattleManager.Instance.*`、`GameRoot.CreateTimer`、`VFXManager.Creat` … 共约 60 处）—— 后者是切 `06_Gameplay` 前的最后一道机械工作。

**⑥ P5-1b：再补 3 个服务契约 —— ✅ 已完成（2026-09-30，编译 + 反射验证）**
| 接口 | 成员 | 实现方 / 注册点 | 备注 |
|---|---|---|---|
| `IResService` | `CreatPrefab` / `LoadSprite` / `AsyncLoadScene` / `AsyncContinueLoadScene` | `ResSvc` @ `Init()` | 4 个成员全部只依赖 UnityEngine/Basic ✓ |
| `IArchiveService` | `GetSetting(string) → float` | `ArchiveSvc` @ `Init()` | ⚠ **只开了 `GetSetting`**：`ArchiveSvc.Archive` 返回 `ArchivesData_SO`（玩法层 SO）⇒ 契约层无法命名它 ⇒ 那部分走「数据自持」 |
| `IWindowService` | `WindowState` / `OnWindowStateChange`(event) / `CreatNotice(role,type,Func<bool>,vaildTime)` | `WndManager` @ `Awake()` | `WindowStateEnum` 恰好在 `00Core/ResCfg.cs`（**低于契约层** ✓）；6 个具体窗口字段（`airdropConfigWnd`/`guideWnd`/…）留给 Phase 6 的 `WindowRegistry` |
- **`ServiceLocator` 现 8 槽**（反射实测）：`Task, Battle, Flow, Vfx, Net, Res, Archive, Wnd`。
- **反射验证**：3 个接口的成员数 + **5 个实现绑定**（`ResSvc`/`ArchiveSvc`/`WndManager`/`BattleManager`/`GameRoot`）全部 ✓。
- ⚠ 踩点：`ResSvc`/`ArchiveSvc` 都在 `Init()` 注册，而 **`WndManager` 没有 `Init()`**（它是 `Singleton<T>` + `Awake` 覆盖）⇒ 注册放 `Awake()` 里 `base.Awake()` 之后。
- ⚠ **静态成员无法隐式实现接口**又出现一次：`WndManager.WindowState`（静态属性）与 `OnWindowStateChange`（**静态事件**）⇒ 事件用 `add/remove` 转发到静态事件（接口里的 event 是实例成员）。
- **仍待处理**：`RoomManager`(2)/`PathRequestManager`(1)/`PropertyManager`(1) 三个小契约；`Archive`/`nowTask` 两处「数据自持」；以及 **146 处调用点替换**（`BattleManager.Instance 66` · `VFXManager 24` · `TaskManager.Instance 16` · `ArchiveSvc 11` · `GameRoot.Create* 9` · `WndManager.Instance 8` · `ResSvc.Instance 7`）—— 这是切 `06_Gameplay` 前的最后一道机械工作。

**⑦ P5-1c：最后 2 个小契约 + `Archive` 数据自持 —— ✅ 已完成（2026-09-30，编译 + 反射验证）**
| 项 | 结论 |
|---|---|
| `IRoomService` | 只开 **2 个整数投影**（`MasterIndex`/`PlayerCount`）—— `RoomManager.Master`/`players` 的类型是 `PlayerData`（上层）⇒ 契约层无法命名它（**接口隔离在本项目的第 2 例**，第 1 例是 `IUnitScale`）；`RoomManager` 在 `Init()` 注册，`MasterIndex` 加了"无房主返回 0"保护 |
| `IPathService` | `RequestPath(NavMeshAgent agent, Vector3 destination, bool log = false)`；⚠ `PathRequestManager` **没有 `Init()`** ⇒ 新加 `public override void Awake()` 注册（继 `WndManager` 之后第 2 个"无 Init 的管理器"，说明**注册钩子要逐个确认，别假定有 `Init()`**） |
| `PropertyManager` | **不需要契约** ✓ —— 全仓只有 `Effect/CreatOOPart.cs` 用它，而 `Effect` 在目标分层里位于 `09_Managers` **之上** ⇒ 可以直接引管理器。**判据：先看调用方在层级的哪一侧，再决定要不要契约** |
| `Archive` 数据自持 | `ArchivesData_SO` 加 `static Current { get; set; }`，由 `ArchiveSvc.Init()` 写入；玩法层 5 处 `ArchiveSvc.Archive.*` → `ArchivesData_SO.Current.*`（**与 `AboStateData_SO.Dic` 同一套路**；比另建 holder 类更省，因为"数据自己持引用"不新增文件） |
- `ServiceLocator` 现 **10 槽**：`Task, Battle, Flow, Vfx, Net, Res, Archive, Wnd, Room, Path`（反射实测）；Console 0 error。
- **剩余**：`nowTask` 数据自持（**18 处**，`TaskManager.nowTask` 的类型 `SelectTaskData` 在玩法层 ⇒ 同 `Archive` 的解法）+ **约 140 处调用点替换**（`BattleManager.Instance 66` 是大头）。

**⑧ P5-1d：批量替换（玩法侧 manager 直连 → `ServiceLocator`）—— ✅ 主体完成（2026-09-30，编译 0 error）**
- **前置：`00GameContract/NullServices.cs`（空对象兜底）**：给 10 个槽各配一个"中性值"实现，`public static IBattleService Battle { get; set; } = NullServices.Battle;`。
  ⇒ 调用点可放心写 `ServiceLocator.X.Y(...)`（**不必到处 `?.`、返回值也不会变 nullable**），**批量替换才能机械化**；语义与契约里"服务未就绪取中性值"的约定完全一致，且以后新增调用点天然带兜底。
- **替换脚本**：`.codebuddy/plans/p5_sweep.py` —— 只扫**玩法侧**目录（`02Game`/`00Tools`/`06Gameplay`），**跳过 `Effect`/`04UI`**（它们在目标分层里位于 `09_Managers` **之上**，直连管理器是允许的）；跳过 `//`/`///`/`*` 注释行。**结果：112 行 / 48 个文件**。
- **替换后暴露 8 条错误 → 5 类修法**（都是"契约表达力"问题，值得记）：
  | 症状 | 根因 | 修法 |
  |---|---|---|
  | `IBattleService` 未包含 `MissionCont`/`WaveCont` | 它们是 `BattleManager` 的**子管理器字段** | 开窄投影 `int WaveCount { get; }`；`MissionCont` 则**把行为搬进 `MissionController.RevealAll()`**，契约只暴露 `void RevealAllMissions()` |
  | `Flow.GameState = X` 只读 | 接口属性无 setter | 加 `void SetGameState(GameStateEnum)` |
  | `Creat(ProjectileBase)` 参数不匹配、返回 `Component` 后 `.Shoot` 找不到 | 用 `Component` 表达组件模板会**丢类型** | **改泛型**：`T Creat<T>(T template, Vector3, Quaternion) where T : Component` ⇒ 调用方直接拿回 `ProjectileBase`，零强转 |
  | `Vfx.Release(ProjectileBase)` 转不了 `GameObject` | 池不同（抛射物池 vs 普通池），**不能改传 `gameObject`**（会进错池） | 加 `void Release(Component)`，实现里按类型分流 |
- **效果：玩法侧 manager 直连 146 → 17**（`VFXManager`/`GameRoot`/`ResSvc`/`RoomManager` 已归 **0**）。
- **剩余 17 的构成**：`TaskManager.Instance`(5) + `nowTask`(4) ⇒ 待做「数据自持」；6 处**裸 `Instance` 当 bool / 当类型用**需改写语义（`MissionView:99`、`MissionBase.manager` 应改 `IBattleService`）；`WndManager` 3 处**具体窗口字段**（`airdropConfigWnd` 等）⇒ §4-E 的 `WindowRegistry`（Phase 6）；1 处注释噪声。

**⑨ P5-1e：`TaskManager` DTO 解嵌套下沉 + `nowTask` 数据自持 —— ✅ 完成（2026-09-30，编译 0 error）**
- **为什么必须做**：`MissionBase`（核心玩法类）的公开 API 用了 `TaskManager.TaskItem`/`TaskManager.SelectTaskData`，而这三个 DTO 是 **`TaskManager` 的嵌套类型**（写在 `01Manager/Global/TaskManager.cs` 里）⇒ 不搬出来，核心玩法类就进不了 `06_Gameplay`。
- **做法**（Python 按行区间精抽）：`TaskManager.cs:392-511`（`SelectTaskData` + `TaskCfg` + `TaskItem`）→ **`06Gameplay/TaskData.cs`**（文件作用域 ⇒ **即"解嵌套"**；`TaskManager.cs` 516 → **396** 行）；全工程 `TaskManager.X` → 裸名（**实际只有 2 个文件有真实引用，其余 17 处在注释里**）；删掉 `using TaskItem = TaskManager.TaskItem;` 这类别名。
- **`TaskCfg` 的向上依赖**：原读 `TaskManager.Instance.Missions` ⇒ 改读 **`MissionData_SO.Catalog`**（**数据自持**：`TaskManager` 加载配置时写入；与 `ArchivesData_SO.Current` 同套路，不新增文件）。
- **`ITaskService` 按实测扩容**：`CollectProperty`（= `nowTask.collectProperty`；`Furniture_KeiSubmit`×2 / `OOPart`×1）、`EnemyVarietyType`（`MissionBase` 选预制体用）。
- **`MissionBase.manager` 从 `BattleManager` 改 `IBattleService`** —— 这一改**立刻暴露**一批「Mission 子类调了不在契约上的 `manager.X`」⇒ 补 `CreatPatrol`；`MissionEradicate` 的 `task.difficulty` → `task.Difficulty`。
- **成果：玩法侧 manager/DTO 直连 146 → 1**。唯一真实剩余 = `Furniture_General` 的 **7 处具体窗口字段**（`selectRoleWnd.SetWndState(true)` 等）⇒ 属 §4-E `WindowRegistry`（Phase 6）；另 3 处是注释/字符串噪声。
- `06Gameplay/` 现有 3 个文件（`GlobalEventSub` / `BattleEventSub` / `TaskData`）= 未来 `06_Gameplay` 的落点。
- ⚠ 过程中新增的契约成员（`CreatPatrol`/`WaveCount`/`RevealAllMissions`/`SetGameState`/`CollectProperty`/`EnemyVarietyType`/泛型 `Creat<T>`/`Release(Component)`）都已同步进 `NullServices` 的空对象实现 —— **凡是往契约加成员，就同步加空对象实现**，否则批量替换后的兜底会漏。

#### ⚠ P5 期间并行会话覆盖事故（2026-09-30，必读）
- **现象**：另一窗口把 `00GameContract/` 重构成"一接口一文件"并**删除 `Interface_Manager.cs`**，只搬走它认识的 5 个接口 ⇒ 我写在里面的 **4 个接口（`INetService`/`IResService`/`IArchiveService`/`IWindowService`）连同 `IBattleService`/`IFlowService`/`IVfxService` 的扩容成员一起消失**，`ServiceLocator.cs` 报 4 条 `CS0246`。
- **恢复**：按新布局重建 7 个文件（验收：10 个接口成员数 + `ServiceLocator` 10 槽，全部反射确认）。
- **附带**：对方还给 `00Core/SKVP.cs`、`00Core/Constants.cs` 加了 `namespace Core` 而消费方漏 `using Core;` ⇒ `04_Data`/`08_Map` 编译失败（连带卡住 `Assembly-CSharp`）。补了 2 处 `using Core;` + 用 `p5_fix_usings.py` 扫了 10 个文件的缺失 `using`。
- **两条铁律（已进 MEMORY）**：
  1. **新增独立文件 ≫ 修改共享文件**：我新建的 `Interface_Login.cs` 安然无恙，而写在 `Interface_Manager.cs` 里的 4 个接口被整文件删除带走。
  2. **每轮动手前先看热区文件的改动时间**（`Get-ChildItem -Sort LastWriteTime`），能立刻判断"这是谁在动、是不是中间态"；对 `00GameContract`/`00Core` 这类双方热区，**同一时间只应有一个会话在改**。

- 原先写的"`Mission → Interactable` 必须反转（23+13 次）"**作废**（合并后同集，无需反转）；`BagBase` 的 `IVehicleUIController` 仍按 §4-C 走契约。

### Phase 6 · Managers / UI / Effect 收口 —— ✅ **已完成（2026-10-01，编译 0 error）**
`09_Managers`(28) → `10_UI`(56) → `10_Effect`(23) 全部切出；`WndManager` 的 10 个窗口字段已删、改进 `WindowRegistry` + UI 侧 `WndHub`；`BridgeSys.armament` 改 `IBridgeArmamentSink`；`BattleManager` 的 `VFXAirdropEffect` 池改契约 `IAirdropEffect`。⚠ 关键坑见 §4-E 与 §5.1："Unity 不会为 `SetActive(false)` 的对象调 `Awake`" ⇒ 自注册必须配 `WndHub.Scan()` 兜底 + `[RuntimeInitializeOnLoadMethod]` 启动钩子。

### Phase 7 · 编辑器程序集 —— ✅ **已完成（2026-10-01，编译 0 error）**
> `Assets/Editor/ProjectEditors.asmdef`(41 cs) + `Assets/Shader/Editor/ShaderEditors.asmdef`(3 cs) 已建 ⇒ **`Assembly-CSharp-Editor` 归零**（该程序集已不存在）；内层 `Assets/Editor/Tool/EditorTools.asmdef` 保持独立（`references:[]`，后来补了 `["00_Utils"]`）。
> ⚠ **以下为当年（2026-09-30）的阻塞分析，作为历史保留** —— 它说明了"为什么 P7 必须排在最后"。

**（历史）阻塞原因**：asmdef 程序集**不能引用 `Assembly-CSharp`**。而 `Assets/Editor/` 下**每一个**脚本都引用了还留在 `Assembly-CSharp` 的运行时类型：

| 编辑器脚本 | 引用 Assembly-CSharp 类型的次数 |
|---|---|
| `WeaponUpgradeEditorWindow.cs` | 40 |
| `EnemyStatEditorWindow.cs` | 27 |
| `LimbEditorWindow.cs` | 18 |
| `Drawer/ArchiverDataHandle.cs` · `Drawer/ModifyAttrDataDrawer.cs` | 12 · 12 |
| `DataTabs/MissionTabModule.cs` | 11 |
| `ActorHalfHeightTool.cs` · `MiniProfiler.cs` · `WeaponUpgradeTabModule.cs` … | 8 · 7 · 7 |
| `Drawer/*`、`DataTabs/*` 其余 | 5~6 各 |
| （最少的几个，如 `DataEditorWindow.cs`/`MaterialUsageFinder.cs`） | 1（多为注释） |

⇒ 现在给 `Assets/Editor/Drawer/`、`DataTabs/` 建 asmdef，它们会立刻看不到 `DamageData`/`ModifyAttrData`/`RoleData_SO`/`WeaponPlayerController`/`Actor` 等类型 = 编译爆炸。**这也正是 P7 被排在最后的原因**：它依赖 P3~P6 把运行时类型移出 `Assembly-CSharp`。

**现在唯一可做且已做完的事**：`Assets/Editor/Tool/EditorTools.asmdef`（`references: []`）保持"只放纯工具"；⚠ 该目录下脚本看不到 UGUI/项目类型，需要它们的工具**别放这里**。

**P7 的启动条件**：P6 结束后，`Assets/Editor/` 按下列 3 个程序集切分，并逐个补 `references`（届时编译器会直接告诉你缺哪个）：
| 目标程序集 | 覆盖 | 预期 references |
|---|---|---|
| `EditorTools`（已有） | `Editor/Tool/`（6 个纯工具） | 保持 `[]` 或按需补 |
| `Editor.Drawer` | `Drawer/`（14 个 Drawer/Override）+ `SOPickerPopup.cs` | `01_GameContract` + `02_Utils` + `04_Data` + `05_UnitCore` + `06_Gameplay`（+TMP，若用到） |
| `Editor.DataEditor` | `DataEditorWindow.cs` + `DataTabs/`（13 个 TabModule） | `04_Data` + `Editor.Drawer` + 相关数据程序集 |
| （其余散落窗口：`WeaponUpgradeEditorWindow`/`EnemyStatEditorWindow`/`LimbEditorWindow`/`ActorHalfHeightTool`/`PrefabReplacerEditor`/`MiniProfiler`…） | 留在 `Assembly-CSharp-Editor` 或单独建 `Editor.Windows` | 依赖最多，建议最后动 |

---

## 6. 验收与工具

1. **重跑依赖矩阵**：`powershell -NoProfile -ExecutionPolicy Bypass -File .codebuddy/plans/AsmDepScan.ps1`
   关注：目标边是否归零（如 Phase 3 后不应再有 `02Game/* -> 01Manager/*`）。
2. **判定编译**：看**目标**程序集 dll 的 mtime（`Library/ScriptAssemblies/<Name>.dll` > 源码 mtime）。⚠ 别只看 `Assembly-CSharp.dll`（`00Tools/Test/*` 属 `00_Utils.dll`，历史踩过两次）。
3. **Console**：`read_console(types=[error])` 取 0 条；新建 `.cs` 后必须 `refresh_unity(mode=force, scope=assets, compile=request)`（默认 `if_dirty` 不导入新文件）。
4. **行为回归**：每条 Phase 结束跑一次 Play 冒烟清单：巡逻队会走（寻路）、开火有噪声/警惕、任务进度推进、家具长按/瞬时交互、护盾重塑、撤离联动。
5. ⚠⚠ **改 asmdef 后 Unity 可能"假刷新"**（2026-09-30 亲历）：`refresh_unity(mode=force, scope=assets, compile=request)` 返回 `refresh_triggered: true / resulting_state: idle`，但 **dll 时间戳没变、编译根本没发生**（编辑器 `is_focused: false` 时尤甚）。可靠姿势（按序）：
   ```csharp
   // execute_code（codedom 即可）
   AssetDatabase.ImportAsset("Assets/Scripts/XX/xx.asmdef", ImportAssetOptions.ForceUpdate);
   AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
   UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
   ```
   然后**用三件事交叉验证**（不要只看 refresh 的返回值）：
   - `Library/ScriptAssemblies/<Name>.dll` 的 mtime 是否 > 源码/asmdef mtime（新程序集首次要等出 dll）；
   - `read_console(types=["error"])` 是否 0 条（⚠ 上一次失败编译的错误会**一直挂着**，直到下一次成功编译才被清掉，容易误判）；
   - `CompilationPipeline.GetAssemblies()` 里该程序的 `sourceFiles.Length` 与 `assemblyReferences`（**这是验证 asmdef references 是否按预期解析的唯一权威手段**，比读 JSON 靠谱）。

---

## 7. 反模式禁令（本项目特有，拆分过程中必须守）

1. **不在 `01_GameContract` 放 MonoBehaviour/UI/UnityEditor**（纯接口 + `[Serializable]` 数据 + 枚举）。
2. **不让任何 `_SO` 引用 Manager**（SO 是数据，不是服务消费者）。
3. **不在低层新建 Manager 单例直连**；低层要服务只走 `ServiceLocator` 的契约接口。
4. **不为了拆而拆**：`00Tools` 根 + `01Manager` + `02Game` 若某两个目录互引超过 50 次且语义同源（如 `Rendering`+`Feature/Snow`），**合并成一个程序集**比互相抽接口更划算。
5. **`autoReferenced` 分阶段**：过渡期（`Assembly-CSharp` 还在）新 asmdef **必须 `true`**，否则 `Assembly-CSharp` 里的 330 个 cs 看不到它的类型（实例：`SnowController` 被 `FpsHelper`/`BattleManager`/`WeatherEffect*`/`BridgeRoleManager` 调用）；等 Phase 7 把 `Assembly-CSharp` 拆空后，再统一切 `false` + 显式 `references`（终态目标）。
6. 跨层只传 `GameObject`/基础类型/契约接口（asmdef 内引用不到 `Assembly-CSharp`，`08_Map` 用不了 `MapData_SO` 就是这个坑）。
6b. ⚠ **新程序集的依赖必须"已经在 asmdef 里"**（asmdef ← `Assembly-CSharp` 是彻头彻尾的单向墙，见 §5 铁律②）。动 asmdef 前**先扫**：① 按"已声明类型名全集"扫；② 扫**扩展方法**（`static X M(this ...)`）与 **`internal` 成员**；③ 扫**包的命名空间**（`Unity.AI.Navigation`/TMP/URP 是**程序集**，不是 `UnityEngine` 自带）。只做 ① 必漏（P4 实测：6 项扫查全过，仍被第 ① 类之外的 `IsValid` 扩展卡住）。
7. 每阶段收尾：`git status` 干净 + 单次提交只做一个阶段的一件事，便于二分回滚。

---

## 8. 行业视角复审（2026-09-30）：哪些必须拆、哪些没必要拆

### 8.1 判据：asmdef 的收益只有 4 条，其余都是成本
**收益**：① **增量编译时间**（大项目拆 asmdef 的首要动机）② **编译期强制依赖方向**（越界即失败，架构防腐）③ **可独立分发/复用**（包、插件、跨项目）④ **编辑器/运行时隔离**（硬需求）。
**成本**：每个程序集都是独立编译单元 + `references` 靠 GUID 人工维护（漏引用只有编译时才暴露 —— 本会话亲历 `CS1061`/`CS0246`）；跨程序集**失去 `internal`**；拆得越碎，**为过编译而写的接口/事件样板**越多。程序集数量还会**拉长全量重编与打包**（程序集图更宽）。

**经验区间**：2000 脚本以内的 Unity 游戏项目，**8~15 个运行时程序集**是常态，且绝大多数按「**层次 / 生命周期**」拆（Core / Contracts / Utils / Data / Gameplay / UI / Editor / ThirdParty），**极少按功能目录一一对应拆**（AI、Player、Weapon、Mission 各一个程序集只在超大项目 + 多团队分工时才划算）。**asmdef 数量 ≠ 架构质量**。

### 8.2 必须拆（收益明确或有硬约束）
| 项 | 理由 | 状态 |
|---|---|---|
| `00_Attribute` / `00_Core` / `01_GameContract` / `02_Utils` | 全员依赖的底座；契约层是**唯一能承载跨层类型**的地方（不拆就永远解不了环） | ✅ |
| `Editor` 系列 | **硬需求**（编辑器 API 不能进玩家包） | ⬜ P7 |
| 第三方：`NavMeshComponents` / `MackySoft` / `KCPNet.dll` | **硬需求**（升级/替换时隔离面） | ✅ |
| **`03_EventBus`** | **唯一主环的解环关键**（`01Manager ↔ 02Game` 的回边一半来自 `BattleEventSub`/`GlobalEventSub` 的签名） | ⬜ P3 |
| `04_Data` | 数据被上中下三层都要用，放中层必然成环 | ✅ |
| `02_Rendering` | 独立渲染特性（URP RendererFeature + 雪/云/描边），依赖极少、体量大（单个 `SnowRendererFeature.cs` 32KB） | ✅ |
| `08_Map` | 地形生成（46KB 工具 + 2200 行生成器），独立且被玩法依赖 | ✅ |
| `02_Net` | 独立网络模块（对外零引用） | ✅ 2026-09-30 |
| **`05_UnitCore`** | `Actor`/`Health`/`Damageable`/`UnitQueryGrid` 是**被所有玩法依赖、自身不依赖玩法**的"单位内核" | ⬜ P4 |
| `03_Audio` | `AudioSvc` 被 UI 侧的 `WndManager` 依赖 ⇒ 必须在 `01Manager` 之下，否则"同层服务互相引用" | ⬜ P3 |

### 8.3 **没必要拆**（原计划的过度设计，已修正）
| 原计划 | 为什么不拆 | 修正 |
|---|---|---|
| P5 把玩法拆成 **`06_Weapon`/`06_AI`/`07_Player`/`07_Mission`/`08_Interactable` 5 个程序集** | 它们之间**双向密集互引**（实测 `Player→Weapon 62`、`Mission→Interactable 38`、`AI→Weapon 32`、`Interactable→Player 28`、`Player→Interactable 27`…）⇒ 拆完要为每一对写接口/事件，**纯为过编译而设计** | **合并为 1~2 个**：`06_Gameplay`（AI+Player+Weapon+Mission+Interactable+Npc+Gameplay+`Game/Data`）；层内改用**命名空间 + 文件夹**分层 |
| `00_WndTools` + `04_UI` 两个 UI 程序集 | 同层同性质、单向依赖、都无业务依赖（5+3 cs） | **合并 `05_UI.Framework`** |
| `05_EffectComp` 与 `Effect/` 根+`VFX` | "半拆"状态：同属表现层，一半有 asmdef 一半在 Assembly-CSharp | **合并 `10_Effect`** |
| `02_Rendering` 与 `DayNightSystem` | Rendering 正向依赖 `WeatherAtmosphereController`，二者同属"环境渲染" | **可合并 `06_Rendering`**（想保留 DayNight 独立特性则维持现状） |
| `02Data/Variant/` 单独建集 | 已是同集子目录 | 保持不拆 |
| `02Game/Game/Data/`（6 个带逻辑引用的 SO）单独建集 | 引用游戏层类型，独立建集只会制造无谓引用 | **随 `06_Gameplay`** |
| `Plugins/DynamicBone`（4 cs 第三方小工具） | 零收益 | 不拆 |
| 为 `FpsHelper`/`VFXManager` 等**单文件**制造接口 | 收益 < 成本 | 按**内容**归位（结算进 `05_UnitCore`、纯工具进 `02_Utils`），不强行拆集 |
| `00Tools/Test` 改名 | 纯目录名 | 可选，无收益 |

### 8.4 修正后的目标清单（≈13 个运行时 + 3 编辑器，原计划 ~20）
```
00_Attribute → 00_Core → 01_GameContract → 02_Utils → 02_Net
                                            ↓
03_EventBus（解环关键）  04_Data  03_Audio  04_Map(现 08Map)  05_UI.Framework(00_WndTools+04_UI 合并)
05_UnitCore（Actor/Health/Damageable/UnitQueryGrid/ActorsManager/ProjectileBase）
06_Gameplay（AI+Player+Weapon+Mission+Interactable+Npc+Gameplay+Game/Data）  ← 合并大块
06_Rendering（02_Rendering + DayNightSystem，可选合并）
10_UI（04UI 根+UI）   10_Effect（Effect 根+VFX+05_EffectComp 合并）
EditorTools / Editor.Drawer / Editor.DataEditor
```

### 8.5 ⚠ 由此引出的关键决策：`07_Managers` 与 `06_Gameplay` 要不要分家
**不分家的方案（工程化推荐）**：`01Manager` 与玩法同属一个程序集 ⇒ **P3 只需做「事件总线下沉 + `IResService`/`ITaskService`/`IWndService` 三个服务契约」**（约 1/3 工作量），就能拿到"契约/数据/单位内核/渲染/网络/编辑器独立 + UI、Effect 与玩法分开"的**全部实质收益**。
**分家的方案（完整分层）**：还要为 `BattleManager`(110)/`GameRoot`(74)/`WndManager`(68)/`TaskManager`(63)/`ResSvc`(48)/`ArchiveSvc`(41) 逐一建接口并改 300+ 调用点，换来"服务层与玩法层编译期隔离"这一条边界。

### 8.6 比"多拆 5 个程序集"更划算的替代手段
1. **架构守卫脚本**：把 `.codebuddy/plans/AsmDepScan.ps1` 当 CI / 提交前检查（`02Game/* -> 01Manager/*` 这类边超阈值就报警）。asmdef 管不到"同集内的跨命名空间越界"，脚本能。
2. **命名空间纪律**：`FPSGame.AI` / `FPSGame.Player` / `FPSGame.Weapon` / `FpsGame.Mission` 各守其位，配合上面的脚本，等价于"软 asmdef"，成本几乎为零。
3. **只在"依赖方向确定 + 被多方依赖"处切 asmdef**（底座 / 契约 / 数据 / 内核 / 表现 / 编辑器 / 第三方），**其余用文件夹 + 命名空间**。
