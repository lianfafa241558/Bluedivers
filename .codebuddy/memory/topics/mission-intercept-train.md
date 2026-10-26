# 拦截火车任务（MissionInterceptTrain）· 全貌与口径

> 代码：`Assets/Scripts/06Gameplay/Mission/Main/` 下 `MissionInterceptTrain{,_Rail,_Train,_Turret,_Guard}.cs` + `RailPathPlanner.cs` + `RailMetrics.cs` + `RailTrack.cs` + `TrainUnit.cs`（程序集 `06_Gameplay`）；2026-10-15 建立、**2026-10-16 拆文件**（原来 3 个 partial 共 ~1550 行）。
> 联机口径单独一份 → `net-spawn.md §7.2x`（走 4052 的那部分）。

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
- 配置：`List<SKVP<int,GameObject>> additionalTurrets`（**Key 之和 = 单节车厢挂载概率 %**，命中后按各自 Key 占比抽预制体；总和 > 100 = 必挂、Key 退化成权重）+ 挂点名常量 `TurretMountName = "Additional"`（2026-10-18 从字段收成常量）。
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
- ⚠ 清物目标带 `Cover` 会连**巨型悬崖**（`RockCoverDestructor`）一起销毁 ⇒ 只给 `VegetationAndDetail`。
  （2026-10-18 落地成常量 `_Rail.cs` 的 `ClearTarget`。⚠ 纠正了一个坑：`Robbery.prefab` 里这一项存的是 **-1**（所有位都置上 = 连 Cover 一起清），常量按项目口径取 7。）

## 5. Inspector 字段（2026-10-18 从 42 收到 **18**，同日加箭头后又变 **19**）
判据：`Robbery.prefab` 逐字段比对代码默认值 ⇒ **只有 5 个偏离**（`departureCount` 8 / `trainSpeed` 3 / `edgeInset` 5 / `smoothPasses` 3 / `clearTarget` -1），其余"写完没再动过" ⇒ 24 个收成 `const`（**取值一律照 prefab 现值 ⇒ 行为不变**）。
5 个分组（顺序 = 声明顺序）：
- **任务**：`destroyTarget` / `escapeLimit` / `firstDepartureDelay` / `departureInterval` / `departureCount`
- **列车**：`trainEnginePrefab` / `trainCarPrefab` / `firstCarCount` / `maxCarCount` / `trainSpeed`
- **火车卫队**：`guardSquadsPerDeparture` / `guardSquadLimit`（主文件）+ `guardSpawnBack` / `guardLead`（`_Guard.cs`）
  ⚠ 跨文件搬字段必须自带 `[Foldout("火车卫队", true)]`（`EditorOverride` 按"上一个带 Foldout 的字段"归组）
- **轨道**：`trackCount` / `railPrefab` / `edgeInset` / `trainArrowPrefab`（在 `_Arrow.cs`，靠同名 Foldout 并进本组）
- **附加炮塔**：`additionalTurrets`
常量集中在各文件类顶部 `#region 调参常量`（`_Rail.cs` 18 个、`_Guard.cs` 4 个、`_Turret.cs` 1 个、`_Train.cs` 1 个；常量名用帕斯卡，尾注释保留原中文名）。**要再调就改常量**（改回 `[SerializeField]` 字段即可回到 Inspector）。
2026-10-16 的历史变化：`卫队落点后移(米)` 从「列车生成」挪进「火车卫队」；`卫队跟随刷新(秒)` 已删（节拍由 `TickTime` 决定）。

## 6. 日志（排查先看这几条，走 `NetSyncLog`）
`[拦截火车] 轨道 N 规划：路点/总长/走廊登记/起终` · `路基整平：动土 N 像素 / 最大高差 X m` · `发车 #N：轨道/车厢/附加炮塔/卫队` · `附加炮塔计划：N 门（班×节，单节概率%）` · 同步 `生成/发·单位`、`生成/收·单位`。

## 7. 遗留 / 待拍板
- 场景里 `trainCarPrefab` / `trainEnginePrefab` 未拖 ⇒ 目前恒走 `Resources` 兜底路径（编译期会报 `CS0649` 警告，不是错）。
- 附加炮塔概率表默认空 ⇒ 不发炮塔（会打一条日志说明）。
- 铁轨走廊登记半径 `CorridorRegisterRadius`、`PlanExpand` 等值未做过压力实测，大规模地图上按日志里的"走廊登记 N 处"再调 —— 2026-10-18 起它们是 `_Rail.cs` 顶部的常量（不在 Inspector 里了）。
- `guardLead`（24，字段）与 `trainSpeed`（3，字段）不再匹配注释里的"建议 = 火车速度×TickTime"（=9）—— 是刻意保持 24m 前导还是想跟着速度走，待定。
- ⚠ 调试遗留：`_Turret.cs` 里两处 `Debug.LogError("给…加浮空标签")`（正常路径打红字、每门炮塔一条）—— 浮空验证完该删。
- 拆分脚本留档：`.codebuddy/plans/split_intercept_train.py`（行号锚点式拆分，改过这 5 个文件后**不要再重跑**；源文件备份在 `Temp/split_backup/`）。

> 卫队护航（§8）/ 轨道方向箭头（§9）→ `mission-intercept-extras.md`（2026-10-27 拆出）
