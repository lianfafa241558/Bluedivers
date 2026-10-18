# -*- coding: utf-8 -*-
"""分析 Editor-prev.log 尾部：找出卡死前最后的日志消息与刷屏源。"""
import io
import os
import collections

p = os.path.join(os.environ['LOCALAPPDATA'], 'Unity', 'Editor', 'Editor-prev.log')
raw = open(p, 'rb').read()
for enc in ('utf-8', 'gbk', 'latin-1'):
    try:
        text = raw.decode(enc)
        break
    except Exception:
        continue
lines = text.splitlines()

def is_msg(l):
    s = l.strip()
    if not s:
        return False
    if s.startswith('0x'):
        return False
    if s.startswith('(Filename') or s.startswith('UnityEngine.') or s.startswith('System.'):
        return False
    return True

msgs = [l.strip() for l in lines if is_msg(l)]
print('total lines', len(lines), 'msg lines', len(msgs))
print('--- last 25 msgs ---')
for m in msgs[-25:]:
    out = m.encode('utf-8', 'replace').decode('utf-8')
    print(out[:180])
print('--- top repeated (last 3000 msgs) ---')
for c, m in collections.Counter(msgs[-3000:]).most_common(8):
    print(c, m[:160])
