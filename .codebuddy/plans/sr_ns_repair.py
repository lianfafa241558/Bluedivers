# -*- coding: utf-8 -*-
"""
修复 [SerializeReference] 命名空间失配（审计脚本 sr_ns_audit.py 的修复版）。

背景：给 .cs 补命名空间后，资产 YAML 里旧的 `type: {class: X, ns: , asm: A}` 解析不到类型
⇒ Unity 报 "Missing types referenced from component ..."，序列化数据静默丢失。

用法：
    python sr_ns_repair.py            # 只统计
    python sr_ns_repair.py --apply    # 落盘

实现：全程字节级（部分 .asset 是二进制序列化，不能按文本解码）。
"""
import os
import re
import sys
from collections import defaultdict

ASSETS = r"d:\Pro\Bluedivers\Assets"
YAML_EXT = (".prefab", ".unity", ".asset")
LINE_PAT = re.compile(rb"^(\s*type:\s*\{class:\s*)([^,}]+)(,\s*ns:\s*)([^,}]*)(,\s*asm:\s*)([^,}]+)(\}\s*)$")

DECL = re.compile(r"^\s*(?:public|internal|private|protected)?\s*(?:static\s+|abstract\s+|sealed\s+|partial\s+)*"
                  r"(class|struct|enum|interface)\s+([A-Za-z_][\w]*)", re.M)
NSDECL = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)

APPLY = "--apply" in sys.argv


def collect_types():
    table = defaultdict(set)
    for dirpath, _, filenames in os.walk(ASSETS):
        low = dirpath.replace("\\", "/")
        if "/MackySoft/" in low or "/Plugins/" in low:
            continue
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            try:
                t = open(os.path.join(dirpath, fn), "rb").read().decode("utf-8-sig")
            except Exception:
                continue
            m = NSDECL.search(t)
            ns = m.group(1) if m else ""
            for _, name in DECL.findall(t):
                table[name].add(ns)
    return table


def main():
    table = collect_types()
    changed = {}
    stat = defaultdict(int)
    skipped_binary = 0

    for dirpath, _, filenames in os.walk(ASSETS):
        for fn in filenames:
            if not fn.endswith(YAML_EXT):
                continue
            p = os.path.join(dirpath, fn)
            raw = open(p, "rb").read()
            nl = b"\r\n" if b"\r\n" in raw else b"\n"
            lines = raw.split(nl)
            dirty = False
            for i, line in enumerate(lines):
                m = LINE_PAT.match(line)
                if not m:
                    continue
                cls = m.group(2).strip().decode("ascii", "ignore")
                ns = m.group(4).strip()
                owner = cls.split("/")[0]
                real = {x for x in table.get(owner, set()) if x}
                if ns or not real:
                    continue
                if len(real) > 1:
                    print("!! 歧义（%s ∈ %s），跳过：%s" % (owner, sorted(real), p))
                    continue
                lines[i] = m.group(1) + m.group(2) + m.group(3) + sorted(real)[0].encode() + m.group(5) + m.group(6) + m.group(7)
                dirty = True
                stat["%s -> %s" % (cls, sorted(real)[0])] += 1
            if dirty:
                changed[p] = nl.join(lines)

    for k in sorted(stat):
        print("%-45s %d 处" % (k, stat[k]))
    print("涉及资产文件数:", len(changed), "（跳过的二进制文件:%d）" % skipped_binary)

    if APPLY:
        for p, data in changed.items():
            open(p, "wb").write(data)
        print("已写入。")
    else:
        print("（未写入，加 --apply 落盘）")


if __name__ == "__main__":
    main()
