# -*- coding: utf-8 -*-
"""Vfx_AboHacker.prefab：把 enabled 的 VelocityModule 里 orbitalX/orbitalZ 的
minMaxState 由 0(Constant) 统一成 orbitalY 的 3(Two Curves)。

依据：Unity 只支持 orbitalX/Y/Z 三轴同模式，否则每次 Simulate 刷
"Particle Orbital Velocity curves must all be in the same mode"。
逐行替换，保留 LF、无 BOM，其余序列化字段不动。
"""
P = r"d:\Pro\Bluedivers\Assets\Resources\VFX\AboState\Vfx_AboHacker.prefab"


def indent_of(s):
    return len(s) - len(s.lstrip())


with open(P, "rb") as f:
    lines = f.read().decode("utf-8").splitlines(keepends=True)

fixed = []
for i, ln in enumerate(lines):
    if ln.strip() != "VelocityModule:":
        continue
    mod_ind = indent_of(ln)
    end = len(lines)
    for j in range(i + 1, len(lines)):
        if lines[j].strip() and indent_of(lines[j]) <= mod_ind:
            end = j
            break

    states = {}
    for j in range(i, end):
        key = lines[j].strip().rstrip(":")
        if key in ("orbitalX", "orbitalY", "orbitalZ"):
            for k in range(j + 1, min(end, j + 6)):
                s = lines[k].strip()
                if s.startswith("minMaxState:"):
                    states[key] = (k, int(s.split(":")[1]))
                    break
    if len(states) != 3:
        continue
    vals = set(v for _, v in states.values())
    if len(vals) <= 1:
        continue

    target = states["orbitalY"][1]  # 以 Y 为准（视觉上的绕轴旋转）
    for key in ("orbitalX", "orbitalZ"):
        k, v = states[key]
        if v != target:
            lines[k] = lines[k][: indent_of(lines[k])] + "minMaxState: %d\n" % target
            fixed.append((key, k + 1, v, target))

assert fixed, "no mismatch found"
with open(P, "wb") as f:
    f.write("".join(lines).encode("utf-8"))
print("fixed:", fixed)
