# -*- coding: utf-8 -*-
"""向今日记忆日志追加一段（append-only，绝不覆盖已有内容）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-01.md"

NOTE = """

## MEMORY.md 注入超限压缩（第十七次）

- 会话开局注入时 `MEMORY.md` 因超限被截断 ⇒ 从 110 行 / 10294 字符压到 **76 行 / 7410 字符**；只删冗余表述与已收官明细，**所有 ⚠ 坑与判据保留**，章节结构未变。
- 顺手沉淀两条：①**子目录命名风格** = 程序集根下用纯英文名词、无数字前缀（`00Core/{Interfaces,Pool,Timer}`、`10_Effect/VFX`、`06Gameplay/{02Game,Common,Airdrop}`）；②MCP 实测实例改为 `Bluedivers@4a3e6a7b`:6401（`mcpforunity://instances` 实时值）——规则表里的 `Bluedivers@3d9f2357`:6400 **已过期**，hash 随项目路径变。

## 05UnitCore 分目录方案（只出方案 + 拖拽清单，未动文件）

- **现状**：20 个 `.cs` / 2725 行，全部 ns `FPSGame.Game`、同属 asmdef `05_UnitCore`（根目录平铺）。
- **方案 7 组**（纯分工；不动命名空间、不动 asmdef ⇒ **零代码改动、零 using 改动**，子目录不改变程序集归属）：
  - `Actor/` — `Actor.cs`(488)、`ActorsManager.cs`(98)、`AutoDeath.cs`(30)
  - `Health/` — `Health.cs`(351)、`Health_AboState.cs`(222)、`HealthPlayer.cs`(68)、`HealthEnemy.cs`(53)、`HealthShield.cs`(49)、`HealthOther.cs`(31)、`HealthSpecUnit.cs`(12)
  - `Damage/` — `Damageable.cs`(428)、`TransferDamageable.cs`(47)
  - `Shield/` — `ShieldBehaviour.cs`(120)、`ShieldRebuild.cs`(133)
  - `Query/` — `UnitQueryGrid.cs`(275)、`UnitQueryGridDebugger.cs`(68)、`UnitQuery.cs`(59)
  - `Event/` — `UnitEventSub.cs`(124)
  - `Common/` — `MinMaxParameters.cs`(40)、`GameConstants.cs`(29)
- **关键判据**：`HealthShield` 归 `Health/`（它是 `Health` 家族一员，护盾数值/音效配置就挂在它身上，拆出去打断继承链）；`Shield/` 只装"挂在 Health 上的护盾行为组件"（二者都不继承 `Health`）；`UnitQueryGridDebugger` **必须**留运行时目录（是 `MonoBehaviour`，进 Editor 程序集会 Missing Script）；`Health.cs` + `Health_AboState.cs` 是同一个 partial 类的两半，规范要求同目录。
- **引用热度**（新工具 `.codebuddy/plans/unitcore_scan.py`）：`Actor` 40 文件 / `ActorsManager` 33 / `UnitEventSub` 26 / `Health` 17 / `UnitQuery` 13 / `Damageable` 9 / `HealthEnemy` 4 / `NoiseData` 4；**0 外部引用** = `AutoDead`、`GameConstants`、`ShieldBehaviour`、`HealthSpecUnit`、`MinMaxColor`、`MinMaxVector3`、`AboGaugeEntry`、`ArmorBreakEffect`。
- **用户选择：自己在 Unity Project 窗口拖**（文件一律未动）⇒ 待拖完只读核对目录结构。
- ⚠ 已提醒的红线：**只能在 Unity 内拖**（Windows 资源管理器剪切粘贴会重分配 GUID ⇒ prefab Missing Script，项目踩过一次）；`.cs.meta` 自动跟随不用手动碰；`05_UnitCore.asmdef` 留在根目录不动；目录里那批 `5lflit5j.5s3~` 编辑器残留不属 Unity 资产，不在范围内。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
