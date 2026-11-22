# -*- coding: utf-8 -*-
"""对比 ToonLit 家族各 shader 的 Properties 声明，找出缺失项。"""
import io
import os
import re

DIR = r"d:\Pro\Bluedivers\Assets\Shader\ToonLit\Main"

PROP_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*([A-Za-z_][\w]*)\s*\(")


def props(path):
    txt = io.open(path, encoding="utf-8-sig", errors="replace").read()
    m = re.search(r"Properties\s*\{(.*?)\n\s*\}\s*\n", txt, re.S)
    if not m:
        return []
    names = []
    for line in m.group(1).splitlines():
        s = line.strip()
        if not s or s.startswith("//"):
            continue
        mm = PROP_RE.match(s)
        if mm:
            names.append(mm.group(1))
    return names


files = ["ToonLit.shader", "ToonLit_Unit.shader", "ToonLit_Scene.shader", "ToonLit_Stone.shader",
         "ToonLit_Hair.shader", "ToonLit_Face.shader", "ToonLit_MouthEye.shader", "ToonLit_Colour.shader"]

data = {}
for f in files:
    p = os.path.join(DIR, f)
    if os.path.exists(p):
        data[f] = props(p)

union = []
for f, names in data.items():
    for n in names:
        if n not in union:
            union.append(n)

for f, names in data.items():
    missing = [n for n in union if n not in names]
    print(f, "count=", len(names))
    print("  missing:", ", ".join(missing) if missing else "(none)")
