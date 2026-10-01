# -*- coding: utf-8 -*-
"""旧 daily 归档：先生成"标题索引"，再（可选）删除正文。

为什么留索引：daily 正文删掉后虽然可以用 `git checkout` 找回，但"哪天讨论过什么"没法搜。
索引只留 `##`/`###` 标题，几百行换回整个月的可检索性。

用法：
    python memory_archive_legacy.py --cut 2026-09-01                 # 只生成索引
    python memory_archive_legacy.py --cut 2026-09-01 --delete        # 生成索引后删除正文
"""
import glob
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MEM = os.path.join(ROOT, ".codebuddy", "memory")
OUT = os.path.join(MEM, "archive")
DATE_RE = re.compile(r"^(\d{4})-(\d{2})-(\d{2})\.md$")
HEAD_RE = re.compile(r"^(#{2,3})\s+(.+?)\s*$")


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    argv = sys.argv
    cut = argv[argv.index("--cut") + 1] if "--cut" in argv else "2026-09-01"
    do_delete = "--delete" in argv

    files = sorted(f for f in glob.glob(os.path.join(MEM, "*.md"))
                   if DATE_RE.match(os.path.basename(f)) and os.path.basename(f)[:10] < cut)
    if not files:
        print("没有 < %s 的 daily" % cut)
        return

    lines = [
        "# 旧 daily 标题索引（%s 之前）" % cut,
        "",
        "> 正文已归档删除。**要读全文**：`git checkout HEAD -- .codebuddy/memory/<日期>.md`（这些文件曾提交进 HEAD）。",
        "> 本索引只保留 `##`/`###` 标题，用于回答「哪天讨论过什么」。",
        "",
    ]
    total_chars = 0
    head_count = 0
    for f in files:
        name = os.path.basename(f)
        text = open(f, encoding="utf-8-sig").read()
        total_chars += len(text)
        heads = [HEAD_RE.match(ln).groups() for ln in text.splitlines() if HEAD_RE.match(ln)]
        head_count += len(heads)
        lines.append("## %s（%d 字符，%d 节）" % (name[:10], len(text), len(heads)))
        for lvl, title in heads:
            lines.append("%s- %s" % ("  " if lvl == "###" else "", title))
        lines.append("")

    os.makedirs(OUT, exist_ok=True)
    idx = os.path.join(OUT, "INDEX-legacy-%s.md" % cut.replace("-", ""))
    with open(idx, "w", encoding="utf-8", newline="\n") as fp:
        fp.write("\n".join(lines).rstrip() + "\n")

    print("索引：%s" % os.path.relpath(idx, ROOT))
    print("  覆盖 %d 篇 daily、%d 字符、%d 个标题；索引本身 %d 字符" % (
        len(files), total_chars, head_count, len(open(idx, encoding="utf-8").read())))

    if do_delete:
        for f in files:
            os.remove(f)
        print("  已删除正文 %d 个文件" % len(files))
    else:
        print("  （未删除，加 --delete 才删）")


if __name__ == "__main__":
    main()
