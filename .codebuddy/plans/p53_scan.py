# -*- coding: utf-8 -*-
"""P5 侦察：逐个候选玩法程序集的"出边"与"阻塞项"。

用法: python .codebuddy/plans/p5_recon.py > out.txt

相比 AsmDepScan.ps1 的三处修正（P4 踩过的坑）：
  1. 先剥注释再匹配（原矩阵含大量注释/字符串噪声）；
  2. 额外收集**扩展方法**名（调用点 x.M() 不出现类名，纯类型名扫描抓不到）；
  3. 候选层按**文件目录路径**匹配（不按截断后的 unit）。

输出：每个候选层 ① 下游 asmdef 边(=references 依据) ② 指向 Assembly-CSharp 的边(=阻塞项)
      ③ 具体阻塞类型 Top N(=需要先下沉的东西)；另附 ④ 候选层互引 ⑤ FpsHelper 依赖面。
"""

import io
import os
import re
import sys
import collections

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = os.path.abspath(r"d:\Pro\Bluedivers\Assets\Scripts")

# ---------------- 单元(unit) = 最近 asmdef 目录，否则目录前缀 ----------------
asmdef_dirs = {}
for dp, dn, fn in os.walk(ROOT):
    for f in fn:
        if f.endswith(".asmdef"):
            asmdef_dirs[os.path.normpath(dp)] = f[: -len(".asmdef")]


def unit_of(path):
    d = os.path.normpath(os.path.dirname(path))
    while d and len(d) >= len(ROOT):
        if d in asmdef_dirs:
            return "ASM:" + asmdef_dirs[d]
        p = os.path.dirname(d)
        if not p or p == d:
            break
        d = p
    rel = os.path.relpath(os.path.dirname(path), ROOT)
    seg = rel.split(os.sep) if rel != "." else ["<root>"]
    depth = 4 if seg[0] == "02Game" else 2
    if len(seg) >= depth:
        return "/".join(seg[:depth])
    if len(seg) >= 2:
        return "/".join(seg[:2])
    return seg[0]


def rel_dir(path):
    r = os.path.relpath(os.path.dirname(path), ROOT)
    return r.replace(os.sep, "/")


# ---------------- 声明：类型名 + 扩展方法名 -> 所属单元 ----------------
files = []
for dp, dn, fn in os.walk(ROOT):
    for f in fn:
        if f.endswith(".cs"):
            files.append(os.path.join(dp, f))
files.sort()

decl_re = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*"
    r"\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)",
    re.M,
)
ext_re = re.compile(
    r"\bstatic\s+[\w<>\[\],\.\?]+\s+([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(\s*this\s"
)

type_units = collections.defaultdict(collections.Counter)
ext_units = collections.defaultdict(collections.Counter)
texts = {}
for p in files:
    try:
        t = open(p, encoding="utf-8", errors="replace").read()
    except Exception:
        continue
    texts[p] = t
    u = unit_of(p)
    for m in decl_re.finditer(t):
        type_units[m.group(1)][u] += 1
    for m in ext_re.finditer(t):
        ext_units[m.group(1)][u] += 1

uniq = {}
for k, c in type_units.items():
    if len(c) == 1:
        uniq[k] = list(c)[0]
for k, c in ext_units.items():
    if len(c) == 1 and k not in uniq:
        uniq[k] = list(c)[0]

names = sorted([n for n in uniq if len(n) >= 3], key=len, reverse=True)
# 噪声过滤：跳过 "x.Set(" 这类**成员方法调用**（前有 '.' 且后跟 '('），
# 但保留 "GameContract.ServiceLocator" 这类限定名（不以 '(' 结尾）。
NAME_ALT = "(?:" + "|".join(map(re.escape, names)) + r")"
rx = re.compile(r"(?<![A-Za-z0-9_])" + NAME_ALT + r"(?![A-Za-z0-9_])")
rx_member_call = re.compile(r"\.\s*" + NAME_ALT + r"\s*(?:<[^>]*>)?\s*\(")

BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.S)
LINE_COMMENT = re.compile(r"//[^\n]*")


def strip_comments(s):
    return LINE_COMMENT.sub(" ", BLOCK_COMMENT.sub(" ", s))


# ---------------- 候选层（顺序 = 优先级，重叠归前者；EXACT 表示只匹配该目录本身） ----------------
# P5-3：玩法层要当作**一个**候选集扫（互引双向稠密 ⇒ 必须合并成一个 06_Gameplay，见 assembly-plan §Phase 5）
CANDIDATES = [
    ("06_Gameplay(合并候选)", [
        "02Game/Game", "02Game/AI", "02Game/03Player",
        "02Game/05Interactable", "02Game/06Npc", "02Game/Gameplay", "06Gameplay",
    ]),
    ("PREREQ-00ToolsRoot", ["00Tools"]),
]
EXACT = set()
PRIORITY = {}
for idx, (cname, prefs) in enumerate(CANDIDATES):
    for pref in prefs:
        PRIORITY[pref] = idx


def candidate_of(relpath):
    best = None
    for pref, idx in PRIORITY.items():
        ok = (relpath == pref) if pref in EXACT else (relpath == pref or relpath.startswith(pref + "/"))
        if ok and (best is None or idx < best[0]):
            best = (idx, pref)
    return CANDIDATES[best[0]][0] if best else None


def candidate_of_file(path):
    """文件归属：目录优先，再退回按文件名（处理散落在外面的单文件）。"""
    c = candidate_of(rel_dir(path))
    if c:
        return c
    base = os.path.basename(path)[:-3]
    for pref in list(EXACT):
        if base == pref.split("/")[-1]:
            return CANDIDATES[PRIORITY[pref]][0]
    return None


# ---------------- 扫边 ----------------
file_count = collections.Counter()
out_edges = collections.defaultdict(collections.Counter)
blocker_types = collections.defaultdict(collections.Counter)
cross_edges = collections.Counter()
fps_helper = collections.Counter()
file_out = collections.defaultdict(collections.Counter)

for p in files:
    src_unit = unit_of(p)
    cand = candidate_of_file(p)
    file_count[cand] += 1
    body = strip_comments(texts[p])
    # 先记录"成员调用"位置，扫边时跳过
    member_calls = set()
    for mm in rx_member_call.finditer(body):
        member_calls.add((mm.start() + 1 + len(mm.group(0).split(".", 1)[1]) - len(mm.group(0).split(".", 1)[1]), mm.start()))
        member_calls.add(mm.start())
    seen = set()
    for m in rx.finditer(body):
        if (m.start() - 1) in member_calls and body[m.start() - 1] == ".":
            continue
        name = m.group(0)
        tgt_unit = uniq[name]
        if tgt_unit == src_unit:
            continue
        if name.startswith("FpsHelper"):
            fps_helper[src_unit] += 1
            file_out[p][name] += 1
        if cand is None:
            continue
        key = (cand, tgt_unit)
        if (key, name) in seen:
            continue
        seen.add((key, name))
        out_edges[cand][tgt_unit] += 1
        if not tgt_unit.startswith("ASM:"):
            blocker_types[key][name] += 1
        tgt_cand = candidate_of_file(os.path.join(ROOT, tgt_unit.replace("/", os.sep) + os.sep + "x.cs"))
        if tgt_cand and tgt_cand != cand:
            cross_edges[(cand, tgt_cand)] += 1

# ---------------- 输出 ----------------
want = sys.argv[1] if len(sys.argv) > 1 else None
print("Assets/Scripts: %d cs / %d unit / %d 已声明类型或扩展名" %
      (len(files), len(set(unit_of(p) for p in files)), len(uniq)))

for cname, prefs in CANDIDATES:
    if want and want.lower() not in cname.lower():
        continue
    units = out_edges[cname]
    asm_edges = sorted(((u, c) for u, c in units.items() if u.startswith("ASM:")), key=lambda x: -x[1])
    non_edges = sorted(((u, c) for u, c in units.items() if not u.startswith("ASM:")), key=lambda x: -x[1])
    print("\n" + "=" * 78)
    print("### %s   (%d cs 文件)" % (cname, file_count[cname]))
    print("-- 下游 asmdef 边 (references 依据) --")
    if not asm_edges:
        print("   (无)")
    for u, c in asm_edges:
        print("   %-36s %d" % (u, c))
    print("-- 指向 Assembly-CSharp 的边 [!] 阻塞项 --")
    if not non_edges:
        print("   (无)  <== 可以直接切")
    for u, c in non_edges[:12]:
        print("   %-36s %d" % (u, c))
    print("   合计阻塞引用 = %d" % sum(c for _, c in non_edges))
    if non_edges:
        print("-- 具体阻塞类型 Top 24 (类型 -> 所属单元) --")
        agg = collections.Counter()
        for u, _ in non_edges:
            for t, c in blocker_types[(cname, u)].items():
                agg[(t, u)] += c
        for (t, u), c in agg.most_common(24):
            print("   %-28s %-30s %d" % (t, u, c))

print("\n" + "=" * 78)
print("### 候选层互引（from -> to）")
for (a, b), c in cross_edges.most_common(30):
    print("   %-22s -> %-22s %d" % (a, b, c))

print("\n" + "=" * 78)
print("### FpsHelper* 使用面 (unit -> 次数)，00Tools 根在 Assembly-CSharp = 谁用谁被卡")
for u, c in fps_helper.most_common(18):
    print("   %-38s %d" % (u, c))
