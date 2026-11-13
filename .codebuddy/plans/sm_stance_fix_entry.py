# -*- coding: utf-8 -*-
"""修正：AnyState→子状态机 会每帧重入（CanTransitionToSelf 对 SM 目标无效）。

改成「具体状态 → 子状态机」：
  Idle/Move --IsCrouch--> CrouchSM ，Idle/Move --IsProne--> ProneSM
  CrouchIdle/CrouchMove --IsProne--> ProneSM ，ProneIdle/ProneMove --IsCrouch--> CrouchSM
站着/趴着回站立仍由 AnyState→Idle/Move（普通状态目标，实测不循环）兜住。
"""
import hashlib
import io
import re

CTRLS = [
    r"e:\Bluedivers\Assets\Art\Anim\Student\Aris\Aris.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Noa\Noa.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Nagisa\Nagisa.controller",
]

DOC_RE = re.compile(r"--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- |\Z)", re.S)
IF, IF_NOT = 1, 2


def gid(seed):
    h = hashlib.md5(seed.encode("utf-8")).hexdigest()
    return -(int(h[:15], 16) % 9000000000000000000) - 1


def name_of(body):
    m = re.search(r"\n  m_Name:(?: (.*))?\n", body)
    return (m.group(1) or "").strip().strip('"')


def ref_of(body, key):
    m = re.search(r"\n  %s: \{fileID: (-?\d+)\}" % key, body)
    return int(m.group(1)) if m else 0


def list_refs(body, key):
    m = re.search(r"\n  %s:\n((?:  - \{[^}]*\}\n)+)" % key, body)
    if not m:
        return []
    return [int(x) for x in re.findall(r"fileID: (-?\d+)", m.group(1))]


def replace_list(body, key, entries):
    lines = body.split("\n")
    out, i, done = [], 0, False
    while i < len(lines):
        if not done and lines[i].startswith("  " + key + ":"):
            done = True
            i += 1
            while i < len(lines) and (lines[i].startswith("  - ") or lines[i].startswith("    ")):
                i += 1
            if entries:
                out.append("  %s:" % key)
                out.extend(entries)
            else:
                out.append("  %s: []" % key)
            continue
        out.append(lines[i])
        i += 1
    assert done, key
    return "\n".join(out)


def trans_doc(tid, conds, dst_sm, can_self=0):
    body = "".join(
        "  - m_ConditionMode: %d\n    m_ConditionEvent: %s\n    m_EventTreshold: 0\n" % c
        for c in conds
    )
    return (
        "--- !u!1101 &%d\n"
        "AnimatorStateTransition:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_Name:\n"
        "  m_Conditions:\n"
        "%s"
        "  m_DstStateMachine: {fileID: %d}\n"
        "  m_DstState: {fileID: 0}\n"
        "  m_Solo: 0\n"
        "  m_Mute: 0\n"
        "  m_IsExit: 0\n"
        "  serializedVersion: 3\n"
        "  m_TransitionDuration: 0.15\n"
        "  m_TransitionOffset: 0\n"
        "  m_ExitTime: 0.75\n"
        "  m_HasExitTime: 0\n"
        "  m_HasFixedDuration: 1\n"
        "  m_InterruptionSource: 0\n"
        "  m_OrderedInterruption: 1\n"
        "  m_CanTransitionToSelf: %d\n" % (tid, body, dst_sm, can_self)
    )


def patch(path):
    raw = io.open(path, encoding="utf-8").read()

    docs = list(DOC_RE.finditer(raw))
    prefix = raw[:docs[0].start()]
    order, info = [], {}
    for m in docs:
        cls, rid, body = m.group(1), int(m.group(2)), m.group(3)
        order.append(rid)
        info[rid] = [cls, body]

    base, sms, states = None, {}, {}
    for rid, (cls, body) in info.items():
        if cls == "1107":
            nm = name_of(body)
            if nm == "Base Layer":
                base = rid
            else:
                sms[nm] = rid
        elif cls == "1102":
            states[name_of(body)] = rid

    idle = states.get("Idle") or states["Idle 1"]
    move = states["Move"]
    ci, cm = states["CrouchIdle"], states["CrouchMove"]
    pi, pm = states["ProneIdle"], states["ProneMove"]
    csm, psm = sms["Crouch"], sms["Prone"]

    # 幂等：Idle 已经有指向子状态机的过渡 ⇒ 说明已修过
    if any(ref_of(info[t][1], "m_DstStateMachine") != 0
           for t in list_refs(info[idle][1], "m_Transitions")):
        print("skip (already fixed):", path)
        return

    # 1) 删掉 AnyState→子状态机 的两条（每帧重入的元凶）
    stale = set()
    kept = []
    for t in list_refs(info[base][1], "m_AnyStateTransitions"):
        if ref_of(info[t][1], "m_DstStateMachine") != 0:
            stale.add(t)
        else:
            kept.append(t)
    info[base][1] = replace_list(info[base][1], "m_AnyStateTransitions",
                                 ["  - {fileID: %d}" % t for t in kept])

    # 2) 具体状态 → 子状态机
    tag = path.replace("\\", "/").split("/")[-1]
    plan = [
        # (源状态, 目标SM, 条件, key)
        (idle, csm, [(IF, "IsCrouch")], "i2c"),
        (idle, psm, [(IF, "IsProne")], "i2p"),
        (move, csm, [(IF, "IsCrouch")], "m2c"),
        (move, psm, [(IF, "IsProne")], "m2p"),
        (ci, psm, [(IF, "IsProne")], "ci2p"),
        (cm, psm, [(IF, "IsProne")], "cm2p"),
        (pi, csm, [(IF, "IsCrouch")], "pi2c"),
        (pm, csm, [(IF, "IsCrouch")], "pm2c"),
    ]

    new_docs = []
    add = {}
    for src, dst_sm, conds, key in plan:
        tid = gid(tag + "smfix_" + key)
        new_docs.append(trans_doc(tid, conds, dst_sm))
        add.setdefault(src, []).append(tid)

    for src, tids in add.items():
        refs = list_refs(info[src][1], "m_Transitions") + tids
        info[src][1] = replace_list(info[src][1], "m_Transitions",
                                    ["  - {fileID: %d}" % t for t in refs])

    for t in stale:
        info.pop(t, None)

    out = [prefix]
    for rid in order:
        if rid in info:
            out.append("--- !u!%s &%d\n%s\n" % (info[rid][0], rid, info[rid][1]))
    out.extend(new_docs)
    io.open(path, "w", encoding="utf-8", newline="\n").write("".join(out))
    print("fixed:", tag, "removedAny", len(stale), "added", len(new_docs))


for p in CTRLS:
    patch(p)
