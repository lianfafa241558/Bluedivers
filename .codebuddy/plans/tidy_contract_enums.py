# -*- coding: utf-8 -*-
"""Enums 目录整理收尾：①统一 UTF-8 BOM + LF + 行尾空白 ②与 git HEAD 做「枚举成员名+取值」差异比对。

用法：
    python tidy_contract_enums.py            # 只报告
    python tidy_contract_enums.py --fix      # 规范化字节（BOM/LF/行尾空白）

判据：成员比对里除「预期改名」外任何差异都必须为 0，否则说明整理时动了语义。
"""
import os
import re
import subprocess
import sys

REL_DIR = r"Assets/Scripts/00GameContract/Enums"

# 预期改名（旧成员名 -> 新成员名，裸名），比对时按此归一化后再比
RENAMES = {
    "hideAll": "HideAll",
    "hideSelf": "HideSelf",
    "placeholder3": "Placeholder3",
    "placeholder4": "Placeholder4",
}

BOM = b"\xef\xbb\xbf"

ENUM_RE = re.compile(r"\benum\s+(\w+)")
MEMBER_RE = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(\w+)\s*(?:=\s*([^,]+?))?\s*,?\s*$")


def root_dir():
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def members(text):
    """提取 { 枚举名: [(成员名, 显式值|None), ...] }，忽略注释、特性行与全角分隔注释。

    兼容 `enum X {` 与 Allman 风格 `enum X` + 下一行 `{`。
    """
    out, cur, depth, pending = {}, None, 0, None
    for raw in text.splitlines():
        line = raw.split("//")[0]
        m = ENUM_RE.search(line)
        if m:
            pending = m.group(1)
        if pending is not None and "{" in line:
            cur, depth = pending, line.count("{") - line.count("}")
            out[cur] = []
            pending = None
            continue
        if cur is None:
            continue
        depth += line.count("{") - line.count("}")
        if depth <= 0:
            cur = None
            continue
        s = line.strip()
        if not s:
            continue
        mm = MEMBER_RE.match(s)
        if mm and mm.group(1) not in ("public", "internal", "private"):
            out[cur].append((mm.group(1), (mm.group(2) or "").strip() or None))
    return out


def normalize(data):
    txt = data[3:] if data[:3] == BOM else data
    txt = txt.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    lines = [ln.rstrip() for ln in txt.split(b"\n")]
    while lines and lines[-1] == b"":
        lines.pop()
    return BOM + b"\n".join(lines) + b"\n"


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    root = root_dir()
    fix = "--fix" in sys.argv
    d = os.path.join(root, REL_DIR)
    files = sorted(f for f in os.listdir(d) if f.endswith(".cs"))
    bad = []
    for name in files:
        p = os.path.join(d, name)
        with open(p, "rb") as fp:
            data = fp.read()
        now_bom = data[:3] == BOM
        now_crlf = b"\r\n" in data
        if not now_bom or now_crlf:
            bad.append((name, now_bom, now_crlf))
            if fix:
                with open(p, "wb") as fp:
                    fp.write(normalize(data))
    print("=== 字节规范化 ===")
    for name, b, c in bad:
        print("  %-22s bom=%-5s crlf=%-5s%s" % (name, b, c, "  -> 已修复" if fix else ""))
    print("  需修复 %d 个（fix=%s）" % (len(bad), fix))

    print("=== 枚举成员差异（当前 vs git HEAD） ===")
    diff_total = 0
    for name in files:
        rel = REL_DIR + "/" + name
        p = os.path.join(root, rel)
        with open(p, "rb") as fp:
            now_txt = fp.read().decode("utf-8-sig")
        r = subprocess.run(["git", "show", "HEAD:" + rel], cwd=root, capture_output=True)
        if r.returncode != 0:
            print("  %-22s [HEAD 无此文件，跳过]" % name)
            continue
        old_txt = r.stdout.decode("utf-8-sig")
        old, new = members(old_txt), members(now_txt)
        if set(old) != set(new):
            print("  %-22s ⚠ 枚举类型集合变化: %s -> %s" % (name, sorted(old), sorted(new)))
            diff_total += 1
        for en in sorted(set(old) & set(new)):
            a = [RENAMES.get(n, n) for n, _ in old[en]]
            b = [RENAMES.get(n, n) for n, _ in new[en]]
            if a != b:
                print("  %-22s ⚠ %s 成员序列变化\n      旧: %s\n      新: %s" % (name, en, a, b))
                diff_total += 1
            else:
                print("  %-22s ok  %-14s %2d 个成员，顺序一致" % (name, en, len(b)))
    print("---")
    print("语义差异项 = %d（应为 0）" % diff_total)


if __name__ == "__main__":
    main()
