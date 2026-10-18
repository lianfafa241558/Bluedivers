# -*- coding: utf-8 -*-
"""新建 2026-10-19 日报并写入「RailRoutes 缺 CanvasRenderer」这条修正。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, '.codebuddy', 'memory', '2026-10-19.md')

NOTE = """# 2026-10-19

## 小地图铁轨线不显示：RailRoutes 上没有 CanvasRenderer

- 现象：Hierarchy 里 `RailRoutes` 存在但"什么子物体都没有"，小地图上没有任何线（用户截图确认）。
- 诊断（MCP `execute_code` 读运行中真实状态，Play 中）：`graphic`/`routes=2`/`project` 都在，`rect=(−256,−256,512,512)`，
  轨道点投影 `p0=(289.28,6.59)`、`pmid=(197.73,210.05)` 都在框内 ⇒ **几何数据没问题**；但 `go.GetComponents<Component>()`
  里没有 `CanvasRenderer`，读 `Graphic.depth` 直接抛 `MissingComponentException: There is no 'CanvasRenderer' attached`。
- 根因：`[RequireComponent]` 只在**编辑器 Add Component 菜单**生效，**运行时 `AddComponent` 不会自动补依赖**。
  `Graphic` 靠 `canvasRenderer.SetMesh(...)` 提交网格，没有它 ⇒ 一条顶点都画不出来（表现=空物体、画面无线）。
- 修法：`new GameObject("RailRoutes", typeof(RectTransform), typeof(CanvasRenderer))`（`MiniMapWnd.CreateRailGraphic`）。
- 顺带验证（编辑器内空跑，不依赖 Play）：建同样的 GO + 恒等投影喂 3 段折线，反射调 `OnPopulateMesh(VertexHelper)`
  → `verts=8`（2 段在框内、1 段出界被 Liang–Barsky 裁掉），首个四边形顶点 = 拐点 ±1.5px，符合预期。
  ⚠ 反射取 `OnPopulateMesh` 必须定点重载（`Graphic` 同时有 `VertexHelper` 和 `Mesh` 两个版本，直接 `GetMethod` 会 AmbiguousMatchException）。
- 结论：以后在**运行时**给 Graphic/Renderer 类组件建 GameObject，依赖组件（CanvasRenderer/MeshFilter/MeshRenderer/RectTransform）
  都要在 `new GameObject(name, typeof(...))` 里写全，别指望 RequireComponent。
"""

if os.path.exists(PATH):
    io.open(PATH, 'a', encoding='utf-8', newline='').write('\n' + NOTE)
else:
    io.open(PATH, 'w', encoding='utf-8', newline='').write(NOTE)
print('written', PATH)
