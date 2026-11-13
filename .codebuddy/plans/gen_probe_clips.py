# -*- coding: utf-8 -*-
"""一次性探测：每次只加一个肌肉偏移，量出各肌肉对脚/髋的作用方向与量级。
产物放在 Noa/_probe/ 下，测完即删。"""
import io
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_stance_clips import BLOCK_RE, MUSCLE_CLAMP, VALUE_RE, is_muscle  # noqa

ROOT = r"e:\Bluedivers\Assets\Art\Anim\Student\Noa"
OUT = os.path.join(ROOT, "_probe")
SRC = os.path.join(ROOT, "Walk.anim")

PROBES = {
    "P_ULFB_p": {"Left Upper Leg Front-Back": 0.30, "Right Upper Leg Front-Back": 0.30},
    "P_ULFB_n": {"Left Upper Leg Front-Back": -0.30, "Right Upper Leg Front-Back": -0.30},
    "P_LLS_p": {"Left Lower Leg Stretch": 0.30, "Right Lower Leg Stretch": 0.30},
    "P_LLS_n": {"Left Lower Leg Stretch": -0.30, "Right Lower Leg Stretch": -0.30},
    "P_FOOT_p": {"Left Foot Up-Down": 0.30, "Right Foot Up-Down": 0.30},
    "P_SPINE_n": {"Spine Front-Back": -0.30},
}


def shift(body, delta, attr):
    lo, hi = (-3.0, 3.0) if not is_muscle(attr) else MUSCLE_CLAMP

    def repl(m):
        v = float(m.group(2)) + delta
        v = max(lo, min(hi, v))
        return "%s%.6f" % (m.group(1), v)

    return VALUE_RE.sub(repl, body)


def make(name, deltas):
    s = io.open(SRC, encoding="utf-8").read()
    s = s.replace("  m_Name: Walk\n", "  m_Name: %s\n" % name, 1)

    def repl(m):
        head, body, tail, attr = m.group(1), m.group(2), m.group(3), m.group(4)
        if attr in deltas:
            body = shift(body, deltas[attr], attr)
        return head + body + tail

    out = BLOCK_RE.sub(repl, s)
    io.open(os.path.join(OUT, name + ".anim"), "w", encoding="utf-8", newline="\n").write(out)
    print("probe", name)


os.makedirs(OUT, exist_ok=True)
for n, d in PROBES.items():
    make(n, d)
