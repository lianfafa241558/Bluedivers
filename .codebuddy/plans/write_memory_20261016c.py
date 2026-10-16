# -*- coding: utf-8 -*-
"""2026-10-16：卫队改走 HomePoint（规避 Patrol 到达自毁）+ 补巡逻队上报（daily 追加 + topic §8 改写）。"""
import io, os

d = r'e:\Bluedivers\.codebuddy\memory'

daily = os.path.join(d, '2026-10-16.md')
text = io.open(daily, encoding='utf-8', errors='ignore').read() if os.path.exists(daily) else '# 2026-10-16\n'
text += """
## 卫队改走 HomePoint（用户提议，已落地）：规避 Patrol 的"到达即自毁"
- 结论：**可行且更对**。`HomePoint` = "到达即**待命**"（`EnemyController` 注释明写与 `PatrolPos` 的"到达即**自毁**"不同），
  而且这正是项目自己的口径 —— `MissionOilRefining.CreatRepairPatrol` = `CreatPatrol` + `controller.HomePoint = target`
  + `BattleEventBus.ReportPatrolSpawn(spawn, target, home: true)`
- 两个必须一起做的事（否则换过去不动）：
  1. `HomePoint` 只在 `EnterIdle`（`EnemyMobile`）被读**一次**，`IdleBehavior` 不重发 ⇒ **必须自己每 Tick 调
     `controller.SetNavDestination(护航位)`**；顺带这也把 `Return`（战斗后 AI 下"回出生点 m_OriginPos"）覆盖掉
     —— 取证：`ReturnBehavior()` 只有 `UpdateAutoRotate()`，目的地只在状态切换时下发一次，**不会抢回去**
  2. Idle 自己还有一个自毁：`!IsFixed && Time.time >= m_IdleStartTime + IdleMaxDuration(120s) && 最近玩家>80m ⇒ Kill(true)`
     ⇒ 给卫队 `IActor.IsFixed = true`（⚠ `Actor` 上该字段注释"只对 EnemyMoble 有效"，全工程只有那一处读它 ⇒ 无副作用）
- ⚠ `PatrolPos` 必须**保持 default**：出生那一帧 `EnemyMobile.Start` 按 `PatrolPos != default` 选状态 ⇒ 设了就会进 Patrol（自毁又回来）
- ⚠ 成员端 `SetNavDestination` 被 `RemoteDrivenMovement` 门掉（还会刷限流日志）⇒ 只在非成员端推；成员靠房主广播
  （取证：`EnemyController.SetNavDestination` 内部就是 `BattleEventBus.EnemyMove → NetRoomFlow.SendEnemyMove(4024?)` 那一处）
- **顺带修掉一个真 bug**：`SpawnGuards` 从来**没有上报巡逻队**（`CreatPatrol` 在成员端被闸成空表）⇒ **成员端根本没有火车卫队**；
  现已按 `MissionOilRefining` 口径补 `ReportPatrolSpawn(spawnPos, spawnPos, home: true)`。
  旧口径（PatrolPos）在成员端还会被 `ReplayPatrol` 复刻成 `PatrolPos` ⇒ 成员侧卫队走到该点后自毁（HomePoint 口径没这问题）
- 现在的平衡站位 ≈ **车头侧前 ~9m、两侧 ±10m**（护航位 = 车头 + 前向×24m(一个 Tick 行程) + 右向×±10m + 同侧错开 8m）
- 验证：离线编译 0 错误、lints 干净；待重进 Play 复测
"""

io.open(daily, 'w', encoding='utf-8').write(text)

topic = os.path.join(d, 'topics', 'mission-intercept-train.md')
t = io.open(topic, encoding='utf-8', errors='ignore').read()
t = t.split('## 8. 卫队护航')[0].rstrip() + '\n'

t += """
## 8. 卫队护航（2026-10-16 实测口径，最终版）
### 8.1 用 `HomePoint`，不用 `PatrolPos`
- `PatrolPos` = "到达即自毁"（`EnemyMobile.PatrolBehavior → UpdatePathDestination()` 命中 `PathReachingRadius`=2m ⇒ `Kill(true)`）
  ⇒ 追移动目标必踩；`HomePoint` = "到达即待命"（`EnemyController` 注释原话），`MissionOilRefining.CreatRepairPatrol` 就是这套。
- ⚠ `PatrolPos` 必须保持 `default`：出生那一帧 `EnemyMobile.Start` 按它选状态（有值 ⇒ Patrol；否则 Idle + HomePoint）。
- ⚠ `HomePoint` 只在 `EnterIdle` 读一次、`IdleBehavior` 不重发 ⇒ **必须自己推目的地**：每 Tick
  `controller.SetNavDestination(护航位)`。顺带覆盖 `Return`（战斗后 AI 会下"回出生点 `m_OriginPos`"；
  取证：`ReturnBehavior()` 只有 `UpdateAutoRotate()`，不会抢回目的地）。
- ⚠ Idle 的另一个自毁：`!IsFixed && 超过 IdleMaxDuration(120s) && 最近玩家 > 80m ⇒ Kill(true)` ⇒ 给卫队
  `IActor.IsFixed = true`（该字段"只对 EnemyMobile 有效"，全工程仅此处读 ⇒ 无副作用）。
### 8.2 生成与联机
- 生成：`manager.CreatPatrol(spawn)` → 每只 `GuideGuard()`（`IActor.IsFixed = true` + `HomePoint`）
  → **同帧** `RefreshAllGuards()`（出生那帧 `Start` 就要读到护航位）。
- 联机：房主权威，必须 `BattleEventBus.ReportPatrolSpawn(spawn, target, home: true)`（4052）。
  ⚠ 不报 ⇒ **成员端没有这支卫队**（`CreatPatrol` 在成员端被闸成空表）；报 `home:true` 才能让成员端
  `ReplayPatrol` 写成 `HomePoint`（报 `PatrolPos` 的话成员侧会走 Patrol 然后自毁）。
- 成员端的移动目的地靠房主广播：`EnemyController.SetNavDestination` 内部就是
  `BattleEventBus.EnemyMove → NetRoomFlow.SendEnemyMove` 那一处 ⇒ 成员端**不要**自己推（会被 `RemoteDrivenMovement` 门掉 + 刷日志）。
### 8.3 编队与速度
- 护航位 = 车头 + 前向×`guardLead`(24 = `trainSpeed`×`TickTime`，为了跑动连贯不顿挫) + 右向×`guardLateral`(10)×±1
  + 同侧沿轨道错开 `guardLineSpacing`(8m)×(index>>1) ⇒ 10 只时左右各 5 槽；平衡站位 ≈ **车头侧前 ~9m、两侧 ±10m**。
- 速度**按帧**兜（目标点走 Tick，避免打爆 `PathRequestManager`）：> 1.5×`guardHoldDistance`(15) ⇒ `1.35×` 追；
  附近 ⇒ `1.0×` 等速跟住；< 0.6× ⇒ `0.9×` 主动落回（别往车厢里钻）。
  ⚠ `AddModifier(Extra, want − FinalValue)` 必须"补差"（Extra 累加）—— AI 在 Patrol/Beware/Return 切换时会自己增删修饰（实测 2~3 m/s）。
- 取数手法：`execute_code` 反射 dump `liveTrains → TrainRuntime.Guards` + 每只的
  `HomePoint/PatrolPos/m_lastDestination/aiState/NavMeshAgent(speed,velocity,hasPath,remainingDistance,isOnNavMesh)`。
"""

io.open(topic, 'w', encoding='utf-8').write(t)
print('ok', len(text), len(t))
