# -*- coding: utf-8 -*-
"""
按 asmdef 引用关系，剪掉"不该补"的 using（避免下层引用上层导致 CS0234）。
判断：消费文件向上找最近的 .asmdef，若其 references 不含目标程序集（同时支持
     名字形式与 "GUID:<guid>" 形式），则说明该程序集看不到目标 ⇒ 删掉这个 using。
用法：python prune_using.py <命名空间> <目标程序集名> [--apply]
"""
import io
import json
import os
import re
import sys

ROOT = r'D:\Pro\Bluedivers\Assets'
NS = sys.argv[1]
TARGET_ASM = sys.argv[2]
APPLY = '--apply' in sys.argv


def nearest_asmdef(p):
    d = os.path.dirname(p)
    while len(d) > len(ROOT):
        cands = [f for f in sorted(os.listdir(d)) if f.endswith('.asmdef')]
        if cands:
            return os.path.join(d, cands[0])
        d = os.path.dirname(d)
    return None


def asm_guid(asmdef_path):
    meta = asmdef_path + '.meta'
    if not os.path.exists(meta):
        return None
    m = re.search(r'guid:\s*([0-9a-f]{32})', io.open(meta, encoding='utf-8-sig', errors='replace').read())
    return m.group(1) if m else None


# 目标程序集的 guid（用于识别 "GUID:xxx" 形式的引用）
# ⚠ 必须从「本批的目标目录」推导，不能硬编码 —— 之前硬编码成 01Manager 导致 guid 取错
def asmdef_of_dir(d):
    while len(d) > len(ROOT):
        cands = [f for f in sorted(os.listdir(d)) if f.endswith('.asmdef')]
        if cands:
            return os.path.join(d, cands[0])
        d = os.path.dirname(d)
    return None


TARGET_DIR = sys.argv[3] if len(sys.argv) > 3 else 'Scripts'
t_asmdef = asmdef_of_dir(os.path.abspath(os.path.join(ROOT, TARGET_DIR)))
t_guid = asm_guid(t_asmdef) if t_asmdef else None
print('目标程序集 %s (asmdef=%s guid=%s)'
      % (TARGET_ASM, os.path.relpath(t_asmdef, ROOT) if t_asmdef else '?', t_guid))

bad = []
seen = 0
TARGET_ABS = os.path.abspath(os.path.join(ROOT, TARGET_DIR))
for dp, dn, fn in os.walk(os.path.join(ROOT, 'Scripts')):
    if os.path.abspath(dp).startswith(TARGET_ABS):
        continue  # 目标程序集自己的文件：原本就有的同类 using 不能删（本次在 05UnitCore 上踩到）
    for f in fn:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(dp, f)
        t = io.open(p, encoding='utf-8-sig', errors='replace').read()
        if ('using ' + NS + ';') not in t:
            continue
        seen += 1
        a = nearest_asmdef(p)
        refs = []
        if a:
            try:
                refs = json.load(io.open(a, encoding='utf-8-sig')).get('references', [])
            except Exception:
                refs = []
        # ⚠ 同程序集内跨命名空间引用是合法的，asmdef 不会引用自己 ⇒ 必须放行，
        #   否则会把「原本就有的」同程序集 using 误删（本批在 06Gameplay 上踩到，误删 26 个）
        same_asm = a and os.path.abspath(a).startswith(os.path.abspath(t_asmdef or '')) and a == t_asmdef
        if a and t_asmdef and (os.path.normcase(os.path.abspath(a)) == os.path.normcase(os.path.abspath(t_asmdef))):
            same_asm = True
        ok = same_asm or (TARGET_ASM in refs) or (t_guid and ('GUID:' + t_guid) in refs)
        if not ok:
            bad.append(p)
            print('  REMOVE  asm=%-14s %s' % (os.path.relpath(a, ROOT) if a else '?', os.path.relpath(p, ROOT)))

print()
print('检查了 %d 个带该 using 的文件，需撤销 %d 个' % (seen, len(bad)))

if APPLY:
    for p in bad:
        t = io.open(p, encoding='utf-8-sig', newline='').read()
        nl = '\r\n' if '\r\n' in t else '\n'
        lines = [ln for ln in t.split(nl) if ln.strip() != 'using ' + NS + ';']
        io.open(p, 'w', encoding='utf-8-sig', newline='').write(nl.join(lines))
    print('  -> 已撤销')
