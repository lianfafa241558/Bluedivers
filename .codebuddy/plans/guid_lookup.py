# -*- coding: utf-8 -*-
"""按 guid 反查资产路径：python guid_lookup.py <guid> [<guid> ...]"""
import glob
import io
import os
import re
import sys

for _s in (sys.stdout, sys.stderr):
    try:
        _s.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
want = set(sys.argv[1:])
found = {}

for p in glob.glob(os.path.join(ROOT, "Assets/**/*.meta"), recursive=True):
    with io.open(p, encoding="utf-8", errors="ignore") as f:
        head = f.read(200)
    m = re.search(r"^guid: ([0-9a-f]{32})", head, re.M)
    if m and m.group(1) in want:
        found[m.group(1)] = os.path.relpath(p[:-5], ROOT).replace("\\", "/")

for g in sys.argv[1:]:
    print("%s -> %s" % (g, found.get(g, "<无主：断链>")))

for g, p in found.items():
    with io.open(os.path.join(ROOT, p), encoding="utf-8", errors="ignore") as f:
        s = f.read()
    nm = re.search(r"^  name: \"(.+)\"$", s, re.M)
    print("   ", p, "|", nm.group(1) if nm else "?")
