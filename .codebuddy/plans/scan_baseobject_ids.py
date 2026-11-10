# 扫描全仓 prefab 的 BaseObject.Id，统计唯一性（为“全局 prefab 表”方案取数据）
import os
import re
import collections

ROOT = r"d:\Pro\Bluedivers\Assets"
GUID = "669a96ab6487d5b47a05a4f0d3c637ce"

blocks_re = re.compile(r"^--- !u!114 &-?\d+\n(.*?)(?=^--- |\Z)", re.S | re.M)

files = []
for d, _, fs in os.walk(ROOT):
    for f in fs:
        if f.endswith(".prefab"):
            files.append(os.path.join(d, f))

ids = collections.defaultdict(list)
with_bo = 0
for p in files:
    s = open(p, encoding="utf-8", errors="ignore").read()
    if GUID not in s:
        continue
    with_bo += 1
    for blk in blocks_re.findall(s):
        if GUID not in blk:
            continue
        m = re.search(r"^  Id:[ \t]*(.*)$", blk, re.M)
        if not m:
            continue
        v = m.group(1).strip()
        if not v:
            continue
        ids[v].append(os.path.relpath(p, ROOT))

print("prefabs total      :", len(files))
print("with BaseObject    :", with_bo)
print("unique non-empty Id:", len(ids))
dups = {k: v for k, v in ids.items() if len(v) > 1}
print("duplicate Ids      :", len(dups))
for k, v in sorted(dups.items(), key=lambda x: -len(x[1])):
    print("  %-28s %d  %s" % (k, len(v), v[:3]))
