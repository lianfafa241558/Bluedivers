# -*- coding: utf-8 -*-
"""从指定根资源出发做 guid 依赖闭包，报告闭包内「解析不到的 m_Script」（= Missing Script 判据）。

背景：`Resources.LoadAll<T>(path)` 不只看目标文件夹里的对象，还会把它们的**依赖对象**一并
反序列化（SO 的 prefab 引用、prefab 的材质/子 prefab 引用……）。所以报在 LoadAll 栈上的
`The referenced script (Unknown) on this Behaviour is missing!` 往往来自依赖链上的某个 prefab。

用法：
    python dep_closure_missing.py Assets/Resources/GameData/Airdrop
    python dep_closure_missing.py Assets/Resources/GameData/Airdrop --max 5000
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
META_ROOTS = ("Assets", "Packages", os.path.join("Library", "PackageCache"))
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
GUID_REF = re.compile(r"guid:\s*([0-9a-f]{32})")
SCRIPT_REF = re.compile(r"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})")
TEXT_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller", ".playable", ".anim",
            ".mixer", ".shadergraph", ".shadersubgraph", ".preset", ".guiskin",
            ".fontsettings", ".lighting", ".terrainlayer", ".brush")


def build_guid_index():
    idx = {}
    for sub in META_ROOTS:
        base = os.path.join(ROOT, sub)
        if not os.path.isdir(base):
            continue
        for dp, _dn, fs in os.walk(base):
            for f in fs:
                if not f.endswith(".meta"):
                    continue
                p = os.path.join(dp, f)
                try:
                    with open(p, encoding="utf-8-sig", errors="replace") as fp:
                        m = META_GUID.search(fp.read(400))
                except OSError:
                    continue
                if m:
                    idx.setdefault(m.group(1), p[:-5])
    return idx


def read_text(path):
    if not path.lower().endswith(TEXT_EXT):
        return None
    try:
        with open(path, encoding="utf-8-sig", errors="replace") as fp:
            return fp.read()
    except OSError:
        return None


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    limit = 4000
    for i, a in enumerate(sys.argv[1:]):
        if a == "--max" and i + 2 <= len(sys.argv) - 1:
            limit = int(sys.argv[i + 2])
    if not args:
        print(__doc__)
        return
    seeds = []
    for a in args:
        p = a if os.path.isabs(a) else os.path.join(ROOT, a)
        if os.path.isdir(p):
            for dp, _dn, fs in os.walk(p):
                seeds += [os.path.join(dp, f) for f in fs
                          if f.lower().endswith(TEXT_EXT)]
        else:
            seeds.append(p)

    idx = build_guid_index()
    print("guid 索引条目 = %d" % len(idx))
    print("种子文件 = %d" % len(seeds))

    seen = set()
    parent = {}
    queue = [(p, None) for p in seeds]
    broken = {}          # (script_guid, owner_abspath) -> 1
    while queue:
        p, par = queue.pop()
        rp = os.path.normcase(os.path.abspath(p))
        if rp in seen or len(seen) > limit:
            continue
        seen.add(rp)
        parent[rp] = par
        text = read_text(p)
        if text is None:
            continue
        for g in set(SCRIPT_REF.findall(text)):
            if g.startswith("0000000000000000"):
                continue   # Unity 内建组件
            if g not in idx:
                broken[(g, os.path.abspath(p))] = 1
        for g in set(GUID_REF.findall(text)):
            tgt = idx.get(g)
            if tgt and os.path.normcase(tgt) not in seen:
                queue.append((tgt, p))

    print("闭包内资产数 = %d（上限 %d）" % (len(seen), limit))
    print("解析不到的 m_Script 组合 = %d" % len(broken))
    print("---")
    by_guid = {}
    for (g, owner) in broken:
        by_guid.setdefault(g, []).append(owner)
    for g, owners in sorted(by_guid.items(), key=lambda kv: -len(kv[1])):
        print("脚本 guid = %s   出现在 %d 个资产" % (g, len(owners)))
        for o in sorted(owners)[:12]:
            print("  资产: " + os.path.relpath(o, ROOT).replace("\\", "/"))
            chain = []
            cur = os.path.normcase(os.path.abspath(o))
            while cur in parent and parent[cur]:
                chain.append(os.path.relpath(parent[cur], ROOT).replace("\\", "/"))
                cur = os.path.normcase(os.path.abspath(parent[cur]))
            for step in reversed(chain):
                print("      <-- " + step)


if __name__ == "__main__":
    main()
