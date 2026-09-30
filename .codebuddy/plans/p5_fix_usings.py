# -*- coding: utf-8 -*-
"""补齐因"类型被移入命名空间"而缺失的 using（机械补齐，重复 using 无害）。

背景：并行会话把 00GameContract/*.cs 拆成一接口一文件并加上 `namespace GameContract`，
同时给 00Core/SKVP.cs、00Core/Constants.cs 加上 `namespace Core`，
但消费方的 using 还没扫完 ⇒ 21 条 CS0246/CS0103。本脚本只做"补 using"，不改任何逻辑。
"""

import io
import os
import re

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts"

TARGETS = [
    (r"02Game\05Interactable\Furniture_HandEquip.cs", "GameContract"),
    (r"02Game\05Interactable\Furniture_OOPartDepositBase.cs", "GameContract"),
    (r"02Game\05Interactable\Furniture_Equip.cs", "GameContract"),
    (r"02Game\03Player\VehicleWeaponsManager.cs", "GameContract"),
    (r"04UI\UI\VehicleUI.cs", "GameContract"),
    (r"02Game\Game\Data\WeaponModuleData_SO.cs", "Core"),
    (r"02Game\Game\Data\WeaponUpgradeData_SO.cs", "Core"),
    (r"04UI\TipWnd.cs", "Core"),
    (r"02Game\AI\StateMachine\EnemyNestBuild.cs", "Core"),
    (r"01Manager\Battle\WeatherSystem.cs", "Core"),
    (r"02Game\Game\Shared\Weapon\WeaponCfg.cs", "Core"),
]

USE_RE = re.compile(r"^\s*using\s+[\w\.]+\s*;\s*$")
STOP_RE = re.compile(r"^\s*(namespace\b|public\b|internal\b|\[)")

patched = skipped = failed = 0
for rel, ns in TARGETS:
    path = os.path.join(ROOT, rel)
    if not os.path.exists(path):
        print("!! missing :", rel)
        failed += 1
        continue
    with io.open(path, "r", encoding="utf-8-sig", newline="") as f:
        lines = f.read().split("\n")

    want = "using %s;" % ns
    if any(l.strip() == want for l in lines):
        print("skip (has %s) :" % want, rel)
        skipped += 1
        continue

    last = -1
    for i, l in enumerate(lines[:80]):
        if USE_RE.match(l):
            last = i
        elif STOP_RE.match(l):
            break

    if last < 0:
        print("!! no using block :", rel)
        failed += 1
        continue

    lines.insert(last + 1, want)
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write("\n".join(lines))
    print("patched :", rel, "->", want)
    patched += 1

print("\npatched=%d skipped=%d failed=%d" % (patched, skipped, failed))
