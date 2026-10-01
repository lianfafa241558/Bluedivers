# -*- coding: utf-8 -*-
"""ServiceLocator.Battle -> BattleHub.Current、NullServices.Battle -> NullBattleService.Instance（字节级，保 BOM/CRLF）。

⚠ 替换顺序：先长前缀（FPSGame.GameContract.ServiceLocator.Battle），再裸写（ServiceLocator.Battle），
  否则裸写规则会把长前缀替换后的结果再动一次。
"""
import os

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts"

RULES = [
    (b"FPSGame.GameContract.ServiceLocator.Battle", b"FPSGame.GameContract.BattleHub.Current"),
    (b"ServiceLocator.Battle", b"BattleHub.Current"),
    (b"NullServices.Battle", b"NullBattleService.Instance"),
]

total = {k: 0 for k, _ in RULES}

for dirpath, _, filenames in os.walk(ROOT):
    for fn in filenames:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dirpath, fn)
        data = open(p, "rb").read()
        orig = data
        hits = []
        for old, new in RULES:
            c = data.count(old)
            if c:
                total[old] += c
                data = data.replace(old, new)
                hits.append("%s x%d" % (old.decode("utf-8"), c))
        if data != orig:
            open(p, "wb").write(data)
            print("PATCH %-72s %s" % (os.path.relpath(p, ROOT), " | ".join(hits)))

print("---")
for k, v in total.items():
    print("%-52s %d" % (k.decode("utf-8"), v))
print("TOTAL=%d" % sum(total.values()))
