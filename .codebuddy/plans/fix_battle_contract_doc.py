# -*- coding: utf-8 -*-
"""修正 IBattleService 头部注释里已过时的两句（授权/统计已事件化）。字节级改写，保 BOM/CRLF。"""
import os

P = r"d:\Pro\Bluedivers\Assets\Scripts\00GameContract\Services\IBattleService.cs"

REPL = [
    ("查单位 / 生波 / 授权战备 /", "查单位 / 生波 / 释放空投 /"),
    ("记录战斗数据这类", "取随机源这类"),
    (
        "只能由本契约承接（配合 <see cref=\"ServiceLocator\"/>）。",
        "只能由本契约承接（配合 <see cref=\"ServiceLocator\"/>）。\r\n"
        "    /// 至于**无返回值的命令**（授权战备 / 战斗统计 / 结束游戏…），2026-10-01 起已全部改走事件\r\n"
        "    /// <c>FPSGame.Gameplay.BattleEventSub</c>，不再占用契约（见下方 ③）。",
    ),
]

data = open(P, "rb").read()
ok = True
for old, new in REPL:
    o, n = old.encode("utf-8"), new.encode("utf-8")
    c = data.count(o)
    print("count=%d  %s" % (c, old))
    if c != 1:
        ok = False
        continue
    data = data.replace(o, n)

if ok:
    open(P, "wb").write(data)
    print("WROTE")
else:
    print("NOT WRITTEN (片段不唯一)")
