# -*- coding: utf-8 -*-
"""列出 Unity 编译产物 dll 的修改时间（编译验收的硬判据之一）。

用法：
    python dll_mtime.py            # 列出最近修改的 12 个 dll
    python dll_mtime.py 10         # 先等 10 秒（等异步编译排队完成）再列
    python dll_mtime.py 10 09 06   # 等 10 秒，并只列名字含 09/06 的 dll
"""
import glob
import os
import re
import sys
import time

LIB = os.path.join("Library", "ScriptAssemblies")


def main():
    args = sys.argv[1:]
    wait = 0
    if args and args[0].isdigit():
        wait = int(args[0])
        args = args[1:]
    if wait:
        time.sleep(wait)

    files = glob.glob(os.path.join(LIB, "*.dll"))
    if args:
        files = [p for p in files if all(a.lower() in os.path.basename(p).lower() for a in args)]
    files.sort(key=os.path.getmtime, reverse=True)

    for p in files[:12]:
        ts = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(os.path.getmtime(p)))
        print("%s  %s" % (ts, os.path.basename(p)))
    print("---")
    print("count=%d (dir=%s)" % (len(files), LIB))


if __name__ == "__main__":
    main()
