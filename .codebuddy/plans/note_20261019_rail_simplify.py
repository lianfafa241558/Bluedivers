# -*- coding: utf-8 -*-
"""追加：小地图铁轨线改「分桶平均成直线段 + 按长度铺虚线」。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, '.codebuddy', 'memory', '2026-10-19.md')

NOTE = """
## 小地图铁轨线：去掉"碎折角"——按弧长分桶取平均 + 虚线改成按长度

- 用户反馈：为了做软边把轨道拆得太碎，小地图上每 5~6px 一个折角（毛刺）；"完全可以变成一段一段的直线（取轨道的平均值）"。
- 数据：轨道点距 ≈6.5m（160 点/条），小地图 ≈0.875px/m（NowMapSize≈585m/512px）⇒ 折角间距只有 5~6px，所以看着碎。
- 改法（`MiniMapRailGraphic`）：
  1. `BuildStraightRoute()`：按**世界弧长**分桶（`simplifyStep=30m`），桶内所有点取平均作为该段代表点 ⇒ 一段段直线；末点强制用真实终点（否则轨道短一截）。
  2. 虚线不再按"折线段序号"算，改成**按弧长**铺：`dashLength=21m` / `gapLength=7m`（≈3:1），
     每个实线小段再投影+裁剪+生成四边形。这样改简化步长不会连带改变虚线节奏。
- 验证（编辑器空跑，反射调 `BuildStraightRoute`/`OnPopulateMesh`）：
  - 每点 ±3px 抖动的 60 段折线 → 简化为 22 段，**中间点偏离 0.00px**（抖动被平均干净）；
  - 480px 直线 → 实线总长 361px，占空比 0.752（期望 21/28 = 0.750）。
- 遗留注意：`OnPopulateMesh` 里的投影必须喂"锚点坐标（0..512）"，自测时传负坐标会被 `rect.min` 再推出去，看着像算法错。
"""

with io.open(PATH, 'a', encoding='utf-8', newline='') as f:
    f.write(NOTE)
print('appended')
