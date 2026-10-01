# -*- coding: utf-8 -*-
"""列出全部活跃 asmdef 的 name（用于把整个程序集图离线编译一遍）。"""
import os
import re
import json

ROOT = r'd:\Pro\Bluedivers'
names = []
for r, d, fs in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fs:
        if f.endswith('.asmdef'):
            p = os.path.join(r, f)
            try:
                j = json.load(open(p, encoding='utf-8-sig'))
                names.append(j.get('name') or f[:-7])
            except Exception as e:
                names.append('!!%s(%s)' % (f, e))
# 保留出现顺序去重
seen, out = set(), []
for n in names:
    if n not in seen:
        seen.add(n)
        out.append(n)
print(' '.join(out))
print('共 %d 个' % len(out))
