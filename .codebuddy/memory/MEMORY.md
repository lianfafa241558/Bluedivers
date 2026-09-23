# Bluedivers 项目长期记忆

> 细节见各日 `YYYY-MM-DD.md`。最后整理：2026-09-23（v13：合并同类项、压缩措辞、去重复，未删结论）
> 顺序即重要性：前 8 节通用约定，后 3 节参考手册。

## 协作偏好与通用坑
- 不确定先问；不主动纠结/移除 using；结构改动（改名/移动）由用户**手动**做，AI 只给方案
- 用户常连问"某功能能不能做/为什么没有"→ 先给**结论 + 证据（文件:行）**，再给选项与代价，最后才谈落地
- 双机/换机后第一件事：确认 MCP 连的是当前副本再动写操作
- 编辑器特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 会顶掉全局 fallback
- 资产 YAML 整行缺失的字段 = 脚本新增后资产未重存；Unity **保留 C# 字段初始化器的值** → 新增带非 0 默认值的序列化字段安全
- 诊断"某组件没生效"先看**是不是压根没人调它**；视觉问题先看**实际材质/资产属性值** + 物体落在哪个 Pass/队列
- 新增交互家具：新建 `Furniture_Attached` 子类（**不改** Furniture_General/AttachedGeneral 的静态字典）→ override ShowName/Id/Desc/Icon；可运行时 AddComponent
- ⚠ **别什么都往 `BattleEventSub` 塞**：能拿到对方实例就订阅**实例事件**；全局事件层只留真广播；跨实例状态别用 `static`。**例外**：需"记住最后一次值给晚订阅者"的初始状态广播
- 新增脚本尽量**运行时自动挂载**接入，少改场景/prefab YAML；必须手改 prefab 时明确告知用户；改 URP renderer 资产可走 MCP `SerializedObject`
- ⚠ 新增 `const`/`static` 一律放**类顶部**（规范硬要求）
- 上游包文件丢失排查：`git ls-files` 与磁盘清单比对；Unity 报"meta 在但文件夹不存在"多为文件被忽略/删除

## 环境与工具链
- 副本多份：本机 `D:\Pro\Bluedivers`（MCP 规则钉 `E:\Bluedivers`）、`D:\Project\RTSClient`；**实例 hash 随路径变**，勿跨机照抄
- Unity 2022.3.62f3 / URP 14.0.12 / C#9 + .NET Std 2.1 / TMP 3.0.9 / Navigation 1.1.6；单机 PvE（FPS Sample 改，`Unity.FPS.*`）
- 随机源统一 `BattleRandom`；确定性计算 `PEMaths`；git `core.autocrlf=true`；GitHub 直连被重置 → 走 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore` 的 Unity 生成目录规则必须 `/` 锚根（裸 `[Bb]uild/` 曾吞 `Packages/**/Tools/Build/`、`Assets/Art/{Anim,Modle/Enemy}/Build/`）
- 工具坑：`search_content` 的 `glob` 不可靠 → 整目录搜或单文件 `path`；输出易爆 → 带 `headLimit`；`findstr` 读部分 fbx 报错 → `select-string`；无 `Debug.DrawWireSphere` → `Tool.DrawWireSphere`
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`

## 结构与命名
- `Assets/Scripts/` 数字前缀=依赖顺序（00Core→00GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），上层可引下层，反之禁止
- asmdef 共 10 个：`00Attribute`、`00GameContract/01_GameContract`、`00Core/00_Core`、`08Map/08_Map`(FpsGame.MapUtils)、`DayNightSystem`、`00Tools/Test/00_Utils`(**TerrainUtils 在此**)、`04UI/Assembly/04_UI`、`04UI/WndTool/00_WndTools`、`Effect/EffectComp/05_EffectComp`、`Plugins/NavMeshComponents/Scripts`；`01Manager/`、`02Data/`、`02Game/`、`04UI/` 根脚本、`Effect/*.cs`、`Rendering/*.cs` 全在 **Assembly-CSharp**；asmdef 均 `autoReferenced:true` → CSharp 可调它们、反向不行
- 存在 **01Manager↔02Game↔02Data 三角循环**（02Data 引 `FpsGame.Mission`/`Unity.FPS.Game`）→ 拆 asmdef 须先断 02Data→02Game
- ⚠ **asmdef 内引用不到 Assembly-CSharp**（`08_Map` 用不了 `MapData_SO`/`TerrainItemInfo`）→ 跨层只传 `GameObject[]`/基础类型，由上层调用时从 SO 取
- 约定：跨模块优先事件 `GlobalEventSub`/`BattleEventSub`；接口主流 `I_` 前缀；SO 用 `_SO`；partial 用 `主类名_分部名.cs` 同目录；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 只对**字段**有效

## 架构与数据分层
- 组件间避免 `GetComponent` 互取；AI 用 `AIController` 组合 + 接口代理，**不继承 `Actor`**；默认操作视角 `ArchivesData_SO.settingDic["默认操作视角"]`
- 竖直占位 `I_Entity.HalfHeight`（`CenterPos.y±HalfHeight`，0=未配置不过滤）；批量填充 `Assets/Editor/ActorHalfHeightTool.cs`
- 窗口：`WndManager` 挂 `public XxxWnd` 字段（GameRoot.prefab 填 0），窗口 `Init()` 由场景/prefab 的 UnityEvent 调；家具入口 `Furniture_General.furnData`/`Furniture_AttachedGeneral.furnData` 按 `Id` 分发
- **SO 不能持有 prefab/场景实例引用**：子物体/挂点放 prefab 的 `[SerializeField]`；SO 只放与实例无关项（音效/粒子/材质/颜色/数值）。"希望同类单位一起吃?"→SO；语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；需"任意组件任意方法"才用 UnityEvent；共享+需引用实例层级→间接引用（key/相对路径+运行时解析+编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- 家具：`Furniture_Attached : BaseMono, IFurniture`，OnEnable/OnDisable 维护静态 `Furniture_Attached.list`（交互扫描源）；`Handle(user)`→`CanOperate`+`Operate`；`ShowName`/`Id`/`Icon` 默认取自 `_actor`，**非 Actor 物体挂家具必须 override 这三项**
- 计时器：`TickBehaviour.Tick()` **1 秒 1 次**；`GameRoot.CreateTimer(cb,秒,次数,endcb)`；`GameRoot.CreatePerTimer(percb,秒,endcb)`=**每帧回调**持续 秒，到点回调 endcb
- 波次：`BattleManager.CreatWave(WaveCreateParams)` → `WaveManager` 建 `RobotWave`(凤凰鹰)/`ZergWave`(空降)/`KaiserWave`；皆 `I_TickClass`、1s Tick，`End` 时 `Dispose()` 返回 false 被移出
  - `WaveCreateParams{center/points/range/scale/tip/onEnd/centerGetter}`：`onEnd` 在 `Dispose` 回调（续刷安全，`Update` 倒序遍历）；`centerGetter` 每 Tick 刷 `center`，**只影响尚未落点**的单位
  - ZergWave **重部署**：`centerGetter!=null`+随机生成点+Ongoing+距 `anchorCenter`>50m → `RedeployPods()`（Play"End"+`ResetLift(6)`）→ 重算生成环重投；`WaveUseObject[0]`=空投舱
  - `KaiserWave` 每 `360f/creats.Count` 秒建 PhoenixEagle+6 单位（±2 网格）；`CampTemplate.patrolTemplate`=`List<SKVP<string,int>>`；`EnemyController.PatrolPos` 是**到达即自毁**哨兵点（`HomePoint` 才是到达待命），巡逻队只能 `BattleManager.CreatPatrol(Vector3)`

## 场景加载与事件时序（易踩）
- `ResSvc.AsyncLoadScene(name, cb, ...)` 用 `LoadSceneMode.Single`，`completed` 只置标记、**回调被 `DelayedInvoke` 延后一帧** → **场景内组件 `Start` 一定早于回调里 `new GameObject().AddComponent<BattleManager>()`**
- ⇒ 凡"场景物体 `Start` 里只抛一次"的初始事件（`DayNightBrain.Start`→`DayNightTriggerModule.Initialize`→`GlobalEventSub.DaySwitch`），**BattleManager 收不到开局那一次**。修法：总线缓存最后值（`GlobalEventSub.LastDaySwitchIsNoon`）+ `BattleManager` 就绪后补发（`ApplyInitDaySwitch()` 在 `ADCont.Init()+CacheReinforceAd()` 之后）
- 同理：`Awake` 订阅的场景组件没问题；`Start`/回调之后才创建的（`BattleManager`、战备 UI）必须"补初始状态"
- `BattleManager._initQueue`（`EnqueueInit`）给"场景里早于 BattleManager 存在的物体"排队，`Init/InitSpecial` 末尾 `DrainInitQueue()` 兑现；依赖 `ADCont` 的调用前必须判空
- ⚠⚠ **`authorizeCounter` 是裸计数、无下限**（`+= state?1:-1`，`IsAuthorize => !cfg.authorize || counter > 0`）⇒ **"白天"=计数 0 的自然状态，绝不能在白天做 `-1`**，否则战备永久锁死

## 组件存活/回收（易踩）
- `VFXManager.Release(GameObject)` 只认根物体上的 `ParticleSystem`(Stop)/`LimitedLife`(置 `allowRelease`)，皆无=空转；`ProjectileBase` 走另一重载（回池+`Template`）
- `LimitedLife.IsAlive()` 只被 VFXManager 池 Update 轮询 → 非池化实例（含 Nest 预置）不会被回收；`HealthOther.AutoDestroy` 才是"死亡即销毁"；`SubtitleAirdrop.Update` 也用 `IsAlive()`
- `LimitedLife` **延时回收**：到寿先 `InvokeEnd()`（`endInvoked` 保证一次），再等 `EndDelay` 秒；`allowRelease` 强制回收**不走延时**；`AllowPreRelease` 阈值 `+EndDelay`；`OnShow`/`ResetLift`/延长寿命都复位 `endInvoked`
- 池化对象"本次状态"字段必须在 OnDisable/OnEnable 复位，且放在早退 return 之前

## 昼夜与天气
- `DayNightBrain` + Modules（`TimeProgression`/`CelestialRotation`/`CelestialVisuals`/`EnvironmentLighting`/`DayNightTrigger`）；预制体 `Day-Night-Manager.prefab`（**桥 Utnapishitim 与 TestScene 各一实例**）；天空盒运行时建副本写值（`GetSkyboxForWrite`，编辑器非播放不写）；旧 `Effect/DayNightCycle.cs` 不参与玩法场景
- 渐变时间轴约定（模块头注释）：**0%=日出、25%=正午、50%=日落、75%=午夜、100%=日出**
- `EnvironmentLightingModule`：环境光三色 ×`AmbientBrightnessMultiplier`；天空盒有 `_Lerp` 写 `_Lerp`、无则用 `_Exposure`（基准取组件配置 `skyBaseExposure`，**不读材质**）×`SkyLerpMultiplier`；沙尘把 `_SkyTint`/`_GroundColor` 向 `SkyDustColor` 插值、`_AtmosphereThickness` 按 `GetSkyAtmosphereThickness`
- **全屏雾参数合成**：雾色渐变一次采样两路用——RGB=雾色（×环境光倍率），**A=该时刻「雾出现程度」遮罩**；`intensity = Clamp01(昼夜强度曲线 × 渐变A + 天气 FogIntensityAdd)`（**天气增量不参与遮罩**，否则正午 A=0 会连暴雨一起抵消）；`density = 曲线 / max(visibility,0.05)`；`startLine/endLine ×visibility`；高度雾 `±heightRise = (1-visibility)*fogHeightRise + FogHeightAdd`。`color.a` 最终被 Feature 用 intensity 覆盖 → 遮罩只经由 intensity 生效
- 地图雾色渐变资产现状：`m_NumAlphaKeys=2`、`atime0=0/atime1=65535`、`key0.a=key1.a=1` → **A 通道恒为 1**（遮罩是无副作用的可调通道）
- 天气：`WeatherSystem`（开局按 `mapCfg.WeatherInfos` 用 `BattleRandom` 抽）+`WeatherEffect`（Rain/Desert/Snow），`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")`；预制体默认值须中性
- `WeatherAtmosphereController`（**全局命名空间**静态桥，供 Assembly-CSharp 免 using 访问）：`TaskManager` 选图时注入 `FogColorGradient = mapData.fogColor`；天气侧写 `Target*`，消费方每帧读 `*Multiplier`；`Smooth(dt)` 有帧保护（`_lastSmoothFrame`）。字段：`Visibility/FogIntensityAdd/CloudCoverage/CloudBrightness/SunIntensity/AmbientBrightness/SkyLerpMultiplier/SkyDust/SkyDustColor/FogHeightAdd/FogColor`

## 任务系统
- 抽象链 `MissionBase : TickBehaviour`（Tick 1s；`UpdateText/UpdateTip/UpdateMission/CompleteMission/FailMission/EndMission/Uninit/Link/Activation`；`Uninit` 由 `EndMission` 或 `OnDestroy(!end)` 调）→ `MissionEvacuateBase` → 静态(终端 KeyScreen)/动态(凯伊 ReturnBag)；`UseSceneStartPoint` 虚开关控快速模式是否顶替 StartPoint（Mobile 覆写 false）
- `MedivacController`：`Land`=插入下机；`Evacuate`=撤离接人；`TakeOff()`（Play"Evacuate"+开 cam+派发 `Complete`+**只隐藏 IsInBox 玩家**）/`IsInBox`/`ForceTakeOff()`
- 「创建后」走 `InitMission`，「全部任务初始化后」走 `StartMission`；`Link(mission)` 订阅 `mission.OnMissionEnd += Activation`；子任务 `OnMissionCompleted` 汇总给父任务
- 主任务 `MissionCompleteKeySceern`（拼写如此）=终端流程；`MissionOilRefining`：Init 空投平台(id15)/连接点(id14)→Wait 等 `MissionSubConnectPipes`→Start(Load 180s+`errorTimes` 波次)→Repair→End
- 管道状态是 `Furniture_Pipe.Id` 字符串：`Pipe`→`PipeLink`→`PipeWait`→`PipeComplete`/`PipeError`；**无"已修复"标志位**；`Operate()` 里 `base.Operate()` 先、`Complete()` 后 → 不能在 `OnOperate` 回调里读 Id 判完成，要 Tick 轮询
- 台词只能 `WndManager.CreatNotice(角色, groupName)`，**角色键=NoticeTree_SO.ID**（Ayane/Yuuka/Kai），groupName 必须真实存在

## 战备系统
- 资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；运行时 `ResSvc.airdropDic`
- `AirdropData_SO`：`type`(Red0轰炸/Blue1装备/Greed2炮台/Orange3载具/Yellow4补给)、`labels`(`[Flags]`)、`opter`（Left0/Up1/Right2/Down3，LE uint 数组）、`subAirdrop`、`creatObect`、`coolGroup`、`isHide`
- 按键序列：首键严格 →轨道轰炸/↑鹰与空中支援/↓炮台地雷装备补给/←载具；尾键(伤害类型指纹)规律基本未贯彻
- ⚠ `labels` 是后加字段：除 `ADSO_R_Railgun` 外资产 YAML 无该行（=0）；⚠ `AirdropData_SOEditor` 显式列字段，漏列不显示
- UI：`AirdropWnd`(HUD)、`AirdropConfigWnd`、`ArmamentWnd`；`ArmamentButton.prefab` 子1=偏好标记；`ArchivesData_SO` 的 `AirdropBuyDic`/`AirdropPreferList`
- 部署链路：`VFXAirdropEffect` 按 `deliveryType` 分支（Pod 用 `Instantiate(creatObect)`；`ImpactVfx` 由 `FpsHelper.Hit` 走 `VFXManager.Creat`+`SetOwner`）；改 `arriveTime` 须同步 `time`；`permanentPod` 会 `LimitedLife.ResetLift(9999)`
- **授权**：`AirdropController.Authorize(id,state)` → `authorizeCounter += state?1:-1`（**0=隐藏且不可用**，`IsAuthorize=counter>0`）；任务必需战备 `TaskManager.RequiredAD = {SupplyId, HealBag, IlluminatorId(17), LampTowerId(16)}`
- **昼夜解锁照明战备**：`BattleManager.OnDatSwitch(isNoon)` 暂存 → `ApplyDaySwitch()` → `Authorize(LampTowerId/IlluminatorId, isNight)`，**只在翻转时动计数**（`_nightAuthorized` 去重），白天什么都不做；开局事件会漏 → `LastDaySwitchIsNoon` 缓存 + `ApplyInitDaySwitch()` 补发

## 其他功能
- 敌人特效（`02Game/AI/FxCont`）：`EnemyControllerFX`（抽象 partial）+`EnemyFXControllerUnit`/`BuildingFXController`；Start 订阅 `I_AIController` 事件、OnDestroy 退订；`TriggerFX(type,pos,rot,parent,ignoreAudio)` 是统一入口
  - 三层配置：渲染模板 SO `EnemyFxData_SO.rendererSet`（按 `sharedMaterials[i]==mat` 匹配槽位）/事件 SO `EnemyFxEventData_SO.fxDic`/组件 `fxMaterial`/`BirthMaterial`/`Animator`；SO 不存实例→`EVT_*.go` 全空；`_HitColor` 为自发光叠加
  - **MPB 所有权在"渲染槽位"**（`RendererSlot{Renderer,MaterialIndex,mpb,dirty}`）：条目只 SetColor，帧末 `UpdateRS()` 统一 Flush；同槽位只能一块；收尾必须写回"无效果值"
- 伤害总入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸(`BattleManager.FindUnits`/`GetExplosionDamage`/穿甲/拆毁)→冲击波→**地形破坏**→警告/特效/音效/弹痕
- 地雷 `02Game/Gameplay/Projectile/DeployableMine.cs`（`Mine`/`RobotMine`/`spore`）：`Actor`/`Health`/`LimitedLife` 三件套；触发链路 = 自动命中/被摧毁/外部调**幂等的** `TriggerExplosion()` → `OnTriggered` → `ExplosionDelay` 秒协程 → `DoExplosion()`（伤害 → `OnExploded`（**在 `VFXManager.Release` 之前**派发）→ 回池）；`LimitedLife` 到寿"已触发则立即引爆、未触发才 `Tool.Destroy`"
- 寻路：`PathRequestManager` 禁止 `pathPending` 期间"超时重试"，只在 `pathPending=false` 后判 `PathInvalid/PathPartial` 并投影重试+10m 兜底；log 由 `EnemyController.SetNavDestination(isImportant)` 控
- 音频：`AudioManaqerBase.sourcePool` 工厂须 `SetActive(false)`；NPC 语音家具 `Furniture_NPCChat` 用 `SoundGroup_SO`+协程等 `AudioSource.isPlaying`
- `PlayerWeaponsManager.OnWeaponSwitched`：`isSec=true` 时只设左手 IK，**不要**覆盖主武器右手 IK；`PhoenixEagleController` 旋转乱跳=阶段切换别改 `lastPos.y`
- 项目全景：伤害模型（弱点/护甲/护盾/抗性）、属性、武器、投射物、玩家（视角/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次、`UnitQueryGrid`、噪声地形、任务、昼夜+夜间敌袭、战备、战略大地图、撤离、购买与偏好
- 改进优先级：① 随机源统一 `BattleRandom` ② `I_Damagable`/`I_Entity` 解耦为纯 DTO ③ 统一命名空间+rootNamespace ④ God Class 审查 ⑤ `UnitQueryGrid` List 池化
- ⚠ 本仓库植被/岩石素材包基本**未被游戏场景引用**（只在各自 Demo/TestScene 用）；`Rock1LOD_grup*.prefab` 在 Lod/ 下才被地形原型引用

## UI 展示模型（ArmamentWnd/SettingWnd/AirdropConfigWnd）
- prefab→禁 MonoBehaviour+Collider、Rigidbody kinematic→`SetChildLayer`；独立相机+RenderTexture 预览，`RectTransformUtility.RectangleContainsScreenPoint` 判拖动区
- ⚠ 要**连 Awake 都不执行**：`enabled=false` 无效，须在未激活挂点下实例化→移除逻辑组件→再激活（Destroy 帧末生效）；现成实现 `AirdropConfigWnd.ShowModel`
- `FitModelScale`：相机视野半高/半宽 + 包围盒 XZ 半对角线/Y 半高求缩放；中心对齐 `focus=camPos+fwd*dot(root-camPos,fwd)`；朝向 `_modelBaseEuler`(默认 180)+`ApplyModelRotation()`
- ⚠ 量包围盒四前提：① 未激活 bounds=0 ② 带 Animator 先 `animator.Update(0f)` 再等一帧 ③ 蒙皮 bounds 随姿势变 ④ 量前把自身旋转归零
- ⚠ **不能 Encapsulate 全部 Renderer**：混零体积粒子/几百米 LineRenderer/范围网格。过滤顺序：画不出的→特效类→材质名关键字(`RangeMaterialKeys`)→零体积；全只剩特效才退回全量

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath/serializedObject 才用 `PropertyDrawer`；`InlineFieldDrawer` 是 `[Singleline]` 内联的**唯一实现**
- `EditorOverride`（全局 fallback Inspector）**被专属 `[CustomEditor]` 完全顶掉**（全仓仅 `SoundGroup_SOEditor`、`AirdropData_SOEditor`）；反射自建 Drawer 须手动注入 `m_Attribute`，特性类标 `CustomPropertyDrawer`，取目标类型用 `GetCustomAttributesData().ConstructorArguments[0].Value as Type`
- 复用：`SOPickerPopup<T>`、`PrefabBatchToolBase`；Drawer 集中 `Drawer/`
- `[DisplayField]`：编辑期不画、运行期只读；只对已序列化字段生效；白名单 Integer/Float/Boolean/String/ObjectReference/Color/Vector2/Vector3
- 数据编辑器：`Editor/DataEditorWindow.cs`+`DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须补 `DrawField("新字段")`
- `Assets/Editor/MaterialUsageFinder.cs`（`Tools/材质引用查询`）：`GetDependencies(path,false)` 建「材质/模型→引用者」表，再定位 `Renderer.sharedMaterials[i]`/`Terrain.materialTemplate`/`Graphic.m_Material`/任意序列化字段，沿表 BFS 出预制体变体（只收 `PrefabAssetType.Variant`）
- ⚠ `DisplayProgressBar` 会派发编辑器事件 → 长任务窗口须加重入锁 `_busy`；⚠ **EditorWindow 私有字段会被 Unity 存档并在域重载后恢复**，UI 开关应在 OnEnable 复位
- asmdef 限制：`Editor/Tool/EditorTools.asmdef` 的 `references: []` → 看不到 `UnityEngine.UI`；要用 UGUI/URP/项目类型就放 `Assets/Editor/`

## UI 图片/配色词汇表（`Assets/Images/`，参照 SelectRoleWnd.prefab）
- 按钮底 `FX_TEX_Lock.png`（Sliced，ppu=2，亮青 0.65/0.87/0.87）；锁定框 `FX_TEX_Lock_Frame.png`；列表条目/中面板 `frame_panel13.png`（Sliced）：未选中 (0.84,1,1,0.40)(RoleButton)/(0.88,0.96,1,0.40)(WeaponPreviewItem)，选中 (1,0.97,0.84,0.40)(SelectBtn)
- 大面板框 `SelectFrame3/4.png`（Sliced，ppu=0.5，(0.67,0.96,1,0.70)）；气泡 `SelectFrame2.png`；小图标框 `UI_Frame4.png`；图标底 `Common_Main_SkillBG.png`；六边形 `Hexagonal_Frame(2).png`；进度/连接条 `LinkBar.png`(Filled)；细边框 `frame5/6.png`；箭头 `Arrow_Left/Right/Left2/Right2/Down.png`；全屏底 `99997.png`；纯色深底惯例：无 sprite+(0,0,0,0.2~0.55)

## MCP for Unity
- ⚠ **本机实时实例 = `Bluedivers@4a3e6a7b`**（path `D:/Pro/Bluedivers/Assets`，port 6400，Unity 2022.3.62f3，本会话实测）。工作区规则里写的 `Bluedivers@3d9f2357` + `E:/Bluedivers` 是**另一台设备**的；本机一律以 `mcpforunity://instances` 实时结果为准。Unity 安装路径 `D:/UnityHub/Editor/2022.3.62f3/Editor`
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（已入库含汉化）；手册=skill `unity-mcp/`；**写操作前先 git commit**；多实例路由细节见工作区规则「Unity MCP 多实例路由规则」
- ⚠ 会话里看不到 `mcp__`/`mcpforunity__` 工具时**不要假装能查编辑器**：只能读文件/资产（二进制 TerrainData、prefab 真实数值拿不到），要如实说"未证实"
- 客户端配置两份互不相通：IDE 读 `%USERPROFILE%\.codebuddy\mcp.json`；Unity 窗口 Configure 只写 CLI 的 `%USERPROFILE%\.codebuddy.json`；Unity 侧 Transport 必须 Stdio
- `execute_code` 只有 CodeDom(C#6)：不能写现代语法；`set_active_instance` 后 `refresh_unity` + 轮询 `mcpforunity://editor/state` 的 `advice.ready_for_tools`
- 改材质关键字/属性后 `EditorUtility.SetDirty`+`AssetDatabase.SaveAssets`；⚠ `Editor/Tools/Build/*.cs` 曾被 .gitignore 误清→编译失败(MCP 全挂)，升级包前先 `git add -f` 汉化
- 改完脚本：`validate_script`(level=standard) → `refresh_unity` → 桥短暂失联属域重载，等 ~10s 再 `read_console`（`types` 传**数组**）；⚠ `refresh_unity` 可能检测不到 .cs 变更，兜底 `AssetDatabase.Refresh(ForceUpdate)`+`CompilationPipeline.RequestScriptCompilation()`
- ⚠ 新 `.cs` 文件**不会**被 `refresh_unity` 导入；编辑器里 `AddComponent` 不调 `Awake`（`Instance` 为 null）

## 第三方素材包
- Pandazole 包（`Assets/Pandazole_Ultimate_Pack`）99 个 fbx 经 `.fbx.meta` 的 `externalObjects` 全部重映射到**共享材质** `PandaMat.mat`，shader = 项目的 `Assets/Shader/ToonLit/Main/ToonLit_Scene.shader`（URP 下正常，不会洋红）
- ⚠⚠ **Pandazole 包 git 状态与磁盘长期不一致**：磁盘是 `Tree/{Models,Prefabs}`+`Details`，git 索引里却是顶层 `Models/`(518 条)+`Prefabs/` 扁平结构，`Tree/`、`Details/` **全为 untracked（612 个文件）** → 工作区常驻 **1180 个 `D` + 612 个 `??`**。⇒ 该包内资产改动**无法用 git 回滚**，批量改资产前先备份；`PrefabUtility.SaveAsPrefabAsset` 会重建文件但不改 fbx

## 参考手册：地形/植被/破坏
- `Assets/Art/TerrainData/MainMap.asset`（**二进制**读不了）；地形**每局运行时重建**：`BattleManager.InitTerrain` 改 `heightmapResolution=MapRes+1`/`alphamapResolution=MapRes`(512/1024)/`size=(MapSize,MapHeight,MapSize)` → `MapRoot.Init(true)` → `SetTextures` → `ApplyFractalNoiseToTerrain`。**→ 场景预摆物体 Y 每局失效**
- 实测地形：`640×96×640`、heightRes 1025、alphaRes 1024、detailRes 512、`treePrototypes=11`（0-6 石块/7-10 树）、`detailPrototypes=7`（全是草）
- `GenerateNoiseTerrain` 顺序：基础地形→后处理→材质→分块提交高度图(128)→alphamap→`PlaceRockCovers`→`SpawnVegetation`(石块/树)→`SetTreeInstances`→`TreeDestructor.Rebuild`→`TerrainDetailEraser.Rebuild`→`RebuildTreeColliders`→`SpawnDetails`(草)→NavMesh
- `ApplyFractalNoiseToTerrain(type, stone[], tree[], detail[], stoneMul, treeMul, rockCoverMul, detailsMul)`：树原型=**石块在前+树紧随**，区间按数组长度推导（`(0,stone-1)`/`(stone,…)`，0=空区间）；同步写 `TerrainClearer.Rock/TreePrototypeRange`；必须 `RefreshPrototypes()`
- ⚠⚠ 换 `treePrototypes` 前必须先清树实例（`SetTreeInstances(Array.Empty<TreeInstance>(), false)`），否则刷 `Tree removed: invalid prototype N`
- ⚠ **无兜底**：MapData 的 `stone/treePrototypes` 空=该图无石块与树；`detailPrototypes` 空=无草（`MD_Millennium` 三组空 → 光地）
- 生成参数：`TerrainPresetData.rockSpawn/treeSpawn`(`probability/minSlope/maxSlope/minHeight/maxHeight`)+`rockCover`+`detail*`；`probability`=**每格高度图索引**概率，先掷骰早退再查高度/坡度，缩放 `Random.Range(0.7,1.5)`；`_overridePreset`+`_rockProbability`/`treeProbability`(<0=沿用预设)；地图级倍率 `StoneSpawnMultiplier`/`TreeSpawnMultiplier`/`RockCoverMultiplier`/`detailsMultiplier`（默认 1，0=不生成）
- ⚠ `GetSteepness` 的 `cellSize=1/16` 是**历史近似**（非真实米制坡度；真实格距=`size.x/(heightmapResolution-1)` 且高差要乘 `size.y`）→ 实测坡度被压缩 5~40 倍，只保证大小关系。**覆盖石走另一条真实米制路径**（`SampleRockFootprint` 按占地圆采样米制高差）
- **覆盖石（悬崖/巨石）**：`count`+占地圆互斥+高度（归一化）+**占地圆倾角**+**排除峰顶**（`isLocalPeak = 中心高 - max(近圈,外圈) > 0.5m`）；多出来的尝试由 `_rockCoverAttemptsPerCover×targetCount` 限
- 细节（草）层数据存在 TerrainData 资产里：⚠ **细节原型变少时多出的层不会自动清**，必须在换原型数组**之前** `SetDetailLayer(0,0,i,零数组)`（`GenerateNoiseTerrain.ClearDetailLayers`）；`SpawnDetails` 即使不撒草也要把各层提交为空。`detailPrototypes==null`=不碰，长度 0=本图无草（清空）
- **细节（草）渲染模式**（Unity 2022.3 手册「Grass and other details」）：共 4 种 = **Instanced mesh**（勾 `useInstancing`，推荐）/ Vertex Lit mesh（`renderMode=VertexLit`，不实例化，**所有实例合并成一个网格**、数量受限、只用 `MainTex`）/ Grass mesh（`renderMode=Grass`，法线朝上+**随风摆动**）/ Grass Texture。**勾了 Use GPU Instancing 后 Render Mode 变灰失效**（renderMode 值不再有意义）；实例化会**禁用 Healthy/Dry Color 噪声**（要自己 shader 里做），每批 ≤1023 实例，不吃 lightprobe/lightmap，用 **prefab 的材质+shader** 渲染且用持久化实例 CB（GPU 内存略增）
  - 本项目实测：`MainMap.asset` 的 7 个细节原型是**混合模式**——`[0..3] renderMode=Grass, useInstancing=false`、`[4..6] VertexLit, useInstancing=true`；材质 `PandaMat2`(shader `ToonLit/ToonLit_Scene`) **`enableInstancing=true`**；Shader 有 `#pragma multi_compile_instancing` 但**全仓 ToonLit 没有 `UNITY_SETUP_INSTANCE_ID`/`UNITY_INSTANCING_BUFFER`**（不支持 per-instance 属性）。当前地形 7 层共 **43,327** 个细节实例（每层约 6,000，约 1,400 格 / 512×512）
  - ⚠ `new DetailPrototype()` 默认 = `usePrototypeMesh=false, useInstancing=false, renderMode=Grass(2), density=1, useDensityScaling=false, targetCoverage=1, alignToGround=0, positionJitter=0, minW=1/maxW=2/minH=1/maxH=2, noiseSpread=0.1`（`DetailRenderMode` 值：GrassBillboard=0、VertexLit=1、Grass=2）⇒ **代码注入超过资产原型数量的层会拿到这套默认值**，模式不统一
  - ✅ **已落地（`ApplyMapPrototypes`）**：注入的每层统一写死 `renderMode = DetailRenderMode.VertexLit` + `useInstancing = true`，不再继承资产（`DetailPrototype` 是 **class**，但 `terrainData.detailPrototypes` getter **每次返回新对象副本**，实测 `ReferenceEquals(a[0],b[0])==false` → 改写不污染资产；尺寸/噪声/颜色仍沿用资产同索引原型）；配套 `CollectMaterialsWithoutInstancing`（材质没勾 Enable GPU Instancing 就 warn）+ `LogDetailPrototypes`（打印每层模式，进图可核对）
- 贴图注入 `SetTextures`（实测 6 层，`CampData_SO.NestTerrainItem` 覆盖巢穴层）；`ApplyTextures` **硬编码**贴图索引 1/2/3/4
- ⚠ **隐形碰撞体**：草永远无碰撞体；树/石碰撞体=原型 prefab 的 **CapsuleCollider**+`TerrainCollider.m_EnableTreeColliders`（**不看 LODGroup**），属 `TerrainCollider` 非 GameObject；`HaveObstacle()` 不做前探 ⇒ 不报"有障碍物"；诊断=从"脚底+1.8m"向上射线；修复=删原型上除树干胶囊外所有 Collider
- ⚠ 树原型**必须带 `LODGroup`** 才会被地形渲染（无 → 判为 Tree Editor 树 → 基本不渲染但仍产生隐形胶囊）。工具 `Assets/Editor/TerrainTreePrototypePreparer.cs`（`Tools/地形树原型预处理`）已对 Pandazole 99 个 prefab 全量执行成功
- ⚠⚠ **树碰撞体不随 `SetTreeInstances` 更新**：必须 `TerrainUtils.RebuildTreeColliders(terrain)`（切一次 enabled，≈10.75ms）→ 已接入 `TerrainClearer`（`CommitVersion` 门控+同帧去重+`ColliderSyncInterval=0.5s`）
- 悬崖落点**按网格最低点对齐**：`GetRendererMinY(go) - go.transform.position.y`，pivot 放到 `地表 - sinkDepth - 该偏移`
- `TerrainData` **无 `RemoveTreeInstance`**；只能整表 `SetTreeInstance(s)`（单实例版不能改 position/prototypeIndex）；`TreeInstance.position` 归一化 0~1、`rotation` 是 X-Z 弧度；`treePrototypes`/`detailPrototypes` 是 public get/set（getter 返回副本）
- 树销毁 `08Map/TreeDestructor.cs`（按帧合并，限流 4/s）：销毁=实例 scale 置 0（索引不变、可 `RestoreAll`），`SetTreeInstances(arr,false)`+`terrain.Flush()`；`DestroyInRadius` 走白名单，`ClearInRadius/ClearInRectXZ` 按原型区间（-1=不限）；白名单落点 `_destructibleTreePrototypes`
- 草擦除 `08Map/TerrainDetailEraser.cs`（限流 8/s，`Rebuild(Terrain)` 自动挂载）：逐层 `GetDetailLayer→清零→SetDetailLayer`（rect 先 Clamp；**`Terrain.position` 是角点**）
- **地表物清除统一门面 `08Map/TerrainClearer.cs`**（静态 + `[Flags] TerrainClearTarget{Tree,Rock,Vegetation,Detail,VegetationAndDetail,Cover,All}`）：请求入队 → 帧末隐藏驱动器统一下发 → 按 `RefreshInterval`(0.25s) 合并，**顺序必须先生效再重烘**；`Rebuild` 含 `RockCoverDestructor.Rebuild`=ClearAll，**只能在放置覆盖物之前调**
- **弹坑链路**：`FpsHelper.Hit`（`destructe>0`）→ 擦雪+`ModifyHeightMap(refresh:false)`+`MarkTerrainChanged()`；`ModifyTerrain`（圆按 `outerRadius`；附加地形=角点+半尺寸矩形+`transitionDistance` 过渡带；`clearVegetationRange`(7,10)/`clearRockRange`(0,6)，**石块必须一起清**）挂在 31 个任务/兴趣点 prefab；兜底 `MissionController.InitializeAsync` 末尾 `AsyncRefresh(true)`
- 最贵四步：① `ModifyHeightMap`(`SetHeightsDelayLOD`+`SyncHeightmap`+`ModifyAlphaMap`) ② 树实例整表提交 ③ 树碰撞体重建 ④ `UpdateNavMesh`。实测：`RebuildTreeColliders`≈10.75ms、`SetTreeInstances(3650)`=0.08ms、`terrain.Flush`=0.69ms、高度图写+Sync 2~5ms、`UpdateNavMesh` 5.6~8.8ms。计时入口 `TerrainClearer.LogTiming/ResetTimingStats`、`TerrainUtils.Last*Ms/HeightModifyCount`
- `TerrainUtils`（partial static，`00Tools/Test/TerrainMainUtils.cs`）：`WSToHeight`（prefab 落位贴地用它）、`WRToHR/WRToAR/ARToHR`、`ModifyHeightMap`、`AdditionTerrain`、`Refresh/AsyncRefresh(refreshNav)`、`RebuildTreeColliders`；`AreaCircles`（地表占地圆）唯一写入者=`RockCoverDestructor`、生产者=`PlaceRockCovers`、消费者=`MissionController`/`ModifyTerrain`
- 层掩码（`GameContract.LayerDefinition`）：子弹 `HittableLayers=73`、高速 601、玩家移动 8265、`UnitSeeLayers=16393`、`AirWallLayers=8192`；Terrain 在 Ground(3)、`preserveTreePrototypeLayers=False`；`Physics.queriesHitTriggers=True`
- **NavMesh**：MapRoot 上 `NavMeshSurface`，`collectObjects=Volume(1)`、`UseGeometry=0`(RenderMeshes)、LayerMask=8(Ground) → **树/石块（Default 层）不进 NavMesh**；想让运行时摆的石头挡 AI：放 Ground 层或生成后再烘
- "看不见的碰撞体"三类：① 地形树/石碰撞体（非 GameObject）② `MapRoot.CreatAirWall()` 18 个边界墙 ③ 建筑隐形代理（`OilPlane`/`Turret_Mission_Machine` 的 `PlaneA1..F`/`Spine2`/`StairsA1..C2`/`GunArmor`，Default 层无 Renderer）+`EntityRoot.KeyScreen`

## 参考手册：渲染
- **URP14 pass 时序**：Opaques 300 / BeforeSkybox 350 / AfterSkybox 400（CopyColor 生成 `_CameraOpaqueTexture`）/ BeforeTransparents **450** / AfterTransparents 500 / BeforePostProcessing 550；**同 event 时 Renderer Feature 排在 URP 内置 Pass 之前** → 全屏雾(450) 先于透明 Pass(450) → "队列 Transparent 的物体永远不吃雾"
- 背景层（`Environment/SkyboxLayer`：星星/月亮/云，尺度 13000~15000，`ZClip Off`）：队列 <2500 → 被天空盒擦掉；队列 Transparent → 能看到但不吃雾。**解法（已落地）**：Pass 改自定义 `LightMode="SkyboxLayerBeforeFog"` + `Rendering/SkyboxLayerBeforeFogRendererFeature.cs` 在 **445** 绘制；要点：① pass 必须 `CoreUtils.SetRenderTarget(cmd, colorHandle, depthHandle)` 同时绑深度 ② FilteringSettings 用 `RenderQueueRange.transparent`+`SortingCriteria.CommonTransparent`。同款先例 `WarpingBeforeFogRendererFeature`(445)；**自定义 LightMode 的代价 = 特性被禁用后该特效完全不显示**
- URP 资产 `Assets/Setting/New Universal Render Pipeline Asset.asset` → `..._Renderer.asset`；已挂 5 个 Feature：Outline(300)/Snow(300)/SkyboxLayerBeforeFog(445)/WarpingBeforeFog(445)/FullScreenFog(450)；⚠ 手改别动 `m_RendererFeatureMap`，用 `SerializedObject`+`AddObjectToAsset` 后调 `ValidateRendererFeatures()`（反射）重写 map
- **全屏雾包** `Packages/Fog`（Meryuhi，ns `Meryuhi.Rendering`）：`FullScreenFog : VolumeComponent`(`mode/intensity/color/densityMode/startLine/endLine/startHeight/endHeight/density/noise*`)；Feature 内 **`color.a = intensity.value` → intensity 即最终混合强度**；`densityMode` 默认 `ExponentialSquared` 时 `density` 进 `_MainParams.y`，`mode=HeightAndDistance` 时另传 `_HeightParams`；`IsActive() => intensity.value != 0`（**恰好 0 → 整个 Pass 跳过**）；`FullScreenFogController.Enabled` 是总开关；只对 `CameraRenderType.Base` 生效
- **ToonLit**：5 pass（ForwardLit/Outline/ShadowCaster/DepthOnly/DepthNormalsOnly）**共用 `ToonLit_Shared.hlsl`**（7 shader 共用：`ToonLit`/`_Colour`/`_Face`/`_Hair`/`_MouthEye`/`_Scene`/`_Stone`）；`GetFinalBaseColor` 是 albedo 唯一入口；透明完全靠 `clip()`
- 给 shared 加东西三条边界：① 宏只写在**目标 shader 自己的 `HLSLINCLUDE`** ② shared 里只**新增** `#ifdef` 块 ③ 新数据只走**全局 uniform**（不进 Properties/CBUFFER，别动 `Varyings`）；**宏按 pass 精确控制**
- 附加光阴影需同时：变体带 `_ADDITIONAL_LIGHT_SHADOWS` + 3 参 `GetAdditionalLight(i,posWS,shadowMask)`；`LIGHT_LOOP_BEGIN` 前先声明 `InputData inputData`。URP Asset：附加光 Realtime、`m_AdditionalLightShadowsSupported:1`、`m_ShadowDistance:100`、级联 3；URP14 `USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA` 硬编码 0
- 未写进 CBUFFER 的属性=死属性；新增 CBUFFER 字段须同步 Properties；toggle 局部关键字名=**原名**（无 `_ON` 后缀）；`_ShadowMapColor`=暗面里该光源的乘数；`_MAIN_LIGHT_SHADOWS` 管接收；**材质不投影首因**=`disabledShaderPasses: - SHADOWCASTER`；UI 相机勾 Clear Depth 会致贴花消失
- `ToonLit_Stone.shader`：`Cull Back` 硬编码、`_BaseMap`+`_BlendingMap` 双图混合
- **让材质纹理自动跟随数据 = 脚本注入 + 保留 shader**：① MPB per-renderer ② `Shader.SetGlobalTexture` 全局纹理+`#define` 支路（推荐；全局纹理**不可写进 Properties**）③ 真随地形层变才改 shader。**已实施**：`ToonLit_Stone` ForwardLit/Outline + shared 两处 `#ifdef` + `GenerateNoiseTerrain.SetTextures` 里 `SetGlobalTexture("_TerrainBaseTex"/"_TerrainBlendingTex")`（⚠ 材质贴图槽位对石头不再生效）
- 从 Properties 删**被代码读取**的属性 = CBUFFER 取 0；`_BaseMap_ST`/`_BlendingMap_ST` 仍被 uv 使用不能删；`ToonLit.shader`(main) 挂 `CustomEditor "ToonLitMainShaderGUI"`（不判空）→ 别在 main 上删属性；`FakeAreaLight.shader` 菲涅尔：`_FresnelScale` 符号定方向、绝对值定强度
- **屏幕空间贴花**（`Assets/Shader/Decal/`，源自 NiloCat）：`SimpleDecal`(fog 恒开)/`SimpleDecal_Colour`；cube 罩目标+`ZTest Off`/`ZWrite off`/`Queue Transparent-499`。⚠ `_Cull` 必须 Front(1) 或 Off(0)、绝不能 Back(2)；⚠ 雾与混合模式必须匹配（加法混合会在纹理全黑处叠雾色），已加 `[Toggle(_DecalAdditiveFog)]`；⚠ 预乘 `col.rgb *= col.a` + 硬件 SrcAlpha = alpha²
- **自定义 UI Shader**：`Mask` 走模板缓冲、`RectMask2D` 走 `CanvasRenderer.EnableRectClipping`+`_ClipRect`；必须自带 `#pragma multi_compile_local _ UNITY_UI_CLIP_RECT`、`float4 _ClipRect`（**不写进 Properties**）、顶点局部坐标传片元、`UnityGet2DClipping`；常见失效：Graphic 未勾 Maskable、图标在子 Canvas 下
- **积雪**：`SnowController`(`_SnowEnabled`/`_GlobalSnowAmount`/`_SnowMask`+`_SnowMaskRect`/`_SnowMaskTiles`)+RF(300)+`SnowVolume`；挂钩 `FpsHelper.Hit`、`BattleManager.InitTerrain`（换局 `ResetMask`）。**不是后处理，是"用雪材质重画一遍"**（`DrawRenderers`+overrideMaterial+`RenderQueueRange.all`）→ 原材质 clip 全透明也会留**幽灵轮廓**。层配置：`snowEntries[0]` mask=65→`SnowOverlay_Unit.mat`、`[1]` mask=8(Ground)→`SnowOverlay_Ground.mat`；**Default 层默认就积雪**
