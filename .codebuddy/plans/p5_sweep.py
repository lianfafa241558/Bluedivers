# -*- coding: utf-8 -*-
"""P5-1d：把玩法层对管理器的直连调用批量换成 ServiceLocator 契约调用。

只处理**玩法侧**目录（02Game / 00Tools / 06Gameplay）：
  - Effect / 04UI 在目标分层里位于 09_Managers **之上** ⇒ 直连管理器是允许的，不动。
跳过以 // /// * 开头的注释行，避免改坏说明性注释。
`TaskManager.Instance.*` 与 `nowTask` 不在本脚本内（要先做"数据自持"）。
"""

import io
import os
import re
import sys

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts"
DIRS = ["02Game", "00Tools", "06Gameplay"]

# 顺序有意义：先把带成员的整段换掉，再处理裸用法
SUBS = [
    (r"\bBattleManager\.Instance\.", "GameContract.ServiceLocator.Battle."),
    (r"\bVFXManager\.Creat\(", "GameContract.ServiceLocator.Vfx.Creat("),
    (r"\bVFXManager\.Release\(", "GameContract.ServiceLocator.Vfx.Release("),
    (r"\bGameRoot\.Instance\.StartCoroutine\(", "GameContract.ServiceLocator.Flow.RunCoroutine("),
    (r"\bGameRoot\.CreateTimer\(", "GameContract.ServiceLocator.Flow.CreateTimer("),
    (r"\bGameRoot\.CreatePerTimer\(", "GameContract.ServiceLocator.Flow.CreatePerTimer("),
    (r"\bGameRoot\.GameState\b", "GameContract.ServiceLocator.Flow.GameState"),
    (r"\bResSvc\.Instance\.", "GameContract.ServiceLocator.Res."),
    (r"\bWndManager\.Instance\.CreatNotice\(", "GameContract.ServiceLocator.Wnd.CreatNotice("),
    (r"\bWndManager\.WindowState\b", "GameContract.ServiceLocator.Wnd.WindowState"),
    (r"\bWndManager\.OnWindowStateChange\b", "GameContract.ServiceLocator.Wnd.OnWindowStateChange"),
    (r"\bArchiveSvc\.GetSetting\(", "GameContract.ServiceLocator.Archive.GetSetting("),
    (r"\bPathRequestManager\.Instance\.", "GameContract.ServiceLocator.Path."),
    (r"\bRoomManager\.Instance\.", "GameContract.ServiceLocator.Room."),
]

COMMENT = re.compile(r"^\s*(//|///|\*|/\*)")

files = []
for d in DIRS:
    base = os.path.join(ROOT, d)
    for dp, dn, fn in os.walk(base):
        for f in fn:
            if f.endswith(".cs"):
                files.append(os.path.join(dp, f))
files.sort()

total = 0
touched = 0
detail = {}
for path in files:
    with io.open(path, "r", encoding="utf-8-sig", newline="") as fh:
        lines = fh.read().split("\n")
    hits = 0
    for i, line in enumerate(lines):
        if COMMENT.match(line):
            continue
        new = line
        for pat, rep in SUBS:
            new = re.sub(pat, rep, new)
        if new != line:
            hits += len([1 for _ in range(1)])  # 计行数
            lines[i] = new
            total += 1
    if hits:
        with io.open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write("\n".join(lines))
        touched += 1
        detail[os.path.relpath(path, ROOT)] = hits

print("替换行数=%d  涉及文件=%d" % (total, touched))
for k in sorted(detail, key=lambda x: -detail[x])[:40]:
    print("   %2d  %s" % (detail[k], k))
if not detail:
    print("   (无命中)")
