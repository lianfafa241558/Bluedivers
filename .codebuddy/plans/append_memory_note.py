# -*- coding: utf-8 -*-
"""向当日记忆文件追加一条记录（保原编码/换行/BOM 状态）。"""
import io, sys

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-01.md"
raw = open(p, "rb").read()
bom = raw[:3] == b"\xef\xbb\xbf"
txt = raw.decode("utf-8-sig")
nl = "\r\n" if "\r\n" in txt else "\n"

note = """
## 审计：`06Gameplay` 目录整理前置盘点（2026-10-01，只诊断 + 修 1 处构建隐患）

- **背景**：用户要求「整理 `Assets/Scripts/06Gameplay` 目录」。这是最后一个大玩法程序集（`06_Gameplay`，190 cs），搬运时是**整目录 `02Game/` 平移**进来的，所以内部还留着旧的预定义程序集编号与层级。
- **现状三层**：① 根散落 3 个事件契约（`BattleEventSub`/`GlobalEventSub`/`TaskData`）② `Common/` 17 cs（混装 4 个命名空间：`FPSGame.Gameplay` 12 / `FPSGame.Game` 2 / `FPSGame.GameContract` 2 / `FPSGame.Furn` 1）③ `02Game/` 169 cs（`03Player`16 / `05Interactable`22 / `06Npc`4 / `AI`45 / `Game`57 / `Gameplay`22 / `Util`3）+ `Airdrop/` 1。
- **审计结论（脚本 `.codebuddy/plans/audit_06gameplay.py` + `audit_06gameplay_types.py` + `audit_editor_folders.py`）**：
  - 文件系统干净：**孤儿 `.meta` = 0、缺 `.meta` = 0、空目录 = 0**。
  - 命名空间 **(global) 已清零**（190/190 全 `FPSGame.*`）；但**目录与命名空间错位**：`FPSGame.Game`(38) 跨 `02Game/Game/**` + `03Player` + `AI/Skill` + `Common`；`FPSGame.Gameplay`(74) 跨 `03Player`/`05Interactable`/`06Npc`/`Gameplay`/`Util`/`Airdrop`/`Common`/根。
  - **一文件多顶层类型 20 处**（规范只允许"1 主类 + 伴生内部类"）：最重的 `WeaponAttributeFactory.cs`(6 类型)、`WeaponUpgradeController.cs`(4)、`TaskData.cs`/`AIController.cs`/`WeaponController.cs`/`AirdropData.cs`/`InputManager.cs`(各 3)。
  - 结构异味：`06Gameplay/02Game/Gameplay/` **父子重名**；`02Game`/`03Player`/`05Interactable`/`06Npc` 是**旧程序集编号残留**（asmdef 内部应按规范用纯英文名词）；`03Player/MainController`、`AI/FxCont` 缩写不达意；`Game/Shared/Weapon`(17) 与 `AI/FxCont`(13) 是独立子系统却埋在 4~5 层深。
- **⚠⚠ 唯一硬伤（已修）**：`06Gameplay/02Game/AI/StateMachine/Editor/TurretDrawer.cs` **未守卫 `using UnityEditor`**。Unity 官方手册明确：**在 asmdef 覆盖的目录下，`Editor/` 文件夹失去 editor-only 特权**，脚本会被编进该 asmdef（`06_Gameplay` 的 `includePlatforms` 为空 = 全平台）⇒ **非 Editor 平台构建必报 CS0246**。这是 `02Game/` 整目录搬进 asmdef 时引入的**回归**（搬之前它属预定义程序集，Editor 文件夹规则仍生效）。
  - **全仓扫描结论**：6 个 `Editor/` 目录里 5 个都有 `includePlatforms:["Editor"]` 的独立 Editor 程序集，**只有这 1 个漏**。
  - **修法**：按项目既有惯例（`InputManager.cs`/`KeyScreen.cs`/`WeaponCfg.cs` 同款）**给整个文件首尾加 `#if UNITY_EDITOR`**，未另开 asmdef。字节级 Python 写入，**BOM 保住**（`BOM=True`）。
  - 其余 5 个含 UnityEditor 的文件（`InputManager`/`KeyScreen`/`WeaponCfg`/`RoleData_SO`/`PrefabReplacerOnInstance`）均在 `#if` 守卫内；`EnemyFXControllerUnit`/`BuildingFXController` 用全限定 `UnityEditor.*` 且在 `#endif` 内；`DetectionModule` 那处是注释。
- **整理方案（目录级映射，⏳ 待用户拍板——按项目约定搬移由用户手动走 Unity）**：拆掉 `02Game/` 这层；`03Player→Player`（`MainController→Player/Controller`）、`05Interactable→Interactable`（收 `Common/FurnitureContract.cs`）、`06Npc→Npc`、`AI→AI`（`FxCont→Fx`、`Editor/` 单列或留守卫）、`Game/Shared/Weapon + 根 3 个武器机制文件→Weapon/`、`Game/Mission→Mission/`、`Game/Data→Data/`、`Gameplay/{Bag,Projectile,SPEffect}→Bag/Projectile/Effect`、`Gameplay/Util→Common`、`Airdrop/AirdropData + Projectile/AirdropPod→Airdrop/`、根 3 个事件→`Events/`、`Game/` 剩余杂项→`Game/`。**共约 20 个目录级移动，覆盖 ~180 个 .cs**。
- **未做**：未搬任何文件（按约定用户手动）；未编译未 Play（按 MCP 规则不主动 refresh）。
"""

if not txt.endswith(nl):
    txt += nl
txt += note.replace("\n", nl)
open(p, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
print("appended, new bytes =", len(open(p, "rb").read()))
