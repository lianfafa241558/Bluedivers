# -*- coding: utf-8 -*-
"""扫描所有 .prefab 的 Upgrade 列表：报告空引用(fileID: 0)与工程里不存在的 guid。"""
import os, re, sys, glob

ROOT = r'd:\Pro\Bluedivers'

# 1. 收集工程内所有 guid -> 资产路径
guid2path = {}
for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    for f in fn:
        if f.endswith('.meta'):
            p = os.path.join(dp, f)
            try:
                txt = open(p, 'r', encoding='utf-8', errors='ignore').read(400)
            except Exception:
                continue
            m = re.search(r'guid:\s*([0-9a-f]{32})', txt)
            if m:
                guid2path[m.group(1)] = p[:-5]

print('工程 guid 数:', len(guid2path))

bad = []
for dp, dn, fn in os.walk(os.path.join(ROOT, 'Assets')):
    if 'Library' in dp or 'Temp' in dp:
        continue
    for f in fn:
        if not f.endswith('.prefab'):
            continue
        p = os.path.join(dp, f)
        try:
            lines = open(p, 'r', encoding='utf-8', errors='ignore').read().splitlines()
        except Exception:
            continue
        # 找 "  Upgrade:" 块，缩进 2 的字段；块内直到缩进<=2 的下一字段
        inblk = False
        blk_name = ''
        for i, ln in enumerate(lines):
            if re.match(r'^  \w+:', ln):
                inblk = ln.strip().startswith('Upgrade:')
                blk_name = ln.strip()
            if not inblk:
                continue
            for m in re.finditer(r'\{fileID:\s*(\d+)(?:,\s*guid:\s*([0-9a-f]{32}))?', ln):
                fid, guid = m.group(1), m.group(2)
                if fid == '0' and not guid:
                    bad.append((p, i + 1, 'fileID:0 空引用', ln.strip()[:80]))
                elif guid and guid not in guid2path:
                    bad.append((p, i + 1, 'guid 不存在:' + guid, ln.strip()[:80]))

print('可疑条目:', len(bad))
for b in bad:
    print(' %s:%d  %s' % (b[0].replace(ROOT + '\\', ''), b[1], b[2]))
