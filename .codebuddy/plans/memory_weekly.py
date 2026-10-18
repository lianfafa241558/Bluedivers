# -*- coding: utf-8 -*-
"""记忆周检：一条命令跑完「体检 + 归档建议 + 收尾提示」。

背景：IDE 的 automation 在本机不可用（`automation_update` 返回空、`~/.codebuddy/automations` 不生成），
所以把周检做成可手动/可挂计划任务的脚本。判定口径与 skill `memory-hygiene` 一致。

用法：
    python -X utf8 .codebuddy/plans/memory_weekly.py              # 只体检 + 给建议（默认，安全）
    python -X utf8 .codebuddy/plans/memory_weekly.py --archive    # 额外生成 daily 索引（不删正文）
    python -X utf8 .codebuddy/plans/memory_weekly.py --archive --delete   # 生成索引并删正文（要求已 git 跟踪）

退出码：0 = 全部在预算内；1 = 有超预算（方便挂 CI / 计划任务告警）。
"""
import datetime
import glob
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MEM = os.path.join(ROOT, ".codebuddy", "memory")
PLANS = os.path.join(ROOT, ".codebuddy", "plans")
BUDGET_L0, BUDGET_L1, DAILY_STALE_DAYS = 3500, 8000, 30


def chars(path):
    with open(path, encoding="utf-8-sig") as fp:
        return len(fp.read())


def run_gate():
    out = subprocess.run([sys.executable, "-X", "utf8", os.path.join(PLANS, "memory_gate.py")],
                         capture_output=True)
    return out.stdout.decode("utf-8", "replace")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    argv = sys.argv[1:]
    do_archive = "--archive" in argv
    do_delete = "--delete" in argv

    print("=" * 66)
    print("记忆周检  %s" % datetime.date.today().isoformat())
    print("=" * 66)
    report = run_gate()
    print(report.rstrip())

    # ⚠ gate 末尾那句规则说明里也有"超预算"三个字，只能认 "超预算 +N" 这种真实条目
    over = [l.strip() for l in report.splitlines() if re.search(r"超预算\s*\+\d+", l)]
    if over:
        print("\n⇒ 有 %d 处超预算：按 skill `memory-hygiene` §2/§3 拆分或下沉，" % len(over))
        print("   拆分脚本沿用 `plans/memory_split_20261018.py` 的「区间 + 覆盖断言 + 逐行回查」。")

    # ---- 过期 daily
    cutoff = (datetime.date.today() - datetime.timedelta(days=DAILY_STALE_DAYS)).isoformat()
    dailies = sorted(os.path.basename(p) for p in glob.glob(os.path.join(MEM, "*.md"))
                     if re.match(r"^\d{4}-\d{2}-\d{2}\.md$", os.path.basename(p)))
    stale = [d for d in dailies if d[:10] < cutoff]
    if stale:
        tracked = subprocess.run(["git", "ls-files", ".codebuddy/memory"], cwd=ROOT,
                                 capture_output=True).stdout.decode("utf-8", "replace")
        untracked = [d for d in stale if ".codebuddy/memory/" + d not in tracked]
        print("\n⇒ 可归档 daily %d 篇（< %s）：%s …" % (len(stale), cutoff, ", ".join(d[:10] for d in stale[:3])))
        if untracked:
            print("   ⚠ 其中 %d 篇**未被 git 跟踪** ⇒ 删了找不回，先 commit：%s"
                  % (len(untracked), ", ".join(untracked[:3])))
        if do_archive and not untracked:
            cmd = [sys.executable, "-X", "utf8", os.path.join(PLANS, "memory_archive_legacy.py"),
                   "--cut", cutoff]
            if do_delete:
                cmd.append("--delete")
            print("   -> 执行：%s" % " ".join(cmd[2:]))
            subprocess.run(cmd)
        elif not do_archive:
            print("   -> 归档：本脚本加 `--archive`（只出索引）/ `--archive --delete`（删正文）")
    else:
        print("\n⇒ 没有超过 %d 天的 daily。" % DAILY_STALE_DAYS)

    # ---- 收尾提示
    print("\n收尾（有改动才做）：")
    print("  1) 新 .md 必须无 BOM、UTF-8 + LF；`MEMORY.md` 改前重读（多窗口并发写）。")
    print("  2) 拆分/搬家后跑保真校验：`git show HEAD:<源文件>` 逐行比对，只允许旧标题/指针化长句/断链名。")
    print("  3) 结论追加到当天 daily；没改动就到此为止，别为了产出改没超预算的文件。")

    if over:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
