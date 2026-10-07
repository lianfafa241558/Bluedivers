# 主题 · 玩法 / AI / 战斗 / 表现 / 家具任务 / 地形渲染

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。

## 战斗 / AI
- 组件间避免 GetComponent 互取；AI = `EnemyController` + `EnemyMobile` + `DetectionModule` 组合
- team：玩家 **0**、敌人 **2**；`HalfRange>=1` = 大型单位；`HalfHeight=0` = 不过滤
- `PatrolPos` 到达即自毁（`HomePoint` 才是待命）；巡逻队只能经 `BattleManager.CreatPatrol`；`Actor.IsFixed` = 不会因不动被删
- 枪声警惕：`BewarePoint.HasValue` 且点距 `<= Max(Hearing, Detection)`；取点后 `ClearBeware()`；冷却 10s / 半径 10m 收在 `ShouldInvestigateBeware()`
- 伤害统一入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸→冲击波→地形→警告→特效；`isDirect` 绕开爆炸抗性/遮挡；噪声走 `NoiseData` + `OnNoise`（枪口 20、弹着点 8，消费在 `DetectionModule.OnNoise`）
- 波次 `BattleManager.CreatWave(WaveCreateParams)`；`centerGetter` 只影响未落点单位；`extraWave` 跳过冷却
- 波次类层级（2026-10-03 抽基类）：`09Manager/Battle/WaveBase.cs` = `abstract : I_TickClass, IDisposable`，持 `Tick()` 模板（`TickStart` 默认 -4 进 Ongoing／`TickOngoing` **abstract**／`TickNearEnd`=`TryEnterNearEnd(0)`）+ `Trans`（`Yuuka` WaveStart/WaveEnd_Zerg、MusicGroup.Wave/Game）+ 工具方法 `RegisterUnit/SetUnitHome/SetBehaviourEnabled/SetUnitAnimator/EnableUnitColliders/RandomYaw/SnapToNavMesh/GetDropPoint(pad)`。子类：`RobotWave`（鹰群空投，按人口装船 ≤16、到达后 3s 投放，旧名 KaiserWave）、`ZergWave`（兵营空投 + `RedeployPods` 跟随，`centerGetter` 偏离 30m 重部署）
- 上帝类已 partial 拆：`Tool` / `FpsHelper` 各 `_` 系列；`Health.cs` + `Health_AboState.cs`
- ⭐ **敌人状态机 `EnemyMobile`**（`06Gameplay/AI/StateMachine/`；**类顶 XML summary 已是权威逻辑说明**，改行为前先读它）：框架 = `AIInputBaseController<T>`（`StateMachineCore<T>` 内核：状态表/迁移/守卫）→ `AIInputUnitController<T>`（+`turrets` 炮塔瞄准）；状态 `Idle/Patrol/Follow/Attack/Beware/Return/Death`，异常态在同目录 `EnemyMobile_AboState.cs`
  - 每帧守卫链（命中即 return）：`BirthComplete` → `Death` → 弱点僵直清除 → `IsMoveLocked`(Freeze/Vertigo 停导航) → Hacker 锁同队 → `IsForcedAttack`(Toxicity 乱走乱攻) → Terror 逃离 → 查表执行状态行为
  - ⚠ `OnDetectedTarget` **只在** Idle/Patrol/Beware/Return 时重置 `m_TimeStartedDetection`；已在 Follow/Attack 时重置 ⇒ 开火延迟反复重算 ⇒ `mustStop` 分支跳开火 ⇒ 武器卡 `InShoots`（表现"开火被打断/换弹完才开火"）
  - ⚠ `AttackStop` 站桩用 `IsFiringNow`（只含蓄力/激光/射击，**不含** `CanShoot`，否则炮塔永久冻结转不动）；`Return` 迁移里**先判"已回原点"再判"停留时间到"**（顺序反了会永久卡 Return 回不去 Idle/Patrol）
  - 距离一律"水平距离 − 目标 `HalfRange`"再比阈值；速度类状态（Patrol/Beware/Return）用 `ModifierType.Extra` 差量修饰：进入 `(目标速度 − FinalValue)`、退出取反移除
  - **Beware 的"以看代到"（2026-10-02 加）**：结束条件三条 —— 见敌→Follow／到达 `BewareReachRadius` 或 `CanObserveBeware()` 观察位／超 `BewareTimeout` 兜底。`CanObserveBeware` = 水平距离进 `SearchObserveDistance`(追敌,4m)或 `BewareObserveDistance`(听声,10m)（**按落点来源分流**，`<=0` 关闭）**且** `DetectionModule.HasLineOfSight(落点+`_ObserveAimHeight`1.5m)` 无遮挡 **且** 连续满足 `ObserveSettleTime`(0.4s) 防抖 ⇒ 不必贴到点脚下，转入原地停留 `BewareStayDuration` 扫视后回原点
  - ⚠ 视线查询必须用 `DetectionModule.HasLineOfSight`（新增，无副作用）；**别用 `CanLook`**：protected，且命中烟幕层会改写 `TimeLastSeenTarget` 污染目标锁定。另：警惕点多为地面弹着点，射线要打到"落点上方 1.5m"否则被地面自身挡住

## 伤害管线（DamagePacket + 护甲/穿甲结算）
- 统一入口 `FpsHelper.Hit(...)` → **`IDamageable.InflictDamage(DamagePacket)`**（签名只吃**一个纯逻辑 DTO**，不再混传 `GameObject`/`Vector3`）
  - 契约：`00GameContract/Data/DamagePacket.cs` + `00GameContract/Entities/IDamageable.cs`
  - 实现：`05UnitCore/Damage/Damageable.cs` + `05UnitCore/Damage/TransferDamageable.cs`
- **护甲模型（绝地潜兵2 式）**：肢体持 **护甲等级 `ArmorLevel`(int)**，攻击方给 **穿甲等级 `packet.AP`**；结算在 `Damageable.InflictDamage`：
  `armorFactor = Clamp(1 - (armorLevel - AP) * ArmorPenaltyPerLevel, ArmorMinFactor, 1)`（`ArmorPenaltyPerLevel = 0.33f`、`ArmorMinFactor = 0.1f`）⇒ `damage *= hitExplosionResistance * armorFactor`
- **整体抗性是两层**：`Health.showArmorLists`（`List<SKVP<DamageTypeEnum,float>>`）构建 `_armorResistance` 字典作**按伤害类型的减伤乘区**（未配置 = 1），在 `TakeDamage` 里 `value * GetResistance(type)`；`ExplosionResistance` 由接口暴露、与 AP 系数相乘
- 伤害配置**两套**、都实现 `IDamageData`（`06Gameplay/Common/IDamageData.cs`）：`DamageData`（直击/爆炸瞬时，`Game/Shared/Weapon/DamageData.cs`）+ `SustainedDamageData`（持续/异常状态）
- `TransferDamageable` = 子肢体代理，`ArmorLevel`/`ExplosionResistance`/`ActorGo` 全部转发给 `Source`
- ⚠ **已废弃**：早期"每个肢体按伤害类型各自持抗性 `GetArmor(DamageTypeEnum)`"的模型 —— 照旧代码写会错
- ⚠ `.codebuddy/plans/伤害抗性迁移到Health_穿甲等级结算_b1f3efdc(未完成).md` 的 `(未完成)` 是**文件名过期**，该重构**已落地**（2026-10-01 核对）

## 寻路
- NavMesh 异步管线由 `PathRequestManager` 统一排队
- ⚠ 老坑：单位多（~40）时 `pathPending` 被误判超时 ⇒ 重试风暴 ⇒ 队列堆积、单位执行过时旧路径乱走（`agent.destination` 仍是旧目标）。**修法：只在 `pathPending=false` 之后判真正失败**（`PathInvalid` / 单点 `PathPartial`）+ 投影重试 + 10m 兜底；`RequestPath` 的 `log` 参数由 `EnemyController.SetNavDestination(isImportant)` 控制

## 玩家 / 相机
- 相机：`PlayerController_ThirdPerson` 两个机位点 (0.5,1,-4) / (0.8,0.4,-1.8) × `ThirdPersonDistanceScale`（**`abs(z)` 才是视距**）；`IsAiming` 只在 `m_WeaponSwitchState==Up` 时重估（`WeaponSwitchDelay=0.2`×2 ⇒ ~0.4s 延迟）
- ⚠ `PlayerWeaponsManager.OnWeaponSwitched(bool isSec)`：`isSec=true`（副武器）只设左手 IK，**不覆盖**主武器右手 IK（否则"替换主武器"连触发两次互相覆盖 ⇒ IK 错乱 ⇒ `leftFree/rightFree` 误判 ⇒ 允许本不该允许的双持）
- `PhoenixEagleController` 旋转乱跳：阶段切换时**不要**改 `lastPos.y`，统一在 `Update` 里按阶段 flatten y
- 第三人称武器朝向 = `FirstPersonSocket` 世界旋转 `Euler(m_CameraVerticalAngle, transform.eulerAngles.y)`（`PlayerController.LateUpdate`）+ 子弹方向由 `PlayerWeaponsManager.UpdateWeaponThirdPersonAim` 注入 `ThirdPersonAimTarget = ScreenCenterTargetPoint` 收敛到准星（瞄准恒注入；未瞄准由 `ThirdPersonAimAtViewCenter` 控）。⚠ yaw 用**角色朝向**而非 `_cameraYaw`：非瞄准移动时二者可差 180°，硬对齐会让枪口与身体撕裂、手臂 IK 崩

## 组件存活 / 回收 / 表现
- `VFXManager.Release` 只认根上的 ParticleSystem / LimitedLife；`LimitedLife` 到寿先 `InvokeEnd()` 再等 `EndDelay`
- 池化对象"本次状态"字段必须在 `OnEnable`/`OnDisable` 复位、且放在早退之前；`Invoke`/协程在宿主 `SetActive(false)` 时**不执行**
- ⚠ 池工厂创建的 GameObject 默认 `active=true` ⇒ **空闲对象必须显式 `SetActive(false)`**（否则 Hierarchy 里"反了"）
- ⚠ Unity 不能序列化接口字段 ⇒ 存 `GameObject` + 运行时 `GetComponent`；**判 MonoBehaviour 是否被使用只能搜 `.cs.meta` 的 guid**（类名搜索无效）；删脚本 `.cs` + `.meta` 成对
- 表现层 FX 三分：`FxControllerBase` / `EnemyControllerFX` / `HitDeathFX`；敌人特效 MPB 所有权在渲染槽位帧末 Flush
- ⚠ **单位注册表不变量**（2026-10-07 修崩后定）：`ActorsManager` 的 `Players/Actors/Enemys/SpecUnits` 里**绝不留已销毁对象**。**死亡 ≠ 移除**（只是倒地：`BattleManager.IsTeamWiped` 统计全队阵亡、`HealBag`/`Furniture_PlayerDown` 救援都要靠 `Players` 里的倒地者）；**只有对象被销毁才摘**（离场/换场景）⇒ 统一走 **`ActorsManager.Despawn(actor)`**（= 先 `Unregister` 再 `Tool.Destroy`，顺序固定，别自己拼两行），`Actor.OnDestroy` 里也调 `Unregister` 兜底。⚠ 命名别用 `Kill`（`Actor.Kill()` 是"致死"，走 `OnDie` → `UnitEventSub.UnitDeath`，两回事）。⚠ 接口引用判空必须用 `IsValidMono()`，**不能用 `== null` / `?.` / `??`**（后两者绕过 Unity 的假 null 重载）。漏摘的后果实测：12 处遍历方集体 `MissingReferenceException`

## 家具 / 任务 / 事件
- 家具入口 `Furniture_*.furnData` 按 Id 分发；手持范式 `Furniture_HandEquip` + `HandEquip : IEquippable`；按键 **E = Operate、X = Equip**（X = 卸载轮盘 `EquipmentUninitiatedUI`）
- 手持装备时 **按 E 的三去向**（2026-10-02）：入口 `PlayerOperationController` → `EquipController.TryDropHandEquip(bool replace)`（遍历找 `HandEquip` → `UninstallEquip`，一次一件）。① 正前方 `target` 是**按下即拾取的手持物**（`is Furniture_HandEquip && MeetTime == 0`）→ **先丢手中的、再拿面前那件**（同一帧；`replace=true` ⇒ 经 `HandEquip.SkipRestoreWeaponOnUninstall` 不切主武器、并 `UninstallEquip(equip, silent:true)` 不播"卸载"语音——紧接着会播"安装"语音，两条连播会打架）② `target.CanOperate(player)` 为真 → E 让给交互（炮位 `Furniture_Artillery` / Kei 提交点 `Furniture_KeiSubmit` 都要求"手上有物"）③ 其余（无目标 / 目标不可交互 / `target` 粘滞）→ 丢下。⚠ 判据必须用 `CanOperate` 而非只判 `target != null`：`target` 会**粘滞**在已不可交互的旧目标上。`HandEquip` 不再自己监听输入（原 `UpdateDropByOperate`/`m_WaitOperateRelease` 已删）——丢下判定在 `target.Handle()` **之前**，天然不会"拾取当帧即丢下"
- 交互链：`PlayerOperationController`（扫描 / 长按 / `IStepPress`）→ `IFurniture.Handle` → `GlobalEventSub.FurnitureOperate`；`InOperation` 只有 `FurnitureFlag.SwitchState` 家具会被置 true（消费方：`PlayerInputHandler`、`PlayerWeaponsManager.OnOperation` 切空手）
- NPC 语音家具 `Furniture_NPCChat`：持 `SoundGroup_SO`，交互 → `AudioSvc.PlaySound(soundGroup.Get(pos))`，协程 `WaitForVoiceEnd` 靠 `isPlaying` 串行化，`OnDisable` 复位
- 计时器 `TickBehaviour.Tick()` 1s；⚠ `GameRootBase<T>` 静态成员下层不能调 ⇒ 委托反转（`TimerRequest`/`ClipLoader`）或搬 `00_Core`
- 任务战备 `BattleEventSub.RequestAuthorize(id, state)` → `+= state?1:-1`（0 = 隐藏不可用）；台词只能走 `WndManager.CreatNotice`
- 静态事件总线层 = `min(发布者层, 订阅者层)`：`UnitEventSub`(05_UnitCore) / `GlobalEventSub` / `BattleEventSub`(06Gameplay)
- `CampTemplate.patrolTemplate` = `List<SKVP<string,int>>`（Key = 巡逻队名，Value = 权重），驱动 `WaveManager` 的 Patrol 构建
- 战备 SO 键序列：`DirectionEnum` Left0/Up1/Right2/Down3；`opter` 是 little-endian `uint[]`；首键分类 100% 合规、尾键（伤害类型指纹）规律未贯彻；`Y_Machine`/`Y_OilPlane`/`Y_EvacuationBeacon` 的 `opter` 为空
- 任务类型枚举 `MissionEnum` 的取值分段与"禁止中间插入"约束见 `topics/contract.md`
- 任务生命周期（2026-10-02 补）：**起** = `TaskManager.SetTask()`（选图，`GameState=Ready`）；**止** = 进 `GameStateEnum.Bridge` → `OnGameStateChange` → `ResetTask()`（`nowTask = new SelectTaskData()` + 复用 `SyncTaskState()` 发中性值）；`TaskState` 发布点共 **3** 处（`Init`/`SetTask`/`ResetTask`）
- ⚠ 回大厅后 `nowTask` 为空（= 「还没选图」的中性态）：`TaskState.Countdown` **不**在 `ResetTask` 复位（约定归 `SetTask` 的 16）；读 `MapId`(=`nowTask.mapCfg`)/`EnemyVarietyType`/`RequiredAD` 的消费点必须自带守卫（`TaskState.HasTask`、`nowTask.activeTask`），`SelectMapWnd` 只读 `TaskCfgs/MapData/Camps` 故安全

## 逻辑帧 / 时序
- 驱动 = `09Manager/Global/NetManager.cs`（`LogicFrame.Sink` 宿主）：`_accumulator += Time.deltaTime`，每够 `Constants.LoginFrame`(0.02s=50Hz) 跑一帧，单渲染帧封顶 `MaxCatchUpTicks=10`（**债务保留不丢弃**，余量后续帧还）
- ⚠⚠ 旧写法"绝对 `Time.time` 比较 + 每渲染帧最多 1 tick" ⇒ **FPS<50 时逻辑帧率 = FPS**（44fps→0.88×），按 `TickTime` 累加的**武器冷却/射速系统性变慢**（战备"首发 2.68s"漂到 3s+），且债务单调累积、帧率恢复后"快进"回放 ⇒ **一切"逻辑时间 vs 真实时间"偏差先查这里**
- `Add/Remove` 在 tick 期间**入队**、`TickOnce` 末尾 `FlushPending`（同帧 Add/Remove 互相抵消），防 `LogicTick` 内销毁 ⇒ `OnDestroy→Unregister→Remove` 改动 `list` 漏跳
- `Time.deltaTime` 与旧 `Time.time` **同域**（都随 `timeScale`）⇒ 暂停/慢动作语义不变，别换 `Time.unscaledDeltaTime`

## 存档 / 设置（`ArchivesData_SO` / `DisplayDic`）
- 唯一读入口 `ArchivesData_SO.Current.GetSetting(name)`；`ArchiveLoader.Init()` 写入 `Current`；**补齐默认设置必须同步做在 `Init()` 里**（广播/落盘可留协程）——协程的 `yield return null` 比同帧 `Awake` 晚一帧，期间读缺键就出事
- ⚠ 缺键 = 拿到 `DisplayDic` 的 `DefaultValue` 模板（`ArchivesFloat.value == ""`）⇒ `float.Parse("")` **抛 FormatException**（`?? "0"` 挡不住空串）⇒ `ArchivesFloat` 必须 `TryParse` + `InvariantCulture`，写入端 `ToString` 也要 `InvariantCulture`（当前文化写 "0.10" 在逗号小数点区域会被读成 10）
- ⚠ `DisplayDic` 索引器兜底会**把占位条目写进字典**，之后 `Synchronize` 认为"键已存在"拒绝补齐 ⇒ 已加 `[NonSerialized] _placeholderKeys`（占位可被真实值覆盖、setter/`Add` 清标记）；**插入行为保留**，因为 `VehicleWnd` 靠"索引器返回同一引用"就地改载具改装项
- 存档落点是 `Application.dataPath/../`（编辑器再 `/../`）⇒ **编辑器与打包读的是两个不同文件**（`D:\Pro\...\KivotosCraftArc.json` vs 打包根 `<exe>/KivotosCraftArc.json`）："编辑器好、打包坏"先比两个 json 的 `settingDic` 键集合

## 地形 / 渲染
- NavMesh 在 `MapRoot`；跨层只传 `GameObject[]` / 基础类型
- ToonLit 5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` = albedo 唯一入口；未进 CBUFFER = 死属性；溶解 `clip(step(dissolve,v)-dissolve+_EdgeWidth)`
