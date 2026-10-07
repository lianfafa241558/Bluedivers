# -*- coding: utf-8 -*-
"""在托管 DLL 的原始字节里搜 ASCII 记号（比 ripgrep 强：rg 默认跳过二进制）。

用途：判断 KCPNet.dll 自身是否也在运行时用 MessagePackSerializer（即它的序列化是否也踩
IL2CPP 无 Reflection.Emit 的坑）。注意 .NET #Strings 堆有**后缀共享**，
所以"搜不到"不等于"没引用"，但"搜到"就是实锤。
"""
import os
import sys

DEFAULT_DIR = r"d:\Pro\Bluedivers\Assets\Plugins\KCPNet"
TOKENS = [
    b"MessagePackObject",
    b"KeyAttribute",
    b"MessagePackSerializer",
    b"GeneratedMessagePackResolver",
    b"Reflection.Emit",
    b"NetMessageSerializer",
]


def scan(path):
    with open(path, "rb") as f:
        blob = f.read()
    print(f"===== {os.path.basename(path)}  ({len(blob)} bytes)")
    for t in TOKENS:
        print(f"  {t.decode():32s} {'HIT x%d' % blob.count(t) if t in blob else '-'}")


def main():
    targets = sys.argv[1:]
    if not targets:
        targets = [os.path.join(DEFAULT_DIR, n) for n in os.listdir(DEFAULT_DIR) if n.endswith(".dll")]
    for p in targets:
        scan(p)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
