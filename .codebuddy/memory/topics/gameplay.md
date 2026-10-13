# 主题 · 战斗 / AI / 伤害 / 寻路 / 玩家 / 表现

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。同伴：`gameplay-world.md`（家具任务 / 时序 / 存档 / 地形）。
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
  - 联机侧字段与远端重建见 `net-battle.md`（`DamageMsg.CausedNetId`）。

## 战备（AirdropController）
- 表**各端各自一份**：`useAd` 只装本机玩家那份（`Self.airdrop` + `RequiredADOf(自己角色)`）；别人的呼叫只以"结果"过网（4027/4028 → `NetFriendBridge.HandleAirdropCall` → `ReleaseAirdrop` 复现，`owner = null` ⇒ 不计呼叫次数、不回声）。
- ⭐ **团队冷却 `teamCool`（2026-10-13 实装）**：任一玩家释放 ⇒ **每台机器把自己那份同名战备**置 Cool（`time = item.cool`，与 `coolGroup` 同口径）。两个触发点：① 本机释放 = `OnAirdropStateChange(Arrive)` 里 `ApplyTeamCool(id, except: data)`（那一发自己走 Arrive→Sustain→Cool，`cool - 持续 - 部署` ⇒ 结束时刻正好对齐）；② 队友释放 = 4028 接收钩子 `HandleAirdropCall` 里 `bm.ADCont?.ApplyTeamCool(id)`。判据 `cfg.teamCool` 在 `ApplyTeamCool` 内查；`Unavailable`（次数耗尽）不动（团灭判负读它）。
- ⚠ `isDirect`（增援 HealBag）**不走信标、也不发 4027/4028** ⇒ `teamCool` 对它不生效（2026-10-13 用户拍板：保持次数制 + "有层数不读条"不改）。另一种共 CD 是 `coolGroup`（同组条目一起 Cool，只在本机 useAd 内生效）。

## 寻路
- NavMesh 异步管线由 `PathRequestManager` 统一排队
- ⚠ 老坑：单位多（~40）时 `pathPending` 被误判超时 ⇒ 重试风暴 ⇒ 队列堆积、单位执行过时旧路径乱走（`agent.destination` 仍是旧目标）。**修法：只在 `pathPending=false` 之后判真正失败**（`PathInvalid`/单点 `PathPartial`）+ 投影重试 + 10m 兜底；`RequestPath` 的 `log` 参数由 `EnemyController.SetNavDestination(isImportant)` 控制

## 玩家 / 相机
- 相机：`PlayerController_ThirdPerson` 两个机位点 (0.5,1,-4)/(0.8,0.4,-1.8) × `ThirdPersonDistanceScale`（**`abs(z)` 才是视距**）；`IsAiming` 只在 `m_WeaponSwitchState==Up` 时重估（~0.4s 延迟）
- ⚠ `PlayerWeaponsManager.OnWeaponSwitched(bool isSec)`：`isSec=true`（副武器）只设左手 IK，**不覆盖**主武器右手 IK（否则"替换主武器"连触发两次互相覆盖 ⇒ IK 错乱 ⇒ `leftFree/rightFree` 误判 ⇒ 允许本不该允许的双持）
- `PhoenixEagleController` 旋转乱跳：阶段切换时**不要**改 `lastPos.y`，统一在 `Update` 里按阶段 flatten y
- 第三人称武器朝向 = `FirstPersonSocket` 世界旋转 `Euler(m_CameraVerticalAngle, transform.eulerAngles.y)`（`PlayerController.LateUpdate`）+ 子弹方向由 `PlayerWeaponsManager.UpdateWeaponThirdPersonAim` 注入 `ThirdPersonAimTarget`。⚠ yaw 用**角色朝向**而非 `_cameraYaw`（非瞄准移动时二者可差 180°，硬对齐会让枪口与身体撕裂、手臂 IK 崩）
- 玩家欧帕兹背包 `PlayerOOPartInventory`（每种上限 `MaxPerType=5`）：`CurrentCount/IsFull/TryAdd/Remove/GetCount/GetAll`；⚠ 远端盟友那份是 `FriendOOPartInventory`（幽灵背包，见 `net-battle.md`），容器类家具**面向 `IOOPartInventory`** 取背包
- ⚠⭐ **"被压住自动脱困"的排除条件**（`BaseSelfMoveableController.IsPenetrating`/`HaveObstacle`）：`Overlap*` 系列必须排除**自身 + 全部子物体**（`c.transform.IsChildOf(transform)`），只排根上的 `CharacterController` 会恒判"被压住" ⇒ 每 ~1.3s 把单位往上瞬移 `height*1.2` 米（Exosuit 高 4.5 ⇒ 抬 4m）⇒ **"落地→悬浮→又掉下来"死循环**（2026-10-13 修，机甲/玩家都会中；`LayerDefinition.MoveableLayers` 是 Everything，放大了它）。取证口：Console 筛 `自动挣脱`、或 `execute_code` 复算 `OverlapCapsuleNonAlloc` 打印每个命中的 `isSelfChild`。

## 组件存活 / 回收 / 表现
- `VFXManager.Release` 只认根上的 `ParticleSystem`/`LimitedLife`；`LimitedLife` 到寿先 `InvokeEnd()` 再等 `EndDelay`
- 池化对象"本次状态"字段必须在 `OnEnable`/`OnDisable` 复位、且放在早退之前；`Invoke`/协程在宿主 `SetActive(false)` 时**不执行**
- ⚠ 池工厂创建的 GameObject 默认 `active=true` ⇒ **空闲对象必须显式 `SetActive(false)`**
- ⚠ Unity 不能序列化接口字段 ⇒ 存 `GameObject` + 运行时 `GetComponent`；**判 MonoBehaviour 是否被使用只能搜 `.cs.meta` 的 guid**（类名搜索无效）；删脚本 `.cs` 与 `.meta` 成对
- 表现层 FX 三分：`FxControllerBase`/`EnemyControllerFX`/`HitDeathFX`；敌人特效 MPB 所有权在渲染槽位帧末 Flush
- ⚠ **单位注册表不变量**：`ActorsManager` 的 `Players/Actors/Enemys/SpecUnits` 里**绝不留已销毁对象**。**死亡 ≠ 移除**（只是倒地：`BattleManager.IsTeamWiped` 统计全队阵亡、`HealBag`/`Furniture_PlayerDown` 救援都要靠 `Players` 里的倒地者）；**只有对象被销毁才摘** ⇒ 统一走 **`ActorsManager.Despawn(actor)`**（先 `Unregister` 再 `Tool.Destroy`，顺序固定），`Actor.OnDestroy` 也调 `Unregister` 兜底。⚠ 命名别用 `Kill`（`Actor.Kill()` 是"致死"）。⚠ 接口引用判空必须用 `IsValidMono()`，**不能用 `== null`/`?.`/`??`**。漏摘后果实测：12 处遍历方集体 `MissingReferenceException`
