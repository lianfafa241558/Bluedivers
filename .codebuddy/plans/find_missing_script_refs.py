# -*- coding: utf-8 -*-
"""定位「脚本引用解析不到」的资产（Missing Script 的静态判据）。

与 scan_missing_scripts.py 的区别：
  1. guid 索引同时纳入 `Assets/**`、`Packages/**`、`Library/PackageCache/**`，
     排除 TMP / URP / 内置包脚本造成的海量假阳性；
  2. 支持 `--under <相对路径>` 只扫描指定目录（加速定向排查）；
  3. 输出每个缺失 guid 的引用文件明细与「引用该 guid 的文件里是否含 m_Script」。

用法：
    python find_missing_script_refs.py
    python find_missing_script_refs.py --under Assets/Resources/GameData
    python find_missing_script_refs.py --under Assets/Resources/GameData --detail 30
"""
import os
import re
import sys
from collections import defaultdict

META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
SCRIPT_REF = re.compile(r"m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})")
EXT = (".prefab", ".unity", ".asset", ".controller", ".playable")

META_ROOTS = ("Assets", "Packages", os.path.join("Library", "PackageCache"))


def repo_root():
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def collect_guids(root):
    guids = {}
    for sub in META_ROOTS:
        base = os.path.join(root, sub)
        if not os.path.isdir(base):
            continue
        for dirpath, _dirnames, files in os.walk(base):
            for f in files:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dirpath, f)
                try:
                    with open(p, "r", encoding="utf-8-sig", errors="replace") as fp:
                        m = META_GUID.search(fp.read())
                except OSError:
                    continue
                if m:
                    guids.setdefault(m.group(1), os.path.relpath(p, root))
    return guids


def main():
    root = repo_root()
    detail = 10
    scope = None
    argv = sys.argv[1:]
    for i, a in enumerate(argv):
        if a == "--under" and i + 1 < len(argv):
            scope = argv[i + 1]
        if a == "--detail" and i + 1 < len(argv):
            detail = int(argv[i + 1])

    guids = collect_guids(root)
    scan_base = os.path.join(root, scope) if scope else os.path.join(root, "Assets")
    print("guid 索引条目 = %d" % len(guids))
    print("扫描范围 = %s" % os.path.relpath(scan_base, root))

    missing = defaultdict(list)
    scanned = 0
    for dirpath, _dirnames, files in os.walk(scan_base):
        for f in files:
            if not f.endswith(EXT):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, root)
            scanned += 1
            try:
                with open(p, "r", encoding="utf-8-sig", errors="replace") as fp:
                    text = fp.read()
            except OSError:
                continue
            for file_id, g in SCRIPT_REF.findall(text):
                if g not in guids:
                    missing[(g, file_id)].append(rel)

    print("扫描资产数 = %d" % scanned)
    print("解析不到的脚本引用 = %d 条（%d 个不同 guid）" % (sum(len(v) for v in missing.values()), len(missing)))
    print("---")
    for (g, file_id), files in sorted(missing.items(), key=lambda kv: -len(kv[1])):
        print("guid=%s fileID=%s  引用 %d 次" % (g, file_id, len(files)))
        for x in sorted(set(files))[:detail]:
            print("      " + x)


if __name__ == "__main__":
    main()
