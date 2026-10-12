#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把 .cs 文件统一成项目约定：UTF-8 with BOM + LF 行尾（不改动其他内容）。

用法:
    python -X utf8 .codebuddy/plans/ensure_cs_bom.py <文件或目录> [更多...]
目录会递归收集 *.cs。只补 BOM / 归一化行尾，幂等，可重跑。
"""
import sys
from pathlib import Path

BOM = b"\xef\xbb\xbf"


def collect(args):
    out = []
    for a in args:
        p = Path(a)
        if p.is_dir():
            out.extend(sorted(p.rglob("*.cs")))
        elif p.is_file():
            out.append(p)
        else:
            print(f"[skip] 不存在: {p}")
    return out


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    changed = 0
    for p in collect(argv[1:]):
        raw = p.read_bytes()
        body = raw[3:] if raw.startswith(BOM) else raw
        new = body.replace(b"\r\n", b"\n")
        fixed = BOM + new
        if fixed != raw:
            p.write_bytes(fixed)
            changed += 1
            print(f"[fix] {p}  (BOM={not raw.startswith(BOM)}, CRLF->LF={body.count(b'\\r\\n')})")
        else:
            print(f"[ok ] {p}")
    print(f"完成：{changed} 个文件被修正")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
