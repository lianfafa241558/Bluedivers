# -*- coding: utf-8 -*-
"""把 CampData_SODrawer.cs 截断到第 441 行（CampTemplateDrawer 类结束），
再补回命名空间收尾 '}'。MissionEnumGridDrawer 已迁往 CampData_SOEditor.cs。
可重跑：仅当第 443 行仍含 'MissionEnumGridDrawer' 时才动手。"""
import io

PATH = r"e:\Bluedivers\Assets\Editor\Drawer\CampData_SODrawer.cs"
KEEP = 441  # 保留到该行（= CampTemplateDrawer 类的 '}'）

with io.open(PATH, "r", encoding="utf-8-sig", newline="") as f:
    lines = f.read().splitlines(keepends=True)

if len(lines) <= KEEP or "MissionEnumGridDrawer" not in "".join(lines[KEEP:KEEP + 30]):
    print("SKIP: 已是目标状态或锚点不匹配，行数 =", len(lines))
    raise SystemExit(0)

body = "".join(lines[:KEEP])
if not body.endswith("\n"):
    body += "\r\n"
body += "}\r\n"  # namespace FPSGame.EditorExt 收尾

with io.open(PATH, "w", encoding="utf-8-sig", newline="") as f:
    f.write(body)

print("OK: 截断为", KEEP + 1, "行")
