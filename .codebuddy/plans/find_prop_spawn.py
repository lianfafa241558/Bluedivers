# -*- coding: utf-8 -*-
"""找出这些"分叉 Id"的 prefab 文件、guid，以及**引用它们的地方**（代码 / prefab / asset / 场景）。"""
import glob
import io
import os
import re

ROOT = r'e:\Bluedivers'
NAMES = ['OilDrum', 'fuel', 'Container', 'Spore', 'post', 'wire', 'LightA', 'LightB', 'rollerShutter']
OUT = os.path.join(ROOT, '.codebuddy', 'plans', 'prop_spawn_report.txt')

guid2name = {}
lines = []
for nm in NAMES:
    for p in glob.glob(os.path.join(ROOT, 'Assets', '**', nm + '.prefab'), recursive=True):
        meta = p + '.meta'
        if not os.path.exists(meta):
            continue
        m = re.search(r'^guid: (\w+)', io.open(meta, encoding='utf-8').read(), re.M)
        if not m:
            continue
        guid2name[m.group(1)] = (nm, os.path.relpath(p, ROOT))

refs = {g: [] for g in guid2name}
for pat in ('*.cs', '*.prefab', '*.asset', '*.unity'):
    for q in glob.glob(os.path.join(ROOT, 'Assets', '**', pat), recursive=True):
        try:
            t = io.open(q, encoding='utf-8', errors='ignore').read()
        except OSError:
            continue
        for g in guid2name:
            if g in t:
                refs[g].append(os.path.relpath(q, ROOT))

for g, (nm, rel) in guid2name.items():
    lines.append('=== %s  (%s)  guid=%s' % (nm, rel, g))
    for r in refs[g][:15]:
        lines.append('    <- %s' % r)
    if len(refs[g]) > 15:
        lines.append('    ... 共 %d 处' % len(refs[g]))
    lines.append('')

io.open(OUT, 'w', encoding='utf-8').write('\n'.join(lines))
print('ok', len(guid2name))
