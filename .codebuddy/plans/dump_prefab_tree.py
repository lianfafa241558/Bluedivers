import re, sys, io

path = sys.argv[1] if len(sys.argv) > 1 else r"d:\Pro\Bluedivers\Assets\Resources\UI\Wnd\SelectMapWnd.prefab"
text = io.open(path, encoding="utf-8-sig", errors="replace").read()

docs = re.split(r"^--- !u!(\d+) &(\d+)", text, flags=re.M)
# docs: [pre, type, id, body, type, id, body, ...]
objs = {}
i = 1
while i < len(docs):
    t = docs[i]
    oid = docs[i + 1]
    body = docs[i + 2] if i + 2 < len(docs) else ""
    objs[oid] = (t, body)
    i += 3

go_name = {}
for oid, (t, body) in objs.items():
    if t == "1":
        m = re.search(r"m_Name: (.*)", body)
        go_name[oid] = m.group(1).strip() if m else ""

tr = {}
for oid, (t, body) in objs.items():
    if t in ("4", "224"):
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
        fa = re.search(r"m_Father: \{fileID: (\d+)\}", body)
        kids = re.findall(r"- \{fileID: (\d+)\}", body.split("m_Children:")[1]) if "m_Children:" in body else []
        tr[oid] = (t, go.group(1) if go else None, fa.group(1) if fa else "0", kids)

# root transform: father 0 or missing in tr
roots = [k for k, v in tr.items() if v[2] == "0" or v[2] not in tr]

def walk(oid, depth, index_path):
    t, goid, fa, kids = tr[oid]
    name = go_name.get(goid, "?")
    print("%s[%s] %s  (tr=%s go=%s)" % ("  " * depth, index_path, name, oid, goid))
    for idx, k in enumerate(kids):
        if k in tr:
            walk(k, depth + 1, "%s,%d" % (index_path, idx) if index_path else "%d" % idx)

for r in roots:
    walk(r, 0, "")
