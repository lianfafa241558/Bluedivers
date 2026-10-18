# -*- coding: utf-8 -*-
"""追加：小地图铁轨线改成 3 实 1 虚 + 白色 50%。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, '.codebuddy', 'memory', '2026-10-19.md')

NOTE = """
## 小地图铁轨线：虚线（3 实 1 虚）+ 白色 50%

- 用户口径："太密了，改虚线；按 3 段实 1 段虚；颜色白、透明度 50%（或灰色）"。
- `MiniMapRailGraphic` 新增 `dashSolid = 3` / `dashGap = 1`（均为 `[SerializeField]`）：在 `OnPopulateMesh` 里
  `if (solid < cycle && (i - 1) % cycle >= solid) continue;`（按折线段序号循环，每条路线都从"实"开始）。
  折线点距 ≈6.5m（160 点/条），3 实 1 虚 ≈ 20m 实 + 6.5m 空，小地图上约 40px 实 / 13px 空。
- `MiniMapWnd.RailColor` 默认改 `(1,1,1,0.5)`（白色 50%）；`RailWidth` 仍 3px。
- 验证：编辑器空跑 8 段全在框内 → `verts=24`（6 段实 × 4 顶点），字段默认 3/1；offline_compile 10_UI 0 error；Unity 重编后 Console 0 error。
- ⚠ 自测脚本教训：喂给投影的点必须是"锚点坐标"（原点 = 小地图左下角，0..512），传负坐标会被 `rect.min` 再推出去，看起来像"虚线算错"。
"""

with io.open(PATH, 'a', encoding='utf-8', newline='') as f:
    f.write(NOTE)
print('appended')
