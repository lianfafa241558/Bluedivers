# -*- coding: utf-8 -*-
"""契约层公开类型的跨层使用面扫描（剥注释 + 剥字符串，避免假阳性）。

用途：判断 01_GameContract 的每个类型"谁在生产 / 谁在消费"，
     以决定它该留在契约层、还是下沉、还是上移到表现层。
输出：类型 -> 顶层目录分布（按总次数降序）。
"""
import os
import re
import sys
import collections

SCRIPTS = r"d:\Pro\Bluedivers\Assets\Scripts"
CONTRACT = os.path.join(SCRIPTS, "00GameContract")
SKIP_DIRS = {"Plugins", "MackySoft", "DynamicBone"}
SELF = "00GameContract"


def strip_code(src):
    """把注释内容抹掉（保留换行）、字符串/字符字面量替换为 ''，避免误命中。"""
    res = []
    i, n, st = 0, len(src), 0
    while i < n:
        c = src[i]
        d = src[i + 1] if i + 1 < n else ''
        if st == 0:
            if c == '/' and d == '/':
                st = 1; i += 2; continue
            if c == '/' and d == '*':
                st = 2; i += 2; continue
            if c == '@' and d == '"':
                res.append('@""'); st = 5; i += 2; continue
            if c == '"':
                res.append('""'); st = 3; i += 1; continue
            if c == "'":
                res.append("''"); st = 4; i += 1; continue
            res.append(c); i += 1; continue
        if st == 1:
            if c == '\n':
                res.append('\n'); st = 0
            i += 1; continue
        if st == 2:
            if c == '*' and d == '/':
                st = 0; i += 2; continue
            if c == '\n':
                res.append('\n')
            i += 1; continue
        if st == 3:
            if c == '\\':
                i += 2; continue
            if c == '"':
                st = 0
            i += 1; continue
        if st == 4:
            if c == '\\':
                i += 2; continue
            if c == "'":
                st = 0
            i += 1; continue
        if st == 5:
            if c == '"' and d == '"':
                i += 2; continue
            if c == '"':
                st = 0
            i += 1; continue
    return ''.join(res)


def collect_contract_types():
    names = {}
    pat = re.compile(r'\b(?:class|struct|enum|interface)\s+(\w+)')
    for fn in sorted(os.listdir(CONTRACT)):
        if not fn.endswith('.cs'):
            continue
        src = open(os.path.join(CONTRACT, fn), encoding='utf-8', errors='ignore').read()
        code = strip_code(src)
        for m in pat.finditer(code):
            names.setdefault(m.group(1), fn)
    return names


def main():
    types = collect_contract_types()
    # 预编译：一个 token 词表即可（逐文件扫描时按 token 匹配）
    stat = {t: collections.Counter() for t in types}
    files = {t: [] for t in types}
    total_files = 0
    for dirpath, dirnames, filenames in os.walk(SCRIPTS):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        rel = os.path.relpath(dirpath, SCRIPTS)
        top = rel.split(os.sep)[0]
        if top == SELF:
            continue
        for fn in filenames:
            if not fn.endswith('.cs'):
                continue
            total_files += 1
            p = os.path.join(dirpath, fn)
            try:
                code = strip_code(open(p, encoding='utf-8', errors='ignore').read())
            except Exception:
                continue
            toks = set(re.findall(r'\b[A-Za-z_]\w*\b', code))
            for t in types:
                if t in toks:
                    stat[t][top] += code.count(t)
                    if len(files[t]) < 3:
                        files[t].append(os.path.join(rel, fn))
    print("扫描文件数(不含契约层自身): %d，契约层类型: %d\n" % (total_files, len(types)))
    rows = sorted(stat.items(), key=lambda kv: -sum(kv[1].values()))
    print("%-28s %5s  %s" % ("类型", "总", "顶层目录分布"))
    print("-" * 110)
    for t, cnt in rows:
        tot = sum(cnt.values())
        dist = " ".join("%s:%d" % (k, v) for k, v in cnt.most_common())
        print("%-28s %5d  %s" % (t, tot, dist if dist else "(仅契约层内部使用)"))
        if tot and "--detail" in sys.argv:
            print("        files: %s" % ", ".join(files[t]))


if __name__ == '__main__':
    main()
