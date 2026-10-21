# -*- coding: utf-8 -*-
"""打印一个 prefab 的 GameObject 层级 + 每个物体上的脚本组件（按 .meta 的 guid 反查类名）。

用法： python .codebuddy/plans/dump_prefab_tree.py Assets/Resources/Prefabs/Airdrop/ShieldBag.prefab
只读 Unity YAML，不引第三方库（用正则切文档块）。嵌套 prefab 实例只标出实例根，不展开。
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GUID2NAME = {}


def build_guid_map():
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".cs.meta"):
                continue
            p = os.path.join(dirpath, f)
            try:
                txt = open(p, encoding="utf-8", errors="ignore").read()
            except OSError:
                continue
            m = re.search(r"guid:\s*([0-9a-f]{32})", txt)
            if m:
                GUID2NAME[m.group(1)] = f[:-5]


DOC = re.compile(r"^--- !u!(\d+) &(\d+).*$", re.M)


def parse(path):
    txt = open(path, encoding="utf-8", errors="ignore").read()
    heads = list(DOC.finditer(txt))
    docs = {}
    for i, m in enumerate(heads):
        end = heads[i + 1].start() if i + 1 < len(heads) else len(txt)
        docs[m.group(2)] = (int(m.group(1)), txt[m.start():end])
    return docs


def refid(text):
    m = re.search(r"fileID:\s*(-?\d+)", text)
    return int(m.group(1)) if m else 0


def field(block, name):
    m = re.search(r"^\s*%s:\s*(.*)$" % re.escape(name), block, re.M)
    return m.group(1).strip() if m else None


def main():
    if len(sys.argv) < 2:
        print("usage: dump_prefab_tree.py <prefab>")
        return
    path = sys.argv[1]
    build_guid_map()
    docs = parse(path)

    # GameObject id -> (name, [component ids], is_prefab_instance_root)
    gos = {}
    for oid, (cls, block) in docs.items():
        if cls != 1:
            continue
        name = field(block, "m_Name") or "?"
        comps = re.findall(r"- component:\s*\{fileID:\s*(\d+)\}", block)
        gos[oid] = [oid, name, comps, False]
    # Transform id -> (go id, father id, prefabInstance id)
    trs = {}
    for oid, (cls, block) in docs.items():
        if cls not in (4, 224):
            continue
        go = field(block, "m_GameObject")
        father = field(block, "m_Father")
        pi = field(block, "m_PrefabInstance")
        if not go or not father:
            continue
        trs[oid] = (refid(go), refid(father), pi)

    def comp_names(comp_ids, depth):
        out = []
        for cid in comp_ids:
            if cid not in docs:
                out.append("?")
                continue
            cls, block = docs[cid]
            if cls == 114:
                g = re.search(r"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-f]{32})", block)
                nm = GUID2NAME.get(g.group(1), "guid:" + g.group(1)[:8]) if g else "MonoBehaviour?"
                out.append(nm)
            else:
                out.append("Transform" if cls in (4, 224) else "class%d" % cls)
        return out

    def walk(father_id, depth):
        for tr_id, (go_id, father, pi) in trs.items():
            if father != father_id or go_id not in gos:
                continue
            _, name, comps, _ = gos[go_id]
            names = comp_names(comps, depth)
            tag = "  [嵌套prefab] " + (pi or "") if pi and pi != "{fileID: 0}" else ""
            print("%s%s%s" % ("  " * depth, name, tag))
            if names:
                print("%s  <- %s" % ("  " * depth, ", ".join(names)))
            walk(tr_id, depth + 1)

    print("== %s ==" % path)
    walk(0, 0)


if __name__ == "__main__":
    main()
