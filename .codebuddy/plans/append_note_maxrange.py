# -*- coding: utf-8 -*-
p = r'e:\Bluedivers\.codebuddy\memory\2026-11-03.md'
note = """
## 武器属性新增「最大射程 MaxRange」(2026-11-03)
- `WeaponAttrType.MaxRange = 24`（复用原注释掉的 MaxDistance 占位号）+ `attributeConfigs` 一行 `(false, AttrTag.OnlyInt, ModifierType.All)`
- 全链已通：`DamageData.GetAttr(Attr.MaxRange)` → **等效米数**（`MaxRange > 0 ? MaxRange : MaxLifeTime*Speed`）；`SetMaxRange` 非正值回落 -1；`WeaponPlayerController.InitAttribute` 的 `Set(Attr.MaxRange, ...)`；`WeaponUpgradeController.ApplyUpgrade` 里按 DirectAP 同款补预览初值
- ⚠ 核心坑：`MaxRange` 原值 -1 = 无限、0 = **零射程**（光束零长 / 子弹出生即超距）⇒ 不能直接把字段值当 base，否则 "+10m" 会变成 9m
- 刻意**没**进 `WeaponCfg.TypePara`（Reset 会补 0 值 ⇒ 零射程）；要在参数面板显示需武器 prefab 的 `showAttr` 手动勾
- 离线编译 06_Gameplay：0 error
"""
with open(p, 'a', encoding='utf-8') as f:
    f.write(note)
print('ok')
