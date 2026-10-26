# 主题 · 联机 · 重复结算（"每端各跑一份"的伤害源）

> 2026-10-26 立。跟 `net-damage.md` 互补：那篇讲**伤害通道怎么走**，这篇讲**哪些东西会被每个端各算一次**。

## 一句话

现有伤害通道的口径是「**谁开枪谁结算**」：真弹只在开枪端模拟，远端只有"表现弹"（`SpawnVisualBullet` 摘掉 `FpsHelper.Hit`、换挂 `PlayImpactFxRemote`）。
凡是**各端本地生成 + 自带武器/持续伤害**的东西，都会在**每个客户端各结算一次**，再各自上报/广播 ⇒ **N 端 = N 倍伤害**。

## 判据（拿这三条去扫新东西）

1. 它是 `VfxPool.Creat` / `Instantiate` 在**本地**建的（`ImpactVfx` / `LandingPrefab` / 空投 `creatObect` / 持续效果），**没有**走 4052 生成同步；
2. 它自己会**造成伤害**（挂 `WeaponBaseController` 系 / `SustainedEffect` / 直接调 `FpsHelper.Hit`）；
3. 它上面**没有**任何"本端是否结算权威"的门（`BattleState.IsTickDamageAuthority` / `IsVehicleDamageAuthority` / `LocalDamageGate`）。

三条全中 ⇒ 就是这篇说的问题。

## 已确认实例 ①：Strafe 榴弹的 ImpactVfx（2026-10-26）

- `Weapons/Grenade/Strafe.prefab:160` 的 `ImpactVfx` = `Assets/Resources/Prefabs/Grenade/Grenade_Strafe.prefab`
- 该预制体根上是 **`WeaponTemporaryController`**(guid `1fefde50582945046a4ddc355073c6d6`) + `LimitedLife` 4s；`Damages[0]` = `DamageDirect 50` / `DirectAP 0` / 5 个 FirePoint
- `WeaponTemporaryController.LogicTick`(:33-38) 每逻辑帧 `HandleShootInputs(true,true,false)`（模拟按住扳机）⇒ 装置持续开**真子弹**
- 两端都会生成它：本端 `FpsHelper.Hit` → `PlayImpactFx`（`FpsHelper_Hit.cs:448`）；远端表现弹 `PlayImpactFxRemote` ← `WeaponBaseController.cs:434-439`
- 2 人局：H 副本 = 自己 50 + M 上报 50；M 副本 = 自己 50 + H 广播 50 ⇒ **两边都 2 倍**（血量两端仍一致，只表现为"死得快"）
- 附带：`WeaponTemporaryController.LogicInit` 里 `Owner = gameObject` ⇒ 起源 = 装置自身、无 NetId ⇒ 击杀归属/统计两端都解析不到

## 已确认实例 ②：空投装置（Track_120MM 为代表）

- `GameData/Airdrop/ADSO_R_120MM.asset`：ID 103、`deliveryType: 1`（`AirdropDeliveryEnum.Bomb`）、arrive 8s / sustain 16s、`creatObect` = `Prefabs/Airdrop/Track_120MM.prefab`
- Track_120MM 挂的**就是同一个** `WeaponTemporaryController`；数据 = `DamageDirect 550` / `DamageExplosion 200` / `DirectAP 7` / `ExplosionAP 5` / `demolishValue 40` / `DestructeRadius 3` / `ShockwaveRadius 11`
- `VFXAirdropEffect` 的 5 条生成路径全是**本端 `Instantiate`**：`UpdatePod:449`、`PodHit:532`、`UpdateBomb:592`、`StartJet:628`、`StartMedivac:672`；远端复现 = `NetFriendBridge.cs:502` → `BattleManager.ReleaseAirdrop` → `TmpAirdrop` ⇒ **每端一台自动炮**
- ⚠ 更隐蔽：`weapon.RandomSeed = AirdropWeaponSeed(ID)`（`VFXAirdropEffect.cs:573-596`）让两端弹道**完全一致** ⇒ 看起来"同步得很好"，只是血掉一倍。原注释"轨道轰炸这类各端各自执行一次"就是根因自述
- 影响面：挂了 `WeaponTemporaryController` 的预制体共 **20 个** —— 17 个 `Prefabs/Airdrop/*`（`Track_120MM/380MM/Gatling/Railgun/Laser/Airburst/Gas/Smoke/Illuminator`、`Eagle Airstrike/Gatling/Rocket/Smoke/Gas/500KG/Cluster/Cluster_Explode`）+ `Grenade_Strafe` + `Dec_ColossusTower` + `Fx_Noa_Ghost_NS`

## 为什么架构没兜住

- `PlayImpactFx` 的契约写着"只播表现、**不结算任何伤害**"，但 `VfxPool.Creat(ImpactVfx)` 之后没人拦住"这个表现物自己开火"
- `SustainedEffect` 同理：tick 绕过 `OnHit` 直接调 `FpsHelper.Hit`，只有 `IsVisualBullet` 分流（碰撞事件那条），**没有权威门**
- 空投/战备这块的口径是"各端各自重放一遍、只保证种子一致"，**只统一了表现，没统一结算**

## 待拍板的修法

### A. 权威端开火（最小改动）
`WeaponTemporaryController` 加 `[SerializeField] bool OnlyAuthorityTick`，`LogicTick` 里 `if (OnlyAuthorityTick && !BattleState.IsTickDamageAuthority) return;`
- 复用现成权威门（`04Data/BattleState.cs:48`；写入方 `WaveManager.Awake`，房主掉线由 `EnemyNetBridge.OnHostLost` 自动恢复）
- 代价：成员端看不到弹幕/爆炸（装置模型与音效还在）

### B. 非权威端打"表现弹"（推荐）
给 `WeaponBaseController` 加"仅表现"开关，`HandleShoot` 走现成的 `SpawnVisualBullet`（只摘伤害、保留 `PlayImpactFxRemote`）
- 挂载点集中在 `VFXAirdropEffect` 已有的 5 处 `weapon.Origin/RandomSeed` 赋值旁（:453/:535/:595/:631/:697）⇒ 一处判定覆盖所有 Bomb/Jet/Medivac 装置
- 表现与伤害都保住，但要动武器核心路径

### C. 走 4052 生成同步（最重）
装置变真实体、房主生成成员复刻。只有以后需要"装置能被摧毁/被击毁"时才值得。

## 验证手段

- 两端开 `NetSyncLog`，看「命中上报·发」/「伤害下发·发」是否**成对出现两遍**（`EnemyNetBridge.OnLocalHit/OnLocalDamaged`）
- 或临时在 `WeaponTemporaryController.LogicTick` 打一条带本端身份的日志，观察两端是否都在开火

## 相关

- 伤害通道 / 4025 上报 / 4040 广播 / tick 权威门 → `net-damage.md`
- 生成同步 4052 / 生成器复刻 → `net-spawn.md`
- 手雷黏性（同一天改的，已完成）→ `2026-10-26.md`
