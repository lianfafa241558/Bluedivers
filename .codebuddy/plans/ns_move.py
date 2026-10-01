# -*- coding: utf-8 -*-
"""通用「命名空间搬迁引擎」：把若干 .cs 的 namespace 从旧值改到新值，并同步所有 using。

输入 moves.json: [[ "Assets/Scripts/.../X.cs", "New.Namespace" ], ...]

做的事：
  1) 改这些文件的 `namespace <old>` -> `namespace <new>`（字节级，保 BOM/换行）；
  2) 改所有 `using static <old>.<Type>;` -> `using static <new>.<Type>;`（Type 属被搬文件）；
  3) 引用被搬类型的所有 .cs：若处新命名空间或已有该 using 则跳过，否则补 `using <new>;`
     （普通 using 插在最后一条普通 using 之后，不越过 using static/别名）；
  4) 被搬文件自身：若引用的类型落在其它命名空间而它没有对应 using，则补上（仅当该类型名唯一归属时，避免歧义）。

用法： python ns_move.py <moves.json> [--apply]
"""
import os
import re
import io
import sys
import json
import collections

ROOT = r'd:\Pro\Bluedivers'
A = os.path.join(ROOT, 'Assets')
APPLY = '--apply' in sys.argv

DECL = re.compile(r'\b(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|struct|interface|enum)\s+(\w+)')
NSD = re.compile(r'namespace\s+([\w\.]+)')
USE_ORD = re.compile(rb'(?m)^[ \t]*using\s+(?!static\b)[A-Za-z_][\w\.]*\s*;')
HAS_USE_T = 'using\\s+{}\\s*;'   # 普通 using 存在性


def strip(t):
    t = re.sub(r'/\*.*?\*/', ' ', t, flags=re.S)
    t = re.sub(r'//[^\n]*', ' ', t)
    t = re.sub(r'"(\\.|[^"\\])*"', '""', t)
    t = re.sub(r"'(\\.|[^'\\])*'", "''", t)
    return t


def add_using(b, ns):
    bom = b[:3] if b[:3] == b'\xef\xbb\xbf' else b''
    body = b[len(bom):]
    nl = b'\r\n' if b'\r\n' in body else b'\n'
    line = b'using ' + ns.encode() + b';' + nl
    us = list(USE_ORD.finditer(body))
    if us:
        pos = us[-1].end()
        if body[pos:pos + len(nl)] == nl:
            return bom + body[:pos + len(nl)] + line + body[pos + len(nl):]
        return bom + body[:pos] + nl + line + body[pos:]
    m = re.search(rb'(?m)^[ \t]*namespace\b', body)
    if m:
        return bom + body[:m.start()] + line + body[m.start():]
    return bom + line + body


def main():
    moves = json.load(open(sys.argv[1], encoding='utf-8'))
    # 载入全部 .cs
    files = {}
    for r, d, fs in os.walk(A):
        for f in fs:
            if f.endswith('.cs'):
                p = os.path.join(r, f)
                rel = os.path.relpath(p, ROOT).replace('\\', '/')
                files[rel] = open(p, 'rb').read()

    old_ns_of = {}
    new_ns_of = {}
    types_of = {}
    old_texts = {}
    for rel, _ in moves:
        t = files[rel].decode('utf-8-sig', 'replace')
        old_texts[rel] = t
        m = NSD.search(t)
        old_ns_of[rel] = m.group(1) if m else ''
        types_of[rel] = set(DECL.findall(t))
    for rel, nns in moves:
        new_ns_of[rel] = nns

    # 1) 改 namespace
    plan = {}
    for rel, nns in moves:
        b = files[rel]
        old = old_ns_of[rel]
        nb, n = re.subn((r'namespace\s+' + re.escape(old) + r'\b').encode(),
                        ('namespace ' + nns).encode(), b, count=1)
        if n != 1:
            print('!! 改 ns 失败', rel, old, '->', nns)
            continue
        files[rel] = nb
        plan[rel] = ['ns:%s->%s' % (old, nns)]

    # 2) using static <old>.<Type> -> <new>.<Type>
    for rel, nns in moves:
        old = old_ns_of[rel]
        for tn in types_of[rel]:
            pat = re.compile(r'(using\s+static\s+)' + re.escape(old) + r'\.' + re.escape(tn) + r'\b')
            for f2, b2 in list(files.items()):
                t2 = b2.decode('utf-8-sig', 'replace')
                if pat.search(t2):
                    nb = re.sub(pat, lambda mm: mm.group(1) + nns + '.' + tn, b2.decode('utf-8-sig')).encode('utf-8')
                    bom = b2[:3] == b'\xef\xbb\xbf'
                    files[f2] = (b'\xef\xbb\xbf' if bom else b'') + nb
                    plan.setdefault(f2, []).append('static-using %s.%s->%s' % (old, tn, nns))

    # 3) 类型 -> 命名空间（用改后的文本）
    type_ns = collections.defaultdict(set)
    for rel, b in files.items():
        t = strip(b.decode('utf-8-sig', 'replace'))
        m = NSD.search(t)
        ns = m.group(1) if m else ''
        for n in set(DECL.findall(t)):
            type_ns[n].add(ns)

    def cur_ns(rel):
        if rel in new_ns_of:
            return new_ns_of[rel]
        m = NSD.search(strip(files[rel].decode('utf-8-sig', 'replace')))
        return m.group(1) if m else ''

    def has_using(b, ns):
        return re.search((r'(?m)^[ \t]*' + HAS_USE_T.format(re.escape(ns))).encode(), b) is not None

    # 4) 被搬类型的引用者：补 using <new>;
    moved_types = set()
    for rel in new_ns_of:
        moved_types |= types_of[rel]
    for rel, b in list(files.items()):
        code = strip(b.decode('utf-8-sig', 'replace'))
        hit = [n for n in moved_types if re.search(r'\b' + n + r'\b', code)]
        if not hit:
            continue
        need = set()
        for n in hit:
            for src in new_ns_of:
                if n in types_of[src]:
                    need.add(new_ns_of[src])
        for nns in need:
            if cur_ns(rel) == nns or has_using(b, nns):
                continue
            files[rel] = add_using(b, nns)
            b = files[rel]
            plan.setdefault(rel, []).append('+using ' + nns)

    # 5) 被搬文件自身：补齐它引用到的「其它命名空间」的 using（--no-self 时跳过，改由离线编译器兜底）
    for rel in ([] if '--no-self' in sys.argv else new_ns_of):
        b = files[rel]
        code = strip(b.decode('utf-8-sig', 'replace'))
        own = new_ns_of[rel]
        ref_names = [n for n in type_ns if re.search(r'\b' + n + r'\b', code) and n not in types_of[rel]]
        need = set()
        for n in ref_names:
            if len(type_ns[n]) == 1:
                ns = next(iter(type_ns[n]))
                if ns and ns != own:
                    need.add(ns)
        for ns in sorted(need):
            if not has_using(b, ns):
                files[rel] = add_using(b, ns)
                plan.setdefault(rel, []).append('(self)+using ' + ns)

    print('涉及文件 %d 个：' % len(plan))
    for p in sorted(plan):
        print('  [%s] %s' % (','.join(plan[p]), p))

    if APPLY:
        for p in plan:
            open(os.path.join(ROOT, p.replace('/', os.sep)), 'wb').write(files[p])
        print('已写入 %d 个文件' % len(plan))
    else:
        print('（未写入，加 --apply 落盘）')


if __name__ == '__main__':
    main()
