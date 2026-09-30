# -*- coding: utf-8 -*-
"""
批量补齐缺失的 using（本次 24 条 CS0103/CS1061 全部是"类型已有命名空间、调用点缺 using"）。

安全要点：
- 字节级读写，保留 BOM（utf-8-sig）与原有换行符（CRLF/LF），避免 replace_in_file 丢 BOM 的问题。
- 插入点：顶层（行首无缩进）using 块内；**插在第一个顶层 `using static` 之前**（坑 6：静态导入引用具体类型）。
- 已存在同名 using 则跳过；不删任何行、不缩进。
"""
import os
import sys

ROOT = r"d:\Pro\Bluedivers"

# 文件 -> 需要补的命名空间
PLAN = {
    r"Assets/Scripts/10_Effect/VFX/VFXHaloEffect.cs": ["FPSGame.Gameplay"],
    r"Assets/Scripts/04UI/DeathUI.cs": ["FPSGame.Core", "FPSGame.Gameplay"],
    r"Assets/Scripts/04UI/LoadWnd.cs": ["FPSGame.Utils"],
    r"Assets/Scripts/04UI/MissionCompleteWnd.cs": ["FPSGame.Gameplay"],
    r"Assets/Scripts/04UI/GameEndWnd.cs": ["FPSGame.GameContract"],
    r"Assets/Scripts/04UI/Window.cs": ["FPSGame.Gameplay"],
    r"Assets/Scripts/04UI/PlotSubtitles.cs": ["FPSGame.Utils"],
    r"Assets/Scripts/04UI/UI/CanvasController.cs": ["FPSGame.Gameplay"],
    r"Assets/Scripts/04UI/UI/CrosshairManagerBase.cs": ["FPSGame.Utils"],
    r"Assets/Scripts/04UI/UI/MouseMoveEffect.cs": ["FPSGame.Gameplay"],
    r"Assets/Scripts/04UI/UI/Subtitle/SubtitleSpecUnit.cs": ["FPSGame.Gameplay"],
    r"Assets/Scripts/04UI/SettingWnd.cs": ["FPSGame.GameContract"],
}


def is_top_using(line):
    """顶层普通 using（行首无缩进、以 ; 结尾、不含 = 的别名形式）。"""
    if not line.startswith("using "):
        return False
    if line != line.lstrip():
        return False
    return line.rstrip().endswith(";")


def patch(rel):
    path = os.path.join(ROOT, rel.replace("/", os.sep))
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"

    lines = text.split(nl)
    wanted = PLAN[rel]

    existing = {l.strip() for l in lines}
    to_add = [u for u in wanted if ("using %s;" % u) not in existing]
    if not to_add:
        return rel, "skip (already ok)"

    # 定位插入点
    first_static = None
    last_using = None
    for i, l in enumerate(lines):
        s = l.strip()
        if l.startswith("using ") and s.startswith("using static "):
            first_static = i if first_static is None else first_static
            continue
        if is_top_using(l):
            last_using = i
    assert last_using is not None, "no top-level using found in " + rel

    insert_at = first_static if (first_static is not None and first_static < last_using) else last_using + 1
    new_lines = lines[:insert_at] + ["using %s;" % u for u in to_add] + lines[insert_at:]

    out = nl.join(new_lines)
    data = out.encode("utf-8")
    if bom:
        data = b"\xef\xbb\xbf" + data
    open(path, "wb").write(data)
    return rel, "added " + ", ".join(to_add) + " @line %d" % (insert_at + 1)


def main():
    for rel in PLAN:
        print("%-70s %s" % patch(rel))


if __name__ == "__main__":
    main()
