# -*- coding: utf-8 -*-
"""阶段0：生成 06Gameplay 命名空间对齐计划（不改任何代码）。
产出 plans/p2_ns_plan.md：目录->目标ns 映射、需改ns的文件、受影响的外部 using、执行序。
"""
import os, re, io, collections

ROOT = r'd:\Pro\Bluedivers'
ASSETS = os.path.join(ROOT, 'Assets')
G = os.path.join(ASSETS, 'Scripts', '06Gameplay')
NSDECL = re.compile(r'^\s*namespace\s+([\w\.]+)', re.M)
USING = re.compile(r'^\s*using\s+(?:static\s+)?(FPSGame\.[\w\.]+)\s*;', re.M)

# 目录 -> 目标 ns 规则（按优先级从上往下匹配相对路径前綴）
RULES = [
    ('AI/StateMachine/Editor/', 'FPSGame.AI.Editor'),
    ('AI/', 'FPSGame.AI'),
    ('Mission/', 'FPSGame.Mission'),
    ('Weapon/', 'FPSGame.Weapon'),
    ('Data/', 'FPSGame.GameData'),
    ('Game/', 'FPSGame.Game'),
    ('Interactable/', 'FPSGame.Gameplay'),
    ('Common/', 'FPSGame.Gameplay'),
    ('Airdrop/', 'FPSGame.Gameplay'),
    ('Bag/', 'FPSGame.Gameplay'),
    ('Effect/', 'FPSGame.Gameplay'),
    ('Events/', 'FPSGame.Gameplay'),
    ('Npc/', 'FPSGame.Gameplay'),
    ('Player/', 'FPSGame.Gameplay'),
    ('Projectile/', 'FPSGame.Gameplay'),
]


def target_of(rel_under_g):
    for pre, ns in RULES:
        if rel_under_g.startswith(pre):
            return ns
    return 'FPSGame.Gameplay'


def read(p):
    return io.open(p, encoding='utf-8-sig', errors='replace').read()


# 1) 06Gameplay 现状
rows = []
for r, d, fs in os.walk(G):
    for f in fs:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(r, f)
        relat = os.path.relpath(p, G).replace('\\', '/')
        t = read(p)
        m = NSDECL.search(t)
        cur = m.group(1) if m else '(none)'
        tgt = target_of(relat)
        rows.append(('Assets/Scripts/06Gameplay/' + relat, cur, tgt))

cur_dist = collections.Counter(x[1] for x in rows)
tgt_dist = collections.Counter(x[2] for x in rows)
need = [x for x in rows if x[1] != x[2]]

# 2) 外部 using 索引
users = collections.defaultdict(set)
for r, d, fs in os.walk(os.path.join(ASSETS, 'Scripts')):
    if '06Gameplay' in r.replace('\\', '/'):
        continue
    for f in fs:
        if not f.endswith('.cs'):
            continue
        p = os.path.join(r, f)
        rel = os.path.relpath(p, ROOT).replace('\\', '/')
        for u in set(USING.findall(read(p))):
            users[u].add(rel)

# 3) 特例：跨层 2 文件
special = [('Assets/Scripts/06Gameplay/Common/TargetData.cs', 'FPSGame.GameContract', 'FPSGame.Gameplay'),
           ('Assets/Scripts/06Gameplay/Common/IVehicleUIController.cs', 'FPSGame.GameContract', 'FPSGame.Gameplay')]

L = []
L.append('# 06Gameplay 命名空间对齐 · 计划（阶段 0 产出）\n')
L.append('> 决策：**保留独立 ns**（`Weapon`/`Data` 各自独立）；跨层 `TargetData`/`IVehicleUIController` **改 ns 认领为 06**（不搬文件）。\n')
L.append('> 基线：`sr_ns_audit.py` 资产 `[SerializeReference]` 条目 22、失配 **0**（改 ns 后须复跑保持 0）。\n')

L.append('\n## 1. 目录 → 目标命名空间\n')
L.append('| 目录 | 现状 ns | 目标 ns |')
L.append('|---|---|---|')
seen = []
for pre, ns in RULES:
    if pre in [s[0] for s in seen]:
        continue
    seen.append((pre, ns))
# 按现状分布汇总
cur_by_pre = collections.defaultdict(collections.Counter)
for path, cur, tgt in rows:
    rel = path.replace('Assets/Scripts/06Gameplay/', '')
    cur_by_pre[target_of(rel) if False else tgt][cur] += 1
for tgt in sorted(tgt_dist):
    curset = collections.Counter()
    for path, cur, t in rows:
        if t == tgt:
            curset[cur] += 1
    dirs = sorted(set(path.replace('Assets/Scripts/06Gameplay/', '').rsplit('/', 1)[0] or '(根)' for path, cur, t in rows if t == tgt))
    L.append('| %s | %s | **%s** |' % (', '.join(dirs[:6]) + ('…' if len(dirs) > 6 else ''),
                                        ', '.join('%s×%d' % (k, v) for k, v in curset.most_common()), tgt))

L.append('\n## 2. 需改 ns 的文件（共 %d）\n' % len(need))
for path, cur, tgt in sorted(need, key=lambda x: (x[2], x[0])):
    L.append('- `%s`  %s → **%s**' % (path.replace('Assets/Scripts/06Gameplay/', ''), cur, tgt))

def type_refs(name):
    out = []
    rx = re.compile(r'\b' + re.escape(name) + r'\b')
    for r, d, fs in os.walk(os.path.join(ASSETS, 'Scripts')):
        for f in fs:
            if not f.endswith('.cs'):
                continue
            rel = os.path.relpath(os.path.join(r, f), ROOT).replace('\\', '/')
            if rx.search(read(os.path.join(r, f))):
                out.append(rel)
    return out


L.append('\n### 特例（跨层 2 个，阶段 2）\n')
for p, cur, tgt in special:
    tname = p.rsplit('/', 1)[-1][:-3]
    rf = type_refs(tname)
    L.append('- `%s`  %s → **%s**（`%s` 被 %d 个文件引用）' %
             (p.replace('Assets/Scripts/06Gameplay/', ''), cur, tgt, tname, len(rf)))
    for x in rf:
        L.append('    - %s' % x)

L.append('\n## 3. 受影响的外部 using（其它程序集）\n')
for ns in ['FPSGame.Game', 'FPSGame.Gameplay', 'FPSGame.Furn', 'FPSGame.GameContract', 'FPSGame.AI', 'FPSGame.Mission']:
    u = sorted(users.get(ns, []))
    L.append('- `using %s;` 出现在 **%d** 个文件' % (ns, len(u)))
    for x in u[:6]:
        L.append('    - %s' % x)
    if len(u) > 6:
        L.append('    - … 共 %d' % len(u))

L.append('\n## 4. 执行顺序（阶段 3，逐目录推进）\n')
L.append('1. `Interactable/`：`FPSGame.Furn`(8) → 统一 `FPSGame.Gameplay`（外部 `using FPSGame.Furn;` %d 文件）' % len(users.get('FPSGame.Furn', [])))
L.append('2. `Data/`：`FPSGame.Game`(6) → `FPSGame.GameData`')
L.append('3. 零散串味对齐（`AI/Skill/UnitAttributeFactory`、`Game/MissionView`、`Npc/SpecUnitController`、`Player/PlayerMountPoint`、`Common/{IDamageData,SpeechTypeEnum}`，详见 §2 明细）')
L.append('4. `Weapon/`：21 文件 → `FPSGame.Weapon`（外部 `using FPSGame.Game;` %d 文件 + 06 内部若干，**最大刀，单独一轮**）' % len(users.get('FPSGame.Game', [])))
L.append('\n## 5. 每步校验\n')
L.append('- `python -X utf8 .codebuddy/plans/sr_ns_audit.py` → 失配保持 **0**（否则 `sr_ns_repair.py --apply`）')
L.append('- `python -X utf8 .codebuddy/plans/find_dangling_guids.py` → 悬空 GUID **0**')
L.append('- 编译三证（触发前须经同意）：dll mtime 前进 + Console 0 error + `is_compiling:false`')

out = os.path.join(ROOT, '.codebuddy', 'plans', 'p2_ns_plan.md')
open(out, 'w', encoding='utf-8', newline='\n').write('\n'.join(L) + '\n')
print('written', out)
print('现状 ns:', dict(cur_dist))
print('目标 ns:', dict(tgt_dist))
print('需改 ns 文件:', len(need))
