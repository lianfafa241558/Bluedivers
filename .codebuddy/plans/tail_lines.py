# -*- coding: utf-8 -*-
"""打印文件末尾 N 行（读 Editor.log / Player.log 用；本机 PowerShell 没有 tail）。

用法:
    python -X utf8 .codebuddy/plans/tail_lines.py <文件> [N=200] [--grep 关键词]
"""
import sys


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    path = sys.argv[1]
    n = 200
    grep = None
    rest = sys.argv[2:]
    i = 0
    while i < len(rest):
        if rest[i] == "--grep" and i + 1 < len(rest):
            grep = rest[i + 1]
            i += 2
            continue
        try:
            n = int(rest[i])
        except ValueError:
            print("bad arg: %r" % rest[i])
            return 2
        i += 1

    # errors="replace"：Editor.log 里偶有非 UTF-8 字节，别让脚本崩
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()

    total = len(lines)
    tail = lines[-n:] if n < total else lines
    if grep:
        tail = [ln for ln in tail if grep in ln]
        print("# total=%d, tail=%d, grep=%r -> %d hits" % (total, n, grep, len(tail)))
    else:
        print("# total=%d, showing last %d" % (total, len(tail)))
    for ln in tail:
        sys.stdout.write(ln if ln.endswith("\n") else ln + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
