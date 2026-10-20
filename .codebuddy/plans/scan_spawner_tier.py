# -*- coding: utf-8 -*-
"""列出挂了 CreatEnemy（刷怪器）的 prefab 里配置的 tier 值，找 'tier=6(Alert)' 的来源。"""
import glob
import os
import re

ROOT = r'e:\Bluedivers'
GUID = '2cb9d998400cce944bfa76344cb8e758'   # CreatEnemy.cs
NAMES = {0: 'None', 1: 'Small', 2: 'Medium', 3: 'Elite', 4: 'Heavy', 5: 'Giant',
         6: 'Alert', 7: 'Special1', 8: 'Special2', 9: 'Boss'}

rows = []
for p in glob.glob(os.path.join(ROOT, 'Assets', '**', '*.prefab'), recursive=True):
    try:
        t = open(p, encoding='utf-8', errors='ignore').read()
    except OSError:
        continue
    if GUID not in t:
        continue
    for b in t.split('--- !u!'):
        if GUID not in b:
            continue
        tiers = [int(x) for x in re.findall(r'^  tier: (\d+)', b, re.M)]
        if tiers:
            rows.append((os.path.relpath(p, ROOT), [f'{x}({NAMES.get(x, "?")})' for x in sorted(set(tiers))]))

print('刷怪器 prefab 数:', len(rows))
for r in rows:
    print(r[0], '->', ','.join(r[1]))
