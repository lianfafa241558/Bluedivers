# -*- coding: utf-8 -*-
"""日志取上下文：在（可能很大的）日志文件里找 pattern 的最后/全部命中，并打印命中前后若干行。

用法:
    python -X utf8 .codebuddy/plans/log_ctx.py <logfile> <pattern> [before=30] [after=10] [--all]

说明:
    - 流式扫描（不吃内存），只保留命中窗口 ⇒ 可以直接喂 Editor.log / Player.log（几百 MB 也行）。
    - 默认只看**最后一次**命中；加 --all 看全部。
    - 编码按 UTF-8 + errors=replace 读（Unity 日志就是这么写的）。
"""
import io
import os
import sys
from collections import deque


def scan(path, pattern, before, after, all_hits, after_line=0, before_line=0):
    prev = deque(maxlen=before)
    windows = []          # 每项: [ [ (lineno, text), ... ], after_left ]
    pending = []
    lineno = 0
    total_hits = 0
    with io.open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            lineno += 1
            # --after/--before：只看某个行号区间（用于"这局"内部，跳过前面几局的噪声）
            if lineno < after_line:
                prev.append((lineno, line))
                continue
            if before_line and lineno > before_line:
                break
            # 先把这一行补给"还在收集后文"的窗口
            for w in pending:
                w[0].append((lineno, line))
                w[1] -= 1
            pending = [w for w in pending if w[1] > 0]

            if pattern in line:
                total_hits += 1
                win = [(k, t) for k, t in prev]
                win.append((lineno, line))
                if all_hits:
                    windows.append([win, after])
                    pending.append(windows[-1])
                elif after <= 0:
                    windows = [[win, 0]]
                else:
                    windows = [[win, after]]
                    pending = [windows[0]]

            prev.append((lineno, line))

    print("file=%s totalLines=%d hits=%d" % (path, lineno, total_hits))
    for win, _ in windows:
        for k, t in win:
            print("%7d| %s" % (k, t.rstrip("\n")))


def dump_range(path, start, end):
    """按行号区间直接打印（1 起，含两端）。"""
    with io.open(path, "r", encoding="utf-8", errors="replace") as f:
        for lineno, line in enumerate(f, 1):
            if lineno > end:
                break
            if lineno >= start:
                print("%7d| %s" % (lineno, line.rstrip("\n")))


def main():
    raw = sys.argv[1:]
    opts = {}
    args = []
    i = 0
    while i < len(raw):
        a = raw[i]
        if a in ("--after", "--before"):          # 带值的选项
            opts[a.lstrip("-")] = int(raw[i + 1])
            i += 2
            continue
        args.append(a)
        i += 1

    flags = set(a for a in args if a.startswith("--"))
    args = [a for a in args if not a.startswith("--")]

    # 模式二: --range <file> <start> <end>
    if "--range" in flags:
        if len(args) < 3:
            print(__doc__)
            return 1
        path, start, end = args[0], int(args[1]), int(args[2])
        if not os.path.exists(path):
            print("NOT FOUND: " + path)
            return 2
        print("size=%d bytes" % os.path.getsize(path))
        dump_range(path, start, end)
        return 0

    if len(args) < 2:
        print(__doc__)
        return 1

    path, pattern = args[0], args[1]
    before = int(args[2]) if len(args) > 2 else 30
    after = int(args[3]) if len(args) > 3 else 10

    if not os.path.exists(path):
        print("NOT FOUND: " + path)
        return 2

    print("size=%d bytes" % os.path.getsize(path))
    scan(path, pattern, before, after, "--all" in flags,
         opts.get("after", 0), opts.get("before", 0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
