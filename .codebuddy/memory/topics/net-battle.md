# 主题 · 联机 · 战斗同步 / 种子确定性 / 消息号表

> 由 `net.md` 拆出（2026-10-11）。预算 ≤ 8000 字符。同伴：`net.md`（传输/库）、`net-flow.md`（房间/流程/名单）。
> ⚠ 原文（含每条实测与行号）已下沉：`archive/topics-snapshot-20261011/net.md`。

## 1. 世界模型（先定口径）
- 现状 = **两端各跑一份世界** + 房主权威关键量。**不做** lockstep（PhysX + 动画/补间 ⇒ 跨机确定性拿不到）；**不做**子弹权威（各端本地模拟 + 命中上报）。
- **路线（2026-10-07 定调）**：C3 玩家间命中 → C1 波次权威 → **B 敌人权威 + 快照/纠偏**（终局 = "确定性影子模拟 + 意图下发 + 纠偏"：成员跑同一份 AI，房主只发 `EnemyTarget{netId,targetSid}` / 死亡命中事件 / `EnemyCorrect{netId,pos,hp}`）。⚠ **主机 CPU 才是真成本**（带宽 30 怪 × 20B × 20Hz ≈ 12KB/s 够用）⇒ AI LOD + 不做子弹权威。
- ⚠ **"开火同步"≠"命中/血量同步"**：`FpsHelper.Hit` 是**本地**子弹碰撞结算入口 ⇒ 远端开火（4015/4016）刻意不生成子弹、不结算；玩家血量**各自机器本地权威**，同步只为"给别人看"（盟友 `PlayerFriend.prefab` 刻意**没有 `Damageable`**，血量由 4017/4018 镜像进其 `HealthPlayer`）。

## 2. 消息号表（`07NetGame/CmdId.cs`）
| 号 | 名称 | 方向 | 键 / 要点 |
|---|---|---|---|
| 4001/4002 | JoinRoomReq/Rsp | 成员→房主 | |
| 4003 | LeaveRoomNtf | 成员→房主 | `Leave()` 里要同时 `_wasInRoom=false` |
| 4004 | PlayerListSync | 房主→全体 | **战斗期冻结** |
| 4005 | ReadyState | 成员→房主 | 准备位只能由它改 |
| 4006 | TaskConfirm | 房主→全体 | 含 `TaskCfgDto`（以内容为准）+ 指纹 |
| 4007 | PlayerProfileNtf | 成员→房主 | 入房成功即发（否则盟友没身体） |
| 4008/4009 | PlayerArmament Ntf/Sync | | 带 `Sid` |
| 4010/4011 | PlayerBooster Ntf/Sync | | 带 `Sid` |
| 4012 | Transition | 房主→全体 | `MatchId`；成员只推 `GameRoot.GameState` |
| 4013/4014 | PlayerWeaponSwitch Ntf/Sync | | 槽位口径 = `PlayerWeaponsManager.SlotOf` |
| 4015/4016 | PlayerShoot Ntf/Sync | | **只做表现**；带 `HitX/Y/Z` + `DamageIndex`(Key 8) |
| 4017/4018 | PlayerVital Ntf/Sync | | 血/盾/倒地，传"值 + 上限" |
| 4019/4020 | PlayerAmmo Ntf/Sync | | 弹药**比例**（独立通道、10Hz + 变化判定） |
| 4021/4022 | SceneActorReq/Ntf | 成员↔房主 | 场景 actor 快照 |
| 4023 | WaveStartSync | 房主→全体 | `waveIndex` + 参数（**成员不在本地开波**） |
| 4024 | EnemyMoveSync | 房主→全体 | `NetId` + 目标点（意图式） |
| 4025 | EnemyHitNtf | 成员→房主 | 房主用 `DamagePacket` 结算 |
| 4026 | EnemyDiedSync | 房主→全体 | 成员 `Actor.Kill()` 干掉副本 |
| 4027/4028 | AirdropCall Ntf/Sync | | 战备 id + 落点；房主**不在 Ntf 里本地复现** |
| 4029 | PlayerLeftNtf | 房主→全体 | 只带 sid、**不重排名单** |
| 4030/4031 | Speech Ntf/Sync | | |
| 4032 | SceneUnitMoveSync | 房主→全体 | 键 = `Actor.Id`（不是 NetId） |
| 4033 | GameOverNtf | 房主→全体 | 接收端**本局只结束一次**（防两个 timer） |
| 4034 | EvacuateNtf | 房主→全体 | 撤离点 |
| 4035 | MissionUpdateNtf | 房主→全体 | 键 = `MissionBase.netOrder`，**必须带 State** |
| 4036 | FurnitureOperateNtf | 双向 | 键 = `Furniture_Attached.SyncId`；`State/StateKey` 非空 = **状态同步**（走 `ApplyRemoteState`，不重放 `Operate`） |
| 4037 | MarkNtf | 双向 | 表现类；`target` 是引用过不了网 ⇒ 传 null |
| 4038 | CallKaiNtf | 双向 | 凯伊真的走过去 |
| 4039 | WaveCenterNtf | 房主→全体 | 追击中心 ~2Hz |
| 4040 | EnemyDamagedSync | 房主→全体 | 与 4025 成对，血量口径统一在房主 |
| 4041 | LoadCompleteNtf | 成员→房主 | 本机加载完 |
| 4042 | BattleStartSync | 房主→全体 | 全体加载完 ⇒ 一起开打 |
| 4043 | SceneDestructibleNtf | 双向 | 键 = `FNV1a(Actor.Id + 位置0.1m)`；**只传"死了"** |
| 4044/4045 | PlayerLoadout Ntf/Sync | | 改装（档位/模组/载具）。⚠ 应用的**第一步**是 `FriendWeaponView.Setup`（`Instantiate` 后立刻 `ApplyUpgrade`）⇒ 若那把枪的 `LogicInit/InitAttribute` 没跑完，`WeaponCfg[type]` 返回 null、`AttrInfo` 构造当场 NRE、**整条 4045 处理被打断**（既有缺陷，2026-10-14 已加兜底+留痕：见 daily） |
| **4046** | KeyScreenStageNtf | 双向 | 终端家具 `SyncId`；**驱动端**上报、其余端只应用 |
| **4047** | PatrolSpawnNtf | 房主→全体 | 生成点 + 逐单位目标点（构成靠"同落点派生流"） |
| **4048** | OOPartSubmitNtf | 双向 | 容器家具 `SyncId` + count/type ⇒ 远端在幽灵背包上重放 |
| **4049** | KeyScreenInputNtf | 双向 | 终端 `SyncId` + `key/kind` |
| **4050** | PlayerReviveNtf | 双向 | **键 = sid**（房主 = 0）⇒ 被救者本机 `ReviveSelf()`。⚠ 不能蹭 4036：`Furniture_PlayerDown` 的 SyncId 含位置量化，盟友出生点比本人少 0.2m(Y)（`NextSpawnPos` 没带 `RawStartPoint` 的抬升）⇒ 键必然对不上；桥 `NetPlayerStateBridge`（09，`RuntimeInitializeOnLoadMethod` 自举）。⚠ 背包武器开火**复用 4015/4016**，槽位用虚拟值 7（`NetPlayerStateBridge.BagSlotIndex`） |
| **4053** | VehicleMountNtf | 双向 | **载具上下机**：键 = 载具 `Actor.NetId`（房主随 4028 分配，`ActorsManager.VehicleNetIdBase=0x3000_0000`）；值 = 驾驶者 sid + `Mounted`。**一个号双向**（同 4036 做法：房主转发 + 本地应用，桥内按角色分流）。⚠ 载具**从 4036 摘出**（见 §7.3） |
| **4054** | VehicleShootNtf | 双向 | **载具开火表现（只表现，不结算）**：键 = 载具号；值 = 武器下标 + 方向 + 准心落点 + 档位 + 发射点下标。**一个号双向**（同 4053）。上报钩子 = `VehicleWeaponsManager.OnWeaponShoot`（跟着武器生灭走，因为武器会被 `ClearWeapons+AddWeapon` 整批重建；`WeaponBaseController.OnShoot` 每次射击一次）；应用 = `WeaponBaseController.SpawnVisualSalvo`（**只表现**：摘 `FpsHelper.Hit`，保留弹道+命中特效）+ 收端按**开枪者档位**设 `UseDamageIndex`。⚠ 不复用 4015/4016（那是 `PlayerWeaponsManager` 槽位语义，背包已借 7）；丢自己那条按"**这辆车是不是我开的**"（键是载具号，没有 sid） |
| 5001/5002 | PlayerTransformUp / TransformBatchSync | 成员↔房主 | 位姿 20Hz 上行 / 15Hz 批量下行 |

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
  家具 `ExtFloatParameter` **有意不同步**（口径见 §7.2d：它是"prefab 配置常量 + 每帧累加后清零的瞬态 + animator 层号"混用的字段，结果早已由 4036/4046/4049 过网）。
- ⚠⚠ **NetId 错位的最大来源 = "两端各自本地生成敌人"的路径**（2026-10-11 Console 实证：`[异常/敌人移动·放弃] … 仍没有本端副本` + `[异常/远端伤害落地] 找不到 netId=… 的本端副本`）。**无广播、无成员闸门**的生成点：`CreatEnemy`（场景/Nest/Camp 内嵌刷怪器，24 prefab，读全局 `BattleRandom` 抽概率）、`UnitSkill_Summoner`、`EnemyNestBuild`、`UnitSkill_OrbitArmor`、`EnqueueInit` 的 `CreatSupple/CreatOOPart/CreatBuilding`。`NetId` 是**全局单调计数器**且只发给 `type==Enemy` ⇒ 任一处数量/顺序分叉，**其后所有 NetId 整体错位**。修法要么给这些点加房主闸门 + 复刻消息，要么把 NetId 改成"身份派生"（勿把 SpecUnit 塞进同一计数器）。
- ⚠ **SpecUnit 炮塔/探照灯完全没有伤害与死亡同步**：`Actor.type = 8`（`Turret_*.prefab` 8 个 + `LampTower`）⇒ `NetId == 0` ⇒ `EnemyController._OnDamaged` 只打 `[异常/本地命中结算] 这只怪没有 NetId`，`EnemyNetBridge.OnLocalDeath` 有 `NetId == 0` 门、`FindActor` 也只查 `ActorsManager.Enemys` ⇒ 两端血量/生死各算各的（守方任务按 `OnDie` 计数 ⇒ 任务判定也可能分叉）。建议沿用 4043 的 `FNV1a(Id+位置)` 键只同步死亡。

### 7.1 ✅ NetId 一律统一 + 伤害增量广播（2026-10-12 已全 8 步落地）
> 原执行交接文档 `plans/联机_NetId统一_伤害通道_执行计划.md` **已执行完并归档**（索引 `plans/archive/INDEX-plans-20261013.md`，过程见 `2026-10-12.md`）——下面留的是**设计口径**，供后续新增同步照抄。
- **号空间统一、号段分离**（保留 `int`，`FindActor` 只查 `Actors`）：玩家/盟友 = `sid` 段；**场景摆放 + 任务实体 = 派生键**
  `FNV1a(Actor.Id + 量化位置 + 同Id同位置序号)`（两端各算、零消息；`type` 是 `Other`/`Enemy` 都不影响取号）；**运行时生成 = 房主分配**（4051）。
  判据一句话：**"两端能不能算出同一个出生坐标？"** 能 ⇒ 派生；不能 ⇒ 房主分配。
- **房主唯一创建 + 复刻（4051）**：`EnemyNestBuild.ActiveBehavior`(裸 Instantiate + 全局 `RandomUtils`)、`UnitSkill_Summoner.SkillStart`、
  `CreatEnemy.Update`(`BattleRandom.Bool`) 这几条改成调**统一入口**（`CreatUnit` 加 prefab 重载）；消息带 `来源键 + 槽位下标 + NetId + pos/yaw/目标点`；
  成员复刻后**覆盖 `NetId`**（且 `Actor.Awake` 在复刻期间**不得自增**）。⚠ 场景/任务实体**不要**走这条（会欠补发全量）。
- **伤害 = 来源端先扣 → 上报房主 → 房主 `SendToAllExcept(sid)` 转发**（用户口径，取代"房主权威结算"）：消息带 `AttackerNetId` +
  `HitPartIndex`（= `Actor.Damageables[]` 稳定下标）+ **来源端算好的最终扣减量**，接收端走 `DamagePacket.Resolved = true` **不重算** AP/抗性。
  ⚠ 玩家血量仍走 `PlayerVital`（自己权威）；死亡仍由房主发 4026。**只做这一半，不做部位全量快照**（不支持重连/中途入房）。
  ⚠ 现状缺口（≥3 人可见）：成员A 的伤害从不到达成员B（`ApplyingRemoteDamage` 把转发挡掉），本方案正好补上。
- **可选（最后）**：`Other` 可破坏物并入同一条伤害事件（升级 4043「只传死了」）。
- ⚠ 开工第一步要先确认：`NetHostSvc`/库侧**有没有 "SendToAllExcept"**（现只见到 `SendToAll`/`SendToSession`）；没有就按 sid 列表逐个 `SendToSession`。
- **✅ 批次 2 执行清单（2026-10-12 第 4~7 步已落地；1~3 步见下）**
  1. ~~`ActorsManager`：加"场景/派生段"常量 + `DerivedNetId(...)`~~ ✅**已落地（2026-10-11，Unity 编译 0 error + 反射核验 True）**：
     `DerivedNetIdBase = 0x4000_0000` + `DerivedNetId(string id, Vector3 pos)`（内部数"同 Id 同量化位置的第几个"）+ 私有 `Fnv1a`。
     ⚠ 调用点必须在 `Actor.Awake` 里、**早于自己 Add 进 `Actors`**（否则会把自己数进去）；`Unimportant` 的 SpecUnit 仍不发号。
  2. ✅**已落地（2026-10-12，Unity 编译 0 error + 反射核验 True）**：`Actor.Awake` 判据改为**"战斗开始前/后"**
     （`BattleState.IsStartBattle`）：开始前 ⇒ `DerivedNetId(Id, pos)`（场景摆放/大厅，从此免疫任何号错位）；
     开始后 ⇒ `type==Enemy` 走 `NextEnemyNetId()`、`SpecUnit`(非 Unimportant) 走 `NextSpecUnitNetId()`；
     `Player/Friend` **不取号**（身份=sid）。⚠ **遗留**：任务实体（`MissionBase.InitMission` 在战斗开始**后**创建）
     目前落进"运行时生成"档（自增号）⇒ 待补：在 `MissionBase` 生成后给子树里的 Actor 显式赋 `DerivedNetId`
     （`NetId` 是 `{get;set;}`，可后赋），或并入 4051。
  2b. （原表述，已由 2 取代）`Actor.Awake` 号来源分三档：`Player/Friend → sid`（不取号）；**场景/任务实体 → 派生键**
     `FNV1a(Actor.Id + 量化位置 + 同Id同位置序号)`（序号需"同组计数器"，在 `Actors` 里按同 Id+量化位置数一遍即可）；
     运行时生成 → 房主分配（复刻期间**不自增**：加一个 `static bool IsReplicating` 门）；
  3. ✅**已落地（2026-10-12，Unity 编译 0 error + 反射核验 True）**：新增 `ActorsManager.ForceDerivedNetId`（静态开关）+
     `MissionBase.InitMission` 用 `try/finally` 包住 `Instantiate` 置位 ⇒ 任务实体里的 `Actor` 在各自 `Awake` 取**派生号**
     （`Instantiate` 同步跑完子树所有 `Awake` 才返回；⚠ 序号口径成立的关键正是"每个 `Awake` 结束才 Add 进 `Actors`"，所以逐个数的序号是唯一且两端一致的）。
     ⚠ 记录：`MissionBase` 的命名空间是 **`FPSGame.Mission`**（不是 `FPSGame.Gameplay`），程序集 `06_Gameplay`。
  4. ✅**已落地（2026-10-12）**：统一入口 `WaveManager.CreatUnit(GameObject prefab, Vector3 pos, Quaternion rot, INetSpawner source, int slot, Vector3 target = default)`
     + 契约转发（`IBattleService.CreatUnit` / `NullBattleService` / `BattleManager.CreatUnit`）。房主（或单机）⇒ Instantiate + 上报 4051；
     **成员 ⇒ return null**（不生成、等复刻）。⚠ 与计划签名差异：多一个 `target`（4051 要带首帧目标点，且只有调用方知道目标）。
  5. ✅**已落地**：**4051 `EnemySpawnNtf`** = `CmdId.EnemySpawnNtf` + `EnemySpawnMsg`（`SourceKey/Slot/NetId/X,Y,Z/Yaw/Tx,Ty,Tz/**PrefabId**`）
     + `NetRoomFlow.SendEnemySpawn / HandleEnemySpawnNtf / static event OnEnemySpawn`；桥 `09Manager/Battle/NetEnemySpawnBridge`（`Install/Uninstall` 挂在
     `WaveManager.Awake/OnDestroy`，`Report` 打 `[同步/生成·发]`、`OnRemoteSpawn` 打 `[同步/生成·收]`，找不到生成器/复刻失败一律 `Warn`）。
  6. ✅**已落地**：三处闸门都实现 `INetSpawner`（`EnemyNestBuild` / `UnitSkill_Summoner` / `10_Effect/CreatEnemy`）——
     `SpawnSourceKey` + `OnEnable/OnDisable` 登记 `SpawnerRegistry`（⚠ 必须 OnEnable：复刻出来的单位在 `Instantiate` 内只跑过 Awake/OnEnable，Start 还没跑）+
     `ReplaySpawn(slot,netId,pos,rot,target,prefabId)`（⚠ 覆盖 `NetId` 早于设目标点；成员用 `ApplyRemoteDestination`、房主用 `SetNavDestination`）。
     `EnemyNestBuild` 顺带把 `RandomUtils.RandomTake` 换成 `EnemyRandom.For(this).RandomTake(creatData)`，槽位 = `creatData.IndexOf(item)`；
     `CreatEnemy` 房主走新 `BattleManager.CreatSpawnerUnit(tier,pos,range,this)`（槽位 = `(int)tier`，成员按 `PrefabId` 回 `WaveManager.FindTierPrefab`），
     **成员 `queue.Clear()` 完全不跑**（不抽 `BattleRandom`）。
  7. ✅ 场景摆放/任务实体**不走** 4051（两端各自创建 + 派生键）。

#### 7.2a 实装时定的口径（与计划有 3 处偏差，别推翻）
- **新增号段 `ActorsManager.HostSpawnNetIdBase = 0x2000_0000` + `NextHostSpawnNetId()`**：运行时生成**不与敌人自增段共用计数器**。
  理由：这类生成的时机由各自 AI / 计时驱动，会与波次的逐 Tick 投放**交错** ⇒ 共用会让两端计数器逐渐错开（其后所有波次单位跟着错）。
  房主侧靠 `ActorsManager.ForceHostSpawnNetId`（Instantiate 前 true / finally 复位）取号；成员侧靠 `ActorsManager.Replicating` **一个号都不领**（号随 4051 覆盖）。
- **来源键 = `NetSpawnKey.Of(go)`（05UnitCore）**：优先 `Actor.NetId`（两端已一致、且不像位置会漂）；只有"无号的场景物件"才退回 `DerivedNetId(Id/名字 + 0.1m 量化)`。
  ⚠ 计划原写"纯 `FNV1a(Actor.Id + 量化位置)`"——对**可移动生成者（召唤师）不成立**（两端位置本来就不同），实装已纠正。
- **asmdef 方向**：06 看不见 09 ⇒ 闸门走契约 `BattleHub.Current.CreatUnit(...)`；10_Effect 看不见 07 ⇒ 成员判定用 `BattleManager.IsNetMember`（新属性）。
- **复刻期间 `Actor.Awake` 不发号** ⇒ 成员不从自增段扣号，`_enemyNetIdSeq` 两端**只被波次/巡逻推进**，口径不再被生成器搅乱。
- 顺序：房主 `CreatUnit` 内**先** `SetNavDestination`（4024 先出）**再** `Report`（4051）；成员侧 4024 先落进 `EnemyNetBridge._pendingMoves`，等副本创建事件补发 ⇒ 无副作用。
- ⚠ 存量问题（本轮**未**改）：`CreatUnit(tier,…)`/`CreatSpawnerUnit` 的派生流**每次调用都用同一个种子**（`UnitPlacement*1000+waveSeq`）⇒ 同 tier 每次都抽到同一把 prefab / 同一偏移。

### 7.2b ✅ 成员端 AI LOD（2026-10-12 落地）
- 新增 `06Gameplay/AI/AiLod.cs`：`ShouldTick(IActor, bool engaged)` —— **只降敌人**（`UnitTypeEnum.Enemy`）、**只在成员端**
  （`EnemyController.RemoteDrivenMovement`）、**交战不降**（`engaged`）；档位按离**本机玩家**的距离：≤60m 每帧 / ≤120m 隔 1 帧 /
  ≤200m 隔 3 帧 / 更远隔 7 帧；⚠ 相位用 `NetId`（无号用 `IndexID`）取模错开，避免整批单位同帧一起跑/一起不跑。
- 落点两处：`AIInputBaseController.Update`（状态机决策；子类可 override `AiLodEngaged` 声明交战中 —— `EnemyMobile` 把
  `Follow/Attack/Beware` 都算交战）+ `DetectionModule.Tick`（1Hz 的 `FindUnits`+视线扫描；⚠ 必须 `return true`，返回 false 会被 TickBehaviour 摘表）。
- **不降**：远端意图落地（`SyncedNavMover`）、伤害/生死（统一通道）、状态与技能 tick（牵扯血量结算）、炮台锁头 `LateUpdate`、动画/FX；
  凯伊/炮台这类 SpecUnit 也整个不降（数量少、走动与开火各自算）。
- ⚠ 两条"为什么这么保守"的硬理由：① 敌人**开火节奏在成员端是本机算的**，而"敌人打我这个玩家"的伤害由本机权威 ⇒ 降频会让玩家少挨打；
  ② 降频**不补 deltaTime** ⇒ 帧内 `Time.deltaTime` 累加的东西（转向/计时器）在远处会变慢。⇒ 想再激进（比如成员端敌人连感知都停）得先有"敌人开火/动画也走同步"。
- 排查口：`AiLod.Enabled` 可置 false 对照；`AiLod.Dump()` 返回"降频帧占比"（execute_code 里读一次即归零）。
- ✅ **"两端 LOD 档位不同会不会不同步" —— 不会**（2026-10-12 查证）：被降频的两处**没有任何"同步量"写入** ——
  `06Gameplay/AI` 里唯一的网络写入是 `EnemyController.cs:309` 的 `BattleEventBus.EnemyMove`，它在 `SetNavDestination` 内部，
  而成员端该方法在 `RemoteDrivenMovement` 处**提前 return**；`DetectionModule.Tick` 只改本地 `Target`。
  同步量各走各的路且都不降：位置（`SyncedNavMover` + agent）、血量（伤害事件 + `Health.Update` 里 `_AboTick=0.5s` 的异常状态 tick、**不在 AI 里**）、生死（`HandleDeath`）、生成（4023/4047/4051）、攻击节奏（交战态不降）。
  ⚠ 扩 LOD 范围的 3 条检查：① 被降代码里不能有网络发送；② 不能写权威量（位置/血量/生死/生成/阶段）；③ 会用 `Time.deltaTime` 累加且结果写回权威的计时器不能降。
- 验证：离线编译 0 error（含 `tmp_compile_newfile.py` 把新文件塞进临时 csproj）+ dll 反射核验（`AiLod.ShouldTick/Dump`、`EnemyMobile.get_AiLodEngaged`）。

### 7.2c 口径确认（用户 2026-10-12 拍板，**别再当缺口/待复核**）
- **不支持重连 / 中途入房**是**设计如此**（不是欠账）⇒ 除 4043 的 `_destroyed` 账本外不需要全量快照；别再加"补发全量"的提议。
- **`MissionController` 的任务/兴趣点布局是静态同步的**（用户确认）⇒ 不必再去复核它用共享 `BattleRandom` / `RandomUtils` 那几处。
- **场景 NPC 的 4032 键用 `Actor.Id` 没问题**（同名不会重复，用户确认）⇒ 不用改成复合键。

### 7.2d `Furniture_Attached.ExtFloatParameter` 为什么不同步（2026-10-12 查证）
- 定义：`Furniture_Attached.cs:89`（`[InspectorName("外部浮点数参数")]`）—— **一个 float 被 5 处按 5 种语义复用**。
- 全仓**只有两处写**（其余全是读 ⇒ 那些是 prefab 上配的常量）：`KeyScreenControl.cs:401/407`（旋钮 `+= 10*deltaTime` 累加）、
  `Furniture_General.cs:139-143`（`SignaTower`：把累加值当"本帧转角"，`relatedTrans.Rotate(...)` 后 `= 0`）。
- 五处用法：① `Furniture_Artillery.cs:80` 放置上限 / `:144` 炮弹下标·伤害档位（读）；② `OOPart.cs:27` 采集数量（读）；
  ③ `Furniture_General.cs:149` `Animator.Play` 的**层号**（读）；④ KeyScreen 旋钮累加量（写）；⑤ SignaTower 转角（写+清零）。
- 结论：**不同步是对的**。①②③ 是 prefab 常量（两端同一个资产 ⇒ 天生一致）；④⑤ 是"每帧累加、读后清零"的**瞬态输入积分** ——
  同步它得每帧发消息，且丢一帧/频率不同就永远对不上角度。而**结果**早就在网上了：终端按键 4049（远端把键盘换成远端按键、跑同一套
  `DirectionUpdate` ⇒ 累加量自然算出同一份）、家具交互 4036（`SyncId` + `Operate()` 重放）、任务阶段 4046。
- 真正的坏味道不是"没同步"，而是**一个字段混 5 种语义**（角度/个数/animator 层号/上限）⇒ 将来要拆就按类型拆成具名字段
  （`ShellLimit`/`ShellIndex`/`CollectCount`/`AnimatorLayer`/`RotationDelta`）。

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

### 7.2f 复核结论（2026-10-12）
- ✅ **玩家部署物（地雷/哨戒炮）不是缺口**：全部由 `10_Effect/VFX/VFXAirdropEffect.cs` 创建 —— 远端走 `TmpAirdrop`（`BattleManager.ReleaseAirdrop` + `SetCallerSid`），
  本机走 `SetOwner` 抛 `BattleEventBus.Airdrop` ⇒ 上报 4027 / 房主 4028 广播 ⇒ **两端各自 Instantiate 同一发战备的 `creatObect`**（落点/战备 id 已过网）。
  残留：落点取各自的地面射线命中点 ⇒ 地形被炸过后会略有差异（与"地形破坏不同步"同源）。
- ✅ **`EnqueueInit` 三件套复核**：`CreateBuilding` **无随机**（位置/旋转取 transform、prefab 由 `data.Get(TaskManager.Instance.EnemyVarietyType)` 决定）⇒ 两端一致，不用改；
  `CreatOOPart` / `CreateSupple` 有随机（`RandomUtils.RandomVector2` 全局流 + `BattleRandom.Bool` + `prefabs.RandomTake`）⇒ **已改**
  `SeedUtil.NewPlacementStream(seed, SeedStream.PropSpawn, transform.position)`（按生成器坐标量化派生 ⇒ 两端同落点/同数量/同 prefab），
  顺带修掉"生成物在战斗前创建 ⇒ 取**派生号**（含位置）⇒ 落点一分叉号就错"的老问题。
- ✅ **`DieLoot` 已静态同步**：掉落数量从 `UnityEngine.Random` 改成 `Derive(Derive(seed, SeedStream.DieLoot), NetId)`（在 `Start` 里算，
  因为同 GameObject 上 Actor/DieLoot 的 Awake 顺序不保证）⇒ 两端数量一致、零消息；位置本来就是常量偏移。
- ⏳ **`UnitSkill_OrbitArmor`（无敌装甲）**：用户问"伤害能同步，装甲状态就能同步吧？" ⇒ **成立，但有两个前提**：
  ① 装甲单位要有**两端一致的号**（现在 `:71` 裸 `Instantiate` ⇒ 必须并进统一生成入口 / 合并后的 `HostSpawn`）；
  ② 主单位的 `PartController.AddInvincibleArmor` 是**本地列表**（`:77`）⇒ 成员端复刻时也必须把装甲注册进它那份 PartController。
  齐了之后：装甲掉血/死亡两端一致 ⇒ `OnAllInvincibleArmorDestroyed` 两端同时触发 ⇒ 无敌状态自然一致。
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

### 7.3 🚗 载具（2026-10-14 P0/P1 落地 · 计划 `plans/联机_载具系统同步_执行计划.md`）
- **事实更正（计划行号是错的）**：`ADSO_O_Exosuit.deliveryType = 3` = **Medivac 运输机**
  （`AirdropDeliveryEnum`：Pod0/Bomb1/Jet2/Medivac3）⇒ 机甲在 `VFXAirdropEffect.StartMedivac` 生成；
  `Furniture_Vehicle` 挂在 **Exosuit 的子物体**上（`relatedTrans` = 座位、`relatedTrans2` = 载具根）。`Turret_Manual_AT` 也用它。
- **P0 号一致**：4027/4028 追加 `[Key(6)] int VehicleNetId`（只许追加）；房主分配，号段
  `ActorsManager.VehicleNetIdBase = 0x3000_0000`；⚠ **07 看不见 05** ⇒ `NetHostSvc.VehicleNetIdAllocator` 由 09 的桥**委托注入**。
  `VFXAirdropEffect.SpawnCreatObject` 把 **5 处** `Instantiate(creatObect)`（373/455/515/551/595）统一收口：
  `ActorsManager.Replicating` 包住（**不领自增号**）→ 生成后盖章根 `SpecUnit Actor`；`_vehicleNetId` 三态（-1 不托管 / 0 待房主定号 / >0 已定）。
  成员自己那发先 0，收到 4028 按 FIFO（`NetFriendBridge._pendingVehicleBeacons`）补号。
- **P1 上下机**：**4053 单号双向**（同 4036 走法）。⚠ **载具必须从 4036 摘出** —— 4036 的远端重放会走
  `Furniture_Vehicle.Operate()` ⇒ `VehicleController.SetOwener(盟友实例)`，而那条路硬要 `PlayerController` ⇒ **NRE**。
  远端改走 `Furniture_Vehicle.ApplyRemoteMount`（挂点 + 摘父级，**刻意不置 `inOperate`**）+ `FriendController.SetVehicleRide`
  （停 `NetTransformView` 免得被位姿拽回地面 + 压停移动动画 免得在舱里狂奔）。上报点 = `SetOwener` 抛
  `BattleEventBus.OnVehicleOwnerChanged`（06 看不见 09）；桥 = `09Manager/Battle/NetVehicleBridge`（自举常驻）。
  ⚠ **远端挂点必须落"座位终点"而不是"入场点"（2026-10-14 修）**：本地 `Furniture_Vehicle.Operate()` 先把驾驶者放到
  入场点 `transform.TransformPoint(0,0,1)`，随后 `InOperateUpdate()` 在 1 秒内 Slerp/Lerp 贴到 `relatedTrans.position/rotation`
  ⇒ 远端 `ApplyRemoteMount` 应**直接落 `relatedTrans`**（Exosuit 实测：座位点相对载具根 `(0,2.638,-0.030)` vs
  入场点 `(0,3.430,-1.045)`，**差 1.287 米**；错了就是"客机上骑手站在机甲顶上偏后"）。
  ⚠ `relatedTrans`/`relatedTrans2` 都在**骨骼**层级下（FV 挂在 `bone_Pelvis/Back`）⇒ 挂点坐标随动画动，
  远端只在"上车那一刻采样一次并烘焙到载具根下"（本地同）。**自检口径**：`载具/上机·收` 的 `局部偏移=` 应≈`(0,2.64,-0.03)`。
  ⚠ **驾驶者本人死亡也必须下车（2026-10-14 修）**：原来 `VehicleController` 只订阅**车自己**的 `Health.OnDie`
  ⇒ 人被打死时 `_owner` 还非空、死人还能接着开。修 = 上车时**额外**订阅驾驶者的 `IHealth.OnDie`，仍走唯一收口
  `SetOwener(null)`（于是 4053 自动上报"下机"，**不需要新消息**）；但**不能**直接复用那套还原 ——
  它会把 `ActorState` 刷回 `Normal`（把倒地的人拽出死亡态）、重开武器相机（死亡镜头上叠武器画面）⇒
  用"只在状态是 `Hide` 时还原 `ActorState`" + `_ownerDead` 守卫解决。同时 `Furniture_Vehicle` 侧也要清
  `inOperate/owner`（否则死后按 F 会走 `Operate()` 把人搬到"入场点"）。
  ⭐ **交互总闸（2026-10-14）**：`PlayerOperationController.CanInteract()` —— `IsDead` 或
  `ActorState ∈ {Dead, Hide}` 时**一帧都不给交互**（早退还要清 `target/Press/音频/InOperation/ForceAim/第三人称距离`）。
  ⚠ 这条**不能**写进 `PlayerInputHandler.CanProcessInput`：倒地呼叫（跳键）也走它，会被一起掐掉。
  全工程只有 `PlayerOperationController` 消费玩家的 `GetOperate*`（载具/炮位的 `TryExit` 用它们**自己**的
  InputHandler）⇒ 一处即总闸。⚠ `EmplacementController`（炮位）与 `VehicleController` 同病：**都没接驾驶者死亡**。

- ⭐ **炮位 = 载具的同一套结构（2026-10-14 落地）**：`Turret_Manual_AT.prefab`（空投手动炮台）与 Exosuit 同型 ——
  根 = `Actor(**SpecUnit**)` + `EmplacementController` + `VehicleWeaponsManager`，`Furniture_Vehicle` 在 `Back`，
  `relatedTrans=Point` / `relatedTrans2=`根 ⇒ **上下机复用 4053、开火复用 4054**，只要：
  ① `EmplacementController.SetOwener` 末尾抛 **`BattleEventBus.VehicleOwnerChanged`**（原来漏了 ⇒ 炮位完全不上报）；
  ② 桥从"只认机甲"放宽：`FindVehicle`/`RideInfo.Vehicle` 用共同基类 **`BaseSelfController`**；
  ③ 炮台是 `BaseSelfController`（**只转不移动**）⇒ `RideInfo.FixedPos = true`，P2 推导位**只推 yaw、位置原样**
  （拿驾驶者位姿推位置会把炮台抖起来）。
  ⚠ 4036 不冲突：`NetFurnitureBridge.OnLocalOperate` 早按 `furn is Furniture_Vehicle` 摘出（炮台共用同一组件）。
  ⚠ 新增可驾驶物（`IDrivable`）的**检查清单**：① 在 `SetOwener` 收口里抛 `VehicleOwnerChanged`；
  ② 桥的 `FindVehicle`/`RideInfo` 能否认它（共同基类）；③ 它是"移动"还是"只转"（`FixedPos`）；
  ④ 驾驶者死亡要下车（订阅 `IHealth.OnDie` → `SetOwener(null)` + 死亡守卫：`ActorState` 只在 `Hide` 时还原、
     别开武器相机）；⑤ 它有 `VehicleWeaponsManager` 才有 4054。
- **P2 位姿（零新消息，2026-10-14 落地）**：载具位姿 = 驾驶者位姿 ⊗ **座位在载具根下的局部偏移⁻¹**
  （`NetVehicleBridge.TryDriveVehiclePose`，由 `NetFriendBridge.HandlePoses` 每条先问，true 就不写盟友实例）。
  载具根取 **`vehicle.transform`（Actor/VehicleController 所在物体）**，不取 `relatedTrans2`（Exosuit 上二者相同，
  但炮台未必）；上车头 0.35s 插值贴合。
  ⭐ **远端必须冻结 `VehicleController`**（它 `Update` 里跑 `GroundCheck` + 重力 `Move`，会和推导位打架 ⇒ 抖动/下沉）；
  下车或被本机抢开时还原（`EvictRideOfVehicle`）。
  ⚠ **三处实测修正（2026-10-14 用户双端验收后）**：
  ① **绝不能直写 `transform`**：20Hz 直写 = "每个包跳一下"（用户原话"高频短距离位移"）⇒ 载具根挂 **`NetTransformView`**
  接收推导位做缓冲插值（下车**必须关掉**，否则它把载具钉在最后一帧位姿上、与回来的运动栈打架）；
  ② 由此**只推 yaw**：座位（`relatedTrans`）常带俯仰/翻滚（Exosuit 的 `Point` 是 180° 绕斜轴）⇒
  `RideInfo` 存 `SeatYawOffset`（**不是**整份四元数，整份反解会把机甲拧歪，本地载具根恒 yaw-only）；
  ③ 冻结运动栈会**连带掐掉走路动画**（动画就挂在 `BaseSelfMoveableController.LateUpdate` 里读 `CharacterVelocity`）
  ⇒ 载具根另挂 `NetVehicleRemoteMotion`（按每帧位移差分驱动 `IsMove`/`Speed`，口径与本地逐字一致）。
- ⭐ **骑手是载具的"子物体" ⇒ 载具一销毁就把他带走（2026-10-14 实测"客机看不见房主"的根因）**：
  载具 prefab 的 `Health` 勾了 `AutoDestroy`（`HandleDeath` 末句 `Destroy(gameObject)`；本端副本由 4040 判死那条
  `EnemyNetBridge.ApplyRemoteDeath → IHealth.Kill()` 触发）⇒ 挂在其下的盟友实例被**连带销毁**，
  而 `Actor.OnDestroy` 只让 `NetFriendBridge.HandleFriendLeave` **静默**摘字典 ⇒ 单方向永久消失
  （重建时机只有"名单同步 / 换场景"，战斗期名单冻结 ⇒ 都不来）。
  ⚠ 别指望 `VehicleController.OnDie → SetOwener(null)`：它只在 `SetOwener` 的 **owner 分支**里订阅，
  而远端副本在本端**从来没有 owner** ⇒ 那条路不触发（本地那侧靠它自己摘父级，所以只有客机坏）。
  修（两层）：① `NetVehicleBridge` 订阅 `UnitEventBus.OnUnitDeath` → `RescueRiders`（载具将死先摘骑手；
  时机安全：`OnDie?.Invoke` 在前、`Destroy` 帧末延迟执行）② `NetFriendBridge` 每 0.5s `ReconcileFriends` 自愈
  + `RebindRide` 重接载具绑定（对付任何意外销毁，不只载具）。
- ⭐ **运输机投放：机甲必须"卸下"才不会丢（2026-10-14 实测"客机第一台机甲没落下来"）**：
  机甲生成即挂成运输机（`NeoNimbus_Vehicle`）的**子物体**，`UpdateMedivac` 在 `data.State == Sustain` 时 `parent = null`；
  ⚠ 本机自己叫的那发 `data` 就是**战备槽位那份共享 `AirdropData`**（`SetOrigin` 里直接赋值）⇒ **连续呼叫同一战备**时
  第二发把它重置回 `Arrive`，第一发永远等不到 `Sustain` ⇒ 机甲一直挂在运输机下、随运输机回池一起消失（"没落下来/不存在"）。
  远端重放是**独立副本**（`TmpAirdrop → new AirdropData` + `isTmp` 自走）⇒ 只有**呼叫端**会丢 ⇒ 两端机甲数不一致（房主 3 / 客机 2）。
  修：卸载判据加 `showTime >= arriveTime` 兜底 + `Init` 复位 `showTime` + `EndMedivac` 就地卸下兜底。

- **"房主强关"客机怎么发现（2026-10-14 修）**：⭐ **只信 `NetSvc.IsConnected` 是不够的** ——
  它等于 KCP 库 `clientSession.IsConnected()`，而房主进程被**强杀**时 **UDP 没有 FIN**，库不一定会（及时）翻成断开
  ⇒ 客机侧 `NetRoomFlow.DetectHostLost` 永不触发 ⇒ **房主的盟友模型 + `PlayerWnd` 队友行会一直挂着**
  （两个症状同时出现 = `OnHostLost` 没触发，别再分别去查模型/UI）。
  修法 = **心跳静默看门狗**（`NetClient.IsHostSilent`：发 Ping 时记 deadline、收到任何 Pong 清掉，默认 5s/心跳 2s）
  ⇒ `DetectHostLost` 判"活着"改成 `IsConnected && !IsHostSilent`，且静默时先 `svc.Disconnect()` 再走清理。
  ⚠ 队友 HUD 行的数据源 = **`ActorsManager.Players`**（`PlayerWnd.UpdatePlayerStates` 每帧遍历 ⇒ 实例一销毁就自愈），
  但 **`TeamManager.players`**（战备/结算界面按它排行 + `IdToIndex`）**不会自己收缩** ⇒
  `TeamNetBridge` 也必须订阅 `OnHostLost` 收敛成"仅本机"（否则回舰桥会看到幽灵行）。

- **载具外观（皮肤/武器）的联机口径与"会被盖掉"的坑（2026-10-14 实测）**：
  外观由 `BattleApplyVehicleData` 按**归属 sid**（`VFXAirdropEffect.StampCaller` 用 `CallerSid` 打标）
  查 `VehicleCustomState.TryGet(sid, vehicleName)`；⚠ `TryGet` 明确"该 sid 有同步表却缺这辆车 ⇒ **不回退**"，
  所以**任何地方都不许再兜一层本机存档**（否则就是"用我的配置冒充别人的"，实战表现 = 两端皮肤都变成本机那个）。
  ⭐ **皮肤写在 `MaterialPropertyBlock` 上（`MpbController`，`SetPropertyBlock` 整块替换语义）**
  ⇒ 会被别的表现系统整块盖掉：受击闪白/溶解（`FxControllerBase` 的 `RendererSlot`，按材质下标写块）
  与**诞生材质**（`EnemyControllerFX`：`BirthDuration` 内把 `sharedMaterials` 换成 `BirthMaterial`，
  到点 `RestoreMat()` 整块写回）。空投 Medivac 的"落地卸载"（`UpdateMedivac`）正好落在诞生材质还原的时间点附近
  ⇒ 实测"天上挂运输机时皮肤是对的、落地后变回默认"。**对策 = 表现切换点强制重贴**
  （`BattleApplyVehicleData.ForceApply`，空投落地那刻由 `VFXAirdropEffect` 调；`Apply()` 有"无变化跳过"，救不回来）。
  排查这类问题时用 `BattleApplyVehicleData.LogAppearance(tag)` 打"渲染器上真正生效的材质 + MPB 里的 `_BaseMap`"。
  ⭐ **修法（2026-10-14 定死）**：皮肤必须写进**与受击/溶解同一个共享槽位块** ——
  `FxControllerBase.GetSharedSlotBlock(r,index)` 拿到块 → 写 `_BaseMap`/`_BlendingMap`/`_BlendingScale` →
  `MarkSlotDirty`。理由：`SetPropertyBlock(block)`（渲染器级）与 `SetPropertyBlock(block,index)`（按下标）
  是**两套存储**，`RendererSlot.Flush()` 用按下标那份 ⇒ 一挨打就把渲染器级那份**永久顶掉**
  （实测：空投落地摔一下触发闪白，机甲皮肤从此变回默认）。⚠ 槽位表在 `FxControllerBase.Start` 才建，
  而载具 `Awake` 就贴外观 ⇒ 必须在 `Update` 里低频补盖（`_slotStampRetry`）。
  ⚠ 判"皮肤不对"前先分清是**配置查错**（应用日志里的 `皮肤=N` 与舰桥设置不符）还是**被盖掉**
  （应用日志对、但探针的 `逐下标[idxX=无(受击槽)]`）；探针只列**本体**渲染器（粒子/拖尾要跳过，
  否则日志会被 `VFX_HitSparks` 占满）。
  ⚠⚠ **"改装配置里没有武器"≠"该清掉武器"（2026-10-15 修，实测坑）**：`ApplyCustom` 原来在 `rebuildWeapons` 时
  **无条件** `_manager.ClearWeapons()` 再按 `data.weaponRights/Lefts` 重建；而炮台/运输机/补给仓/撤离信标这些的
  `VehicleData_SO`（`Vehicle_Turret`＝"雷酱" 等）**两个数组本来就是空**（整个工程只有 Exosuit 是 4/4）
  ⇒ 清完一把都不加 ⇒ 载具变"无武器"，连锁反应很唬人：`VehicleWeaponsManager.HaveWeapon()==false` ⇒
  ① 左右键的射击/瞄准分支（`else if (HaveWeapon())`）整段进不去 ② `OnSetOwner` 里走 `OnStateChange(false)`
  ⇒ **载具 UI 与准星根本不出现**（实测：空投 `Turret_Manual_AT` 上机后无 UI/无准星/LMB/RMB 全无反应，而机甲完全正常）。
  这些车的武器是**预制体预置**的（`Turret_Manual_AT` 自带 `Weapon_Rocket`），且它们**没配** `weaponPointR/L`
  （`Instantiate(go, null)` 会挂到场景根）⇒ 修法 = **配置无武器时跳过清空/重建**（`cfgHasWeapon = R>0||L>0`），
  **不是**往数据资产里塞武器。判据速查：探针看 `VehicleWeaponsManager.m_WeaponSlots.Length` + `BattleApplyVehicleData._applied`。
- **P3 血量/死亡（房主权威，2026-10-14 落地）**：**没有新消息**（复用 4025/4040）。
  ⭐ 关键查证：`EnemyNetBridge` 的应用路径按 `NetId` 在 `Actors` **全表找、不按类型过滤** ⇒ 载具天然吃得下 4040 与死亡；
  缺的只有 ① 上行（载具无 `EnemyController`）② 权威门。口径 = **AI 来源的载具伤害只有房主落地并广播；
  玩家侧来源（Player/Friend；部署物算 AI）照样落地 + 上报**；门插在 `Damageable.LocalDamageGate`（09 注入的委托，
  05 看不见 06/07/09）——返回 false ⇒ 不扣血/不死/**也不派发 `UnitDamaged`**（⇒ 自动不上报）。
  权威位 `BattleState.IsVehicleDamageAuthority`（**与 `IsTickDamageAuthority` 分两位**）；
  桥 = `09Manager/Battle/NetVehicleDamageBridge`（`WaveManager` Install/Uninstall）；上报要带**原始成分 + 部位下标**。
- **P4 开火表现（4054，2026-10-14 落地）**；**P5 三条收尾（2026-10-15）**：
  - ⭐ **一车一人**：原判据只看**本端** `inOperate`（两边各判各的）⇒ "a 上了 b 也能上、两人同车"。
    口径 = **房主唯一裁决 + 先到先得 + 成员永远让**：桥新增占用表 `_drivers`（载具号→驾驶者 sid，本地/远端都写），
    收到别人上机而占用者在案 ⇒ **房主拒绝**该 mount 并把占用者重播回去；**成员**若本机正开着这辆则
    `Furniture_Vehicle.ForceLocalExit()` 让车后再挂占用者；**非占用者报的下机**只清本端、不转发（否则回声把占用清了）。
    本地入口闸 = `Furniture_Vehicle._remoteOccupied`（`ApplyMount/ApplyUnmount` 推）→ `CanOperate` false（连交互提示都收起）。
    ⚠ 只标**远端**骑手：本机自己开时不置位，否则"退出驾驶"提示会被 `OperationWnd` 收掉。
  - ⭐ **弹量随发（4054 载荷末尾 +2 项）**：载具武器两端各一份实例 ⇒ 弹量天然分叉（实测 a 打完下车、b 上车看到满弹）。
    权威 = **驾驶者那台**；`VehicleShootMsg[Key(10/11)] Magazine/Ammo`，发端用
    `VehicleWeaponsManager.TryGetAmmoAfterShot`（⚠ `OnShoot` 是在 `HandleShoot()` 里抛的，`UseMagazine` 还在它后面 ⇒
    直接读会**永远差一发**；折算要 `WeaponBaseController.AmmoPerShot`），收端 `ApplyRemoteAmmo`。
  - ⭐ **"死了不下车"的真凶 = 共享 `OnDie` 列表被前面的订阅者打断**：驾驶者那份 Health 的 `OnDie` 上挂着
    Actor/PlayerController/玩家窗/HUD 一圈；探针实测打出**目标已销毁仍挂在表里**的 `Furniture_Vehicle.OnRiderDie@<noObj>`
    （它排在 `VehicleController.OnDriverDie` **前面**）⇒ 一旦抛异常，后面整段不执行。两层修：
    ① `VehicleController/Furniture_Vehicle` 在 `OnDisable` 退订驾驶者 `OnDie`（`OnEnable` 再挂回）；
    ② `VehicleController.LateUpdate` 加**轮询兜底** `_ownerHealth.GetHpCurrent() <= 0 ⇒ 走同一收口 SetOwener(null)`
    （⚠ 判 `IHealth` 存活要先 `as UnityEngine.Object`，接口引用不吃 Unity 假 null）。
    另：**远端骑手死亡**本机无人收口（他自己的 Health 在他那台）⇒ 桥加 `EvictRiderOfActor`（`OnUnitDeath` 里摘人 + 清占用）。
