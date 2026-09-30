# -*- coding: utf-8 -*-
"""
GUID 完整性检查（2026-10-01，P5-3 过程中发现"文件系统搬文件没保住 GUID"后的止损工具）

背景：此前一直用 `Move-Item <file> <file>.meta` 搬 .cs，以为"保 guid ⇒ 引用不断"。
实测：Unity 在**资产库还没刷新**时看到"新路径的 .meta 声称自己拥有旧路径的 guid"，
判定为冲突（控制台报 `GUID [...] conflicts with: <old path> (current owner)`），
于是给新路径**重新分配了一个 guid**、旧条目仍留在库里（指向不存在的文件）
⇒ 若该脚本是 MonoBehaviour，挂在 prefab/场景上的引用就会变成 **Missing Script**。

本脚本（纯文件分析，不依赖 Unity）：
  1. 用 `Assets/**/*.meta` 建 guid -> 路径 映射；
  2. 扫 `*.prefab` / `*.unity` / `*.asset`，抽出所有 `m_Script: {fileID: ..., guid: X}` 的 X
     以及其它 `guid: X` 引用；
  3. 报出**解析不到**的 guid（= 潜在 Missing Script / 丢失引用），并列出引用它的文件与行号。
"""
import os
import re
import sys
import collections

ROOT = os.path.join(os.getcwd(), "Assets")
GUID_LINE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
SCRIPT_LINE = re.compile(r"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})")
SCAN_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller", ".anim")


def main():
    out = sys.stdout
    guid2path = {}
    dup = collections.defaultdict(list)
    for dirpath, _dn, fns in os.walk(ROOT):
        for fn in fns:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(dirpath, fn)
            try:
                with open(p, encoding="utf-8", errors="ignore") as f:
                    head = f.read(400)
            except OSError:
                continue
            m = re.search(r"guid:\s*([0-9a-fA-F]{32})", head)
            if not m:
                continue
            g = m.group(1)
            full = p[: -len(".meta")].replace("\\", "/")
            if g in guid2path:
                dup[g].append(full)
            else:
                guid2path[g] = full
    out.write("meta 数 = %d，其中重复 guid 的 %d 个\n" % (len(guid2path), len(dup)))
    for g, ps in list(dup.items())[:10]:
        out.write("  DUP %s -> %s\n" % (g, ps))

    missing_script = collections.defaultdict(set)   # guid -> {asset}
    missing_ref = collections.defaultdict(set)
    total_script_refs = 0
    for dirpath, _dn, fns in os.walk(ROOT):
        for fn in fns:
            if not fn.lower().endswith(SCAN_EXT):
                continue
            p = os.path.join(dirpath, fn)
            try:
                with open(p, encoding="utf-8", errors="ignore") as f:
                    text = f.read()
            except OSError:
                continue
            rel = p.replace("\\", "/")
            for line in text.split("\n"):
                ms = SCRIPT_LINE.search(line)
                if ms:
                    total_script_refs += 1
                    g = ms.group(1)
                    # m_Script 里也有内建组件（UnityEngine 的 guid = 0000000000000000e000000000000000 等）
                    if g not in guid2path and not g.startswith("0000000000000000"):
                        missing_script[g].add(rel)
                for mg in GUID_LINE.finditer(line):
                    g = mg.group(1)
                    if g not in guid2path and not g.startswith("0000000000000000"):
                        missing_ref[g].add(rel)

    out.write("\n===== 缺失的 m_Script（= Missing Script 风险） =====\n")
    out.write("引用总数 %d，解析不到 %d 个 guid\n" % (total_script_refs, len(missing_script)))
    for g, files in sorted(missing_script.items(), key=lambda kv: -len(kv[1])):
        out.write("  %s  ← %d 个资产：%s\n" % (g, len(files), ", ".join(sorted(files)[:6])))

    extra = {g: f for g, f in missing_ref.items() if g not in missing_script}
    out.write("\n===== 其它解析不到的 guid 引用（非 m_Script） =====\n")
    out.write("%d 个\n" % len(extra))
    for g, files in sorted(extra.items(), key=lambda kv: -len(kv[1]))[:40]:
        out.write("  %s  ← %d 个资产：%s\n" % (g, len(files), ", ".join(sorted(files)[:4])))


if __name__ == "__main__":
    main()
