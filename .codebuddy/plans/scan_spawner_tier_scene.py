# -*- coding: utf-8 -*-
"""在**场景**里找挂 CreatEnemy 的对象及其 tier（prefab 已单独扫过，无 Alert）。"""
import glob
import os
import re

ROOT = r'e:\Bluedivers'
GUID = '2cb9d998400cce944bfa76344cb8e758'
NAMES = {0: 'None', 1: 'Small', 2: 'Medium', 3: 'Elite', 4: 'Heavy', 5: 'Giant',
         6: 'Alert', 7: 'Special1', 8: 'Special2', 9: 'Boss'}

for p in glob.glob(os.path.join(ROOT, 'Assets', '**', '*.unity'), recursive=True):
    t = open(p, encoding='utf-8', errors='ignore').read()
    if GUID not in t:
        continue
    vals = {}
    for b in t.split('--- !u!'):
        if GUID not in b:
            continue
        m = re.search(r'^  tier: (\d+)', b, re.M)
        if m:
            v = int(m.group(1))
            vals[v] = vals.get(v, 0) + 1
    print(os.path.relpath(p, ROOT), {f'{k}({NAMES.get(k, "?")})': n for k, n in sorted(vals.items())})
