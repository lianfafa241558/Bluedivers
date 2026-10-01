# -*- coding: utf-8 -*-
"""修复「外部整理导致 GUID 被重新分配」⇒ 脚本引用悬空(Missing Script)。

思路：对每个"被 prefab/scene/asset 引用、但磁盘已无该 guid"的脚本 guid，
回查 git HEAD 得到它原本所属的旧 .cs，再用 内容哈希 / 唯一同名 / 类名 三路匹配定位
到工作区里的现文件，最后把「旧 guid」写回现文件的 .meta（Unity 刷新后引用即恢复）。

用法：
    python fix_guid_dangling.py            # 只出计划
    python fix_guid_dangling.py --apply    # 落地修复
"""
import os, re, subprocess, sys, hashlib, collections

ROOT = r'd:\Pro\Bluedivers'
ASSETS = os.path.join(ROOT, 'Assets')
REF_EXT = ('.prefab', '.unity', '.asset', '.controller', '.playable', '.mat',
           '.anim', '.preset', '.overridecontroller', '.inputactions')
GUID_RE = re.compile(rb'guid:\s*([0-9a-fA-F]{32})')
BLOCK_RE = re.compile(rb'([0-9a-f]{40}) blob (\d+)\n')


def git(*a):
    return subprocess.run(['git', '-C', ROOT] + list(a), capture_output=True,
                          text=True, encoding='utf-8', errors='replace').stdout


def cat(shas):
    if not shas:
        return {}
    out = subprocess.run(['git', '-C', ROOT, 'cat-file', '--batch'],
                         input=('\n'.join(shas) + '\n').encode(), capture_output=True).stdout
    res = {}
    for m in BLOCK_RE.finditer(out):
        res[m.group(1).decode()] = out[m.end():m.end() + int(m.group(2))]
    return res


def norm(b):
    return b.replace(b'\r\n', b'\n')


def main():
    apply = '--apply' in sys.argv

    tree = git('ls-tree', '-r', 'HEAD', '--', 'Assets')
    head = {}
    for line in tree.splitlines():
        if line.strip():
            meta, path = line.split('\t', 1)
            head[path] = meta.split()[2]
    head_cs = {p: s for p, s in head.items() if p.endswith('.cs')}
    head_meta = {p: s for p, s in head.items() if p.endswith('.meta')}
    hm_blobs = cat(list(set(head_meta.values())))
    head_g2p, head_p2g = {}, {}
    for p, s in head_meta.items():
        c = hm_blobs.get(s)
        if not c:
            continue
        m = GUID_RE.search(c)
        if m:
            g = m.group(1).decode()
            head_g2p[g] = p
            head_p2g[p] = g
    head_cs_blobs = cat(list(set(head_cs.values())))

    disk_g2p, disk_p2g, disk_cs = {}, {}, []
    for dirpath, _d, fns in os.walk(ASSETS):
        for fn in fns:
            full = os.path.join(dirpath, fn)
            rel = os.path.relpath(full, ROOT).replace('\\', '/')
            if fn.endswith('.meta'):
                m = GUID_RE.search(open(full, 'rb').read(400))
                if m:
                    disk_g2p[m.group(1).decode()] = rel
                    disk_p2g[rel] = m.group(1).decode()
            elif fn.endswith('.cs'):
                disk_cs.append(rel)

    refs = collections.defaultdict(set)
    for dirpath, _d, fns in os.walk(ASSETS):
        for fn in fns:
            if os.path.splitext(fn)[1].lower() not in REF_EXT:
                continue
            p = os.path.join(dirpath, fn)
            try:
                data = open(p, 'rb').read()
            except OSError:
                continue
            rel = os.path.relpath(p, ROOT).replace('\\', '/')
            for g in set(GUID_RE.findall(data)):
                refs[g.decode()].add(rel)

    dangling = []
    for g, files in refs.items():
        if g in disk_g2p or g.startswith('0000000000000000'):
            continue
        hp = head_g2p.get(g)
        if hp and hp.endswith('.cs.meta'):
            dangling.append((g, hp[:-len('.meta')], sorted(files)))

    cur_hash = collections.defaultdict(list)
    cur_content = {}
    for p in disk_cs:
        b = open(os.path.join(ROOT, p), 'rb').read()
        cur_hash[hashlib.md5(norm(b)).hexdigest()].append(p)
        cur_content[p] = b
    by_base = collections.defaultdict(list)
    for p in disk_cs:
        by_base[os.path.basename(p)].append(p)
    head_cs_set = set(head_cs)

    def find_cur(old_cs):
        s = head_cs.get(old_cs)
        c = head_cs_blobs.get(s) if s else None
        if c is not None:                                   # a) 内容哈希
            cands = cur_hash.get(hashlib.md5(norm(c)).hexdigest())
            if cands:
                return cands[0], 'content'
        base = os.path.basename(old_cs)                     # b) 唯一同名(优先新位置)
        cands = by_base.get(base, [])
        newpos = [x for x in cands if x not in head_cs_set]
        if len(newpos) == 1:
            return newpos[0], 'basename'
        if len(cands) == 1:
            return cands[0], 'basename-same'
        if c is not None:                                   # c) 类名
            m = re.search(rb'\b(?:class|struct|interface|enum)\s+(\w+)', c)
            if m:
                pat = re.compile(rb'\b(?:class|struct|interface|enum)\s+' + re.escape(m.group(1)) + rb'\b')
                hit = [p for p, b in cur_content.items() if pat.search(b)]
                if len(hit) == 1:
                    return hit[0], 'class:' + m.group(1).decode()
        return None, 'unmatched'

    plan, nomatch = [], []
    for g, old_cs, files in sorted(dangling, key=lambda x: x[1]):
        cur, how = find_cur(old_cs)
        if cur:
            plan.append((g, old_cs, cur, disk_p2g.get(cur + '.meta'), how, files))
        else:
            nomatch.append((g, old_cs, files))

    print('悬空脚本 GUID 总数        :', len(dangling))
    print('可修复(定位到现文件)      :', len(plan))
    print('找不到现文件(疑已删/改名) :', len(nomatch))
    print()
    print('=== 修复计划 (旧guid -> 现文件.meta) ===')
    for g, old_cs, cur, ng, how, files in plan:
        print('  %-52s -> %s' % (old_cs, cur))
        print('      %s -> %s   [%s]  引用 %d 处' % (ng, g, how, len(files)))
    if nomatch:
        print()
        print('=== 无法定位(需人工/说明) ===')
        for g, old_cs, files in nomatch:
            print('  %s   %s   refs=%d' % (g, old_cs, len(files)))
            for x in files[:3]:
                print('      REF', x)

    if apply and plan:
        print()
        print('=== 落地写入 .meta ===')
        n_ok = n_skip = 0
        for g, old_cs, cur, ng, how, _f in plan:
            if ng and ng in refs:                       # 新 guid 仍被资产引用 => 不能覆盖
                print('  HOLD(新 guid 仍被 %d 处引用，需人工) %s' % (len(refs[ng]), cur))
                n_skip += 1
                continue
            meta = os.path.join(ROOT, cur + '.meta')
            b = open(meta, 'rb').read()
            if ng and ('guid: ' + ng).encode() in b:
                nb = b.replace(('guid: ' + ng).encode(), ('guid: ' + g).encode(), 1)
                open(meta, 'wb').write(nb)
                print('  OK  %s : %s -> %s' % (cur, ng, g))
                n_ok += 1
            else:
                print('  SKIP(未找到当前 guid 串) %s' % meta)
                n_skip += 1
        print('已写 %d 个 .meta，跳过 %d 个；Unity 刷新后引用恢复' % (n_ok, n_skip))


if __name__ == '__main__':
    main()
