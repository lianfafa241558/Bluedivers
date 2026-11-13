# -*- coding: utf-8 -*-
"""蹲/趴移动状态的播放速度按移速倍率调低，减少滑步。"""
import io

FILES = [
    r"e:\Bluedivers\Assets\Art\Anim\Student\Aris\Aris.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Noa\Noa.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Nagisa\Nagisa.controller",
]

PAIRS = [
    ("  m_Name: CrouchMove\n  m_Speed: 1\n", "  m_Name: CrouchMove\n  m_Speed: 0.7\n"),
    ("  m_Name: ProneMove\n  m_Speed: 1\n", "  m_Name: ProneMove\n  m_Speed: 0.45\n"),
]

for p in FILES:
    s = io.open(p, encoding="utf-8").read()
    n = 0
    for a, b in PAIRS:
        if a in s:
            s = s.replace(a, b, 1)
            n += 1
    io.open(p, "w", encoding="utf-8", newline="\n").write(s)
    print(p.split("\\")[-1], "patched", n)
