# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「铁轨改到 InitMission 铺、让其它任务反向避让」决策（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：改到 InitMission 铺轨 ⇒ 让后续任务点反向避让铁轨（用户提议，成立）
### 时序证据（改动前先核过）
- `BattleManager`：先 `TerrainUtils.Main = mapRoot.terrain` + 日志「地形耗时」，**之后**才 `MissionCont.Init(...)`（BattleManager.cs:203）⇒ InitMission 时地形/植被/巨型悬崖占地圆都已就绪
- `MissionController.InitAllMission`：任务按 `mapEntitySize.y` **降序**创建（"优先刷大的"），主线只要尺寸最大就是第一个；**兴趣点/场景点在所有任务之后**生成（InitializeAsync 顺序：InitAllMission → InitInterestPoint → InitScenePoint）
- ⭐ 关键机制：`StartCoroutine` 会**同步执行到第一个 yield 之前**；`CreatMission` 紧接着把任务 `enabled=false`（协程挂起，到开战 `enabled=true; EventStart()` 才继续）
  ⇒ 把「规划 + 走廊登记」放在第一个 yield 之前，登记就发生在**下一个任务点生成之前**
### 实现
- `MissionInterceptTrain.InitMission()` 覆写 → `BuildRailOnce()`（`_buildStarted` 防重；`StartMission` 也调一次做兜底）
- `BuildRail` 的同步前缀（无 yield）：校验 → 量轨道尺寸 → 起终点 → `CollectObstacles` → 规划（含放松重试）→ 折线平滑 → 累计弧长 → **`StampAlongPath`**（登记 + 清物入队）
- `StampAlongPath`：登记用 `corridorRegisterRadius`(默认 12m，用户口径"范围12的点")，**清物用另一个半径** `clearRadius`（轨道半宽 + 余量 + 树冠 + 路基淡化带）——两个半径别混
- 之后才 yield 的部分：路基板整平（一板一帧）→ 逐节摆放（8 节一帧）→ 收尾 Flush
### 注意事项
- 创建顺序不是硬保证（按尺寸降序）⇒ 若之前已有别的任务点，铁轨仍会躲它们（双保险）；想让它更早，就把它的 `mapEntitySize.y` 配到最大
- `MissionController.IsOverlapWithExistingPoints` 的判定是 `圆心距 < 新点半径 + 已占半径 + 5` ⇒ 12m 走廊 + 5m 间隔 = 其它任务点中心离轨道中线还要再加它自己的半径，占位比较"硬"，若兴趣点变少/刷不出就把 `走廊登记半径` 调到 8
- 验证：794 行 + 离线编译 06_Gameplay 0 错误 + `06_Gameplay.dll` 15:57:40 + Console 0 error
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
