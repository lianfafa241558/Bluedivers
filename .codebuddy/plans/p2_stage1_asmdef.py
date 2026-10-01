# -*- coding: utf-8 -*-
"""阶段1：把 06_Gameplay.asmdef 的 rootNamespace 从 "" 补为 "FPSGame.Gameplay"。
字节级修改，保留 BOM/换行；改后自校验。"""
import sys

p = r'd:\Pro\Bluedivers\Assets\Scripts\06Gameplay\06_Gameplay.asmdef'
b = open(p, 'rb').read()
bom = b[:3] == b'\xef\xbb\xbf'
old = b'"rootNamespace": "",'
new = b'"rootNamespace": "FPSGame.Gameplay",'
assert b.count(old) == 1, 'rootNamespace 空串出现 %d 次' % b.count(old)
assert new not in b, 'target 已存在'
nb = b.replace(old, new, 1)
open(p, 'wb').write(nb)

c = open(p, 'rb').read()
print('BOM before/after:', bom, c[:3] == b'\xef\xbb\xbf')
print('CRLF:', b'\r\n' in c)
print('bytes:', len(b), '->', len(c))
print(c.decode('utf-8'))
