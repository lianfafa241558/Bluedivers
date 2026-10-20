# -*- coding: utf-8 -*-
"""分叉细分：同一 Id 的实例，两端是 "x/z 相同、只有 y 不同"（高度量化差）还是 "x/z 也不同"（摆放分叉）。"""
import os
from collections import defaultdict

DIR = os.path.join(os.path.dirname(os.environ.get('LOCALAPPDATA', r'C:\Users\admin\AppData\Local')),
                   'LocalLow', 'GameDevelopmentDepartmentFaFa', 'BlueDivers')


def load(p):
    d = defaultdict(set)
    with open(os.path.join(DIR, p), encoding='utf-8', errors='replace') as f:
        for line in f:
            if not line or line.startswith('#'):
                continue
            q = line.rstrip('\n').split('\t')
            if len(q) < 4:
                continue
            x, y, z = (int(v) for v in q[2].split(','))
            d[q[1]].add((x, y, z))
    return d


a, b = load('netid_map_host.log'), load('netid_map_member.log')
print('%-22s %6s %6s %8s %8s %8s' % ('Id', '唯一A', '唯一B', '同xyz', '同xz异y', 'xz也异'))
for uid in sorted(set(a) | set(b)):
    sa, sb = a[uid], b[uid]
    if sa == sb:
        continue
    same_xyz = sa & sb
    axz = {(x, z) for x, _, z in sa}
    bxz = {(x, z) for x, _, z in sb}
    same_xz = len(axz & bxz) - len({(x, z) for x, _, z in same_xyz})
    xz_diff = len((axz | bxz) - (axz & bxz))
    print('%-22s %6d %6d %8d %8d %8d' % (uid, len(sa), len(sb), len(same_xyz), max(0, same_xz), xz_diff))

    # 举 2 个"xz 相同、y 不同"的例子
    if same_xz > 0:
        shown = 0
        for x, z in (axz & bxz):
            ys_a = sorted(y for xx, y, zz in sa if (xx, zz) == (x, z))
            ys_b = sorted(y for xx, y, zz in sb if (xx, zz) == (x, z))
            if ys_a != ys_b:
                print('      y 差: (%d,?,%d)  A=%s  B=%s' % (x, z, ys_a, ys_b))
                shown += 1
                if shown >= 2:
                    break
