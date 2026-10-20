# -*- coding: utf-8 -*-
"""对照两端的号映射（netid_map_<role>.log），两段口径不同：

  · 非派生段（敌人自增 / 特殊单位 / 房主生成+载具）：**按 NetId 对齐** —— 同一个号是不是同一种单位。
  · 派生段（场景摆放 / 任务实体）：**按 Id + 出生坐标量化 + 同组序号 对齐** —— 两端各算各的号是否相同。

用法：
    python netid_map_diff.py                        # 自动找 LocalLow 下的 host / member
    python netid_map_diff.py a.log b.log
"""
import os
import sys

# LocalLow 与 Local 是同级目录（%LOCALAPPDATA% 指向 ...\AppData\Local）
DEFAULT_DIR = os.path.join(
    os.path.dirname(os.environ.get('LOCALAPPDATA', r'C:\Users\admin\AppData\Local')),
    'LocalLow', 'GameDevelopmentDepartmentFaFa', 'BlueDivers')

SEG_NAME = {0: '敌人自增段', 1: '特殊单位段', 2: '房主生成段'}


def load(path):
    """返回 (头注释, {netId: (Id, 'qx,qy,qz')}, {(Id,qx,qy,qz,ord): netId})"""
    head = ''
    seq = {}
    derived = {}
    mode = None
    with open(path, encoding='utf-8', errors='replace') as f:
        for line in f:
            line = line.rstrip('\n')
            if not line:
                continue
            if line.startswith('#'):
                if 'role=' in line:
                    head = line
                elif line.startswith('# ==='):
                    mode = 'derived' if '派生段' in line else 'seq'
                continue
            p = line.split('\t')
            if len(p) < 3:
                continue
            if mode == 'derived':
                derived[(p[1], p[2], p[3].lstrip('#'))] = int(p[0])
            else:
                seq[int(p[0])] = (p[1], p[2])
    return head, seq, derived


def seg_of(net_id):
    if net_id >= 0x4000_0000:
        return 3
    if net_id >= 0x2000_0000:
        return 2
    if net_id >= 1000000:
        return 1
    return 0


def cmp_seq(sa, sb):
    ka, kb = set(sa), set(sb)
    common = sorted(ka & kb)
    only_a, only_b = sorted(ka - kb), sorted(kb - ka)

    print('\n=== 非派生段（按 NetId 对齐：第 n 号是不是同一种单位）===')
    for seg in (0, 1, 2):
        na = [k for k in ka if seg_of(k) == seg]
        nb = [k for k in kb if seg_of(k) == seg]
        if not na and not nb:
            continue
        print('  %-8s A=%d(max %d)  B=%d(max %d)' % (
            SEG_NAME[seg], len(na), max(na) if na else 0, len(nb), max(nb) if nb else 0))

    diff = [n for n in common if sa[n][0] != sb[n][0]]
    print('  共同号=%d  同号不同 Id=%d  只在A=%d  只在B=%d' % (
        len(common), len(diff), len(only_a), len(only_b)))

    if diff:
        first = diff[0]
        idx = common.index(first)
        lo, hi = max(0, idx - 5), min(len(common), idx + 6)
        print('\n  >>> 第一个分叉号 = %d  <<<' % first)
        print('  %-12s %-24s %-24s' % ('号', 'A.Id', 'B.Id'))
        for n in common[lo:hi]:
            mark = '  <-- 分叉' if sa[n][0] != sb[n][0] else ''
            print('  %-12d %-24s %-24s%s' % (n, sa[n][0], sb[n][0], mark))
        print('\n  分叉号总数=%d（若是一段连续区间，说明"某处多建/少建了一个"，其后整体平移）' % len(diff))

    if only_a:
        print('\n  只在 A 的号:', only_a[:20], '...' if len(only_a) > 20 else '')
    if only_b:
        print('  只在 B 的号:', only_b[:20], '...' if len(only_b) > 20 else '')


def cmp_derived(da, db):
    ka, kb = set(da), set(db)
    same = ka & kb
    only_a, only_b = ka - kb, kb - ka
    bad = [k for k in same if da[k] != db[k]]

    print('\n=== 派生段（按 Id+坐标+序号 对齐：两端算出的号是否相同）===')
    print('  A=%d  B=%d  共同=%d  只在A=%d  只在B=%d  同 key 不同号=%d' % (
        len(ka), len(kb), len(same), len(only_a), len(only_b), len(bad)))
    if not only_a and not only_b and not bad:
        print('  [OK] 派生段完全一致')
        return

    # 按 Id 归组看是"数量不一致"还是"坐标分叉"
    from collections import defaultdict
    ga, gb = defaultdict(set), defaultdict(set)
    for k in ka:
        ga[k[0]].add((k[1], k[2]))
    for k in kb:
        gb[k[0]].add((k[1], k[2]))
    rows = []
    for uid in sorted(set(ga) | set(gb)):
        sa_, sb_ = ga[uid], gb[uid]
        rows.append((uid, len(sa_), len(sb_), len(sa_ & sb_)))
    print('  %-26s %6s %6s %7s %7s' % ('Id', 'A条', 'B条', '两端同', '分叉'))
    for uid, na, nb, same_n in rows:
        if na != nb or same_n != max(na, nb):
            print('  %-26s %6d %6d %7d %7d' % (uid, na, nb, same_n, max(na, nb) - same_n))
    for uid in sorted({k[0] for k in only_a} | {k[0] for k in only_b})[:10]:
        print('  只在单端出现: %s  A=%d B=%d' % (uid, len(ga.get(uid, ())), len(gb.get(uid, ()))))


def main():
    if len(sys.argv) >= 3:
        pa, pb = sys.argv[1], sys.argv[2]
    else:
        pa = os.path.join(DEFAULT_DIR, 'netid_map_host.log')
        pb = os.path.join(DEFAULT_DIR, 'netid_map_member.log')
    for p in (pa, pb):
        if not os.path.exists(p):
            print('!! 找不到:', p)
            print('   两端都要跑过一次战斗（开战 8 秒后自动写；之后每 60 秒覆盖重写）')
            return

    ha, sa, da = load(pa)
    hb, sb, db = load(pb)
    print('A:', os.path.basename(pa))
    print('  ', ha)
    print('B:', os.path.basename(pb))
    print('  ', hb)

    cmp_seq(sa, sb)
    cmp_derived(da, db)


if __name__ == '__main__':
    main()
