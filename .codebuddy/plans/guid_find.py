"""按 guid 反查资产路径（工作区相对，区别于硬编码 e:\\Bluedivers 的 guid_lookup.py）。

用法：
  python -X utf8 .codebuddy/plans/guid_find.py <guid> [guid ...]
  python -X utf8 .codebuddy/plans/guid_find.py --name 关键字   # 按文件名关键字找 prefab
"""
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))), "Assets")

if len(sys.argv) > 2 and sys.argv[1] == "--name":
    key = sys.argv[2].lower()
    for dirpath, _, files in os.walk(ROOT):
        for fn in files:
            if fn.lower().endswith(".prefab") and key in fn.lower():
                print(os.path.join(dirpath, fn).replace(ROOT + os.sep, "Assets" + os.sep))
    sys.exit(0)

wanted = {g.lower() for g in sys.argv[1:]}
found = {}
for dirpath, _, files in os.walk(ROOT):
    for fn in files:
        if not fn.endswith(".meta"):
            continue
        p = os.path.join(dirpath, fn)
        with open(p, "r", encoding="utf-8", errors="replace") as f:
            m = re.search(r"guid:\s*([0-9a-f]{32})", f.read(400))
        if m and m.group(1) in wanted:
            found[m.group(1)] = p[:-5].replace(ROOT + os.sep, "Assets" + os.sep)

for g in sys.argv[1:]:
    print(f"{g} -> {found.get(g.lower(), '(未找到)')}")
