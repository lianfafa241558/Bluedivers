# Bluedivers 长期记忆（索引）

> **只留结论与坑；过程/代码细节见各日 `YYYY-MM-DD.md`。** 2026-09-28 第四次压缩（去重+精简，内容未删要点）。

## 协作与通用坑
- 不确定先问；不动无关 using；改名/移动由用户**手动**做，AI 只给方案
- 问"能不能/为什么没有"→ 先**结论+证据(文件:行)**，再选项与代价，最后才谈落地
- 双机：换机后先确认 MCP 连的是当前副本，再动写操作
- 诊断顺序：先问「**有没有人调它**」→ 看**实际资产/prefab 数值** → 最后看逻辑；能用运行时反射就别只靠读代码
- Inspector 特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 顶掉全局 fallback
- 资产 YAML 缺整行字段 = 脚本新增后资产未重存；Unity **保留 C# 字段初始化器的值** → 新增非 0 默认值字段安全；往**结构体末尾**加字段最稳
- ⚠ 别什么都往 `BattleEventSub` 塞：能拿实例就订阅**实例事件**；跨实例状态别用 `static`（例外＝需"记住最后值给晚订阅者"的初始广播）
- 新增脚本尽量**运行时自动挂载**，少改场景/prefab YAML；必须手改 prefab 要明确告知；改 URP renderer 资产走 MCP `SerializedObject`
- ⚠ 新增 `const`/`static` 放**类顶部**（规范硬要求）
- ⚠ **文本搜索不可全信**：`git grep` 把含异常字节的 `.cs` 当二进制跳过（`02Game/Util/WindowStateController.cs`）；`Select-String` 也会漏（`PlayerWnd.BulletHit` 源码里有、扫描找不到）→ 关键结论用**运行时反射/控制台**复核

## 环境与工具链
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；**MCP 实例 hash/端口随路径变**，实时看 `mcpforunity://instances`（2026-09-28 晚实测 `Bluedivers@4a3e6a7b`:6401；规则文件里的 `3d9f2357`:6400 已过期）
- Unity 2022.3.62f3 / URP 14.0.12 / C#9+.NET Std 2.1；单机 PvE（FPS Sample 改，`Unity.FPS.*`）；随机源统一 `BattleRandom`、确定性用 `PEMaths`
- `Assets/Scripts/Lib/PEMaths.dll`：**PEInt/PEVector3 就是"米"标量**（实测 `PEVector3.Distance(3,4,0)=5`、`(PEInt)20→20`、`RawInt==RawFloat`，无数值缩放）；`RawFloat` 换算后不可再参与运算
- git `core.autocrlf=true`；GitHub 直连被重置 → 走 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore` 的 Unity 生成目录规则必须 `/` 锚根（裸 `[Bb]uild/` 曾吞 `Packages/**/Tools/Build/`）
- 工具坑：`search_content` 的 `glob` 不可靠 → 整目录搜或单文件 `path`+`headLimit`；`findstr` 读部分 fbx 报错 → `select-string`；无 `Debug.DrawWireSphere` → `Tool.DrawWireSphere`
- 判定"代码是否真编译进去"：看**目标程序集** mtime vs 源码 mtime（`Assembly-CSharp.dll` 只装 `01Manager/`+`02Game/`+`Effect/` 等无 asmdef 目录；`00Tools/Test/*` 属 **`00_Utils.dll`**）；历史报错见 `%LOCALAPPDATA%\Unity\Editor\Editor.log`
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`

## 结构与命名
- `Assets/Scripts/` 数字前缀=依赖顺序（00Core→GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），上层可引下层，反之禁止
- asmdef 共 14 个（`00_Utils` 含 TerrainUtils、`08_Map` 含 MapUtils、`04UI/Assembly/04_UI`、`04UI/WndTool/00_WndTools`、`05_EffectComp`、`DayNightSystem`…）；`01Manager/`、`02Data/`、`02Game/`、`04UI/` **根脚本**、`Effect/*.cs`、`Rendering/*.cs` 全在 **Assembly-CSharp**；asmdef 均 `autoReferenced:true` → CSharp 可调它们、反向不行
- ⚠ **asmdef 内引用不到 Assembly-CSharp**（`08_Map` 用不了 `MapData_SO`）→ 跨层只传 `GameObject[]`/基础类型
- 存在 **01Manager↔02Game↔02Data 三角循环**（02Data 引 `FpsGame.Mission`/`Unity.FPS.Game`）→ 拆 asmdef 须先断 02Data→02Game
- 约定：跨模块优先事件；接口主流 `I_` 前缀；SO 用 `_SO`；partial 用 `主类名_分部名.cs`；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 只对**字段**有效
- 层索引：0 Default / 3 Ground / 6 Unit / 13 AirWall / 14 Smoke / 15 ModelView

## 架构与数据分层
- 组件间避免 `GetComponent` 互取；AI 用 `AIController` 组合+接口代理，**不继承 `Actor`**；竖直占位 `I_Entity.HalfHeight`（0=不过滤）；批量填 `Assets/Editor/ActorHalfHeightTool.cs`
- 窗口：`WndManager` 挂 `public XxxWnd` 字段（prefab 填 0），`Init()` 由 UnityEvent 调；家具入口 `Furniture_*.furnData` 按 `Id` 分发
- **SO 不能持有 prefab/场景实例引用**：只放与实例无关项（音效/粒子/材质/颜色/数值）；子物体/挂点放 prefab 的 `[SerializeField]`；"同类单位一起吃?"→SO，语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；需"任意组件任意方法"才用 UnityEvent；共享+需实例层级→间接引用（key/相对路径+运行时解析+编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- 家具：`Furniture_Attached : BaseMono, IFurniture`（**不改**基类静态字典），OnEnable/OnDisable 维护静态 `list`（交互扫描源）；`Handle(user)`→`CanOperate`+`Operate`；非 Actor 物体必须 override `ShowName/Id/Icon`
- **交互键（E）解析在 `PlayerOperationController.{Update→Handle}`**：`target` = 第一人称屏幕中心 Raycast(1.3m) 或第三人称 2m 内最近可交互物；有 `target` 时 E 归交互物
- 第三人称交互拉近：`PlayerController_ThirdPerson.ThirdPersonDistanceScale`(0.1~1) 乘到两分支 `distance`；`PlayerOperationController` 每帧写 `IsThirdPerson && target!=null ? 0.5f : 1f`，窗口切换还原 1
- 装备：`IEquippable` + `EquipController.Equips`；装卸只由交互家具调 `InstallEquip/UninstallEquip`；交互家具"被携带"时自身 `enabled=false` → **装备想监听输入必须写在 `IEquippable` 实现里**；`InputState.Operate`=E(5)、`Equip`=X(22)
- 计时器：`TickBehaviour.Tick()` **1 秒 1 次**（返回 false 即把自己移出 `ticks`）；`CreateTimer(cb,秒,次数,endcb)`；`CreatePerTimer`=**每帧回调**持续秒
- 波次：`BattleManager.CreatWave(WaveCreateParams)` → `RobotWave`/`ZergWave`/`KaiserWave`；皆 `I_TickClass`、1s Tick，`End` 时 `Dispose()` 返回 false 被移出；`onEnd` 在 `Dispose` 回调（续刷安全）；`centerGetter` **只影响尚未落点**的单位；ZergWave 距 `anchorCenter`>50m → `RedeployPods()` 重投

## AI / 寻路（易踩）
- AI 单位 = `EnemyController`（组合）+ `EnemyMobile`（状态机）+ `DetectionModule`（感知）；实机队伍：玩家 `team=0`，敌人 `team=2`
- `EnemyController.PatrolPos` 是**到达即自毁**哨兵点（`HomePoint` 才是到达待命）；巡逻队只能 `BattleManager.CreatPatrol(Vector3)`
- `EnemyMobile.Start` 只在**开场读一次** `PatrolPos` 决定 Patrol/Idle；`UpdateCurrentAiState` 在 `!BirthComplete` 期间整体早退（`IsMoveLocked`/`_vertigoActive`/`_terrorActive` 会冻结状态切换）
- ⚠ **`Actor.IsFixed` 的语义（用户 2026-09-28 明确）**：只表示"**不会因为长期不动被删除**"（`IdleBehavior` 跳过自毁），**不是位置固定** ⇒ 不能拿它当"这个单位不该移动"的门禁
- **枪声警惕（2026-09-28 已补全）**：`EnemyMobile.UpdateAiStateTransitions` 新增 `Idle/Patrol → Beware`，条件 = `DetectionModule.BewarePoint.HasValue` **且** 点距 `<= Mathf.Max(HearingRange, DetectionRange)`（**距离门禁必须有**，否则一枪惊动几十米外全员）；`EnterBeware` 取完点**立刻 `ClearBeware()` 消费**（防来回摆动）；`Beware` 到达或 `BewareTimeout=20s` → `Return`（原地停 `BewareStayDuration` → 回原点 → `TryReturnToIdleOrPatrol`）；Beware 期间炮塔 `CalculationAimTargrt` 水平看过去。`EnemyTurret`（不能走动）走 `BewareLookDuration=3s` 只看过去再恢复 `AutoRotate`，同样带距离门禁
- ⚠ 顺带修的两个既有 bug：① `case Return` 原先把"停留时间到 → `SetNavDestination(原点)`"写在"是否已到原点"**前面**，时间一过前者恒真 ⇒ 到达判定永不进入。**判"到达"的 `else if` 必须排在"时间到"前面**；② `OnLostTarget` 直接赋 `m_BewareDestination` 会被 `EnterBeware` 按 `BewarePoint` 覆盖 ⇒ 现统一"要去的点先写进 `DetectionModule.Beware(...)`/`SearchPoint`，`EnterBeware` 只从点取并消费"
- **警惕冷却（已做）**：`EnemyMobile.BewareCooldown=10s` + `BewareCooldownRadius=10m` —— 从上一个检查点**回到 Idle/Patrol 时**记 `m_BewareCooldownEndTime`；冷却期内"枪声点 距 `m_BewareDestination`（上次检查点）不超过半径"就无视（`m_BewareDestination==default` 时不判）。三条件收在 `ShouldInvestigateBeware()`。`EnemyTurret` 不参与冷却
- ⚠⚠ **寻路请求失败曾被永久锁死（已修）**：`SetNavDestination` 的 `<1m` 去重键 `m_lastDestination` 曾在"请求真要发出去"之前写入 ⇒ 一次失败即永不重发（特征：State=Patrol、`hasPath=false`）。**修后规则**：仅当"目标没变 **且** `pathPending||hasPath`"才跳过，否则按 `NavRetryInterval=0.5s` 节流重发（别退回无条件去重！）
- ⚠ `PathRequestManager`：`pathPending` 期间不重试，结束后判 `PathInvalid/PathPartial(只有1拐点)` → 投影重试 5 次(半径 5) → **10m 兜底**；⚠ **兜底 `SamplePosition` 失败时不许空操作**（否则 agent 停在 hasPath=false 无人再触发）→ 改为无条件 `LogWarning` + 交调用方节流
- `Tool.GetCircleIntersection` 返回 Vector2，`Vector2.ToVector3()` = `(x,0,y)`（**y 恒 0**）⇒ "几何算出来的 XZ 点"必须先补 `y = 地表高度`（`FpsHelper.GetNavMeshPoint`）或用 `spawnPos.y`，再做 `SamplePosition`，否则地形高度(38m)吃掉采样距离(25m) → 必然失败
- 地图圆半径用 `CameraSize/2`、圆心用 `MapSize/2`（不同源）⇒ 巡逻目标圆常落在无 NavMesh 的边界环带 ⇒ 巡逻点必须 `SamplePosition`(40m) 再赋值；`PatrolPos` 由 `PatrolContriller.SpawnPatrol` 统一投影，队伍散开偏移**只加 XZ**
- 敌人受力/击退：`IPhysical` = `ApplyForce`(持续力，按 **Δv=(F/m)·dt** 积分) + `ApplyImpulse`(冲量 **Δv=J/m**，无上限)；实现在 **`EnemyController_Physical.cs`**（partial，`NavMeshAgent.Move` 水平推挤、`Actor` 同物体 ⇒ 零 prefab 改动、推挤期临时关 `autoRepath` 并**还原原值**、`OnDisable` 兜底）
- ⚠ 同一 partial **不能有两个 `Update`** → `EnemyController.Update` 里手动加 `UpdateKnockback();`

## 场景加载与事件时序（易踩）
- `ResSvc.AsyncLoadScene` 用 `LoadSceneMode.Single`，**回调被 `DelayedInvoke` 延后一帧** → 场景内组件 `Start` 一定早于回调里 `AddComponent<BattleManager>()`
- ⇒ 凡"场景物体 `Start` 里只抛一次"的初始事件（如 `DayNightBrain.Start`→`GlobalEventSub.DaySwitch`）BattleManager 收不到 → 修法：总线缓存最后值 + 就绪后补发（`ApplyInitDaySwitch()`）；`BattleManager`、战备 UI 等"后创建者"都要"补初始状态"
- `BattleManager._initQueue`（`EnqueueInit`）给"早于 BattleManager 存在的物体"排队，末尾 `DrainInitQueue()` 兑现；依赖 `ADCont` 前必须判空
- ⚠⚠ **`authorizeCounter` 裸计数、无下限**（`+= state?1:-1`）⇒ **"白天"=计数 0 的自然态，绝不能在白天做 `-1`**，否则战备永久锁死

## 组件存活/回收（易踩）
- `VFXManager.Release(GameObject)` 只认根物体上的 `ParticleSystem`(Stop)/`LimitedLife`(置 `allowRelease`)，皆无=空转；`ProjectileBase` 走另一重载（回池+`Template`）
- `LimitedLife.IsAlive()` 只被 VFXManager 池 Update 轮询 → 非池化实例（含 Nest 预置）不会被回收；`HealthOther.AutoDestroy` 才是"死亡即销毁"
- `LimitedLife` **延时回收**：到寿先 `InvokeEnd()`（`endInvoked` 保证一次）再等 `EndDelay`；`allowRelease` 强制回收**不走延时**
- 池化对象"本次状态"字段必须在 OnDisable/OnEnable 复位，且放在早退 return 之前

## 昼夜与天气
- `DayNightBrain` + Modules（`TimeProgression`/`CelestialRotation`/`CelestialVisuals`/`EnvironmentLighting`/`DayNightTrigger`）；预制体 `Day-Night-Manager.prefab`（桥 Utnapishitim 与 TestScene 各一实例）；旧 `Effect/DayNightCycle.cs` 不参与玩法场景
- 渐变时间轴：**0%=日出、25%=正午、50%=日落、75%=午夜、100%=日出**
- `EnvironmentLightingModule`：环境光三色 ×`AmbientBrightnessMultiplier`；天空盒有 `_Lerp` 写 `_Lerp`、无则写 `_Exposure`（基准取组件配置，**不读材质**）；`_GroundColor` 取 `equatorColor`（凑合）；写值走 `GetSkyboxForWrite()`（**只在 `Application.isPlaying` 建副本**）
- ⚠ Procedural 天空盒只有 `_SkyTint`(乘性)/`_GroundColor`/`_AtmosphereThickness`/`_Exposure`/`_Rotation`/`_SunDisk*`，**无赤道色**；`Skybox_Test.mat` 用它、`Utnapishitim` 的 `m_Sun: 0` → 拿不到太阳方向
- **全屏雾合成**：雾色渐变 RGB=雾色×环境光倍率，**A=该时刻"雾出现程度"遮罩**；`intensity=Clamp01(昼夜曲线×渐变A + 天气 FogIntensityAdd)`（天气增量不参与遮罩）；`density=曲线/max(visibility,0.05)`；`startLine/endLine×visibility`
- 天气：`WeatherSystem`（开局按 `mapCfg.WeatherInfos` 用 `BattleRandom` 抽）+`WeatherEffect`（Rain/Desert/Snow）；`WeatherAtmosphereController`（**全局命名空间**静态桥）：选图时注入 `FogColorGradient`，天气侧写 `Target*`，消费方每帧读 `*Multiplier`

## 任务 / 战备
- `MissionBase : TickBehaviour`（1s）→ `MissionEvacuateBase` → 静态(终端 KeyScreen)/动态(凯伊 ReturnBag)；`UseSceneStartPoint` 虚开关控快速模式是否顶替 StartPoint；「创建后」走 `InitMission`，「全部初始化后」走 `StartMission`；`Link(mission)` 订阅其 `OnMissionEnd` 来激活自己；`Uninit()` 在 `EndMission`（任务完成）时就会跑 ⇒ **别把"任务完成后还要用"的订阅/清理放 Uninit**
- **次要撤离区（2026-09-28 新增，`MissionEvacuateSecondary : MissionDestroyActor`，标旗 Extra）**：静态撤离激活时由 `MissionEvacuateStatic.CallSecondaryBeacons()` 往各区中心各呼叫一个 0 号战备（`ReleaseAirdrop(pos,0,InitBeacon)`，0=撤离信标）；任一处 KeyScreen 走到最后一步＝玩家选中该点 → `HideOtherBeacons()`（其余信标 `Animator.Play("Hide")`）+ 把 `area/areaPoint/pos/beacon/keyScreen` **整体换成该区**（撤离判断随之转移）+ `StartWait()`；`OnKeyScreenStage` 加了 `stage==Activation` 门禁防二次 StartWait
  - 注入链：`MissionController` 按**类型**收集（不依赖标旗）→ 在 `evacuate.Link(main)` 之后 `staticEvacuate.SetSecondaryZones(list)`（GenerateFromData 与 FindFromScene 两条路都做了）
  - `MissionEnum.SecondaryEvacuate = 300`（**显式值**，插在 Placeholder13 后 ⇒ 不动任何既有枚举值；⚠ 别在枚举中间插隐式值，会整体错位）
  - 数据侧仍需用户建：挂该脚本的 MissionBase prefab（标旗=Extra）+ MissionData_SO（`controller` 指该 prefab）
- `MedivacController`：`Land`=插入下机；`Evacuate`=撤离接人；`TakeOff()`=Play"Evacuate"+开 cam+派发 `Complete`+**只隐藏 IsInBox 玩家**
- 主任务 `MissionCompleteKeySceern`（拼写如此）=终端流程；`MissionOilRefining`：Init 空投平台+连接点→Wait `MissionSubConnectPipes`→Start(180s+波次)→Repair→End；管道状态是 `Furniture_Pipe.Id` 字符串（`Pipe`→`PipeLink`→`PipeWait`→`PipeComplete`/`PipeError`），**无"已修复"标志** → 不能读 Id 判完成，要 Tick 轮询
- 台词只能 `WndManager.CreatNotice(角色, groupName)`，角色键=`NoticeTree_SO.ID`
- 战备：资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；`AirdropData_SO`（`type`/`labels`(`[Flags]`)/`opter`(Left0/Up1/Right2/Down3)/`subAirdrop`/`creatObect`/`coolGroup`/`isHide`）；首键严格：→轰炸/↑空中支援/↓炮台地雷补给/←载具；**ID 0 = 撤离信标**（`type=4`、`creatObect=Prefabs/Airdrop/EvacuationBeacon`、`permanentPod`）
- ⚠ `labels` 是后加字段 → 除 `ADSO_R_Railgun` 外资产 YAML 无该行(=0)；`AirdropData_SOEditor` 显式列字段，漏列不显示
- 部署：`VFXAirdropEffect` 按 `deliveryType` 分支；改 `arriveTime` 须同步 `time`；`permanentPod` 会 `LimitedLife.ResetLift(9999)`；Pod 落地回调 `OnCreatObject` 给的就是 `creatObect` 实例（信标真正本体）
- 授权：`AirdropController.Authorize(id,state)` → `+= state?1:-1`（0=隐藏且不可用）；必需战备 `TaskManager.RequiredAD = {SupplyId, HealBag, IlluminatorId(17), LampTowerId(16)}`；昼夜解锁照明只在翻转时动计数，开局事件会漏 → `ApplyInitDaySwitch()` 补发

## 战斗与伤害
- 伤害总入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸(穿甲/拆毁)→冲击波→**地形破坏**→警告→特效/音效/弹痕；末尾 `//警告` 段现在**同时发两条**：表现层 `BattleEventSub.BulletHit(soure,point,soundRadius)`（HUD 用）+ 逻辑层 `BattleEventSub.Noise(NoiseData)`；`soundRadius=damageData.GetSoundRadius(charge)`=**表现层**音效距离（**与 hitData.sfxRange 无关**）
- **逻辑层噪声体系（2026-09-28 新建，表现/逻辑彻底分开）**：契约 `NoiseData{GameObject source, PEVector3 pos, PEInt radius}` + 总线 `BattleEventSub.OnNoise/Noise()`。**生产者两条**：① `WeaponBaseController.HandleShoot()` 里 `OnShoot?.Invoke` 之后发**枪口**噪声，半径 `FireNoiseRadius`（逻辑字段，默认 20；**表现层音效距离是 `SFXRange`，两套分开，别再互相复用**）；② `FpsHelper.Hit` 的 `//警告` 段发**弹着点**噪声，半径 `DamageData.GetImpactSoundRadius(charge)`（逻辑字段 `ImpactSoundRadius`，默认 8；⚠ `UseExplode` 时取 `max(它, 伤害外半径)`）。**消费者** `DetectionModule.OnNoise`（`Start` 订阅、`OnDestroy` 退订；**已不再订阅 `OnBulletHit`**）
- ⚠ `BattleEventSub.OnBulletHit` 是**表现层**（`PlayerWnd`/`HitFlashWnd` 订阅做擦弹/受击提示），**没有**被噪声体系取代，别把它当逻辑用
- **感知"警告"链路**：`OnNoise` 条件 = `m_Actor!=null && source!=null && source.TryGetComponent<Actor>() && actor.Team!=m_Actor.Team && PEVector3.Distance(pos, m_Actor.Logic3Pos) - radius < (PEInt)HearingRange` → `Beware(pos, radius, false)`。`source` 必须与 `Actor` **同物体**（玩家/敌人武器 Owner 都是各自根物体，OK；载具/无人机/炮台 Owner 不同物体则静默失效）
- `DetectionModule` 两个点**分开**：`BewarePoint`(噪声点，带响度 `m_BewareNoise`) 与 `SearchPoint`(丢失目标的搜索**指令**)。`Beware(point, noise, spread)` 里 **`noise < m_BewareNoise` 直接 return 不覆盖**；`ClearBeware()` 连响度一起清；`EnterBeware` 按 `SearchPoint > BewarePoint > m_OriginPos` 取目的地。⚠ `I_AIController.Beware` 签名已加 `noise` 参数（`AIController`/`EnemyController` 同步）
- 敌人特效（`02Game/AI/FxCont`）：`EnemyControllerFX`（抽象 partial）+两个子类；三层配置（渲染模板 SO 按 `sharedMaterials[i]==mat` 匹配槽位 / 事件 SO / 组件字段）；**MPB 所有权在"渲染槽位"**（条目只 SetColor，帧末统一 Flush；收尾写回"无效果值"）
- **"非爆炸范围伤害"**：`DamagePacket.isDirect=true` ⇒ 绕开爆炸抗性/遮挡；骨架 `FpsHelper.HitAreaKinetic(KineticAreaHitData)`（每单位只打离中心最近的肢体、可排除自身、可选击退）；`TrampleEffect` 只做参数拼装；⚠ `FpsHelper` 无 `using System;`（写全 `System.Func`）、`in` 结构体字段不能 lambda 捕获
- 地雷 `DeployableMine.cs`：`Actor`/`Health`/`LimitedLife` 三件套；外部调**幂等** `TriggerExplosion()` → 延时协程 → `DoExplosion()`（`OnExploded` **在 `VFXManager.Release` 之前**派发）；`LimitedLife` 到寿"已触发则立即引爆、未触发才销毁"
- 音频：`AudioManaqerBase.sourcePool` 工厂须 `SetActive(false)`；`Furniture_NPCChat` 用 `SoundGroup_SO`+协程等 `AudioSource.isPlaying`
- `PlayerWeaponsManager.OnWeaponSwitched`：`isSec=true` 只设左手 IK，**不要**覆盖主武器右手 IK；`PhoenixEagleController` 旋转乱跳=阶段切换别改 `lastPos.y`
- 改进优先级：① 随机源统一 ② 接口解耦为纯 DTO ③ 统一命名空间 ④ God Class 审查 ⑤ `UnitQueryGrid` 池化

## 参考：地形/植被/破坏（要点）
- 地形**每局运行时重建**（`BattleManager.InitTerrain`→`MapRoot.Init(true)`）⇒ **场景预摆物体 Y 每局失效**；实测 640×96×640；Terrain 物体在 Ground 层
- 生成顺序：基础地形→材质→高度图→alphamap→`PlaceRockCovers`→`SpawnVegetation`→`SetTreeInstances`→`TreeDestructor.Rebuild`→`TerrainDetailEraser.Rebuild`→`RebuildTreeColliders`→`SpawnDetails`→NavMesh
- ⚠ `mapRes=Large?1024:512` → 高度图分辨率翻倍 = TerrainData 内存 ×4、整图写+Sync ×11、每 patch ×4（弹坑/`ModifyTerrain`）、`UpdateNavMesh` 同量级；树/草/draw call 不受影响。⚠ `heightmapResolution-1` 与 `alphaRes` 现在 1:1（巧合）→ `ModifyAlphaMap` 已改 `WRToAR`
- ⚠ 换 `treePrototypes` 前必须先清树实例；细节原型变少不会自动清（换数组前置零）；⚠ **无兜底**：MapData 的 `stone/tree/detailPrototypes` 空=该图没东西
- ⚠⚠ **树碰撞体不随 `SetTreeInstances` 更新** → 必须 `TerrainUtils.RebuildTreeColliders`（≈10.75ms）；隐形碰撞体=草无碰撞体、树/石走原型 prefab 的 **CapsuleCollider**（**不看 LODGroup**）；树原型**必须带 LODGroup** 才渲染
- `TerrainData` **无 `RemoveTreeInstance`**；`position` 归一化、`rotation` 是 X-Z 弧度；`detailPrototypes` getter 返回副本
- 清除门面 `TerrainClearer`（`TerrainClearTarget` 位标志）：请求入队→帧末驱动器合并下发（`RefreshInterval` 0.25s），**顺序必须先生效再重烘**；`Rebuild` 只能在放置覆盖物之前调
- 弹坑链路：`FpsHelper.Hit`→擦雪+`ModifyHeightMap(refresh:false)`+`MarkTerrainChanged()`；`ModifyTerrain` 按 `shape` 分流（圆/矩形各自 `ClearInRadius/Rect`，**石块必须一起清**）
  - `ModifyHeightMap` 只支持 Circle/Ellipse(实为**正方形**)/Rectangle（可绕 Y，须传 `eulerAngles.y`）；⚠ `ClearInRectXZ` 及消费者**只支持轴对齐矩形**
  - 矩形用**显式内外框**（外=淡化到 0+清除；内=完全拉平），淡化走 `RectFalloffPower` 逐轴归一化；圆/椭圆仍是 `(1-d)/(1-内/外)`
  - ⚠ `depth==0` **不是"不改高度"**（`isSet` 时整块按 `Lerp(旧高,中心高,power)` 拉平）；⚠ `transitionDistance` 是 prefab 级唯一值，细条（半长<transition）清除半尺寸为负 → no-op
  - ⚠ `ModifyTerrain.OnDrawGizmosSelected` 的框必须画在 `pos`；**Prefab Mode 里预制体场景没有地形**，`Physics.Raycast` 会穿到主场景
- 最贵四步：`ModifyHeightMap`/树实例整表提交/树碰撞体重建/`UpdateNavMesh`；计时入口 `TerrainClearer.LogTiming`
- `WRToHR/WRToAR/ARToHR` 返回 **int**；`AreaCircles` 生产者=`PlaceRockCovers`、消费者=`MissionController`/`ModifyTerrain`
- 层掩码：子弹 73、高速 601、玩家移动 8265、`UnitSeeLayers=16393`、`AirWallLayers=8192`
- **NavMesh**：MapRoot 上 `NavMeshSurface`（`Volume`+RenderMeshes+Ground）；运行时摆的石头要挡 AI 必须放 Ground 层
- "看不见的碰撞体"三类：地形树/石碰撞体、`MapRoot.CreatAirWall()` 18 面墙、建筑 Default 层无 Renderer 的代理体

## 参考：渲染（要点）
- **URP14 pass 时序**：Opaques 300 / AfterSkybox 400 / BeforeTransparents **450** / AfterTransparents 500 / BeforePostProcessing 550；**同 event 时 Feature 排在内置 Pass 之前** → 队列 Transparent 的物体永远不吃雾
- 背景层（SkyboxLayer，`ZClip Off`）：队列 <2500 被天空盒擦掉、Transparent 看见但不吃雾 → 已改 `LightMode="SkyboxLayerBeforeFog"`+RF 在 **445**（必须同时绑 color+depth）；**代价=特性被禁用后特效完全不显示**
- URP 资产 `..._Renderer.asset` 挂 5 Feature：Outline(300)/Snow(300)/SkyboxLayerBeforeFog(445)/WarpingBeforeFog(445)/FullScreenFog(450)；⚠ 手改别动 `m_RendererFeatureMap`（用 `SerializedObject`+`AddObjectToAsset`+`ValidateRendererFeatures()`）
- 全屏雾包（`Packages/Fog`）：Feature 内 **`color.a = intensity.value`**；`IsActive() => intensity != 0`（恰好 0 → Pass 跳过）
- **ToonLit**：5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` 是 albedo 唯一入口，透明靠 `clip()`；加东西三条边界：宏只写目标 shader 自己的 HLSLINCLUDE/pass、shared 只**新增** `#ifdef` 块、新数据只走**全局 uniform**
- 未写进 CBUFFER 的属性=死属性；toggle 局部关键字名=**原名**；**材质不投影首因**=`disabledShaderPasses: - SHADOWCASTER`；UI 相机勾 Clear Depth 会致贴花消失
- 材质纹理跟随数据：① MPB ② `Shader.SetGlobalTexture`+`#define`（推荐；**全局纹理不可写进 Properties**）③ 改 shader；已用于 `ToonLit_Stone`+`SetTextures`
- 贴花（`Assets/Shader/Decal/`）：⚠ `_Cull` 必须 Front/Off、绝不能 Back；⚠ 雾与混合模式必须匹配；⚠ 预乘 `col.rgb *= col.a`+SrcAlpha = alpha²
- 自定义 UI Shader：`RectMask2D` 需 `_ClipRect`（**不写进 Properties**）+`UNITY_UI_CLIP_RECT`+顶点局部坐标；失效常见=Graphic 未勾 Maskable
- **积雪**：**"用雪材质重画一遍"**（`DrawRenderers`+overrideMaterial+`RenderQueueRange.all`）→ 原材质 clip 全透明也会留幽灵轮廓；层 mask 65/8；⚠⚠ 雪够不到地形细节（草不是 Renderer）→ 草在自己 shader 里叠雪（`SnowOverlayCommon.hlsl`+`_SNOW_GRASS`，关键字写在材质上、**不做 [Toggle]**）；⚠ `PandaMat2` 被 47 个细节 prefab 共用
- 渐变天空盒（`Environment/GradientSkybox`，按 `HasProperty("_SkyColor")` 分两支）；⚠ `_SunDirection` 是 Vector 属性（存 `m_Colors`）；`_Lerp` 故意不提供；三色同时驱动环境光与天空盒三段色（`nightSkyExposureScale`=0.5）；⚠ 改 `Day-Night-Manager.prefab` 后必须**退出重进 Play**
- 地图级覆盖天空/赤道色：`MapData_SO.overrideSkyColor` → `WeatherAtmosphereController.SkyColorGradient`（**只覆盖天空盒、不影响环境光**）

## 参考：UI 展示模型 / 图片配色（要点）
- 展示模型：prefab→禁 MonoBehaviour+Collider、Rigidbody kinematic→`SetChildLayer`；独立相机+RenderTexture 预览；⚠ 要**连 Awake 都不执行**：`enabled=false` 无效，须在未激活挂点下实例化→移除逻辑组件→再激活（现成实现 `AirdropConfigWnd.ShowModel`）
- `FitModelScale`：视野半高/半宽 + 包围盒求缩放；中心对齐用 `focus` 投影；朝向 `_modelBaseEuler`(默认 180)；⚠ 量包围盒四前提：① 未激活 bounds=0 ② 带 Animator 先 `Update(0f)` 再等一帧 ③ 蒙皮 bounds 随姿势变 ④ 量前旋转归零；⚠ **不能 Encapsulate 全部 Renderer**（过滤顺序：画不出的→特效类→材质名关键字→零体积）
- 图片/配色（`Assets/Images/`，参照 `SelectRoleWnd.prefab`）：按钮底 `FX_TEX_Lock.png`(Sliced/ppu2/亮青 0.65,0.87,0.87)、锁定框 `FX_TEX_Lock_Frame.png`、列表与中面板 `frame_panel13.png`、大面板框 `SelectFrame3/4.png`、气泡 `SelectFrame2.png`、小图标框 `UI_Frame4.png`、图标底 `Common_Main_SkillBG.png`、六边形 `Hexagonal_Frame(2).png`、进度条 `LinkBar.png`、细边框 `frame5/6.png`、全屏底 `99997.png`；纯色深底惯例：无 sprite + (0,0,0,0.2~0.55)

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath 才用 `PropertyDrawer`；`InlineFieldDrawer` 是 `[Singleline]` 内联的唯一实现
- `EditorOverride`（全局 fallback Inspector）**被专属 `[CustomEditor]` 完全顶掉**；反射自建 Drawer 须手动注入 `m_Attribute`，特性类标 `CustomPropertyDrawer`
- 复用：`SOPickerPopup<T>`、`PrefabBatchToolBase`；Drawer 集中 `Drawer/`
- `[DisplayField]`：编辑期不画、运行期只读；只对已序列化字段生效
- 数据编辑器：`Editor/DataEditorWindow.cs`+`DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须补 `DrawField("新字段")`
- ⚠⚠ **专属 `[CustomPropertyDrawer]` 手列的字段，新增字段不会自动出现**：`DamageData`→`Editor/Drawer/DamageDataDrawer.cs:DrawSection_Motion`、`SustainedDamageData`→**同一文件里的 `SustainedDamageDataDrawer.DrawSection_General`**；⚠ **两处行高是硬编码常量**（`GetSectionHeight_Motion` 的 `rows=12`、`GetSectionHeight_General` 的 `3 * (LineHeight+2)`），加一行必须同步改
- `Assets/Editor/MaterialUsageFinder.cs`（`Tools/材质引用查询`）
- ⚠ `DisplayProgressBar` 会派发编辑器事件 → 长任务窗口须加重入锁；⚠ **EditorWindow 私有字段会被 Unity 存档并在域重载后恢复**，UI 开关应在 OnEnable 复位
- ⚠ `Editor/Tool/EditorTools.asmdef` 的 `references: []` → 看不到 `UnityEngine.UI`；要用 UGUI/项目类型就放 `Assets/Editor/`
- **`[Compare]` 条件显隐**（`FPSGame.Attribute`）：按**同层**字段切换显隐（隐藏=高 0 不占位）；⚠ 单参构造 = `Equal + 0`、双参默认 `Greater`；`Contain/NotContain` 是位掩码；⚠ 一个字段只能挂一个；⚠ **找不到控制字段 → 隐藏 + 每帧 `LogError`**
- `CustomLabelDrawer.GetContainerPath`：`a.b.c`→`a.b`；`a.list.Array.data[i]`（特性挂 List 本身）→`a`；`a.list.Array.data[i].x`→`a.list.Array.data[i]`；专属 `[CustomEditor]` 手列字段要自己调 `ShouldDisplayField`

## 第三方素材包
- Pandazole：99 fbx 的 `.fbx.meta` 全重映射到 `PandaMat.mat`；树 prefab 在 Ground 层、47 个 `Details/Prefabs/*.prefab` 在 Default 层共用 `PandaMat2.mat`
- ⚠⚠ **Pandazole git 索引与磁盘长期不一致**（顶层扁平 vs `Tree/`+`Details/`）→ 常驻 1180 `D` + 612 `??` ⇒ 该包改动**无法 git 回滚**，批量改前先备份
- ⚠ 本仓库植被/岩石素材包基本**未被游戏场景引用**（只在各自 Demo 用）

## MCP for Unity
- ⚠ 实时实例见 `mcpforunity://instances`；多实例未钉选时 server **拒绝猜测** → 先 `set_active_instance`；写操作前先 `git commit`
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（已入库含汉化）；手册=skill `unity-mcp/`
- ⚠ 会话里看不到 `mcp__`/`mcpforunity__` 工具时**不要假装能查编辑器**：只能读文件/资产，要如实说"未证实"
- 客户端配置两份互不相通：IDE 读 `~/.codebuddy/mcp.json`；Unity 窗口 Configure 只写 CLI；Transport 必须 Stdio
- `execute_code` 默认 auto（有 Roslyn 则 C#12）；**反射扫全场 `AppDomain.CurrentDomain.GetAssemblies()` + `FindObjectsOfType` 是诊断利器**；`FindObjectsOfType` 不含未激活对象，要含就用 `Resources.FindObjectsOfTypeAll`
- `mcp_call_tool` 参数校验严格（不接受 schema 外字段）；改完脚本：`validate_script`→`refresh_unity`→等 ~10s→`read_console`（`types` 传**数组**）；编辑器里 `AddComponent` 不调 `Awake`
- ⚠⚠ **新建 `.cs` 文件必须先 `refresh_unity(mode=force, scope=assets)`**：默认 `if_dirty` 会返回 `refresh_triggered:false`，新文件没被导入 ⇒ 编译报"找不到类型"（本次实测）；判据是**新 `.cs.meta` 的 mtime 早于 DLL** 才算编进去
