# 06Gameplay 命名空间对齐 · 计划（阶段 0 产出）

> 决策：**保留独立 ns**（`Weapon`/`Data` 各自独立）；跨层 `TargetData`/`IVehicleUIController` **改 ns 认领为 06**（不搬文件）。

> 基线：`sr_ns_audit.py` 资产 `[SerializeReference]` 条目 22、失配 **0**（改 ns 后须复跑保持 0）。


## 1. 目录 → 目标命名空间

| 目录 | 现状 ns | 目标 ns |
|---|---|---|
| AI, AI/Controller, AI/DetectionModule, AI/Fx, AI/Skill, AI/StateMachine | FPSGame.AI×44 | **FPSGame.AI** |
| AI/StateMachine/Editor | FPSGame.AI.Editor×1 | **FPSGame.AI.Editor** |
| Game | FPSGame.Game×11 | **FPSGame.Game** |
| Data | FPSGame.GameData×6 | **FPSGame.GameData** |
| Airdrop, Bag, Common, Common/FpsHelper, Effect, Events… | FPSGame.Gameplay×85 | **FPSGame.Gameplay** |
| Mission, Mission/Evacuate, Mission/Extra, Mission/Main, Mission/Sub | FPSGame.Mission×22 | **FPSGame.Mission** |
| Weapon | FPSGame.Weapon×21 | **FPSGame.Weapon** |

## 2. 需改 ns 的文件（共 0）


### 特例（跨层 2 个，阶段 2）

- `Common/TargetData.cs`  FPSGame.GameContract → **FPSGame.Gameplay**（`TargetData` 被 5 个文件引用）
    - Assets/Scripts/06Gameplay/AI/Controller/EnemyController.cs
    - Assets/Scripts/06Gameplay/AI/DetectionModule/DetectionModule.cs
    - Assets/Scripts/06Gameplay/AI/StateMachine/EnemyMobile.cs
    - Assets/Scripts/06Gameplay/Common/TargetData.cs
    - Assets/Scripts/06Gameplay/Npc/SpecUnitController.cs
- `Common/IVehicleUIController.cs`  FPSGame.GameContract → **FPSGame.Gameplay**（`IVehicleUIController` 被 5 个文件引用）
    - Assets/Scripts/06Gameplay/Bag/BagBase.cs
    - Assets/Scripts/06Gameplay/Bag/WeaponBag.cs
    - Assets/Scripts/06Gameplay/Common/IVehicleUIController.cs
    - Assets/Scripts/06Gameplay/Player/VehicleWeaponsManager.cs
    - Assets/Scripts/10UI/UI/VehicleUI.cs

## 3. 受影响的外部 using（其它程序集）

- `using FPSGame.Game;` 出现在 **57** 个文件
    - Assets/Scripts/09Manager/Battle/AirdropController.cs
    - Assets/Scripts/09Manager/Battle/BattleManager.cs
    - Assets/Scripts/09Manager/Battle/BattleRoleManager.cs
    - Assets/Scripts/09Manager/Battle/PatrolContriller.cs
    - Assets/Scripts/09Manager/Battle/RobotWave.cs
    - Assets/Scripts/09Manager/Battle/VFXManager.cs
    - … 共 57
- `using FPSGame.Gameplay;` 出现在 **66** 个文件
    - Assets/Scripts/09Manager/Battle/AirdropController.cs
    - Assets/Scripts/09Manager/Battle/BattleManager.cs
    - Assets/Scripts/09Manager/Battle/BattleRoleManager.cs
    - Assets/Scripts/09Manager/Battle/MissionController.cs
    - Assets/Scripts/09Manager/Battle/PatrolContriller.cs
    - Assets/Scripts/09Manager/Battle/RobotWave.cs
    - … 共 66
- `using FPSGame.Furn;` 出现在 **0** 个文件
- `using FPSGame.GameContract;` 出现在 **66** 个文件
    - Assets/Scripts/04Data/AirdropData_SO.cs
    - Assets/Scripts/04Data/BattleState.cs
    - Assets/Scripts/04Data/Booster_SO.cs
    - Assets/Scripts/04Data/CampData_SO.cs
    - Assets/Scripts/04Data/MapData_SO.cs
    - Assets/Scripts/04Data/TaskState.cs
    - … 共 66
- `using FPSGame.AI;` 出现在 **7** 个文件
    - Assets/Scripts/09Manager/Battle/PatrolContriller.cs
    - Assets/Scripts/09Manager/Battle/RobotWave.cs
    - Assets/Scripts/09Manager/Battle/WaveManager.cs
    - Assets/Scripts/09Manager/Battle/ZergWave.cs
    - Assets/Scripts/10UI/UI/HpItemBoss.cs
    - Assets/Scripts/10_Effect/CreateBuilding.cs
    - … 共 7
- `using FPSGame.Mission;` 出现在 **6** 个文件
    - Assets/Scripts/09Manager/Battle/MissionController.cs
    - Assets/Scripts/09Manager/Battle/PatrolContriller.cs
    - Assets/Scripts/10UI/UI/MissionHUDItem.cs
    - Assets/Scripts/10UI/Wnd/MiniMapWnd.cs
    - Assets/Scripts/10UI/Wnd/MissionCompleteWnd.cs
    - Assets/Scripts/10UI/Wnd/MissionWnd.cs

## 4. 执行顺序（阶段 3，逐目录推进）

1. `Interactable/`：`FPSGame.Furn`(8) → 统一 `FPSGame.Gameplay`（外部 `using FPSGame.Furn;` 0 文件）
2. `Data/`：`FPSGame.Game`(6) → `FPSGame.GameData`
3. 零散串味对齐（`AI/Skill/UnitAttributeFactory`、`Game/MissionView`、`Npc/SpecUnitController`、`Player/PlayerMountPoint`、`Common/{IDamageData,SpeechTypeEnum}`，详见 §2 明细）
4. `Weapon/`：21 文件 → `FPSGame.Weapon`（外部 `using FPSGame.Game;` 57 文件 + 06 内部若干，**最大刀，单独一轮**）

## 5. 每步校验

- `python -X utf8 .codebuddy/plans/sr_ns_audit.py` → 失配保持 **0**（否则 `sr_ns_repair.py --apply`）
- `python -X utf8 .codebuddy/plans/find_dangling_guids.py` → 悬空 GUID **0**
- 编译三证（触发前须经同意）：dll mtime 前进 + Console 0 error + `is_compiling:false`
