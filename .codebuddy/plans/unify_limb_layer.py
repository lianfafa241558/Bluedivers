# -*- coding: utf-8 -*-
"""把「单位肢体」所在的 GameObject 统一到 Unit(6) 层。

口径（为什么这么圈）：
  - 肢体 = 挂了 Damageable / TransferDamageable 的 GameObject，**外加它子树里带 Collider 的 GameObject**
    （护甲片是子物体、Damageable.Awake 会把它们的 Collider 一起收进 m_colliders）。
  - 只动 `m_Layer` 这一行，其余字节原样保留（不经过 Unity 的 LoadPrefabContents/SaveAsPrefabAsset，
    那一套会按内存旧值回写、抹掉没碰的字段 —— 见 memory/MEMORY.md 的 MCP 存盘警示）。

用法：
    python unify_limb_layer.py            # 只扫描报告
    python unify_limb_layer.py --apply    # 落盘
"""
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

# 脚本 GUID（改了脚本文件要同步这里）
GUID_DAMAGEABLE = 'f9b0ce1bb69c96b49a7ac1098a7f5796'
GUID_TRANSFER = 'c661b922f5a2365469cd86394bae0e06'
GUID_ACTOR = '81b4c4e7c5d6cd34bb04cb46517b4e23'
LIMB_GUIDS = (GUID_DAMAGEABLE, GUID_TRANSFER)

CLS_GAMEOBJECT = '1'
CLS_TRANSFORM = '4'
CLS_RECTTRANSFORM = '224'
# 算「碰撞体载体」的 classID（Box/Sphere/Capsule/Mesh/Terrain/Wheel/CharacterController）
COLLIDER_CLASSES = {'64', '65', '135', '136', '143', '146', '154', '230'}

UNIT_LAYER = 6

DOC_RE = re.compile(r'^--- !u!(\d+) &(\d+)(.*)$')
GO_OBJ_RE = re.compile(r'^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}')
LAYER_RE = re.compile(r'^(\s*)m_Layer:\s*(-?\d+)\s*$')
COMP_RE = re.compile(r'^\s*- component:\s*\{fileID:\s*(-?\d+)\}')
CHILD_RE = re.compile(r'^\s*- \{fileID:\s*(-?\d+)\}')


def split_docs(text):
    """按 `--- !u!<cls> &<id>` 切文档，返回 (preamble, [(cls, id, lines)])。

    ⚠ preamble = 第一个 `--- !u!` 之前的行（`%YAML 1.1` / `%TAG ...`），**必须原样写回**：
    丢了这两行 Unity 仍能读，但文件就不再是标准 Unity YAML（2026-11-16 踩过一次）。
    """
    docs, cur, preamble = [], None, []
    for line in text.split('\n'):
        m = DOC_RE.match(line)
        if m:
            if cur:
                docs.append(cur)
            cur = [m.group(1), m.group(2), [line]]
        elif cur is not None:
            cur[2].append(line)
        else:
            preamble.append(line)
    if cur:
        docs.append(cur)
    return preamble, docs


def scan_file(path):
    with open(path, 'rb') as f:
        raw = f.read()
    had_bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    preamble, docs = split_docs(text)

    by_id = {d[1]: d for d in docs}

    # transform: gameObject / father / children
    go_of_tr = {}
    children_of_tr = {}
    tr_of_go = {}
    for cls, did, lines in docs:
        if cls not in (CLS_TRANSFORM, CLS_RECTTRANSFORM):
            continue
        go_id = None
        kids, in_kids = [], False
        for ln in lines:
            m = GO_OBJ_RE.match(ln)
            if m:
                go_id = m.group(1)
            if ln.strip().startswith('m_Children:'):
                in_kids = True
                continue
            if in_kids:
                mc = CHILD_RE.match(ln)
                if mc:
                    kids.append(mc.group(1))
                elif ln.strip() and not ln.startswith(' ' * 4):
                    in_kids = False
        if go_id:
            go_of_tr[did] = go_id
            children_of_tr[did] = kids
            tr_of_go[go_id] = did

    # gameObject: components / layer
    comps_of_go = {}
    layer_of_go = {}
    layer_line_idx = {}
    for cls, did, lines in docs:
        if cls != CLS_GAMEOBJECT:
            continue
        comps = []
        for i, ln in enumerate(lines):
            mc = COMP_RE.match(ln)
            if mc:
                comps.append(mc.group(1))
            ml = LAYER_RE.match(ln)
            if ml:
                layer_of_go[did] = int(ml.group(2))
                layer_line_idx[did] = i
        comps_of_go[did] = comps

    # 肢体 GO：挂了 Damageable / TransferDamageable 的 MonoBehaviour
    limb_gos = set()
    actor_gos = set()
    for cls, did, lines in docs:
        if cls != '114':
            continue
        head = '\n'.join(lines[:12])
        target = None
        if any(g in head for g in LIMB_GUIDS):
            target = limb_gos
        elif GUID_ACTOR in head:
            target = actor_gos
        if target is None:
            continue
        for ln in lines:
            m = GO_OBJ_RE.match(ln)
            if m:
                target.add(m.group(1))
                break

    if not limb_gos:
        return None

    # 子树里带 Collider 的 GO
    carriers = set()
    for go in list(layer_of_go.keys()):
        for cid in comps_of_go.get(go, ()):
            if cid in by_id and by_id[cid][0] in COLLIDER_CLASSES:
                carriers.add(go)
                break

    def descendants(tr_id):
        out, stack = [], list(children_of_tr.get(tr_id, ()))
        while stack:
            t = stack.pop()
            out.append(t)
            stack.extend(children_of_tr.get(t, ()))
        return out

    # ⚠ 只处理「Actor 子树里」的肢体：Assets/Art/Prefabs/Object 里那些带 Damageable 的
    #   环境道具（墙/卷帘/线缆）不是单位肢体，它们留在 Default 层当"地面"才对。
    #   `--no-actor-filter` 关掉这一层限制：有些复合预制体（Mission_DetectorTower / JammerTower）
    #   的可破坏部件是**嵌套**进来的道具预制体，它们自己不带 Actor ⇒ 必须单独处理。
    if actor_gos and not NO_ACTOR_FILTER:
        in_actor = set()
        for ag in actor_gos:
            tr = tr_of_go.get(ag)
            if not tr:
                continue
            for t in descendants(tr):
                g = go_of_tr.get(t)
                if g:
                    in_actor.add(g)
        in_actor |= actor_gos
        limb_gos &= in_actor
        if not limb_gos:
            return None

    targets = set(limb_gos)
    for limb in limb_gos:
        tr = tr_of_go.get(limb)
        if not tr:
            continue
        for t in descendants(tr):
            g = go_of_tr.get(t)
            if g in carriers:
                targets.add(g)

    changed = []
    for go in targets:
        if layer_of_go.get(go, UNIT_LAYER) != UNIT_LAYER:
            changed.append((go, layer_of_go.get(go), layer_line_idx.get(go)))

    if not changed:
        return {'path': path, 'limbs': len(limb_gos), 'targets': len(targets), 'changed': []}

    if APPLY:
        for go, old, idx in changed:
            d = by_id[go]
            line = d[2][idx]
            m = LAYER_RE.match(line)
            d[2][idx] = '%sm_Layer: %d' % (m.group(1), UNIT_LAYER)

        body = '\n'.join('\n'.join(d[2]) for d in docs)
        pre = '\n'.join(preamble).rstrip('\n')
        out = (pre + '\n' + body) if pre else body
        data = out.encode('utf-8')
        if had_bom:
            data = b'\xef\xbb\xbf' + data
        with open(path, 'wb') as f:
            f.write(data)

    return {'path': path, 'limbs': len(limb_gos), 'targets': len(targets), 'changed': changed}


def main():
    total_files = total_limbs = total_targets = total_changed = 0
    rows = []
    for base in ('Assets',):
        for dp, _dn, fn in os.walk(os.path.join(ROOT, base)):
            for f in fn:
                if not f.endswith(('.prefab', '.unity')):
                    continue
                p = os.path.join(dp, f)
                r = scan_file(p)
                if not r:
                    continue
                total_files += 1
                total_limbs += r['limbs']
                total_targets += r['targets']
                total_changed += len(r['changed'])
                if r['changed']:
                    rows.append((os.path.relpath(p, ROOT), r['limbs'], r['targets'], len(r['changed'])))

    rows.sort(key=lambda x: -x[3])
    print('扫描文件 %d 个，肢体 GO %d 个，待处理 GO %d 个，层需要改 %d 个  (%s)'
          % (total_files, total_limbs, total_targets, total_changed, 'APPLY' if APPLY else 'DRY-RUN'))
    for r in rows[:40]:
        print('  %-70s 肢体%3d  目标%3d  改%3d' % r)
    if len(rows) > 40:
        print('  ... 还有 %d 个文件' % (len(rows) - 40))


APPLY = '--apply' in sys.argv
NO_ACTOR_FILTER = '--no-actor-filter' in sys.argv

if __name__ == '__main__':
    main()
