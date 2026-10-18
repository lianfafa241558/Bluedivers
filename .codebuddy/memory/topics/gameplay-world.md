# 主题 · 家具 / 任务 / 事件 / 逻辑帧 / 存档 / 地形

> 由 `gameplay.md` 拆出（2026-10-11）。预算 ≤ 8000 字符。同伴：`gameplay.md`（战斗/AI/伤害/组件存活）、`gameplay-player.md`（玩家/相机/武器/战备/寻路）。
> ⚠ 原文已下沉：`archive/topics-snapshot-20261011/gameplay.md`。

## 家具 / 手持 / 交互
- 家具入口 `Furniture_*.furnData` 按 `Id` 分发；手持范式 `Furniture_HandEquip` + `HandEquip : IEquippable`；按键 **E = Operate、X = Equip**（X = 卸载轮盘 `EquipmentUninitiatedUI`）
- 手持装备时**按 E 的三去向**：入口 `PlayerOperationController` → `EquipController.TryDropHandEquip(bool replace)`。
  ① 正前方 `target` 是**按下即拾取的手持物**（`is Furniture_HandEquip && MeetTime == 0`）→ **先丢手中的、再拿面前那件**（`replace=true` ⇒ 经 `HandEquip.SkipRestoreWeaponOnUninstall` 不切主武器 + `UninstallEquip(silent:true)`，避免两条语音连播打架）
  ② `target.CanOperate(player)` → E 让给交互（炮位 / Kei 提交点都要求"手上有物"）
  ③ 其余（无目标 / 不可交互 / `target` 粘滞）→ 丢下
  ⚠ 判据必须用 `CanOperate` 而非只判 `target != null`（`target` 会**粘滞**）；丢下判定在 `target.Handle()` **之前** ⇒ 天然不会"拾取当帧即丢下"。`HandEquip` 不再自己监听输入
- 交互链：`PlayerOperationController`（扫描/长按/`IStepPress`）→ `IFurniture.Handle` → `GlobalEventBus.FurnitureOperate`；`InOperation` 只有 `FurnitureFlag.SwitchState` 家具会被置 true（消费方：`PlayerInputHandler`、`PlayerWeaponsManager.OnOperation` 切空手）
- NPC 语音家具 `Furniture_NPCChat`：持 `SoundGroup_SO`，交互 → `AudioSvc.PlaySound(soundGroup.Get(pos))`，协程 `WaitForVoiceEnd` 靠 `isPlaying` 串行化，`OnDisable` 复位
- `Furniture_OOPartDepositBase`（欧帕兹提交台 / 玩家火炮的基类）：**接管按压** `IStepPress`，按 `SubmitPerSecond=5` 逐个从操作者背包转移到容器（`held++` + `OnSubmit`）；松手保留已转移部分
- ⚠⚠ **`Furniture_General.Operate()` 不调 `base.Operate()`**，且 `furnData["SignaTower"]` 只配了 `_InOperateUpdate` ⇒ `inOperate` 永为 false ⇒ 旋转不生效、**联机 4036 也永不触发**。同款空 `_Operate` 的配置点了没反应（`Beacon.prefab` 的 4 个 `Plane`）。新增配置项要么给 `_Operate`，要么在 lambda 里显式调 `furn.BaseOp()`
- ⚠ `Furniture_Pipe` 的 `Id` 就是状态键（Pipe/PipeLink/PipeWait/PipeComplete/PipeError），写入走 `SetId`（写回身份源 `Identity.Id`）；`InOperateUpdate` 用**操作者位置**做落点射线；`Place()` 会 `ModifyHeightMap`（改地形高度图）与 `UpdatePipeBounds` ⇒ 远端重放时须跳过（`Furniture_Attached.IsRemoteReplay`）
- ⚠ `MissionLidarStation` 原来写 `OnComple.RemoveListener`（2026-10-11 修成 `AddListener`）⇒ 雷达站任务原本**永远完不成**、`RevealAllMissions` 也不触发

## 任务系统
- 计时器 `TickBehaviour.Tick()` 1s；⚠ `GameRootBase<T>` 静态成员下层不能调 ⇒ 委托反转（`TimerRequest`/`ClipLoader`）或搬 `00_Core`
- 任务战备 `BattleEventSub.RequestAuthorize(id, state)` → `+= state?1:-1`（0 = 隐藏不可用）；任务台词只能走 `WndManager.CreatNotice` / `GlobalEventBus.Notice`
- 静态事件总线层 = `min(发布者层, 订阅者层)`：`UnitEventBus`(05_UnitCore) / `GlobalEventBus` / `BattleEventBus`(06Gameplay)
- `CampTemplate.patrolTemplate` = `List<SKVP<string,int>>`（Key = 巡逻队名，Value = 权重），驱动 `WaveManager` 的 Patrol 构建
- 战备 SO 键序列：`DirectionEnum` Left0/Up1/Right2/Down3；`opter` 是 little-endian `uint[]`；首键分类 100% 合规、尾键（伤害类型指纹）规律未贯彻；`Y_Machine`/`Y_OilPlane`/`Y_EvacuationBeacon` 的 `opter` 为空
- 任务生命周期：**起** = `TaskManager.SetTask()`（选图，`GameState=Ready`）；**止** = 进 `GameStateEnum.Bridge` → `OnGameStateChange` → `ResetTask()`（`nowTask = new SelectTaskData()` + 复用 `SyncTaskState()` 发中性值）。`TaskState` 发布点共 3 处（`Init`/`SetTask`/`ResetTask`）
- ⚠ 回大厅后 `nowTask` 为空（=「还没选图」的中性态）：`TaskState.Countdown` **不**在 `ResetTask` 复位（约定归 `SetTask` 的 16）；读 `MapId`(=`nowTask.mapCfg`)/`EnemyVarietyType`/`RequiredAD` 的消费点必须自带守卫（`TaskState.HasTask`、`nowTask.activeTask`）
- `MissionOilRefining`（炼油）：`errorTimes`（何时开始维修）与"挑哪几根管坏"在联机下走**纯函数种子**（`Derive(seed, index)`；`Seed==0` 单机走 `random`）；维修期间 `keyScreen.AddTime(1)` 抵掉 tick 让终端停摆
- 任务实体的朝向 `MissionBase.MissionAngle()` 从本局种子派生（同 seed + 同 pos ⇒ 恒等；单机走 `RandomUtils`）
- `KeyScreen`（按键终端）：阶段推进由**驱动端**上报（`IsStageDriver()`：有人操作 = 操作者那台 / 无人阶段 = 房主），其余端只应用；非驱动端把**输入换成远端按键**跑同一套逻辑

## 逻辑帧 / 时序
- 驱动 = `09Manager/Global/NetManager.cs`（`LogicFrame.Sink` 宿主）：`_accumulator += Time.deltaTime`，每够 `Constants.LoginFrame`(0.02s = 50Hz) 跑一帧，单渲染帧封顶 `MaxCatchUpTicks=10`（**债务保留不丢弃**）
- ⚠⚠ 旧写法"绝对 `Time.time` 比较 + 每渲染帧最多 1 tick" ⇒ **FPS<50 时逻辑帧率 = FPS**（44fps→0.88×），按 `TickTime` 累加的**武器冷却/射速系统性变慢**（战备"首发 2.68s"漂到 3s+），且债务单调累积、帧率恢复后"快进"回放 ⇒ **一切"逻辑时间 vs 真实时间"偏差先查这里**
- `Add/Remove` 在 tick 期间**入队**、`TickOnce` 末尾 `FlushPending`（同帧 Add/Remove 互相抵消），防 `LogicTick` 内销毁 ⇒ `OnDestroy→Unregister→Remove` 漏跳
- `Time.deltaTime` 与旧 `Time.time` **同域**（都随 `timeScale`）⇒ 暂停/慢动作语义不变，别换 `Time.unscaledDeltaTime`

## 存档 / 设置（`ArchivesData_SO` / `DisplayDic`）
- 唯一读入口 `ArchivesData_SO.Current.GetSetting(name)`；`ArchiveLoader.Init()` 写入 `Current`；**补齐默认设置必须同步做在 `Init()` 里**（`yield return null` 比同帧 `Awake` 晚一帧，期间读缺键就出事）
- ⚠ 缺键 = 拿到 `DisplayDic` 的 `DefaultValue` 模板（`ArchivesFloat.value == ""`）⇒ `float.Parse("")` **抛 FormatException**（`?? "0"` 挡不住空串）⇒ `ArchivesFloat` 必须 `TryParse` + `InvariantCulture`，写入端 `ToString` 也要 `InvariantCulture`（当前文化写 "0.10" 在逗号小数点区域会被读成 10）
- ⚠ `DisplayDic` 索引器兜底会**把占位条目写进字典**，之后 `Synchronize` 认为"键已存在"拒绝补齐 ⇒ 已加 `[NonSerialized] _placeholderKeys`（占位可被真实值覆盖、setter/`Add` 清标记）；**插入行为保留**，因为 `VehicleWnd` 靠"索引器返回同一引用"就地改载具改装项
- 存档落点是 `Application.dataPath/../`（编辑器再 `/../`）⇒ **编辑器与打包读的是两个不同文件**："编辑器好、打包坏"先比两个 json 的 `settingDic` 键集合

## 地形 / 渲染
- NavMesh 在 `MapRoot`；跨层只传 `GameObject[]`/基础类型
- ToonLit 5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` = albedo 唯一入口；未进 CBUFFER = 死属性；溶解 `clip(step(dissolve,v)-dissolve+_EdgeWidth)`
- 天气 `WeatherSystem.RollWeather` 的随机源**必须按权威种子派生独立流**（`SeedStream.Weather=14`）：原来抽全局 `BattleRandom`，它在开局被任务点/兴趣点生成推进到不同位置 ⇒ 联机"一边晴一边雨"；单机（`TaskState.Seed==0`）仍沿用全局流
- ⭐ **主地形 = 一图一份 `TerrainData`**：`MapData_SO.terrainAsset` 持有，`BattleManager.InitTerrain` 在**读 terrainData / 设 size / SetTextures 之前**换上（留空 = 沿用 BattleScene 里那份 `MainMap`）。⚠ 打包端 `treePrototypes`/`detailPrototypes` 的 setter 是 **Editor-only**（只刷"only implemented in editor"、什么都不改）⇒ 原型**只能烘在资产里**；生成入口 `Tools/地形/生成每图 TerrainData（方案A）`（`Assets/Editor/TerrainDataBaker.cs`，**改了原型数组后打包前必跑**）+ `Tools/地形/体检`
- ⚠⚠ **`TerrainCollider` 自带独立一份 `terrainData` 引用、不跟随 `Terrain` 组件** ⇒ 换地形时必须同时 `terrain.GetComponent<TerrainCollider>().terrainData = ...`，否则"看着是新地形、脚下踩空/悬空"（2026-10-11 修）
- ⚠ 地形资产硬约束：`detailResolution`/`detailResolutionPerPatch` 是**只读属性**（脚本改不了，运行时也没人赋值）⇒ 专属资产**必须由"复制 MainMap"得来**，`new TerrainData()` 会让草分辨率变默认值而变糙；能缩的只有运行时**每次都会重设**的 `heightmapResolution`(→33)/`alphamapResolution`(→16)（`BattleManager` + `ApplyAlphamapsInChunks` 整张重写混合图），再加上**把细节（草）层全部清零**（`ClearAllDetailLayers`，草每局现场生成）⇒ 单份 13MB → 1.17MB → **约 49KB**（7 份合计 ~344KB）。⚠ 烘焙顺序必须**先清树实例、再换 `treePrototypes`**（反了会逐条报 `Tree removed: invalid prototype N`）。⚠ `terrainLayers` 不能省：`SetTextures` 只改已有层的 diffuse/tile、**从不建层**

> 以下由 `net-battle.md` §7.2d 并入（2026-10-18）：一个 float 被 5 处按 5 种语义复用；
> 结论是**有意不同步**（结果早已由 4036/4046/4049 过网）。联机口径见 `net-battle.md` / `net-damage.md`。

### 7.2d `Furniture_Attached.ExtFloatParameter` 为什么不同步（2026-10-12 查证）
- 定义：`Furniture_Attached.cs:89`（`[InspectorName("外部浮点数参数")]`）—— **一个 float 被 5 处按 5 种语义复用**。
- 全仓**只有两处写**（其余全是读 ⇒ 那些是 prefab 上配的常量）：`KeyScreenControl.cs:401/407`（旋钮 `+= 10*deltaTime` 累加）、
  `Furniture_General.cs:139-143`（`SignaTower`：把累加值当"本帧转角"，`relatedTrans.Rotate(...)` 后 `= 0`）。
- 五处用法：① `Furniture_Artillery.cs:80` 放置上限 / `:144` 炮弹下标·伤害档位（读）；② `OOPart.cs:27` 采集数量（读）；
  ③ `Furniture_General.cs:149` `Animator.Play` 的**层号**（读）；④ KeyScreen 旋钮累加量（写）；⑤ SignaTower 转角（写+清零）。
- 结论：**不同步是对的**。①②③ 是 prefab 常量（两端同一个资产 ⇒ 天生一致）；④⑤ 是"每帧累加、读后清零"的**瞬态输入积分** ——
  同步它得每帧发消息，且丢一帧/频率不同就永远对不上角度。而**结果**早就在网上了：终端按键 4049（远端把键盘换成远端按键、跑同一套
  `DirectionUpdate` ⇒ 累加量自然算出同一份）、家具交互 4036（`SyncId` + `Operate()` 重放）、任务阶段 4046。
- 真正的坏味道不是"没同步"，而是**一个字段混 5 种语义**（角度/个数/animator 层号/上限）⇒ 将来要拆就按类型拆成具名字段
  （`ShellLimit`/`ShellIndex`/`CollectCount`/`AnimatorLayer`/`RotationDelta`）。
