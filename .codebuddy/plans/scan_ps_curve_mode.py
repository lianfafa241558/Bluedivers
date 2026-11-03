# -*- coding: utf-8 -*-
"""扫描 prefab/scene 里 ParticleSystem 的 velocityOverLifetime 曲线模式是否一致。

Unity 只支持 "三轴同模式"：orbitalX/Y/Z 以及 x/y/z/radial 若混用
Constant(=0) / Curve(=1) / TwoConstants(=2) / TwoCurves(=3)，每帧刷
  Particle Orbital Velocity curves must all be in the same mode
  Particle Velocity curves must all be in the same mode
纯只读，不写任何文件。
"""
import os
import re
import sys

ROOT = r"d:\Pro\Bluedivers\Assets"
EXTS = (".prefab", ".unity")

MODULE_RE = re.compile(r"^(\s*)(VelocityModule):\s*$")
KEY_RE = re.compile(r"^(\s*)(orbitalX|orbitalY|orbitalZ|orbitalOffsetX|orbitalOffsetY|orbitalOffsetZ|radial|x|y|z):\s*$")
GAMEOBJ_RE = re.compile(r"^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}")


def read_block(lines, start, indent):
    """返回从 start+1 开始、缩进大于 indent 的行区间 [s, e)。"""
    i = start + 1
    while i < len(lines):
        ln = lines[i]
        if ln.strip():
            cur = len(ln) - len(ln.lstrip())
            if cur <= indent:
                break
        i += 1
    return i


def scan_file(path):
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        lines = f.read().splitlines()

    issues = []
    for idx, ln in enumerate(lines):
        m = MODULE_RE.match(ln)
        if not m:
            continue
        mod_indent = len(m.group(1))
        end = read_block(lines, idx, mod_indent)

        # 归属的 GameObject fileID（往上找最近一个 m_GameObject）
        go_id = "?"
        for j in range(idx, max(-1, idx - 400), -1):
            gm = GAMEOBJ_RE.match(lines[j])
            if gm:
                go_id = gm.group(1)
                break

        curves = {}
        for j in range(idx + 1, end):
            km = KEY_RE.match(lines[j])
            if not km:
                continue
            key_indent = len(km.group(1))
            key = km.group(2)
            kend = read_block(lines, j, key_indent)
            state = None
            for k in range(j + 1, kend):
                sm = re.search(r"\bminMaxState:\s*(-?\d+)", lines[k])
                if sm:
                    state = int(sm.group(1))
                    break
            if state is not None:
                curves.setdefault(key, (state, j + 1))

        def check(names, label):
            got = [(n, curves[n]) for n in names if n in curves]
            if len(got) < 2:
                return
            states = set(s for _, (s, _) in got)
            if len(states) > 1:
                detail = ", ".join("%s=%d(line %d)" % (n, s, l) for n, (s, l) in got)
                issues.append((path, idx + 1, go_id, label, detail))

        check(["orbitalX", "orbitalY", "orbitalZ"], "orbital")
        # radial 是独立单曲线，Unity 不参与该判定，排除以免全是误报
        check(["x", "y", "z"], "linear")
    return issues


def main():
    all_issues = []
    count = 0
    for dirpath, dirnames, filenames in os.walk(ROOT):
        for fn in filenames:
            if not fn.lower().endswith(EXTS):
                continue
            p = os.path.join(dirpath, fn)
            count += 1
            try:
                all_issues.extend(scan_file(p))
            except Exception as e:  # noqa
                print("SKIP %s (%s)" % (p, e))

    print("scanned %d files, %d mismatched modules" % (count, len(all_issues)))
    for p, line, go, label, detail in all_issues:
        rel = os.path.relpath(p, r"d:\Pro\Bluedivers")
        print("%s:%d  [%s] GO=%s  %s" % (rel, line, label, go, detail))


if __name__ == "__main__":
    main()
