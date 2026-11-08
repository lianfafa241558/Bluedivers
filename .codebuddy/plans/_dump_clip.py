import io, os, sys, re

guid = sys.argv[1]
hit = None
for root, dirs, files in os.walk('Assets'):
    for f in files:
        if f.endswith('.anim.meta'):
            p = os.path.join(root, f)
            try:
                if guid in io.open(p, encoding='utf-8', errors='ignore').read():
                    hit = p
                    break
            except Exception:
                pass
    if hit:
        break
print('meta:', hit)
if not hit:
    raise SystemExit
p = hit[:-5]
s = io.open(p, encoding='utf-8').read()
print('path:', p)
m = re.search(r'm_Name: (.*)', s)
print('clip name:', m.group(1) if m else '')
print('loop:', re.search(r'm_LoopTime: (\d)', s).group(1) if re.search(r'm_LoopTime: (\d)', s) else '?')
print('stopTime/rate:', re.search(r'm_StopTime: (.*)', s).group(1) if re.search(r'm_StopTime: (.*)', s) else '?')
for b in re.finditer(r'attribute: (\S+)\n(\s+path: .*?)?\n\s+classID: (\d+)\n\s+script: .*?\n\s+typeID: (\d+)', s):
    pass
# crude: list bindings and curve counts
print('--- bindings ---')
for b in re.finditer(r'- curve:\n((?:    .*\n)+?)\s*attribute: (\S+)\n\s+path: (.*)\n\s+classID: (\d+)\n', s):
    body, attr, path, cls = b.group(1), b.group(2), b.group(3), b.group(4)
    keys = body.count('time:')
    print(attr, '|', path, '| classID', cls, '| keys', keys)
print('--- first curve values ---')
for b in re.finditer(r'- curve:\n((?:    .*\n)+?)\s*attribute: (\S+)\n\s+path: (.*)\n\s+classID: (\d+)\n', s):
    body, attr, path, cls = b.group(1), b.group(2), b.group(3), b.group(4)
    if attr in ('m_Alpha', 'm_LocalScale.x', 'm_AnchoredPosition.x'):
        vals = re.findall(r'value: (.*)', body)
        print(attr, path, '->', vals[:4], vals[-1:] if len(vals) > 4 else '')
