# -*- coding: utf-8 -*-
"""
P5-3 外部阻塞明细扫描（2026-10-01）

目的：玩法层要合并成 `06_Gameplay`（176 cs），只要还有"指向**合并集之外、且不在已允许 asmdef 里**的类型"的引用，
程序集就编不过。本脚本把这类引用精确打到 **文件:行** ，供逐个消解。

与 p5_recon/p53_scan 的关系：那两个脚本出"统计"（哪些单元、多少条），本脚本出"待办清单"（哪一行）。

关键点（复用踩过的坑）：
  1. 先剥注释（行注释 + 块注释）—— 块注释会造成假阳性（WeaponPlayerController.cs:265 的教训）；
  2. 除类型名外**额外收集扩展方法名**（调用点 `x.M()` 不出现类名，P4 的 `IsValid` 教训）；
  3. 单元归属：有 asmdef 的目录 = `ASM:<name>`，否则取相对路径前 2 段（如 `01Manager/Battle`）。
"""
import os
import re
import sys
import collections

SCRIPTS = os.path.join(os.getcwd(), "Assets", "Scripts")

# ---------------- 玩法层合并候选集（P5-3 的目标） ----------------
GAMEPLAY_PREFS = [
    "Effect",
]

# 已允许的下游 asmdef（= 06_Gameplay.asmdef 的 references，来自 p53_scan 实测）
ALLOWED_ASM = {
    "ASM:00_Attribute", "ASM:00_Core", "ASM:01_GameContract", "ASM:00_Utils",
    "ASM:05_UnitCore", "ASM:03_Audio", "ASM:04_Data", "ASM:00_WndTools",
    "ASM:02_Rendering", "ASM:FpsGame.MapUtils", "ASM:05_EffectComp", "ASM:02_Net", "ASM:06_Gameplay", "ASM:04_UI", "ASM:00_WndTools",
    "ASM:DayNightSystem", "ASM:NavMeshComponents",
}

BLOCK_COMMENT = re.compile(r"/\*.*?\*/", re.S)
LINE_COMMENT = re.compile(r"//[^\n]*")
DECL = re.compile(
    r"(?m)^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*"
    r"\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)"
)
EXT_METHOD = re.compile(
    r"(?:static)\s+[\w<>\[\],\.\?]+\s+(\w+)\s*(?:<[^>]*>)?\s*\(\s*(?:this|ref\s+this)\s"
)
WORD = re.compile(r"[A-Za-z_]\w*")


def strip_comments(s):
    return LINE_COMMENT.sub(" ", BLOCK_COMMENT.sub(" ", s))


def load_asmdefs():
    d = {}
    for dirpath, _dirnames, filenames in os.walk(SCRIPTS):
        for fn in filenames:
            if fn.endswith(".asmdef"):
                d[dirpath] = "ASM:" + os.path.splitext(fn)[0]
    return d


ASMDEFS = load_asmdefs()


def unit_of(path):
    d = os.path.dirname(path)
    while len(d) >= len(SCRIPTS):
        if d in ASMDEFS:
            return ASMDEFS[d]
        p = os.path.dirname(d)
        if p == d:
            break
        d = p
    rel = os.path.relpath(os.path.dirname(path), SCRIPTS).replace("\\", "/")
    if rel == ".":
        return "(root)"
    seg = rel.split("/")
    return "/".join(seg if len(seg) <= 2 else seg[:2])


def rel_of(path):
    return os.path.relpath(path, SCRIPTS).replace("\\", "/")


def is_gameplay(rel):
    for pref in GAMEPLAY_PREFS:
        if rel == pref or rel.startswith(pref + "/"):
            return True
    return False


def main():
    out = sys.stdout
    files = []
    for dirpath, _dirnames, filenames in os.walk(SCRIPTS):
        for fn in filenames:
            if fn.endswith(".cs"):
                files.append(os.path.join(dirpath, fn))

    # 1) 声明表：类型名 -> 声明它的单元集合；扩展方法名同理
    decl = collections.defaultdict(set)
    texts = {}
    for p in files:
        try:
            with open(p, encoding="utf-8", errors="ignore") as f:
                t = f.read()
        except OSError:
            continue
        texts[p] = t
        body = strip_comments(t)
        u = unit_of(p)
        for m in DECL.finditer(body):
            decl[m.group(1)].add(u)
        for m in EXT_METHOD.finditer(body):
            decl[m.group(1)].add(u)
        # 嵌套类型（TaskManager.SelectTaskData 那种）也按外层类名可见，无需单列

    # 2) 只保留"名字足够长、且声明单元全在玩法集之外"的名字
    ext_names = {}
    for name, units in decl.items():
        if len(name) < 4:
            continue
        if any(u.startswith("ASM:") and u in ALLOWED_ASM for u in units):
            continue
        # 声明单元里只要有一个属于玩法集，就说明合并后可见 ⇒ 不算阻塞
        if any(u == "(root)" or is_gameplay(u) for u in units):
            continue
        ext_names[name] = units

    rx = re.compile(
        r"(?<![A-Za-z0-9_])(?:" + "|".join(sorted(map(re.escape, ext_names), key=len, reverse=True)) + r")(?![A-Za-z0-9_])"
    )

    out.write("外部阻塞候选类型数 = %d\n" % len(ext_names))
    per_unit = collections.Counter()
    per_type = collections.Counter()
    sites = []
    for p in files:
        rel = rel_of(p)
        if not is_gameplay(rel):
            continue
        body = strip_comments(texts.get(p, ""))
        own = unit_of(p)
        for m in rx.finditer(body):
            name = m.group(0)
            units = ext_names.get(name)
            if not units:
                continue
            ln = body.count("\n", 0, m.start()) + 1
            line = body.splitlines()[ln - 1].strip()
            u = "/".join(sorted(units))
            sites.append((rel, ln, name, u, line))
            per_unit[u] += 1
            per_type[name] += 1

    out.write("\n===== 按外部单元汇总 =====\n")
    for u, c in per_unit.most_common():
        out.write("  %-28s %d\n" % (u, c))

    out.write("\n===== 按类型汇总（Top 40） =====\n")
    for n, c in per_type.most_common(40):
        out.write("  %-32s %-3d  <- %s\n" % (n, c, "/".join(sorted(ext_names[n]))))

    out.write("\n===== 逐条明细（文件:行: 名字 [所属单元] 代码） =====\n")
    for rel, ln, name, u, line in sorted(sites):
        out.write("  %s:%d: %s  [%s]  | %s\n" % (rel, ln, name, u, line[:150]))


if __name__ == "__main__":
    main()
