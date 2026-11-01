# -*- coding: utf-8 -*-
"""扫描全项目 prefab/scene/asset 里 Health.showArmorLists 的每一项（Key=伤害类型, Value=减伤乘区）。

用法：
  python scan_armor_resist.py            # 只报告
  python scan_armor_resist.py 9 0        # 把 Value==9 的改成 0（只在 showArmorLists 块内改）

⚠ 只改列表块内、紧跟 `- Key:` 的 `Value:`，不会误伤其它同名字段。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "Assets")
EXTS = (".prefab", ".unity", ".asset")

TYPE_NAMES = {
    0: "Gun", 1: "Explosion", 2: "Destruction", 3: "Real", 4: "Toxicity",
    5: "Burn", 6: "Freeze", 7: "Electric", 8: "Vertigo", 9: "Terror",
    10: "Radiation", 11: "Hacker",
}


def scan(path):
    """返回 [(行下标, key, value, 'Key 行文本', 'Value 行文本')]"""
    with open(path, "rb") as f:
        raw = f.read()
    text = raw.decode("utf-8-sig", errors="replace")
    lines = text.split("\n")
    out = []
    in_block = False
    cur_key = None
    cur_key_idx = None
    for i, line in enumerate(lines):
        s = line.strip()
        if s.startswith("showArmorLists:"):
            in_block = not s.endswith("[]")
            cur_key = None
            continue
        if not in_block:
            continue
        # 块结束：出现顶格内容（顶层字段）
        if line and not line[0].isspace():
            in_block = False
            cur_key = None
            continue
        if s.startswith("- Key:"):
            try:
                cur_key = int(s.split(":", 1)[1].strip())
            except ValueError:
                cur_key = None
            cur_key_idx = i
        elif s.startswith("Value:") and cur_key is not None:
            try:
                val = float(s.split(":", 1)[1].strip())
            except ValueError:
                cur_key = None
                continue
            out.append((i, cur_key, val, cur_key_idx))
            cur_key = None
    return out


def main():
    src = float(sys.argv[1]) if len(sys.argv) > 2 else None
    dst = float(sys.argv[2]) if len(sys.argv) > 2 else None

    total = 0
    changed_files = 0
    for dirpath, _dirs, files in os.walk(ASSETS):
        for fn in files:
            if not fn.endswith(EXTS):
                continue
            path = os.path.join(dirpath, fn)
            try:
                items = scan(path)
            except OSError:
                continue
            if not items:
                continue
            rel = os.path.relpath(path, ROOT)
            for idx, key, val, key_idx in items:
                total += 1
                mark = ""
                if src is not None and abs(val - src) < 1e-6:
                    mark = "   <== 命中"
                print(f"{rel}:{idx + 1}  Key={key}({TYPE_NAMES.get(key, '?')})  Value={val}{mark}")

            if src is None:
                continue

            # 有命中才改，按行号倒序替换避免错位
            targets = [i for (i, _k, v, _ki) in items if abs(v - src) < 1e-6]
            if not targets:
                continue
            with open(path, "rb") as f:
                raw = f.read()
            bom = raw.startswith(b"\xef\xbb\xbf")
            text = raw.decode("utf-8-sig")
            lines = text.split("\n")
            for i in targets:
                old = lines[i]
                # ⚠ 保留原行尾（split("\n") 后 CRLF 文件的每行仍带 '\r'）—— 丢掉就变混合行尾，diff 全脏
                cr = "\r" if old.endswith("\r") else ""
                body = old[: -1] if cr else old
                indent = body[: len(body) - len(body.lstrip())]
                lines[i] = f"{indent}Value: {dst:g}{cr}"
            new_text = "\n".join(lines)
            data = new_text.encode("utf-8")
            if bom:
                data = b"\xef\xbb\xbf" + data
            with open(path, "wb") as f:
                f.write(data)
            changed_files += 1
            print(f"[改] {rel}  命中 {len(targets)} 项")

    print(f"\nshowArmorLists 条目总数={total}，本次改动文件数={changed_files}")


if __name__ == "__main__":
    main()
