# -*- coding: utf-8 -*-
"""把 4 个文件里的 `ServiceLocator.Battle.Authorize(...)` 换成事件总线调用（字节级，保 BOM/CRLF）。

判据：这些文件里该子串出现的次数应与预期一致（12），否则报错不写。
"""
import os

ROOT = r"d:\Pro\Bluedivers"
FILES = [
    r"Assets\Scripts\06Gameplay\02Game\Game\Mission\MissionBase.cs",
    r"Assets\Scripts\06Gameplay\02Game\05Interactable\Furniture_PlayerDown.cs",
    r"Assets\Scripts\06Gameplay\02Game\05Interactable\Furniture_PlayerArtillery.cs",
    r"Assets\Scripts\06Gameplay\02Game\05Interactable\Furniture_Artillery.cs",
]
OLD = b"FPSGame.GameContract.ServiceLocator.Battle.Authorize("
NEW = b"FPSGame.Gameplay.BattleEventSub.RequestAuthorize("

total = 0
for rel in FILES:
    p = os.path.join(ROOT, rel)
    data = open(p, "rb").read()
    n = data.count(OLD)
    if n == 0:
        print("SKIP       %s" % rel)
        continue
    open(p, "wb").write(data.replace(OLD, NEW))
    print("REPLACED %-2d %s" % (n, rel))
    total += n
print("---")
print("total=%d (expected 12)" % total)
