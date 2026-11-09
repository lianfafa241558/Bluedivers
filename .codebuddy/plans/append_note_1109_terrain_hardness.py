# -*- coding: utf-8 -*-
"""把今天的"地形硬度场"记事追加到 daily（追加模式，不动原内容）。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
path = os.path.join(ROOT, '.codebuddy', 'memory', '2026-11-09.md')

NOTE = """## 地形硬度场（低分辨率，2026-11-09）
- `TerrainUtils`（`Assets/Scripts/00Tools/TerrainMainUtils.cs`）新增硬度场：`float[] hardness`，分辨率 = `heightmapRes/2`（高度图 512 ⇒ 256²），`Main` 赋值时重建、置空时作废。
- 只在"挖"这条路上生效：`ModifyHeightMap(..., isSet:false)`（炸弹坑 `FpsHelper_Hit`、`Furniture_Pipe`）实际挖深 × (1-硬度)；`isSet:true`（任务点整平 / 附加地形）不受硬度约束，否则任务平台永远压不平。硬度 = 1 的格子整格跳过（不能只把挖深归零 —— 那步 Lerp 到 centerOldHeight 会把低洼处抬起来）。
- 采样 `SampleHardness(u,v)` 必须双线性，否则低分辨率会在坑壁上留方格棱；高度图像素 → uv 就是 `(xBase+x)/heightmapRes`。
- 写硬度：`StampHardness(uv, 内/外半长, shape, angleDeg, 内值, 外值)`，内圈取 inner、外圈取 outer、中间线性过渡，**同格只增不减**；⚠ 必须在本轮高度写完之后调（先刷后挖 ⇒ 这个坑被自己刚刷的硬度削弱）。
- `ModifyTerrainData` 新增 `innerHardness`/`outerHardness`（`[Range(0,1)]`），圆/矩形两条路径都透传；0 = 不动硬度（旧数据行为不变）。
- 验证：`python .codebuddy/plans/offline_compile.py 00_Utils 06_Gameplay` → 0 error。
"""

with io.open(path, 'a', encoding='utf-8', newline='\n') as f:
    f.write('\n' + NOTE)

print('appended', path)
