# -*- coding: utf-8 -*-
"""追加：小地图铁轨线导致 Unity 无响应的排查与加固。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, '.codebuddy', 'memory', '2026-10-19.md')

NOTE = """
## 小地图铁轨线 · Unity 卡死（无响应）事故与加固

- 现场：用户"进入游戏"时 Unity 无响应。`Editor-prev.log` **最后一条消息** = `警告任务Robbery(Clone)没有实体`
  （`MiniMapWnd.MissionPointCreat` ← `BattleEventBus.MissionStart` ← `MissionBase.EventStart`），即卡死点就在
  "小地图开始建线"那一帧附近；日志里**没有任何异常/刷屏**，Windows 事件日志也没有崩溃/挂起记录（手动杀的，无 dump）。
- 我写的风险代码（v2 版）：`OnPopulateMesh` 里用 `while (walked < len) { ... walked += piece; }` 铺虚线 ——
  **自增靠浮点累加**，一旦 `piece` 小于 `walked` 的精度就永远不前进 ⇒ 直接卡死编辑器。这类"用 float 累加当循环进度"
  的写法在 UI 重建里是致命的（每帧重建一次）。
- 修法：改成**按整数周期枚举**（`for (int k = firstCycle; k <= lastCycle; k++)`，区间 = `[k*cycle, k*cycle+dash] ∩ [start,end]`），
  迭代次数只由 `len/cycle` 决定，天然有界、不依赖累加进度。另加：`len` 非有限/退化跳过、`dash` 用 `Clamp(0.1, cycle)`、
  顶点硬上限 `MaxVerts=40000`。
- 可观测性/开关（下次卡住能定位）：`[小地图/铁轨] 开始生成网格：路线 N 条` + `... 简化后共 M 点 / 网格顶点 K`
  （每次 `SetRoutes` 只打一次）；`MiniMapWnd` 新增 `显示铁轨路线` 开关，关掉就不建对象、不进画线代码。
- 旁证：float32 语义仿真旧循环 20 万组参数（phase 0~1100 / len 0.01~40）**没复现**卡死；新代码压测 2×160 点、
  连续重建 200 次 = 35ms（单次 0.176ms、212 顶点）⇒ 顶点量不可能造成卡顿。所以"不能 100% 断言就是自增循环"，
  但那条循环无论如何都该改（形态上就是会挂的写法）。
- 教训：UI `OnPopulateMesh` 里**任何循环都必须有整数迭代界**（或明确的最大步数），不能靠"每次至少前进一点"的浮点假设。
"""

with io.open(PATH, 'a', encoding='utf-8', newline='') as f:
    f.write(NOTE)
print('appended')
