# -*- coding: utf-8 -*-
"""在 Assets 文本类资产(prefab/scene/asset/...)中查找「GUID 被改写」那 32 个脚本的旧 GUID，
判断是否存在悬空引用(Missing Script)。"""
import subprocess, os, io, re, hashlib, collections

REPO = r'd:\Pro\Bluedivers'
TEXT_EXT = {'.prefab', '.unity', '.asset', '.controller', '.mat', '.anim', '.playable',
            '.preset', '.inputactions', '.json', '.txt', '.overrideController', '.meta'}
SCAN_EXT = {'.prefab', '.unity', '.asset', '.controller', '.playable', '.mat', '.anim',
            '.preset', '.inputactions', '.overrideController'}

def git(*a):
    return subprocess.run(['git', '-C', REPO] + list(a), capture_output=True,
                          text=True, encoding='utf-8', errors='replace').stdout

def norm(b): return b.replace(b'\r\n', b'\n')

def cat_blobs(shas):
    if not shas: return {}
    inp = ('\n'.join(shas) + '\n').encode('utf-8')
    out = subprocess.run(['git', '-C', REPO, 'cat-file', '--batch'], input=inp, capture_output=True).stdout
    res = {}
    for m in re.finditer(rb'([0-9a-f]{40}) blob (\d+)\n', out):
        res[m.group(1).decode()] = out[m.end():m.end() + int(m.group(2))]
    return res

tree = git('ls-tree', '-r', 'HEAD', '--', 'Assets/Scripts')
head = {}
for line in tree.splitlines():
    if line.strip():
        meta, path = line.split('\t', 1); head[path] = meta.split()[2]

disk = set()
for r, d, fs in os.walk(os.path.join(REPO, 'Assets', 'Scripts')):
    for f in fs:
        disk.add(os.path.relpath(os.path.join(r, f), REPO).replace('\\', '/'))

gone = [p for p in head if p.endswith('.cs') and p not in disk]
newcs = [p for p in disk if p.endswith('.cs') and p not in head]
blobs = cat_blobs([head[p] for p in gone])
h2o = {}
for p in gone:
    c = blobs.get(head[p])
    if c is not None: h2o.setdefault(hashlib.md5(norm(c)).hexdigest(), []).append(p)

om = [p + '.meta' for p in gone if p + '.meta' in head]
sha2g = {}
for sha, c in cat_blobs([head[p] for p in om]).items():
    m = re.search(rb'guid:\s*([0-9a-f]{32})', c); sha2g[sha] = m.group(1).decode() if m else None

def guid_disk(p):
    t = io.open(os.path.join(REPO, p), encoding='utf-8-sig', errors='replace').read()
    m = re.search(r'guid:\s*([0-9a-f]{32})', t); return m.group(1) if m else None

changed = []
for np in sorted(newcs):
    h = hashlib.md5(norm(open(os.path.join(REPO, np), 'rb').read())).hexdigest()
    for op in h2o.get(h, [])[:1]:
        og = sha2g.get(head.get(op + '.meta'))
        ng = guid_disk(np + '.meta') if os.path.exists(os.path.join(REPO, np + '.meta')) else None
        if og and ng and og != ng:
            changed.append((op, og, np, ng))

print('GUID 被改写脚本数:', len(changed))
print()
# 扫描引用
refs = collections.defaultdict(list)
oldset = {c[1] for c in changed}
for root, dirs, files in os.walk(os.path.join(REPO, 'Assets')):
    for f in files:
        ext = os.path.splitext(f)[1]
        if ext not in SCAN_EXT: continue
        full = os.path.join(root, f)
        try:
            data = open(full, 'rb').read()
        except Exception:
            continue
        for g in oldset:
            if g.encode() in data:
                refs[g].append(os.path.relpath(full, REPO).replace('\\', '/'))

hit = [c for c in changed if refs.get(c[1])]
print('旧 GUID 仍被引用的脚本数:', len(hit), '/', len(changed))
print()
for op, og, np, ng in changed:
    r = refs.get(og, [])
    flag = '!! 有悬空引用' if r else '   无引用(安全)'
    print(f'{flag}  {np}')
    print(f'        old {og}  new {ng}   <- {op}')
    for x in r[:6]:
        print('          REF', x)
