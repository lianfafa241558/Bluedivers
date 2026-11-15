import re, sys, io

path = sys.argv[1]
txt = io.open(path, encoding='utf-8').read()

# 拆成 YAML 文档
docs = re.split(r'^--- !u!(\d+) &(\d+)', txt, flags=re.M)
# docs = [head, classId, fileId, body, classId, fileId, body, ...]
nodes = {}      # fileId -> dict(name, layer, go)
trans = {}      # fileId -> (goFileId, fatherFileId)

for i in range(1, len(docs), 3):
    cls, fid, body = docs[i], docs[i + 1], docs[i + 2]
    if cls == '1':
        name = re.search(r'm_Name: (.*)', body)
        layer = re.search(r'm_Layer: (\d+)', body)
        nodes[fid] = {'name': (name.group(1).strip() if name else '?'),
                      'layer': int(layer.group(1)) if layer else -1}
    elif cls == '4':
        go = re.search(r'm_GameObject: \{fileID: (\d+)\}', body)
        father = re.search(r'm_Father: \{fileID: (\d+)\}', body)
        trans[fid] = (go.group(1) if go else None, father.group(1) if father else None)

# go fileId -> transform fileId
go2tr = {v[0]: k for k, v in trans.items()}

def children(trFileId):
    out = []
    for t, (go, father) in trans.items():
        if father == trFileId:
            out.append(t)
    return out

def walk(trFileId, depth, rootTr):
    go, father = trans[trFileId]
    n = nodes.get(go, {'name': '?', 'layer': -1})
    print('%s%s  [layer=%d]' % ('  ' * depth, n['name'], n['layer']))
    if depth > 6:
        print('%s...' % ('  ' * (depth + 1)))
        return
    for c in children(trFileId):
        walk(c, depth + 1, rootTr)

# 根：没有 father 的 transform
for t, (go, father) in trans.items():
    if not father or father == '0':
        walk(t, 0, t)
