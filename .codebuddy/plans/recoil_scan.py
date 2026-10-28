# -*- coding: utf-8 -*-
"""扫玩家武器 prefab，读出后坐力分档需要的数据（射速/弹丸数/弹匣/爆炸），只读。

用法：python recoil_scan.py [根目录，默认 Assets/Resources/Weapons]
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
WPC_GUID = 'e03ea2fb50b06ab468194bc5b83375d6'   # WeaponPlayerController.cs.meta
SCRIPT_RE = re.compile(r'm_Script: \{fileID: 11500000, guid: %s' % WPC_GUID)
# 组件块：从 "--- !u!" 到下一个 "--- !u!"
DOC_RE = re.compile(r'(?ms)^--- !u!\d+ &\d+\n.*?(?=^--- !u!|\Z)')
KV_RE = re.compile(r'(?m)^\s*- Key: (\d+)\n\s*Value: ([-\d.]+)\s*$')
FIELD_RE = r'(?m)^  %s: ([-\d.]+)\s*$'


def one_field(block, name, default=None):
    m = re.search(FIELD_RE % name, block)
    return float(m.group(1)) if m else default


def scan(path):
    src = open(path, encoding='utf-8', errors='replace').read()
    for doc in DOC_RE.findall(src):
        if not SCRIPT_RE.search(doc):
            continue
        cfg = {int(k): float(v) for k, v in KV_RE.findall(doc)}
        return {
            'interval': cfg.get(2),        # ShootInterval
            'mag': cfg.get(1),             # Magazine
            'bullets': cfg.get(6),         # BulletsPerShot
            'explosion': one_field(doc, 'DamageExplosion'),
            'recoil_force': one_field(doc, 'RecoilForce'),
            'has_profile': 'RecoilProfile:' in doc,
            'name': one_field(doc, 'WeaponName'),
        }
    return None


def main():
    base = os.path.join(ROOT, sys.argv[1] if len(sys.argv) > 1 else 'Assets/Resources/Weapons')
    rows = []
    for dp, _dn, fn in os.walk(base):
        for f in fn:
            if not f.endswith('.prefab'):
                continue
            p = os.path.join(dp, f)
            info = scan(p)
            if info:
                info['path'] = os.path.relpath(p, ROOT).replace('\\', '/')
                rows.append(info)

    rows.sort(key=lambda r: (r['interval'] if r['interval'] is not None else 99))
    print('%-42s %8s %6s %7s %10s %6s %s' % ('prefab', 'interview', 'mag', 'bullets', 'explosion', 'RF', 'profile'))
    for r in rows:
        print('%-42s %8s %6s %7s %10s %6s %s' % (
            r['path'].replace('Assets/Resources/Weapons/', ''),
            r['interval'], r['mag'], r['bullets'], r['explosion'], r['recoil_force'], r['has_profile']))
    print('总计 %d 把' % len(rows))


if __name__ == '__main__':
    main()
