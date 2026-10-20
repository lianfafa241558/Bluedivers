# -*- coding: utf-8 -*-
"""派生号映射：按 Id 汇总（两端坐标是否一致）+ 反查 netid_probe.log 里失败过的号属于哪个 Id。"""
import os
import re
from collections import defaultdict

DIR = os.path.join(os.path.dirname(os.environ.get('LOCALAPPDATA', r'C:\Users\admin\AppData\Local')),
                   'LocalLow', 'GameDevelopmentDepartmentFaFa', 'BlueDivers')
OUT = r'e:\Bluedivers\.codebuddy\plans\netid_map_report.txt'


def load(path):
    rows = {}
    with open(path, encoding='utf-8', errors='replace') as f:
        for line in f:
            if not line or line.startswith('#'):
                continue
            p = line.rstrip('\n').split('\t')
            if len(p) < 4:
                continue
            rows[(p[1], p[2], p[3].lstrip('#'))] = int(p[0])
    return rows


a = load(os.path.join(DIR, 'netid_map_host.log'))
b = load(os.path.join(DIR, 'netid_map_member.log'))

ga, gb = defaultdict(set), defaultdict(set)
for k in a:
    ga[k[0]].add((k[1], k[2]))
for k in b:
    gb[k[0]].add((k[1], k[2]))

out = []
out.append('host 记录=%d  member 记录=%d' % (len(a), len(b)))
out.append('%-28s %6s %6s %7s %7s' % ('Id', 'A条', 'B条', '两端同', '分叉'))

bad_ids = []
for uid in sorted(set(ga) | set(gb)):
    sa, sb = ga[uid], gb[uid]
    same = len(sa & sb)
    diff = max(len(sa), len(sb)) - same
    out.append('%-28s %6d %6d %7d %7d' % (uid, len(sa), len(sb), same, diff))
    if diff:
        bad_ids.append(uid)

out.append('')
out.append('分叉的 Id 数=%d：%s' % (len(bad_ids), ', '.join(bad_ids)))

# 反查 probe 里失败过的 netId
# ⚠ 只读**分角色**的新文件：老的 netid_probe.log 是上一局（且两端互相覆盖过）的残留，混进来会误报
probe_txt = ''
for nm in ('netid_probe_host.log', 'netid_probe_member.log'):
    p2 = os.path.join(DIR, nm)
    if os.path.exists(p2):
        probe_txt += '\n' + open(p2, encoding='utf-8', errors='replace').read()
if not probe_txt:
    p2 = os.path.join(DIR, 'netid_probe.log')
    if os.path.exists(p2):
        probe_txt = open(p2, encoding='utf-8', errors='replace').read()
if probe_txt:
    txt = probe_txt
    miss = [int(x) for x in re.findall(r'netId=(\d+)', txt)]
    miss = sorted(set(miss))
    by_netid = {}
    for k, v in a.items():
        by_netid.setdefault(v, []).append(k)
    out.append('')
    out.append('netid_probe.log: 出现的号 %d 个，其中能在 host 映射里查到的：' % len(miss))
    hit = defaultdict(int)
    for nid in miss:
        for k in by_netid.get(nid, []):
            hit[k[0]] += 1
    for uid, n in sorted(hit.items(), key=lambda x: -x[1])[:20]:
        out.append('   %-28s %d 次（该 Id 是否分叉：%s）' % (uid, n, '是' if uid in bad_ids else '否'))
    if not hit:
        out.append('   （一个都没查到 ⇒ 失败的号不是派生段，或不是本端算出来的）')

open(OUT, 'w', encoding='utf-8').write('\n'.join(out))
print('ok', len(out))
