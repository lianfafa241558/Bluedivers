# -*- coding: utf-8 -*-
"""
无 namespace 补全（通用批次脚本，可复用）
用法：python ns_batch.py <目标目录> <命名空间> [--apply]
     不带 --apply 时只做 dry-run 报告，不写文件。

已内置前几批踩坑后的正确做法：
  · 行尾：nl = '\r\n' if '\r\n' in t else '\n'，t.split(nl) + nl.join() 配套使用（避免 \r\r\n）
  · 插入点：只认「行首无缩进 + 以 'using ' 开头 + 以 ';' 结尾 + 不含 '='」的普通 using
            （自动排除方法体内的 using var 与别名 using）
  · 加 namespace：在头部 using 块之后插入声明，文件末尾补 '}'
  · 补 using：按目标类型名扫描；跳过目标目录自身；跳过 Plugins
"""
import io
import os
import re
import sys

ROOT = r'D:\Pro\Bluedivers\Assets'
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
    """
    返回头部 using 块末尾的插入下标。
    只认「普通 using」：行首无缩进 + 以 'using ' 开头 + 以 ';' 结尾 + 不含 '='。
    ⚠ 必须排除 `using static X;` 与别名 using `using A = B;` —— 它们引用了具体类型，
      普通 using 必须排在它们**之前**，否则 CS0246（本批在 VehicleUI.cs 上踩过）。
    """
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


def main():
    target = os.path.abspath(os.path.join(ROOT, sys.argv[1]))
    ns = sys.argv[2]
    apply_changes = '--apply' in sys.argv

    # ---- 1) 目标文件的 namespace ----
    targets, type_names = [], set()
    for dp, dn, fn in os.walk(target):
        for f in fn:
            if not f.endswith('.cs'):
                continue
            p = os.path.join(dp, f)
            t = io.open(p, encoding='utf-8-sig', errors='replace').read()
            if re.search(r'^\s*namespace\s+', strip(t), re.M):
                continue
            targets.append(p)
            for _k, n in TYPE_RE.findall(strip(t)):
                type_names.add(n)

    print('目标目录 : %s' % os.path.relpath(target, ROOT))
    print('目标命名空间: %s' % ns)
    print('待加 namespace 文件: %d ; 类型: %d 个' % (len(targets), len(type_names)))

    if apply_changes:
        for p in targets:
            t = io.open(p, encoding='utf-8-sig', newline='').read()
            nl = eol(t)
            lines = t.split(nl)
            i = head_using_end(lines)
            lines = lines[:i] + ['', 'namespace ' + ns, '{'] + lines[i:]
            t2 = nl.join(lines)
            if not t2.endswith(nl):
                t2 += nl
            t2 += '}' + nl
            io.open(p, 'w', encoding='utf-8-sig', newline='').write(t2)
        print('  -> 已写入 namespace')

    # ---- 2) 消费点补 using ----
    if not type_names:
        return
    pat = re.compile(r'\b(?:' + '|'.join(re.escape(n) for n in sorted(type_names)) + r')\b')
    todo = []
    for dp, dn, fn in os.walk(ROOT):
        rel_top = os.path.relpath(dp, ROOT).split(os.sep)[0]
        if rel_top not in ('Scripts', 'Editor'):
            continue  # 白名单：第三方（Plugins/MackySoft/Pixeye/...）一律不碰
        if os.path.abspath(dp).startswith(target):
            continue
        for f in fn:
            if not f.endswith('.cs'):
                continue
            p = os.path.join(dp, f)
            t = io.open(p, encoding='utf-8-sig', errors='replace').read()
            if pat.search(strip(t)) is None:
                continue
            if re.search(r'^\s*using\s+' + re.escape(ns) + r'\s*;', t, re.M):
                continue
            if re.search(r'^\s*namespace\s+' + re.escape(ns) + r'\b', t, re.M):
                continue
            todo.append(p)

    print('需补 using 的消费点: %d 文件' % len(todo))
    if apply_changes:
        for p in todo:
            t = io.open(p, encoding='utf-8-sig', newline='').read()
            nl = eol(t)
            lines = t.split(nl)
            i = head_using_end(lines)
            lines = lines[:i] + ['using ' + ns + ';'] + lines[i:]
            io.open(p, 'w', encoding='utf-8-sig', newline='').write(nl.join(lines))
        print('  -> 已补 using')
    else:
        for p in sorted(todo)[:40]:
            print('   ' + os.path.relpath(p, ROOT))


main()
