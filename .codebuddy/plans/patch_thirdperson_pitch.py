# -*- coding: utf-8 -*-
"""把 Player.prefab 上已序列化的 _thirdPersonUpperLimit 从 80 改成 25（只改这一行）。
⚠ 不要用 MCP 存 prefab（会按内存旧值回写、抹掉没碰的字段）。"""
import io

P = 'Assets/Resources/Prefabs/BattleBase/Player.prefab'

with io.open(P, 'r', encoding='utf-8', newline='') as f:
    s = f.read()

old = '_thirdPersonUpperLimit: 80'
new = '_thirdPersonUpperLimit: 25'
n = s.count(old)
print('命中次数:', n)
if n != 1:
    raise SystemExit('命中次数不为 1，放弃修改')

s2 = s.replace(old, new)
with io.open(P, 'w', encoding='utf-8', newline='') as f:
    f.write(s2)
print('已写入:', P)
