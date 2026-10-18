# -*- coding: utf-8 -*-
"""把 2026-10-18 的「拦截火车 轨道/小地图/车厢伤害」改动追加到日报（一次性脚本，可重跑会重复）。"""
import io
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
PATH = os.path.join(ROOT, '.codebuddy', 'memory', '2026-10-18.md')

NOTE = """
## 拦截火车：轨道可跳过 / 小地图画路线 / 车厢可被击伤

- 轨道（`MissionInterceptTrain_Rail.cs`）：`PlanTrack` 里"规划失败 → 退化成直线"整段删掉，改成 `return null`（上层 `continue` + 一条"跳过 N 条"警告）。直线兜底会叠着切挖同一片地形（褶皱）且那条车照样跑。`RailTrack.Index` 改成 `tracks.Count`（跳过后编号仍连续，日志/箭头名一致）。
- 小地图路线（`10UI/UI/MiniMapRailGraphic.cs` 新增 + `MiniMapWnd.cs`）：`MaskableGraphic` 自绘折线（每段一个四边形，线宽固定 px）；`OnPopulateMesh` 先 `project(world)+rect.min` 换算到本组件局部坐标、再 Liang–Barsky 裁剪到 rect；挂在 `MissionPointRoot` 下 sibling 0（底图之上、任务点之下），根节点 `Mask` 兜底裁剪。投影函数 = `MiniMapWnd.WorldPosToMapPos`；口径依据：点位预制体根 anchorMin=anchorMax=(0,0)、pivot=(0.5,0.5) ⇒ anchoredPosition 以父左下角为原点。
- 数据入口：`MissionInterceptTrain.Active`（static，InitMission 赋值 / Uninit 清空）+ `IReadOnlyList<RailTrack> Tracks`。小地图 FirstShow 建线并喂数据，ShowWnd 每次重投，2s 巡检条数变化；任务结束后不清空（轨道留作遗迹）。
- 车厢不掉血根因：`TrainEngineCar.prefab` 上只有 `Damageable`（armorLevel 3 / armorValue 0）、**没有任何 Health**（车头是 Actor+HealthEnemy+Damageable）⇒ `Damageable.InflictDamage` 第一行 `!Health.IsValidMono()` 就 return。之前 `DepartTrain` 只给 `TransferDamageable` 赋 source，而车厢上没有这个组件。
- 修法（决定：**保持一列车一个 Actor + 一份血量**，不改成多 Actor）：`Damageable` 加 `[NonSerialized] public Damageable relay` + `SetRelay()`；`InflictDamage` 开头转给 relay；`IDamageable.ActorGo` 跟随 relay（顺带修掉 `AirdropPod`/穿透弹读车厢 ActorGo 的 NRE）；`Tick` 在无 Health 时直接返回。`DepartTrain` 改为 `car.TryGetComponent(out Damageable d) → d.SetRelay(engineDamageable)`。锁定类武器仍只锁车头（锁定目标池 = Actor），一列车本就一份血，暂不改。
- 验证：offline_compile 05_UnitCore / 06_Gameplay / 10_UI 0 error（10_UI 需先 `tmp_compile_newfile.py` 塞新文件）；MCP `refresh_unity` 后 meta 生成、`10_UI.dll` 23:54 更新、Console error 0。
- 未验证：小地图折线在 Play 里的视觉（要开一局拦截火车）；车厢受击的实际手感。
"""

io.open(PATH, 'a', encoding='utf-8', newline='').write(NOTE)
print('appended', len(NOTE), 'chars ->', PATH)
