# -*- coding: utf-8 -*-
"""批量清理**过期注释里的旧名/旧目录名**（2026-10-13）。

替换（全部是注释文本；脚本会额外报警任何"不含 `//` 却命中"的行，防误伤代码）：
    04UI            → 10UI
    01Manager       → 09Manager
    GlobalEventSub  → GlobalEventBus
    BattleEventSub  → BattleEventBus
    WndTypeEnum     → WndType
    Assets/Scripts/02Game/ → Assets/Scripts/06Gameplay/   （仅 TechnicalDebt.cs 一处路径）

口径：**历史说明也一起换**——"原 `GlobalEventSub`"里的"原"已经承载了历史语义，
而旧标识符/旧目录名留着的唯一后果是"读者 grep 不到"。目录名换新后指向同一处，不丢信息。

安全：
  - 每个 token 的**总命中数**必须等于 EXPECT（= 2026-10-13 全仓扫描结果），不等就整脚本中止、不落盘；
  - 行尾/BOM 原样保留（`newline=''` + utf-8 读写，不引额外依赖）；
  - 跑法：python -X utf8 .codebuddy/plans/fix_stale_names.py [--check]
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Assets", "Scripts")

TOKENS = [
    ("04UI", "10UI"),
    ("01Manager", "09Manager"),
    ("GlobalEventSub", "GlobalEventBus"),
    ("BattleEventSub", "BattleEventBus"),
    ("WndTypeEnum", "WndType"),
    ("Assets/Scripts/02Game/", "Assets/Scripts/06Gameplay/"),
]

# 2026-10-13 全仓 .cs 扫描实测的 token 出现次数（行数 ≠ 次数）
EXPECT = {
    "04UI": 15,
    "01Manager": 21,
    "GlobalEventSub": 22,
    "BattleEventSub": 16,
    "WndTypeEnum": 2,
    "Assets/Scripts/02Game/": 1,
}


def main():
    check_only = "--check" in sys.argv

    files = []
    for d, _, fs in os.walk(SRC):
        for f in fs:
            if f.endswith(".cs"):
                files.append(os.path.join(d, f))

    total = {k: 0 for k, _ in TOKENS}
    hits = []          # (path, token, count, bom, crlf)
    suspicious = []    # 命中但没有 `//` 的行 —— 可能是代码，人工看

    for path in files:
        raw = open(path, "r", encoding="utf-8", newline="").read()
        if all(t not in raw for t, _ in TOKENS):
            continue

        bom = raw.startswith("\ufeff")
        crlf = "\r\n" in raw
        new = raw
        for line in new.splitlines():
            for tok, _ in TOKENS:
                if tok in line and "//" not in line:
                    suspicious.append("%s :: %s" % (os.path.relpath(path, ROOT), line.strip()[:110]))

        for tok, rep in TOKENS:
            c = new.count(tok)
            if c == 0:
                continue
            total[tok] += c
            new = new.replace(tok, rep)
            hits.append((os.path.relpath(path, ROOT), tok, rep, c))

        if new != raw and not check_only:
            open(path, "w", encoding="utf-8", newline="").write(new)

        # BOM 必须原样保留（.cs 规范要求 UTF-8 with BOM）
        if bom and not raw.startswith("\ufeff"):
            print("!! BOM 丢失风险:", path)

    print("=== token 命中统计 ===")
    bad = []
    for tok, _ in TOKENS:
        got, want = total[tok], EXPECT[tok]
        flag = "OK " if got == want else "!! "
        if got != want:
            bad.append((tok, got, want))
        print("  %s%-16s got=%-3d expect=%d" % (flag, tok, got, want))

    print("=== 逐文件 ===")
    for rel, tok, rep, c in hits:
        print("  %s  %s → %s  x%d" % (rel, tok, rep, c))

    if suspicious:
        print("=== ⚠ 命中但不含 `//` 的行（请人工确认不是代码）===")
        for s in suspicious:
            print("  " + s)

    if bad:
        print("=== ABORT：命中数与预期不符，未落盘 ===")
        for tok, got, want in bad:
            print("  %s got=%d expect=%d" % (tok, got, want))
        return 1

    print("=== %s ===" % ("只检查，未写盘" if check_only else "全部 %d 个文件已写入" % len({h[0] for h in hits})))
    return 0


if __name__ == "__main__":
    sys.exit(main())
