# -*- coding: utf-8 -*-
"""把 asmdef 的 GUID references 翻译成程序集名（Unity 允许 asmdef 用 GUID 引用）。

用法：
    python asmdef_refs.py            # 打印全部 asmdef 的 name -> references(名字)
    python asmdef_refs.py 05_UnitCore 04_Data   # 只看指定程序集（按 name 或 asmdef 文件名）
"""
import json
import os
import re
import sys

ROOT = r"d:\Pro\Bluedivers\Assets"


def guid_of(path):
    meta = path + ".meta"
    if not os.path.exists(meta):
        return None
    m = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", open(meta, "r", encoding="utf-8-sig").read(), re.M)
    return m.group(1) if m else None


def main():
    asmdefs = {}
    for dirpath, _, filenames in os.walk(ROOT):
        for fn in filenames:
            if fn.endswith(".asmdef"):
                p = os.path.join(dirpath, fn)
                g = guid_of(p)
                if g:
                    asmdefs[g] = (p, json.load(open(p, "r", encoding="utf-8-sig")))

    name_of = {}
    for g, (p, data) in asmdefs.items():
        name_of[g] = data.get("name") or os.path.basename(p)[:-7]

    want = set(sys.argv[1:])
    for g, (p, data) in sorted(asmdefs.items(), key=lambda kv: name_of[kv[0]]):
        name = name_of[g]
        if want and not (name in want or os.path.basename(p)[:-7] in want):
            continue
        refs = []
        for r in data.get("references", []):
            if r.startswith("GUID:"):
                refs.append(name_of.get(r[5:], "??" + r[5:][:8]))
            else:
                refs.append(r)
        rel = os.path.relpath(p, r"d:\Pro\Bluedivers")
        print("%-16s %-46s -> %s" % (name, rel, ", ".join(refs)))


if __name__ == "__main__":
    main()
