# -*- coding: utf-8 -*-
"""把 prefab 里 AutomaticReleaseOnCharged: 1 的武器改成 WeaponFlag |= AutoFireOnFull(1<<5)，
并删掉该行（C# 字段已移除）。字节级改写，保留原换行/编码，避免整份换 CRLF。
"""
import io
import os
import sys

ROOT = r"e:/Bluedivers"
FLAG = 1 << 5  # WeaponFlag.AutoFireOnFull

FILES = [
    "Assets/Resources/Weapons/Warrant.prefab",
    "Assets/Resources/Weapons/PlasmaCannon.prefab",
    "Assets/Resources/Weapons/Accumulator.prefab",
    "Assets/Resources/Prefabs/Enemy/Robot/Royal/GuardTower_Grenade.prefab",
    "Assets/Resources/Prefabs/Enemy/Zerg/DEC/Dec_Queen.prefab",
    "Assets/Resources/Prefabs/Enemy/Zerg/DEC/Dec_Stalker.prefab",
    "Assets/Resources/Prefabs/Enemy/Zerg/DEC/Dec_Ultralisk.prefab",
    "Assets/Resources/Prefabs/Enemy/Zerg/DEC/Dec_Roach.prefab",
    "Assets/Resources/Prefabs/Enemy/Zerg/DEC/Dec_Hydralisk.prefab",
    "Assets/Resources/Prefabs/Enemy/Zerg/UNG/Ung_Stalker.prefab",
    "Assets/Resources/Prefabs/Enemy/Zerg/DEC/Dec_Bane.prefab",
    "Assets/Resources/Prefabs/Airdrop/Healdrone_HaloBomb.prefab",
]


def patch(path):
    full = os.path.join(ROOT, path)
    raw = open(full, "rb").read()
    lines = raw.splitlines(keepends=True)
    out = []
    hits = 0
    for ln in lines:
        s = ln.decode("utf-8")
        if s.strip() == "AutomaticReleaseOnCharged: 1":
            hits += 1
            ok = False
            for j in range(len(out) - 1, -1, -1):
                t = out[j].decode("utf-8")
                if t.strip().startswith("WeaponFlag:"):
                    body = t.rstrip("\r\n")
                    ending = t[len(body):]
                    indent = body[: len(body) - len(body.lstrip())]
                    val = int(body.split(":", 1)[1].strip())
                    out[j] = (indent + "WeaponFlag: %d" % (val | FLAG) + ending).encode("utf-8")
                    ok = True
                    break
            if not ok:
                print("!! 找不到 WeaponFlag 行: %s" % path)
                return hits, False
            continue  # 删除该行
        out.append(ln)
    new = b"".join(out)
    if hits:
        open(full, "wb").write(new)
    return hits, True


total = 0
for f in FILES:
    n, ok = patch(f)
    total += n
    print("%-70s hits=%d ok=%s" % (f, n, ok))

print("total=%d" % total)
