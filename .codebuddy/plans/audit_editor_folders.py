# -*- coding: utf-8 -*-
"""全仓扫描：位于 asmdef 程序集内部、却命名为 Editor 的文件夹（Unity 官方：asmdef 下 Editor 文件夹失去 editor-only 特殊待遇）。
同时列出这些文件是否用 #if UNITY_EDITOR 守卫了 using UnityEditor。"""
import os, re, io, json

ROOT = r"d:\Pro\Bluedivers\Assets"
asmdefs = []
for r, ds, fs in os.walk(ROOT):
    for f in fs:
        if f.endswith(".asmdef"):
            p = os.path.join(r, f)
            with io.open(p, "r", encoding="utf-8-sig", errors="replace") as fh:
                try:
                    j = json.load(fh)
                except Exception:
                    j = {}
            asmdefs.append((r, f, j.get("name", "?"), j.get("includePlatforms", []), j.get("rootNamespace", "")))

print("=" * 90)
print("asmdef 总数:", len(asmdefs))
editor_only = [a for a in asmdefs if a[3] == ["Editor"]]
print("其中 includePlatforms=['Editor'] 的:", [a[2] for a in editor_only])

# 找所有名为 Editor 的目录
targets = []
for r, ds, fs in os.walk(ROOT):
    if os.path.basename(r) == "Editor":
        targets.append(r)

print("=" * 90)
print("名为 Editor 的目录共:", len(targets))
print("-" * 90)
for d in sorted(targets):
    # 本目录或子目录是否有自己的 asmdef
    own = [a for a in asmdefs if a[0] == d or a[0].startswith(d + os.sep)]
    # 找最近的祖先 asmdef
    anc = None
    cur = os.path.dirname(d)
    while len(cur) > len(ROOT):
        hit = [a for a in asmdefs if a[0] == cur]
        if hit:
            anc = hit[0]
            break
        cur = os.path.dirname(cur)
    tag = "OK(独立Editor程序集)" if (own and own[0][3] == ["Editor"]) else ("!!!" if (own or anc) else "-")
    print("%-72s %s" % (os.path.relpath(d, ROOT), tag))
    if own:
        for a in own:
            print("        own asmdef: %s includePlatforms=%s" % (a[2], a[3]))
    if anc:
        print("        祖先 asmdef: %s  (includePlatforms=%s)" % (anc[2], anc[3]))
    if tag == "!!!":
        # 列文件与守卫情况
        for r2, ds2, fs2 in os.walk(d):
            for f2 in fs2:
                if not f2.endswith(".cs"):
                    continue
                p2 = os.path.join(r2, f2)
                with io.open(p2, "r", encoding="utf-8-sig", errors="replace") as fh:
                    txt = fh.read()
                if re.search(r"^\s*using\s+UnityEditor", txt, re.M):
                    lines = txt.split("\n")
                    for i, ln in enumerate(lines):
                        if re.match(r"^\s*using\s+UnityEditor", ln):
                            ok = any(lines[j].strip().startswith("#if") and "UNITY_EDITOR" in lines[j]
                                     for j in range(max(0, i - 5), i))
                            print("            %-50s line %d %s" % (f2, i + 1, "guarded" if ok else "*** 未守卫 ***"))
                            break
                else:
                    print("            %-50s 无 UnityEditor 引用" % f2)
