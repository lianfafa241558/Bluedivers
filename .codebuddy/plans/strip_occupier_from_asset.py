# -*- coding: utf-8 -*-
"""从 Archive_Default.asset 移除已废弃的 occupierDic 段。

势力占领度已拆到独立世界状态（OccupierState + OccupierState.json）⇒ 存档资产里不应再留一份，
否则以后策划改这里毫无效果（字段已不存在），属于数据双写。

字节级读写，保留原 BOM / 行尾；只删「  occupierDic:」到下一个 2 空格缩进字段之前。
"""
import sys
from pathlib import Path

ASSET = Path(r'd:/Pro/Bluedivers/Assets/Resources/GameData/Archive_Default.asset')
BOM = b'\xef\xbb\xbf'

raw = ASSET.read_bytes()
has_bom = raw.startswith(BOM)
body = raw[3:] if has_bom else raw
text = body.decode('utf-8')
lines = text.splitlines(keepends=True)

start = None
for i, l in enumerate(lines):
    if l.strip() == 'occupierDic:' and l.startswith('  ') and not l.startswith('   '):
        start = i
        break
if start is None:
    print('[skip] 没找到 occupierDic 段（可能已被 Unity 清掉）')
    sys.exit(0)

end = None
for j in range(start + 1, len(lines)):
    s = lines[j]
    if s.strip() and s.startswith('  ') and not s.startswith('   '):
        end = j
        break
if end is None:
    print('[abort] 找不到结束边界，未修改')
    sys.exit(1)

removed = ''.join(lines[start:end])
out = ''.join(lines[:start] + lines[end:])
ASSET.write_bytes((BOM if has_bom else b'') + out.encode('utf-8'))

print(f'[ok] 删除第 {start + 1}..{end} 行，共 {end - start} 行')
print(f'     BOM={has_bom}  剩余行数={len(lines) - (end - start)}')
print('     下一段:', lines[end].rstrip())
print('     被删首行:', removed.splitlines()[0])
print('     被删末行:', removed.splitlines()[-1])
