# -*- coding: utf-8 -*-
"""核验记忆里提到的文件路径是否还存在（目录大改后重定向用）。

覆盖三类写法（记忆里混用）：
  A `Assets/Scripts/09Manager/Global/ResSvc.cs`  —— 全路径
  B `09Manager/Global/ResSvc.cs` / `06Gameplay/AI/...` —— 片段路径（自动补 Assets/Scripts/ 试）
  C `ResSvc.cs` —— 裸文件名（只在磁盘上找不到时才报）
另附 EXTRA：来自 `update_memory` 长期记忆条目里的路径（不在 .md 里）。

用法：
    python memory_path_audit.py [--all] [--bare]
"""
import os
import re
import sys
from collections import defaultdict

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MEM = os.path.join(REPO, ".codebuddy", "memory")

EXT = "cs|py|md|asset|prefab|unity|mat|shader|asmdef|json|png|txt|dll|controller|anim"
FULL_RE = re.compile(r"Assets/[\w\u4e00-\u9fa5./\-]*[\w\u4e00-\u9fa5]")
FRAG_RE = re.compile(r"\b\d{2}[A-Za-z_]\w*(?:/[\w\u4e00-\u9fa5.\-]+)+")
BARE_RE = re.compile(r"\b[\w\u4e00-\u9fa5]+\.(?:%s)\b" % EXT)

# 来自 update_memory 长期记忆条目的路径（不在 .md 文件里）
EXTRA = [
    "Assets/Scripts/06Gameplay/Npc/Furniture_NPCChat.cs",
    "Assets/Scripts/04Data/AirdropData_SO.cs",
    "Assets/Resources/GameData/Airdrop",
    "Assets/Scripts/09Manager/Battle/PathRequestManager.cs",
    "Assets/Scripts/06Gameplay/AI/Controller/EnemyController.cs",
    "Assets/Editor/Drawer/ArchiverDataHandle.cs",
    "Assets/Scripts/00Tools/TechnicalDebt.cs",
    ".codebuddy/rules/UnityCSharp编码规范.md",
]


def clean(tok):
    return tok.strip("`\"'(),;:、，。").rstrip(".")


def targets(all_files):
    out = [os.path.join(MEM, "MEMORY.md")]
    d = os.path.join(MEM, "topics")
    if os.path.isdir(d):
        out += [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith(".md")]
    if all_files:
        out += [os.path.join(MEM, f) for f in sorted(os.listdir(MEM))
                if f.endswith(".md") and f != "MEMORY.md"]
        d = os.path.join(MEM, "archive")
        if os.path.isdir(d):
            out += [os.path.join(d, f) for f in sorted(os.listdir(d)) if f.endswith(".md")]
    return [p for p in out if os.path.exists(p)]


def build_index():
    idx = defaultdict(list)
    for base in (os.path.join(REPO, "Assets"), os.path.join(REPO, ".codebuddy")):
        if not os.path.isdir(base):
            continue
        for dp, dns, fs in os.walk(base):
            for n in list(dns) + list(fs):
                idx[n.lower()].append(os.path.relpath(os.path.join(dp, n), REPO).replace("\\", "/"))
    return idx


def resolve(tok, idx):
    """返回 (ok, 命中路径 或 同名候选列表)"""
    cands = []
    if tok.startswith("Assets/") or tok.startswith(".codebuddy/"):
        cands = [tok]
    elif re.match(r"^\d{2}[A-Za-z_]", tok):
        cands = ["Assets/Scripts/" + tok, "Assets/" + tok]
    else:
        cands = [tok]
    for c in cands:
        c = c.rstrip("/")
        if os.path.exists(os.path.join(REPO, c.replace("/", os.sep))):
            return True, c
    base = tok.rstrip("/").split("/")[-1]
    hits = [h for h in idx.get(base.lower(), [])]
    return False, sorted(set(hits))


def main():
    all_files = "--all" in sys.argv
    with_bare = "--bare" in sys.argv

    idx = build_index()
    bad = {}      # token -> {位置}
    for path in targets(all_files):
        rel = os.path.relpath(path, REPO).replace("\\", "/")
        with open(path, encoding="utf-8-sig", errors="replace") as fp:
            for ln, line in enumerate(fp, 1):
                toks = FULL_RE.findall(line) + FRAG_RE.findall(line)
                if with_bare:
                    toks += BARE_RE.findall(line)
                for raw in toks:
                    t = clean(raw)
                    if not t or t.startswith("d:"):
                        continue
                    ok, _ = resolve(t, idx)
                    if not ok:
                        bad.setdefault(t, set()).add("%s:%d" % (rel, ln))
    for t in EXTRA:
        ok, _ = resolve(t, idx)
        if not ok:
            bad.setdefault(t, set()).add("update_memory 条目")

    print("记忆文件 = %d   失效路径条目 = %d" % (len(targets(all_files)), len(bad)))
    print("=" * 78)
    for t, where in sorted(bad.items(), key=lambda kv: kv[0].lower()):
        ok, hits = resolve(t, idx)
        where_txt = ", ".join(sorted(where)[:5])
        if len(where) > 5:
            where_txt += " …(+%d)" % (len(where) - 5)
        print("[失效] %-58s  ← %s" % (t, where_txt))
        if hits:
            print("        现存同名 → %s" % "  |  ".join(hits[:4]))
        else:
            print("        磁盘上无同名文件/目录（已删或改名）")


if __name__ == "__main__":
    main()
