# -*- coding: utf-8 -*-
"""步骤②反向风险侦察：6 个 Data 文件离开 FPSGame.Game 后，是否还需 using FPSGame.Game;"""
import os
import re
import io

ROOT = r'd:\Pro\Bluedivers'
G = os.path.join(ROOT, 'Assets/Scripts/06Gameplay')
DECL = re.compile(r'\b(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|struct|interface|enum)\s+(\w+)')
NSD = re.compile(r'namespace\s+([\w\.]+)')


def strip(t):
    t = re.sub(r'/\*.*?\*/', ' ', t, flags=re.S)
    t = re.sub(r'//[^\n]*', ' ', t)
    t = re.sub(r'"(\\.|[^"\\])*"', '""', t)
    t = re.sub(r"'(\\.|[^'\\])*'", "''", t)
    return t


pool = {}
for r, d, fs in os.walk(G):
    rel = os.path.relpath(r, G).replace('\\', '/')
    if rel == 'Data' or rel.startswith('Data/'):
        continue
    for f in fs:
        if not f.endswith('.cs'):
            continue
        t = io.open(os.path.join(r, f), encoding='utf-8-sig', errors='replace').read()
        m = NSD.search(t)
        if not m or m.group(1) != 'FPSGame.Game':
            continue
        for n in DECL.findall(t):
            pool[n] = rel

print('仍留在 FPSGame.Game 的类型数:', len(pool))
print('示例:', sorted(pool)[:15])
print()
for f in sorted(os.listdir(os.path.join(G, 'Data'))):
    if not f.endswith('.cs'):
        continue
    t = io.open(os.path.join(G, 'Data', f), encoding='utf-8-sig', errors='replace').read()
    code = strip(t)
    hasG = bool(re.search(r'using\s+FPSGame\.Game\s*;', code))
    hits = [n for n in pool if re.search(r'\b' + n + r'\b', code)]
    print('  %-28s hasGameUsing=%-5s 引用GameNs类型=%s' % (f, hasG, hits[:8]))
