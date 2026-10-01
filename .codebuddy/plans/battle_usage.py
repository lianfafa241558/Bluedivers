# -*- coding: utf-8 -*-
"""枚举 ServiceLocator.Battle 的全部消费点（含局部别名变量），按 文件 / 成员 聚合。"""
import re, os, sys, collections

ROOTS = [r"Assets\Scripts", r"Assets\Editor"]
ROOT_ABS = r"d:\Pro\Bluedivers"

# 1) 直接调用：ServiceLocator.Battle.Member
DIRECT = re.compile(r"ServiceLocator\.Battle\s*(\??)\s*\.?\s*([A-Za-z_]\w*)?")
# 2) 局部别名：`IBattleService xxx = ServiceLocator.Battle;` 之后 `xxx.Member`
ALIAS_DECL = re.compile(r"IBattleService\s+(\w+)\s*=")

files = []
for root in ROOTS:
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT_ABS, root)):
        for fn in filenames:
            if fn.endswith(".cs"):
                files.append(os.path.join(dirpath, fn))

per_file = collections.Counter()
per_member = collections.Counter()
rows = []
alias_rows = []

for path in files:
    rel = os.path.relpath(path, ROOT_ABS)
    try:
        raw = open(path, "rb").read()
    except Exception:
        continue
    try:
        text = raw.decode("utf-8-sig")
    except Exception:
        text = raw.decode("utf-8", "ignore")
    lines = text.splitlines()

    aliases = set()
    for i, line in enumerate(lines, 1):
        m = ALIAS_DECL.search(line)
        if m:
            aliases.add(m.group(1))

    for i, line in enumerate(lines, 1):
        if "ServiceLocator" in line and "Battle" in line:
            for m in DIRECT.finditer(line):
                member = m.group(2) or "<仅取槽/判空>"
                per_file[rel] += 1
                per_member[member] += 1
                rows.append((rel, i, member, line.strip()[:150]))
        for a in aliases:
            if re.search(r"\b%s\s*\??\s*\.\s*(\w+)" % re.escape(a), line):
                mm = re.search(r"\b%s\s*\??\s*\.\s*(\w+)" % re.escape(a), line)
                per_file[rel] += 1
                per_member[mm.group(1)] += 1
                alias_rows.append((rel, i, a + "." + mm.group(1), line.strip()[:150]))

print("=== 按文件 ===")
for k, v in per_file.most_common():
    print(f"{v:4d}  {k}")
print(f"\n合计消费点: {sum(per_file.values())}    文件数: {len(per_file)}")

print("\n=== 按成员 ===")
for k, v in per_member.most_common():
    print(f"{v:4d}  {k}")

print("\n=== 明细（直接） ===")
for r in rows:
    print(f"{r[0]}:{r[1]}  [{r[2]}]  {r[3]}")

if alias_rows:
    print("\n=== 明细（别名变量） ===")
    for r in alias_rows:
        print(f"{r[0]}:{r[1]}  [{r[2]}]  {r[3]}")
