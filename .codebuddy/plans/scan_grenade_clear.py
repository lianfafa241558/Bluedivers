import glob
import io
import math
import re

for path in sorted(glob.glob('Assets/Resources/Weapons/Grenade/*.prefab')):
    text = io.open(path, encoding='utf-8', errors='ignore').read()
    name = re.search(r'WeaponName: "(.*)"', text)
    radius = re.findall(r'ExplosionRange: ([-\d\.]+)', text)
    explosion = re.findall(r'DamageExplosion: ([-\d\.]+)', text)
    direct = re.findall(r'DamageDirect: ([-\d\.]+)', text)
    label = name.group(1) if name else '?'
    try:
        label = label.encode().decode('unicode_escape')
    except Exception:
        pass
    out = []
    for r in radius:
        r = float(r)
        factor = min(1 + math.pi * r * r / 4, 6) if r > 0 else 1
        out.append((r, round(factor, 2)))
    print(path.split('\\')[-1], label, 'dmg=', explosion, direct, 'radius/multi=', out)
