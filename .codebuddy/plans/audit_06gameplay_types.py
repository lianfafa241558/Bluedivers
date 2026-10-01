# -*- coding: utf-8 -*-
"""06Gameplay：统计每个 .cs 的顶层类型（大括号深度 0 的 class/struct/enum/interface），找出"一文件多类型"。"""
import os, re, io

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts\06Gameplay"
TYPE = re.compile(r"(?:^|[;}\]\)])\s*(?:public|internal|private|protected)?\s*"
                  r"(?:static\s+|abstract\s+|sealed\s+|unsafe\s+|readonly\s+|partial\s+)*"
                  r"(class|struct|enum|interface)\s+([A-Za-z_]\w*)")


def strip_code(txt):
    out, i, n = [], 0, len(txt)
    while i < n:
        c = txt[i]
        if c == '/' and i + 1 < n and txt[i + 1] == '/':
            j = txt.find('\n', i)
            i = n if j < 0 else j
        elif c == '/' and i + 1 < n and txt[i + 1] == '*':
            j = txt.find('*/', i + 2)
            i = n if j < 0 else j + 2
        elif c == '"':
            i += 1
            while i < n and txt[i] != '"':
                if txt[i] == '\\':
                    i += 1
                i += 1
            i += 1
        elif c == "'":
            i += 1
            while i < n and txt[i] != "'":
                if txt[i] == '\\':
                    i += 1
                i += 1
            i += 1
        else:
            out.append(c)
            i += 1
    return ''.join(out)


def top_types(txt):
    """大括号深度 0 处出现的类型名（含 namespace 内的，因为 namespace 用 {} 包着）"""
    res, stack, i, n = [], 0, 0, len(txt)
    ns_ranges = []
    for m in re.finditer(r"\bnamespace\s+[\w\.]+\s*\{", txt):
        # 找到匹配的右括号
        d, j = 0, m.end() - 1
        while j < n:
            if txt[j] == '{':
                d += 1
            elif txt[j] == '}':
                d -= 1
                if d == 0:
                    break
            j += 1
        ns_ranges.append((m.end(), j))
    line_starts = [0]
    for m in re.finditer("\n", txt):
        line_starts.append(m.end())
    for m in re.finditer(r"class|struct|enum|interface", txt):
        # 取所在声明段
        s = m.start()
        prev = txt[max(0, s - 200):s]
        seg = prev.split(';')[-1].split('}')[-1].split('{')[-1]
        kind = m.group(0)
        rest = txt[m.end():m.end() + 200]
        mm = re.match(r"\s+([A-Za-z_]\w*)", rest)
        if not mm:
            continue
        # 判断该类型是否在最外层（忽略 namespace 包裹）
        pos = m.start()
        dep = 0
        for ch in txt[:pos]:
            if ch == '{':
                dep += 1
            elif ch == '}':
                dep -= 1
        inside_ns = any(a <= pos < b for a, b in ns_ranges)
        if dep == (1 if inside_ns else 0):
            line = txt.count('\n', 0, pos) + 1
            res.append((mm.group(1), kind, line))
    return res


rows = []
for r, ds, fs in os.walk(ROOT):
    for f in sorted(fs):
        if not f.endswith(".cs"):
            continue
        p = os.path.join(r, f)
        with io.open(p, "r", encoding="utf-8-sig", errors="replace") as fh:
            txt = strip_code(fh.read())
        ts = top_types(txt)
        if len(ts) > 1:
            rows.append((os.path.relpath(p, ROOT), ts))

print("=" * 90)
print("一文件多顶层类型（规范：只允许 1 个主类 + 伴生私有内部类）")
for rel, ts in sorted(rows, key=lambda x: -len(x[1])):
    print("   %-62s %d  %s" % (rel, len(ts), ", ".join("%s(%s)" % (a, b) for a, b, _ in ts)))
print("   合计:", len(rows))
