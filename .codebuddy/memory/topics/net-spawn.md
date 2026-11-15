# 主题 · 联机 · 生成器复刻实例（房主唯一创建 + 4052）

> 由 `net-battle.md` 拆出（2026-10-18），含原 §7.2f 上半（生成侧复核）+ §7.2x（生成器第 4 例 = 任务车厢炮塔）。
> 号空间与 `CreatUnit` 签名见 `net-netid.md`；拦截火车全貌见 `mission-intercept-train.md`。
> ⚠ 原 §7.2f 下半（两族合并 / 伤害落地 / 验证）已拆到 `net-damage.md`。

### 7.2f 复核结论（2026-10-12）
- ✅ **玩家部署物（地雷/哨戒炮）不是缺口**：全部由 `10_Effect/VFX/VFXAirdropEffect.cs` 创建 —— 远端走 `TmpAirdrop`（`BattleManager.ReleaseAirdrop` + `SetCallerSid`），
  本机走 `SetOwner` 抛 `BattleEventBus.Airdrop` ⇒ 上报 4027 / 房主 4028 广播 ⇒ **两端各自 Instantiate 同一发战备的 `creatObect`**（落点/战备 id 已过网）。
  残留：落点取各自的地面射线命中点 ⇒ 地形被炸过后会略有差异（与"地形破坏不同步"同源）。
- ✅ **`EnqueueInit` 三件套复核**：`CreateBuilding` **无随机**（位置/旋转取 transform、prefab 由 `data.Get(TaskManager.Instance.EnemyVarietyType)` 决定）⇒ 两端一致，不用改；
  `CreatOOPart` / `CreateSupple` 有随机（`RandomUtils.RandomVector2` 全局流 + `BattleRandom.Bool` + `prefabs.RandomTake`）⇒ **已改**
  `SeedUtil.NewPlacementStream(seed, SeedStream.PropSpawn, transform.position)`（按生成器坐标量化派生 ⇒ 两端同落点/同数量/同 prefab），
  顺带修掉"生成物在战斗前创建 ⇒ 取**派生号**（含位置）⇒ 落点一分叉号就错"的老问题。
- ✅ **`DieLoot` 已静态同步**：掉落数量从 `UnityEngine.Random` 改成 `Derive(Derive(seed, SeedStream.DieLoot), NetId)`（在 `Start` 里算，
  因为同 GameObject 上 Actor/DieLoot 的 Awake 顺序不保证）⇒ 两端数量一致、零消息；位置本来就是常量偏移。
- ⏳ **`UnitSkill_OrbitArmor`（无敌装甲）**：用户问"伤害能同步，装甲状态就能同步吧？" ⇒ **成立，但有两个前提**：
  ① 装甲单位要有**两端一致的号**（现在 `:71` 裸 `Instantiate` ⇒ 必须并进统一生成入口 / 合并后的 `HostSpawn`）；
  ② 主单位的 `PartController.AddInvincibleArmor` 是**本地列表**（`:77`）⇒ 成员端复刻时也必须把装甲注册进它那份 PartController。
  齐了之后：装甲掉血/死亡两端一致 ⇒ `OnAllInvincibleArmorDestroyed` 两端同时触发 ⇒ 无敌状态自然一致。

### 7.2x ⭐ 生成器第 4 例 = 任务（车厢附加炮塔，2026-10-15）
> 全貌（轨道 / 路基 / 列车 / 炮塔）→ `topics/mission-intercept-train.md`；卫队护航 / 轨道箭头 → `mission-intercept-extras.md`；这里只留**联机口径**。
- `MissionInterceptTrain : MissionBase, INetSpawner`；`InitMission` / `Uninit` 成对 `SpawnerRegistry.Register/Unregister`
  （⚠ 不能放 `Start`：复刻可能在 `Instantiate` 内就发生）。
- 槽位 `slot = 班次 × maxCarCount + 车厢序号`（两端算式一致）；**槽位不带 prefab** ⇒ 这一节挂哪门从两端各持一份的**开局计划表**读
  （计划表在第一个 yield 之前用同一条 `BattleRandom` 算好 ⇒ 抽签一致；发车只查表、不再动随机流）。
- ⚠ **`SpawnSourceKey` 不能照抄 `NetSpawnKey.Of(gameObject)`**：任务根是 `MissionController` 的子物体（transform 位置全一样）、
  名字又只差 "(Clone)" ⇒ 多个任务互相撞键、复刻到别的任务里。任务类改用
  `ActorsManager.DerivedNetId(类名 + "#" + MissionBase.netOrder, transform.position)`（`netOrder` = 任务跨端稳定键）。
- ⚠ **`CreatUnit` 固定把单位挂 `ActorsManager` 下**（"生成者死了不能把单位一起带走"）⇒ 要挂到移动挂点 / 载具**必须自己 re-parent**
  （`SetParent(mount,false)` + 局部零位；成员侧 `Instantiate(prefab, mount.position, mount.rotation, mount)` 天然同款局部值）。
- ⚠ **新形态：复刻早于"载体"出现**。两端各自 Tick 驱动建车厢，房主 4052 可能比成员建车早 1~2 帧 ⇒ `ReplaySpawn` 找不到车厢时
  **不能直接失败**（会掉在轨道上不跟车）：记 `pendingTurrets`、`Update` 里重试、10s 超时丢弃 + `Warn`；
  复刻位置/朝向取**本端挂点**，别用消息里的 pos（含 RTT 期间车厢前进的位移）。
- ⚠ 这条通道的 prefab 必须**有 `Actor` 且 `UnitTypeEnum != Player/Friend`**（这两类不取号 ⇒ `ReportUnit` 只告警不上报 ⇒ 成员端静默缺失）。

### 7.2y ⭐ 列车位置同步 = 4067「弧长」（2026-11-14 落地，生成器第 4 例的补丁）
- 问题：车头 + 车厢两端各自 `Instantiate`，位置由 `TrainUnit` 每帧 `_distance += _speed * Time.deltaTime` 积分，
  发车又由各端自己的 `Tick`（本地 `Time.time`）驱动 ⇒ 帧率/卡顿不同就是固定偏移，且 `maximumDeltaTime` 会永久丢时间。
- 口径：**只发弧长不发位姿**。两端轨道是同一份确定性规划 ⇒ `Distance` 唯一决定车头 + 全部车厢（`TrainUnit.Place`）。
  `TrainPoseMsg { Tour(班次 0 基), Distance }` + `TrainPoseBatchMsg`，房主 2Hz 全量下发（幂等 ⇒ 丢包自愈、不给挂起队列）。
- 键是**班次**：不能用 `Actor.Id`（所有趟次都是 `TrainEngineSam` ⇒ 撞键），也不能用 `Actor.NetId`（见 §7.2x 的号错位风险）。
- 分层：节流在 06（`MissionInterceptTrain_Train.PublishTrainPoses`，成员端 `RemoteDriven` 直接返回）→ 静态事件
  `MissionInterceptTrain.TrainPoseTick`；09 的 `NetTrainBridge`（自举）打包 4067 / 收包调 `ApplyRemoteTrainDistance`。
- 成员端追赶：`TrainUnit.ApplyRemoteDistance` 只在差值 > `SnapDistance(15m)` 时吸附，否则把差值按"限速"在几帧内消化
  （⚠ 不能收到就赋 `_distance` ⇒ 每包跳一下，与载具 `NetTransformView` 的结论一致）。
- ⭐ **收尾（到站 / 被打掉）必须显式同步**：`TrainPoseMsg.State`（0 行驶 / 1 到站逃走 / 2 被打掉），
  房主在 `EndTrain` 里立刻单独补一包（不等 2Hz 周期包——那时车已从活车列表删掉）。只靠"血量归零 / 弧长跑满"
  这两个副作用不够：一旦伤害打偏，客机那列车会继续开到站并自己记一次"逃走"，与房主"已摧毁"相反。
  成员端 `ApplyRemoteTrainEnd` → 同一条 `EndTrain` 链（`runtime.Ended` 幂等）；本端还没发出这趟车时记进
  `_pendingRemoteEnds`，`DepartTrain` 那一刻直接收尾（否则会开到站）。
- ⭐ **车头身份 = 派生号**（`EngineNetIdKey + 班次`）：`Instantiate` 前后用 `ActorsManager.Replicating` 让它**不领自增号**，
  再 `NetId = ActorsManager.DerivedNetId("TrainEngineSam#" + tour, spawnPos)`。
  ⚠ **不能直接 `ForceDerivedNetId`**：派生号的序号按"同 Id 同位置的**当前存活**单位"数，而同轨每趟都从同一起点发车、
  前车早开走 ⇒ 每趟都数成 0 ⇒ 多趟同轨的车会撞号。带班次的键每趟唯一，且位置是确定性起点 ⇒ 两端同号。
  自增段的毛病是"依赖两端创建同序"，而发车（任务 Tick）与刷怪（波次 Tick）是两条独立时钟，交错一变就错一整批。
