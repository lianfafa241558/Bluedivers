# 拦截火车任务（MissionInterceptTrain）· 全貌与口径

> 代码：`Assets/Scripts/06Gameplay/Mission/Main/MissionInterceptTrain{,_Rail,_Train}.cs` + `RailTrack.cs` + `TrainUnit.cs`（程序集 `06_Gameplay`）；2026-10-15 建立。
> 联机口径单独一份 → `net-battle.md §7.2x`（走 4052 的那部分）。

## 1. 三个 partial 的分工
- **主文件**：任务/时刻表/生命周期。⚠ `InitMission` 阶段（第一个 yield 之前）就把"规划 + 铁轨走廊登记"做完 ⇒ 之后创建的其它任务点/兴趣点**反过来躲开铁轨**（事后硬绕实测绕不出去）。
- **`_Rail`**：起终点选点 → A* 规划 → 走廊登记/清物 → 路基统一成型 → 逐节铺轨。
- **`_Train`**：按时刻表发车（车头 + 车厢 + 附加炮塔 + 卫队）、"到站逃走 / 被打掉"两种收尾、进度与失败判定、`INetSpawner` 生成器实现。
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

## 6. 日志（排查先看这几条，走 `NetSyncLog`）
`[拦截火车] 轨道 N 规划：路点/总长/走廊登记/起终` · `路基整平：动土 N 像素 / 最大高差 X m` · `发车 #N：轨道/车厢/附加炮塔/卫队` · `附加炮塔计划：N 门（班×节，单节概率%）` · 同步 `生成/发·单位`、`生成/收·单位`。

## 7. 遗留 / 待拍板
- 场景里 `trainCarPrefab` / `trainEnginePrefab` 未拖 ⇒ 目前恒走 `Resources` 兜底路径（编译期会报 `CS0649` 警告，不是错）。
- 附加炮塔概率表默认空 ⇒ 不发炮塔（会打一条日志说明）。
- 铁轨走廊登记半径 `corridorRegisterRadius`、`planExpand` 等值未做过压力实测，大规模地图上按日志里的"走廊登记 N 处"再调。
