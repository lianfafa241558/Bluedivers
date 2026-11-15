# -*- coding: utf-8 -*-
"""家具 prefab 体检 v2：按 YAML MonoBehaviour 块解析，精确读各家具组件自己的字段
（Id / owner / anim / relatedTrans / ContGo / identitySource / flags），ASCII 输出避免控制台乱码。
只读。
"""
import io
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets')

TARGETS = [
    'Furniture_Door', 'Furniture_Equip', 'Furniture_WeaponPickup', 'Furniture_General',
    'Furniture_Pipe', 'Furniture_Artillery', 'Furniture_PlayerArtillery',
    'Furniture_KeiSubmit', 'Furniture_HandEquip', 'Furniture_GrenadePickup',
    'Furniture_PlayerDown', 'Furniture_ReturnBag', 'Furniture_Vehicle',
    'Furniture_NPCChat', 'OOPart', 'GuideController',
]


def script_guids():
    out = {}
    for dirpath, _d, files in os.walk(ASSETS):
        for f in files:
            if f.endswith('.cs') and f[:-3] in TARGETS:
                meta = os.path.join(dirpath, f + '.meta')
                if os.path.exists(meta):
                    m = re.search(r'guid:\s*([0-9a-f]{32})', io.open(meta, encoding='utf-8', errors='ignore').read())
                    if m:
                        out[f[:-3]] = m.group(1)
    return out


def blocks(txt):
    """按 '--- !u!' 切块，返回 [(classTag, body)]"""
    parts = re.split(r'^--- !u!(\d+) &', txt, flags=re.M)
    res = []
    for i in range(1, len(parts), 2):
        res.append((parts[i], parts[i + 1]))
    return res


def field(body, key):
    m = re.search(r'^\s{2}%s:[ \t]*(.*)$' % re.escape(key), body, flags=re.M)
    return None if m is None else m.group(1).strip()


def main():
    guids = script_guids()
    rev = {v: k for k, v in guids.items()}
    print('scripts with meta:', len(guids))

    for dirpath, _d, files in os.walk(ASSETS):
        for f in sorted(files):
            if not f.endswith('.prefab'):
                continue
            p = os.path.join(dirpath, f)
            txt = io.open(p, encoding='utf-8', errors='ignore').read()
            used = [n for g, n in rev.items() if g in txt]
            if not used:
                continue
            rel = os.path.relpath(p, ROOT)
            print('\n%s  ->  %s' % (rel, ','.join(used)))
            for cls, body in blocks(txt):
                if cls != '114':
                    continue
                g = field(body, 'm_Script') or ''
                if not any(gid in g for gid in rev):
                    continue
                name = [n for gid, n in rev.items() if gid in g][0]
                keep = ['Id', 'ShowName', 'owner', 'anim', 'relatedTrans', 'relatedTrans2',
                        'ContGo', 'identitySource', 'flags', 'canOperate', 'inOperate', 'capacity']
                info = []
                for k in keep:
                    v = field(body, k)
                    if v is None:
                        info.append('%s=<absent>' % k)
                    elif v in ('{fileID: 0}', '[]', ''):
                        info.append('%s=<EMPTY>' % k)
                    else:
                        info.append('%s=%s' % (k, v if len(v) < 40 else v[:40] + '...'))
                print('   [%s] %s' % (name, ' ; '.join(info)))


if __name__ == '__main__':
    main()
