# -*- coding: utf-8 -*-
"""扫描 06Gameplay/Weapon/ 下所有 ns != FPSGame.Weapon 的 .cs，生成 moves_step4.json。"""
import os
import re
import io
import json

ROOT = r'd:\Pro\Bluedivers'
D = os.path.join(ROOT, 'Assets', 'Scripts', '06Gameplay', 'Weapon')
NSD = re.compile(r'namespace\s+([\w\.]+)')
TARGET = 'FPSGame.Weapon'

moves = []
for f in sorted(os.listdir(D)):
    if not f.endswith('.cs'):
        continue
    p = os.path.join(D, f)
    rel = os.path.relpath(p, ROOT).replace('\\', '/')
    t = io.open(p, encoding='utf-8-sig', errors='replace').read()
    m = NSD.search(t)
    cur = m.group(1) if m else ''
    if cur != TARGET:
        moves.append([rel, TARGET])
        print('  %-24s -> %s' % (cur, f))

out = os.path.join(ROOT, '.codebuddy', 'plans', 'moves_step4.json')
json.dump(moves, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('写入 %s（%d 项）' % (out, len(moves)))
