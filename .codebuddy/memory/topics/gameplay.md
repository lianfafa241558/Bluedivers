# 主题 · 玩法 / AI / 战斗 / 表现 / 家具任务 / 地形渲染

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。

## 战斗 / AI
- 组件间避免 GetComponent 互取；AI = `EnemyController` + `EnemyMobile` + `DetectionModule` 组合
- team：玩家 **0**、敌人 **2**；`HalfRange>=1` = 大型单位；`HalfHeight=0` = 不过滤
- `PatrolPos` 到达即自毁（`HomePoint` 才是待命）；巡逻队只能经 `BattleManager.CreatPatrol`；`Actor.IsFixed` = 不会因不动被删
- 枪声警惕：`BewarePoint.HasValue` 且点距 `<= Max(Hearing, Detection)`；取点后 `ClearBeware()`；冷却 10s / 半径 10m 收在 `ShouldInvestigateBeware()`
- 伤害统一入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸→冲击波→地形→警告→特效；`isDirect` 绕开爆炸抗性/遮挡；噪声走 `NoiseData` + `OnNoise`（枪口 20、弹着点 8，消费在 `DetectionModule.OnNoise`）
- 波次 `BattleManager.CreatWave(WaveCreateParams)`；`centerGetter` 只影响未落点单位；`extraWave` 跳过冷却；`KaiserWave` 在 `WaveManager`（鹰挂 6 单位，`PhoenixEagleController.onWait/waitTime` 已 public）
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

## 组件存活 / 回收 / 表现
- `VFXManager.Release` 只认根上的 ParticleSystem / LimitedLife；`LimitedLife` 到寿先 `InvokeEnd()` 再等 `EndDelay`
- 池化对象"本次状态"字段必须在 `OnEnable`/`OnDisable` 复位、且放在早退之前；`Invoke`/协程在宿主 `SetActive(false)` 时**不执行**
- ⚠ 池工厂创建的 GameObject 默认 `active=true` ⇒ **空闲对象必须显式 `SetActive(false)`**（否则 Hierarchy 里"反了"）
- ⚠ Unity 不能序列化接口字段 ⇒ 存 `GameObject` + 运行时 `GetComponent`；**判 MonoBehaviour 是否被使用只能搜 `.cs.meta` 的 guid**（类名搜索无效）；删脚本 `.cs` + `.meta` 成对
- 表现层 FX 三分：`FxControllerBase` / `EnemyControllerFX` / `HitDeathFX`；敌人特效 MPB 所有权在渲染槽位帧末 Flush

## 家具 / 任务 / 事件
- 家具入口 `Furniture_*.furnData` 按 Id 分发；手持范式 `Furniture_HandEquip` + `HandEquip : IEquippable`；按键 **E = Operate、X = Equip**
- 交互链：`PlayerOperationController`（扫描 / 长按 / `IStepPress`）→ `IFurniture.Handle` → `GlobalEventSub.FurnitureOperate`；`InOperation` 只有 `FurnitureFlag.SwitchState` 家具会被置 true（消费方：`PlayerInputHandler`、`PlayerWeaponsManager.OnOperation` 切空手）
- NPC 语音家具 `Furniture_NPCChat`：持 `SoundGroup_SO`，交互 → `AudioSvc.PlaySound(soundGroup.Get(pos))`，协程 `WaitForVoiceEnd` 靠 `isPlaying` 串行化，`OnDisable` 复位
- 计时器 `TickBehaviour.Tick()` 1s；⚠ `GameRootBase<T>` 静态成员下层不能调 ⇒ 委托反转（`TimerRequest`/`ClipLoader`）或搬 `00_Core`
- 任务战备 `BattleEventSub.RequestAuthorize(id, state)` → `+= state?1:-1`（0 = 隐藏不可用）；台词只能走 `WndManager.CreatNotice`
- 静态事件总线层 = `min(发布者层, 订阅者层)`：`UnitEventSub`(05_UnitCore) / `GlobalEventSub` / `BattleEventSub`(06Gameplay)
- `CampTemplate.patrolTemplate` = `List<SKVP<string,int>>`（Key = 巡逻队名，Value = 权重），驱动 `WaveManager` 的 Patrol 构建
- 战备 SO 键序列：`DirectionEnum` Left0/Up1/Right2/Down3；`opter` 是 little-endian `uint[]`；首键分类 100% 合规、尾键（伤害类型指纹）规律未贯彻；`Y_Machine`/`Y_OilPlane`/`Y_EvacuationBeacon` 的 `opter` 为空
- 任务类型枚举 `MissionEnum` 的取值分段与"禁止中间插入"约束见 `topics/contract.md`

## 地形 / 渲染
- NavMesh 在 `MapRoot`；跨层只传 `GameObject[]` / 基础类型
- ToonLit 5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` = albedo 唯一入口；未进 CBUFFER = 死属性；溶解 `clip(step(dissolve,v)-dissolve+_EdgeWidth)`
