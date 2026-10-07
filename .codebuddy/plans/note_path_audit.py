# -*- coding: utf-8 -*-
"""向 2026-10-02 记忆日志追加一条（append-only）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-02.md"

NOTE = """

## 目录重排后的「记忆路径核验 + 修正」（2026-10-02）

- 触发：用户大规模改了 `Assets/Scripts` 目录名（`01Manager`→`09Manager` 等），要求重核记忆里的文件路径。
- 新工具 `plans/memory_path_audit.py`（默认注入层 = `MEMORY.md` + `topics/*.md`；`--all` 追加 daily/archive）：
  抽 `Assets/...` 全路径、`NNxxx/...` 片段路径、裸文件名三种写法，判存在性并给「同名现存路径」；
  `EXTRA` 列表单列 `update_memory` 长期条目里的路径。
- ✅ **权威对照（目录 ↔ asmdef name，16 组，实测）已写入 `topics/architecture.md`「目录约定」**，
  并给出旧名换算：`01Manager`→`09Manager`、`02Data`→`04Data`、`02Game`→`06Gameplay`、
  `04UI`(旧 asmdef `04_UI`)→`10UI`(`10_UI`)、`00Attribute`→`00Attributes`、`00_WndTools`(已删并)→`00Tools`+`10UI`。
  ⚠ **目录名 ≠ asmdef name**（唯一同名例：`05_EffectComp`/`10_Effect`/`DayNightSystem`）⇒ 定位代码用**目录名**，asmdef 名只在改 `references` 时用。
- 本轮修正：`topics/contract.md` 三处（`00GameContract/BattleHub.cs` 无 `Services/`、`10_Effect/VFX/VFXAirdropEffect.cs`、`00Core/CoreEnums.cs`）
  + `architecture.md` 过时的「`04_UI` 的 name=`04_UI`」+ `MEMORY.md` 热区 2 增一行指针。
- 长期记忆条目：更新 3 条路径（`Furniture_NPCChat`→`06Gameplay/Npc/`、`AirdropData_SO`→`04Data/`、
  `PathRequestManager`→`09Manager/Battle/` + `EnemyController`→`06Gameplay/AI/Controller/`）；
  另有 2 条「程序集拆解诊断」条目仍通篇用旧名（`01Manager`/`02Game`/`02Data`），**未改，待拍板**。
- 复验：注入层失效路径 **1 → 0**（末条是把一串 asmdef 名用 `/` 连写造成的假阳性，已改顿号）。
  daily 层 362 条「失效」属**历史流水**（记录当时的真实路径），不改，检索时按对照表换算。
- ⚠ 顺带核验：本次目录重排**没有**造成新的 Missing Script —— 全项目脚本 guid 断链仅 9 个，全在第三方插件
  （WarFX 75 处、FinalIK demo、`Projectile_Disc.prefab`、`Directional Light.prefab`），与重排无关。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
