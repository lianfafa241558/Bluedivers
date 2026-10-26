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

## 已实施：方案 B（2026-10-26）

- `WeaponBaseController` 新增 `protected virtual bool IsShotVisualOnly => false`；`ShootFromMuzzle` 开头分支：为真则逐颗 `SpawnVisualBullet(muzzle, GetShotDirectionWithinSpread(muzzle))` + `ShootFlash` 后 return（跳过真弹与 `SpawnLandingMarker`）。音效 / `OnShoot` / `EmitFireNoise` / 热量仍留在 `HandleShoot` ⇒ 成员端"看得见打得响、不扣血"。
- `WeaponTemporaryController` 覆写 `=> !FPSGame.Data.BattleState.IsTickDamageAuthority`（房主/单机真开火，伤害由它结算后广播；房主掉线 `EnemyNetBridge.OnHostLost` 置回 true ⇒ 剩余端自动接管）。
- 覆盖 **23 个预制体**（20 个 Temp + 3 个 MineLauncher 继承即生效），**未改任何 prefab、无新增序列化字段**。
- ⚠ 刻意不在基类统一判定：玩家自己的枪在成员端必须真开火（4025 上报），一律门掉会永远打不出伤害。
- 验证：`offline_compile.py 06_Gameplay` 0 error；两个文件 BOM OK。

### 其他备选（已否）
- **A** 权威端才 tick（`LogicTick` 直接 return）：改动更小，但成员端连弹幕都没有。已否。
- **C** 装置改 4052 生成同步的真实体：只有以后需要"装置能被摧毁/被击毁"时才值得。

## B 之后仍会重复结算的 ImpactVfx（2026-10-26 扫描）

扫 23 个装置引用的 26 条 `ImpactVfx`（脚本 `.codebuddy/plans/scan_impactvfx.py`），**5 条自带伤害**：

| 装置 | ImpactVfx | 自带 |
|---|---|---|
| `MineLauncher` | `Airdrop/Sub/Mine.prefab` | `DeployableMine` |
| `MineLauncher_Fire` / `_Gas` | 同上（`Mine_Fire`/`Mine_Gas` 是 `Mine.prefab` 的 **variant**） | `DeployableMine` |
| `Eagle Gas` / `Track_Gas` / `MineLauncher_Gas` | `VFX/Smoke/Vfx_GasSmoke_*` | `SustainedEffect` |
| `Eagle_Cluster` | `Sub/Eagle Cluster_Explode` | `WeaponTemporaryController`（**已被本次开关覆盖**） |

- 地雷链：MineLauncher 打 `Mine_Projectile`（`ProjectileStandard`，不是 `ProjectileGrenade`）→ 命中 `PlayImpactFx` 生成 ImpactVfx=真地雷（`DeployableMine`+`Actor`+`HealthOther`+`LimitedLife`）→ `SetOrigin` 部署 → 2s 后 `TryHit` 引爆 → `DoExplosion` 本地 `FpsHelper.Hit`。表现弹**照样**生成这颗真地雷 ⇒ 伤害仍两端各一份。
- ✅ **已修（2026-10-26，逐目标门，不是整段门）**：`ProjectileHitData` 加 `bool localEffectDamage`；`SustainedEffect.ApplyEffect` 与 `DeployableMine.DoExplosion` 把它置 true；落点在 `FpsHelper.Hit` 爆炸循环：
  `if (hitData.localEffectDamage && !BattleState.ShouldApplyTickDamage(item.IsPlayerLike)) continue;`（`Damageable.IsPlayerLike` 由 private 改 public，判据只此一处）。
  ⇒ 表现（触发/预警/回池/命中特效/音效）两端照跑，**只有伤害**被分流：非权威端只对玩家/盟友落地，其余目标等房主广播。
- ✅ **2026-10-27 收口：门只剩一处**。三层链路：
  ① 事件级 `ProjectileHitData.localEffectDamage` / `KineticAreaHitData.localEffectDamage`（生产者：`SustainedEffect` / `DeployableMine` / `AirdropPod` / `TrampleEffect`）；
  ② 包级 `DamagePacket.LocalEffect`（`FpsHelper` 4 处构造点透传：直击 / 爆炸 / 范围动能 / 荆棘反伤）；
  ③ 门 = `Damageable.InflictDamage` 的 `if (packet.LocalEffect && !BattleState.ShouldApplyTickDamage(IsPlayerLike)) return;`（放在 `LocalDamageGate` 之后）。
  收益：**直击分支以前根本没有门**（`AirdropPod` 砸中单位的直击伤害一直两端各算），现在自动覆盖；以后新增伤害管线只要构造 packet 就必然过门。
- ✅ **荆棘反伤改房主权威**：`TryThornArmorReflect` 的 `thornPacket` 标 `LocalEffect = true` ⇒ 敌人近战反伤只由房主落地并广播；打到自己玩家/盟友时本机照常算（`IsPlayerLike` 例外）。
- ⚠ **为什么不能整段门掉（关键）**：盟友实例在别人机器上**没有 `Damageable`**（`RoomMsg.cs:420` 自述）⇒ 房主替成员玩家扣不了血；若成员端整段跳过，自己的玩家站在火里/踩地雷会**完全不掉血**。所以门必须逐目标、且玩家例外。
- ✅ **同坑第二条管线也补了（2026-10-27）**：`TrampleEffect` 不走 `Hit`，走 `FpsHelper.HitAreaKinetic` ⇒ 给 `KineticAreaHitData` 加同样的 `localEffectDamage`，`HitAreaKinetic` 的逐单位循环里做同一道判断。挂载点：`Exosuit`（机甲）/ `Vox_Enemy` / `Dec_Bane_Boss` / `Dec_Colossus` / `Dec_Ultralisk` / `TrainEngineSam`。
- ✅ **击杀归属/统计修了（2026-10-27）**：`WeaponBaseController.Origin` 原来是自动属性、**全仓只写不读**（`VFXAirdropEffect` 5 处写呼叫者，真正参与归属的却是 `Owner`）⇒ 空投装置起源恒等于"没有 NetId 的装置自身"。现在 `Origin` 的 setter 顺手同步给 `Owner`（仅当 Owner 未设 / 等于自身时），玩家武器与炮塔那种已显式赋 Owner 的不受影响。

## 空投支援单位（14 个 `WeaponEnemyController`，2026-10-27）

- 与 14 个战备 SO 一一对应（全是 `creatObect`、走 `VFXAirdropEffect` 本地 `Instantiate`），但**分三类**：
  ① **9 个敌人型炮台**（`Turret_*` + `LampTower`）：`Actor`+`HealthEnemy`+`Damageable`+`EnemyController`+`EnemyTurret`+`DetectionModuleTurret`；
  ② **4 个 `Healdrone_*`**：基类 = `GuardDog`(IEquippable)+`Furniture_Equip`+`Actor`+`DetectionModuleTurret` —— **无 EnemyController/Damageable/Health**，是跟随持有者的无人机；
  ③ **`RocketBag`**：`BaseObject`+`WeaponBag`+`Furniture_Equip` —— 背包武器，靠装备系统上路。
- 开火无门：`EnemyTurret.AttackBehavior` → `EnemyController.TryAtack` → `HandleShootInputs`，两端都打真弹（`RemoteDrivenMovement` **只管移动**）。
- ✅ **打人**：`WeaponBaseController.LocalEffectDamage`（运行期属性），由 `VFXAirdropEffect.SpawnCreatObject → MarkGeneratedUnit` 给生成物上所有 `WeaponEnemyController` 打标 → `ProjectileBase.BuildHitData` 透传 ⇒ 非权威端只对玩家/盟友落地。只认 `WeaponEnemyController`：载具武器是玩家在开，不能标。
- ✅ **号**：`StampVehicleNetId` → `StampCreatUnitNetId`，去掉"仅 SpecUnit"过滤 ⇒ 空投件也能拿到 4028 里房主分配的那个号（**零协议变更**）。
- ⚠ 只盖章会**引入新的 2 倍**（号一致后，两端各自挨的敌方子弹 + 成员那笔 4025 都会落地）⇒ 同时给"挨打"加门：`Actor.HostAuthorityUnit` 由生成时置位，`NetVehicleDamageBridge.IsAuthorityGatedActor`（载具 || 该标记）⇒ 非权威端只吃 4040 下发。
- ⚠ 无人机/背包没有 `Damageable`，`HostAuthorityUnit` 对它们 no-op；但**装备归属本来就同步**（4036 → `ApplyRemoteOperate(盟友实例)` → `InstallEquip` → `GuardDog.OnInstall`）。2026-10-27 实测：无告警、位置一致 ⇒ 不用另开位姿通道。
- ⚠ 残：炮台**自转**（`Turret.Look/AutoRotate`）两端各算，纯表现；无人机状态机节奏亦然。

## 状态与验证（2026-10-27）

- ✅ 已完成：装置（23）/ 本地生成效果（毒烟·火焰·地雷·空投舱）/ 范围动能 / 荆棘反伤 / 支援单位打人 + 挨打 / 无人机装备归属（已实测）。
- ⏳ **差一次数值实测**：2 人局叫 `ADSO_G_Gatling` 或 `ADSO_R_120MM`，确认敌人掉血不再是双倍、炮台不再刷 `这只怪没有 NetId`、打爆后成员那份跟着消失。
- 手段：两端开 `NetSyncLog`，看「命中上报·发」/「伤害下发·发」是否**成对两遍**（`EnemyNetBridge.OnLocalHit/OnLocalDamaged`）；单位号看「载具/号」日志。

## 相关

- 伤害通道 / 4025 上报 / 4040 广播 / tick 权威门 → `net-damage.md`
- 生成同步 4052 / 生成器复刻 → `net-spawn.md`
- 手雷黏性（同一天改的，已完成）→ `2026-10-26.md`
