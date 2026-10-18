# 主题 · 联机 · 伤害通道（DamageMsg / 伤害成分 / 起源拆分）

> 由 `net-battle.md` 拆出（2026-10-18），含原 §7.2e + §7.2f 下半（两族合并 / 伤害落地 / 验证）。
> 原 §7.2f 上半（生成侧复核）见 `net-spawn.md`；本地护甲/AP/抗性结算见 `gameplay.md §伤害管线`。

#### 7.2f 复核结论（下半 · 序号承接 `net-spawn.md`）

### 7.2e ✅ 伤害成分过网（异常状态 / 破甲，2026-10-12 修）
- **确认是真问题**：`Damageable.InflictDamage` 里 `DamageTypeEnum.Destruction → ArmorDestruction`（扣 `remainArmor` + 护甲归零后 `Damageable.Tick` 每 tick 流血），
  其余类型 → `Health.HandleDamage → AddAboGauge`（积蓄槽/满槽效果）；而 `EnemyNetBridge.ApplyRemoteDamage` 把 `DamageGroups` **中立化成 `{Gun,1}`**
  ⇒ 接收端这两条分支**整段被跳过**（主机把怪冻住/破甲了，成员那边它照常跑、护甲还在）。
- 修法（**保留 `Resolved=true` 的"血量不重算"语义**，只补副作用）：
  `Damageable.InflictDamage` 把原始成分挂到 `Health.LastRawGroups`（try/finally 包 `TakeDamage`，出来后清空 ⇒ 流血/异常 tick 拿到 null）；
  `IHealth` 暴露 `LastRawGroups {get;set;}`；`EnemyController._OnDamaged` 用 `PackDamageGroups` 摊平成 `[count,type,coeff,...]`
  （静态缓冲 `m_groupScratch`，只在打包消息内复用）；`EnemyHitMsg`/`EnemyDamagedMsg` 加 `[Key(4)] float[] Groups`；
  `BattleEventBus.OnEnemyHit/OnEnemyDamaged` 与 `NetRoomFlow.OnEnemyHitUp/OnEnemyDamaged` 加第 5 参数；
  `EnemyNetBridge.UnpackDamageGroups` 还原（拿不到 ⇒ `NeutralGroups()` 保住老行为）。日志带 `成分=`。
- ✅ **tick 伤害归属已钉（用户 2026-10-12 选 (a)）**：`FPSGame.Data.BattleState.IsTickDamageAuthority`（**数据自持**，09 在 `WaveManager.Awake` 按角色置位 =
  `flow == null || flow.IsHost || flow.SelfSid == 0u`；`EnemyNetBridge.OnHostLost` 里恢复 true；`BattleState.Reset()` 归位 true = 单机中性）。
  两个结算点各自加门：`Damageable.Tick()`（破甲流血）与 `Health_AboTick → DealAboTickDamage`（异常状态跳伤）——
  条件是 `BattleState.ShouldApplyTickDamage(IsPlayerLike)`；**玩家/盟友例外**（他们血量本机权威 ⇒ 任何角色下都要本地算）。
  成员端**只保留积蓄槽/特效**（那是从已同步状态推导的表现）、血量等房主的伤害广播落地 ⇒ 不会再 N 倍叠加。

- ⏳ **用户已批、待做（合并两族，2026-10-12 拍板）**：
  ① ✅ **伤害/生死族已合并（2026-10-12 落地）**：新的 `DamageMsg{Sid, NetId, Key, Damage, AttackerNetId, Groups, Part, IsDeath}`（`07NetGame/Msg/RoomMsg.cs`）
  取代 `EnemyHitMsg`/`EnemyDamagedMsg`/`EnemyDiedMsg`/`SceneDestructibleMsg`（后三个类已删）。**同一类挂三个号，方向由到达的 cmdId 决定**：
  4025 = 成员上报、4040 = 房主下发（含 `IsDeath`）、4043 = 无号可破坏物双向兜底 + 房主账本补发。
  `NetRoomFlow` 事件变成 `Action<DamageMsg> OnEnemyHitUp / OnEnemyDamaged / OnSceneDestructible`（`OnEnemyDied` 删）；
  `SendEnemyDied(netId)` 现在合成 `DamageMsg{IsDeath}` 走 4040（**4026 已停发，常量保留**）；
  `EnemyNetBridge` 侧 `OnRemoteHit/OnRemoteDamaged(DamageMsg)` + `ApplyRemoteDamage(..., part)` + 新的 `ApplyRemoteDeath(DamageMsg)`
  （`NetId` 优先、为 0 退回 `NetDestructibleBridge.FindByKey(Key)`，该函数因此改成 public）；
  `ResolveDamageable(actor, part)` 已支持"按部位下标取"（发送端目前恒 0，等 `HitPartIndex`）。
  ② ✅ **房主生成族已合并（2026-10-12 落地）**：新 `HostSpawnMsg{Kind, X,Y,Z, Yaw, Sub, Targets[], SourceKey, Slot, NetId, Tx,Ty,Tz, PrefabId}`
  （`07NetGame/Msg/SyncMsg.cs`，`HostSpawnKind{Patrol, Unit}` 同文件）+ **新号 `CmdId.HostSpawnNtf = 4052`**；
  原 `PatrolSpawnMsg` / `EnemySpawnMsg` 两个类**删除**，`4047` / `4051` 停发（常量保留 + 注释说明）。
  新桥 **`09Manager/Battle/NetHostSpawnBridge.cs`** 取代 `NetPatrolBridge` + `NetEnemySpawnBridge`（**两个旧文件 + .meta 已删**），
  API：`ReportPatrol(pos, targets, kind)` / `ReportUnit(source, slot, spawned, rot, target)` / `OnRemoteSpawn` → `ReplayPatrol` / `ReplayUnit`；
  `WaveManager.Install/Uninstall` 收口成一行。`4023/4039` **不合并**（频率档不同）。
  ③ ✅ `UnitSkill_OrbitArmor` 已并进统一生成入口（2026-10-12）：类实现 `INetSpawner`（`SpawnSourceKey => NetSpawnKey.Of(gameObject)`、
  `OnEnable/OnDisable` 登记 `SpawnerRegistry`），`SkillStart` 里改调 `BattleHub.Current.CreatUnit(prefab, pos, rot, this, i)`；
  抽出 `EnsurePartController/BeginArmorPhase/RegisterArmor` 三个助手，**成员端 `ReplaySpawn` 也注册无敌装甲 + 启动装甲战**
  （否则本端主单位不在无敌体系里、且速度不归零会自己走）。
  ④ ✅ `HitPartIndex` 发送端已落地（2026-10-12）：`Actor.IndexOfDamageable(Collider)`（命中体或其父级上的 `Damageable` 在
  `damageables` 表里的下标，-1 = 认不出）→ `EnemyController._OnDamaged` 填进 `BattleEventBus.EnemyHit/EnemyDamaged(..., part)`
  → `DamageMsg.Part` **用"+1"编码**（0 = 未指定，因为 0 是合法下标）→ 接收端 `EnemyNetBridge.DecodePart` → `ResolveDamageable(actor, part)` 优先按部位取。
- ✅ **已落地（2026-10-12）= 执行计划「第 8 步」**：`Other` 可破坏物**并入统一伤害通道** ——
  `05UnitCore/UnitEventBus` 新增 **`OnUnitDamaged(受击者GO, PEInt 最终扣减量, 来源GO, 受击体Collider)`**（发布点 = `Health.TakeDamage` 里
  `trueDamageAmount > 0` 那一支，紧挨着 `UnitHit`）⇒ `NetDestructibleBridge` 订阅它：**有 `NetId`**（战斗前创建的 `Other` 已走派生号）就
  `BattleEventBus.EnemyDamaged`（房主下发 4040）/ `EnemyHit`（成员上报 4025），**没号**仍只靠 4043 兜底。
  ⚠ 回环门：`_applyingRemote` + **`EnemyController.ApplyingRemoteDamage`**（后者也顺手加到 `OnLocalDeath` 上，避免"远端伤害打死本端副本"再报一次 4043）。
  ⚠ 死亡重复到达是安全的：`Health.HandleDeath` 有 `m_IsDead` 守卫（4043/4026/玩家血量三条通道撞一起也只会死一次）。

- ✅ **验证（2026-10-12）**：第 4~8 步都做到了"离线编译（Unity 自带 Roslyn，0 error）+ 编译产物 dll 元数据反射核验"，
  并且 **Unity 侧也各过了一轮**（Editor.log：`00:19:47` 重建 `06_Gameplay/09_Managers/10_Effect`、`00:24:36` 重建 `05_UnitCore/06_Gameplay/09_Managers`，两段都无 `error CS`）——
  这轮 MCP 工具在会话中途掉线（`mcp_call_tool` 不再注册，Unity 桥 6400 仍活着），所以"三证"里的域重载/Console 是用 **Editor.log + dll mtime + 离线编译** 等价替代的，没跑编辑器内 `execute_code` 反射。
  ⚠ 之后新增的 `06Gameplay/AI/AiLod.cs`（成员端 AI LOD）**还没进 Unity 的 csproj** ⇒ 只过了离线编译 + dll 反射，下次 Unity 刷新时补看一次 Console。
- ⚠ `Actor.NetId` 相关 DTO 字段变更是**破坏性**的 ⇒ 双端必须同版本一起换。
- ✅ **已落地（2026-10-11，Unity 侧已编译：域重载 + Console 0 error + 反射核验 True）**：`NetHostSvc.SendToAllExcept(exceptSid,msg)`
  （库无 except 版本 ⇒ 按 `_players` 逐个 `SendTo` 跳过来源）+ `NetRoomFlow.SendEnemyDamaged(netId,damage,exceptSid=0)`（排除来源时
  `<c>Sid</c>` 填开枪者）+ `EnemyNetBridge.OnRemoteHit` 应用完**再转发给除来源外的成员**（补上"成员A 伤害到不了成员B"的缺口）。
  ⏳ 仍待做：`HitPartIndex`（部位下标，用户已确认放在后面）+ ~~房主唯一创建（4051）~~（✅ 第 4~7 步已落地，见 §7.2a）+ ~~派生键号段~~（✅ 已落地）。
- ✅ **已落地（2026-10-11）`AttackerNetId`（批次 1）**：`EnemyHitMsg`/`EnemyDamagedMsg` 各加 `[Key(3)] int AttackerNetId`；
  `BattleEventBus.OnEnemyHit/OnEnemyDamaged` 与 `NetRoomFlow.OnEnemyHitUp/OnEnemyDamaged` 改成带该参数的签名（订阅方只有 `EnemyNetBridge`，
  已同步改）；`EnemyController._OnDamaged` 从 `damageSource` 取 `Actor.NetId` 带上；`SendEnemyHit/SendEnemyDamaged` 加可选参数；
  `ApplyRemoteDamage` 来源改**三级解析**（攻击者号 → 上报者盟友实例 → NoSource）。⚠ **破坏性协议变更，双端必须一起换包**。
  ~~⚠ `HitPartIndex` 仍未做~~ ⇒ ✅ 发送端已落地（见上 §"④ HitPartIndex"）。
- ✅ **已落地（2026-10-12）「直接来源 / 起源」拆分**（执行计划已归档 `plans/archive/INDEX-plans-20261013.md`，过程见当日 daily）：
  `DamagePacket` 的 `DamageSource` **改名 `DamageOrigin`**（语义不变：所有减伤/友伤/归属读它）+ 新增 `DamageCaused`；
  生产者收口在 `ProjectileBase.BuildHitData`（`soure = Owner` 起源 / `caused = WeaponBase.GetComponentInParent<Actor>() ?? Owner` 直接来源），
  地雷/空投舱显式 `caused = gameObject`。**消费口径**：显示/AI（`DetectionModule`/受击改目标/倒地播报/`OnHit`/`OnDamaged`）走 **caused**；
  统计（`命中次数`/`击杀敌人`/`UnitKill`/`ArmorDestruction`）走 **origin**。
  联机：`DamageMsg` 追加 **`[Key(8)] CausedNetId`**（旧 8 个 Key 未动）、`BattleEventBus.EnemyHit/EnemyDamaged` 尾加 `causedNetId`、
  `ApplyRemoteDamage(..., causedNetId)` 两个来源**各自**解析（`ResolveNetEmitter`），`AttackerNetId` 语义仍是**起源**；
  `UnitEventBus.UnitDamaged` 加第 5 参 `origin`（唯一订阅方 `NetDestructibleBridge` 两个号都要）。
  ⚠ **破坏性协议变更：双端必须同版本一起换包**（旧端 `Skip()` 掉 Key(8) ⇒ 退化成"显示成开枪者"）。
