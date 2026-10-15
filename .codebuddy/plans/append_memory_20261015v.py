# -*- coding: utf-8 -*-
"""2026-10-15 收尾小结（索引）→ daily；判据已抽进 topic。可重跑需先删掉末尾同名小节。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

head = '## ✅ 当日收尾小结（拦截火车一条线，索引）'
text = text.split(head)[0].rstrip() + '\n'

text += """

## ✅ 当日收尾小结（拦截火车一条线，索引）
- 过程流水见本文件上文各条；**判据已抽进 topic**：全貌（轨道/路基/列车/炮塔/坑/旋钮）→ `topics/mission-intercept-train.md`；联机口径 → `topics/net-battle.md §7.2x`；路由表已加一行
- 涉及文件：`Mission/Main/MissionInterceptTrain{,_Rail,_Train}.cs`、`RailTrack.cs`、`TrainUnit.cs`、`00Tools/TerrainMainUtils{,_Corridor,_Reserved}.cs`、`09Manager/Battle/MissionController.cs`
- 交付状态：离线编译 `00_Utils` / `06_Gameplay` 0 错误、lints 干净；**尚未在 Play 里实跑验证**（轨道观感、路基平整度、联机复刻日志三条都还没实测）
- 待你填 / 待拍板：① 附加炮塔 `additionalTurrets` 还是空表（= 不发炮塔）② 建议选静止型敌方炮塔（`Prefabs/Enemy/Robot/PMC/PMC_GuardTower_*`），别用玩家空投炮塔 ③ 场景里 `trainEnginePrefab` / `trainCarPrefab` 没拖（恒走 Resources 兜底）④ 清物目标默认 `VegetationAndDetail`（`All` 会连巨型悬崖一起清）
"""

io.open(path, 'w', encoding='utf-8').write(text)

d = r'e:\Bluedivers\.codebuddy\memory'
for rel in ['2026-10-15.md', 'MEMORY.md', 'topics\\mission-intercept-train.md', 'topics\\net-battle.md']:
    p = os.path.join(d, rel)
    print(len(io.open(p, encoding='utf-8', errors='ignore').read()), rel)
