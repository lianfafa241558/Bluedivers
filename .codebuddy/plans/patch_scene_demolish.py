# -*- coding: utf-8 -*-
r"""把场景里（非预制体）Health 的拆毁值直接写进它的主体部位 Damageable。

只改目标 MonoBehaviour 块里的一个整数行，其余字节原样保留（不动 BOM / 行尾）。
用法：python -X utf8 .codebuddy/plans/patch_scene_demolish.py
"""
import sys

# (场景路径, Damageable 的 fileID 锚点, 要写入的拆毁值)
JOBS = [
    ("Assets/Scene/Teach.unity", "1253395055", 30),
]


def patch(path, anchor, value):
    with open(path, "rb") as f:
        raw = f.read()
    text = raw.decode("utf-8")
    lines = text.split("\n")

    start = None
    for i, line in enumerate(lines):
        if line.startswith("--- !u!114 &%s" % anchor):
            start = i
            break
    if start is None:
        print("[失败] %s 里找不到 &%s" % (path, anchor))
        return False

    # 块范围：到下一个 "--- !u!" 为止
    end = len(lines)
    for j in range(start + 1, len(lines)):
        if lines[j].startswith("--- !u!"):
            end = j
            break

    for j in range(start, end):
        if lines[j].strip().startswith("demolishValue:"):
            print("[跳过] %s &%s 已有 demolishValue: %s" % (path, anchor, lines[j].strip()))
            return False

    insert_at = None
    for j in range(start, end):
        if lines[j].strip() == "BleedValue: 0":
            insert_at = j + 1
    if insert_at is None:            # 兜底：贴到块尾（isMain 之前若有）
        insert_at = end
    lines.insert(insert_at, "  demolishValue: %d" % value)

    with open(path, "wb") as f:
        f.write("\n".join(lines).encode("utf-8"))
    print("[改] %s &%s ⇒ demolishValue: %d（第 %d 行）" % (path, anchor, value, insert_at + 1))
    return True


def main():
    failed = 0
    for path, anchor, value in JOBS:
        if not patch(path, anchor, value):
            failed += 1
    print("完成：%d 个任务，%d 个未改动" % (len(JOBS), failed))
    return 0


if __name__ == "__main__":
    sys.exit(main())
