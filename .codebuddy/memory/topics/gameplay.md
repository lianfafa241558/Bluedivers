# 主题 · 战斗 / AI / 伤害管线 / 组件存活

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。
> 2026-10-18 拆出：玩家/相机/武器子弹/战备/寻路 → `gameplay-player.md`；家具任务时序地形 → `gameplay-world.md`。
> ⚠ 原文（含每条实测细节与行号）已下沉：`archive/topics-snapshot-20261011/gameplay.md`。

## 战斗 / AI
- 组件间避免 `GetComponent` 互取；AI = `EnemyController` + `EnemyMobile` + `DetectionModule` 组合
- team：玩家 **0**、敌人 **2**；`HalfRange>=1` = 大型单位；`HalfHeight=0` = 不过滤
- `PatrolPos` 到达即自毁（`HomePoint` 才是待命）；巡逻队只能经 `BattleManager.CreatPatrol`；`Actor.IsFixed` = 不会因不动被删
- 枪声警惕：`BewarePoint.HasValue` 且点距 `<= Max(Hearing, Detection)`；取点后 `ClearBeware()`；冷却 10s / 半径 10m 收在 `ShouldInvestigateBeware()`
- 伤害统一入口 `FpsHelper.Hit(ProjectileHitData)`；噪声走 `NoiseData` + `OnNoise`（枪口 20、弹着点 8，消费在 `DetectionModule.OnNoise`）
- 波次 `BattleManager.CreatWave(WaveCreateParams)`；`centerGetter` 只影响未落点单位；`extraWave` 跳过冷却
- 波次类层级：`09Manager/Battle/WaveBase.cs` = `abstract : I_TickClass, IDisposable`，持 `Tick()` 模板（`TickStart` 默认 -4 进 Ongoing／`TickOngoing` **abstract**／`TickNearEnd`=`TryEnterNearEnd(0)`）+ `Trans` + 工具方法（`RegisterUnit/SetUnitHome/SetBehaviourEnabled/SetUnitAnimator/EnableUnitColliders/RandomYaw/SnapToNavMesh/GetDropPoint(pad)`）。子类 `RobotWave`（鹰群空投，到达后 3s 投放）/ `ZergWave`（兵营空投 + `RedeployPods`，`centerGetter` 偏离 30m 重部署）
- 上帝类已 partial 拆：`Tool` / `FpsHelper` 各 `_` 系列；`Health.cs` + `Health_AboState.cs`
- ⭐ **敌人状态机 `EnemyMobile`**（`06Gameplay/AI/StateMachine/`；**类顶 XML summary 是权威逻辑说明，改行为前先读它**）：框架 = `AIInputBaseController<T>`（内核 `StateMachineCore<T>`：状态表/迁移/守卫）→ `AIInputUnitController<T>`（+ `turrets` 炮塔瞄准）；状态 `Idle/Patrol/Follow/Attack/Beware/Return/Death`，异常态在同目录 `EnemyMobile_AboState.cs`
  - 每帧守卫链（命中即 return）：`BirthComplete` → `Death` → 弱点僵直清除 → `IsMoveLocked`(Freeze/Vertigo 停导航) → Hacker 锁同队 → `IsForcedAttack`(Toxicity 乱走乱攻) → Terror 逃离 → 查表执行状态行为
  - ⚠ `OnDetectedTarget` **只在** Idle/Patrol/Beware/Return 时重置 `m_TimeStartedDetection`；已在 Follow/Attack 时重置 ⇒ 开火延迟反复重算 ⇒ `mustStop` 分支跳开火 ⇒ 武器卡 `InShoots`（表现"开火被打断/换弹完才开火"）
  - ⚠ `AttackStop` 站桩用 `IsFiringNow`（只含蓄力/激光/射击，**不含** `CanShoot`，否则炮塔永久冻结转不动）；`Return` 迁移里**先判"已回原点"再判"停留时间到"**（顺序反了会永久卡 Return）
  - 距离一律"水平距离 − 目标 `HalfRange`"再比阈值；速度类状态（Patrol/Beware/Return）用 `ModifierType.Extra` 差量修饰：进入 `(目标速度 − FinalValue)`、退出取反移除
  - **Beware 的"以看代到"**：结束三条 —— 见敌→Follow／到达 `BewareReachRadius` 或 `CanObserveBeware()` 观察位／超 `BewareTimeout` 兜底。`CanObserveBeware` = 水平距离进 `SearchObserveDistance`(追敌 4m) 或 `BewareObserveDistance`(听声 10m) **且** `DetectionModule.HasLineOfSight(落点 + 1.5m)` 无遮挡 **且** 连续满足 `ObserveSettleTime`(0.4s) 防抖
  - ⚠ 视线查询必须用 `DetectionModule.HasLineOfSight`（无副作用）；**别用 `CanLook`**（protected，且命中烟幕层会改写 `TimeLastSeenTarget` 污染目标锁定）。警惕点多为地面弹着点，射线要打到"落点上方 1.5m"否则被地面自身挡住
- ⭐ **`DetectionModule` = 归属自取 + 示警事件化**（2026-10-16 落地）：
  ① `Start` 自取 `m_Actor = GetComponentInParent<Actor>(true)`（**必须 includeInactive**；`SetActor` 保留但不该再用），`Tick` 开头 `!m_Actor.IsValidMono() → return true` 兜底；`EnemyController`/`StateMachineFrame` 的注入调用已删。⚠ 扫 prefab 资产时别信不带 includeInactive 的结果（资产不在场景 ⇒ `activeInHierarchy` 恒假 ⇒ 会全判失败）。
  ② 扩散一律 **模块→模块**（同 `OnDamaged` 口径）：`Beware(spread)` 找同队 `DetectionModule` 直呼（跳过自己、接受方 spread=false）；**两条扩散各限流 `SpreadInterval=2s`**（听声的门 `noise<m_BewareNoise` 放等响度过、受击完全没有门 ⇒ 不限流就是每声/每发一次范围查询；受击限流期内**仍单独补一次** `OnDamagedDiffuse`，否则挨打的人自己都不还击）；**`I_AIController.Beware` 那套转发已无人调用**（基类空实现、只有 `EnemyController` 覆写 ⇒ `SimpleTurret`/`GuardDog` 会静默丢）。
  ②b `RespondWarning`（Inspector「响应警告」，默认 true）= `Beware` 的**总闸**：交战中 `EnemyMobile`/`EnemyTurret` 在 `OnDetectedTarget` 置 false、`OnLostTarget` 置 true。⚠ **`SetTargetActor(null)` 里也必须恢复 true** —— 异常态(Freeze/Vertigo/Terror/中毒/Hacker)清目标是直接调的、不走 `OnLostTarget`，只挂回调会让那只单位**永久失聪**。受击警告(`OnDamaged`)与视觉(`Tick`)不受它影响。
  ③ 听觉扩散已开：`OnNoise` → `Beware(..., spread:true)`，只扩一层、半径 = 本单位 `AlertRange`（83 个模块里 57 个 AlertRange>0，取值 8~70）。
  ④ 示警事件只剩 **`DetectionModule.onBeware(PEVector3,PEInt)`**；`I_AIController.OnBeware` 那层转发已删（没人消费、换控制器家族就断）。订阅者：`EnemyTurret`（转头看改事件驱动，看哪/看多久自己持 `_bewareLookPoint`）；`EnemyMobile` 仍**轮询** `BewarePoint` —— 它的判据(范围/冷却/异常态冻结)全在逐帧迁移表里，事件回调会绕过这些守卫，别改。
  ⑤ ⚠ `AIInputBaseController.OnDestroy` 已改 `protected virtual`：子类从前写 `private void OnDestroy()` 会**遮住**基类 4 条控制器退订（`EnemyMobile`/`SpecUnitKei` 已修）；`GuardDog` 同名遮住 `StateMachineFrame.OnDestroy`（含 `Uninit()`）**尚未动**，改那族时注意。

## 伤害管线（DamagePacket + 护甲/穿甲结算）
- 统一入口 `FpsHelper.Hit(...)` → **`IDamageable.InflictDamage(DamagePacket)`**（签名只吃**一个纯逻辑 DTO**）。契约 `00GameContract/Data/DamagePacket.cs` + `Entities/IDamageable.cs`；实现 `05UnitCore/Damage/{Damageable,TransferDamageable}.cs`
- **护甲模型（绝地潜兵 2 式）**：肢体持 **护甲等级 `ArmorLevel`(int)**，攻击方给 **穿甲等级 `packet.AP`**；结算在 `Damageable.InflictDamage`：
  `armorFactor = Clamp(1 - (armorLevel - AP) * ArmorPenaltyPerLevel, ArmorMinFactor, 1)`（`0.33f` / `0.1f`）⇒ `damage *= hitExplosionResistance * armorFactor`
- **整体抗性是两层**：`Health.showArmorLists`（`List<SKVP<DamageTypeEnum,float>>`）→ `_armorResistance` 字典作**按伤害类型的减伤乘区**（未配置 = 1），在 `TakeDamage` 里 `value * GetResistance(type)`；`ExplosionResistance` 由接口暴露、与 AP 系数相乘
- 伤害配置两套、都实现 `IDamageData`（`06Gameplay/Common/IDamageData.cs`）：`DamageData`（直击/爆炸瞬时）+ `SustainedDamageData`（持续/异常状态）
- `TransferDamageable` = 子肢体代理，`ArmorLevel`/`ExplosionResistance`/`ActorGo` 全转发给 `Source`
- ⚠ **已废弃**：早期"每个肢体按伤害类型各自持抗性 `GetArmor(DamageTypeEnum)`"的模型 —— 照旧代码写会错
- ⚠ `.codebuddy/plans/伤害抗性迁移到Health_穿甲等级结算_b1f3efdc(未完成).md` 的 `(未完成)` 是**文件名过期**，重构**已落地**
- ⭐ **来源分两个口径（2026-10-12 拆分；动任何伤害代码前先选对）**：
  `DamagePacket.DamageOrigin` = **起源**（谁该记这笔账：武器持有者 / 召唤物呼叫者）、`DamagePacket.DamageCaused` = **直接来源**
  （屏幕上该显示谁：玩家本人的枪 / **炮台本身** / 地雷 / 补给舱；`null` ⇒ `Health` 内部回落 origin）。
  - 生产者**一处收口**在 `ProjectileBase.BuildHitData`：`soure = Owner`（起源）、`caused = WeaponBase.GetComponentInParent<Actor>()?.gameObject ?? Owner`；
    地雷 / 空投舱在各自构造点显式 `caused = gameObject`（拍板：无 Actor 的物件显示成它自己）。
  - **读 origin**：减伤/友伤/全队强化、`ArmorDestruction`、命中次数、击杀归属、`Actor.OnDie → UnitKill`。
  - **读 caused**：`Health.OnHit` 第 1 参、`Health.OnDamaged` 的第 2 参（受击方向/闪变）、`DetectionModule.OnDamaged`（阵营过滤 + 受击改目标）、
    `EnemyController._OnDamaged` 的 AI 分支、`Furniture_PlayerDown`（"被谁击倒"播报）、`PlayerSpeechManager`、`UnitEventBus.UnitHit/UnitDamaged`。
  - 事件签名：`Health.OnDie(caused, origin)`、`Health.OnDamaged(value, caused, origin, collider, noSource)`；
    `IHealth.TakeDamage(..., demolishValue, damageCaused = null)`（第 3 参是 origin）。
    ⚠ `OnDamaged` 有 5 参 ⇒ **只能用 `System.Action<...>`**（`UnityAction` 最多 4 个泛型参数，5 参不存在）。
  - ⚠ `caused` 可能是**无 `Actor` 的物件**（地雷/空投舱）⇒ 每处 `caused.GetComponent<Actor>()` 都要判空；
    `EnemyController._OnDamaged` 的口径 = "取不到可用单位身份就回落 origin 做阵营判定"（保持"地雷炸了怪、怪照样被激怒"）。
  - 联机侧字段与远端重建见 `net-damage.md`（`DamageMsg.CausedNetId`）。

## 组件存活 / 回收 / 表现
- `VFXManager.Release` 只认根上的 `ParticleSystem`/`LimitedLife`；`LimitedLife` 到寿先 `InvokeEnd()` 再等 `EndDelay`
- 池化对象"本次状态"字段必须在 `OnEnable`/`OnDisable` 复位、且放在早退之前；`Invoke`/协程在宿主 `SetActive(false)` 时**不执行**
- ⚠ 池工厂创建的 GameObject 默认 `active=true` ⇒ **空闲对象必须显式 `SetActive(false)`**
- ⚠ Unity 不能序列化接口字段 ⇒ 存 `GameObject` + 运行时 `GetComponent`；**判 MonoBehaviour 是否被使用只能搜 `.cs.meta` 的 guid**（类名搜索无效）；删脚本 `.cs` 与 `.meta` 成对
- 表现层 FX 三分：`FxControllerBase`/`EnemyControllerFX`/`HitDeathFX`；敌人特效 MPB 所有权在渲染槽位帧末 Flush
- ⚠ **单位注册表不变量**：`ActorsManager` 的 `Players/Actors/Enemys/SpecUnits` 里**绝不留已销毁对象**。**死亡 ≠ 移除**（只是倒地：`BattleManager.IsTeamWiped` 统计全队阵亡、`HealBag`/`Furniture_PlayerDown` 救援都要靠 `Players` 里的倒地者）；**只有对象被销毁才摘** ⇒ 统一走 **`ActorsManager.Despawn(actor)`**（先 `Unregister` 再 `Tool.Destroy`，顺序固定），`Actor.OnDestroy` 也调 `Unregister` 兜底。⚠ 命名别用 `Kill`（`Actor.Kill()` 是"致死"）。⚠ 接口引用判空必须用 `IsValidMono()`，**不能用 `== null`/`?.`/`??`**。漏摘后果实测：12 处遍历方集体 `MissingReferenceException`
