# -*- coding: utf-8 -*-
"""
无 namespace 缺口盘点（只读）：
  - 范围限死 Assets/Scripts（Assets/Plugins 下的第三方插件不碰）
  - 按"两级目录"分组，列出每组文件数与其中声明的类型名
  - 顺带报告该组若归入某目标命名空间是否与"已存在同名类型"冲突
剥注释避免假阳性。用完即删。
"""
import collections
import io
import os
import re

SCRIPTS = r'D:\Pro\Bluedivers\Assets\Scripts'
ALL = r'D:\Pro\Bluedivers\Assets'
TYPE_RE = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|static|sealed|abstract|partial|\s)*'
    r'\b(class|struct|interface|enum)\s+([A-Za-z_]\w*)', re.M)


def strip(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    s = re.sub(r'//[^\n]*', '', s)
    return s


# 1) 收集"已经带命名空间"的类型，供冲突预检
ns_types = collections.defaultdict(set)
for dp, dn, fn in os.walk(ALL):
    if os.sep + 'Plugins' + os.sep in dp + os.sep:
        continue
    for f in fn:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(dp, f)
        try:
            t = strip(io.open(p, encoding='utf-8-sig', errors='replace').read())
        except Exception:
            continue
        m = re.search(r'^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)', t, re.M)
        if m:
            for _k, n in TYPE_RE.findall(t):
                ns_types[m.group(1)].add(n)

# 2) 盘点 Assets/Scripts 下无 namespace 的文件
groups = collections.defaultdict(list)
no_ns_total = 0
for dp, dn, fn in os.walk(SCRIPTS):
    for f in fn:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(dp, f)
        try:
            t = strip(io.open(p, encoding='utf-8-sig', errors='replace').read())
        except Exception:
            continue
        if re.search(r'^\s*namespace\s+', t, re.M):
            continue
        no_ns_total += 1
        rel = os.path.relpath(p, SCRIPTS)
        parts = rel.split(os.sep)
        key = os.sep.join(parts[:2]) if len(parts) > 2 else os.sep.join(parts[:-1])
        types = [n for _k, n in TYPE_RE.findall(t)]
        groups[key].append((rel, types))

print('Assets/Scripts 下无 namespace 文件数 = %d' % no_ns_total)
print()
for key in sorted(groups, key=lambda k: -len(groups[k])):
    items = groups[key]
    print('### %-28s %d 文件' % (key, len(items)))
    for rel, types in sorted(items):
        print('   %-56s %s' % (rel, ','.join(types) if types else '(无类型声明)'))
    print()
