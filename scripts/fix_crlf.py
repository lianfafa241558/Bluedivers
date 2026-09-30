# -*- coding: utf-8 -*-
"""
修复被 ns_batch.py 破坏的行尾：\r\r\n -> \r\n
（成因：用 split('\n') 拆行后每行仍带 \r，再用 '\r\n' join 就变成 \r\r\n）
纯字节级替换，不影响内容。用完即删。
"""
import os

ROOT = r'D:\Pro\Bluedivers\Assets'
n = 0
files = []
for dp, dn, fn in os.walk(ROOT):
    for f in fn:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(dp, f)
        with open(p, 'rb') as fh:
            b = fh.read()
        if b'\r\r\n' in b:
            with open(p, 'wb') as fh:
                fh.write(b.replace(b'\r\r\n', b'\r\n'))
            n += 1
            files.append(os.path.relpath(p, ROOT))
print('FIXED FILES = %d' % n)
for x in sorted(files):
    print('  ' + x)
