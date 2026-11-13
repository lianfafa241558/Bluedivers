# -*- coding: utf-8 -*-
"""由 Noa/Walk.anim 派生蹲/趴行走片段（humanoid 肌肉曲线加法偏移）。
只改 m_FloatCurves 与 m_EditorCurves 两处镜像里的 value，其它字节原样保留。
参数在 DELTAS 里，改完重跑即可。"""
import io
import os
import re
import uuid

ROOT = r"e:\Bluedivers\Assets\Art\Anim\Student\Noa"
SRC = os.path.join(ROOT, "Walk.anim")

# attribute -> 加法偏移（肌肉归一化 [-1,1]；RootT.* 是米）
DELTAS = {
    "CrouchWalk": {
        "RootT.y": -0.42,
        "Left Upper Leg Front-Back": 0.42,
        "Right Upper Leg Front-Back": 0.42,
        "Left Lower Leg Stretch": 0.42,
        "Right Lower Leg Stretch": 0.42,
        "Left Foot Up-Down": -0.22,
        "Right Foot Up-Down": -0.22,
        "Spine Front-Back": -0.12,
    },
    "ProneWalk": {
        "RootT.y": -0.95,
        "Left Upper Leg Front-Back": 0.85,
        "Right Upper Leg Front-Back": 0.85,
        "Left Lower Leg Stretch": 0.75,
        "Right Lower Leg Stretch": 0.75,
        "Left Foot Up-Down": -0.45,
        "Right Foot Up-Down": -0.45,
        "Spine Front-Back": -0.30,
    },
}

MUSCLE_CLAMP = (-1.0, 1.0)

BLOCK_RE = re.compile(
    r"(  - serializedVersion: 2\n    curve:\n      serializedVersion: 2\n      m_Curve:\n)"
    r"(.*?)"
    r"(    attribute: ([^\n]*)\n)",
    re.S,
)

VALUE_RE = re.compile(r"(\n        value: )(-?[0-9.eE+-]+)")


def is_muscle(attr):
    return attr not in ("RootT.x", "RootT.y", "RootT.z")


def shift_block(body, delta, attr):
    lo, hi = (-3.0, 3.0) if not is_muscle(attr) else MUSCLE_CLAMP

    def repl(m):
        v = float(m.group(2)) + delta
        v = max(lo, min(hi, v))
        return "%s%.6f" % (m.group(1), v)

    return VALUE_RE.sub(repl, body)


def make(name, deltas):
    s = io.open(SRC, encoding="utf-8").read()
    s = s.replace("  m_Name: Walk\n", "  m_Name: %s\n" % name, 1)

    hits = {}

    def repl(m):
        head, body, tail, attr = m.group(1), m.group(2), m.group(3), m.group(4)
        if attr in deltas:
            hits[attr] = hits.get(attr, 0) + 1
            body = shift_block(body, deltas[attr], attr)
        return head + body + tail

    out = BLOCK_RE.sub(repl, s)

    missing = [a for a in deltas if a not in hits]
    dst = os.path.join(ROOT, name + ".anim")
    io.open(dst, "w", encoding="utf-8", newline="\n").write(out)

    meta = os.path.join(ROOT, name + ".anim.meta")
    guid = uuid.uuid4().hex
    if os.path.exists(meta):
        old = io.open(meta, encoding="utf-8").read()
        guid = re.search(r"guid: ([0-9a-f]{32})", old).group(1)
    io.open(meta, "w", encoding="utf-8", newline="\n").write(
        "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
        "  mainObjectFileID: 7400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n" % guid
    )
    print("%-12s hits=%s missing=%s guid=%s bytes=%d" % (name, hits, missing, guid, len(out)))


for n, d in DELTAS.items():
    make(n, d)
