# -*- coding: utf-8 -*-
"""扫 prefab：PartController 的 legs / invincibleArmor / deathArmor 是否有"字段缺失"(= null)。
HpItemBoss.RebuildArmor 直接 lenght = damageables.Length ⇒ 该数组为 null 就 NRE。
"""
import os
import re

ROOT = r'e:\Bluedivers\Assets'
PC_GUID = 'c2262e3fb5171014c88070bf9ee662c0'
KEYS = ('legs:', 'invincibleArmor:', 'deathArmor:')

bad = []
total = 0
for dp, dn, fn in os.walk(ROOT):
    for f in fn:
        if not f.endswith('.prefab'):
            continue
        p = os.path.join(dp, f)
        try:
            t = open(p, encoding='utf-8', errors='ignore').read()
        except OSError:
            continue
        if PC_GUID not in t:
            continue
        # 按文档块切分，只取含该脚本的块（到下一个 '--- !u!' 为止）
        for b in t.split('--- !u!')[1:]:
            if PC_GUID not in b:
                continue
            total += 1
            miss = [k for k in KEYS if not re.search(r'^  ' + re.escape(k), b, re.M)]
            if miss:
                bad.append((p.replace(ROOT, ''), miss))

print('PartController 块:', total, ' 缺字段:', len(bad))
for x in bad:
    print(x)
