# -*- coding: utf-8 -*-
"""步骤②侦察：剥掉注释/字符串后，精确找出真正引用 Data 目录 6 文件所声明类型的文件。"""
import os, re, io

ROOT = r'd:\Pro\Bluedivers'
A = os.path.join(ROOT, 'Assets')
D = 'Assets/Scripts/06Gameplay/Data/'
SIX = ['ArchivesData_SO.cs', 'MissionData_SO.cs', 'MissionMainData_SO.cs',
       'RoleData_SO.cs', 'WeaponModuleData_SO.cs', 'WeaponUpgradeData_SO.cs']
DECL = re.compile(r'\b(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|struct|interface|enum)\s+(\w+)')
NSDECL = re.compile(r'namespace\s+([\w\.]+)')
GUSE = re.compile(r'using\s+FPSGame\.Game\s*;')
GDUSE = re.compile(r'using\s+FPSGame\.GameData\s*;')


def strip_code(t):
    t = re.sub(r'/\*.*?\*/', ' ', t, flags=re.S)
    t = re.sub(r'//[^\n]*', ' ', t)
    t = re.sub(r'"(\\.|[^"\\])*"', '""', t)
    t = re.sub(r"'(\\.|[^'\\])*'", "''", t)
    return t


allt = set()
for f in SIX:
    allt |= set(DECL.findall(io.open(os.path.join(ROOT, D + f), encoding='utf-8-sig', errors='replace').read()))
print('6 文件声明类型共 %d 个：%s' % (len(allt), sorted(allt)))

six_set = set(D + f for f in SIX)
rows = []
for r, d, fs in os.walk(A):
    for f in fs:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(r, f)
        rel = os.path.relpath(p, ROOT).replace('\\', '/')
        if rel in six_set:
            continue
        raw = io.open(p, encoding='utf-8-sig', errors='replace').read()
        code = strip_code(raw)
        hit = [x for x in allt if re.search(r'\b' + x + r'\b', code)]
        if not hit:
            continue
        ns = NSDECL.search(code)
        ns = ns.group(1) if ns else ''
        rows.append((rel, ns, bool(GUSE.search(code)), bool(GDUSE.search(code)), hit))

print('\n真实引用者 %d 个：' % len(rows))
need, ok, same = [], [], []
for rel, ns, hg, hgd, hit in sorted(rows):
    tag = ('SAME' if ns == 'FPSGame.Game' else ('HASGD' if hgd else ('GUSE' if hg else '!!NO-USE')))
    print('  [%-7s] ns=%-20s %-72s %s' % (tag, ns, rel, hit[:4]))
    if hgd:
        ok.append(rel)
    elif hg or ns == 'FPSGame.Game':
        need.append(rel)
    else:
        same.append((rel, ns, hit))

print('\n需补 using FPSGame.GameData; 的：%d' % len(need))
print('已有 GameData using：%d' % len(ok))
if same:
    print('\n!! 既无 Game 也无 GameData using（需人工确认）：')
    for rel, ns, hit in same:
        print('   ns=%-20s %-70s %s' % (ns, rel, hit))
