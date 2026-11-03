# -*- coding: utf-8 -*-
"""PF_RainSplash.prefab：VelocityModule(line 1493) 的 y 轴是 Constant 0，
而 x/z 是 Two Curves ⇒ 刷 "Particle Velocity curves must all be in the same mode"。
把 y 也改成 Two Curves（曲线和 scalar 全是 0，视觉零变化）。
"""
P = r"d:\Pro\Bluedivers\Assets\Art\Nature\RainSnowCloudEffect\Prefabs\PF_RainSplash.prefab"

with open(P, "rb") as f:
    lines = f.read().decode("utf-8").splitlines(keepends=True)


def ind(s):
    return len(s) - len(s.lstrip())


assert lines[1492].strip() == "VelocityModule:"
assert lines[1493].strip() == "enabled: 1"
assert lines[1547].strip() == "y:"
assert lines[1549].strip() == "minMaxState: 0"
assert lines[1550].strip() == "scalar: 0" and lines[1551].strip() == "minScalar: 0"

lines[1549] = lines[1549][: ind(lines[1549])] + "minMaxState: 3\n"

with open(P, "wb") as f:
    f.write("".join(lines).encode("utf-8"))
print("ok")
