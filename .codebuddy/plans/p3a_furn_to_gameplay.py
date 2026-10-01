# -*- coding: utf-8 -*-
"""阶段3·①：把 Interactable/ 的 8 个 `FPSGame.Furn` 并入 `FPSGame.Gameplay`，并同步所有 using。
- 8 文件：namespace FPSGame.Furn -> FPSGame.Gameplay
- 所有 `using FPSGame.Furn;`：若改后处于 Gameplay 域 或 已有 Gameplay using ⇒ 删除该行；否则改写成 using FPSGame.Gameplay;
字节级读写，保留 BOM/换行。
用法： python p3a_furn_to_gameplay.py [--apply]
"""
import os
import re
import sys

ROOT = r'd:\Pro\Bluedivers'
A = os.path.join(ROOT, 'Assets')
APPLY = '--apply' in sys.argv

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

NSPAT = re.compile(rb'namespace\s+FPSGame\.Furn\b')
ULINE = re.compile(rb'(?m)^([ \t]*)using\s+FPSGame\.Furn\s*;(\r?\n)')
GPUSE = re.compile(rb'(?m)^[ \t]*using\s+FPSGame\.Gameplay\s*;')
NSDECL = re.compile(rb'namespace\s+([\w\.]+)')


def ns_of(b):
    m = NSDECL.search(b)
    return m.group(1).decode('ascii') if m else ''


# 载入全部 .cs
files = {}
for dirpath, _d, fns in os.walk(A):
    for fn in fns:
        if fn.endswith('.cs'):
            fp = os.path.join(dirpath, fn)
            rel = os.path.relpath(fp, ROOT).replace('\\', '/')
            files[rel] = open(fp, 'rb').read()

plan = {}

# 1) 8 个改 ns
for p in EIGHT:
    if p not in files:
        print('!! 缺失文件', p)
        continue
    b = files[p]
    nb, n = NSPAT.subn(b'namespace FPSGame.Gameplay', b, count=1)
    if n != 1:
        print('!! 未找到 namespace FPSGame.Furn :', p)
        continue
    files[p] = nb
    plan[p] = ['ns']

# 2) using 处理（剥离 BOM 后按行匹配，最后还原 BOM，避免行首 BOM 挡住 ^ 锚点）
for p, b in list(files.items()):
    bom = b[:3] if b[:3] == b'\xef\xbb\xbf' else b''
    body = b[len(bom):]
    if not ULINE.search(body):
        continue
    post = 'FPSGame.Gameplay' if p in EIGHT else ns_of(body)
    if post == 'FPSGame.Gameplay' or GPUSE.search(body):
        nb = ULINE.sub(b'', body)
        mode = 'del-using'
    else:
        nb = ULINE.sub(rb'\1using FPSGame.Gameplay;\2', body)
        mode = '->Gameplay'
    files[p] = bom + nb
    plan.setdefault(p, []).append(mode)

print('将改动 %d 个文件：' % len(plan))
for p in sorted(plan):
    print('  [%s] %s' % (','.join(plan[p]), p))

# 校验：改后是否仍残留 FPSGame.Furn
resid = []
for p, b in files.items():
    if b'FPSGame.Furn' in b:
        resid.append(p)
print('\n残留 FPSGame.Furn 的文件：', len(resid))
for p in resid[:30]:
    print('  ', p)

if APPLY:
    n = 0
    for p in plan:
        open(os.path.join(ROOT, p.replace('/', os.sep)), 'wb').write(files[p])
        n += 1
    print('\n已写入 %d 个文件' % n)
else:
    print('\n（未写入，加 --apply 落盘）')
