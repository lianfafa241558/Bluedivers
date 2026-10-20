# -*- coding: utf-8 -*-
"""扫 prefab：Actor 带 Boss 旗标(1<<2) 但没有 PartController 的单位。
HpItemBoss.RebuildArmor 直接读 enemy.invincibleArmor ⇒ 这类单位会 NRE（控制台那条无堆栈 NRE 的候选来源）。
"""
import os
import re

ROOT = r'e:\Bluedivers\Assets'
PC_GUID = 'c2262e3fb5171014c88070bf9ee662c0'

hits = []
for dp, dn, fn in os.walk(ROOT):
    for f in fn:
        if not f.endswith('.prefab'):
            continue
        p = os.path.join(dp, f)
        try:
            t = open(p, encoding='utf-8', errors='ignore').read()
        except OSError:
            continue
        if 'flag:' not in t:
            continue
        go_flag = {}
        go_pc = set()
        for b in t.split('--- !u!'):
            m = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', b)
            if not m:
                continue
            gid = m.group(1)
            fm = re.search(r'^  flag: (\d+)', b, re.M)
            if fm and int(fm.group(1)) & 4:
                go_flag[gid] = int(fm.group(1))
            if PC_GUID in b:
                go_pc.add(gid)
        for gid, v in go_flag.items():
            if gid not in go_pc:
                hits.append((p.replace(ROOT, ''), gid, v))

print('boss-without-PartController:', len(hits))
for h in hits:
    print(h)
