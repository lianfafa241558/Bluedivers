# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「轨道变纯直线 + 一节一节台阶」修复（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：修「路径变纯直线」+「一节一节台阶」
### 病灶 A（直线）：视线拉直只看碰撞、不看坡度
- 我的 string-pulling 判据是 `!LineBlocked(a,b)` —— 只要直线不撞障碍就直连 ⇒ A* 按坡度绕出来的缓坡路线被整段抹平，变成一条直线
- 修法：拉直判据换成「**同一套代价**」——`LineCost(a,b) <= (cost[j] - cost[i]) * 1.02f` 才直连（LineCost 用同样的 slopeWeight×坡度/45° 计价）
- ⚠ 关键细节：不可走格子的 `slope[]` 是 0 ⇒ **LineCost 必须先判 blocked 再累加**，否则穿过悬崖会被算成"更便宜"
- 另外把重建的节点索引一起留着（nodeIndices）才能取 `cost[nodeIndices[j]] - cost[nodeIndices[i]]` 当绕行代价

### 病灶 B（台阶）：每节压平成"自己中心高度的水平平台"，且不俯仰
- 原实现：逐节 ModifyHeightMapRect(depth=0) ⇒ 每节是一块水平平台，相邻节高差 = 地形在 24m 上的抬升（于是台阶），节本身也不跟纵坡
- 修法（三层）：
  ① **设计纵坡**：沿弧长按 bedStep(8m) 取地面高度 → 长窗（bedSmoothLength=60m）滑动平均 → bedProfile；`BedHeight(s)` 线性插值
  ② **节按纵坡俯仰**：`grade = Atan2(bed1-bed0, len)`，forward=(dir.x·cosG, sinG, dir.y·cosG) 交给 LookRotation；y = (bed0+bed1)/2 - sinkDepth - PivotToBottom·cosG ⇒ 相邻节端头落在同一点
  ③ **动土只在地面与设计路基差 > flattenThreshold(1.2m)** 处，且路基板只覆盖 bedStep(≈8m)（不是整节 24m）⇒ 板间高差 = 纵坡×8m，肉眼接近连续坡；淡化带 flattenBlend 照旧
- 参数分组：信息 / 规划 / 路基 / 地表处理
- 验证：704 行 + 离线编译 4 程序集 0 错误 + `06_Gameplay.dll` 15:43:56 前进 + Console 无编译错误
- 顺带发现（与本次无关的既有问题）：Scene 视图每帧刷 `NullReferenceException @ Assets/Scripts/00Tools/DrawLabelUtils.cs:69 (OnDrawGizmos)`
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
