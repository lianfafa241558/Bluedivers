# Bluedivers 长期记忆（索引）

> **只留结论与坑；过程/代码细节见各日 `YYYY-MM-DD.md`。** 2026-09-29 第五次压缩（去重+精简）。

## 协作与通用坑
- 不确定先问；不动无关 using；改名/移动由用户**手动**做，AI 只给方案
- 问"能不能/为什么没有"→ 先**结论+证据(文件:行)**，再选项与代价
- 诊断顺序：先问「**有没有人调它**」→ 看**实际资产/prefab 数值** → 最后看逻辑；能用运行时反射就别只靠读代码
- Inspector 特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 顶掉全局 fallback
- 资产 YAML 缺整行字段 = 脚本新增后资产未重存；Unity **保留 C# 字段初始化器的值** → 新增非 0 默认值字段安全；往**结构体末尾**加字段最稳
- ⚠ 别什么都往 `BattleEventSub` 塞：能拿实例就订阅**实例事件**；跨实例状态别用 `static`（例外＝需"记住最后值给晚订阅者"的初始广播）
- 新增脚本尽量**运行时自动挂载**，少改场景/prefab YAML；必须手改 prefab 要明确告知；改 URP renderer 资产走 MCP `SerializedObject`
- ⚠ 新增 `const`/`static` 放**类顶部**（规范硬要求）
- ⚠ **文本搜索不可全信**：`git grep` 把含异常字节的 `.cs` 当二进制跳过、`Select-String` 也会漏 → 关键结论用**运行时反射/控制台**复核

## 环境与工具链
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；**MCP 实例 hash/端口随路径变**，实时看 `mcpforunity://instances`（2026-09-29 实测 `Bluedivers@3d9f2357`:6400、`RTSClient@6365de15`:6401）
- Unity 2022.3.62f3 / URP 14.0.12 / C#9+.NET Std 2.1；单机 PvE（FPS Sample 改，`Unity.FPS.*`）；随机源统一 `BattleRandom`、确定性用 `PEMaths`
- `Assets/Scripts/Lib/PEMaths.dll`：**PEInt/PEVector3 就是"米"标量**（`RawInt==RawFloat`）；`RawFloat` 换算后不可再参与运算
- git `core.autocrlf=true`；GitHub 直连被重置 → 走 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore` 的 Unity 生成目录规则必须 `/` 锚根（裸 `[Bb]uild/` 曾吞 `Packages/**/Tools/Build/`）
- 工具坑：`search_content` 的 `glob` 不可靠 → 整目录搜或单文件 `path`；`findstr` 读 fbx 报错 → `select-string`；无 `Debug.DrawWireSphere` → `Tool.DrawWireSphere`
- 判定"代码真编译进去"：看**目标程序集** mtime vs 源码 mtime（`Assembly-CSharp.dll` 只装 `01Manager/02Game/Effect` 等无 asmdef 目录；`00Tools/Test/*` 属 **`00_Utils.dll`**）
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`

## 结构与命名
- `Assets/Scripts/` 数字前缀=依赖顺序（00Core→GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），上层可引下层，反之禁止
- asmdef 共 14 个；`01Manager/`、`02Data/`、`02Game/`、`04UI/` **根脚本**、`Effect/*.cs`、`Rendering/*.cs` 全在 **Assembly-CSharp**；asmdef 均 `autoReferenced:true` → CSharp 可调它们、反向不行
- ⚠ **asmdef 内引用不到 Assembly-CSharp**（`08_Map` 用不了 `MapData_SO`）→ 跨层只传 `GameObject[]`/基础类型
- 存在 **01Manager↔02Game↔02Data 三角循环**（02Data 引 `FpsGame.Mission`/`Unity.FPS.Game`）→ 拆 asmdef 须先断 02Data→02Game
- 约定：跨模块优先事件；接口主流 `I_` 前缀；SO 用 `_SO`；partial 用 `主类名_分部名.cs`；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 只对**字段**有效
- 层索引：0 Default / 3 Ground / 6 Unit / 13 AirWall / 14 Smoke / 15 ModelView

## 架构与数据分层
- 组件间避免 `GetComponent` 互取；AI 用 `AIController` 组合+接口代理，**不继承 `Actor`**；竖直占位 `I_Entity.HalfHeight`（0=不过滤）
- 窗口：`WndManager` 挂 `public XxxWnd` 字段，`Init()` 由 UnityEvent 调；家具入口 `Furniture_*.furnData` 按 `Id` 分发
- **SO 不能持有 prefab/场景实例引用**：只放与实例无关项；子物体/挂点放 prefab 的 `[SerializeField]`；"同类单位一起吃?"→SO，语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；需"任意组件任意方法"才用 UnityEvent；共享+需实例层级→间接引用（key/相对路径+运行时解析+编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- 家具：`Furniture_Attached : BaseMono, IFurniture`（**不改**基类静态字典），OnEnable/OnDisable 维护静态 `list`；`Handle(user)`→`CanOperate`+`Operate`；非 Actor 物体必须 override `ShowName/Id/Icon`
- **交互键（E）解析在 `PlayerOperationController.{Update→Handle}`**：target = 第一人称屏幕中心 Raycast(1.3m) 或第三人称 2m 内最近可交互物
- 第三人称交互拉近：`PlayerController_ThirdPerson.ThirdPersonDistanceScale`(0.1~1) 乘到两分支 `distance`；每帧写 `IsThirdPerson && target!=null ? 0.5f : 1f`
- 装备：`IEquippable` + `EquipController.Equips`；装卸只由交互家具调；交互家具"被携带"时自身 `enabled=false` → **装备想监听输入必须写在 `IEquippable` 实现里**；`Operate`=E、`Equip`=X
- 计时器：`TickBehaviour.Tick()` **1 秒 1 次**（返回 false 即移出）；`CreateTimer(cb,秒,次数,endcb)`；`CreatePerTimer`=**每帧回调**
- 波次：`BattleManager.CreatWave(WaveCreateParams)` → Robot/Zerg/KaiserWave；皆 1s Tick，`End` 时 `Dispose()` 返回 false；`onEnd` 在 Dispose 回调；`centerGetter` **只影响尚未落点**的单位；ZergWave 距 anchorCenter>50m → `RedeployPods()`

## AI / 寻路（易踩）
- AI 单位 = `EnemyController`（组合）+ `EnemyMobile`（状态机）+ `DetectionModule`（感知）；实机队伍：玩家 `team=0`，敌人 `team=2`
- `EnemyController.PatrolPos` 是**到达即自毁**哨兵点（`HomePoint` 才是到达待命）；巡逻队只能 `BattleManager.CreatPatrol(Vector3)`
- `EnemyMobile.Start` 只在开场读一次 `PatrolPos`；`UpdateCurrentAiState` 在 `!BirthComplete` 期间整体早退（`IsMoveLocked`/`vertigo`/`terror` 会冻结状态切换）
- ⚠ **`Actor.IsFixed` 语义**：只表示"**不会因长期不动被删除**"（`IdleBehavior` 跳过自毁），**不是位置固定** ⇒ 不能当"不该移动"的门禁
- **枪声警惕**：`EnemyMobile.UpdateAiStateTransitions` 有 `Idle/Patrol → Beware`，条件 = `BewarePoint.HasValue` **且** 点距 `<= Mathf.Max(HearingRange, DetectionRange)`（距离门禁必须有）；`EnterBeware` 取点后**立刻 `ClearBeware()` 消费**；`Beware` 到达或 `BewareTimeout=20s` → `Return`。`EnemyTurret` 走 `BewareLookDuration=3s` 只看过去，同样带距离门禁
- **警惕冷却**：`BewareCooldown=10s` + `BewareCooldownRadius=10m`；从上一检查点**回到 Idle/Patrol 时**记冷却；冷却期内"新枪声点 距 `m_BewareDestination`（上次检查点）不超过半径"就无视。三条件收在 `ShouldInvestigateBeware()`。`EnemyTurret` 不参与冷却
- 两个既有的坑（已修）：① `case Return` 的"判到达的 `else if` 必须排在'时间到'前面"；② "要去的点先写进 `DetectionModule.Beware(...)`/`SearchPoint`，`EnterBeware` 只从点取并消费"
- ⚠⚠ **寻路请求失败曾永久锁死（已修）**：`SetNavDestination` 的 `<1m` 去重键 `m_lastDestination` 曾先于"请求发出"写入 ⇒ 一次失败即永不重发。**修后**：仅当"目标没变 **且** `pathPending||hasPath`"才跳过，否则按 `NavRetryInterval=0.5s` 节流重发（别退回无条件去重！）
- ⚠ `PathRequestManager`：结束后判 `PathInvalid/PathPartial(1拐点)` → 投影重试 5 次(半径 5) → **10m 兜底**；兜底 `SamplePosition` 失败**不许空操作** → 改为无条件 `LogWarning` + 交调用方节流
- `Tool.GetCircleIntersection` 返回 Vector2，`ToVector3()` = `(x,0,y)`（**y 恒 0**）⇒ 几何 XZ 点必须先补 `y`（`FpsHelper.GetNavMeshPoint` 或用 `spawnPos.y`）再做 `SamplePosition`
- 地图圆半径用 `CameraSize/2`、圆心 `MapSize/2`（不同源）⇒ 巡逻目标常落在无 NavMesh 边界环带 ⇒ 巡逻点必须 `SamplePosition`(40m) 再赋值；散开偏移**只加 XZ**
- 击退：`IPhysical` = `ApplyForce`(持续力 Δv=(F/m)·dt) + `ApplyImpulse`(冲量 Δv=J/m)；实现在 **`EnemyController_Physical.cs`**（partial，`NavMeshAgent.Move` 水平推挤、推挤期临时关 `autoRepath` 并**还原原值**、`OnDisable` 兜底）
- ⚠ 同一 partial **不能有两个 `Update`** → `EnemyController.Update` 里手动加 `UpdateKnockback();`

## 场景加载与事件时序（易踩）
- `ResSvc.AsyncLoadScene` 用 `LoadSceneMode.Single`，**回调被 `DelayedInvoke` 延后一帧** → 场景内组件 `Start` 早于回调里 `AddComponent<BattleManager>()`
- ⇒ 凡"场景物体 `Start` 里只抛一次"的初始事件（如 `DaySwitch`）BattleManager 收不到 → 修法：总线缓存最后值 + 就绪后补发（`ApplyInitDaySwitch()`）；"后创建者"都要"补初始状态"
- `BattleManager._initQueue`（`EnqueueInit`）给"早于 BattleManager 存在的物体"排队，末尾 `DrainInitQueue()` 兑现
- ⚠⚠ **`authorizeCounter` 裸计数、无下限** ⇒ **"白天"=计数 0 的自然态**，绝不能在白天做 `-1`，否则战备永久锁死

## 组件存活/回收（易踩）
- `VFXManager.Release(GameObject)` 只认根物体上的 `ParticleSystem`(Stop)/`LimitedLife`(置 `allowRelease`)，皆无=空转；`ProjectileBase` 走另一重载
- `LimitedLife.IsAlive()` 只被池 Update 轮询 → 非池化实例不会被回收；`HealthOther.AutoDestroy` 才是"死亡即销毁"
- `LimitedLife` **延时回收**：到寿先 `InvokeEnd()`（`endInvoked` 一次）再等 `EndDelay`；`allowRelease` 强制回收**不走延时**
- 池化对象"本次状态"字段必须在 OnDisable/OnEnable 复位，且放在早退 return 之前

## 昼夜与天气
- `DayNightBrain` + Modules；预制体 `Day-Night-Manager.prefab`；旧 `Effect/DayNightCycle.cs` 不参与玩法场景
- 渐变时间轴：**0%=日出、25%=正午、50%=日落、75%=午夜、100%=日出**
- `EnvironmentLightingModule`：环境光三色 ×`AmbientBrightnessMultiplier`；天空盒有 `_Lerp` 写 `_Lerp`、无则写 `_Exposure`；写值走 `GetSkyboxForWrite()`（**只在 `Application.isPlaying` 建副本**）
- ⚠ Procedural 天空盒只有 `_SkyTint`(乘性)/`_GroundColor`/`_AtmosphereThickness`/`_Exposure`/`_Rotation`/`_SunDisk*`，**无赤道色**
- **全屏雾合成**：雾色渐变 RGB=雾色×环境光倍率，A=该时刻"雾出现程度"；`intensity=Clamp01(昼夜曲线×渐变A + 天气FogIntensityAdd)`；`density=曲线/max(visibility,0.05)`
- 天气：`WeatherSystem`(开局按 `mapCfg.WeatherInfos` 用 `BattleRandom` 抽) + `WeatherEffect`(Rain/Desert/Snow)；`WeatherAtmosphereController`（全局静态桥）注入 `FogColorGradient`/`SkyColorGradient`

## 任务 / 战备
- `MissionBase : TickBehaviour`（1s）→ `MissionEvacuateBase` → 静态(终端 KeyScreen)/动态(凯伊 ReturnBag)；`UseSceneStartPoint` 虚开关；「创建后」走 `InitMission`，「全部初始化后」走 `StartMission`；`Link(mission)` 订阅其 `OnMissionEnd` 来激活自己；`Uninit()` 在 `EndMission`（任务完成）时就会跑 ⇒ **别把"任务完成后还要用"的订阅/清理放 Uninit**
- **次要撤离区（`MissionEvacuateSecondary : MissionDestroyActor`，标旗 Extra）**：静态撤离激活时由 `MissionEvacuateStatic.CallSecondaryBeacons()` 往各区中心各呼叫一个 0 号战备；任一处 KeyScreen 走到最后一步＝玩家选中该点 → `HideOtherBeacons()` + 把 `area/areaPoint/pos/beacon/keyScreen` **整体换成该区** + `StartWait()`；`OnKeyScreenStage` 有 `stage==Activation` 门禁防二次 StartWait
  - **动态撤离（2026-09-29 补）**：`MissionEvacuateMobile` 在 `OnStartEvacuate` 里 `SelectNearestEvacuatePoint(user)` —— 候选 = 自己的撤离点 ∪ 各次要区，取**离玩家最近**的一处并把 `area/areaPoint/pos` 切过去；**不呼叫信标**（无玩家选择流程），无需 Hide
  - 注入链：`MissionController` 按**类型**收集（不依赖标旗）→ 在 `evacuate.Link(main)` 之后 `if (evacuate is MissionEvacuateBase eb) eb.SetSecondaryZones(list)`（`GenerateFromData` 与 `FindFromScene` 两条路都做）；`_secondaryZones`+`SetSecondaryZones` 在基类 `MissionEvacuateBase`（静态/动态共用）
  - `MissionEnum.SecondaryEvacuate = 300`（**显式值**，⚠ 别在枚举中间插隐式值）
  - 数据侧需用户建：挂该脚本的 MissionBase prefab（标旗=Extra）+ MissionData_SO + 加进 `CampData_SO.extraTypes`
- `MedivacController`：`Land`=插入下机；`Evacuate`=撤离接人；`TakeOff()`=Play"Evacuate"+开 cam+派发 `Complete`+**只隐藏 IsInBox 玩家**
- 主任务 `MissionCompleteKeySceern`（拼写如此）=终端流程；`MissionOilRefining`：Init 空投平台+连接点→Wait `MissionSubConnectPipes`→Start(180s+波次)→Repair→End；管道状态是 `Furniture_Pipe.Id` 字符串，**无"已修复"标志** → 不能读 Id 判完成，要 Tick 轮询
- 台词只能 `WndManager.CreatNotice(角色, groupName)`，角色键=`NoticeTree_SO.ID`
- 战备：资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；`AirdropData_SO`（`type`/`labels`(`[Flags]`)/`opter`/`subAirdrop`/`creatObect`/`coolGroup`/`isHide`）；首键：→轰炸/↑空中支援/↓炮台地雷补给/←载具；**ID 0 = 撤离信标**（`type=4`、`creatObect=Prefabs/Airdrop/EvacuationBeacon`、`permanentPod`）
- 部署：`VFXAirdropEffect` 按 `deliveryType` 分支；Pod 落地回调 `OnCreatObject` 给的就是 `creatObect` 实例；授权 `AirdropController.Authorize(id,state)` → `+= state?1:-1`（0=隐藏且不可用）；必需战备 `TaskManager.RequiredAD`

## 战斗与伤害
- 伤害总入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸(穿甲/拆毁)→冲击波→**地形破坏**→警告→特效/音效/弹痕
- **逻辑层噪声体系（表现/逻辑分开）**：契约 `NoiseData{source,pos,radius}` + `BattleEventSub.OnNoise/Noise()`。生产者：① `WeaponBaseController.HandleShoot()` 发**枪口**噪声，半径 `FireNoiseRadius`（默认 20）；② `FpsHelper.Hit` 的 `//警告` 段发**弹着点**噪声，半径 `GetImpactSoundRadius`（默认 8；⚠ `UseExplode` 时取 `max(它, damageOuterRadius)`）。消费者 `DetectionModule.OnNoise`（`Start` 订阅、`OnDestroy` 退订）
- ⚠ `BattleEventSub.OnBulletHit` 是**表现层**（HUD 擦弹/受击），**没有**被噪声体系取代，别当逻辑用；`FireNoiseRadius`(逻辑) 与 `SFXRange`(表现) **两套分开**
- **感知链路**：`OnNoise` 条件 = `m_Actor!=null && source!=null && source.TryGetComponent<Actor>() && actor.Team!=m_Actor.Team && Distance(pos, Logic3Pos)-radius < HearingRange` → `Beware(pos, radius, false)`；`source` 必须与 `Actor` **同物体**
- `DetectionModule` 两点分开：`BewarePoint`(噪声点，带响度) 与 `SearchPoint`(搜索指令)；`Beware(...)` 里 `noise < m_BewareNoise` 直接 return；`EnterBeware` 按 `SearchPoint > BewarePoint > m_OriginPos` 取目的地
- 敌人特效（`02Game/AI/FxCont`）：三层配置（渲染模板 SO 按槽位匹配 / 事件 SO / 组件字段）；**MPB 所有权在"渲染槽位"**（帧末统一 Flush）
- **"非爆炸范围伤害"**：`DamagePacket.isDirect=true` ⇒ 绕开爆炸抗性/遮挡；骨架 `FpsHelper.HitAreaKinetic(KineticAreaHitData)`（每单位只打最近肢体、可排除自身、可选击退）；⚠ `FpsHelper` 无 `using System;`、`in` 结构体字段不能 lambda 捕获
- 地雷 `DeployableMine.cs`：`Actor`/`Health`/`LimitedLife` 三件套；外部调**幂等** `TriggerExplosion()`；`OnExploded` **在 `VFXManager.Release` 之前**派发
- 改进优先级：① 随机源统一 ② 接口解耦为纯 DTO ③ 统一命名空间 ④ God Class 审查 ⑤ `UnitQueryGrid` 池化

## 参考：地形 / 渲染 / UI（要点，细节见日文件）
- **地形每局运行时重建**（`BattleManager.InitTerrain`→`MapRoot.Init(true)`）⇒ **场景预摆物体 Y 每局失效**；实测 640×96×640
- ⚠ `mapRes=Large?1024:512`：`heightmapResolution ×2` = TerrainData 内存 ×4、整图写+Sync ×11、每 patch ×4；换 `treePrototypes` 前必须先清树实例；**无兜底**（原型数组空=该图没东西）
- ⚠⚠ **树碰撞体不随 `SetTreeInstances` 更新** → 必须 `TerrainUtils.RebuildTreeColliders`；树原型**必须带 LODGroup** 才渲染
- 清除门面 `TerrainClearer`（位标志）：请求入队→帧末合并下发，**顺序先生效再重烘**；弹坑链路 `FpsHelper.Hit`→`ModifyHeightMap(refresh:false)`+`MarkTerrainChanged()`；最贵四步：`ModifyHeightMap`/树实例整表/树碰撞体重建/`UpdateNavMesh`
- 层掩码：子弹 73、高速 601、玩家移动 8265、`UnitSeeLayers=16393`、`AirWallLayers=8192`；**NavMesh**：MapRoot 上 `NavMeshSurface`；运行时摆的石头要挡 AI 必须放 Ground 层
- **URP14 pass 时序**：Opaques 300 / AfterSkybox 400 / BeforeTransparents 450 / AfterTransparents 500 / BeforePostProcessing 550；**同 event 时 Feature 排在内置 Pass 之前** → 队列 Transparent 物体永远不吃雾
- 背景层已改 `LightMode="SkyboxLayerBeforeFog"`+RF 在 **445**（**代价=特性被禁用后特效完全不显示**）；全屏雾包 `color.a = intensity`，`IsActive() => intensity != 0`
- **ToonLit**：5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` 是 albedo 唯一入口；未写进 CBUFFER 的属性=死属性；**材质不投影首因**=`disabledShaderPasses: - SHADOWCASTER`
- 材质纹理跟随数据：① MPB ② `Shader.SetGlobalTexture`+`#define`（推荐；**全局纹理不可写进 Properties**）
- **积雪**：**"用雪材质重画一遍"**（`DrawRenderers`+overrideMaterial+`RenderQueueRange.all`）；⚠ 雪够不到地形细节（草不是 Renderer）→ 草在自己 shader 里叠雪（`SnowOverlayCommon.hlsl`）
- 地图级覆盖天空：`MapData_SO.overrideSkyColor` → `WeatherAtmosphereController.SkyColorGradient`（**只覆盖天空盒、不影响环境光**）
- 展示模型：prefab→禁 MonoBehaviour+Collider、Rigidbody kinematic→`SetChildLayer`；⚠ 要**连 Awake 都不执行**须在未激活挂点下实例化→移除逻辑组件→再激活；⚠ **不能 Encapsulate 全部 Renderer**（过滤顺序：画不出的→特效类→材质关键字→零体积）

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath 才用 `PropertyDrawer`；`EditorOverride`（全局 fallback Inspector）**被专属 `[CustomEditor]` 完全顶掉**
- 复用：`SOPickerPopup<T>`、`PrefabBatchToolBase`、`SOPickerPopup`；Drawer 集中 `Drawer/`
- `[DisplayField]`：编辑期不画、运行期只读；只对已序列化字段生效
- 数据编辑器：`Editor/DataEditorWindow.cs`+`DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须补 `DrawField("新字段")`
- ⚠⚠ **专属 `[CustomPropertyDrawer]` 手列的字段，新增字段不会自动出现**（`DamageDataDrawer` / `SustainedDamageDataDrawer` 同一文件；⚠ **两处行高是硬编码常量**，加一行必须同步改）
- ⚠ `DisplayProgressBar` 会派发编辑器事件 → 长任务窗口须加重入锁；⚠ **EditorWindow 私有字段会被 Unity 存档并在域重载后恢复**，UI 开关应在 OnEnable 复位
- ⚠ `Editor/Tool/EditorTools.asmdef` 的 `references: []` → 看不到 `UnityEngine.UI`；要用 UGUI/项目类型就放 `Assets/Editor/`
- **`[Compare]` 条件显隐**：按**同层**字段切换（隐藏=高 0）；⚠ 单参 = `Equal+0`、双参默认 `Greater`；`Contain/NotContain` 是位掩码；一个字段只能挂一个；**找不到控制字段 → 隐藏 + 每帧 `LogError`**
- `CustomLabelDrawer.GetContainerPath`：`a.b.c`→`a.b`；`a.list.Array.data[i]`（特性挂 List 本身）→`a`；专属 `[CustomEditor]` 手列字段要自己调 `ShouldDisplayField`

## 第三方素材包
- Pandazole：99 fbx 的 `.fbx.meta` 全重映射到 `PandaMat.mat`；树 prefab 在 Ground 层、47 个 `Details/Prefabs/*.prefab` 在 Default 层共用 `PandaMat2.mat`
- ⚠⚠ **Pandazole git 索引与磁盘长期不一致**（顶层扁平 vs `Tree/`+`Details/`）⇒ 该包改动**无法 git 回滚**，批量改前先备份
- ⚠ 本仓库植被/岩石素材包基本**未被游戏场景引用**

## MCP for Unity
- ⚠ 实时实例见 `mcpforunity://instances`；多实例未钉选时 server **拒绝猜测** → 先 `set_active_instance`；写操作前先确认 `projectRoot==E:/Bluedivers`
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（已入库含汉化）；手册=skill `unity-mcp/`
- ⚠⚠ **新建 `.cs` 文件必须先 `refresh_unity(mode=force, scope=assets)`**（默认 `if_dirty` 会返回 `refresh_triggered:false`，新文件没被导入 ⇒ 编译报"找不到类型"）；判据是**新 `.cs.meta` 的 mtime 早于 DLL**
- `execute_code` 默认 auto（有 Roslyn 则 C#12）；**反射扫 `AppDomain.CurrentDomain.GetAssemblies()` + `FindObjectsOfType` 是诊断利器**；`FindObjectsOfType` 不含未激活对象
- 改完脚本：`validate_script`→`refresh_unity`→等 ~10s→`read_console`（`types` 传**数组**）；编辑器里 `AddComponent` 不调 `Awake`
