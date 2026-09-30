# -*- coding: utf-8 -*-
"""P5-1e：把嵌套在 TaskManager 里的 3 个 DTO 抽出来、解嵌套、搬到玩法层。

  1. TaskManager.cs:392-511  = SelectTaskData + TaskCfg + TaskItem（当前**嵌套**在 TaskManager 内）
  2. 抽出后放 Assets/Scripts/06Gameplay/TaskData.cs（文件作用域 ⇒ 即"解嵌套"）
  3. DTO 里 2 处 `Instance.Missions` → `MissionData_SO.Catalog`（数据自持，Catalog 由 TaskManager 写入）
  4. 全工程的 `TaskManager.TaskItem|SelectTaskData|TaskCfg` → 裸名
  5. 删掉 `using TaskItem = TaskManager.TaskItem;` 这类别名（解嵌套后失效）
"""

import io
import os
import re

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts"
TM = os.path.join(ROOT, r"01Manager\Global\TaskManager.cs")
OUT = os.path.join(ROOT, r"06Gameplay\TaskData.cs")

COMMENT = re.compile(r"^\s*(//|///|\*|/\*)")


def read_lines(p):
    with io.open(p, "r", encoding="utf-8-sig", newline="") as f:
        return f.read().split("\n")


def write_lines(p, lines):
    with io.open(p, "w", encoding="utf-8", newline="") as f:
        f.write("\n".join(lines))


tm = read_lines(TM)
print("TaskManager.cs 原行数 =", len(tm))
assert "public class SelectTaskData" in tm[392], tm[392]
assert "public class TaskItem" in tm[491], tm[491]

dto = tm[391:511]  # 1-based 392..511
assert dto[-1].strip() == "}" or dto[-1].strip() == "", repr(dto[-1])

# 3) DTO 内对 TaskManager 私有成员的两处引用 → 数据自持
dto = [l.replace("Instance.Missions", "MissionData_SO.Catalog") for l in dto]

header = [
    "using System.Collections.Generic;",
    "using System.Linq;",
    "using Core;",
    "using GameContract;",
    "using UnityEngine;",
    "",
    "// ============================================================================",
    "// 以下三个 DTO 原先是 `TaskManager` 的**嵌套类型**（写在 01Manager/Global/TaskManager.cs 里），",
    "// 2026-09-30 抽出到玩法层并**解嵌套**（文件作用域）：",
    "//   ① 玩法核心类 MissionBase/MissionItem 的公开 API 用了 `TaskManager.TaskItem`/`SelectTaskData`",
    "//      ⇒ 不搬出来，它们就进不了 06_Gameplay；",
    "//   ② 它们只依赖 SO/枚举/基础类型（`MissionData_SO` 也已在玩法层）⇒ 可安全落这一层。",
    "// ⚠ `TaskCfg` 原先读 `TaskManager.Instance.Missions`（向上依赖）⇒ 改成读 `MissionData_SO.Catalog`",
    "//    （**数据自持**：由 TaskManager 在加载任务配置时写入）。",
    "// ============================================================================",
    "",
]
write_lines(OUT, header + dto)

# 2) TaskManager.cs 去掉 DTO 区间（保留 1..391 与 512..515）
write_lines(TM, tm[:391] + tm[511:])
print("TaskManager.cs 新行数 =", len(read_lines(TM)), " TaskData.cs =", len(read_lines(OUT)))

# 4) 全工程改名 + 5) 删别名
NAMES = ["TaskItem", "SelectTaskData", "TaskCfg"]
alias = re.compile(r"^\s*using\s+(TaskItem|SelectTaskData|TaskCfg)\s*=\s*TaskManager\.\1\s*;\s*$")
changed = 0
files = []
for dp, dn, fn in os.walk(ROOT):
    for f in fn:
        if f.endswith(".cs"):
            files.append(os.path.join(dp, f))

for p in files:
    if os.path.abspath(p) == os.path.abspath(OUT):
        continue
    lines = read_lines(p)
    touched = False
    out = []
    for line in lines:
        if alias.match(line):
            touched = True
            continue
        if not COMMENT.match(line):
            new = line
            for n in NAMES:
                new = new.replace("TaskManager." + n, n)
            if new != line:
                touched = True
                line = new
        out.append(line)
    if touched:
        write_lines(p, out)
        changed += 1
        print("   patched:", os.path.relpath(p, ROOT))

print("改名/删别名涉及文件 =", changed)
