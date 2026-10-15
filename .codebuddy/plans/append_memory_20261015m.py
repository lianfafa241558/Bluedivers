# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「火车：boss 血条 NRE + 多轨道褶皱」修复（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：控制台报错 + 多轨道"褶皱" 定位与修复（有堆栈证据）
### 报错 1：`HpItemBoss.RebuildArmor()` NRE（HpItemBoss.cs:74）
- 堆栈：`HpWnd.OnEnemyCreate` ← `UnitEventBus.EnemyCreate` ← `Actor.WaitSetPos` ← `Actor.Awake` ← **`MissionInterceptTrain:MeasureRail`（rail:711）的 Instantiate 探针**
- 根因 A：`HpItemBoss.Set` 里 `enemy = go.GetComponent<PartController>()` 后 `RebuildArmor` 直接 `enemy.invincibleArmor` ⇒ **单位没有 PartController 就 NRE**（车头当时没挂）
- 根因 B：**探针实例化会跑 Actor.Awake ⇒ 派发 EnemyCreate ⇒ HpWnd 给"探针"建 boss 血条**，而我们随即 Destroy(probe) ⇒ 血条抱着已销毁 Actor，`HpItemBoss.IsPlayerNear → BaseMono.Pos` **每帧 MissingReferenceException**
- 修法：① 车头 prefab 补 `PartController`(FPSGame.AI，字段 legs/invincibleArmor/deathArmor 留空即可) ② **`MeasureRail` 改成不实例化**：用 `worldToRoot * child.localToWorldMatrix` + `mesh.bounds` 8 角点算根空间包围盒（资产也有可用的层级矩阵，零副作用、还更快）
- 验证：反射调 `MeasureRail` 复核三件套 —— tarsonisrail 24/6/Z/0/-12、TrainEngineSam 17.27/9.51/Z/0/0.996、TrainEngineCar 14.09/9.51/Z/0/0.996
  （手工按 `Cube rot=(270,90,0) scale=(24,6,18)` 推根空间盒 = 6×18×24 ⇒ Length24/Width6/YawOffset0/PivotToBottom-12，与实测一致 ✓）
### 报错 2（其实没报错，是 warning）：`轨道2/轨道3 怎么试都规划不出路径，退化成直线`
- 后果：两条直线轨道**横穿彼此与弹坑** ⇒ 走廊反复切挖同一片地 ⇒ 用户看到的"褶皱/阶梯"
- 修法（MissionInterceptTrain_Rail）：
  ① **selfCorridors**：已铺轨道按**实际开挖半径**(clearRadius≈15.5m) 加入下一条的障碍表（登记表里的 12m 太小 ⇒ 两条轨道的路基带会重叠）
  ② **端点周围强行放开**：端点格允许站住、但四周可能全被占位圆封死 ⇒ 把端点 ±stepLength 内的 blocked 格清掉并补坡度（A* 才迈得出去）
  ③ 换起终点重试次数 3 → 5；直线兜底保留但告警写清"地形会被叠着切挖"
- 待用户在 Play 里复查：三条轨道是否都正常绕行（日志应无"退化成直线"）、地形是否不再褶皱
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
