# -*- coding: utf-8 -*-
"""把 Small_RL 下所有 .anim 曲线 path 的根节点前缀 "Droid_Yoheki_RL/" 去掉。
- 字节级替换，保留原换行符(LF)与 BOM 状态
- 只改 "path: Droid_Yoheki_RL/" 这一种形态（已全量校验：3399/3399）
"""
import os
import sys

ANIM_DIR = r'd:\Pro\Bluedivers\Assets\Art\Modle\Enemy\Robot\NewRobot\Anim\Small_RL'
OLD = b'path: Droid_Yoheki_RL/'
NEW = b'path: '

DRY = '--apply' not in sys.argv

total = 0
for name in sorted(os.listdir(ANIM_DIR)):
    if not name.endswith('.anim'):
        continue
    path = os.path.join(ANIM_DIR, name)
    with open(path, 'rb') as f:
        data = f.read()
    hit = data.count(OLD)
    if hit == 0:
        print('[skip] %s' % name)
        continue
    out = data.replace(OLD, NEW)
    # 自检：替换后不应再残留任何 Droid_Yoheki
    left = out.count(b'Droid_Yoheki')
    if left:
        raise SystemExit('残留 %r 于 %s，已中止' % (left, name))
    if not DRY:
        with open(path, 'wb') as f:
            f.write(out)
    total += hit
    print('[%s] %-32s %d' % ('done' if not DRY else 'dry ', name, hit))

print('total curves patched: %d  (%s)' % (total, '已写入' if not DRY else '预演，加 --apply 执行'))
