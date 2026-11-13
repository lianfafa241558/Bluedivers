# -*- coding: utf-8 -*-
"""v2：肌肉加法 + RootQ 整体俯仰。用于蹲/趴片段与根部旋转探针。
不夹紧肌肉值（Walk 里本来就有 >1 的值，夹到 [-1,1] 会把摆腿压平）。"""
import io
import os
import math
import re
import uuid

ROOT = r"e:\Bluedivers\Assets\Art\Anim\Student\Noa"
SRC = os.path.join(ROOT, "Walk.anim")

BLOCK_RE = re.compile(
    r"(  - serializedVersion: 2\n    curve:\n      serializedVersion: 2\n      m_Curve:\n)"
    r"(.*?)"
    r"(    attribute: ([^\n]*)\n)",
    re.S,
)
VALUE_RE = re.compile(r"(\n        value: )(-?[0-9.eE+-]+)")

CLAMP = (-4.0, 4.0)


def shift(body, delta):
    def repl(m):
        v = float(m.group(2)) + delta
        v = max(CLAMP[0], min(CLAMP[1], v))
        return "%s%.6f" % (m.group(1), v)

    return VALUE_RE.sub(repl, body)


def flatten(body, factor):
    """把曲线朝自身均值收（factor=0 完全静止，1 原样）：趴着时腿不能继续大幅摆。"""
    vs = values(body)
    mean = sum(vs) / len(vs)
    return set_values(body, [mean + (v - mean) * factor for v in vs])


def values(body):
    return [float(m.group(2)) for m in VALUE_RE.finditer(body)]


def set_values(body, vals):
    it = iter(vals)

    def repl(m):
        return "%s%.7f" % (m.group(1), next(it))

    return VALUE_RE.sub(repl, body)


def quat_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    )


def euler_x(deg):
    h = math.radians(deg) * 0.5
    s, c = math.sin(h), math.cos(h)
    return (s, 0.0, 0.0, c)


def build(name, out_dir, muscle_deltas, pitch_deg=0.0, flat=None, consts=None):
    s = io.open(SRC, encoding="utf-8").read()
    s = s.replace("  m_Name: Walk\n", "  m_Name: %s\n" % name, 1)

    slots = {}  # attr -> [[head, body, tail], ...]（两处镜像）

    def collect(m):
        slots.setdefault(m.group(4), []).append([m.group(1), m.group(2), m.group(3)])
        return m.group(0)

    BLOCK_RE.sub(collect, s)

    if pitch_deg:
        order = ("RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w")
        for ax in order:
            if ax not in slots:
                raise SystemExit("missing " + ax)
        # ⚠ 四条 RootQ 通道的关键帧时间不统一，按索引对齐会错位 ⇒ 直接写常量俯仰
        rot = euler_x(pitch_deg)
        for i in range(len(slots["RootQ.x"])):
            for j, ax in enumerate(order):
                n = len(values(slots[ax][i][1]))
                slots[ax][i][1] = set_values(slots[ax][i][1], [rot[j]] * n)

    for attr, value in (consts or {}).items():
        for slot in slots.get(attr, []):
            n = len(values(slot[1]))
            slot[1] = set_values(slot[1], [value] * n)

    for attr, factor in (flat or {}).items():
        for slot in slots.get(attr, []):
            slot[1] = flatten(slot[1], factor)

    for attr, delta in muscle_deltas.items():
        if attr not in slots:
            print("  !! missing attr", attr)
            continue
        for slot in slots[attr]:
            slot[1] = shift(slot[1], delta)

    counter = {}

    def repl(m):
        attr = m.group(4)
        if attr not in slots:
            return m.group(0)
        i = counter.get(attr, 0)
        counter[attr] = i + 1
        head, body, tail = slots[attr][i]
        return head + body + tail

    out = BLOCK_RE.sub(repl, s)
    io.open(os.path.join(out_dir, name + ".anim"), "w", encoding="utf-8", newline="\n").write(out)

    meta = os.path.join(out_dir, name + ".anim.meta")
    guid = uuid.uuid4().hex
    if os.path.exists(meta):
        guid = re.search(r"guid: ([0-9a-f]{32})", io.open(meta, encoding="utf-8").read()).group(1)
    io.open(meta, "w", encoding="utf-8", newline="\n").write(
        "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
        "  mainObjectFileID: 7400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n" % guid
    )
    print("%-14s pitch=%-5s guid=%s" % (name, pitch_deg, guid))


if __name__ == "__main__":
    build("CrouchWalk", ROOT, {
        "RootT.y": -0.45,
        "Left Upper Leg Front-Back": 0.40,
        "Right Upper Leg Front-Back": 0.40,
        "Left Lower Leg Stretch": -1.00,
        "Right Lower Leg Stretch": -1.00,
        "Left Foot Up-Down": 0.25,
        "Right Foot Up-Down": 0.25,
        "Spine Front-Back": -0.15,
    })
    # 趴：整体前倾 88°（基本水平），双腿**伸直**（膝盖不弯，否则小腿会朝上翘），
    # 腿的摆动全部抹平（趴着不该继续走路摆腿），再整体下移到贴地高度。
    build("ProneWalk", ROOT, {
        "RootT.y": -0.62,
        "Spine Front-Back": -0.05,
        "Head Nod Down-Up": 0.25,
    }, pitch_deg=88.0, consts={
        "Left Upper Leg Front-Back": 0.02,
        "Right Upper Leg Front-Back": 0.02,
        "Left Lower Leg Stretch": 0.95,
        "Right Lower Leg Stretch": 0.95,
        "Left Foot Up-Down": -0.35,
        "Right Foot Up-Down": -0.35,
    }, flat={
        "Left Foot Up-Down": 0.4,
        "Right Foot Up-Down": 0.4,
    })
