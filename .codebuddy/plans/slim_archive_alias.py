# -*- coding: utf-8 -*-
"""瘦身 `ArchiveLoader.Archive` 别名：全量替换成 `ArchivesData_SO.Current`，并确保 using 齐备。

用法：
    python slim_archive_alias.py            # 只报告（dry-run）
    python slim_archive_alias.py --apply    # 落盘

为什么用脚本而不是 replace_in_file：
  27 处 / 14 文件，且要顺带插 using（须遵守项目规则：普通 using 排在 `using static`/别名之前，
  插在**同一个作用域**里 ⇒ 有 namespace 内 using 的文件要插进 namespace 内部）。
  字节级读写，**保住 UTF-8 BOM**（replace_in_file 会丢）。
"""
import os
import re
import sys

OLD = "ArchiveLoader.Archive"
NEW = "ArchivesData_SO.Current"
USING_LINE = "using FPSGame.Game;"

FILES = [
    r"Assets/Scripts/04UI/SelectMapWnd.cs",
    r"Assets/Scripts/04UI/VehicleWnd.cs",
    r"Assets/Scripts/04UI/SettingWnd.cs",
    r"Assets/Scripts/04UI/SelectRoleWnd.cs",
    r"Assets/Scripts/04UI/GameEndWnd.cs",
    r"Assets/Scripts/04UI/FrontWnd.cs",
    r"Assets/Scripts/04UI/BridgeWnd.cs",
    r"Assets/Scripts/04UI/ArmamentWnd.cs",
    r"Assets/Scripts/04UI/AirdropConfigWnd.cs",
    r"Assets/Scripts/01Manager/Global/TaskManager.cs",
    r"Assets/Scripts/01Manager/Global/RoomManager.cs",
    r"Assets/Scripts/01Manager/Global/RoleManagerBase.cs",
    r"Assets/Scripts/01Manager/Global/PropertyManager.cs",
    r"Assets/Scripts/01Manager/Bridge/BridgeRoleManager.cs",
]

BOM = b"\xef\xbb\xbf"
# 普通 using：行首可有缩进，`using A.B.C;`，不含 static / 别名 / var
RE_NORMAL_USING = re.compile(r"^[ \t]*using\s+[A-Za-z_][\w\.]*\s*;[ \t]*$", re.M)
RE_HAS_TARGET = re.compile(r"^[ \t]*using\s+FPSGame\.Game\s*;", re.M)
# `using static X;` 或别名 `using X = Y;` —— 普通 using 必须排在它们之前
RE_STATIC_OR_ALIAS = re.compile(r"^[ \t]*using\s+(static\s+|[A-Za-z_][\w\.]*\s*=)", re.M)


def ensure_using(text):
    """插入 `using FPSGame.Game;`，返回 (新文本, 是否插入)。"""
    if RE_HAS_TARGET.search(text):
        return text, False

    m_bad = RE_STATIC_OR_ALIAS.search(text)
    anchor = m_bad.start() if m_bad else None
    if anchor is None:
        ms = list(RE_NORMAL_USING.finditer(text))
        if not ms:
            return text, False
        m = ms[-1]
        insert_at = m.end()
        # 与"最后一个普通 using"同作用域 ⇒ 直接紧接其后
        return text[:insert_at] + "\n" + USING_LINE + text[insert_at:], True

    # 插在第一个 static/别名 using 之前，缩进与其对齐（通常为 namespace 内 4 空格）
    line_start = text.rfind("\n", 0, anchor) + 1
    indent = re.match(r"[ \t]*", text[line_start:anchor]).group(0)
    return text[:line_start] + indent + USING_LINE + "\n" + text[line_start:], True


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    apply = "--apply" in sys.argv
    total_repl = 0
    added = []
    for rel in FILES:
        path = os.path.join(root, rel.replace("\\", "/"))
        with open(path, "rb") as fp:
            raw = fp.read()
        had_bom = raw[:3] == BOM
        body = raw[3:] if had_bom else raw
        text = body.decode("utf-8")

        n = text.count(OLD)
        if n == 0:
            print("  skip (0)  " + rel)
            continue
        new_text = text.replace(OLD, NEW)
        new_text, got_using = ensure_using(new_text)
        if got_using:
            added.append(rel)
        total_repl += n
        print("%-58s repl=%-2d using+%s" % (rel, n, "Y" if got_using else "(已有)"))

        if apply:
            out = new_text.encode("utf-8")
            if had_bom:
                out = BOM + out
            with open(path, "wb") as fp:
                fp.write(out)

    print("---")
    print("files=%d  replacements=%d  using_added=%d  apply=%s" %
          (len(FILES), total_repl, len(added), apply))
    if added:
        print("插入 using 的文件：")
        for a in added:
            print("  " + a)


if __name__ == "__main__":
    main()
