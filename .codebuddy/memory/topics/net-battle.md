# 主题 · 联机 · 战斗同步总览（世界模型 / 种子 / 波次 / 世界状态判据）

> 2026-10-18 由原 38.8k 字符的 `net-battle.md` 按小节拆分（每篇 ≤8000）。子篇：
> `net-battle-msg.md`（消息号表 4001~4054 / 5001）· `net-netid.md`（号空间 / 生成入口 / 成员端 LOD）·
> `net-spawn.md`（生成器复刻实例 4052）· `net-damage.md`（伤害通道 / DamageMsg / 起源拆分）·
> `net-vehicle.md` + `net-vehicle-remote.md`（载具 4053/4054）· 家具字段 §7.2d → `gameplay-world.md`。
> 原文（含每条实测与行号）已下沉：`archive/topics-snapshot-20261011/net.md`。

## 1. 世界模型（先定口径）
- 现状 = **两端各跑一份世界** + 房主权威关键量。**不做** lockstep（PhysX + 动画/补间 ⇒ 跨机确定性拿不到）；**不做**子弹权威（各端本地模拟 + 命中上报）。
- **路线（2026-10-07 定调）**：C3 玩家间命中 → C1 波次权威 → **B 敌人权威 + 快照/纠偏**（终局 = "确定性影子模拟 + 意图下发 + 纠偏"：成员跑同一份 AI，房主只发 `EnemyTarget{netId,targetSid}` / 死亡命中事件 / `EnemyCorrect{netId,pos,hp}`）。⚠ **主机 CPU 才是真成本**（带宽 30 怪 × 20B × 20Hz ≈ 12KB/s 够用）⇒ AI LOD + 不做子弹权威。
- ⚠ **"开火同步"≠"命中/血量同步"**：`FpsHelper.Hit` 是**本地**子弹碰撞结算入口 ⇒ 远端开火（4015/4016）刻意不生成子弹、不结算；玩家血量**各自机器本地权威**，同步只为"给别人看"（盟友 `PlayerFriend.prefab` 刻意**没有 `Damageable`**，血量由 4017/4018 镜像进其 `HealthPlayer`）。

## 3. 种子与初始化确定性（`SeedUtil` / `TaskState.Seed`）
- ⭐ **单点权威种子 `TaskState.Seed`**：唯一写入点 `TaskManager.SetTask(...)`（经 `SyncTaskState` 发布）；`BattleManager.ResolveBattleSeed()` 供 `BattleRandom` 与地形。⚠ `RandomUtils.InitRandom()` **与战斗无关**（只喂大厅随机，别指望它）。
- `00Tools/SeedUtil.cs`：`SeedStream` 枚举 + `SeedUtil.Derive(seed, purpose)`（整数混合，全项目可见）。**所有"必须一致"的随机都要派生独立流**，绝不能用全进程静态流 `RandomUtils`（被音效/弹孔/武器散布/谜题共同推进游标）。
- 已派生流：`Terrain`/`TerrainDecor`（地形）、`MissionEntity`（任务点/朝向）、`Wave`（波次构成）、`Ai`（`06Gameplay/AI/EnemyRandom.cs` 按 `NetId`，`WaveManager.Awake` 里 `Clear()`）、`Weapon`（武器/战备散布）、`UnitPlacement`（巡逻队构成）、`Weather`（天气）。
- ⚠ **纯函数 > 一条流**：会"被重试/次数可能不同"的场合（谜题 `KeyScreenControl.PuzzleRange`、炼油 `errorTimes`/`PickRepairPipes`）必须用 `Derive(seed, 细分键)` 的**纯函数**，不能用 `new System.Random(seed)` 一路抽（消费次数不同就错位）。
- **本局配置"以内容为准"**：`TaskConfirmNtf` 带 `TaskCfgDto`（全 int/int[]/float/bool/string）；`SetTask` 有内容就不查本地表 ⇒ 跨窗口不一致不再致命。
- **TaskManager 桶机制**：`CurrentBucket()=UtcNow/1800`（30min 窗口，有 120s 调试开关）；`Update` 做**桶变化检测**且**只在 `GameStateEnum.Bridge` 执行**（修掉休眠漏拍 + 战斗中不换表）；`TaskFingerprint(mapId,taskIndex)`（FNV 整数混合、**不含任务名**）。
- 地形：`GenerateNoiseTerrain` 两条派生流 + 8 处换流；`ApplyFractalNoiseToTerrain(...)` 末尾追加 `int seed = 0`（默认值避免改调用点）；⚠ NavMesh 烘焙跨端非逐位一致（厘米级）⇒ "NavMesh 完成"应纳入窗口结束条件（已改成"不超时即放弃"）。
- ⚠ **`Actor.IndexID` 是进程内自增（跨局不清零、还数着玩家/幽灵/道具）⇒ 绝不能当网络 id**；跨端身份一律用 **`Actor.NetId`**（0 = 非同步）。⚠ 别重置 `GlobalIndexID`（大厅有存活 Actor，`Actor.Equals/GetHashCode` 正按 `IndexID` 比较）。

## 4. 波次 / 敌人
- 波次**内容确定性**：`WaveBase` 种子 = `Derive(TaskState.Seed, (int)SeedStream.Wave * 1000 + 波序)`（波序 = `WaveManager._waveSeq` → `WaveBase.PendingWaveIndex`）。
- 波次**时机权威**：`CreatWave` 有**成员闸门**（成员端直接 `return false`）+ 房主 `PublishWaveStart` 广播；成员 `ApplyRemoteWave` 用**房主的 waveIndex** 复刻。
- **追击中心**：`center` 常是"最近玩家位置"（各端算会因位姿延迟**翻边**）⇒ 房主 ~2Hz 下发 `WaveCenterNtf`，成员 `SmoothRemoteCenter` 插值跟随；⚠ 成员端 `centerGetter` 要换成 `SmoothRemoteCenter`（否则自身 center 会飘）。`extraWave` 跳过冷却。
- ⭐ **`NetId` 分配点已下沉到 `Actor.Awake`**（`type == Enemy` 就分号，`ActorsManager.NextEnemyNetId()` 每局归零）。⚠ 必须在 `Awake`：放 `WaitSetPos` 会晚一帧，而波次单位创建后**同帧**就 `SetNavDestination`；⚠ 判 `ActorsManager.Instance != null`（更早诞生的分 0）。⇒ 覆盖波次/巡逻队/巢穴/召唤/场景刷新**全部路径**。
- 移动（意图式）：钩子 = `EnemyController.SetNavDestination`（唯一漏斗）→ 房主发 4024；成员端 `RemoteDrivenMovement=true` 时本端 AI 决策一律作废，只 `ApplyRemoteDestination`（**绕开**本端的 `<1m + hasPath + 重试节流` 去抖）。`SyncedNavMover`（`06Gameplay/AI/`）把"缓存远端点 + agent 可用才落地 + 每帧重试"抽成组件；⚠ `NPCWalk` **故意不注入**（大厅没有 `PathRequestManager`）。
- 生死：成员 `_OnDamaged` → 4025 上报；房主用 `DamagePacket` 结算（⚠ `DamageGroups` 不能空）⇒ 死亡 4026 ⇒ 成员 `Actor.Kill()`。⚠ `EnemyNetBridge` 有 `_pendingMoves`（目标点比副本先到就暂存，`OnEnemyCreate` 补发）。

## 5. 玩家表现 / 位姿 / 场景单位
- 位姿：`PlayerTransformUp`(5001) 20Hz 上行 → 房主**聚合** → `TransformBatchSync`(5002) 15Hz 批量下行 + 房主本地自派发。⚠ **插值缓冲必须多点**（`BufferSize=6`，`RenderDelay` 大于包间隔时两点插值会退化成"每包跳一下"）；且**吸附判定要与"上一个快照"比**（与当前 transform 比会因渲染滞后误判传送）。
- ⚠ 俯仰必须过网（`PoseSnapshot.Key(6) Pitch`）：只带 yaw ⇒ 别人看你永远平举枪。身体仍 yaw-only，pitch 推给武器挂点。
- ⚠ 远程弹道"穿过去没命中"已修：`SpawnVisualBullet(muzzle, direction, endPoint)` 把命中点传下去（`SetVisualEndPoint` 复位、`ReachedVisualEndPoint` 用**本帧位移投影**判定）；`FpsHelper.PlayImpactFx` 抽出来给表现弹复用。
- ⚠ **"需要每帧制导"的弹（`ProjectilePlayerHoming`＝火箭背包 `Pro_Guided_Rocket`）在接收端曾恒不制导（2026-10-13 已修）**：表现弹是真子弹但 `Owner`＝盟友 GO ⇒ `Owner.GetComponent<PlayerWeaponsManager>()` 为 null ⇒ `m_Camera=null`，制导分支整段跳过；且背包那条路连开火落点都没传（`FriendWeaponView.cs:206` 传 `Vector3.zero`）。同类：`ProjectilePlayerLaser.Update` 读的是 `FpsHelper.PlayerCameraLookPoint`＝**本机玩家**的点。
  - **判据**：需要"实时瞄准"的弹一律**收端本地重算**（yaw 在 transform、pitch 在 `NetTransformView.RenderedPitch` ⇒ 自己打射线），**不要**新加同步字段（目视点是派生量、会跳变，源量 yaw/pitch 已在传）；制导弹的 `SetVisualEndPoint` 要用**实时**点，开火时的落点是过期点。⚠ 别拿 `WeaponBag` 的 `muzzle.forward`（"武器朝向保持 prefab 默认"）。
  - **落地（全在 `06_Gameplay`）**：`FpsHelper.TryGetAimPoint(owner,…)`（相机优先 → 否则 `FriendController.TryGetAimRay`：`dir=Euler(RenderedPitch, transform.eulerAngles.y,0)*forward`，origin 用 `Actor.AimPoint`）；`ProjectilePlayerHoming` 每帧取它 + `IsVisualBullet` ⇒ `SetVisualEndPoint(实时点)`（新标记在 `ProjectileBase`，`Shoot` 里复位）；`ProjectilePlayerLaser` 同入口。⚠ 背包**非制导**武器仍没传落点（当前只有这一个背包投射物 ⇒ 未补）。
- ⚠ 标记上报**只认本机玩家**（另一发布源是"重放远端开枪的命中特效"）；⚠ `GlobalEventBus.Mark` 的 `target` 是**可空契约**，订阅方必须容忍 null（用 `owner + point` 出表现）（字幕类还要用 `pointMark` 区分"本来没目标/目标被销毁"）。
- 场景单位（NPC）：`NPCWalk` 自记在场表（`static s_all` + `FindById`），`SceneUnitMoveSink`（`[RuntimeInitializeOnLoadMethod]` 自举常驻）转发 4032。⚠ **不要扫 `ActorsManager.Actors`**（`ActorsManager.Awake` 会 `Actors = new()` 丢掉先注册的 NPC）。

## 6. 世界状态类同步（4033+）· 判据
- **任务(4035)**：键 = `netOrder`（数据生成模式按创建序；场景模式按 0.1m 位置排序）；成员端 `MissionBase.RemoteDriven` 门"不自判"（⚠ `Uninstall`/`OnHostLost` 必须复位）。**必须带 State**（完成有"进度/事件触发"两个出口）。
- **家具(4036)**：键 = `SyncId = FNV1a(Id + 位置0.1m)`（`Awake` 算一次缓存；`NumberID` 各端自增 ⇒ 不能用）。**一处收口**：远端 `ApplyRemoteOperate(remoteUser)` 跑同一条 `Operate()` ⇒ `OnOOPartCollect`/`OnSubmitOOPart`/`OnKeiSubmit` 不需单独同步。⚠ 操作者只能传**远端单位**（`OnOperation` 靠 `user == gameObject` 过滤）。⚠ 重放期间 `IsRemoteReplay = true` ⇒ 非幂等副作用（管道 `ModifyHeightMap`）要跳过。
- **可破坏物(4043)**：只认 `UnitTypeEnum.Other`；只传"死了"、不传伤害；房主侧 `_destroyed` 账本，新人入房补发。
- **终端(4046/4049)**：`KeyScreen` 的**驱动端** = "操作者所在那台 / 无人阶段时房主"（`IsStageDriver()` 单点判定）；非驱动端把**输入换成远端按键**跑同一套阶段逻辑 ⇒ 所见一致。⚠ 应用阶段会触发 `furn.Operate()` ⇒ 回环门必须**跨桥共享**（`NetApplyGuard`）。
- **容器提交(4048)**：`Furniture_OOPartDepositBase` 面向 `IOOPartInventory` 取背包（`PlayerOOPartInventory` / `FriendOOPartInventory` 两实现）⇒ 远端用盟友的**幽灵背包**真跑 `BeginPress/StepPress`（效果写在 `StepPress` 里，`Operate()` 重放不出来）。
- **巡逻队(4047)**：`WaveManager.CreatPatrol` 成员闸门 + `CreatPatrolRemote` 复刻；构成靠"同落点派生流"，逐单位目标点随消息下发。⚠ 待实测：`CreatUnit` 内部若还有非派生随机，复刻单位**属性**会不同。

## 7. 仍未做
- 敌人**移动纠偏**（会漂）与优先级轮询；`DieLoot`/`NPCWalk`/`MissionOilRefining`/`UnitSkill_Summoner` 仍有非派生随机；成员端血量非权威。
  家具 `ExtFloatParameter` **有意不同步**（它是"prefab 常量 + 每帧累加后清零的瞬态 + animator 层号"混用字段，结果早已由 4036/4046/4049 过网 ⇒ 口径见 `gameplay-world.md`）。
- ⚠⚠ **NetId 错位的头号来源 = "两端各自本地生成"的路径**（`CreatEnemy` / `UnitSkill_Summoner` / `EnemyNestBuild` / `UnitSkill_OrbitArmor` / `EnqueueInit` 三件套）⇒ 已由 4052 统一生成入口收口；清单、判据、复刻细节见 `net-netid.md` + `net-spawn.md`。唯一还留的原则：**别把 SpecUnit 塞进敌人计数器**。
- ⚠ **SpecUnit 炮塔/探照灯完全没有伤害与死亡同步**：`Actor.type = 8`（`Turret_*.prefab` 8 个 + `LampTower`）⇒ `NetId == 0` ⇒ `EnemyController._OnDamaged` 只打 `[异常/本地命中结算] 这只怪没有 NetId`，`EnemyNetBridge.OnLocalDeath` 有 `NetId == 0` 门、`FindActor` 也只查 `ActorsManager.Enemys` ⇒ 两端血量/生死各算各的（守方任务按 `OnDie` 计数 ⇒ 任务判定也可能分叉）。建议沿用 4043 的 `FNV1a(Id+位置)` 键只同步死亡。
