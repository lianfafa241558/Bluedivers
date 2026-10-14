# -*- coding: utf-8 -*-
"""列出目录下最近改动的文件（判"到底编没编"用：比对 .cs 与 dll 的 mtime）。

用法:
    python -X utf8 .codebuddy/plans/recent_files.py <目录> [N=15] [--ext dll,cs]
"""
import os
import sys
import time


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    root = sys.argv[1]
    n = 15
    exts = None
    rest = sys.argv[2:]
    i = 0
    while i < len(rest):
        if rest[i] == "--ext" and i + 1 < len(rest):
            exts = tuple("." + e.strip().lstrip(".") for e in rest[i + 1].split(",") if e.strip())
            i += 2
            continue
        try:
            n = int(rest[i])
        except ValueError:
            print("bad arg: %r" % rest[i])
            return 2
        i += 1

    rows = []
    for dirpath, _dirnames, filenames in os.walk(root):
        for fn in filenames:
            if exts and not fn.lower().endswith(exts):
                continue
            full = os.path.join(dirpath, fn)
            try:
                st = os.stat(full)
            except OSError:
                continue
            rows.append((st.st_mtime, st.st_size, full))

    rows.sort(reverse=True)
    print("# root=%s matched=%d" % (root, len(rows)))
    for mtime, size, full in rows[:n]:
        print("%s  %9d  %s" % (time.strftime("%m-%d %H:%M:%S", time.localtime(mtime)), size,
                               os.path.relpath(full, root)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
