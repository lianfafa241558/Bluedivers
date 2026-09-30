# -*- coding: utf-8 -*-
"""
审计 [SerializeReference] 的命名空间失配：
资产 YAML 里存 `type: {class: X, ns: , asm: A}`，而 X 现在已落到某个命名空间 ⇒ Unity 报
"Missing types referenced from component ..."（数据静默丢失）。

判据：
- 扫 *.prefab / *.unity / *.asset 里 `type: {class: ..., ns: , asm: ...}`（ns 为空）；
- 将该 class 名（`Owner/Nested` 取 Owner）在 .cs 里查其实际 namespace；
- 若实际 namespace 非空 ⇒ 失配（应把 ns 补上）。
"""
import os
import re
import sys
from collections import defaultdict

ROOT = r"d:\Pro\Bluedivers\ASSETS"
ASSETS = r"d:\Pro\Bluedivers\Assets"

YAML_EXT = (".prefab", ".unity", ".asset")
PAT = re.compile(r"type:\s*\{class:\s*([^,}]+),\s*ns:\s*([^,}]*),\s*asm:\s*([^,}]+)\}")

# 1) 收集类名 -> 命名空间（只取 class/struct/enum 声明；忽略第三方目录）
DECL = re.compile(r"^\s*(?:public|internal|private|protected)?\s*(?:static\s+|abstract\s+|sealed\s+|partial\s+)*"
                  r"(class|struct|enum|interface)\s+([A-Za-z_][\w]*)", re.M)
NSDECL = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)

type_ns = defaultdict(set)
for dirpath, dirnames, filenames in os.walk(ASSETS):
    low = dirpath.replace("\\", "/")
    if any(x in low for x in ("/MackySoft/", "/Plugins/", "/Editor/")) and "/Scripts/Editor/" not in low:
        continue
    for fn in filenames:
        if not fn.endswith(".cs"):
            continue
        p = os.path.join(dirpath, fn)
        try:
            t = open(p, "rb").read().decode("utf-8-sig")
        except Exception:
            continue
        ns = NSDECL.search(t)
        ns = ns.group(1) if ns else ""
        for _, name in DECL.findall(t):
            type_ns[name].add(ns)

# 2) 扫资产
hits = defaultdict(list)   # class -> [(file, line, asm)]
empty_total = 0
for dirpath, dirnames, filenames in os.walk(ASSETS):
    for fn in filenames:
        if not fn.endswith(YAML_EXT):
            continue
        p = os.path.join(dirpath, fn)
        try:
            t = open(p, "rb").read().decode("utf-8-sig", "ignore")
        except Exception:
            continue
        for i, line in enumerate(t.splitlines(), 1):
            m = PAT.search(line)
            if not m:
                continue
            cls, ns, asm = m.group(1).strip(), m.group(2).strip(), m.group(3).strip()
            empty_total += 1
            owner = cls.split("/")[0].strip()
            real = type_ns.get(owner, set())
            real_nonempty = {x for x in real if x}
            if ns == "" and real_nonempty:
                hits[cls].append((p, i, asm, sorted(real_nonempty)))

print("含 SerializeReference 类型声明的资产条目总数:", empty_total)
print("其中 ns 为空 且 该类型现已落入命名空间 的条目:")
tot = 0
for cls in sorted(hits):
    items = hits[cls]
    tot += len(items)
    print("  %-40s %s  实际命名空间=%s  命中 %d 个资产" %
          (cls, items[0][2], ",".join(items[0][3]), len(items)))
    for p, i, asm, _ in items[:3]:
        print("      %s:%d" % (os.path.relpath(p, r"d:\Pro\Bluedivers").replace("\\", "/"), i))
    if len(items) > 3:
        print("      ... 共 %d 处" % len(items))
print("合计需修复条目:", tot)
