# -*- coding: utf-8 -*-
"""把 TaskManager.cs 里本次插入块前那个空白行统一成 CRLF（消除本次编辑唯一引入的孤立 LF）。"""

import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PATH = os.path.join(ROOT, "Assets/Scripts/09Manager/Global/TaskManager.cs")

raw = open(PATH, "rb").read()

head = "}".encode() + b"\r\n" + "        /// <summary>".encode()
old = head.replace(b"\r\n", b"\r\n\n")   # '}' + CRLF + LF + '        /// <summary>'（空白行是孤立 LF）
new = head.replace(b"\r\n", b"\r\n\r\n")  # 把该空白行改成 CRLF

hits = raw.count(old)
print("锚点命中:", hits)
if hits != 1:
    sys.exit("FAIL: 期望 1 处，实际 %d 处 —— 未写入" % hits)

open(PATH, "wb").write(raw.replace(old, new))
raw = open(PATH, "rb").read()
print("已写入。crlf=%d lf=%d（差值 = 孤立 LF 行数）" % (raw.count(b"\r\n"), raw.count(b"\n")))
