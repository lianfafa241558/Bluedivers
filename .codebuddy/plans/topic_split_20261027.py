# -*- coding: utf-8 -*-
"""按小节拆分超预算 topic（2026-10-27）。

两处：
  ① gameplay-world.md  8133 → 搬出「地形 / 渲染」→ topics/terrain-render.md
  ② mission-intercept-train.md 10023 → 搬出「8. 卫队护航」「9. 轨道方向箭头」→ topics/mission-intercept-extras.md

断言（任一失败即中止，不写盘）：
  - 锚点各出现且顺序正确、区间不越界；
  - 搬出的每一行都出现在新文件里；
  - 原文件每一行都出现在「父文件 + 新文件」里（只允许父文件里被改写的那行抬头/指针）；
  - 新文件 ≤ 8000 字符、父文件 ≤ 8000 字符；
  - 写出的 .md 一律 无 BOM + LF。
"""
import io
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".codebuddy", "memory", "topics"))


def read(name):
    return io.open(os.path.join(ROOT, name), encoding="utf-8").read()


def write(name, text):
    assert not text.startswith("\ufeff")
    text = text.replace("\r\n", "\n")
    with io.open(os.path.join(ROOT, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("  写出 %-38s %d 字符" % (name, len(text)))


def split(src, anchors, dest, dest_title, dest_intro, parent_pointer):
    """anchors = (起锚点行首, 结束锚点行首) —— 搬 [起, 结束) 之间的内容。"""
    text = read(src)
    lines = text.split("\n")
    start = end = -1
    for i, ln in enumerate(lines):
        if start < 0 and ln.startswith(anchors[0]):
            start = i
        elif start >= 0 and anchors[1] and ln.startswith(anchors[1]):
            end = i
            break
    if start < 0:
        return "锚点未命中: " + anchors[0]
    if end < 0:
        end = len(lines)
    if end <= start:
        return "区间非法"

    moved = lines[start:end]
    while moved and not moved[-1].strip():
        moved.pop()
    parent = lines[:start] + lines[end:]

    # 父文件：插一行指针（放在被搬走的位置）
    ptr_at = start
    parent = parent[:ptr_at] + [parent_pointer, ""] + parent[ptr_at:]

    body = [dest_title, "", dest_intro, ""] + moved
    new_text = "\n".join(body).rstrip("\n") + "\n"
    parent_text = "\n".join(parent).rstrip("\n") + "\n"

    # ---- 断言 ----
    new_set = set(l.rstrip() for l in new_text.split("\n") if l.strip())
    lost = [l for l in moved if l.strip() and l.rstrip() not in new_set]
    if lost:
        return "新文件缺行 %d 条，例：%r" % (len(lost), lost[0][:60])

    all_set = new_set | set(l.rstrip() for l in parent_text.split("\n") if l.strip())
    orig_set = set(l.rstrip() for l in text.split("\n") if l.strip())
    dropped = [l for l in orig_set if l not in all_set]
    if dropped:
        return "原文有 %d 行消失，例：%r" % (len(dropped), dropped[0][:60])

    if len(new_text) > 8000 or len(parent_text) > 8000:
        return "仍超预算：父 %d / 子 %d" % (len(parent_text), len(new_text))

    print("== %s → %s" % (src, dest))
    print("   父 %d 字符 / 子 %d 字符（搬出 %d 行）" % (len(parent_text), len(new_text), len(moved)))
    write(src, parent_text)
    write(dest, new_text)
    return None


def main():
    errs = []
    errs.append(split(
        "gameplay-world.md",
        ("## 地形 / 渲染", "> 以下由 `net-battle.md`"),   # 只搬「地形/渲染」小节，别把下面的 §7.2d 带走
        "terrain-render.md",
        "# 主题 · 地形 / 渲染（TerrainData / 天气 / ToonLit）",
        "> 2026-10-27 从 `gameplay-world.md` 拆出（超 8000 预算）。同伴：`gameplay-world.md`（家具/任务/逻辑帧/存档）、`08Map` 程序集。",
        "> 地形 / 渲染 / TerrainData / ToonLit / 天气 → `terrain-render.md`（2026-10-27 拆出）",
    ))
    errs.append(split(
        "mission-intercept-train.md",
        ("## 8. 卫队护航", None),
        "mission-intercept-extras.md",
        "# 拦截火车 · 附加系统（卫队护航 / 轨道方向箭头）",
        "> 2026-10-27 从 `mission-intercept-train.md` 拆出（超 8000 预算）。主文件讲任务本体与轨道/车厢/附加炮塔。",
        "> 卫队护航（§8）/ 轨道方向箭头（§9）→ `mission-intercept-extras.md`（2026-10-27 拆出）",
    ))

    bad = [e for e in errs if e]
    for e in bad:
        print("✗ " + e)
    if bad:
        return 1
    print("OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
