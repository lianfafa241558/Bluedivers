# -*- coding: utf-8 -*-
"""
第二批修复（Assets/Editor，ProjectEditors 程序集）：
A. 补 using：
   - EditorOverride.cs / EditorOverrideInLine.cs（namespace Pixeye.Unity）→ using FPSGame.EditorExt;（InlineFieldDrawer/CustomLabelDrawer）
   - WeaponUpgradeEditorWindow.cs（namespace FPSGame.Game.Editor）→ using FPSGame.EditorExt;（SOPickerPopup<>）
   - EnemyStatEditorWindow.cs（namespace FPSGame.EditorExt）→ using FPSGame.GameContract;（DifficultyEnum）
B. 命名空间遮蔽：位于 `FPSGame.*` 内的文件裸写 `Attribute.X`（本意 System.Attribute）会被 `FPSGame.Attribute` 遮蔽 ⇒ 加 `System.` 前缀。

字节级读写，保 BOM 与换行符。
"""
import os
import re

ROOT = r"d:\Pro\Bluedivers"

ADD_USING = {
    r"Assets/Editor/Drawer/EditorOverride.cs": ["FPSGame.EditorExt"],
    r"Assets/Editor/Drawer/EditorOverrideInLine.cs": ["FPSGame.EditorExt"],
    r"Assets/Editor/WeaponUpgradeEditorWindow.cs": ["FPSGame.EditorExt"],
    r"Assets/Editor/EnemyStatEditorWindow.cs": ["FPSGame.GameContract"],
}

# 文件 -> 需要加 System. 前缀的成员
QUALIFY = {
    r"Assets/Editor/Drawer/InlineFieldDrawer.cs": ["Attribute\\."],
    r"Assets/Editor/Drawer/CustomLabelDrawer.cs": ["Attribute\\."],
}


def read(rel):
    path = os.path.join(ROOT, rel.replace("/", os.sep))
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    nl = "\r\n" if "\r\n" in text else "\n"
    return path, bom, text, nl


def write(path, bom, text, nl):
    data = text.encode("utf-8")
    if bom:
        data = b"\xef\xbb\xbf" + data
    open(path, "wb").write(data)


def is_top_using(line):
    if not line.startswith("using ") or line != line.lstrip():
        return False
    return line.rstrip().endswith(";")


def add_using(rel):
    path, bom, text, nl = read(rel)
    lines = text.split(nl)
    existing = {l.strip() for l in lines}
    to_add = [u for u in ADD_USING[rel] if ("using %s;" % u) not in existing]
    if not to_add:
        return rel, "skip"

    first_static = None
    last_using = None
    for i, l in enumerate(lines):
        if l.startswith("using ") and l.strip().startswith("using static "):
            if first_static is None:
                first_static = i
            continue
        if is_top_using(l):
            last_using = i
    assert last_using is not None, rel
    insert_at = first_static if (first_static is not None and first_static < last_using) else last_using + 1
    lines = lines[:insert_at] + ["using %s;" % u for u in to_add] + lines[insert_at:]
    write(path, bom, nl.join(lines), nl)
    return rel, "added " + ",".join(to_add)


def qualify(rel):
    path, bom, text, nl = read(rel)
    new, n = re.subn(r"(?<![\w.])Attribute\.", "System.Attribute.", text)
    if n:
        write(path, bom, new, nl)
    return rel, "qualified %d" % n


def main():
    for rel in ADD_USING:
        print("%-58s %s" % add_using(rel))
    for rel in QUALIFY:
        print("%-58s %s" % qualify(rel))


if __name__ == "__main__":
    main()
