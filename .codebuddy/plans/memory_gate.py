# -*- coding: utf-8 -*-
"""记忆分层体检：报告各层字符数 vs 预算，并列出"该下沉"的候选段落。

分层（见 .codebuddy/memory/MEMORY.md 头部规则）：
    L0  .codebuddy/memory/MEMORY.md          热区 + 路由表     预算 5000 字符
    L1  .codebuddy/memory/topics/*.md         按需加载主题       预算 8000 字符/篇
    L2  .codebuddy/memory/YYYY-MM-DD.md       过程流水（不注入）  仅统计，不设预算
    L3  30 天以上的 daily                     建议归档/删除

用法：
    python memory_gate.py                # 只报告
    python memory_gate.py --days 30      # 指定"过期 daily"阈值（默认 30）
    python memory_gate.py --candidates   # 额外列出 L0/L1 里像"具体过程细节"的行（可下沉候选）
"""
import datetime
import glob
import os
import re
import sys

BUDGET_L0 = 3500
BUDGET_L1 = 8000
MEM_DEFAULT = 30

# L0 允许的主题小节（只有这些可以待在 MEMORY.md）
L0_ALLOWED = ("热区 0", "热区 1", "热区 2", "热区 3", "路由表", "待办")

# "可下沉候选"启发式：行里出现这些词，说明它更像主题细节而非热区红线
TOPIC_HINT = re.compile(
    r"(判据|口径|准入|手法|落法|取代|下沉|asmdef|命名空间|using|Slot|槽位|"
    r"窗口|家具|任务|波次|伤害|寻路|相机|池|Shader|Drawer|Inspector|枚举)"
)


def project_root():
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def read(path):
    with open(path, "r", encoding="utf-8-sig") as fp:
        return fp.read()


def report_file(label, path, budget):
    if not os.path.exists(path):
        print("  %-34s 缺失" % label)
        return 0
    n = len(read(path))
    flag = "超预算 +%d" % (n - budget) if n > budget else "ok"
    print("  %-34s %6d / %5d 字符  %s" % (label, n, budget, flag))
    return n


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    root = project_root()
    mem = os.path.join(root, ".codebuddy", "memory")
    days = MEM_DEFAULT
    if "--days" in sys.argv:
        days = int(sys.argv[sys.argv.index("--days") + 1])

    print("=== L0 MEMORY.md ===")
    report_file("MEMORY.md", os.path.join(mem, "MEMORY.md"), BUDGET_L0)

    print("=== L1 topics/*.md ===")
    topics = sorted(glob.glob(os.path.join(mem, "topics", "*.md")))
    if not topics:
        print("  （无 topics 目录）")
    for p in topics:
        report_file("topics/" + os.path.basename(p), p, BUDGET_L1)

    print("=== L2 daily（只统计，不设预算）===")
    dailies = sorted(f for f in glob.glob(os.path.join(mem, "*.md"))
                     if re.match(r"\d{4}-\d{2}-\d{2}\.md$", os.path.basename(f)))
    cutoff = (datetime.date.today() - datetime.timedelta(days=days)).isoformat()
    fresh = [f for f in dailies if os.path.basename(f)[:10] > cutoff]
    stale = [f for f in dailies if os.path.basename(f)[:10] < cutoff]
    print("  共 %d 篇；近 %d 天 %d 篇 / %d 字符；更早 %d 篇 / %d 字符（建议归档）" % (
        len(dailies), days, len(fresh), sum(len(read(f)) for f in fresh),
        len(stale), sum(len(read(f)) for f in stale)))
    if stale:
        print("  最老的 3 篇：%s" % ", ".join(os.path.basename(f) for f in stale[:3]))

    if "--candidates" in sys.argv:
        print("=== 可下沉候选（L0 里不属于热区/路由/待办的小节）===")
        cur = None
        hits = 0
        for line in read(os.path.join(mem, "MEMORY.md")).splitlines():
            if line.startswith("## "):
                cur = line[3:].strip()
                continue
            if cur and not any(cur.startswith(a) for a in L0_ALLOWED):
                print("  [%s] %s" % (cur, line[:100]))
                hits += 1
        print("  候选 %d 行" % hits)

    print("---")
    print("规则：L0 超预算 ⇒ 把整段移到对应 topic，只留一行指针；topic 超预算 ⇒ 按小节再拆一篇。")


if __name__ == "__main__":
    main()
