# -*- coding: utf-8 -*-
"""检查/补回 UTF-8 BOM（replace_in_file 会丢 BOM，规范要求 UTF-8 with BOM）。

用法：
    python check_bom.py                 # 只检查（列哪些文件缺 BOM）
    python check_bom.py --fix           # 检查并补回 BOM（字节级，不动其它内容）

默认扫描「本次改动的文件」清单，可通过命令行传路径覆盖。
"""
import sys
import os

DEFAULT_FILES = [
    r"Assets/Scripts/00Core/GameRootBase.cs",
    r"Assets/Scripts/00GameContract/ServiceLocator.cs",
    r"Assets/Scripts/01Manager/Global/GameRoot.cs",
    r"Assets/Scripts/01Manager/Global/ArchiveSvc.cs",
    r"Assets/Scripts/01Manager/Global/NetManager.cs",
    r"Assets/Scripts/01Manager/Global/ResSvc.cs",
    r"Assets/Scripts/01Manager/Global/RoomManager.cs",
    r"Assets/Scripts/01Manager/Global/TaskManager.cs",
    r"Assets/Scripts/01Manager/Global/WndManager.cs",
    r"Assets/Scripts/01Manager/Battle/BattleManager.cs",
    r"Assets/Scripts/01Manager/Battle/PathRequestManager.cs",
    r"Assets/Scripts/01Manager/Battle/VFXManager.cs",
]

BOM = b"\xef\xbb\xbf"


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    fix = "--fix" in sys.argv
    files = args if args else [os.path.join(root, f) for f in DEFAULT_FILES]

    missing = []
    for path in files:
        if not os.path.exists(path):
            print("MISSING FILE  " + path)
            continue
        with open(path, "rb") as fp:
            head = fp.read(3)
        has_bom = head == BOM
        if not has_bom:
            missing.append(path)
        if not has_bom and fix:
            with open(path, "rb") as fp:
                data = fp.read()
            with open(path, "wb") as fp:
                fp.write(BOM + data)
            print("BOM ADDED     " + os.path.relpath(path, root))
        else:
            print(("BOM OK        " if has_bom else "BOM MISSING   ") + os.path.relpath(path, root))

    print("---")
    print("total=%d  missing=%d  fix=%s" % (len(files), len(missing), fix))


if __name__ == "__main__":
    main()
