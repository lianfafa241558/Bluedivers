# -*- coding: utf-8 -*-
"""列 controller 的 state / transition 条件：source(含 AnyState/Entry) -> dst [conds]"""
import io
import re
import sys

s = io.open(sys.argv[1], encoding='utf-8').read()
blocks = re.split(r'\n--- !u!(?=\d)', s)

states, trans, sms = {}, {}, {}
for b in blocks:
    m = re.match(r'(\d+) &(-?\d+)\n(.*)', b, re.S)
    if not m:
        continue
    cls, oid, body = m.group(1), m.group(2), m.group(3)
    if cls == '1102':
        name = re.search(r'm_Name: (.*)', body)
        seg = body[body.find('m_Transitions:'):] if 'm_Transitions:' in body else ''
        states[oid] = (name.group(1) if name else '?', re.findall(r'- \{fileID: (-?\d+)\}', seg))
    elif cls == '1107':
        name = re.search(r'm_Name: (.*)', body)
        any_seg = body[body.find('m_AnyStateTransitions:'):] if 'm_AnyStateTransitions:' in body else ''
        ent_seg = body[body.find('m_EntryTransitions:'):] if 'm_EntryTransitions:' in body else ''
        dft = re.search(r'm_DefaultState: \{fileID: (-?\d+)\}', body)
        sms[oid] = (name.group(1) if name else '?', re.findall(r'- \{fileID: (-?\d+)\}', any_seg),
                    re.findall(r'- \{fileID: (-?\d+)\}', ent_seg), dft.group(1) if dft else '0')
    elif cls == '1101':
        dst = re.search(r'm_DstState: \{fileID: (-?\d+)\}', body)
        conds = re.findall(r'm_ConditionMode: (\d+)\n\s+m_ConditionEvent: (.*?)\n\s+m_EventTreshold: (.*?)\n', body)
        exit_t = re.search(r'm_HasExitTime: (\d)', body)
        trans[oid] = (dst.group(1) if dst else '0',
                      [(c[1], 'true' if c[0] == '1' else 'false' if c[0] == '2' else c[0]) for c in conds],
                      exit_t.group(1) if exit_t else '?')


def sname(i):
    return states.get(i, ('<exit>', []))[0] if i != '0' else '<exit>'


for sid, (name, tids) in states.items():
    for t in tids:
        if t in trans:
            dst, conds, et = trans[t]
            print(f'{name} -> {sname(dst)}  {conds} exitTime={et}')

for oid, (name, anyt, ent, dft) in sms.items():
    print(f'SM {name} default={sname(dft)}')
    for t in anyt:
        if t in trans:
            dst, conds, et = trans[t]
            print(f'  [AnyState] -> {sname(dst)}  {conds} exitTime={et}')
    for t in ent:
        if t in trans:
            dst, conds, et = trans[t]
            print(f'  [Entry] -> {sname(dst)}  {conds} exitTime={et}')
