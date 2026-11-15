# -*- coding: utf-8 -*-
"""家具（Furniture_*）prefab 引用体检：列出每个家具脚本被哪些 prefab 使用，
并对几个"代码里未判空"的字段做存在性核对（ContGo / anim / relatedTrans / 身份组件）。
只读，不改任何文件。
"""
import io
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ASSETS = os.path.join(ROOT, 'Assets')

TARGETS = [
    'Furniture_Door', 'Furniture_Equip', 'Furniture_WeaponPickup', 'Furniture_General',
    'Furniture_AttachedGeneral', 'Furniture_Pipe', 'Furniture_Artillery',
    'Furniture_PlayerArtillery', 'Furniture_KeiSubmit', 'Furniture_HandEquip',
    'Furniture_GrenadePickup', 'Furniture_PlayerDown', 'Furniture_ReturnBag',
    'Furniture_Supplies', 'Furniture_Vehicle', 'Furniture_NPCChat', 'OOPart',
    'GuideController', 'Furniture_OOPartDepositBase',
]


def script_guids():
    """脚本名 -> guid（读 .meta）"""
    out = {}
    for dirpath, _dirs, files in os.walk(ASSETS):
        for f in files:
            if not f.endswith('.cs'):
                continue
            name = f[:-3]
            if name not in TARGETS:
                continue
            meta = os.path.join(dirpath, f + '.meta')
            if not os.path.exists(meta):
                continue
            with io.open(meta, encoding='utf-8', errors='ignore') as fh:
                m = re.search(r'guid:\s*([0-9a-f]{32})', fh.read())
            if m:
                out[name] = m.group(1)
    return out


def prefab_files():
    for dirpath, _dirs, files in os.walk(ASSETS):
        for f in files:
            if f.endswith('.prefab'):
                yield os.path.join(dirpath, f)


def main():
    guids = script_guids()
    missing = [t for t in TARGETS if t not in guids]
    print('脚本 guid 解析：%d 个，缺失 %s' % (len(guids), missing))

    rev = {v: k for k, v in guids.items()}
    hits = {k: [] for k in guids}

    for p in prefab_files():
        try:
            txt = io.open(p, encoding='utf-8', errors='ignore').read()
        except Exception as e:
            print('读失败', p, e)
            continue
        for g, name in rev.items():
            if g in txt:
                hits[name].append((os.path.relpath(p, ROOT), txt))

    for name in TARGETS:
        files = hits.get(name) or []
        print('\n=== %s : %d 个 prefab ===' % (name, len(files)))
        for rel, txt in files:
            notes = []
            if name in ('Furniture_Equip',):
                notes.append('ContGo=' + ('有' if 'ContGo:' in txt else '**缺**'))
            if 'anim: {fileID: 0}' in txt:
                notes.append('anim空(靠Awake取)')
            if name in ('Furniture_General', 'Furniture_Pipe', 'Furniture_Artillery', 'Furniture_PlayerArtillery'):
                m = re.search(r'\n\s*Id:\s*(.*)', txt)
                if m:
                    notes.append('Id=' + m.group(1).strip())
            print('   %-70s %s' % (rel, ' | '.join(notes)))


if __name__ == '__main__':
    main()
