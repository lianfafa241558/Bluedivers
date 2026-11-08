"""在 dll 里找字符串（.NET 元数据里的字符串是 UTF-16LE，普通 grep 搜不到）。

用法: python find_dll_string.py "关键字" [目录...]
默认扫描 Assets/Plugins 与 Library/ScriptAssemblies。
"""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


def main():
    kw = sys.argv[1]
    dirs = sys.argv[2:] or [os.path.join(ROOT, "Assets", "Plugins"), os.path.join(ROOT, "Library", "ScriptAssemblies")]
    pat_u16 = kw.encode("utf-16-le")
    pat_u8 = kw.encode("utf-8")
    found = 0
    for d in dirs:
        d = os.path.abspath(d)
        if not os.path.isdir(d):
            continue
        for dirpath, _, filenames in os.walk(d):
            for fn in filenames:
                if not fn.lower().endswith((".dll", ".exe")):
                    continue
                p = os.path.join(dirpath, fn)
                try:
                    with open(p, "rb") as f:
                        data = f.read()
                except OSError:
                    continue
                if pat_u16 in data or pat_u8 in data:
                    found += 1
                    print("命中:", os.path.relpath(p, ROOT))
    print("合计", found, "个文件")


if __name__ == "__main__":
    main()
