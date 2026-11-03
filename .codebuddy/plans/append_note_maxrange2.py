# -*- coding: utf-8 -*-
p = r'e:\Bluedivers\.codebuddy\memory\2026-11-03.md'
note = """
### 更正（同日）：MaxRange 属性 base 直接取原值
- 用户口径：**不要动初值**，他自己配；-1 的武器本来就不会给射程改装
- `DamageData.GetAttr(Attr.MaxRange)` 改为直接返回 `MaxRange`（撤掉"等效米数"折算，那套推算不做了）
- `SetMaxRange` 保留"非正值落成 -1"：防 cfg 手填 0 ⇒ 零射程（光束零长/子弹出生即超距）
"""
with open(p, 'a', encoding='utf-8') as f:
    f.write(note)
print('ok')
