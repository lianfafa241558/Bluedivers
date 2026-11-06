import glob
import io
import re

ELEMENT = {4: '毒', 5: '燃烧', 6: '冰冻', 7: '电击', 8: '眩晕', 9: '恐惧', 10: '辐射', 11: '骇入', 12: '催化'}

for path in sorted(glob.glob('Assets/Resources/Weapons/**/*.prefab', recursive=True)):
    text = io.open(path, encoding='utf-8', errors='ignore').read()
    name = re.search(r'WeaponName: "(.*)"', text)
    label = name.group(1) if name else '?'
    try:
        label = label.encode('utf-8').decode('unicode_escape')
    except Exception:
        pass
    # 取第 0 档伤害配置里的爆炸伤害组
    block = re.search(r'Damages:\n  - (.*?)\n  UseDamageIndex', text, re.S)
    if not block:
        continue
    body = block.group(1)
    explosion = re.search(r'DamageExplosion: ([-\d\.]+)', body)
    group = re.search(r'DamageGroupExplosion:\n((?:    - Key: \d+\n      Value: [-\d\.]+\n?)+)', body)
    if not group:
        continue
    pairs = re.findall(r'Key: (\d+)\n      Value: ([-\d\.]+)', group.group(1))
    elements = [(ELEMENT.get(int(k), k), float(v)) for k, v in pairs if int(k) in ELEMENT]
    if not elements:
        continue
    print(path.replace('\\', '/'), label, 'explosion=', explosion.group(1) if explosion else '?',
          'elements=', elements, 'sum=', round(sum(v for _, v in elements), 2))
