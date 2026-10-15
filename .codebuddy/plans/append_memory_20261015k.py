# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「拦截火车：prefab 单位化 + 列车/卫队系统」实现（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 拦截火车：prefab 单位化 + 列车/卫队系统（大块功能落地）
### prefab 单位化（用 MCP execute_code + PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset 做的，已逐项复核）
- 事实依据（探针）：敌人单位在 **layer 6 = Unit**；`EnemyController.InitComponent` 对 NavMeshAgent/DetectionModule 都有兜底（`if(NavMeshAgent)` / `if (DetectionModule.IsValid())`）
- TrainEngineSam(车头)：递归设 layer=6 + `BoxCollider`(9.51×8.18×17.27，center 按 bounds) + `Damageable` + `HealthEnemy`(MaxHealth=3000 整数型!、MainPart→Damageable) + `Actor`(Id=TrainEngineSam、type=4 Enemy、team=1、**flag=4 Boss**、UseHpBar=true、HpHeight=1、rangeLength/halfHeight 按 bounds)
  - ⚠ `Health.MaxHealth/MaxShield` 是 **Integer（PEInt）** 序列化属性 ⇒ 用 `p.intValue`，用 floatValue 会失败（第一次跑就踩到）
  - 车头**不挂** EnemyController/NavMeshAgent：移动由我们自己的 TrainUnit 直接写 Transform，不需要 AI
- TrainEngineCar(车厢)：layer=6 + `BoxCollider`(9.51×7.28×14.09) + `TransferDamageable`（`source` 留 null，生成时由代码指向车头 Damageable）
- 两个模型的长轴都在 **Z**（尺寸 x≈9.5 / z≈17.3、14.1）⇒ `MeasureRail` 自动判 LengthAxis=2、YawOffset=0 ✓
- 复核心得：SerializedProperty 的枚举字段读值要用 `intValue`（`ToString()` 只会回 "UnityEditor.SerializedProperty"）
### 新代码（06_Gameplay 0 错误，源 204）
- `RailTrack.cs`：把原来嵌套在任务里的轨道数据类提成独立 public 类（铺轨与列车共用 ⇒ 火车必然跑在铺出来的线上）
- `TrainUnit.cs`（挂在车头）：沿轨道逐帧推进，车厢按弧长偏移跟随；`SampleForward` 已含纵坡；到站 fire `OnArrived`
- `MissionInterceptTrain_Train.cs`（partial）：发车时刻表驱动的 TickLoop（`TickTime=5`）、`DepartTrain`、卫队 `CreatPatrol` + 每 0.5s 改写 `PatrolPos` 跟随、两种收尾（到站逃走 / 被打掉）、进度与失败判定
  - 到站：车头 `Health.Kill()`（origin=null ⇒ 不计击杀，但照 fire OnEnemyDead ⇒ HpWnd 收掉 boss 血条）+ 销毁车厢 + `Kill(true)` 移除卫队 + `escapedTrains++`，满 `escapeLimit` 就 FailMission
  - 被打掉：`IHealth.OnDie` → `AddProgress`（`Ended` 标记区分"到站移除"⇒ 不计进度）；卫队留在原地（走到最后目标点后自毁）
  - 卫队里的 Boss 标：`CreatPatrol` 返回后立刻 `actor.RemoveFlag(ActorFlag.Boss)`
  - 生成口径：车头/车厢**两端各自 Instantiate**（走敌人自增 NetId，与波次同套）；卫队房主专属 + 4052 复刻
  - 两个预制体有 Resources 兜底（`Prefabs/GameEvent/TrainEngineSam|TrainEngineCar`），Inspector 不拖也能跑
### 已知待确认
- 车头/车厢是**两端各自运动**（`Time.deltaTime`），长时间行驶会小幅漂移；要严格一致就得走 `SyncedNavMover` 那类房主权威通道
- BattleScene 里本来就摆着两个 TrainEngine 模型（静态装饰），现在它们也带上了单位组件 ⇒ 建议从场景里删掉/换成实例外的美术
- boss 血条在 `HpWnd.OnEnemyCreate` 时按 flag 建，所以"摘 flag"发生在极早期才稳（我们是在 `CreatPatrol` 返回后立刻摘的）
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
