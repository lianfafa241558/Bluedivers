# -*- coding: utf-8 -*-
"""
GUID 审计 / 修复（2026-10-01）

问题：这一系列 asmdef 拆分都用 `Move-Item <x.cs> <x.cs.meta>` 搬文件，以为"带 .meta 就保 guid"。
实测：Unity 在资产库未及时刷新时会把"新路径声称拥有旧 guid"判为冲突
（控制台：`GUID [...] conflicts with: <old path> (current owner)`），于是给新路径**分配新 guid**，
旧 guid 仍被 prefab/场景引用 ⇒ **Missing Script**。

判据（决定性）：拿 git HEAD 里该文件的 `.meta` 原 guid，与工作区同名文件的新 `.meta` guid 比对；
不一致就再查"旧 guid 是否仍被 *.prefab/*.unity/*.asset 引用"。

用法：
    python p53_guid_repair.py           # 只报告
    python p53_guid_repair.py --apply   # 把旧 guid 写回新位置的 .meta（恢复标识，修好引用）
"""
import os
import re
import subprocess
import sys
import collections

ROOT = os.getcwd()
ASSETS = os.path.join(ROOT, "Assets")
GUID_RE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
REF_EXT = (".prefab", ".unity", ".asset")


def git(args):
    r = subprocess.run(["git"] + args, capture_output=True, text=True, cwd=ROOT, encoding="utf-8", errors="ignore")
    return r.stdout if r.returncode == 0 else ""


def cur_files():
    out = []
    for dirpath, _dn, fns in os.walk(ASSETS):
        for fn in fns:
            if fn.endswith(".cs"):
                p = os.path.join(dirpath, fn)
                out.append(os.path.relpath(p, ROOT).replace("\\", "/"))
    return out


def read_guid(path):
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8", errors="ignore") as f:
        m = GUID_RE.search(f.read(400))
    return m.group(1) if m else None


def main():
    apply_fix = "--apply" in sys.argv
    head = set(x for x in git(["ls-tree", "-r", "HEAD", "--name-only"]).split("\n") if x.endswith(".cs"))
    by_base = collections.defaultdict(list)
    for h in head:
        by_base[os.path.basename(h)].append(h)

    moved = []      # (old_path, new_path, old_guid, new_guid)
    samepath_mismatch = []
    unknown = []
    for cf in cur_files():
        new_guid = read_guid(os.path.join(ROOT, cf) + ".meta")
        meta = os.path.join(ROOT, cf) + ".meta"
        old_guid = read_guid(meta)
        if cf in head:
            og = None
            r = subprocess.run(["git", "show", "HEAD:" + cf + ".meta"], capture_output=True, text=True,
                               cwd=ROOT, encoding="utf-8", errors="ignore")
            if r.returncode == 0:
                m = GUID_RE.search(r.stdout)
                og = m.group(1) if m else None
            if og and new_guid and og != new_guid:
                samepath_mismatch.append((cf, og, new_guid))
            continue
        cands = by_base.get(os.path.basename(cf), [])
        cands = [c for c in cands if c not in head or True]
        if len(cands) == 1:
            r = subprocess.run(["git", "show", "HEAD:" + cands[0] + ".meta"], capture_output=True, text=True,
                               cwd=ROOT, encoding="utf-8", errors="ignore")
            og = None
            if r.returncode == 0:
                m = GUID_RE.search(r.stdout)
                og = m.group(1) if m else None
            if og and new_guid and og != new_guid:
                moved.append((cands[0], cf, og, new_guid))
        else:
            unknown.append((cf, new_guid, len(cands)))

    print("同路径但 guid 变了（异常）: %d" % len(samepath_mismatch))
    for p, og, ng in samepath_mismatch:
        print("   %s  %s -> %s" % (p, og, ng))

    print("\n搬过位置且 guid 变了（本次审计重点）: %d" % len(moved))

    # 旧 guid 是否仍被资产引用
    ref_index = collections.defaultdict(list)
    for dirpath, _dn, fns in os.walk(ASSETS):
        for fn in fns:
            if not fn.lower().endswith(REF_EXT):
                continue
            p = os.path.join(dirpath, fn)
            try:
                with open(p, encoding="utf-8", errors="ignore") as f:
                    t = f.read()
            except OSError:
                continue
            for g in set(GUID_RE.findall(t)):
                ref_index[g].append(os.path.relpath(p, ROOT).replace("\\", "/"))

    broken = []
    for old_path, new_path, og, ng in sorted(moved):
        refs = ref_index.get(og, [])
        flag = "!! 被引用 %d 处" % len(refs) if refs else "（未被引用，无实际影响）"
        print("   %-58s -> %s\n        old=%s new=%s  %s" % (old_path, new_path, og, ng, flag))
        if refs:
            broken.append((old_path, new_path, og, ng, refs))
            for r_ in refs[:5]:
                print("        ← %s" % r_)

    print("\n===== 汇总 =====")
    print("搬迁文件 %d 个，其中 guid 变化 %d 个，**旧 guid 仍被引用（= Missing Script）%d 个**"
          % (len(moved), len(moved), len(broken)))

    if unknown:
        print("\n无法唯一匹配旧路径的（新文件或重名）: %d" % len(unknown))
        for cf, ng, n in unknown[:20]:
            print("   %s  (候选 %d)" % (cf, n))

    if apply_fix and broken:
        print("\n===== 修复：把旧 guid 写回新位置 .meta =====")
        for old_path, new_path, og, ng, _refs in broken:
            meta = os.path.join(ROOT, new_path) + ".meta"
            with open(meta, encoding="utf-8") as f:
                t = f.read()
            t2 = t.replace("guid: " + ng, "guid: " + og, 1)
            with open(meta, "w", encoding="utf-8", newline="\n") as f:
                f.write(t2)
            print("   %s : %s -> %s" % (new_path, ng, og))
        print("已修 %d 个 .meta（Unity 刷新后会把旧 guid 指到新路径）" % len(broken))


if __name__ == "__main__":
    main()
