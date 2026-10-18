# 主题 · 玩家侧（相机 / 武器子弹配置 / 战备 / 寻路）

> 由 `gameplay.md` 拆出（2026-10-18）。战斗/AI/伤害管线/组件存活仍在 `gameplay.md`。
> 联机侧的战备/位姿/载具口径见 `net-*.md`。

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

## 武器 · 子弹与伤害配置（`DamageData` 迁移遗漏）
- ⚠ **`Damages[UseDamageIndex].BulletPrefab` 为空 = 这发子弹不存在**：`VfxPool.Creat<T>` 返回 null（VFXManager 打"空模板"错），
  旧代码会把 null 交给 `OnBulletShoot`（`GetChargeBullet`/`GetLaserBullet`）⇒ NRE。⚠ 异常穿出 `LogicFrameHost.TickOnce` 的 for ⇒
  **该帧后面所有单位不 tick + 每帧刷异常栈**，不是"少一发子弹"那么简单。现已由 `ShootFromMuzzle` 拦 null（每武器只报一次错）兜底。
- ⚠ 空 `Damages`（列表本身为空）更早就会炸：`CurrentDamgeData => Damages[i]` 直接 ArgumentOutOfRange，`CurrentSpeed/CurrentGravity` 同源。
- 已知漏配（`plans/audit_weapon_bulletprefab.py` 可重跑全量）：`Airdrop/Healdrone_HaloBomb`（ShootType=Charge，
  原值 `Projectiles/Airdrop/Pro_Empty.prefab`）、`BattleBase/HealdroneBase`（原 `Projectiles/Weapon/Pro_Normal_Blue.prefab`）、
  `Enemy/Robot/Royal/Droid_RL` 第二个武器组件（`Damages: []`）、旧武器 `Weapon_Shotgun/Blaster/Launcher`（`Damages: []`）。
- 根因是 2026-09-11 把**武器级** `ProjectilePrefab` 迁进 `DamageData.BulletPrefab` 时漏迁（值丢），
  2026-10-02 代码里武器级字段又整体注释掉 ⇒ 连兜底都没了。查这类"字段搬家"必须比对迁移前那一版的 prefab YAML。
