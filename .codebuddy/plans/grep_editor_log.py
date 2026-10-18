# -*- coding: utf-8 -*-
"""在 Editor-prev.log 里检索关键证据：新日志、异常、RailRoutes 相关。"""
import io
import os

p = os.path.join(os.environ['LOCALAPPDATA'], 'Unity', 'Editor', 'Editor-prev.log')
raw = open(p, 'rb').read()
text = None
for enc in ('utf-8', 'gbk', 'latin-1'):
    try:
        text = raw.decode(enc)
        break
    except Exception:
        continue
lines = text.splitlines()

keys = ['小地图/铁轨', '拦截火车', 'MissingComponent', 'CanvasRenderer', 'RailRoutes',
        'Invalid AABB', 'StackOverflow', 'NullReferenceException', 'ArgumentException',
        'IndexOutOfRange', 'graphic', 'MiniMap']
for k in keys:
    hits = [(i, l.strip()) for i, l in enumerate(lines) if k in l and not l.strip().startswith('0x')]
    print('===', k, len(hits))
    for i, l in hits[:6]:
        print('   ', i, l[:160])
    if len(hits) > 6:
        print('    ... last:', hits[-1][0], hits[-1][1][:160])
