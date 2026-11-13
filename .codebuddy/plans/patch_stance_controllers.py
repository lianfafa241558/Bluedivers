# -*- coding: utf-8 -*-
"""给 Aris/Noa/Nagisa 三个 AnimatorController 的 Base Layer 加蹲/趴四个状态 + 八条过渡。
只往 YAML 里追加文档与列表项，不动既有内容；重复执行会先检测是否已打过补丁。"""
import hashlib
import io
import re

CONTROLLERS = [
    r"e:\Bluedivers\Assets\Art\Anim\Student\Aris\Aris.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Noa\Noa.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Nagisa\Nagisa.controller",
]

CROUCH_GUID = "4554d44964004d0eb1f3b00cb7b3c9da"
PRONE_GUID = "850cdd3f0131411ea4aa7153d10db89a"

DOC_RE = re.compile(r"--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- |\Z)", re.S)


def gid(seed):
    h = hashlib.md5(seed.encode("utf-8")).hexdigest()
    return -(int(h[:15], 16) % 9000000000000000000) - 1


def state_doc(sid, name, motion_guid, speed, exits, pos):
    if exits:
        head = "  m_Transitions:\n" + "".join("  - {fileID: %d}\n" % e for e in exits)
    else:
        head = "  m_Transitions: []\n"
    return (
        "--- !u!1102 &%d\n"
        "AnimatorState:\n"
        "  serializedVersion: 6\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_Name: %s\n"
        "  m_Speed: %s\n"
        "  m_CycleOffset: 0.15\n"
        "%s"
        "  m_StateMachineBehaviours: []\n"
        "  m_Position: {x: %.0f, y: %.0f, z: 0}\n"
        "  m_IKOnFeet: 1\n"
        "  m_WriteDefaultValues: 1\n"
        "  m_Mirror: 0\n"
        "  m_SpeedParameterActive: 0\n"
        "  m_MirrorParameterActive: 0\n"
        "  m_CycleOffsetParameterActive: 0\n"
        "  m_TimeParameterActive: 0\n"
        "  m_Motion: {fileID: 7400000, guid: %s, type: 2}\n"
        "  m_Tag: \n"
        "  m_SpeedParameter: \n"
        "  m_MirrorParameter: \n"
        "  m_CycleOffsetParameter: \n"
        "  m_TimeParameter: \n"
        % (sid, name, speed, head, pos[0], pos[1], motion_guid)
    )


def trans_doc(tid, conds, dst, can_self):
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
        "  m_Name: \n"
        "  m_Conditions:\n"
        "%s"
        "  m_DstStateMachine: {fileID: 0}\n"
        "  m_DstState: {fileID: %d}\n"
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
        "  m_CanTransitionToSelf: %d\n" % (tid, body, dst, can_self)
    )


IF, IF_NOT = 1, 2


def patch(path):
    s = io.open(path, encoding="utf-8").read()
    if "m_Name: CrouchMove" in s:
        print("skip (already patched):", path)
        return

    docs = list(DOC_RE.finditer(s))
    states = {}
    base_sm = None
    for m in docs:
        cls, rid, body = m.group(1), int(m.group(2)), m.group(3)
        if cls == "1102":
            nm = re.search(r"\n  m_Name: ([^\n]+)\n", body)
            if nm:
                states[nm.group(1).strip()] = rid
        elif cls == "1107" and "m_Name: Base Layer" in body:
            base_sm = m
    idle_key = "Idle" if "Idle" in states else ("Idle 1" if "Idle 1" in states else None)
    if base_sm is None or idle_key is None or "Move" not in states:
        print("!! 结构不符，跳过:", path, list(states))
        return

    idle_id, move_id = states[idle_key], states["Move"]
    tag = path.replace("\\", "/").split("/")[-1]

    ci, cm = gid(tag + "CrouchIdle"), gid(tag + "CrouchMove")
    pi, pm = gid(tag + "ProneIdle"), gid(tag + "ProneMove")
    t = {k: gid(tag + k) for k in
         ("any_ci", "any_cm", "any_pi", "any_pm", "ci_out", "cm_out", "pi_out", "pm_out")}

    new_docs = [
        state_doc(ci, "CrouchIdle", CROUCH_GUID, 0, [t["ci_out"]], (520, 400)),
        state_doc(cm, "CrouchMove", CROUCH_GUID, 1, [t["cm_out"]], (700, 400)),
        state_doc(pi, "ProneIdle", PRONE_GUID, 0, [t["pi_out"]], (520, 500)),
        state_doc(pm, "ProneMove", PRONE_GUID, 1, [t["pm_out"]], (700, 500)),
        trans_doc(t["any_ci"], [(IF, "IsCrouch"), (IF_NOT, "IsMove")], ci, 0),
        trans_doc(t["any_cm"], [(IF, "IsCrouch"), (IF, "IsMove")], cm, 0),
        trans_doc(t["any_pi"], [(IF, "IsProne"), (IF_NOT, "IsMove")], pi, 0),
        trans_doc(t["any_pm"], [(IF, "IsProne"), (IF, "IsMove")], pm, 0),
        trans_doc(t["ci_out"], [(IF_NOT, "IsCrouch")], idle_id, 0),
        trans_doc(t["cm_out"], [(IF_NOT, "IsCrouch")], move_id, 0),
        trans_doc(t["pi_out"], [(IF_NOT, "IsProne")], idle_id, 0),
        trans_doc(t["pm_out"], [(IF_NOT, "IsProne")], move_id, 0),
    ]

    start, end = base_sm.start(), base_sm.end()
    body = s[start:end]

    child_add = "".join(
        "  - serializedVersion: 1\n    m_State: {fileID: %d}\n    m_Position: {x: %d, y: %d, z: 0}\n"
        % (sid, x, y)
        for sid, (x, y) in ((ci, (520, 400)), (cm, (700, 400)), (pi, (520, 500)), (pm, (700, 500)))
    )
    any_add = "".join(
        "  - {fileID: %d}\n"
        % t[k]
        for k in ("any_ci", "any_cm", "any_pi", "any_pm")
    )

    body = body.replace("  m_ChildStateMachines: []", child_add + "  m_ChildStateMachines: []", 1)
    body = body.replace("  m_EntryTransitions: []", any_add + "  m_EntryTransitions: []", 1)

    s = s[:start] + body + s[end:]
    s = s.rstrip("\n") + "\n" + "".join(new_docs)
    io.open(path, "w", encoding="utf-8", newline="\n").write(s)
    print("patched:", tag, "states=", (ci, cm, pi, pm))


for p in CONTROLLERS:
    patch(p)
