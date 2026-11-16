# -*- coding: utf-8 -*-
"""统计某个场景里各预制体实例有多少个（按 m_SourcePrefab 的 guid 反查路径）。

用途：判断「运行时看到的 Default 层肢体」到底来自哪些预制体 —— 层改在预制体上有没有用，
取决于这些对象是"场景里直接摆的"还是"预制体实例"。
"""
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

scene = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Scene/BattleScene.unity'

SRC_RE = re.compile(r'm_SourcePrefab:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})')

# guid -> prefab 路径
guid2path = {}
for dp, _dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if not f.endswith('.prefab.meta'):
            continue
        p = os.path.join(dp, f)
        with open(p, encoding='utf-8', errors='replace') as fh:
            m = re.search(r'guid:\s*([0-9a-f]{32})', fh.read())
        if m:
            guid2path[m.group(1)] = os.path.relpath(p[:-5], ROOT).replace(os.sep, '/')

text = open(os.path.join(ROOT, scene), encoding='utf-8', errors='replace').read()
count = {}
for g in SRC_RE.findall(text):
    count[g] = count.get(g, 0) + 1

rows = sorted(((n, guid2path.get(g, '<未知 ' + g + '>')) for g, n in count.items()), reverse=True)
print('%s 里共有 %d 种预制体实例、合计 %d 个实例' % (scene, len(rows), sum(count.values())))
for n, p in rows[:35]:
    print('  %4d  %s' % (n, p))
