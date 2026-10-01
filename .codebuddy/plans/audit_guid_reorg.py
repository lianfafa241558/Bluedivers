# -*- coding: utf-8 -*-
"""审计外部整理导致的 .meta GUID 变更程度。
按 .cs 内容哈希(归一化换行)把「HEAD 中已从磁盘消失的 .cs」与「磁盘新位置未跟踪的 .cs」配对，
比较二者 .meta 的 guid，找出 GUID 被重新生成的脚本（=> 预制体引用会 Missing Script）。
"""
import subprocess, os, io, re, hashlib, collections, sys

REPO = r'd:\Pro\Bluedivers'
def git(*a):
    return subprocess.run(['git', '-C', REPO] + list(a), capture_output=True,
                          text=True, encoding='utf-8', errors='replace').stdout

def norm(b):
    return b.replace(b'\r\n', b'\n')

def cat_blobs(shas):
    """批量读取 blob 内容: sha -> bytes"""
    if not shas:
        return {}
    inp = ('\n'.join(shas) + '\n').encode('utf-8')
    out = subprocess.run(['git', '-C', REPO, 'cat-file', '--batch'],
                         input=inp, capture_output=True).stdout
    res = {}
    for m in re.finditer(rb'([0-9a-f]{40}) blob (\d+)\n', out):
        sha = m.group(1).decode(); size = int(m.group(2)); start = m.end()
        res[sha] = out[start:start + size]
    return res

# 1) HEAD tracked
tree = git('ls-tree', '-r', 'HEAD', '--', 'Assets/Scripts')
head = {}
for line in tree.splitlines():
    if not line.strip():
        continue
    meta, path = line.split('\t', 1)
    head[path] = meta.split()[2]

# 2) 磁盘现状
disk = set()
for r, d, fs in os.walk(os.path.join(REPO, 'Assets', 'Scripts')):
    for f in fs:
        rel = os.path.relpath(os.path.join(r, f), REPO).replace('\\', '/')
        disk.add(rel)

# 3) 消失的 .cs 与新出现的 .cs
gone = [p for p in head if p.endswith('.cs') and p not in disk]
newcs = [p for p in disk if p.endswith('.cs') and p not in head]

# 4) 读取内容做哈希匹配
gone_cs = {p: head[p] for p in gone}
blobs = cat_blobs(list(gone_cs.values()))
hash2old = {}
for p, sha in gone_cs.items():
    c = blobs.get(sha)
    if c is None:
        continue
    hash2old.setdefault(hashlib.md5(norm(c)).hexdigest(), []).append(p)

def read(p):
    return open(os.path.join(REPO, p), 'rb').read()

def guid_of_meta_disk(p):
    t = io.open(os.path.join(REPO, p), encoding='utf-8-sig', errors='replace').read()
    m = re.search(r'guid:\s*([0-9a-f]{32})', t)
    return m.group(1) if m else None

# 5) 批量读 HEAD 旧 meta guid
old_meta_paths = [p + '.meta' for p in gone]
om_shas = [head[p] for p in old_meta_paths if p in head]
om_blobs = cat_blobs(om_shas) if om_shas else {}
sha2guid = {}
for sha, c in om_blobs.items():
    m = re.search(rb'guid:\s*([0-9a-f]{32})', c)
    sha2guid[sha] = m.group(1).decode() if m else None

matched = []      # (old_cs, old_guid, new_cs, new_guid)
unmatched = []
for np in sorted(newcs):
    h = hashlib.md5(norm(read(np))).hexdigest()
    olds = hash2old.get(h)
    if not olds:
        unmatched.append(np)
        continue
    op = olds[0]
    og = sha2guid.get(head.get(op + '.meta')) if (op + '.meta') in head else None
    ng = guid_of_meta_disk(np + '.meta') if os.path.exists(os.path.join(REPO, np + '.meta')) else None
    matched.append((op, og, np, ng))

changed = [m for m in matched if m[1] and m[3] and m[1] != m[3]]
same = [m for m in matched if m[1] and m[3] and m[1] == m[3]]

print('HEAD 下 .cs 总数            :', sum(1 for p in head if p.endswith('.cs')))
print('磁盘 .cs 总数              :', sum(1 for p in disk if p.endswith('.cs')))
print('消失(旧路径) .cs           :', len(gone))
print('新增(新路径) .cs           :', len(newcs))
print('内容匹配上的移动对         :', len(matched))
print('  ├ GUID 不同(需修复)      :', len(changed))
print('  └ GUID 相同(正常)        :', len(same))
print('内容未匹配(改名/被改)      :', len(unmatched))
print()
def top(p):
    parts = p.split('/')
    return '/'.join(parts[:4])
c = collections.Counter(top(np) for _, _, np, _ in changed)
print('=== GUID 被改写的脚本，按新目录分布 ===')
for k, v in sorted(c.items(), key=lambda x: -x[1]):
    print(f'  {v:3d}  {k}')
print()
print('=== 样例(前 25) 旧guid -> 新guid ===')
for op, og, np, ng in changed[:25]:
    print(f'  {op}\n      {og} -> {ng}\n      => {np}')
if unmatched:
    print()
    print('=== 未匹配(前 20) ===')
    for u in unmatched[:20]:
        print('  ', u)
