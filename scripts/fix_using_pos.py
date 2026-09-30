# -*- coding: utf-8 -*-
"""
修复批 1 的 using 位置错误：
ns_batch.py 用"最后一个以 using 开头的行"来定位插入点，被方法体内的
`using var x = ...`（C# 8 using 声明）误导，把 `using FPSGame.Data;` 插到了文件中部 ⇒ 不生效。

本脚本：
  1) 从 02Data 重新提取全部类型名
  2) 对每个引用了这些类型的 .cs（排除 02Data 自身与 Plugins）：
     - 删掉任意位置的 `using FPSGame.Data;`
     - 若需要，插到"头部 using 块"末尾（只认行首无缩进的 using，天然排除方法内的 using var）
所有读写用 newline=''，不再做 split/join 行尾拼接（改用整串替换，避免 \r\r\n 重现）。用完即删。
"""
import io
import os
import re

ROOT = r'D:\Pro\Bluedivers\Assets'
TARGET_DIR = os.path.join(ROOT, 'Scripts', '02Data')
NS = 'FPSGame.Data'
LINE = 'using ' + NS + ';'
TYPE_RE = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|static|sealed|abstract|partial|\s)*'
    r'\b(class|struct|interface|enum)\s+([A-Za-z_]\w*)', re.M)


def strip(s):
    s = re.sub(r'/\*.*?\*/', '', s, flags=re.S)
    s = re.sub(r'//[^\n]*', '', s)
    return s


type_names = set()
for dp, dn, fn in os.walk(TARGET_DIR):
    for f in fn:
        if not f.endswith('.cs'):
            continue
        t = io.open(os.path.join(dp, f), encoding='utf-8-sig', errors='replace').read()
        for _k, n in TYPE_RE.findall(strip(t)):
            type_names.add(n)

pat = re.compile(r'\b(?:' + '|'.join(re.escape(n) for n in sorted(type_names)) + r')\b')
fixed = []
for dp, dn, fn in os.walk(ROOT):
    if os.sep + 'Plugins' + os.sep in dp + os.sep:
        continue
    if os.path.abspath(dp).startswith(os.path.abspath(TARGET_DIR)):
        continue
    for f in fn:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(dp, f)
        t = io.open(p, encoding='utf-8-sig', newline='').read()
        if pat.search(strip(t)) is None:
            continue
        nl = '\r\n' if '\r\n' in t else '\n'
        head = t.split('\n')[:80]
        head_ok = any(ln.strip() == LINE for ln in head)
        has_line = LINE in t
        if head_ok and t.count(LINE) == 1:
            continue

        # ① 删掉所有该 using 行（含行尾）
        t = re.sub(r'(?m)^[ \t]*' + re.escape(LINE) + r'[ \t]*\r?\n', '', t)
        t = re.sub(r'(?m)^[ \t]*' + re.escape(LINE) + r'[ \t]*$', '', t)

        # ② 找头部 using 块末尾（只认行首无缩进的 using）
        lines = t.split(nl)
        idx = 0
        for i, ln in enumerate(lines):
            if ln.startswith('using ') and ln.rstrip().endswith(';'):
                idx = i + 1
        lines.insert(idx, LINE)
        io.open(p, 'w', encoding='utf-8-sig', newline='').write(nl.join(lines))
        fixed.append(os.path.relpath(p, ROOT))

print('FIXED = %d' % len(fixed))
for x in sorted(fixed):
    print('  ' + x)
