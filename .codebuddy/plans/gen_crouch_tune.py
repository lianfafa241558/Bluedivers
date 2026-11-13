# -*- coding: utf-8 -*-
"""蹲姿参数扫描：量出 ULFB / LLS 对脚高的实际影响，用来定最终数值。"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_stance_clips2 import build  # noqa

ROOT = r"e:\Bluedivers\Assets\Art\Anim\Student\Noa"
OUT = os.path.join(ROOT, "_tune")
os.makedirs(OUT, exist_ok=True)

BASE = {
    "RootT.y": -0.45,
    "Left Foot Up-Down": 0.25,
    "Right Foot Up-Down": 0.25,
    "Spine Front-Back": -0.15,
}

VARIANTS = {
    "V1_lls-1_ulfb0.4": (-1.00, 0.40),
    "V2_lls0_ulfb0.4": (0.00, 0.40),
    "V3_lls-2_ulfb0.4": (-2.00, 0.40),
    "V4_lls-1_ulfb0.8": (-1.00, 0.80),
    "V5_lls-1_ulfb0": (-1.00, 0.00),
    "V6_lls-1.5_ulfb0.6": (-1.50, 0.60),
}

for name, (lls, ulfb) in VARIANTS.items():
    d = dict(BASE)
    d["Left Lower Leg Stretch"] = lls
    d["Right Lower Leg Stretch"] = lls
    d["Left Upper Leg Front-Back"] = ulfb
    d["Right Upper Leg Front-Back"] = ulfb
    build(name, OUT, d)
