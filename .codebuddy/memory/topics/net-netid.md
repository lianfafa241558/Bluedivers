# 主题 · 联机 · 号空间（NetId）/ 生成入口 / 成员端 LOD

> 由 `net-battle.md` 拆出（2026-10-18），含原 §7.1 / §7.2a / §7.2b / §7.2c。
> 一句话判据：**"两端能不能算出同一个出生坐标？"** 能 ⇒ 派生号；不能 ⇒ 房主分配（4052）。

### 7.1 ✅ NetId 一律统一 + 伤害增量广播（2026-10-12 已全 8 步落地）
> 原执行交接文档 `plans/联机_NetId统一_伤害通道_执行计划.md` **已执行完并归档**（索引 `plans/archive/INDEX-plans-20261013.md`，过程见 `2026-10-12.md`）——下面留的是**设计口径**，供后续新增同步照抄。
- **号空间统一、号段分离**（保留 `int`，`FindActor` 只查 `Actors`）：玩家/盟友 = `sid` 段；**场景摆放 + 任务实体 = 派生键**
  `FNV1a(Actor.Id + 量化位置 + 同Id同位置序号)`（两端各算、零消息；`type` 是 `Other`/`Enemy` 都不影响取号）；**运行时生成 = 房主分配**（4051）。
  判据一句话：**"两端能不能算出同一个出生坐标？"** 能 ⇒ 派生；不能 ⇒ 房主分配。
- **房主唯一创建 + 复刻（4051）**：`EnemyNestBuild.ActiveBehavior`(裸 Instantiate + 全局 `RandomUtils`)、`UnitSkill_Summoner.SkillStart`、
  `CreatEnemy.Update`(`BattleRandom.Bool`) 这几条改成调**统一入口**（`CreatUnit` 加 prefab 重载）；消息带 `来源键 + 槽位下标 + NetId + pos/yaw/目标点`；
  成员复刻后**覆盖 `NetId`**（且 `Actor.Awake` 在复刻期间**不得自增**）。⚠ 场景/任务实体**不要**走这条（会欠补发全量）。
- **伤害 = 来源端先扣 → 上报房主 → 房主 `SendToAllExcept(sid)` 转发**（用户口径，取代"房主权威结算"）：消息带 `AttackerNetId` +
  `HitPartIndex`（= `Actor.Damageables[]` 稳定下标）+ **来源端算好的最终扣减量**，接收端走 `DamagePacket.Resolved = true` **不重算** AP/抗性。
  ⚠ 玩家血量仍走 `PlayerVital`（自己权威）；死亡仍由房主发 4026。**只做这一半，不做部位全量快照**（不支持重连/中途入房）。
  ⚠ 现状缺口（≥3 人可见）：成员A 的伤害从不到达成员B（`ApplyingRemoteDamage` 把转发挡掉），本方案正好补上。
- **可选（最后）**：`Other` 可破坏物并入同一条伤害事件（升级 4043「只传死了」）。
- ⚠ 开工第一步要先确认：`NetHostSvc`/库侧**有没有 "SendToAllExcept"**（现只见到 `SendToAll`/`SendToSession`）；没有就按 sid 列表逐个 `SendToSession`。
- **✅ 批次 2 执行清单（2026-10-12 第 4~7 步已落地；1~3 步见下）**
  1. ~~`ActorsManager`：加"场景/派生段"常量 + `DerivedNetId(...)`~~ ✅**已落地（2026-10-11，Unity 编译 0 error + 反射核验 True）**：
     `DerivedNetIdBase = 0x4000_0000` + `DerivedNetId(string id, Vector3 pos)`（内部数"同 Id 同量化位置的第几个"）+ 私有 `Fnv1a`。
     ⚠ 调用点必须在 `Actor.Awake` 里、**早于自己 Add 进 `Actors`**（否则会把自己数进去）；`Unimportant` 的 SpecUnit 仍不发号。
  2. ✅**已落地（2026-10-12，Unity 编译 0 error + 反射核验 True）**：`Actor.Awake` 判据改为**"战斗开始前/后"**
     （`BattleState.IsStartBattle`）：开始前 ⇒ `DerivedNetId(Id, pos)`（场景摆放/大厅，从此免疫任何号错位）；
     开始后 ⇒ `type==Enemy` 走 `NextEnemyNetId()`、`SpecUnit`(非 Unimportant) 走 `NextSpecUnitNetId()`；
     `Player/Friend` **不取号**（身份=sid）。⚠ **遗留**：任务实体（`MissionBase.InitMission` 在战斗开始**后**创建）
     目前落进"运行时生成"档（自增号）⇒ 待补：在 `MissionBase` 生成后给子树里的 Actor 显式赋 `DerivedNetId`
     （`NetId` 是 `{get;set;}`，可后赋），或并入 4051。
  2b. （原表述，已由 2 取代）`Actor.Awake` 号来源分三档：`Player/Friend → sid`（不取号）；**场景/任务实体 → 派生键**
     `FNV1a(Actor.Id + 量化位置 + 同Id同位置序号)`（序号需"同组计数器"，在 `Actors` 里按同 Id+量化位置数一遍即可）；
     运行时生成 → 房主分配（复刻期间**不自增**：加一个 `static bool IsReplicating` 门）；
  3. ✅**已落地（2026-10-12，Unity 编译 0 error + 反射核验 True）**：新增 `ActorsManager.ForceDerivedNetId`（静态开关）+
     `MissionBase.InitMission` 用 `try/finally` 包住 `Instantiate` 置位 ⇒ 任务实体里的 `Actor` 在各自 `Awake` 取**派生号**
     （`Instantiate` 同步跑完子树所有 `Awake` 才返回；⚠ 序号口径成立的关键正是"每个 `Awake` 结束才 Add 进 `Actors`"，所以逐个数的序号是唯一且两端一致的）。
     ⚠ 记录：`MissionBase` 的命名空间是 **`FPSGame.Mission`**（不是 `FPSGame.Gameplay`），程序集 `06_Gameplay`。
  4. ✅**已落地（2026-10-12）**：统一入口 `WaveManager.CreatUnit(GameObject prefab, Vector3 pos, Quaternion rot, INetSpawner source, int slot, Vector3 target = default)`
     + 契约转发（`IBattleService.CreatUnit` / `NullBattleService` / `BattleManager.CreatUnit`）。房主（或单机）⇒ Instantiate + 上报 4051；
     **成员 ⇒ return null**（不生成、等复刻）。⚠ 与计划签名差异：多一个 `target`（4051 要带首帧目标点，且只有调用方知道目标）。
  5. ✅**已落地**：**4051 `EnemySpawnNtf`** = `CmdId.EnemySpawnNtf` + `EnemySpawnMsg`（`SourceKey/Slot/NetId/X,Y,Z/Yaw/Tx,Ty,Tz/**PrefabId**`）
     + `NetRoomFlow.SendEnemySpawn / HandleEnemySpawnNtf / static event OnEnemySpawn`；桥 `09Manager/Battle/NetEnemySpawnBridge`（`Install/Uninstall` 挂在
     `WaveManager.Awake/OnDestroy`，`Report` 打 `[同步/生成·发]`、`OnRemoteSpawn` 打 `[同步/生成·收]`，找不到生成器/复刻失败一律 `Warn`）。
  6. ✅**已落地**：三处闸门都实现 `INetSpawner`（`EnemyNestBuild` / `UnitSkill_Summoner` / `10_Effect/CreatEnemy`）——
     `SpawnSourceKey` + `OnEnable/OnDisable` 登记 `SpawnerRegistry`（⚠ 必须 OnEnable：复刻出来的单位在 `Instantiate` 内只跑过 Awake/OnEnable，Start 还没跑）+
     `ReplaySpawn(slot,netId,pos,rot,target,prefabId)`（⚠ 覆盖 `NetId` 早于设目标点；成员用 `ApplyRemoteDestination`、房主用 `SetNavDestination`）。
     `EnemyNestBuild` 顺带把 `RandomUtils.RandomTake` 换成 `EnemyRandom.For(this).RandomTake(creatData)`，槽位 = `creatData.IndexOf(item)`；
     `CreatEnemy` 房主走新 `BattleManager.CreatSpawnerUnit(tier,pos,range,this)`（槽位 = `(int)tier`，成员按 `PrefabId` 回 `WaveManager.FindTierPrefab`），
     **成员 `queue.Clear()` 完全不跑**（不抽 `BattleRandom`）。
  7. ✅ 场景摆放/任务实体**不走** 4051（两端各自创建 + 派生键）。

#### 7.2a 实装时定的口径（与计划有 3 处偏差，别推翻）
- **新增号段 `ActorsManager.HostSpawnNetIdBase = 0x2000_0000` + `NextHostSpawnNetId()`**：运行时生成**不与敌人自增段共用计数器**。
  理由：这类生成的时机由各自 AI / 计时驱动，会与波次的逐 Tick 投放**交错** ⇒ 共用会让两端计数器逐渐错开（其后所有波次单位跟着错）。
  房主侧靠 `ActorsManager.ForceHostSpawnNetId`（Instantiate 前 true / finally 复位）取号；成员侧靠 `ActorsManager.Replicating` **一个号都不领**（号随 4051 覆盖）。
- **来源键 = `NetSpawnKey.Of(go)`（05UnitCore）**：优先 `Actor.NetId`（两端已一致、且不像位置会漂）；只有"无号的场景物件"才退回 `DerivedNetId(Id/名字 + 0.1m 量化)`。
  ⚠ 计划原写"纯 `FNV1a(Actor.Id + 量化位置)`"——对**可移动生成者（召唤师）不成立**（两端位置本来就不同），实装已纠正。
- **asmdef 方向**：06 看不见 09 ⇒ 闸门走契约 `BattleHub.Current.CreatUnit(...)`；10_Effect 看不见 07 ⇒ 成员判定用 `BattleManager.IsNetMember`（新属性）。
- **复刻期间 `Actor.Awake` 不发号** ⇒ 成员不从自增段扣号，`_enemyNetIdSeq` 两端**只被波次/巡逻推进**，口径不再被生成器搅乱。
- 顺序：房主 `CreatUnit` 内**先** `SetNavDestination`（4024 先出）**再** `Report`（4051）；成员侧 4024 先落进 `EnemyNetBridge._pendingMoves`，等副本创建事件补发 ⇒ 无副作用。
- ⚠ 存量问题（本轮**未**改）：`CreatUnit(tier,…)`/`CreatSpawnerUnit` 的派生流**每次调用都用同一个种子**（`UnitPlacement*1000+waveSeq`）⇒ 同 tier 每次都抽到同一把 prefab / 同一偏移。

### 7.2b ✅ 成员端 AI LOD（2026-10-12 落地）
- 新增 `06Gameplay/AI/AiLod.cs`：`ShouldTick(IActor, bool engaged)` —— **只降敌人**（`UnitTypeEnum.Enemy`）、**只在成员端**
  （`EnemyController.RemoteDrivenMovement`）、**交战不降**（`engaged`）；档位按离**本机玩家**的距离：≤60m 每帧 / ≤120m 隔 1 帧 /
  ≤200m 隔 3 帧 / 更远隔 7 帧；⚠ 相位用 `NetId`（无号用 `IndexID`）取模错开，避免整批单位同帧一起跑/一起不跑。
- 落点两处：`AIInputBaseController.Update`（状态机决策；子类可 override `AiLodEngaged` 声明交战中 —— `EnemyMobile` 把
  `Follow/Attack/Beware` 都算交战）+ `DetectionModule.Tick`（1Hz 的 `FindUnits`+视线扫描；⚠ 必须 `return true`，返回 false 会被 TickBehaviour 摘表）。
- **不降**：远端意图落地（`SyncedNavMover`）、伤害/生死（统一通道）、状态与技能 tick（牵扯血量结算）、炮台锁头 `LateUpdate`、动画/FX；
  凯伊/炮台这类 SpecUnit 也整个不降（数量少、走动与开火各自算）。
- ⚠ 两条"为什么这么保守"的硬理由：① 敌人**开火节奏在成员端是本机算的**，而"敌人打我这个玩家"的伤害由本机权威 ⇒ 降频会让玩家少挨打；
  ② 降频**不补 deltaTime** ⇒ 帧内 `Time.deltaTime` 累加的东西（转向/计时器）在远处会变慢。⇒ 想再激进（比如成员端敌人连感知都停）得先有"敌人开火/动画也走同步"。
- 排查口：`AiLod.Enabled` 可置 false 对照；`AiLod.Dump()` 返回"降频帧占比"（execute_code 里读一次即归零）。
- ✅ **"两端 LOD 档位不同会不会不同步" —— 不会**（2026-10-12 查证）：被降频的两处**没有任何"同步量"写入** ——
  `06Gameplay/AI` 里唯一的网络写入是 `EnemyController.cs:309` 的 `BattleEventBus.EnemyMove`，它在 `SetNavDestination` 内部，
  而成员端该方法在 `RemoteDrivenMovement` 处**提前 return**；`DetectionModule.Tick` 只改本地 `Target`。
  同步量各走各的路且都不降：位置（`SyncedNavMover` + agent）、血量（伤害事件 + `Health.Update` 里 `_AboTick=0.5s` 的异常状态 tick、**不在 AI 里**）、生死（`HandleDeath`）、生成（4023/4047/4051）、攻击节奏（交战态不降）。
  ⚠ 扩 LOD 范围的 3 条检查：① 被降代码里不能有网络发送；② 不能写权威量（位置/血量/生死/生成/阶段）；③ 会用 `Time.deltaTime` 累加且结果写回权威的计时器不能降。
- 验证：离线编译 0 error（含 `tmp_compile_newfile.py` 把新文件塞进临时 csproj）+ dll 反射核验（`AiLod.ShouldTick/Dump`、`EnemyMobile.get_AiLodEngaged`）。

### 7.2c 口径确认（用户 2026-10-12 拍板，**别再当缺口/待复核**）
- **不支持重连 / 中途入房**是**设计如此**（不是欠账）⇒ 除 4043 的 `_destroyed` 账本外不需要全量快照；别再加"补发全量"的提议。
- **`MissionController` 的任务/兴趣点布局是静态同步的**（用户确认）⇒ 不必再去复核它用共享 `BattleRandom` / `RandomUtils` 那几处。
- **场景 NPC 的 4032 键用 `Actor.Id` 没问题**（同名不会重复，用户确认）⇒ 不用改成复合键。
