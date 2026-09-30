# -*- coding: utf-8 -*-
"""
按「命名空间」补 using（修复 prune_using.py 误删的同程序集 using）。
用法：python fix_using_ns.py <命名空间> [--apply]

逻辑：
  1) 收集声明了该命名空间的所有文件的类型名
  2) 全仓（白名单 Scripts/Editor）扫描：引用了这些类型名、但既没有该 using、
     也不在该命名空间内的文件 ⇒ 补 using（插到头部 using 块末尾，排除 using static 与别名）
"""
import io
import os
import re
import sys

ROOT = r'D:\Pro\Bluedivers\Assets'
NS = sys.argv[1]
APPLY = '--apply' in sys.argv
TYPE_RE = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|static|sealed|abstract|partial|\s)*'
    r'\b(class|struct|interface|enum)\s+([A-Za-z_]\w*)', re.M)


def strip(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    s = re.sub(r'//[^\n]*', '', s)
    return s


def eol(t):
    return '\r\n' if '\r\n' in t else '\n'


def head_using_end(lines):
    idx = 0
    for i, ln in enumerate(lines):
        s = ln.rstrip()
        if s.startswith('using ') and s.endswith(';') and '=' not in s and not s.startswith('using static '):
            idx = i + 1
        elif s.strip() == '' or s.startswith('//') or s.startswith('/*') or s.startswith('*'):
            continue
        else:
            break
    return idx


type_names = set()
for dp, dn, fn in os.walk(ROOT):
    for f in fn:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(dp, f)
        t = strip(io.open(p, encoding='utf-8-sig', errors='replace').read())
        if re.search(r'^\s*namespace\s+' + re.escape(NS) + r'\s*$', t, re.M):
            for _k, n in TYPE_RE.findall(t):
                type_names.add(n)

print('命名空间 %s 下收集到 %d 个类型' % (NS, len(type_names)))
if not type_names:
    raise SystemExit('没有收集到类型，检查命名空间名是否正确')

pat = re.compile(r'\b(?:' + '|'.join(re.escape(n) for n in sorted(type_names)) + r')\b')
todo = []
for dp, dn, fn in os.walk(ROOT):
    top = os.path.relpath(dp, ROOT).split(os.sep)[0]
    if top not in ('Scripts', 'Editor'):
        continue
    for f in fn:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(dp, f)
        t = io.open(p, encoding='utf-8-sig', errors='replace').read()
        s = strip(t)
        if re.search(r'^\s*namespace\s+' + re.escape(NS) + r'\s*$', s, re.M):
            continue
        if re.search(r'^\s*using\s+' + re.escape(NS) + r'\s*;', t, re.M):
            continue
        if pat.search(s) is None:
            continue
        todo.append(p)

print('需补 using 的文件 = %d' % len(todo))
for p in sorted(todo):
    print('  ' + os.path.relpath(p, ROOT))

if APPLY:
    for p in todo:
        t = io.open(p, encoding='utf-8-sig', newline='').read()
        nl = eol(t)
        lines = t.split(nl)
        i = head_using_end(lines)
        lines = lines[:i] + ['using ' + NS + ';'] + lines[i:]
        io.open(p, 'w', encoding='utf-8-sig', newline='').write(nl.join(lines))
    print('  -> 已补 using')
