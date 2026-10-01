# -*- coding: utf-8 -*-
"""完整检测：Assets 中被 prefab/scene/asset 引用、但磁盘上已不存在的 GUID（悬空引用）。
并将悬空 GUID 回查 git HEAD 的 .meta，得到它原本属于哪个脚本（旧路径），从而给出修复映射。"""
import subprocess, os, io, re, collections

REPO = r'd:\Pro\Bluedivers'
REF_EXT = {'.prefab', '.unity', '.asset', '.controller', '.playable', '.mat', '.anim',
           '.preset', '.overrideController', '.inputactions'}

def git(*a):
    return subprocess.run(['git', '-C', REPO] + list(a), capture_output=True,
                          text=True, encoding='utf-8', errors='replace').stdout

def cat_blobs(shas):
    if not shas: return {}
    inp = ('\n'.join(shas) + '\n').encode('utf-8')
    out = subprocess.run(['git', '-C', REPO, 'cat-file', '--batch'], input=inp, capture_output=True).stdout
    res = {}
    for m in re.finditer(rb'([0-9a-f]{40}) blob (\d+)\n', out):
        res[m.group(1).decode()] = out[m.end():m.end() + int(m.group(2))]
    return res

# 1) 磁盘上所有 .meta 的 guid -> 路径
disk_guid = {}
for r, d, fs in os.walk(os.path.join(REPO, 'Assets')):
    for f in fs:
        if f.endswith('.meta'):
            full = os.path.join(r, f)
            t = io.open(full, encoding='utf-8-sig', errors='replace').read()
            m = re.search(r'guid:\s*([0-9a-f]{32})', t)
            if m:
                disk_guid[m.group(1)] = os.path.relpath(full, REPO).replace('\\', '/')

# 2) 扫描引用
refs = collections.defaultdict(list)
for root, d, fs in os.walk(os.path.join(REPO, 'Assets')):
    for f in fs:
        if os.path.splitext(f)[1] not in REF_EXT: continue
        full = os.path.join(root, f)
        try: data = open(full, 'rb').read()
        except Exception: continue
        for m in set(re.findall(rb'guid:\s*([0-9a-f]{32})', data)):
            refs[m.decode()].append(os.path.relpath(full, REPO).replace('\\', '/'))

dangling = {g: v for g, v in refs.items() if g not in disk_guid}

# 3) 回查 HEAD，得到旧 guid -> 旧路径
tree = git('ls-tree', '-r', 'HEAD', '--', 'Assets')
head = {}
for line in tree.splitlines():
    if line.strip():
        meta, path = line.split('\t', 1); head[path] = meta.split()[2]
head_metas = {p: s for p, s in head.items() if p.endswith('.meta')}
sha2p = {}
for sha in set(head_metas.values()):
    pass
bl = cat_blobs(list(set(head_metas.values())))
head_guid2path = {}
for p, sha in head_metas.items():
    c = bl.get(sha)
    if not c: continue
    m = re.search(rb'guid:\s*([0-9a-f]{32})', c)
    if m: head_guid2path[m.group(1).decode()] = p

# 4) 当前磁盘 .cs 名字索引（用于把旧路径映射到新路径）
disk_cs_by_name = collections.defaultdict(list)
for r, d, fs in os.walk(os.path.join(REPO, 'Assets', 'Scripts')):
    for f in fs:
        if f.endswith('.cs'):
            disk_cs_by_name[f].append(os.path.relpath(os.path.join(r, f), REPO).replace('\\', '/'))

print('磁盘 .meta 数        :', len(disk_guid))
print('被引用 GUID 总数     :', len(refs))
print('悬空 GUID 数         :', len(dangling))
print()
script_dangling = []
other_dangling = []
for g, files in dangling.items():
    op = head_guid2path.get(g)
    if op and op.endswith('.cs.meta'):
        script_dangling.append((g, op, files))
    else:
        other_dangling.append((g, op, files))

print('其中「脚本(MonoScript)悬空」:', len(script_dangling))
print('其中「其它资产悬空」        :', len(other_dangling))
print()
print('=== 脚本悬空明细 (旧路径 -> 候选新路径) ===')
for g, op, files in sorted(script_dangling, key=lambda x: x[1]):
    name = op.rsplit('/', 1)[-1][:-8]  # xxx.cs.meta -> xxx.cs
    newc = disk_cs_by_name.get(name, [])
    print(f'  old {g}  <= {op}')
    print(f'        现磁盘同名: {newc if newc else "(未找到同名!)"}')
    for x in files[:3]:
        print('        REF', x)
print()
if other_dangling:
    print('=== 其它悬空(前 20) ===')
    for g, op, files in other_dangling[:20]:
        print(f'  {g}  <= {op}   refs={len(files)}')
