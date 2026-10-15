# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「拦截火车：车厢附加炮塔」实现（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：车厢附加炮塔（静态同步）
- 改的文件：`MissionInterceptTrain_Train.cs`（主体）+ `MissionInterceptTrain_Rail.cs`（补一行调用）
- 配置：新 Foldout「附加炮塔」→ `List<SKVP<int, GameObject>> additionalTurrets`（Key=概率，Value=炮塔预制体）
  + `turretMountName`（默认 "Additional"，车厢预制体 `Resources/Prefabs/GameEvent/TrainEngineCar.prefab` 下已加该空子物体，局部 y=8）
- 概率口径：表里所有 Key 之和 = **单节车厢挂炮塔的概率(%)**；命中后按各自 Key 占比挑预制体；总和 > 100 ⇒ 必挂、Key 退化成权重
- 静态同步（关键）：`BuildTurretPlan()` 在**本任务第一个 yield 之前**（紧随 `BuildDepartureSchedule()`）用 `random`(BattleRandom)
  把 [班次][车厢序号] ⇒ 条目下标（-1=不挂）一次算好；发车时只查表、不再动随机流 ⇒ 两端连"挂不挂"都一致。
  每节固定取 2 次随机（不挂也取）⇒ 随机流长度与结果无关，少一处隐性分叉。
- 挂载：`AttachTurret` 用 `car.transform.Find("Additional")`；**必须显式 `Instantiate(prefab, mount.position, mount.rotation, mount)`** ——
  本项目有预制体根上残留建模世界坐标（TrainEngineCar 根上就是 (272,29,36,302)），用 2 参 `Instantiate(prefab, parent)` 会把炮塔甩到几百米外。
  挂点缺失 ⇒ 告警并回落车厢根。
- 收尾：`TrainRuntime.Turrets` 记录本趟炮塔，`DetachTrain` 里先 `RemoveTurrets`（有 `I_AIController` 就 `Kill(true)` 静默，否则 Destroy）再销毁车厢
- 两端各自 Instantiate（车头→车厢→该节炮塔，顺序固定）= 现有口径，**不发新网络消息**；`Actor.Awake` 是 NetId 唯一分配点，同序 ⇒ 两端同号
- ⚠ 炮塔预制体要选"敌方单位"型（如 `Prefabs/Enemy/Robot/PMC/PMC_GuardTower_Fusillade`、`PMC_GuardTower_Grenade`）；
  `Turret_Mission_Machine` 的 flag 含 `Unimportant + Invincible`（不取号、无敌）⇒ 不适合当可打掉的附加炮塔
- 验证：离线编译 00_Utils / 06_Gameplay 0 错误；lints 干净
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
