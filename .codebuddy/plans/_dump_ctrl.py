import io, re, sys

path = sys.argv[1]
s = io.open(path, encoding='utf-8').read()

# state machines
for m in re.finditer(r'--- !u!(110|91) &(-?\d+)\n(.*?)(?=\n--- |\Z)', s, re.S):
    cls = m.group(1)
    body = m.group(3)
    if cls == '110':
        name = re.search(r'm_Name: (.*)', body)
        print('STATE', m.group(2), name.group(1) if name else '')
        mo = re.search(r'm_Motion: \{fileID: (-?\d+), guid: (\w+)', body)
        speed = re.search(r'm_Speed: (.*)', body)
        print('   motion', mo.groups() if mo else None, 'speed', speed.group(1) if speed else '')
    else:
        name = re.search(r'm_Name: (.*)', body)
        print('SM', m.group(2), name.group(1) if name else '')
        ds = re.findall(r'm_State: \{fileID: (-?\d+)\}', body)
        print('   states', ds)

# transitions
for m in re.finditer(r'--- !u!122 &(-?\d+)\n(.*?)(?=\n--- |\Z)', s, re.S):
    body = m.group(2)
    dst = re.search(r'm_DstState: \{fileID: (-?\d+)\}', body)
    conds = re.findall(r'm_ConditionMode: (\d+)\n\s+m_ConditionEvent: (.*?)\n\s+m_EventTreshold: (.*?)\n', body)
    exit_t = re.search(r'm_HasExitTime: (\d)', body)
    print('TRANS', m.group(1), '->', dst.group(1) if dst else '?', conds, 'exitTime', exit_t.group(1) if exit_t else '')
