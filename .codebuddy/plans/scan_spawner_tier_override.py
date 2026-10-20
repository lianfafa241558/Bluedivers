# -*- coding: utf-8 -*-
"""找 prefab/scene 里对 CreatEnemy.tier 的覆写（预制体实例的 m_Modification），列出所有取值。"""
import glob
import os
import re

ROOT = r'e:\Bluedivers'
GUID = '2cb9d998400cce944bfa76344cb8e758'
NAMES = {0: 'None', 1: 'Small', 2: 'Medium', 3: 'Elite', 4: 'Heavy', 5: 'Giant',
         6: 'Alert', 7: 'Special1', 8: 'Special2', 9: 'Boss'}

rows = []
for pat in ('*.prefab', '*.unity'):
    for p in glob.glob(os.path.join(ROOT, 'Assets', '**', pat), recursive=True):
        try:
            t = open(p, encoding='utf-8', errors='ignore').read()
        except OSError:
            continue
        if 'propertyPath: tier' not in t:
            continue
        for m in re.finditer(r'propertyPath: tier\n\s+value: (\d+)', t):
            v = int(m.group(1))
            rows.append((os.path.relpath(p, ROOT), v, NAMES.get(v, '?')))

print('tier 覆写:', len(rows))
from collections import Counter
print(Counter(r[2] for r in rows))
for r in rows:
    if r[2] == 'Alert':
        print('ALERT <-', r[0])
