# -*- coding: utf-8 -*-
import io, re, sys

p = sys.argv[1]
s = io.open(p, encoding='utf-8').read()
# 每个 curve 块: "- curve:\n ... attribute: X\n path: Y\n classID: N\n"
blocks = re.split(r'\n  - curve:', s)
for b in blocks[1:]:
    m = re.search(r'attribute: (\S+)\n\s+path: (.*?)\n\s+classID: (-?\d+)', b)
    if not m:
        continue
    attr, path, cls = m.group(1), m.group(2), m.group(3)
    vals = re.findall(r'value: (.*)', b)
    times = re.findall(r'time: (.*)', b)
    # 只打印 alpha / 关键属性
    if attr in ('m_Alpha', 'm_Enabled', 'm_IsActive'):
        print('%-12s path="%s" cls=%s' % (attr, path, cls))
        for t, v in zip(times, vals):
            print('      t=%-12s v=%s' % (t.strip(), v.strip()))
