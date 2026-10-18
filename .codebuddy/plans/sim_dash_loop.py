# -*- coding: utf-8 -*-
"""用 float32 语义复现旧的虚线自增循环，找出会卡住的 (phase, len) 组合。"""
import random
import numpy as np

f32 = np.float32


def old_loop(phase, length, dash=21.0, cycle=28.0, limit=200000):
    walked = f32(0.0)
    length = f32(length)
    phase = f32(phase)
    for it in range(limit):
        if not (walked < length):
            return it
        t = f32(f32(phase + walked) % f32(cycle))
        if t >= f32(dash):
            step = max(f32(0.001), f32(f32(cycle) - t))
            walked = f32(walked + step)
            continue
        piece = min(f32(f32(dash) - t), f32(length - walked))
        walked = f32(walked + piece)
    return None   # 卡住


random.seed(7)
worst = (0, None)
stalls = []
for _ in range(200000):
    phase = round(random.uniform(0, 1100), random.choice([0, 1, 2, 3, 6]))
    length = f32(round(random.uniform(0.01, 40), random.choice([1, 2, 3, 6])))
    it = old_loop(phase, length)
    if it is None:
        stalls.append((phase, float(length)))
        if len(stalls) > 5:
            break
    elif it > worst[0]:
        worst = (it, (phase, float(length)))

print('stalls:', stalls[:6])
print('worst iterations:', worst)

# 再针对性扫描：让实线终点刚好落在浮点边界附近
print('--- boundary scan ---')
bad = []
for k in range(0, 60):
    for eps_exp in range(-9, -3):
        for sign in (1, -1):
            phase = f32(k * 28.0) + f32(sign * 10 ** eps_exp)
            it = old_loop(float(phase), 30.0)
            if it is None:
                bad.append((float(phase), sign, eps_exp))
print('boundary stalls:', bad[:6], 'count', len(bad))
