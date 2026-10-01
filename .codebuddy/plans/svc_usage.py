# -*- coding: utf-8 -*-
"""统计 ServiceLocator 各槽位的真实使用面（按槽位 / 按消费层）。

用法：
    python svc_usage.py            # 汇总表
    python svc_usage.py Battle     # 只列 Battle 槽的明细（文件:行: 代码）
"""
import os
import re
import sys
from collections import Counter, defaultdict

SLOTS = ["Task", "Battle", "Flow", "Vfx", "Net", "Res", "Archive", "Wnd", "Room", "Path"]
ROOTS = ["Assets/Scripts", "Assets/Editor"]
PAT = re.compile(r"ServiceLocator\s*\.\s*(%s)\b" % "|".join(SLOTS))
DECL = re.compile(r"ServiceLocator\s*\.\s*(%s)\s*=(?!=)" % "|".join(SLOTS))


def strip_comments(text):
    """剥 /* */ 与行尾 // 注释（避免注释里的假阳性）。

    ⚠ 块注释必须用**等量换行**替换，否则行号会整体前移、报出的"文件:行"全是错的。
    """
    text = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S)
    out = []
    for line in text.split("\n"):
        # 很粗的字符串保护：不处理，注释剥离已能覆盖绝大多数假阳性
        i = line.find("//")
        out.append(line if i < 0 else line[:i])
    return "\n".join(out)


def unit_of(path):
    """按路径给出"消费层"简称。"""
    p = path.replace("\\", "/")
    for key, name in [
        ("/00GameContract/", "01_GameContract"),
        ("/00Core/", "00_Core"),
        ("/00Attribute/", "00_Attribute"),
        ("/00Tools/", "00_Utils"),
        ("/01Manager/", "09_Managers"),
        ("/02Data/", "04_Data"),
        ("/05UnitCore/", "05_UnitCore"),
        ("/06Gameplay/", "06_Gameplay"),
        ("/04UI/", "10_UI"),
        ("/Effect/", "10_Effect"),
        ("/NetTmp/", "02_Net"),
        ("/08Map/", "FpsGame.MapUtils"),
    ]:
        if key in p:
            return name
    return "other"


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    detail = sys.argv[1] if len(sys.argv) > 1 else None

    hits = []          # (slot, unit, relpath, lineno, code)
    for r in ROOTS:
        base = os.path.join(root, r)
        for dirpath, _, files in os.walk(base):
            for fn in files:
                if not fn.endswith(".cs"):
                    continue
                full = os.path.join(dirpath, fn)
                rel = os.path.relpath(full, root)
                with open(full, "r", encoding="utf-8-sig", errors="replace") as fp:
                    raw = fp.read()
                code_only = strip_comments(raw)
                for i, line in enumerate(code_only.split("\n"), 1):
                    if DECL.search(line):      # 注册点（实现方写槽位）不算"消费"
                        continue
                    for m in PAT.finditer(line):
                        hits.append((m.group(1), unit_of(rel), rel, i, line.strip()))

    if detail:
        rows = [h for h in hits if h[0] == detail]
        for slot, unit, rel, i, code in rows:
            print("%s:%d  [%s]  %s" % (rel, i, unit, code[:110]))
        print("---")
        print("%s total=%d" % (detail, len(rows)))
        return

    per_slot = Counter(h[0] for h in hits)
    print("=== 每个槽位的使用点数 ===")
    for s in SLOTS:
        print("  %-8s %3d" % (s, per_slot.get(s, 0)))
    print("  total    %3d" % len(hits))

    print("\n=== 谁在用（槽位 x 消费层）===")
    grid = defaultdict(Counter)
    for slot, unit, rel, i, code in hits:
        grid[unit][slot] += 1
    for unit in sorted(grid, key=lambda u: -sum(grid[u].values())):
        row = " ".join("%s=%d" % (s, grid[unit][s]) for s in SLOTS if grid[unit][s])
        print("  %-16s %3d  |  %s" % (unit, sum(grid[unit].values()), row))

    print("\n=== 涉及文件数 ===")
    print("  files = %d" % len(set(h[2] for h in hits)))


if __name__ == "__main__":
    main()
