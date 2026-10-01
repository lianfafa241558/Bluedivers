# -*- coding: utf-8 -*-
"""06Gameplay 目录整理前置审计：孤儿 meta / 缺 meta / 空目录 / 类名文件名不一致 / 命名空间分布 / 顶层散落文件 / UnityEditor 越界"""
import os, re, io, sys, collections

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts\06Gameplay"

def read(p):
    with io.open(p, "r", encoding="utf-8-sig", errors="replace") as f:
        return f.read()

files_all = []
dirs = []
for r, ds, fs in os.walk(ROOT):
    dirs.append(r)
    for f in fs:
        files_all.append(os.path.join(r, f))

cs = [p for p in files_all if p.endswith(".cs")]
metas = set(p for p in files_all if p.endswith(".meta"))

print("=" * 70)
print("A. 孤儿 .meta（没有对应文件的 meta）")
orphan = []
for m in sorted(metas):
    target = m[:-5]
    if not os.path.exists(target):
        orphan.append(os.path.relpath(m, ROOT))
for o in orphan:
    print("   -", o)
print("   合计:", len(orphan))

print("=" * 70)
print("B. 缺 .meta 的文件/目录（除 .meta 自身外）")
miss = []
for p in files_all:
    if p.endswith(".meta"):
        continue
    if (p + ".meta") not in metas:
        miss.append(os.path.relpath(p, ROOT))
for d in dirs:
    if d == ROOT:
        continue
    if (d + ".meta") not in metas:
        miss.append(os.path.relpath(d, ROOT) + "  [目录]")
for o in sorted(miss):
    print("   -", o)
print("   合计:", len(miss))

print("=" * 70)
print("C. 空目录（无任何文件，含隐藏）")
for d in sorted(dirs):
    if d == ROOT:
        continue
    if not os.listdir(d):
        print("   -", os.path.relpath(d, ROOT))

print("=" * 70)
print("D. 类名/文件名不一致（第一个 class/struct/enum/interface 声明）")
pat = re.compile(r"^\s*(?:public|internal|private|protected)?\s*(?:static\s+|abstract\s+|sealed\s+|partial\s+)*\b(class|struct|enum|interface)\b\s+([A-Za-z_]\w*)", re.M)
bad = []
for p in cs:
    txt = read(p)
    # 剥掉行注释与块注释的粗略正则，避免注释中的假阳性
    txt2 = re.sub(r"/\*.*?\*/", " ", txt, flags=re.S)
    txt2 = re.sub(r"^\s*//.*$", " ", txt2, flags=re.M)
    m = pat.search(txt2)
    if not m:
        continue
    name = m.group(2)
    fname = os.path.splitext(os.path.basename(p))[0]
    base = fname.split("_")[0]
    if name != fname and not fname.startswith(name):
        bad.append((os.path.relpath(p, ROOT), name))
for o, n in sorted(bad):
    print("   -", o, "->", n)
print("   合计:", len(bad))

print("=" * 70)
print("E. 命名空间分布")
ns_pat = re.compile(r"^\s*namespace\s+([\w\.]+)", re.M)
cnt = collections.Counter()
nons = []
for p in cs:
    m = ns_pat.search(read(p))
    if m:
        cnt[m.group(1)] += 1
    else:
        nons.append(os.path.relpath(p, ROOT))
for k, v in sorted(cnt.items(), key=lambda x: -x[1]):
    print("   %-28s %d" % (k, v))
print("   (无 namespace)            %d" % len(nons))
for o in sorted(nons):
    print("        *", o)

print("=" * 70)
print("F. 目录 -> 主要命名空间 交叉表（看目录/命名空间是否错位）")
d2ns = collections.defaultdict(collections.Counter)
for p in cs:
    m = ns_pat.search(read(p))
    ns = m.group(1) if m else "(none)"
    rel = os.path.relpath(os.path.dirname(p), ROOT).replace("\\", "/")
    d2ns[rel][ns] += 1
for d in sorted(d2ns):
    items = ", ".join("%s:%d" % (k, v) for k, v in sorted(d2ns[d].items(), key=lambda x: -x[1]))
    print("   %-42s %s" % (d, items))

print("=" * 70)
print("G. UnityEditor 使用点是否在 #if UNITY_EDITOR 守卫内（简易判定）")
for p in cs:
    txt = read(p)
    if not re.search(r"^\s*using\s+UnityEditor", txt, re.M):
        continue
    lines = txt.split("\n")
    for i, ln in enumerate(lines):
        if re.match(r"^\s*using\s+UnityEditor", ln):
            guarded = False
            depth = 0
            for j in range(i - 1, -1, -1):
                s = lines[j].strip()
                if s.startswith("#if"):
                    guarded = ("UNITY_EDITOR" in s)
                    break
            print("   %-70s line %d  %s" % (os.path.relpath(p, ROOT), i + 1, "OK" if guarded else "!!! 未守卫"))
            break

print("=" * 70)
print("H. 顶层散落文件（ROOT 直属 cs）")
for p in sorted(cs):
    if os.path.dirname(p) == ROOT:
        print("   -", os.path.basename(p))
