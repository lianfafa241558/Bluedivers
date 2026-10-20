# -*- coding: utf-8 -*-
"""从 Player.log 抽取：NullReferenceException 上下文 + 盟友实例/武器槽相关行。"""
import io
import re

LOG = r'C:\Users\admin\AppData\LocalLow\GameDevelopmentDepartmentFaFa\BlueDivers\Player.log'
OUT = r'e:\Bluedivers\.codebuddy\plans\player_log_digest.txt'

lines = io.open(LOG, encoding='utf-8', errors='replace').read().splitlines()
out = []
out.append('总行数=%d' % len(lines))

# 1) 异常/报错（含堆栈）
idx = [i for i, l in enumerate(lines) if 'Exception' in l or 'NullReference' in l]
out.append('\n=== 异常行 %d 处 ===' % len(idx))
for i in idx[:6]:
    out.append('--- line %d ---' % i)
    out.extend(lines[i:i + 18])

# 2) 盟友实例 / 武器槽 / 自愈
pat = re.compile(r'创建盟友实例|移除盟友实例|盟友自愈|绑本机武器|槽=|切枪')
hits = [l for l in lines if pat.search(l)]
out.append('\n=== 盟友/武器槽相关 %d 行（前 60） ===' % len(hits))
out.extend(hits[:60])

# 3) Health 生命值>0 / 警告
warn = [l for l in lines if l.startswith('警告') or ' 警告' in l or 'tier' in l]
out.append('\n=== 警告 %d 行（前 30） ===' % len(warn))
out.extend(warn[:30])

io.open(OUT, 'w', encoding='utf-8').write('\n'.join(out))
print('ok', len(out))
