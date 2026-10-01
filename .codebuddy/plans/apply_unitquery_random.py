# -*- coding: utf-8 -*-
"""把 FindUnits / BattleRandom 的契约调用点改到新落点（字节级，保 BOM/CRLF）。

规则：
  1) `FPSGame.GameContract.ServiceLocator.Battle.FindUnits(`      -> `FPSGame.Game.UnitQuery.FindUnits(`        全仓
  2) `FPSGame.GameContract.ServiceLocator.Battle.BattleRandom`    -> `FPSGame.Data.BattleState.BattleRandom`   全仓
  3) `manager.BattleRandom`                                       -> `FPSGame.Data.BattleState.BattleRandom`   **仅 MissionBase.cs**
     ⚠ `manager.BattleRandom` 在 09_Managers 内部（WaveManager/PatrolContriller/MissionController）也是同一写法，
       但那里的 manager 是 BattleManager 实例、属同层直连 ⇒ 绝不能改。
"""
import os

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts"
MISSION_BASE = "MissionBase.cs"

R_ALL = [
    (b"FPSGame.GameContract.ServiceLocator.Battle.FindUnits(", b"FPSGame.Game.UnitQuery.FindUnits("),
    (b"FPSGame.GameContract.ServiceLocator.Battle.BattleRandom", b"FPSGame.Data.BattleState.BattleRandom"),
]
R_MISSION_ONLY = [
    (b"manager.BattleRandom", b"FPSGame.Data.BattleState.BattleRandom"),
]

EXPECT = {b"FPSGame.GameContract.ServiceLocator.Battle.FindUnits(": 12,
          b"FPSGame.GameContract.ServiceLocator.Battle.BattleRandom": 4,
          b"manager.BattleRandom": 3}

found = {k: 0 for k, _ in R_ALL + R_MISSION_ONLY}

for dirpath, _, filenames in os.walk(ROOT):
    for fn in filenames:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dirpath, fn)
        data = open(p, "rb").read()
        orig = data
        rules = list(R_ALL)
        if fn == MISSION_BASE:
            rules += list(R_MISSION_ONLY)
        hits = []
        for old, new in rules:
            c = data.count(old)
            if c:
                found[old] += c
                data = data.replace(old, new)
                hits.append("%s x%d" % (old.decode("utf-8"), c))
        if data != orig:
            open(p, "wb").write(data)
            print("PATCH %-70s %s" % (os.path.relpath(p, os.path.dirname(ROOT)), " | ".join(hits)))

print("---")
total_ok = True
for k, v in found.items():
    e = EXPECT.get(k, 0)
    flag = "OK" if v == e else "!! 期望 %d" % e
    if v != e:
        total_ok = False
    print("%-60s 实际=%d  %s" % (k.decode("utf-8"), v, flag))
print("ALL_OK" if total_ok else "MISMATCH")
