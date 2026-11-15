# -*- coding: utf-8 -*-
"""扫全工程 prefab / scene，找出挂了 Furniture_General 的物体，并解析它真正生效的 Id
（现在的 Id = Identity.Id，即同物体/子树上的 BaseObject 或 Actor；prefab 里 Furniture_General 块内的
 `Id:` 是旧字段残留，代码已不读）。
只读。
"""
import io
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets')


def guid_of(cs_rel):
    p = os.path.join(ROOT, cs_rel + '.meta')
    m = re.search(r'guid:\s*([0-9a-f]{32})', io.open(p, encoding='utf-8', errors='ignore').read())
    return m.group(1)


G_FURN = guid_of(os.path.join('Assets', 'Scripts', '06Gameplay', 'Interactable', 'Furniture_General.cs'))
G_BASE = guid_of(os.path.join('Assets', 'Scripts', '00Core', 'BaseObject.cs'))
G_ACTOR = guid_of(os.path.join('Assets', 'Scripts', '05UnitCore', 'Actor', 'Actor.cs'))


def blocks(txt):
    parts = re.split(r'^--- !u!(\d+) &(\d+)', txt, flags=re.M)
    out = []
    for i in range(1, len(parts), 3):
        out.append((parts[i], parts[i + 1], parts[i + 2]))
    return out


def field(body, key):
    m = re.search(r'^\s{2}%s:[ \t]*(.*)$' % re.escape(key), body, flags=re.M)
    return None if m is None else m.group(1).strip()


def scan(path):
    txt = io.open(path, encoding='utf-8', errors='ignore').read()
    if G_FURN not in txt:
        return []
    bl = blocks(txt)
    # GameObject fileID -> name
    goname = {}
    # Transform fileID -> (goFileID, fatherFileID)
    trans = {}
    for cls, fid, body in bl:
        if cls == '1':
            goname[fid] = field(body, 'm_Name')
        elif cls == '4' or cls == '224':
            go = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', body)
            fa = re.search(r'm_Father:\s*\{fileID:\s*(\d+)\}', body)
            trans[fid] = (go.group(1) if go else None, fa.group(1) if fa else None)

    def lineage(go):
        """该 GameObject 及其所有祖先的 go fileID 集合（用于找挂在父物体上的身份）"""
        res = {go}
        for tf, (g, fa) in trans.items():
            if g != go:
                continue
            cur = fa
            while cur and cur != '0':
                g2, fa2 = trans.get(cur, (None, None))
                if g2:
                    res.add(g2)
                cur = fa2
        return res

    rows = []
    for cls, fid, body in bl:
        if cls != '114':
            continue
        s = field(body, 'm_Script') or ''
        if G_FURN not in s:
            continue
        go = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', body)
        go = go.group(1) if go else '?'
        scope = lineage(go)
        ids = []
        for c2, f2, b2 in bl:
            if c2 != '114':
                continue
            s2 = field(b2, 'm_Script') or ''
            if G_BASE not in s2 and G_ACTOR not in s2:
                continue
            g2 = re.search(r'm_GameObject:\s*\{fileID:\s*(\d+)\}', b2)
            g2 = g2.group(1) if g2 else None
            tag = 'BaseObject' if G_BASE in s2 else 'Actor'
            samego = (g2 == go)
            near = g2 in scope
            ids.append('%s(go=%s%s%s) Id=%r ShowName=%r' % (
                tag, g2, ',同物体' if samego else '', ',祖先' if (near and not samego) else '',
                field(b2, 'Id'), field(b2, 'ShowName')))
        rows.append('   go=%s(%s) 旧Id字段=%r 启用=%s\n      %s' % (
            go, goname.get(go, '?'), field(body, 'Id'), field(body, 'm_Enabled'),
            '\n      '.join(ids) if ids else '**同一物体/祖先上没有 BaseObject/Actor（Identity 只能靠子物体兜底或解析失败）**'))
    return rows


def main():
    for dp, _d, fs in os.walk(ASSETS):
        for f in sorted(fs):
            if not (f.endswith('.prefab') or f.endswith('.unity')):
                continue
            p = os.path.join(dp, f)
            try:
                rows = scan(p)
            except Exception as e:
                print('读失败', p, e)
                continue
            if rows:
                print('\n=== %s' % os.path.relpath(p, ROOT))
                for r in rows:
                    print(r)


if __name__ == '__main__':
    main()
