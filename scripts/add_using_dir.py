# -*- coding: utf-8 -*-
"""给指定目录下、处于指定命名空间的 .cs 批量补一条 using（行尾安全）。用完即删。"""
import io
import os
import sys

DIRP = sys.argv[1]
NS_IN = sys.argv[2]      # 只处理声明了该命名空间的文件
NS_ADD = sys.argv[3]     # 要补的 using

for f in sorted(os.listdir(DIRP)):
    if not f.endswith('.cs'):
        continue
    p = os.path.join(DIRP, f)
    t = io.open(p, encoding='utf-8-sig', newline='').read()
    if ('namespace ' + NS_IN) not in t:
        continue
    if ('using ' + NS_ADD + ';') in t:
        continue
    nl = '\r\n' if '\r\n' in t else '\n'
    lines = t.split(nl)
    idx = 0
    for k, ln in enumerate(lines):
        s = ln.rstrip()
        if s.startswith('using ') and s.endswith(';') and '=' not in s:
            idx = k + 1
        elif s.strip() == '' or s.startswith('//'):
            continue
        else:
            break
    lines = lines[:idx] + ['using ' + NS_ADD + ';'] + lines[idx:]
    io.open(p, 'w', encoding='utf-8-sig', newline='').write(nl.join(lines))
    print('  added to ' + f)
