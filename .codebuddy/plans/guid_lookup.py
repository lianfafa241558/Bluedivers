"""按 guid 反查脚本文件名。

用法：python -X utf8 .codebuddy/plans/guid_lookup.py <guid> [guid ...]
"""
import os
import re
import sys

ROOT = r"e:\Bluedivers\Assets"
wanted = {g.lower() for g in sys.argv[1:]}
found = {}
for dirpath, _, files in os.walk(ROOT):
    for fn in files:
        if not fn.endswith(".meta"):
            continue
        with open(os.path.join(dirpath, fn), "r", encoding="utf-8", errors="replace") as f:
            m = re.search(r"guid:\s*([0-9a-f]{32})", f.read())
        if m and m.group(1) in wanted:
            found[m.group(1)] = os.path.join(dirpath, fn)[:-5]

for g in sys.argv[1:]:
    print(f"{g} -> {found.get(g.lower(), '(未找到)')}")
