# -*- coding: utf-8 -*-
"""把 WeaponPlayerController.InitAttribute 里 45 条 `Set(Attr.X, damages.SetY)` 改成
`Set(Attr.X, (d, v) => d.SetY(v))` —— setter 不再捕获具体的 DamageData 实例，
模组切换"子弹配置序号"后加成才会落到当前那条配置上。

⚠ 保留 BOM 与 LF；先整读进内存再写，禁止 open(p,'w') 里套 open(p).read()。
"""
import io
import re

PATH = r'Assets/Scripts/06Gameplay/Weapon/WeaponPlayerController.cs'

text = io.open(PATH, encoding='utf-8-sig', newline='').read()
pat = re.compile(r'Set\(Attr\.(\w+), damages\.(Set\w+)\);')
new_text, count = pat.subn(lambda m: 'Set(Attr.%s, (d, v) => d.%s(v));' % (m.group(1), m.group(2)), text)

if count == 0:
    raise SystemExit('没有匹配到任何 Set 调用，未写入')

io.open(PATH, 'w', encoding='utf-8-sig', newline='').write(new_text)
print('replaced', count)
