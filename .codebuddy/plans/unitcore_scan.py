# -*- coding: utf-8 -*-
"""扫描 05UnitCore 目录：行数 / 命名空间 / using / 类型声明 / 引用者数量。"""
import os, re, io, sys, subprocess

sys.stdout.reconfigure(encoding="utf-8")

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DIR = os.path.join(ROOT, "Assets", "Scripts", "05UnitCore")

rows = []
for f in sorted(os.listdir(DIR)):
    if not f.endswith(".cs"):
        continue
    p = os.path.join(DIR, f)
    txt = io.open(p, encoding="utf-8-sig").read()
    lines = txt.splitlines()
    ns = re.findall(r"^\s*namespace\s+([\w\.]+)", txt, re.M)
    types = re.findall(
        r"^\s*(?:public|internal|sealed|abstract|static|partial|\s)*\b(?:class|interface|struct|enum)\s+(\w+)",
        txt, re.M)
    rows.append((f, len(lines), ",".join(sorted(set(ns))), ",".join(types)))

print(f"{'file':30s} {'lines':>5s}  {'types':40s} namespace")
for f, n, ns, t in rows:
    print(f"{f:30s} {n:5d}  {t[:40]:40s} {ns}")
print("total files:", len(rows), " total lines:", sum(r[1] for r in rows))

# 引用者统计：在整个 Assets 里搜 "new Xxx(" / "Xxx xx" 太粗，这里只统计文件名被提及次数
print("\n--- 被其它 .cs 提及次数（按类型名）---")
src_dirs = [os.path.join(ROOT, "Assets")]
corpus = {}
for base in src_dirs:
    for dirpath, dirnames, filenames in os.walk(base):
        if "05UnitCore" in dirpath:
            continue
        for fn in filenames:
            if fn.endswith(".cs"):
                fp = os.path.join(dirpath, fn)
                try:
                    corpus[fp] = io.open(fp, encoding="utf-8-sig", errors="ignore").read()
                except Exception:
                    pass
print("外部 .cs 文件数:", len(corpus))
for f, n, ns, t in rows:
    names = [x for x in t.split(",") if x]
    for nm in names:
        cnt = sum(1 for c in corpus.values() if re.search(r"\b" + re.escape(nm) + r"\b", c))
        print(f"  {nm:28s} 被 {cnt:3d} 个外部文件提及   (in {f})")
