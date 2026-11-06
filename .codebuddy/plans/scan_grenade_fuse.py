import glob
import io
import re

for path in glob.glob('Assets/Resources/Weapons/Grenade/*.prefab'):
    text = io.open(path, encoding='utf-8').read()
    life = re.findall(r'MaxLifeTime: ([-\d\.]+)', text)
    name = re.search(r'WeaponName: "(.*)"', text)
    interval = re.findall(r'- Key: 2\n      Value: ([-\d\.]+)', text)
    mag = re.findall(r'- Key: 1\n      Value: ([-\d\.]+)', text)
    ammo = re.findall(r'- Key: 0\n      Value: ([-\d\.]+)', text)
    print(path.split('/')[-1], name.group(1) if name else '?', 'life=', life,
          'mag=', mag, 'ammo=', ammo, 'interval=', interval)
