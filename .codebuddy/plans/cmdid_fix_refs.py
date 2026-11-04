# -*- coding: utf-8 -*-
"""按行定点修正：把"其实还在用的号"从历史号改成现在真正承载它的号。

背景：机械映射会把"原 XXX"（历史）和"上报 XXX"（现役）一起换，
后者必须指到合并后的新号（载具上下机 -> 4059 FurnitureOperate；生成族复刻 -> 4063 HostSpawn）。
"""
import os

ROOT = r'e:\Bluedivers\Assets\Scripts'

ITEMS = [
    ('09Manager/Battle/NetVehicleBridge.cs', 317, '它回出来的那条 4093', '它回出来的那条 4059'),
    ('09Manager/Battle/NetVehicleBridge.cs', 336, '走法同 4093：', '走法同 4059：'),
    ('09Manager/Battle/NetVehicleBridge.cs', 711, '回包就是 4093 ', '回包就是 4059 '),
    ('09Manager/Battle/NetVehicleBridge.cs', 718, '上报 4093。', '上报 4059。'),
    ('07NetGame/NetRoomFlow.cs', 792, '与 4093 / 4059 完全同款', '与载具上下机（同走 4059）完全同款'),
    ('06Gameplay/Player/Controller/VehicleController.cs', 200, '上报 4093', '上报 4059'),
    ('06Gameplay/Player/Controller/FriendController.cs', 283, '（联机 4093）', '（联机 4059）'),
    ('06Gameplay/Player/Controller/EmplacementController.cs', 117, '上报 4093（上下机）', '上报 4059（上下机）'),
    ('06Gameplay/Player/Controller/EmplacementController.cs', 131, '上报 4093 的唯一来源', '上报 4059 的唯一来源'),
    ('06Gameplay/Interactable/Furniture_Vehicle.cs', 78, '上报 4093 的唯一来源', '上报 4059 的唯一来源'),
    ('06Gameplay/Interactable/Furniture_Vehicle.cs', 143, '= 4093 上报的唯一来源', '= 4059 上报的唯一来源'),
    ('06Gameplay/Events/BattleEventBus.cs', 190, '上报 4093', '上报 4059'),
    ('05UnitCore/Actor/NetIdProbe.cs', 112, '含 4092 复刻后覆盖', '含 4063 复刻后覆盖'),
    ('05UnitCore/Actor/NetIdProbe.cs', 416, '没复刻出 4092 那一只', '没复刻出 4063 那一只'),
    ('05UnitCore/Actor/Actor.cs', 63, '复刻（4092）', '复刻（4063）'),
]

for rel, lineno, old, new in ITEMS:
    path = os.path.join(ROOT, rel.replace('/', os.sep))
    raw = open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    lines = text.split('\n')
    if old not in lines[lineno - 1]:
        print('MISS %s:%d  (line=%s)' % (rel, lineno, lines[lineno - 1].strip()[:80]))
        continue
    lines[lineno - 1] = lines[lineno - 1].replace(old, new)
    out = '\n'.join(lines).encode('utf-8')
    if bom:
        out = b'\xef\xbb\xbf' + out
    with open(path, 'wb') as f:
        f.write(out)
        f.flush()
        os.fsync(f.fileno())
    print('OK   %s:%d' % (rel, lineno))
