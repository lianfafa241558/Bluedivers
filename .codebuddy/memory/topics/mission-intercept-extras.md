# 拦截火车 · 附加系统（卫队护航 / 轨道方向箭头）

> 2026-10-27 从 `mission-intercept-train.md` 拆出（超 8000 预算）。主文件讲任务本体与轨道/车厢/附加炮塔。

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

## 9. 轨道方向箭头（2026-10-18，`MissionInterceptTrain_Arrow.cs`）

- 生成：铺轨循环里 `k > 0` 的每个**节接缝**放一个（`FinishTrack` 调 `SpawnTrackArrow`），落点/朝向沿用该节：
  `rot = LookRotation(forward) * Euler(0, YawOffset, 0)`（forward 已含纵坡俯仰）、
  `pos = (接缝XZ, bed0 − cos(grade) − PivotToBottom) + rot * Vector3.up * ArrowLift(0.5m)`
  —— **抬升走箭头自身法线（局部 +Y）**而不是世界 Y（斜轨上法线是斜的）；`k = 0` 的轨道起点不放（那里是车头出生点）。
- 预制体：字段 `trainArrowPrefab`，空则 `Resources.Load("Prefabs/GameEvent/TrainArrow")`；都没有就整局跳过（装饰物不该拦任务）。
- 实测（Unity 内反射，2026-10-18）：`TrainArrow.prefab` **单网格、bounds 居中 (0,0,0)、size 3.0×0.3×3.9（长轴 Z ⇒ YawOffset=0）**、
  材质 `TrainArrow_ncl` shader = `Custom/EmittBlend`、`HasProperty("_EmittIntensity")=true`；
  ⚠ 根上残留建模坐标 `(0,0.64,-12)` + 手工斜置 `(356,1.4,345)` ⇒ 必须用**带 parent 的 Instantiate**（会一并覆盖掉）。
- 点亮：发车 `DepartTrain` → `RefreshTrackArrows(track)`；收尾 `DetachTrain` 摘掉后再刷一次。
  **不是"谁结束就关"**：按"这条轨道上还有没有活着的火车"重算（`runtime.Unit.Track == track`），同轨两列车不会互相误关。
- 写法：`MaterialPropertyBlock` 写 `_EmittIntensity`（1 亮 / 0 灭），**先 `GetPropertyBlock` 再 `SetFloat`**（别清掉别人的块属性）；
  铺完一条轨道 `SealTrackArrows` 先置 0（材质资产里强度是亮的）。
- ⚠⚠ **`MaterialPropertyBlock` 不能写成字段初始化器 `= new()`**：Unity 原生对象在 MonoBehaviour 构造期创建会抛
  `UnityException: CreateImpl is not allowed to be called from a MonoBehaviour constructor (or instance field initializer)`
  —— 2026-10-18 实测踩到（Console 里两条红字），改成懒创建。
- 缓存：`Dictionary<RailTrack, TrackArrowSet>`（key 用**实例**，因为 `departureTracks` 存的是 `tracks` 的列表下标、
  而 `RailTrack.Index` 在"某条轨道规划失败"时会错位）+ `TrackArrowSet{ Objects, Renderers }`（渲染器只 GetComponents 一次）。
- 疑似笔误（未动）：铺轨那行 `y = (bed0+bed1)*0.5 − Mathf.Cos(grade)` ⇒ 实际是 `bed − 1m`，
  1m 这个常量更像 "该用 `railMetrics.PivotToBottom`" 的占位；箭头沿用了同一高度。
