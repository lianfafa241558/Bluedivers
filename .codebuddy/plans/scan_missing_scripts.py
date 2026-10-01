# -*- coding: utf-8 -*-
"""扫描 prefab / 场景里"解析不到的脚本引用"（Missing Script 的静态判据）。

原理：收集 `Assets/**/*.meta` 里所有 guid；再扫 *.prefab/*.unity 的
`m_Script: {fileID: 11500000, guid: XXX, type: 3}`；guid 不在集合里 ⇒ 该组件挂了。

用法：
    python scan_missing_scripts.py            # 汇总
    python scan_missing_scripts.py --detail   # 列出每个缺失 guid 所在文件与次数
"""
import os
import re
import sys
from collections import defaultdict

META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
SCRIPT_REF = re.compile(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})")
EXT = (".prefab", ".unity", ".asset")


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    assets = os.path.join(root, "Assets")
    detail = "--detail" in sys.argv

    guids = set()
    meta_of = {}
    for dirpath, dirnames, files in os.walk(assets):
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
                guids.add(m.group(1))
                meta_of[m.group(1)] = os.path.relpath(p, root)

    missing = defaultdict(list)
    for dirpath, dirnames, files in os.walk(assets):
        for f in files:
            if not f.endswith(EXT):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, root)
            try:
                with open(p, "r", encoding="utf-8-sig", errors="replace") as fp:
                    text = fp.read()
            except OSError:
                continue
            for g in SCRIPT_REF.findall(text):
                if g not in guids:
                    missing[g].append(rel)

    print("meta 里 guid 总数 = %d" % len(guids))
    print("解析不到的脚本 guid 数 = %d" % len(missing))
    total = sum(len(v) for v in missing.values())
    print("受影响资产引用条数 = %d" % total)
    print("---")
    for g, files in missing.items():
        print("%s  被引用 %d 次" % (g, len(files)))
        if detail:
            for x in files[:10]:
                print("      " + x)


if __name__ == "__main__":
    main()
