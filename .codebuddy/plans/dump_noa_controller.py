import re

paths = {
    '1408d645c89863947a74a21776ae387d': 'Noa.controller',
    '850cdd3f0131411ea4aa7153d10db89a': 'ProneWalk.anim',
    '4554d44964004d0eb1f3b00cb7b3c9da': 'CrouchWalk.anim',
    '790cdb7c48f0d8e419a5febf28dbb882': 'Idle 1.anim',
    '1b3c7695c1ac90247ab85ebd01d3e776': 'Idle 2.anim',
    'c4fd40ddd7b83e84fbc0d0ca4d7d3b68': 'Walk.anim',
    '3bb5c2e6e7dd5e4418d43fb982cb1f09': 'Ready.anim',
    '1792f6d1223361f46b1ae69ce3304034': 'Death.anim',
    '9ca31535f26d72b428c5f02950ad8fe5': '??? (外部 clip)',
}

s = open('Assets/Art/Anim/Student/Noa/Noa.controller', encoding='utf-8').read()


def name_of(guid):
    return paths.get(guid, guid)


print('==== PARAMETERS ====')
for m in re.finditer(r'  - m_Name: (\w+)\n    m_Type: (\d+)', s):
    print('  ', m.group(1), 'type=', m.group(2))

print('==== LAYERS ====')
for m in re.finditer(r'  - serializedVersion: \d+\n    m_Name: ([^\n]*)\n    m_StateMachine: ([^\n]*)', s):
    print('  layer:', repr(m.group(1)), m.group(2))

print('==== STATES ====')
for m in re.finditer(r'(AnimatorState|AnimatorStateMachine):\n(.*?)(?=\n--- !u!)', s, re.S):
    kind, body = m.group(1), m.group(2)
    n = re.search(r'\n  m_Name: ([^\n]*)', body)
    mot = re.search(r'\n  m_Motion: \{fileID: \d+, guid: (\w+)', body)
    spd = re.search(r'\n  m_Speed: ([^\n]*)', body)
    mirror = re.search(r'\n  m_Mirror: ([^\n]*)', body)
    if kind == 'AnimatorState':
        print('  STATE', repr(n.group(1) if n else '?'),
              '|', name_of(mot.group(1)) if mot else '(none)',
              '| speed', spd.group(1) if spd else '-',
              '| mirror', mirror.group(1) if mirror else '-')
    else:
        print('  MACHINE', repr(n.group(1) if n else '?'))

print('==== TRANSITIONS ====')
for m in re.finditer(r'AnimatorStateTransition:\n(.*?)(?=\n--- !u!)', s, re.S):
    b = m.group(1)
    conds = re.findall(r'm_ConditionMode: (\d+)\n      m_ConditionEvent: ([^\n]*)\n      m_EventTreshold: ([^\n]*)', b)
    dur = re.search(r'\n  m_TransitionDuration: ([^\n]*)', b)
    dst = re.findall(r'm_DstState: \{fileID: (\d+)\}', b)
    hasExit = re.search(r'm_HasExitTime: ([^\n]*)', b)
    print('  T->', dst, 'dur', dur.group(1) if dur else '-', 'exit', hasExit.group(1) if hasExit else '-', conds)
