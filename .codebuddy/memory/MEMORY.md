# Bluedivers 长期记忆（索引）

> **只留结论与坑；数据/过程/代码细节见各日 `YYYY-MM-DD.md`。** 2026-09-27 压缩整理。

## 协作与通用坑
- 不确定先问；不动无关 using；改名/移动由用户**手动**做，AI 只给方案
- 问"能不能/为什么没有"→ 先给**结论+证据(文件:行)**，再给选项与代价，最后才谈落地
- 双机/换机后先确认 MCP 连的是当前副本，再动写操作
- Inspector 特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 顶掉全局 fallback
- 资产 YAML 缺整行字段 = 脚本新增后资产未重存；Unity **保留 C# 字段初始化器的值** → 新增非 0 默认值字段安全；往**结构体末尾**加字段最稳
- 诊断"组件没生效"先看是否**根本没人调它**；视觉问题先看**实际材质/资产属性值**+物体落在哪个 Pass/队列
- 家具：新建 `Furniture_Attached` 子类（**不改**基类静态字典）；非 Actor 物体必须 override `ShowName/Id/Icon`；可运行时 AddComponent
- ⚠ 别什么都往 `BattleEventSub` 塞：能拿实例就订阅**实例事件**，全局层只留真广播，跨实例状态别用 `static`；例外=需"记住最后值给晚订阅者"的初始广播
- 新增脚本尽量**运行时自动挂载**，少改场景/prefab YAML；必须手改 prefab 时明确告知；改 URP renderer 资产走 MCP `SerializedObject`
- ⚠ 新增 `const`/`static` 放**类顶部**（规范硬要求）；上游包文件丢失排查用 `git ls-files` 与磁盘比对

## 环境与工具链
- 副本：`D:\Pro\Bluedivers`、`D:\Project\RTSClient`；**MCP 实例 hash/端口随路径变**，勿照抄规则里的 `E:\Bluedivers`
- Unity 2022.3.62f3 / URP 14.0.12 / C#9+.NET Std 2.1；单机 PvE（FPS Sample 改，`Unity.FPS.*`）；随机源统一 `BattleRandom`、确定性用 `PEMaths`
- git `core.autocrlf=true`；GitHub 直连被重置 → 走 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore` 的 Unity 生成目录规则必须 `/` 锚根（裸 `[Bb]uild/` 曾吞 `Packages/**/Tools/Build/` 等）
- 工具坑：`search_content` 的 `glob` 不可靠（花括号无效）→ 整目录搜或单文件 `path`，并带 `headLimit`；`findstr` 读部分 fbx 报错 → `select-string`；无 `Debug.DrawWireSphere` → `Tool.DrawWireSphere`
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`

## 结构与命名
- `Assets/Scripts/` 数字前缀=依赖顺序（00Core→GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），上层可引下层，反之禁止
- asmdef 共 10 个（`00_Utils` 含 **TerrainUtils**、`08_Map` 含 MapUtils）；`01Manager/`、`02Data/`、`02Game/`、`04UI/` 根脚本、`Effect/*.cs`、`Rendering/*.cs` 全在 **Assembly-CSharp**；asmdef 均 `autoReferenced:true` → CSharp 可调它们、反向不行
- ⚠ **asmdef 内引用不到 Assembly-CSharp**（`08_Map` 用不了 `MapData_SO`）→ 跨层只传 `GameObject[]`/基础类型，由上层从 SO 取
- 存在 **01Manager↔02Game↔02Data 三角循环**（02Data 引 `FpsGame.Mission`/`Unity.FPS.Game`）→ 拆 asmdef 须先断 02Data→02Game
- 约定：跨模块优先事件；接口主流 `I_` 前缀；SO 用 `_SO`；partial 用 `主类名_分部名.cs`；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 只对**字段**有效
- 层索引：0 Default / 3 **Ground** / 6 Unit / 13 AirWall / 14 Smoke / 15 ModelView

## 架构与数据分层
- 组件间避免 `GetComponent` 互取；AI 用 `AIController` 组合+接口代理，**不继承 `Actor`**；竖直占位 `I_Entity.HalfHeight`（0=不过滤）；批量填 `Assets/Editor/ActorHalfHeightTool.cs`
- 窗口：`WndManager` 挂 `public XxxWnd` 字段（prefab 填 0），`Init()` 由 UnityEvent 调；家具入口 `Furniture_*.furnData` 按 `Id` 分发
- **SO 不能持有 prefab/场景实例引用**：只放与实例无关项（音效/粒子/材质/颜色/数值）；子物体/挂点放 prefab 的 `[SerializeField]`；"同类单位一起吃?"→SO，语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；需"任意组件任意方法"才用 UnityEvent；共享+需实例层级→间接引用（key/相对路径+运行时解析+编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- 家具：`Furniture_Attached : BaseMono, IFurniture`，OnEnable/OnDisable 维护静态 `list`（交互扫描源）；`Handle(user)`→`CanOperate`+`Operate`
- **交互键（E）解析在 `PlayerOperationController.{Update→Handle}`**：`target` = 第一人称屏幕中心 Raycast(1.3m) 或第三人称 2m 内最近可交互物；有 `target` 时 E 归交互物
- 第三人称交互拉近：`PlayerController_ThirdPerson.ThirdPersonDistanceScale`(0.1~1) 乘到两分支的 `distance`；`PlayerOperationController` 每帧写 `IsThirdPerson && target!=null ? 0.5f : 1f`，窗口切换时还原 1
- 装备：`IEquippable` + `EquipController.Equips`；装卸只由交互家具调 `InstallEquip/UninstallEquip`；交互家具"被携带"时自身 `enabled=false` → **装备想监听输入必须写在 `IEquippable` 实现里**；`InputState.Operate`=E(5)、`Equip`=X(22)
- 计时器：`TickBehaviour.Tick()` **1 秒 1 次**；`CreateTimer(cb,秒,次数,endcb)`；`CreatePerTimer`=**每帧回调**持续秒
- 波次：`BattleManager.CreatWave(WaveCreateParams)` → `RobotWave`(凤凰鹰)/`ZergWave`(空降)/`KaiserWave`；皆 `I_TickClass`、1s Tick，`End` 时 `Dispose()` 返回 false 被移出；`onEnd` 在 `Dispose` 回调（续刷安全）；`centerGetter` **只影响尚未落点**的单位
  - ZergWave **重部署**：`centerGetter!=null`+距 `anchorCenter`>50m → `RedeployPods()` 重投
  - `EnemyController.PatrolPos` 是**到达即自毁**哨兵点（`HomePoint` 才是到达待命）；巡逻队只能 `BattleManager.CreatPatrol(Vector3)`

## 场景加载与事件时序（易踩）
- `ResSvc.AsyncLoadScene` 用 `LoadSceneMode.Single`，**回调被 `DelayedInvoke` 延后一帧** → **场景内组件 `Start` 一定早于回调里 `AddComponent<BattleManager>()`**
- ⇒ 凡"场景物体 `Start` 里只抛一次"的初始事件（如 `DayNightBrain.Start`→`GlobalEventSub.DaySwitch`），**BattleManager 收不到开局那一次**。修法：总线缓存最后值 + `BattleManager` 就绪后 `ApplyInitDaySwitch()` 补发
- 同理：`Start`/回调之后才创建的（`BattleManager`、战备 UI）必须"补初始状态"
- `BattleManager._initQueue`（`EnqueueInit`）给"早于 BattleManager 存在的物体"排队，末尾 `DrainInitQueue()` 兑现；依赖 `ADCont` 前必须判空
- ⚠⚠ **`authorizeCounter` 裸计数、无下限**（`+= state?1:-1`）⇒ **"白天"=计数 0 的自然态，绝不能在白天做 `-1`**，否则战备永久锁死

## 组件存活/回收（易踩）
- `VFXManager.Release(GameObject)` 只认根物体上的 `ParticleSystem`(Stop)/`LimitedLife`(置 `allowRelease`)，皆无=空转；`ProjectileBase` 走另一重载（回池+`Template`）
- `LimitedLife.IsAlive()` 只被 VFXManager 池 Update 轮询 → 非池化实例（含 Nest 预置）不会被回收；`HealthOther.AutoDestroy` 才是"死亡即销毁"
- `LimitedLife` **延时回收**：到寿先 `InvokeEnd()`（`endInvoked` 保证一次）再等 `EndDelay` 秒；`allowRelease` 强制回收**不走延时**
- 池化对象"本次状态"字段必须在 OnDisable/OnEnable 复位，且放在早退 return 之前

## 昼夜与天气
- `DayNightBrain` + Modules（`TimeProgression`/`CelestialRotation`/`CelestialVisuals`/`EnvironmentLighting`/`DayNightTrigger`）；预制体 `Day-Night-Manager.prefab`（**桥 Utnapishitim 与 TestScene 各一实例**）；旧 `Effect/DayNightCycle.cs` 不参与玩法场景
- 渐变时间轴：**0%=日出、25%=正午、50%=日落、75%=午夜、100%=日出**
- `EnvironmentLightingModule`：环境光三色 ×`AmbientBrightnessMultiplier`；天空盒有 `_Lerp` 写 `_Lerp`、无则用 `_Exposure`（基准取组件配置，**不读材质**）；`_GroundColor` 取 **`equatorColor`（凑合）**；写值走 `GetSkyboxForWrite()`（**只在 `Application.isPlaying` 建副本**）
- ⚠ Procedural 天空盒只有 `_SkyTint`(乘性)/`_GroundColor`/`_AtmosphereThickness`/`_Exposure`/`_Rotation`/`_SunDisk*`，**无赤道色**；`_AtmosphereThickness` 是物理大气密度，**不是浑浊调色**；`Skybox_Test.mat` 用它、`Utnapishitim` 的 `m_Sun: 0` → 拿不到太阳方向
- **全屏雾合成**：雾色渐变 RGB=雾色（×环境光倍率），**A=该时刻"雾出现程度"遮罩**；`intensity=Clamp01(昼夜曲线×渐变A + 天气 FogIntensityAdd)`（**天气增量不参与遮罩**）；`density=曲线/max(visibility,0.05)`；`startLine/endLine×visibility`；`color.a` 最终被 Feature 用 intensity 覆盖
- 天气：`WeatherSystem`（开局按 `mapCfg.WeatherInfos` 用 `BattleRandom` 抽）+`WeatherEffect`（Rain/Desert/Snow），`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")`
- `WeatherAtmosphereController`（**全局命名空间**静态桥，供 Assembly-CSharp 免 using 访问）：选图时注入 `FogColorGradient`；天气侧写 `Target*`，消费方每帧读 `*Multiplier`

## 任务系统
- `MissionBase : TickBehaviour`（Tick 1s）→ `MissionEvacuateBase` → 静态(终端 KeyScreen)/动态(凯伊 ReturnBag)；`UseSceneStartPoint` 虚开关控快速模式是否顶替 StartPoint
- `MedivacController`：`Land`=插入下机；`Evacuate`=撤离接人；`TakeOff()`=Play"Evacuate"+开 cam+派发 `Complete`+**只隐藏 IsInBox 玩家**
- 「创建后」走 `InitMission`，「全部初始化后」走 `StartMission`；`Link(mission)` 订阅 `OnMissionEnd`；子任务完成汇给父任务
- 主任务 `MissionCompleteKeySceern`（拼写如此）=终端流程；`MissionOilRefining`：Init 空投平台+连接点→Wait 等 `MissionSubConnectPipes`→Start(180s+波次)→Repair→End
- 管道状态是 `Furniture_Pipe.Id` 字符串（`Pipe`→`PipeLink`→`PipeWait`→`PipeComplete`/`PipeError`）；**无"已修复"标志位** → 不能在 `OnOperate` 回调读 Id 判完成，要 Tick 轮询
- 台词只能 `WndManager.CreatNotice(角色, groupName)`，**角色键=NoticeTree_SO.ID**，groupName 必须真实存在

## 战备系统
- 资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；运行时 `ResSvc.airdropDic`
- `AirdropData_SO`：`type`(Red轰炸/Blue装备/Greed炮台/Orange载具/Yellow补给)、`labels`(`[Flags]`)、`opter`（Left0/Up1/Right2/Down3，LE uint 数组）、`subAirdrop`、`creatObect`、`coolGroup`、`isHide`
- 按键序列首键严格：→轨道轰炸/↑鹰与空中支援/↓炮台地雷装备补给/←载具；尾键(伤害类型指纹)基本未贯彻
- ⚠ `labels` 是后加字段 → 除 `ADSO_R_Railgun` 外资产 YAML 无该行（=0）；⚠ `AirdropData_SOEditor` 显式列字段，漏列不显示
- UI：`AirdropWnd`(HUD)、`AirdropConfigWnd`、`ArmamentWnd`；`ArmamentButton.prefab` 子1=偏好标记
- 部署：`VFXAirdropEffect` 按 `deliveryType` 分支；改 `arriveTime` 须同步 `time`；`permanentPod` 会 `LimitedLife.ResetLift(9999)`
- **授权**：`AirdropController.Authorize(id,state)` → 计数 `+= state?1:-1`（**0=隐藏且不可用**）；必需战备 `TaskManager.RequiredAD = {SupplyId, HealBag, IlluminatorId(17), LampTowerId(16)}`
- **昼夜解锁照明战备**：`ApplyDaySwitch()` 里 `Authorize(LampTowerId/IlluminatorId, isNight)`，**只在翻转时动计数**；开局事件会漏 → `ApplyInitDaySwitch()` 补发

## 其他功能
- 敌人特效（`02Game/AI/FxCont`）：`EnemyControllerFX`（抽象 partial）+ 两个子类；三层配置（渲染模板 SO 按 `sharedMaterials[i]==mat` 匹配槽位 / 事件 SO / 组件字段）；SO 不存实例 → `EVT_*.go` 全空
  - **MPB 所有权在"渲染槽位"**：条目只 SetColor，帧末统一 Flush；同槽位只能一块；收尾必须写回"无效果值"
- 伤害总入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸(穿甲/拆毁)→冲击波→**地形破坏**→警告/特效/音效/弹痕
- 地雷 `DeployableMine.cs`：`Actor`/`Health`/`LimitedLife` 三件套；外部调**幂等的** `TriggerExplosion()` → 延时协程 → `DoExplosion()`（`OnExploded` **在 `VFXManager.Release` 之前**派发）；`LimitedLife` 到寿"已触发则立即引爆、未触发才销毁"
- 寻路：`PathRequestManager` 禁止 `pathPending` 期间"超时重试"，只在 `pathPending=false` 后判 `PathInvalid/PathPartial` 并投影重试+10m 兜底；log 由 `EnemyController.SetNavDestination(isImportant)` 控
- 音频：`AudioManaqerBase.sourcePool` 工厂须 `SetActive(false)`；`Furniture_NPCChat` 用 `SoundGroup_SO`+协程等 `AudioSource.isPlaying`
- `PlayerWeaponsManager.OnWeaponSwitched`：`isSec=true` 只设左手 IK，**不要**覆盖主武器右手 IK；`PhoenixEagleController` 旋转乱跳=阶段切换别改 `lastPos.y`
- 改进优先级：① 随机源统一 ② 接口解耦为纯 DTO ③ 统一命名空间 ④ God Class 审查 ⑤ `UnitQueryGrid` 池化
- ⚠ 本仓库植被/岩石素材包基本**未被游戏场景引用**（只在各自 Demo 用）

## UI 展示模型（ArmamentWnd/SettingWnd/AirdropConfigWnd）
- prefab→禁 MonoBehaviour+Collider、Rigidbody kinematic→`SetChildLayer`；独立相机+RenderTexture 预览
- ⚠ 要**连 Awake 都不执行**：`enabled=false` 无效，须在未激活挂点下实例化→移除逻辑组件→再激活；现成实现 `AirdropConfigWnd.ShowModel`
- `FitModelScale`：视野半高/半宽 + 包围盒求缩放；中心对齐用 `focus` 投影；朝向 `_modelBaseEuler`(默认 180)
- ⚠ 量包围盒四前提：① 未激活 bounds=0 ② 带 Animator 先 `Update(0f)` 再等一帧 ③ 蒙皮 bounds 随姿势变 ④ 量前旋转归零
- ⚠ **不能 Encapsulate 全部 Renderer**（混零体积粒子/超长 LineRenderer/范围网格）；过滤顺序：画不出的→特效类→材质名关键字→零体积

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath 才用 `PropertyDrawer`；`InlineFieldDrawer` 是 `[Singleline]` 内联的**唯一实现**
- `EditorOverride`（全局 fallback Inspector）**被专属 `[CustomEditor]` 完全顶掉**（全仓仅 2 个）；反射自建 Drawer 须手动注入 `m_Attribute`，特性类标 `CustomPropertyDrawer`
- 复用：`SOPickerPopup<T>`、`PrefabBatchToolBase`；Drawer 集中 `Drawer/`
- `[DisplayField]`：编辑期不画、运行期只读；只对已序列化字段生效
- 数据编辑器：`Editor/DataEditorWindow.cs`+`DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须补 `DrawField("新字段")`
- `Assets/Editor/MaterialUsageFinder.cs`（`Tools/材质引用查询`）：`GetDependencies` 建「材质/模型→引用者」表再 BFS 出预制体变体
- ⚠ `DisplayProgressBar` 会派发编辑器事件 → 长任务窗口须加重入锁；⚠ **EditorWindow 私有字段会被 Unity 存档并在域重载后恢复**，UI 开关应在 OnEnable 复位
- ⚠ `Editor/Tool/EditorTools.asmdef` 的 `references: []` → 看不到 `UnityEngine.UI`；要用 UGUI/项目类型就放 `Assets/Editor/`
- **`[Compare]` 条件显隐**（`FPSGame.Attribute`，`00Attribute/CustomAttribute.cs`）：按**同层**字段的值切换字段显示（隐藏=高度 0 不占位）；⚠ 单参构造 = `Equal + 0`（"等于 0"）、双参默认操作符是 `Greater`（"大于"），要显式写；`Contain/NotContain` 是位掩码（`[Flags]` 用）；⚠ 一个字段只能挂一个（`AllowMultiple=false`）；⚠ **找不到控制字段 → 隐藏 + 每帧 `LogError`**，"字段莫名消失+Console 刷 error"先查是否同层
- 控制字段定位 `CustomLabelDrawer.GetContainerPath`（2026-09-27 修）：`a.b.c`→`a.b`；`a.list.Array.data[i]`（特性挂 List 字段本身）→容器 `a`；`a.list.Array.data[i].x`（特性挂元素内字段）→容器 `a.list.Array.data[i]`（**修前推成顶层 `contField`，字段永远不显示**）；专属 `[CustomEditor]` 手列字段的必须自己调 `CustomLabelDrawer.ShouldDisplayField`

## UI 图片/配色（`Assets/Images/`，参照 SelectRoleWnd.prefab）
- 按钮底 `FX_TEX_Lock.png`（Sliced，ppu=2，亮青 0.65/0.87/0.87）；锁定框 `FX_TEX_Lock_Frame.png`；列表条目/中面板 `frame_panel13.png`（Sliced）：未选中 (0.84,1,1,0.40)/(0.88,0.96,1,0.40)，选中 (1,0.97,0.84,0.40)
- 大面板框 `SelectFrame3/4.png`（ppu=0.5，(0.67,0.96,1,0.70)）；气泡 `SelectFrame2.png`；小图标框 `UI_Frame4.png`；图标底 `Common_Main_SkillBG.png`；六边形 `Hexagonal_Frame(2).png`；进度条 `LinkBar.png`；细边框 `frame5/6.png`；箭头 `Arrow_*.png`；全屏底 `99997.png`；纯色深底惯例：无 sprite+(0,0,0,0.2~0.55)

## MCP for Unity
- ⚠ **本机实时实例 = `Bluedivers@4a3e6a7b`**（`D:/Pro/Bluedivers/Assets`，port 6401）；规则里的 `E:/Bluedivers` 是**另一台设备**，本机以 `mcpforunity://instances` 实时结果为准
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（已入库含汉化）；手册=skill `unity-mcp/`；**写操作前先 git commit**
- ⚠ 会话里看不到 `mcp__`/`mcpforunity__` 工具时**不要假装能查编辑器**：只能读文件/资产，要如实说"未证实"
- 客户端配置两份互不相通：IDE 读 `~/.codebuddy/mcp.json`；Unity 窗口 Configure 只写 CLI 的 `~/.codebuddy.json`；Transport 必须 Stdio
- `execute_code` 只有 CodeDom(C#6)：不能写现代语法；`set_active_instance` 后 `refresh_unity`+轮询 `advice.ready_for_tools`
- 改材质关键字后 `SetDirty`+`SaveAssets`；⚠ `Editor/Tools/Build/*.cs` 曾被 .gitignore 误清→编译失败，升级包前先 `git add -f`
- 改完脚本：`validate_script`(standard) → `refresh_unity` → 等 ~10s 再 `read_console`（`types` 传**数组**）；⚠ `refresh_unity` 可能检测不到 .cs 变更 → 用 `execute_code` 比 `Assembly-CSharp.dll` 与 .cs 的 mtime
- ⚠ 新 `.cs` 文件**不会**被 `refresh_unity` 导入；编辑器里 `AddComponent` 不调 `Awake`

## 第三方素材包
- Pandazole：99 个 fbx 经 `.fbx.meta` 的 `externalObjects` 全重映射到 `PandaMat.mat`；树 prefab 在 Ground 层、47 个 `Details/Prefabs/*.prefab` 在 Default 层且共用 `PandaMat2.mat`
- ⚠⚠ **Pandazole git 索引与磁盘长期不一致**（顶层扁平 vs `Tree/`+`Details/`）→ 常驻 1180 `D` + 612 `??` ⇒ 该包改动**无法 git 回滚**，批量改前先备份

## 参考手册：地形/植被/破坏（细节见 09-20/09-21 日志）
- 地形**每局运行时重建**（`BattleManager.InitTerrain`→`MapRoot.Init(true)`→`SetTextures`→`ApplyFractalNoiseToTerrain`）⇒ **场景预摆物体 Y 每局失效**；实测 640×96×640、`treePrototypes=11`(0-6 石块/7-10 树)、`detailPrototypes=7`(全草)；Terrain 物体在 Ground 层
- **分辨率代价（09-28 实测；`InitTerrain` 里 `mapRes=Large?1024:512` → heightRes=mapRes+1、alphaRes=mapRes）**：heightRes 1025→2049 = TerrainData 内存 2.1→8.3MB(×4)、**整张高度图写+Sync 417→4631ms(×11，直接加在开局)**、同世界半径 patch 的 `SetHeights+Sync` 5.96→23.63ms(×4，每弹坑/每条 `ModifyTerrain` 一次⇒爆炸掉帧)；`Flush`/`UpdateNavMesh`/TerrainCollider 三角数同量级涨；**树/草/draw call/地形形状不受影响**（`GenerateNoiseTerrain.mapscale` 只用于 `TerrainPosToWorldPos`，自适应）
- ⚠ `heightRes-1` 与 `alphaRes` 当前 **1:1**（1024==1024）：`ModifyAlphaMap` 原用 `WRToHR` 求半径却交给按 `alphamapResolution` 取 patch 的 `GetAlphas`，靠这个巧合才正确 → 已改 `WRToAR`（数值零变化）；否则**单独翻倍 heightRes 会让贴图改动范围错 2 倍**
- 生成顺序：基础地形→材质→高度图→alphamap→`PlaceRockCovers`→`SpawnVegetation`→`SetTreeInstances`→`TreeDestructor.Rebuild`→`TerrainDetailEraser.Rebuild`→`RebuildTreeColliders`→`SpawnDetails`→NavMesh
- ⚠⚠ 换 `treePrototypes` 前必须先清树实例，否则刷 `Tree removed: invalid prototype N`
- ⚠ **无兜底**：MapData 的 `stone/treePrototypes` 空=该图无石块与树；`detailPrototypes` 空=无草
- ⚠ 细节原型变少时**多出的层不会自动清**，必须在换原型数组**之前** `SetDetailLayer(0,0,i,零数组)`
- ⚠ `GetSteepness` 的 `cellSize=1/16` 是历史近似（坡度被压缩 5~40 倍）；覆盖石走另一条真实米制路径
- ⚠ **隐形碰撞体**：草无碰撞体；树/石=原型 prefab 的 **CapsuleCollider**+`m_EnableTreeColliders`（**不看 LODGroup**）；诊断=脚底+1.8m 向上射线；树原型**必须带 LODGroup** 才渲染（否则隐形胶囊仍在）
- ⚠⚠ **树碰撞体不随 `SetTreeInstances` 更新** → 必须 `TerrainUtils.RebuildTreeColliders`（切 enabled，≈10.75ms）；已接入 `TerrainClearer`（`CommitVersion` 门控+同帧去重+0.5s 限流）
- `TerrainData` **无 `RemoveTreeInstance`**，只能整表 `SetTreeInstance(s)`；`position` 归一化 0~1、`rotation` 是 X-Z 弧度；`detailPrototypes` getter 返回副本（改写不污染资产）
- 清除门面 `TerrainClearer`（`TerrainClearTarget` 位标志）：请求入队→帧末驱动器合并下发（`RefreshInterval` 0.25s），**顺序必须先生效再重烘**；`Rebuild` 只能在放置覆盖物之前调
- 弹坑链路：`FpsHelper.Hit`→擦雪+`ModifyHeightMap(refresh:false)`+`MarkTerrainChanged()`；`ModifyTerrain` 按 `shape` 分流（圆→`ModifyHeightMap`+`ClearInRadius`、矩形→`ModifyHeightMapRect`+`ClearInRectXZ`），**石块必须一起清**；兜底 `MissionController.InitializeAsync` 末尾 `AsyncRefresh(true)`
  - `ModifyHeightMap` 只支持 Circle/Ellipse/Rectangle；`Ellipse` 历史实现是**正方形**；`Rectangle`/`ModifyHeightMapRect` 两轴独立半长、**可绕 Y 旋转**（`angleDeg` 通常传 `transform.eulerAngles.y`：兴趣点实例运行时会被随机 Y 旋转，不传角度 4 条边会拧成"风车"：中心跟着转了、矩形方向没转）；旋转时补丁按**外接圆半径**取，轴对齐时仍是 `max(ox,oz)`
  - ⚠ `TerrainClearer.ClearInRectXZ` 及其消费者（`TreeDestructor`/`TerrainDetailEraser`/`RockCoverDestructor`）**只支持轴对齐矩形**（细节擦除连矩形判定都没有，直接清整个 AABB）→ 旋转矩形要清地表物需另加角度
  - **矩形用显式内外框**：`矩形外半尺寸`(淡化到 0 + 清除范围，逐轴回落 外半径) + `矩形内半尺寸`(完全拉平，逐轴回落 `外框 × 内半径/外半径`)；淡化走 `RectFalloffPower` 逐轴归一化（等比例内外框时与圆形的标量式等价，允许内外长宽比不同）；圆/椭圆仍是 `(1-d)/(1-内/外)`，未动
  - ⚠ `ModifyTerrain` 的 `depth == 0` **不是"不改高度"**：`isSet=true` 时 `centerHeight = 中心旧高`，整块按 `Lerp(旧高, 中心高, power)` ⇒ 内圈被**拉平到中心高度**（+ 圆形还 `ClearInRadius(外半径-transitionDistance)` 清植被）→ "只想清场/改贴图"的条目会顺手平掉一大块
  - ⚠ `transitionDistance` 是 prefab 级唯一值：细条（半长 < transition）算出的清除半尺寸为负 → `ClearInRectXZ` 直接 no-op ⇒ 沟内的树石不清、挖完悬空；细条沟要单独调小（0.5 级）
  - ⚠ `ModifyTerrain.OnDrawGizmosSelected` 的框**必须画在 `pos`**（`transform.TransformPoint(localPos)`，与运行时同中心）；**Prefab Mode 里预制体场景没有地形，`Physics.Raycast` 会穿到主场景地形上**（实测高差 60m）→ 画在命中点就等于画到高空看不见。射线只用于 `onGround` 判色（命中 && |Δy|≤`GizmoGroundSnapRange`20m）；内框高度 = `down * depth`
- 最贵四步：`ModifyHeightMap`/树实例整表提交/树碰撞体重建/`UpdateNavMesh`（实测 10.75ms、0.08ms、`Flush` 0.69ms、5.6~8.8ms）；计时入口 `TerrainClearer.LogTiming`
- `WRToHR/WRToAR/ARToHR` 返回 **int**（除法先转 float）；`AreaCircles` 生产者=`PlaceRockCovers`、消费者=`MissionController`/`ModifyTerrain`
- 层掩码：子弹 73、高速 601、玩家移动 8265、`UnitSeeLayers=16393`、`AirWallLayers=8192`
- **NavMesh**：MapRoot 上 `NavMeshSurface`（`Volume`+RenderMeshes+Ground）；运行时摆的石头要挡 AI 就必须放 Ground 层
- "看不见的碰撞体"三类：地形树/石碰撞体、`MapRoot.CreatAirWall()` 18 面墙、建筑 Default 层无 Renderer 的代理体

## 参考手册：渲染（细节见 09-23/09-25/09-27 日志）
- **URP14 pass 时序**：Opaques 300 / AfterSkybox 400 / BeforeTransparents **450** / AfterTransparents 500 / BeforePostProcessing 550；**同 event 时 Feature 排在内置 Pass 之前** → 队列 Transparent 的物体永远不吃雾
- 背景层（SkyboxLayer，尺度 1.3~1.5 万，`ZClip Off`）：队列 <2500 被天空盒擦掉、Transparent 能看到但不吃雾 → 已改自定义 `LightMode="SkyboxLayerBeforeFog"`+RF 在 **445** 绘制（必须同时绑 color+depth）；**代价=特性被禁用后特效完全不显示**
- URP 资产 `..._Renderer.asset` 挂 5 Feature：Outline(300)/Snow(300)/SkyboxLayerBeforeFog(445)/WarpingBeforeFog(445)/FullScreenFog(450)；⚠ 手改别动 `m_RendererFeatureMap`（用 `SerializedObject`+`AddObjectToAsset`+`ValidateRendererFeatures()`）
- 全屏雾包（`Packages/Fog`）：Feature 内 **`color.a = intensity.value`**；`IsActive() => intensity != 0`（**恰好 0 → Pass 跳过**）
- **ToonLit**：5 pass **共用 `ToonLit_Shared.hlsl`**（7 shader 共用），`GetFinalBaseColor` 是 albedo 唯一入口，透明靠 `clip()`
- 给 shared 加东西三条边界：宏只写在**目标 shader 自己**的 HLSLINCLUDE/pass、shared 里只**新增** `#ifdef` 块、新数据只走**全局 uniform**（别动 Varyings/CBUFFER）
- 未写进 CBUFFER 的属性=死属性；toggle 局部关键字名=**原名**；**材质不投影首因**=`disabledShaderPasses: - SHADOWCASTER`；UI 相机勾 Clear Depth 会致贴花消失
- 材质纹理跟随数据：① MPB ② `Shader.SetGlobalTexture`+`#define`（推荐；**全局纹理不可写进 Properties**）③ 改 shader；已用于 `ToonLit_Stone`+`SetTextures`
- 贴花（`Assets/Shader/Decal/`）：⚠ `_Cull` 必须 Front/Off、绝不能 Back；⚠ 雾与混合模式必须匹配（加法混合会在纹理全黑处叠雾色）；⚠ 预乘 `col.rgb *= col.a`+SrcAlpha = alpha²
- 自定义 UI Shader：`RectMask2D` 需 `_ClipRect`（**不写进 Properties**）+`UNITY_UI_CLIP_RECT`+顶点局部坐标；失效常见=Graphic 未勾 Maskable
- **积雪**：**"用雪材质重画一遍"**（`DrawRenderers`+overrideMaterial+`RenderQueueRange.all`）→ 原材质 clip 全透明也会留**幽灵轮廓**；层 mask 65(Default+Unit)/8(Ground)
- ⚠⚠ **雪够不到地形细节（草）**：草不是 Renderer（Terrain 内部实例化绘制）→ **改 mask 无效**；已用方案 A 让草在自己 shader 里叠雪（`SnowOverlayCommon.hlsl`+`ToonLit_Shared.hlsl` 里 `#ifdef _SNOW_GRASS`，关键字写在材质上、**不做 [Toggle]** 以免动共享 CBUFFER）；⚠ `PandaMat2` 被 47 个细节 prefab 共用
- 渐变天空盒已落地（`Environment/GradientSkybox`，模块按 `HasProperty("_SkyColor")` 分两支）；⚠ `_SunDirection` 是 Vector 属性（存 `m_Colors`）；`_Lerp` 故意不提供（否则昼夜走 `_Lerp` 而不是 `_Exposure`）
- 天空盒三色同时驱动环境光与天空盒三段色（`nightSkyExposureScale`=0.5）→ 代价：夜间环境光变暗蓝、夜云更亮；⚠ 改 `Day-Night-Manager.prefab` 后必须**退出重进 Play**
- 地图级覆盖天空/赤道色：`MapData_SO.overrideSkyColor` → `WeatherAtmosphereController.SkyColorGradient`（**只覆盖天空盒、不影响环境光**）
