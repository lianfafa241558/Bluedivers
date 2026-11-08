import io, os, re, sys

path = sys.argv[1]
s = io.open(path, encoding='utf-8').read()

# map guid -> script path
guids = {}
for root, dirs, files in os.walk('Assets'):
    for f in files:
        if f.endswith('.cs.meta'):
            p = os.path.join(root, f)
            try:
                txt = io.open(p, encoding='utf-8', errors='ignore').read()
            except Exception:
                continue
            m = re.search(r'guid: (\w+)', txt)
            if m:
                guids[m.group(1)] = p[:-5]

# objects
objs = {}
for m in re.finditer(r'--- !u!(\d+) &(-?\d+)\n(.*?)(?=\n--- |\Z)', s, re.S):
    objs[m.group(2)] = (m.group(1), m.group(3))

# gameobject names
goname = {}
for k, (cls, body) in objs.items():
    if cls == '1':
        nm = re.search(r'm_Name: (.*)', body)
        goname[k] = nm.group(1) if nm else ''

for k, (cls, body) in objs.items():
    if cls == '114':
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
        sc = re.search(r'm_Script: \{fileID: \d+, guid: (\w+)', body)
        en = re.search(r'm_Enabled: (\d)', body)
        print(goname.get(go.group(1) if go else '', '?'), '|', guids.get(sc.group(1) if sc else '', sc.group(1) if sc else '?'), '| enabled', en.group(1) if en else '?')
