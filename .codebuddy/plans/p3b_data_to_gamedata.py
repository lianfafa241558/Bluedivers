# -*- coding: utf-8 -*-
"""阶段3·②：06Gameplay/Data/ 6 文件 `FPSGame.Game` -> `FPSGame.GameData`，并同步 using。
- 6 文件改 namespace；
- 所有真实引用 Data 类型的文件补 `using FPSGame.GameData;`（剥注释后判定）；
- 6 文件中仍引用「留守 FPSGame.Game 的类型」的补 `using FPSGame.Game;`。
字节级读写保 BOM/换行；普通 using 插在最后一条普通 using 之后（不越到 using static/别名之后）。
用法： python p3b_data_to_gamedata.py [--apply]
"""
import os
import re
import io
import sys
import collections

ROOT = r'd:\Pro\Bluedivers'
A = os.path.join(ROOT, 'Assets')
D = 'Assets/Scripts/06Gameplay/Data/'
SIX = ['ArchivesData_SO.cs', 'MissionData_SO.cs', 'MissionMainData_SO.cs',
       'RoleData_SO.cs', 'WeaponModuleData_SO.cs', 'WeaponUpgradeData_SO.cs']
APPLY = '--apply' in sys.argv

DECL = re.compile(r'\b(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|struct|interface|enum)\s+(\w+)')
NSD = re.compile(r'namespace\s+([\w\.]+)')
USE_ORD = re.compile(rb'(?m)^[ \t]*using\s+(?!static\b)[A-Za-z_][\w\.]*\s*;')
HAS_GD = re.compile(rb'(?m)^[ \t]*using\s+FPSGame\.GameData\s*;')
HAS_G = re.compile(rb'(?m)^[ \t]*using\s+FPSGame\.Game\s*;')


def readp(p):
    return open(os.path.join(ROOT, p.replace('/', os.sep)), 'rb').read()


def text(p):
    return io.open(os.path.join(ROOT, p.replace('/', os.sep)), encoding='utf-8-sig', errors='replace').read()


def strip(t):
    t = re.sub(r'/\*.*?\*/', ' ', t, flags=re.S)
    t = re.sub(r'//[^\n]*', ' ', t)
    t = re.sub(r'"(\\.|[^"\\])*"', '""', t)
    t = re.sub(r"'(\\.|[^'\\])*'", "''", t)
    return t


def add_using(b, ns):
    bom = b[:3] if b[:3] == b'\xef\xbb\xbf' else b''
    body = b[len(bom):]
    nl = b'\r\n' if b'\r\n' in body else b'\n'
    line = b'using ' + ns.encode() + b';' + nl
    us = list(USE_ORD.finditer(body))
    if us:
        pos = us[-1].end()
        if body[pos:pos + len(nl)] == nl:
            return bom + body[:pos + len(nl)] + line + body[pos + len(nl):]
        return bom + body[:pos] + nl + line + body[pos:]
    m = re.search(rb'(?m)^[ \t]*namespace\b', body)
    if m:
        return bom + body[:m.start()] + line + body[m.start():]
    return bom + line + body


# 1) Data 类型 + 留守 FPSGame.Game 类型
data_types = set()
for f in SIX:
    data_types |= set(DECL.findall(text(D + f)))

game_types = {}
type_ns = collections.defaultdict(set)
for r, d, fs in os.walk(os.path.join(A, 'Scripts')):
    for f in fs:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(r, f)
        rel = os.path.relpath(p, ROOT).replace('\\', '/')
        t = io.open(p, encoding='utf-8-sig', errors='replace').read()
        m = NSD.search(strip(t))
        ns = m.group(1) if m else ''
        for n in set(DECL.findall(t)):
            type_ns[n].add(ns)
        if ns == 'FPSGame.Game' and rel not in [D + x for x in SIX]:
            for n in DECL.findall(t):
                game_types[n] = rel

# 2) 歧义预警
amb = {n: sorted(s) for n, s in type_ns.items() if n in data_types and len(s) > 1}
print('== Data 类型重名预警（同名存在于多个命名空间）==')
for n, s in sorted(amb.items()):
    print('  %-24s %s' % (n, s))
if not amb:
    print('  无')

# 3) 找引用者（剥注释）
six_set = set(D + f for f in SIX)
referrers = []
for r, d, fs in os.walk(A):
    for f in fs:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(r, f)
        rel = os.path.relpath(p, ROOT).replace('\\', '/')
        if rel in six_set:
            continue
        code = strip(io.open(p, encoding='utf-8-sig', errors='replace').read())
        if any(re.search(r'\b' + x + r'\b', code) for x in data_types):
            referrers.append(rel)

# 4) 6 文件中需回补 using FPSGame.Game; 的
back = []
for f in SIX:
    code = strip(text(D + f))
    if not HAS_G.search(readp(D + f)) and any(re.search(r'\b' + x + r'\b', code) for x in game_types):
        back.append(D + f)

print('\n引用者数:', len(referrers), '（需补 using FPSGame.GameData;）')
print('Data 文件回补 using FPSGame.Game; 的:', len(back))
for x in back:
    print('   ', x)

plan = {}
for f in SIX:
    b = readp(D + f)
    nb, n = re.subn(rb'namespace\s+FPSGame\.Game\b', b'namespace FPSGame.GameData', b, count=1)
    if n != 1:
        print('!! 改 ns 失败', f)
        continue
    plan[D + f] = nb
for rel in referrers:
    b = plan.get(rel) or readp(rel)
    if HAS_GD.search(b):
        continue
    plan[rel] = add_using(b, 'FPSGame.GameData')
for rel in back:
    b = plan.get(rel) or readp(rel)
    if HAS_G.search(b):
        continue
    plan[rel] = add_using(b, 'FPSGame.Game')

print('\n共改动 %d 个文件' % len(plan))
if APPLY:
    for p, b in plan.items():
        open(os.path.join(ROOT, p.replace('/', os.sep)), 'wb').write(b)
    print('已写入。')
else:
    print('（未写入，加 --apply 落盘）')
