# 拦截火车任务（MissionInterceptTrain）· 全貌与口径

> 代码：`Assets/Scripts/06Gameplay/Mission/Main/` 下 `MissionInterceptTrain{,_Rail,_Train,_Turret,_Guard}.cs` + `RailPathPlanner.cs` + `RailMetrics.cs` + `RailTrack.cs` + `TrainUnit.cs`（程序集 `06_Gameplay`）；2026-10-15 建立、**2026-10-16 拆文件**（原来 3 个 partial 共 ~1550 行）。
> 联机口径单独一份 → `net-battle.md §7.2x`（走 4052 的那部分）。

## 1. 文件分工（2026-10-16 拆分后）
- **主文件 `MissionInterceptTrain.cs`**（~112 行）：任务字段 / 生命周期（`InitMission`→`BuildRailOnce`、`StartMission`、`Uninit`）/ 发车时刻表。⚠ `InitMission` 阶段（第一个 yield 之前）就把"规划 + 铁轨走廊登记"做完 ⇒ 之后创建的其它任务点/兴趣点**反过来躲开铁轨**（事后硬绕实测绕不出去）。
- **`_Rail.cs`**（~310 行，只剩流程编排）：量 prefab 尺寸 → 选起终点 → 规划 → 走廊登记/清物 → 路基统一成型 → 逐节铺轨。算法与量尺已外置（见下面两行）。
- **`_Train.cs`**（~255 行）：按时刻表发车（车头 + 车厢）、"到站逃走 / 被打掉"两种收尾、进度/失败判定、开战节拍。
  - 节拍：`BeginTrainService` 里 `TickTime = 3s`；**发车 + 卫队引导都在 `Tick()` 里**（引导在发车之后，见 §4），`Update()` 只留按帧的 `FlushPendingTurrets()`。
- **`_Turret.cs`**（~323 行）：附加炮塔的配置字段 / 概率计划表 / `CreatUnit` + 4052 / 槽位复刻与挂起补挂（原 `_Train` 的「附加炮塔」region，见 §3）。
- **`_Guard.cs`**（~92 行）：卫队生成（房主 `CreatPatrol`）、Tick 引导、静默移除；配置字段 `guardSpawnBack` 也在这（**必须自带 `[Foldout("火车卫队")]`** —— `EditorOverride` 是按"上一个带 Foldout 的字段"归组的，见 `plans/tooling.md`）。
- **`RailPathPlanner.cs`**（~465 行，`internal sealed class`，纯算法不碰 MonoBehaviour）：边缘选点 + 栅格 A* + 拉直 + Chaikin + 坡度采样，参数全由构造注入（`stepLength/planExpand/slopeWeight/avoidMargin/minAngleSeparation/minChordRatio/random` ⇒ 类内是 `_` 前缀字段，XML `<see cref>` 已同步改）。
- **`RailMetrics.cs`**（~82 行，`internal struct`）：`RailMetrics.Measure(prefab)` 能量出长轴/短轴/轴向/YawOffset/PivotToBottom（⚠ 绝不能 Instantiate 探针，见 §4）。
- `RailTrack`（折线 + 弧长表 + 设计路基纵坡）被**铺轨与行驶共用** ⇒ 火车必然跑在铺出来的那条线上；`TrainUnit` 挂车头、按弧长摆车厢。

## 2. 定的口径（别推翻）
- **起终点 = 空气墙圆周**：半径 `= min(size.x,size.z)/2 − Constants.TaskBorder − edgeInset`（不是 `TerrainData` 边界）；约束 = 夹角 ≥ `minAngleSeparation`、弦长 ≥ 半径 × `minChordRatio`、离每个 `AreaCircle` 圆心 ≥ `Radius/2`，并在合格候选里挑**最平**的一处。
- **规划**：栅格 A*（外扩 `planExpand`、避让 `ReservedCircles`+`AreaCircles` 并留 `avoidMargin`）→ Chaikin 平滑到单顶点转角 ≤ `maxVertexTurnDeg`。
- **路基**：按 `bedStep` 采样地面 → 纵坡按 `bedSmoothLength`(60m) 滑动平均 → `TerrainUtils.SmoothCorridorBed` 成型（实现坑见 §4）。
- **铺轨**：`MeasureRail` 量 prefab 包围盒得真实尺寸（不写死）→ 按弧长切成整数节、节内按**弦向**摆 + 长轴缩放 ⇒ 首尾相接；`tarsonisrail` 长轴在 local X ⇒ `YawOffset = −90°`。
- **随机**：一律 `random`（BattleRandom 派生），且时刻表/炮塔计划都在**第一个 yield 之前**算好 ⇒ 两端一致。
- **联机**：车头 + 车厢（都没有 `Actor`）两端各自 Instantiate、靠**同名**对齐；**附加炮塔有 `Actor` ⇒ 必须走 `CreatUnit` + 4052 复刻**；卫队走房主 `CreatPatrol`。

## 3. 附加炮塔
- 配置：`List<SKVP<int,GameObject>> additionalTurrets`（**Key 之和 = 单节车厢挂载概率 %**，命中后按各自 Key 占比抽预制体；总和 > 100 = 必挂、Key 退化成权重）+ `turretMountName`（默认 `Additional`）。
- 挂点：`Resources/Prefabs/GameEvent/TrainEngineCar.prefab` 的 `Additional` 空子物体（局部 y = 8）；挂点缺失 = 告警 + 本节不挂（两端一致）。
- 计划表 `turretPlan[班次][车厢] = 条目下标 / −1`，开局算一次；发车只查表、不再动随机流。
- 槽位 `slot = 班次 × maxCarCount + 车厢序号`；车厢按名字 `TrainCar_{班次+1}_{车厢+1}` 找回。
- 预制体要求：**有 `Actor`、`UnitTypeEnum != Player/Friend`、静止型**（自带 `NavMeshAgent` 的会跟父级 transform 打架）。
- 收尾：`RemoveTurrets`（有 `I_AIController` 就 `Kill(true)` 静默）→ 再销毁车厢。

## 4. 踩过的坑（都实测过）
- ⚠⚠ **火车卫队引导必须"先发车、后引导"，且在同一帧内**：`EnemyMobile.Start` 按 `PatrolPos != default` 决定进 Patrol 还是 Idle，而 **Idle 的 `IdleBehavior` 根本不查 `PatrolPos`** ⇒ 新卫队若晚一帧以上才拿到巡逻点，就**永久站桩**（并且被 Idle 超时自毁逻辑盯上）。所以 `RefreshAllGuards()` 放在 `Tick()` 里 `DepartTrain()`（→ `SpawnGuards`）**之后**。
  也**不能**图省事在 `SpawnGuards` 里设 `PatrolPos = spawnPos`：那是哨兵语义，`UpdatePathDestination` 一到点即 true ⇒ `PatrolBehavior` 当场自毁（落点自带 5m 抖动只能偶尔救回来）。
  节拍 3s（原 0.5s 在 `Update` 里刷）：火车 8m/s，每跳目标点挪 24m ⇒ `SetNavDestination` 的 `<1m` 去重救不了、必然重排路径，降频就是省 `PathRequestManager`。
- ⚠ `Terrain` 上**没有** `GetSteepness`，要用 `TerrainData.GetSteepness(u,v)`（吃归一化坐标、返回**弧度**）。
- ⚠ 轨道预制体长轴在 **local X**（子 Cube `localScale (24,6,6)`）⇒ 只 `LookRotation`（对齐 +Z）会每节转 90°、看着断断续续，必须带 `YawOffset`。
- ⚠ 预制体根上可能残留**建模世界坐标**（`TrainEngineCar` 根上就是 `(272,29.36,302.42)`）⇒ 一律 `Instantiate(prefab, pos, rot, parent)`，**别用两参版**（会把物体甩到几百米外）。
- ⚠⚠ `SmoothCorridorBed` **不能"按段顺序就地 lerp"**：弯道处相邻段的断面矩形重叠 ⇒ 同一像素被推两次、两次目标不同 ⇒ 地形切出**周期性 V 形锯齿**（2026-10-15 实测截图）。
  修法：像素用"到线段的**最近点投影参数 t**"定位（不用段的局部坐标系），**每个像素只由最近的那一段成型一次**（`bestDistance[]` 胜出 + 弧长 target），软门用**原始高度**判断要不要动土。
- ⚠ 路基太"花"的三个旋钮：`bedSmoothLength`↑ ⇒ 纵坡更直（挖填更多）；`flattenThreshold`↑ ⇒ 小起伏**完全不动土**（消掉"半切"波纹）；`flattenBlend`↑ ⇒ 护坡更缓。
- ⚠ 清物理不要用 `ClearInRectXZ`（**只支持轴对齐矩形**，斜轨会多清一大片）；沿弦向盖圆 `ClearInRadius`（半径 = 轨道半宽 + 余量）拼出胶囊走廊，铺完 `TerrainClearer.Flush()` 一次兑现。
- ⚠ `clearTarget = All` 会连**巨型悬崖**（`RockCoverDestructor`）一起销毁 ⇒ 默认只 `VegetationAndDetail`。

## 5. Inspector 分组（可调项都在注释里写了"为什么"）
轨道 / 轨道规划 / 路基 / 地表处理 / 任务 / 列车 / 列车生成 / 附加炮塔 / 火车卫队。
（2026-10-16：`卫队落点后移(米)` 从「列车生成」挪进「火车卫队」；原「卫队跟随刷新(秒)」**已删** —— 刷新节拍由 `TickTime` 决定。字段名都没变，prefab 值不受影响。）

## 6. 日志（排查先看这几条，走 `NetSyncLog`）
`[拦截火车] 轨道 N 规划：路点/总长/走廊登记/起终` · `路基整平：动土 N 像素 / 最大高差 X m` · `发车 #N：轨道/车厢/附加炮塔/卫队` · `附加炮塔计划：N 门（班×节，单节概率%）` · 同步 `生成/发·单位`、`生成/收·单位`。

## 7. 遗留 / 待拍板
- 场景里 `trainCarPrefab` / `trainEnginePrefab` 未拖 ⇒ 目前恒走 `Resources` 兜底路径（编译期会报 `CS0649` 警告，不是错）。
- 附加炮塔概率表默认空 ⇒ 不发炮塔（会打一条日志说明）。
- 铁轨走廊登记半径 `corridorRegisterRadius`、`planExpand` 等值未做过压力实测，大规模地图上按日志里的"走廊登记 N 处"再调。
- ⚠ 调试遗留：`_Turret.cs` 里两处 `Debug.LogError("给…加浮空标签")`（正常路径打红字、每门炮塔一条）—— 浮空验证完该删。
- 拆分脚本留档：`.codebuddy/plans/split_intercept_train.py`（行号锚点式拆分，改过这 5 个文件后**不要再重跑**；源文件备份在 `Temp/split_backup/`）。

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
