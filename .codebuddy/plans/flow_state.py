# -*- coding: utf-8 -*-
"""批 1：把 `ServiceLocator.Flow.GameState`（含 `?.`）机械替换为 `FPSGame.Data.FlowState.GameState`。

只匹配 `.GameState` 这一个成员 ⇒ 不碰 `SetGameState`/`CreateTimer`/`IsMainStage`/`RunCoroutine`
（那些留给批 2/批 3）。
跳过契约层（00GameContract/，槽位本身要与 NullFlowService 一起删）。
逐文件保留 BOM。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCAN = os.path.join(ROOT, "Assets", "Scripts")
SKIP_DIRS = ("00GameContract",)
PAT = re.compile(r"(?:FPSGame\.GameContract\.)?ServiceLocator\.Flow\??\.GameState")
REPL = "FPSGame.Data.FlowState.GameState"


def main():
    total_files = 0
    total_hits = 0
    for dirpath, _, fns in os.walk(SCAN):
        rel_dir = dirpath.replace("\\", "/")
        if any(d in rel_dir for d in SKIP_DIRS):
            continue
        for fn in sorted(fns):
            if not fn.endswith(".cs"):
                continue
            full = os.path.join(dirpath, fn)
            raw = open(full, "rb").read()
            bom = raw.startswith(b"\xef\xbb\xbf")
            txt = raw.decode("utf-8-sig")
            new, n = PAT.subn(REPL, txt)
            if n == 0:
                continue
            out = new.encode("utf-8")
            if bom:
                out = b"\xef\xbb\xbf" + out
            open(full, "wb").write(out)
            total_files += 1
            total_hits += n
            print("  %-72s %d  (BOM=%s)" % (os.path.relpath(full, ROOT), n, bom))
    print("---")
    print("files=%d  hits=%d" % (total_files, total_hits))


if __name__ == "__main__":
    main()
