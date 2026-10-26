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

## 玩家 Actor 生命周期 / 接口引用判空
- ⚠ **`ActorsManager.Player` 的类型是 `IActor`（接口）⇒ 它上面的 `== null` 走引用比较、吃不掉 Unity 假 null**：玩家被销毁后它仍是僵尸引用，判空照样通过，随后读 `.transform`/`.Pos` 抛 `MissingReferenceException`。接口引用一律用 `IsValidMono()`（`00Core/Interfaces/CoreInterfaces.cs` 的扩展）。
- 已修（2026-10-26）：`ActorsManager.Unregister` 补 `ReferenceEquals(Player, actor) ⇒ Player = null`，`Awake` 补 `Player = null`（原本 `Actors`/`Players`/`SpecUnits` 三张表都清了，唯独漏了这个单值引用）。
- 触发路径：舰船 `SettingWnd.TryTeach` / `FrontWnd.EnterTeach` 走 `AsyncLoadScene("Teach")` **单场景加载** ⇒ 舰船 Player 先销毁、Teach 的 Player 后注册，中间几帧就是僵尸窗口（大厅→战斗没有这个空窗，所以长期没暴露）。崩点当时在 `SubtitleWnd.Update` 的第三人称分支。
- ⚠ **第二处同类（同一天）**：`PlayerWnd` 把玩家组件缓存进字段（`m_Controller`/`m_WeaponsManager`/`m_Health`）+ `initPlayer=true` 永不复位 ⇒ 换场景后每帧在已销毁的 `PlayerController` 上读 transform。修法：`Update` 里 `if (initPlayer && !m_Controller) UnbindPlayer();`（"控制器没了"= 需要重新绑定），`HideWnd` 的退订抽成 `UnbindPlayer()` 防止事件叠两份。
- ⭐ 通用口径：**跨场景存活的窗口/系统，缓存玩家组件或玩家接口引用都必须每帧复查**——`UnityEngine.Object` 字段用假 null（`if (!x)`），接口引用用 `IsValidMono()`；玩家换代就整段解绑重绑（`OperationWnd:69`、`EquipmentUninitiatedUI:36` 是现成的自愈写法）。
- ⭐⭐ **单位"注册"必须等注册表存在**（2026-10-26 修）：`Actor.WaitSetPos` 的闸门是 `IsMainStage() || ActorsManager.Instance != null` 双条件。只判主流程时，从舰船进新手教程（`GameState` 本来就是 Bridge）会让**场景里摆着的 Actor** 在激活那一帧就把 `UnitEventBus.*Create` 发给还没人订阅的静态事件（`ActorsManager` 要等 `BattleManager.Creat` 才 new）⇒ 引导 NPC 永久不注册：字幕框不出现、语音也没人播（语音**只**由 `SubtitleSpecUnit/NPC/Shout.OnActorSpeech` 播）。运行时 Instantiate 的单位不受影响。
- ⚠ 遗留：`ActorsManager.Awake` 里的 `Actors = new()` 会抹掉"先于它 Awake 的场景 Actor"自注册的条目（`RegisterSpecUnit` 不补回 `Actors`）⇒ 场景摆放单位的 `FindActor(NetId)` 查不到。
- ⭐ **`ActorFlag.Invincible` = "AI 不可选目标"，不只是"不掉血"**：`FpsHelper.VaildTarget` 里 `!HasFlag(Invincible)` 是所有 `DetectionModule.CheckNewTarget` / 炮台锁头候选的硬门（伤害另有 `Health.TakeDamage` 那道门，两者语义不同）。舰上无敌由 `PlayerController.Start()` 按 `GameState==Bridge` 加，**离开舰桥必须撤**（2026-10-26 修：从舰桥进教程时玩家在 Bridge 那一刻实例化，旗标只加不撤 ⇒ 贴脸也锁不到）。
- ⚠ `BattleManager.unitQueryGrid` 必须在**任何 Actor 实例化之前**建好（`EnsureQueryGrid` 幂等，`InitTerrain` 开头 / `InitSpecial` 的 `mapRoot.Init` 之后各调一次）。原来只在 `Start()` 建，而 `Start()` 晚于创建那一帧的协程首段 ⇒ `InitSpecial` 首段建任务时 `Actor.Awake → UnitPosChange` 空引用，异常冒出 `StartCoroutine` 打断整条 InitSpecial。
- ⚠⚠ **不要在舰船场景里把 `GameState` 设成 `Transition`（8）**：`Utnapishitim.unity` 的 `GameStateController` 绑了 `state:8 进入 → TransSceneController.StartLoad`（进是 `StartLoad`/`Play`/`SetTrigger`，退是 `Lock`/`set_enabled`）⇒ 在 `SettingWnd.TryTeach` 的 `AsyncLoadScene` **之前**设，会立刻触发"加载战斗场景 + `BattleManager.Creat(true)`"，与 Teach 那次 `LoadSceneAsync` 抢同一个 `ResSvc.AsyncOp/sceneLoadedCallback`（双加载 / 回调被覆盖）。要设只能设在**加载回调里**（此时舰船已卸载、那个 Controller 已销毁）。各场景实际绑定：舰船 `2/4/8/128`、教程只有 `32` ⇒ 想找"非主流程停放态"用 `Front(1)` 或 `Load(16)`（都没绑，差别只在 `AudioSvc`：Front 切封面 BGM、Load 播加载乐）。
- ⚠⚠ **`TransSceneController` 的宿主就是船本体**（`MapRoot/Utnapishitim`，19 个子物体 + Animator(`Bridge`)+GameStateController+ShowVehicle 都在它上面）：`StartLoad()` 把它 SetParent(null)+DontDestroyOnLoad **带进战斗场景**当转场用，`state:8 退出 → SetTrigger("EndLoad")` → `EndLoad.anim` 第 2 秒动画事件 `Destroy()` 把它删掉收尾。⇒ 任何"**船内**退出 Transition"的路径都会**把整条船删掉**（2026-10-26 教程回大厅实测：`EndGame` 的 `!IsNormal` 分支带着 Transition 加载船、回调再设 Bridge）。修法：`EndGame` 该分支加载前先 `GameState = Load`（在旧场景里退出 Transition）+ `Destroy()` 加"必须在 DontDestroyOnLoad 场景里"的守卫。长期该把动画/脚本拆到独立子物体，别挂在船根上。

## 玩家 / 相机
- 相机：`PlayerController_ThirdPerson` 两个机位点 (0.5,1,-4)/(0.8,0.4,-1.8) × `ThirdPersonDistanceScale`（**`abs(z)` 才是视距**）；`IsAiming` 只在 `m_WeaponSwitchState==Up` 时重估（~0.4s 延迟）
- ⚠ `PlayerWeaponsManager.OnWeaponSwitched(bool isSec)`：`isSec=true`（副武器）只设左手 IK，**不覆盖**主武器右手 IK（否则"替换主武器"连触发两次互相覆盖 ⇒ IK 错乱 ⇒ `leftFree/rightFree` 误判 ⇒ 允许本不该允许的双持）
- `PhoenixEagleController` 旋转乱跳：阶段切换时**不要**改 `lastPos.y`，统一在 `Update` 里按阶段 flatten y
- 第三人称武器朝向 = `FirstPersonSocket` 世界旋转 `Euler(m_CameraVerticalAngle, transform.eulerAngles.y)`（`PlayerController.LateUpdate`）+ 子弹方向由 `PlayerWeaponsManager.UpdateWeaponThirdPersonAim` 注入 `ThirdPersonAimTarget`。⚠ yaw 用**角色朝向**而非 `_cameraYaw`（非瞄准移动时二者可差 180°，硬对齐会让枪口与身体撕裂、手臂 IK 崩）
- 玩家欧帕兹背包 `PlayerOOPartInventory`（每种上限 `MaxPerType=5`）：`CurrentCount/IsFull/TryAdd/Remove/GetCount/GetAll`；⚠ 远端盟友那份是 `FriendOOPartInventory`（幽灵背包，见 `net-battle.md`），容器类家具**面向 `IOOPartInventory`** 取背包
- ⚠⭐ **"被压住自动脱困"的排除条件**（`BaseSelfMoveableController.IsPenetrating`/`HaveObstacle`）：`Overlap*` 系列必须排除**自身 + 全部子物体**（`c.transform.IsChildOf(transform)`），只排根上的 `CharacterController` 会恒判"被压住" ⇒ 每 ~1.3s 把单位往上瞬移 `height*1.2` 米（Exosuit 高 4.5 ⇒ 抬 4m）⇒ **"落地→悬浮→又掉下来"死循环**（2026-10-13 修，机甲/玩家都会中；`LayerDefinition.MoveableLayers` 是 Everything，放大了它）。取证口：Console 筛 `自动挣脱`、或 `execute_code` 复算 `OverlapCapsuleNonAlloc` 打印每个命中的 `isSelfChild`。

## 昵称（`ArchivesData_SO.playerName`）读写口径（2026-10-26）
- ⭐ **改名唯一入口 = `Window.SetSelfUserName(name)`**（10UI）：写档 + `Save()` + `GlobalEventBus.PlayerNameChange(name)`。直接在窗口里写 `archive.playerName = ...` 界面不会刷新；而存档类在 00Core 引用不到 06 的事件总线 ⇒ "写档+广播"只能在这个 helper 里成对做。
- 订阅方按"昵称优先、空则角色名"重画：`BridgeWnd.RefreshSelfName()`（回落串缓存在 `_selfNameFallback`，**不缓存实体引用**）、`PlayerWnd.OnPlayerNameChange`（回落 `m_Controller.PlayerName`）。SettingWnd 自己的名牌没订阅，改名回调里就地 `SetText`。
- ⚠ 未覆盖：改名**不会**把新名字重报给已加入的房间（`PlayerList` 仍是进房时的名字），要同步得再走 `TeamNetBridge.SendSelfProfile`。
- 默认名 = `"用户"+UID`，判据 `ArchivesDataBase_SO.IsDefaultPlayerName()`（别在 UI 里重写前缀）。

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
