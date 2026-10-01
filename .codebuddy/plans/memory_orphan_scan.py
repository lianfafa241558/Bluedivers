# -*- coding: utf-8 -*-
"""删除旧 daily 前的"知识漏搬体检"。

思路：旧 daily 里被反引号包起来的标识符（类/文件/方法/常量）代表它讨论过的实体。
若某个标识符在 `MEMORY.md` + `topics/*.md` 里**一次都没出现**，说明那篇 daily 的
知识可能没被蒸馏进长期记忆 —— 删之前先人工过一眼。

用法：
    python memory_orphan_scan.py              # 阈值 4 次，列出前 50 个孤儿标识符
    python memory_orphan_scan.py --min 3 --top 80
    python memory_orphan_scan.py --cut 2026-09-01   # 指定"旧 daily"分界（默认保留最近 30 天）
"""
import collections
import datetime
import glob
import os
import re
import sys

MEM = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   ".codebuddy", "memory")
DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}\.md$")
# 反引号里的"实体"：标识符 / 路径 / 泛型 / 调用
TOKEN_RE = re.compile(r"`([^`\n]{2,48})`")
NAME_RE = re.compile(r"[\w\.\-/<>\(\)\[\]]{3,48}")


def long_term_text():
    files = [os.path.join(MEM, "MEMORY.md")] + sorted(glob.glob(os.path.join(MEM, "topics", "*.md")))
    return "".join(open(f, encoding="utf-8-sig").read() for f in files)


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    argv = sys.argv
    min_cnt = int(argv[argv.index("--min") + 1]) if "--min" in argv else 4
    top = int(argv[argv.index("--top") + 1]) if "--top" in argv else 50
    if "--cut" in argv:
        cut = argv[argv.index("--cut") + 1]
    else:
        cut = (datetime.date.today() - datetime.timedelta(days=30)).isoformat()

    old = sorted(f for f in glob.glob(os.path.join(MEM, "*.md"))
                 if DATE_RE.match(os.path.basename(f)) and os.path.basename(f)[:10] < cut)
    if not old:
        print("没有分界 %s 之前的 daily" % cut)
        return
    known = long_term_text()

    cnt = collections.Counter()
    for f in old:
        for tok in TOKEN_RE.findall(open(f, encoding="utf-8-sig").read()):
            tok = tok.strip()
            if NAME_RE.fullmatch(tok) and not tok.isdigit():
                cnt[tok] += 1

    orphans = [(t, c) for t, c in cnt.most_common() if c >= min_cnt and t not in known]
    print("旧 daily：%d 篇（< %s），共 %d 字符" % (len(old), cut, sum(len(open(f, encoding='utf-8-sig').read()) for f in old)))
    print("反引号实体 %d 个；长期记忆里查无此名的（>=%d 次）= %d 个" % (len(cnt), min_cnt, len(orphans)))
    for t, c in orphans[:top]:
        print("  %4d  %s" % (c, t))
    print("---")
    print("判读：出现 1~2 次的孤儿通常是临时路径/一次性脚本；成片出现的才是「可能漏搬的子系统」。")


if __name__ == "__main__":
    main()
