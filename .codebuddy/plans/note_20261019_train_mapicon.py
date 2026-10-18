# -*- coding: utf-8 -*-
"""追加：小地图上的"跟着列车走"的任务图标。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, '.codebuddy', 'memory', '2026-10-19.md')

NOTE = """
## 小地图：火车动态任务图标（Robbery 没有 entity）

- 现象根因：`MiniMapWnd.MissionPointCreat` 第一件事就是取 `mission.entity`（`MissionView`），拦截火车任务
  `Robbery.prefab` **没有 entity** ⇒ 直接 `LogWarning("警告任务…没有实体") + return` ⇒ 地图上永远没有火车图标。
- 没有改数据去"造 entity"，而是给列车单独一条"动态图标"通道：
  - `MissionInterceptTrain`（Train 分部）新增 `public int LiveTrainCount` / `public TrainUnit GetLiveTrain(int index)`
    （越界 null；`TrainUnit.Position` = 车头当前世界 XZ；到站/被打掉会从 `liveTrains` 移除 ⇒ 计数立刻减）。
  - `MiniMapWnd` 新增 `List<RectTransform> TrainPoints` + `RefreshTrainPoints()`（**每帧**，因为车在动，不能放地图插值分支）
    + `AddTrainPoint()`：懒创建，复用 `MissionPointPrefab`，`[0]` 区域圈关、`[1]` 图标体开、`[1,0]` 外框关、
    `[1,1]` 设 `train.icon` 与 `MainColor`。多列同时在跑就多摆几个，多余图标隐藏。
- 预制体结构（实测）：root 子物体 = [0] area / [1] icon；icon 子物体 = [1,0] bg（Hexagonal_Frame6 外框）/ [1,1] icon（Image）。
- 注意：`Image` 的 sprite 为 null 时**不生成几何**（=不可见）⇒ 图标为空时打一条警告。
- 验证：06_Gameplay / 10_UI 离线编译 0 error；Unity 重编后 Console 无编译错误。另外这条时机也顺带确认了
  **上一次 Play 没有再卡死**（日志里是 `UpdateAndCompileScripts → ExitPlayMode`，说明 Play 一直在正常跑）。
"""

with io.open(PATH, 'a', encoding='utf-8', newline='') as f:
    f.write(NOTE)
print('appended')
