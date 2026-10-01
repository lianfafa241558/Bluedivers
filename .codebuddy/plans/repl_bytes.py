# -*- coding: utf-8 -*-
"""按 JSON 清单做字节级精确替换（保 BOM），每项要求精确出现 count 次。
用法: python repl_bytes.py <repls.json> [--apply]
      repls.json: [[relpath, old, new, expect_count], ...]  （第 4 项省略时=1）
"""
import os
import sys
import json

ROOT = r'd:\Pro\Bluedivers'
APPLY = '--apply' in sys.argv
repls = json.load(open(sys.argv[1], encoding='utf-8'))
bad = 0
for item in repls:
    rel, old, new = item[0], item[1], item[2]
    expect = item[3] if len(item) > 3 else 1
    p = os.path.join(ROOT, rel.replace('/', os.sep))
    b = open(p, 'rb').read()
    n = b.count(old.encode())
    status = 'OK' if n == expect else '!! 出现 %d 次(期望 %d)' % (n, expect)
    print('  [%s] %s : %r -> %r' % (status, rel, old, new))
    if n != expect:
        bad += 1
        continue
    if APPLY:
        open(p, 'wb').write(b.replace(old.encode(), new.encode()))
print('APPLY=%s  异常项=%d' % (APPLY, bad))
