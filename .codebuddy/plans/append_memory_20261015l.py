# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「SerializedProperty 写枚举」踩坑与最终复核（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 补记：MCP 改 prefab 时写枚举字段的坑（2026-10-15）
- `SerializedProperty.intValue` 对枚举是**写底层值**（不是索引）：`UnitTypeEnum` 声明值不连续（Names 顺序 None,Player,Friend,Enemy,SpecUnit,Other,All，但 `Enemy` 的底层值是 4）⇒ 写 4 得到 Enemy ✓
- 写不支持的值 Unity 只 `Debug.LogError("xxx is not a supported int value")` 并**忽略这次写入**（不抛异常、脚本照常往下跑）⇒ 事后一定要**复核**
- 复核别用 SerializedProperty：它 `ToString()` 只会回类型名。用**反射**读真实字段最稳：
  `type.GetField(name, BindingFlags.Instance|Public|NonPublic).GetValue(comp).ToString()`
- 本次最终值（反射复核）：type=Enemy ✓ team=1 ✓ flag=Boss ✓ shape=Circle ✓ actorState=Normal ✓ UseHpBar=True ✓ HpHeight=1 ✓ Id=TrainEngineSam ✓
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
