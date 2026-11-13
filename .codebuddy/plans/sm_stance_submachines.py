# -*- coding: utf-8 -*-
"""把蹲/趴四个状态收进 Crouch / Prone 两个子状态机，并把站立/移动改成 AnyState 中枢。

结构（Base Layer）：
  AnyState -> Ready / Death / Crouch(SM) / Prone(SM) / Idle / Move
  Crouch(SM): Entry -> CrouchIdle | CrouchMove ; 两者互切
  Prone(SM) : 同上
站立时不再有 Idle<->Move 直连线，全部由 AnyState 中枢接管。
"""
import hashlib
import io
import re

CTRLS = [
    r"e:\Bluedivers\Assets\Art\Anim\Student\Aris\Aris.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Noa\Noa.controller",
    r"e:\Bluedivers\Assets\Art\Anim\Student\Nagisa\Nagisa.controller",
]

CROUCH_GUID = "4554d44964004d0eb1f3b00cb7b3c9da"
PRONE_GUID = "850cdd3f0131411ea4aa7153d10db89a"

DOC_RE = re.compile(r"--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- |\Z)", re.S)
IF, IF_NOT = 1, 2


def gid(seed):
    h = hashlib.md5(seed.encode("utf-8")).hexdigest()
    return -(int(h[:15], 16) % 9000000000000000000) - 1


def name_of(body):
    m = re.search(r"\n  m_Name:(?: (.*))?\n", body)
    return (m.group(1) or "").strip().strip('"')


def ref_of(body, key):
    """读 `  key: {fileID: n}` 里的 n（0 表示空引用）。"""
    m = re.search(r"\n  %s: \{fileID: (-?\d+)\}" % key, body)
    return int(m.group(1)) if m else 0


def list_refs(body, key):
    """读 m_XxxTransitions/m_Transitions 这类 `- {fileID: n}` 列表。"""
    m = re.search(r"\n  %s:\n((?:  - \{[^}]*\}\n)+)" % key, body)
    if not m:
        return []
    return [int(x) for x in re.findall(r"fileID: (-?\d+)", m.group(1))]


def replace_list(body, key, entries):
    """把 `  key:` 后面的列表整段换成 entries（entries 是若干行字符串）。"""
    lines = body.split("\n")
    out = []
    i = 0
    done = False
    while i < len(lines):
        if not done and lines[i].startswith("  " + key + ":"):
            done = True
            rest = lines[i][len("  " + key + ":"):].strip()
            i += 1
            # 吃掉旧列表（含多行条目）
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


def cond_lines(conds):
    return "".join(
        "  - m_ConditionMode: %d\n    m_ConditionEvent: %s\n    m_EventTreshold: 0\n" % c
        for c in conds
    )


def state_doc(sid, name, motion, speed, trans, pos):
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
        "  m_Position: {x: %d, y: %d, z: 0}\n"
        "  m_IKOnFeet: 1\n"
        "  m_WriteDefaultValues: 1\n"
        "  m_Mirror: 0\n"
        "  m_SpeedParameterActive: 0\n"
        "  m_MirrorParameterActive: 0\n"
        "  m_CycleOffsetParameterActive: 0\n"
        "  m_TimeParameterActive: 0\n"
        "  m_Motion: {fileID: 7400000, guid: %s, type: 2}\n"
        "  m_Tag:\n"
        "  m_SpeedParameter:\n"
        "  m_MirrorParameter:\n"
        "  m_CycleOffsetParameter:\n"
        "  m_TimeParameter:\n"
        % (
            sid, name, speed,
            ("  m_Transitions:\n  - {fileID: %d}\n" % trans) if trans else "  m_Transitions: []\n",
            pos[0], pos[1], motion,
        )
    )


def trans_doc(tid, conds, dst_state, dst_sm, can_self):
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
        "  m_CanTransitionToSelf: %d\n" % (tid, cond_lines(conds), dst_sm, dst_state, can_self)
    )


def entry_doc(tid, conds, dst_state):
    return (
        "--- !u!1109 &%d\n"
        "AnimatorTransition:\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_Name:\n"
        "  m_Conditions:\n"
        "%s"
        "  m_DstStateMachine: {fileID: 0}\n"
        "  m_DstState: {fileID: %d}\n"
        "  m_Solo: 0\n"
        "  m_Mute: 0\n"
        "  m_IsExit: 0\n"
        "  serializedVersion: 1\n" % (tid, cond_lines(conds), dst_state)
    )


def sm_doc(sid, name, children, entries, default, pos):
    ch = "".join(
        "  - serializedVersion: 1\n    m_State: {fileID: %d}\n    m_Position: {x: %d, y: %d, z: 0}\n"
        % (cid, x, y) for cid, (x, y) in children
    )
    en = "".join("  - {fileID: %d}\n" % e for e in entries)
    return (
        "--- !u!1107 &%d\n"
        "AnimatorStateMachine:\n"
        "  serializedVersion: 6\n"
        "  m_ObjectHideFlags: 1\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_Name: %s\n"
        "  m_ChildStates:\n"
        "%s"
        "  m_ChildStateMachines: []\n"
        "  m_AnyStateTransitions: []\n"
        "  m_EntryTransitions:\n"
        "%s"
        "  m_StateMachineTransitions: {}\n"
        "  m_StateMachineBehaviours: []\n"
        "  m_AnyStatePosition: {x: 50, y: 20, z: 0}\n"
        "  m_EntryPosition: {x: 50, y: 120, z: 0}\n"
        "  m_ExitPosition: {x: 800, y: 120, z: 0}\n"
        "  m_ParentStateMachinePosition: {x: 800, y: 20, z: 0}\n"
        "  m_DefaultState: {fileID: %d}\n" % (sid, name, ch, en, default)
    )


def patch(path):
    raw = io.open(path, encoding="utf-8").read()
    if "m_Name: Crouch\n" in raw:
        print("skip (already):", path)
        return

    docs = list(DOC_RE.finditer(raw))
    prefix = raw[:docs[0].start()]
    order = []
    info = {}
    for m in docs:
        cls, rid, body = m.group(1), int(m.group(2)), m.group(3)
        order.append(rid)
        info[rid] = [cls, body]

    base = None
    states = {}
    for rid, (cls, body) in info.items():
        if cls == "1102":
            states[name_of(body)] = rid
        elif cls == "1107" and name_of(body) == "Base Layer":
            base = rid

    idle_name = "Idle" if "Idle" in states else "Idle 1"
    idle, move = states[idle_name], states["Move"]
    ci, cm = states["CrouchIdle"], states["CrouchMove"]
    pi, pm = states["ProneIdle"], states["ProneMove"]

    # ---- 要删掉的过渡：蹲/趴四条出口 + Idle<->Move 两条 + 指向蹲/趴状态的 AnyState 四条
    stale = set()
    for sid in (ci, cm, pi, pm):
        stale.update(list_refs(info[sid][1], "m_Transitions"))
    for sid in (idle, move):
        for t in list_refs(info[sid][1], "m_Transitions"):
            dst = ref_of(info[t][1], "m_DstState") if t in info else 0
            if dst in (move, idle):
                stale.add(t)
    base_any = list_refs(info[base][1], "m_AnyStateTransitions")
    kept_any = []
    for t in base_any:
        dst = ref_of(info[t][1], "m_DstState")
        if dst in (ci, cm, pi, pm):
            stale.add(t)
        else:
            kept_any.append(t)

    for sid in (ci, cm, pi, pm):
        stale.add(sid)
    for t in stale:
        info.pop(t, None)

    tag = path.replace("\\", "/").split("/")[-1]
    ids = {k: gid(tag + k) for k in (
        "csm", "psm", "ci", "cm", "pi", "pm",
        "e_ci", "e_cm", "e_pi", "e_pm",
        "t_cicm", "t_cmci", "t_pipm", "t_pmpi",
        "a_crouch", "a_prone", "a_idle", "a_move")}

    # ---- Idle/Move 的直连过渡清空
    for sid in (idle, move):
        info[sid][1] = replace_list(info[sid][1], "m_Transitions", [])

    # ---- Base Layer 重写
    body = info[base][1]
    child_entries = [
        "  - serializedVersion: 1\n    m_State: {fileID: %d}\n    m_Position: {x: 190, y: 320, z: 0}" % idle,
        "  - serializedVersion: 1\n    m_State: {fileID: %d}\n    m_Position: {x: 363, y: 229, z: 0}" % move,
        "  - serializedVersion: 1\n    m_State: {fileID: %d}\n    m_Position: {x: 240, y: 120, z: 0}" % states["Ready"],
        "  - serializedVersion: 1\n    m_State: {fileID: %d}\n    m_Position: {x: 230, y: 20, z: 0}" % states["Death"],
    ]
    body = replace_list(body, "m_ChildStates", child_entries)
    body = replace_list(body, "m_ChildStateMachines", [
        "  - serializedVersion: 1\n    m_StateMachine: {fileID: %d}\n    m_Position: {x: 520, y: 400, z: 0}" % ids["csm"],
        "  - serializedVersion: 1\n    m_StateMachine: {fileID: %d}\n    m_Position: {x: 520, y: 540, z: 0}" % ids["psm"],
    ])
    any_entries = ["  - {fileID: %d}" % t for t in kept_any] + [
        "  - {fileID: %d}" % ids["a_crouch"],
        "  - {fileID: %d}" % ids["a_prone"],
        "  - {fileID: %d}" % ids["a_idle"],
        "  - {fileID: %d}" % ids["a_move"],
    ]
    body = replace_list(body, "m_AnyStateTransitions", any_entries)
    body = re.sub(r"\n  m_DefaultState: \{[^}]*\}", "\n  m_DefaultState: {fileID: %d}" % idle, body, count=1)
    info[base][1] = body

    # ---- 新增文档
    new = [
        state_doc(ids["ci"], "CrouchIdle", CROUCH_GUID, 0, ids["t_cicm"], (170, 100)),
        state_doc(ids["cm"], "CrouchMove", CROUCH_GUID, 0.7, ids["t_cmci"], (380, 100)),
        state_doc(ids["pi"], "ProneIdle", PRONE_GUID, 0, ids["t_pipm"], (170, 100)),
        state_doc(ids["pm"], "ProneMove", PRONE_GUID, 0.45, ids["t_pmpi"], (380, 100)),
        trans_doc(ids["t_cicm"], [(IF, "IsMove")], ids["cm"], 0, 1),
        trans_doc(ids["t_cmci"], [(IF_NOT, "IsMove")], ids["ci"], 0, 1),
        trans_doc(ids["t_pipm"], [(IF, "IsMove")], ids["pm"], 0, 1),
        trans_doc(ids["t_pmpi"], [(IF_NOT, "IsMove")], ids["pi"], 0, 1),
        entry_doc(ids["e_ci"], [(IF_NOT, "IsMove")], ids["ci"]),
        entry_doc(ids["e_cm"], [(IF, "IsMove")], ids["cm"]),
        entry_doc(ids["e_pi"], [(IF_NOT, "IsMove")], ids["pi"]),
        entry_doc(ids["e_pm"], [(IF, "IsMove")], ids["pm"]),
        sm_doc(ids["csm"], "Crouch",
               [(ids["ci"], (170, 100)), (ids["cm"], (380, 100))],
               [ids["e_ci"], ids["e_cm"]], ids["ci"], (520, 400)),
        sm_doc(ids["psm"], "Prone",
               [(ids["pi"], (170, 100)), (ids["pm"], (380, 100))],
               [ids["e_pi"], ids["e_pm"]], ids["pi"], (520, 540)),
        trans_doc(ids["a_crouch"], [(IF, "IsCrouch")], 0, ids["csm"], 0),
        trans_doc(ids["a_prone"], [(IF, "IsProne")], 0, ids["psm"], 0),
        trans_doc(ids["a_idle"], [(IF_NOT, "IsCrouch"), (IF_NOT, "IsProne"), (IF_NOT, "IsMove")], idle, 0, 0),
        trans_doc(ids["a_move"], [(IF_NOT, "IsCrouch"), (IF_NOT, "IsProne"), (IF, "IsMove")], move, 0, 0),
    ]

    out = [prefix]
    for rid in order:
        if rid in info:
            out.append("--- !u!%s &%d\n%s\n" % (info[rid][0], rid, info[rid][1]))
    out.extend(new)
    io.open(path, "w", encoding="utf-8", newline="\n").write("".join(out))
    print("patched:", tag, "removed", len(stale), "docs")


for p in CTRLS:
    patch(p)
