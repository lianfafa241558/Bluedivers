# -*- coding: utf-8 -*-
"""把 <src> 的字节原样**追加**到 <dst> 末尾（用于给记忆文件追加段落）。

用法:
    python -X utf8 .codebuddy/plans/md_append.py <dst.md> <src.md|src.txt>

为什么要有它：记忆文件的尾部常被别处/用户编辑过、且正文里有全角引号，用"字符串匹配 + 替换"容易对不上；
按字节追加不依赖任何匹配，也不碰编码（BOM/行尾都不动）。
"""
import io
import os
import sys


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    dst, src = sys.argv[1], sys.argv[2]
    if not os.path.exists(dst):
        print("NOT FOUND: " + dst)
        return 2
    if not os.path.exists(src):
        print("NOT FOUND: " + src)
        return 2

    with io.open(src, "rb") as f:
        blob = f.read()
    with io.open(dst, "ab") as f:
        f.write(blob)

    print("appended %d bytes: %s -> %s (now %d bytes)"
          % (len(blob), src, dst, os.path.getsize(dst)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
