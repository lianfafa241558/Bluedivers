# -*- coding: utf-8 -*-
"""追加"硬度改成独立 list"的更正条目（追加模式）。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
path = os.path.join(ROOT, '.codebuddy', 'memory', '2026-11-09.md')

NOTE = """## 更正（同日）：地形硬度改为独立 list，不挂在整平条目上
- 撤回 `ModifyTerrainData.innerHardness/outerHardness`（用户口径：硬度是"地表属性"，不该跟"这次要不要压平"绑在一起，半径也常常不同）。
- `ModifyTerrain` 新增 `List<ModifyTerrainHardnessData> hardnessDatas`（localPos / shape / 内外半径或矩形内外半尺寸 / 内外硬度），**只刷硬度不改高度**；在 `Modify` 协程里所有改高度的操作之后统一 `TerrainUtils.StampHardness`（先刷后挖会被自己的硬度削弱），gizmo 里画绿色外框便于对齐。
- 内外框回落规则抽成 `ResolveOuterHalfSize` / `ResolveInnerHalfSize`，整平条目与硬度条目共用一套判据（避免两份渐渐跑偏）。
- 验证：`python .codebuddy/plans/offline_compile.py 06_Gameplay` → 0 error。
"""

with io.open(path, 'a', encoding='utf-8', newline='\n') as f:
    f.write('\n' + NOTE)

print('appended', path)
