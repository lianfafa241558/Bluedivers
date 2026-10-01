# -*- coding: utf-8 -*-
"""步骤①复核：8 文件 ns、残留 FPSGame.Furn、Furn 类型引用者的命名空间可访问性。"""
import os, re

ROOT = r'd:\Pro\Bluedivers'
A = os.path.join(ROOT, 'Assets')
EIGHT = [
    'Assets/Scripts/06Gameplay/Interactable/FurnitureContract.cs',
    'Assets/Scripts/06Gameplay/Interactable/Furniture_Attached.cs',
    'Assets/Scripts/06Gameplay/Interactable/Furniture_AttachedGeneral.cs',
    'Assets/Scripts/06Gameplay/Interactable/Furniture_Equip.cs',
    'Assets/Scripts/06Gameplay/Interactable/Furniture_PlayerDown.cs',
    'Assets/Scripts/06Gameplay/Interactable/Furniture_ReturnBag.cs',
    'Assets/Scripts/06Gameplay/Interactable/Furniture_Supplies.cs',
    'Assets/Scripts/06Gameplay/Interactable/Furniture_WeaponPickup.cs',
]
FURN_TYPES = ['IFurniture', 'FurnitureFlag', 'Furniture_Attached', 'Furniture_AttachedGeneral',
              'Furniture_Equip', 'Furniture_PlayerDown', 'Furniture_ReturnBag', 'Furniture_Supplies',
              'Furniture_WeaponPickup']

print('== 8 文件 ns / BOM ==')
for p in EIGHT:
    b = open(os.path.join(ROOT, p), 'rb').read()
    bom = b[:3] == b'\xef\xbb\xbf'
    m = re.search(rb'namespace\s+([\w\.]+)', b)
    print('  %-22s %s  %s' % ((m.group(1).decode() if m else '(none)'), 'BOM' if bom else '   ', p.split('/')[-1]))

resid, bad = [], []
for r, d, fs in os.walk(A):
    for f in fs:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(r, f)
        b = open(p, 'rb').read()
        if b'FPSGame.Furn' in b:
            resid.append(p.replace(ROOT, '').replace('\\', '/'))
        t = b.decode('utf-8-sig', 'replace')
        hit = [x for x in FURN_TYPES if re.search(r'\b' + x + r'\b', t)]
        if not hit:
            continue
        ns = re.search(r'namespace\s+([\w\.]+)', t)
        ns = ns.group(1) if ns else ''
        if ns != 'FPSGame.Gameplay' and not re.search(r'using\s+FPSGame\.Gameplay\s*;', t):
            bad.append((p.replace(ROOT, '').replace('\\', '/'), ns, hit))

print('\n== 残留 FPSGame.Furn 的文件 ==', len(resid))
for x in resid:
    print('  ', x)

print('\n== 引用 Furn 类型但缺 Gameplay 访问（应为 0）==', len(bad))
for x in bad:
    print('  ', x[0], 'ns=', x[1], x[2][:3])
