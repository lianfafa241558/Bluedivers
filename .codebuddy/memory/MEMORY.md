# Bluedivers 长期记忆（索引）

> 只留结论与坑。**2026-10-01 第十次压缩**（原文件超限被截断，按主题合并去重；明细在各日 `YYYY-MM-DD.md`）。

## 协作与通用
- **脚本优先 Python**（本机 Python 3.12 + pip + dnfile）；不写 `.ps1`；PowerShell 只作临时命令（中文易解析错、`$var` 会被吞）
- 不确定先问；不动无关 using；改名/移动由用户手动做；`const`/`static` 放类顶部
- 问"能不能/为什么没有"→ 先结论+证据(文件:行)再列选项；诊断序：谁调它 → prefab 实际数值 → 逻辑（能反射就别只读代码）
- ⚠ 文本搜索**不可全信**：不识别块注释 `/* */`、异常字节会让 grep 漏 ⇒ 结论性判据用"会剥注释的脚本 + 反射/Console"交叉复核
- 资产 YAML 缺整行字段=脚本新增字段后未重存；新字段加结构体末尾最稳
- ⚠ 少塞 `BattleEventSub`：能拿实例就订实例事件；跨实例状态别用 static；prefab 变体只改基 prefab

## 环境与工具链
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；**MCP 实例 hash 随路径变** ⇒ 每次开局读 `mcpforunity://instances` 再 `set_active_instance`（2026-10-01 实测 `Bluedivers@4a3e6a7b`:6401）
- Unity 2022.3.62f3 / URP 14.0.12 / C#9；工程 API 级别实为 **NET_Standard_2_0**；单机 PvE；随机源 `BattleRandom`
- `Lib/PEMaths.dll`：PEInt/PEVector3=米标量（`RawInt==RawFloat`）；`RawFloat` 换算后不可再运算
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`、`kcpnet-online/`；方案与工具在 `.codebuddy/plans/`

## 编译验证（反复踩，判据要齐）
- 判编译成功**必须三证齐**：`Library/ScriptAssemblies/*.dll` mtime 前进 + Console 0 error + `is_compiling:false`（Console 空 / dll 晚于源码都不够）
- Unity 会**假刷新**（refresh 返回成功却没编译）⇒ 用 `execute_code` 调 `AssetDatabase.ImportAsset(dir, ForceUpdate|ImportRecursive)` + `Refresh` + `CompilationPipeline.RequestScriptCompilation()`；必要时 Python `os.utime()` touch。⚠ 本版本**没有** `RequestScriptCompilationOptions`（勿用 `CleanBuildCache` 重载）
- **空转判据**：Console 有错 + dll mtime 不动 + **错误行号比源码差 1 行** ⇒ 编译器读的是旧快照，改源码无用
- ⚠⚠ **某程序集编译失败时，依赖它的程序集不报错也不编译**（被中止）⇒ 错误**逐层暴露**，必须迭代编译，别只看第一屏
- 目标程序集反射：`CompilationPipeline.GetAssemblies()`（`sourceFiles.Length`/`outputPath`）

## 结构与程序集（asmdef）
- `Assets/Scripts/` 数字前缀=依赖序（00Core→GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），只能上层引下层
- ✅ **`Assets/Scripts/**` 100% 进 asmdef**；自有 **20 个** + `Assets/Editor/ProjectEditors.asmdef`(41cs) + `Assets/Shader/Editor/ShaderEditors.asmdef`(3cs) ⇒ `Assembly-CSharp-Editor` 归零；`Assembly-CSharp` 只剩第三方 1 cs
- 规模：`06_Gameplay 186`/`10_UI 56`/`01_GameContract 44`/`00_Utils 30`/`09_Managers 28`/`00_Core 27`/`10_Effect 22`/`05_UnitCore 19`/`05_EffectComp 18`/`04_Data 14`/`02_Net 12`/`02_Rendering 8`/`DayNightSystem 8`/`00_Attribute 7`/`FpsGame.MapUtils 6`/`EditorTools 6`/`04_UI 5`/`DynamicBone 4`/`00_WndTools 3`/`03_Audio 1`（+MackySoft 3）
- ⚠ 过渡期铁律：① 新 asmdef 必须 `autoReferenced:true` ② **asmdef 永远看不到 `Assembly-CSharp` 的类型** ⇒ 它要用的每样东西必须已在某个 asmdef/插件 dll 里 ③ `references` 要写 asmdef 的 **`name` 字段**（`08_Map.asmdef` 的 name 是 `FpsGame.MapUtils`；`04_UI.asmdef` 的 name 是 `04_UI`，`FPSGame.UI` 只是它的命名空间）
- ⚠ 动 asmdef 前先扫 3 类"看不见的引用"：**扩展方法**（调用点不出现类名，如 `IsValid`/`RandomTake`）、**`internal` 成员**、**废弃 using 制造的假依赖**（`using static UnityEngine.Rendering.DebugUI;` 0 使用却拖包）⇒ 引用清单最终由编译器确认
- ⚠ **不要过度拆**：asmdef 只在①增量编译②强制依赖方向③独立复用④编辑器隔离上有收益；玩法层双向互引稠密 ⇒ 必须合并为 `06_Gameplay`；最终 ≈13 运行时 + 3 编辑器（原计划 ~20）
- ⛔ 编辑器 asmdef 不能引用 `Assembly-CSharp` ⇒ 编辑器程序集必须等运行时类型全移出后才建（现已满足）
- ⚠⚠⚠ **搬 `.cs` 一律用 `AssetDatabase.MoveAsset`**：文件系统 `Move-Item` + `.meta` 会被判 GUID 冲突并**重新分配 GUID** ⇒ prefab/场景上的组件变 **Missing Script**（历史 14 个 guid 变化 / 7 个真损坏：`MissionData_SO` 被 26 个资产引用、`InputManager` 被 prefab+场景引用…）。**整目录搬**比逐文件安全（实测 GUID 0 异常）。审计修复：`.codebuddy/plans/p53_guid_repair.py`（`--apply` 把旧 guid 写回新 `.meta`），每次搬完都跑。**2026-10-01 补充**：只要用 `AssetDatabase.MoveAsset`，**单文件逐个搬也零丢失**（实测 5 个文件 `AssetPathToGUID` 前后一致，`LayerConfigInitializer` 的 guid 仍被 `GameRoot.prefab` 正确引用）
- ⚠ 抽取代码块到新文件时 **using 不会跟着走**；按行区间搬运要"两阶段收集再写出"+成员完整性校验+打印头部确认大括号结构

## 命名空间（2026-10-01 全量收敛到 `FPSGame` 前缀）
- 现有：`FPSGame.{Game,AI,GameContract,Core,Core.Interface,Mission,Gameplay,Utils,Furn,EditorExt,Attribute,DayNightSystem,MapUtils,UI,EffectComp,WndTools,Game.Editor,AI.Editor,Effect,Data,Managers,Net,Rendering,Audio}`；非 FPSGame 只剩第三方（`RootMotion.*`/`MackySoft.*`/`UnityEngine.AI`(官方 NavMeshComponents，**禁改**)/`Pixeye.Unity`(仅 `Assets/Editor/Drawer/EditorOverride*.cs`)）
- ⚠⚠ **`FPSGame.Attribute` 遮蔽 `System.Attribute`**：位于 `FPSGame.*` 内且 `using FPSGame.Attribute;` 的文件裸写 `Attribute.X` ⇒ `CS0234`。修法一律 `System.Attribute.X`（已命中 `ModifyAttrDataDrawer.cs`、`CustomLabelDrawer.cs`、`InlineFieldDrawer.cs`）⇒ **命名空间批次后要全仓扫裸 `Attribute.`**
- ⚠⚠⚠ **给 `.cs` 补命名空间会打坏资产里的 `[SerializeReference]` 数据**（编译 0 error、**数据静默丢失**）：YAML 存 `type: {class: Outer/Nested, ns: , asm: X}`，`ns` 必须同步 ⇒ 否则 Unity 报 `Missing types referenced from component ...`（本次 16 prefab / 22 处 `EmittController/ScaleModify|OffsetModify` 光环失效）。工具：`.codebuddy/plans/sr_ns_audit.py`（审计）+ `sr_ns_repair.py --apply`（**字节级**，二进制 `.asset` 不能按文本解码）；验证用 `SerializedObject.FindProperty(...)` 读 `managedReferenceValue`（`ok=22 miss=0`）。⇒ **加/改命名空间后必跑此审计**
- ⚠ 补命名空间后引用方要补 `using`（CS0103/CS1061）；**扩展方法最难静态发现**，只能编译兜底
- **补 using 三段式**：①批量加 namespace + 补 using ②按 asmdef 引用剪枝（`prune_using.py`；⚠**同程序集盲区**：asmdef 不引用自己 ⇒ 会误删同集 using，判据需加"同 asmdef ⇒ 放行"+"跳过目标目录自身"）③编译兜底；误删用 `fix_using_ns.py <ns>` 精确恢复
- ⚠ 补 using 的坑：`using var`（C#8 using 声明）会被误判为 using 块尾（判定要"行首无缩进 + `using ` + 以 `;` 结尾 + 不含 `=`"）；**普通 using 必须排在 `using static X;` 与别名 using 之前**（否则 CS0246 且报错指向别名右侧）；拆行重拼要配对的 `nl.split/nl.join`（否则行尾变 `\r\r\n`）；插 using 前先确认目标程序集的 asmdef 引用了来源程序集；`rootNamespace` 要同步
- 消费点扫描必须**白名单**（`Assets/Scripts` + `Assets/Editor`），否则污染 `Assets/MackySoft/`（不在 `Plugins` 下）

## 契约层 / 服务定位器
- `01_GameContract`（`FPSGame.GameContract`）是跨层契约唯一归属地（服务接口 + 跨层封闭枚举 + 纯数据 DTO）；asmdef references 只有 `00_Core`+`00_Attribute`
- ⚠ **编译报错先甄别"新旧"，别只读 Console**：批量改动（"剪 A 文件 + 新建 B 文件"）会让 Unity **自动编译一次中间态**并留下**假错误**（实测 25 条 `CS0103: 不存在名称"LayerDefinition"` 全是残留）。**两步定性**：① 看哪个 `Library/ScriptAssemblies/*.dll` **没更新**（时间戳比依赖它的一方更旧 ⇒ 那轮失败或错误是残留）；② **直接查 `CompilationPipeline.GetAssemblies().sourceFiles`**，确认新文件真的进了目标程序集 —— 这是决定性证据。另：读 `.cs` 的 namespace **别用紧凑正则 + `Matches[0]`**（会给出错值：曾把 `FPSGame.GameContract` 读成 `GameContract`/`(global)`，导致误判"8 个文件无命名空间"）—— **`01_GameContract` 的真实 namespace = `FPSGame.GameContract`**（`asmdef.rootNamespace` 只管新建文件默认值，不等于已有代码）。
- **准入判据「三进三出」**：**进**=①服务接口（实现方在上、调用方在下，两侧互不可见）②跨层封闭枚举 ③纯数据 DTO（无引擎对象引用/无 `[SerializeField]`/无行为）；**出**=①消费者全在一条合法依赖链上 ⇒ 跟随生产层（先查依赖图确认上层能看到下层）②表现概念（`Sprite`/`Color`/`AudioClip`）⇒ 拆"逻辑值+表现查表" ③引擎/编辑器设施 ⇒ 契约层不得有组件。⚠ **反向边界（防过度净化）**：服务接口返回 `GameObject`/`Sprite` 是本质不是污染；被实体接口绑定的类型不能搬（`UnitQueryGridNode` 被 `I_Actor.GridNodes` 绑）。**2026-10-01 已落地**：契约层分 `Services(11)/Entities(12)/Data(6)/Enums(9)` + 根 2（`ServiceLocator`/`WindowRegistry`）；唯一 MonoBehaviour 已移出、`AboStateViewInfo`→`AboStateGauge`（去 `Sprite`/`Color`，表现查询移到 `HpItemBase`）⇒ **契约层已无 `Sprite`/`Color`/`AudioClip`/`MonoBehaviour`/`[SerializeField]`**（仅剩 `IResService.LoadSprite→Sprite`＝资源服务本质）。⚠ **剩余 `GameObject`/`Transform`/`Collider`/`Coroutine`/`LayerMask`/`UnityAction` 一律不改**：它们是服务/实体接口的**本质**、不是污染 —— 别为"更纯"去改 `DamagePacket`/`IHealth` 签名（~20 处跨 3 程序集，收益仅少两个引擎类型）
- ⚠ 硬判据：**基类只能待在 `min(子类层, 消费者层)`** ⇒ 搬类型前先找全部子类/派生者所在层
- `ServiceLocator` **10 槽** `Task/Battle/Flow/Vfx/Net/Res/Archive/Wnd/Room/Path`；`NullServices` 给每槽中性值（批量替换才能机械化）。**加契约成员必须同步加空对象实现**；`Dump()` 排查未注册
- **定位器硬化**：槽位 setter = `internal set` + `[assembly: InternalsVisibleTo("09_Managers")]/("10_UI")` ⇒ 下层只读（编译期保证）
- ⚠⚠ **契约替换铁律：存在性判断 ≠ 状态判断**。`X.Instance != null` 换服务成员必须用"服务已就位"语义（`IBattleService.IsPresent`），**不能**用 `IsStartBattle` 等状态位 —— `NullServices` 让槽位永不 null，状态位在服务未注册时返回 false ⇒ **静默跳过、编译无错**（`ModifyTerrain` 实拍）⇒ **批量契约替换后必须进 Play 冒烟**；排查用 `git show HEAD:<原路径>` 逐条比对语义
- 拆依赖取舍：下层只要"一个值"用**接口隔离**（`IUnitScale`/`IRoomService` 投影成员），别拖整个类型族；**"数据"让数据自持**（`AboStateData_SO.Dic`/`MissionData_SO.Catalog`/`ArchivesData_SO.Current`），**"能力"才进定位器**；服务归属看**生命周期**（`IFlowService` 必须挂状态所有者 `GameRoot`，不能挂 `BattleManager`）
- 契约表达力 5 类修法：子管理器字段⇒开窄投影（行为搬进子管理器）；要写属性⇒加 setter 方法；组件模板⇒泛型 `T Creat<T>(T,...) where T:Component`（`Component` 会丢类型）；`Release` 按类型分流；**静态成员无法隐式实现接口**（用 `add/remove` 转发）
- 未做：`~180 个 ServiceLocator 消费点`换显式注入（`Net` 槽因 `LogicBehaviour` 基类被 26 子类共用 ⇒ **不可行**）；建议 A（认定定位器为受控接缝）优于 B（部分迁移⇒两套机制并存）

## UI / 窗口跨层范式
- **`WndTypeEnum` 放契约层**（`WndManager`(管理器)/`Window` 子类(UI)/玩法层三者共见 ⇒ 三者共见的最低层）；`IWindowService` 只开**动词** `SetWndState(WndTypeEnum,bool)`；实现方 `WndManager` 用 `switch` 映射字段 ⇒ 具体 UI 类型只出现在一个文件
- ⚠⚠ **Unity 不会为 `SetActive(false)` 的对象调 `Awake`** ⇒ 窗口（平时关着）自注册会**死锁**。解法四层兜底：`Window.Awake` 自注册 + `WndHub.Scan()` 用 `FindObjectsOfType<Window>(true)` + 插槽空时回调 `Scanner` + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`。**今后 `Window` 子类不要定义 `Awake`**（会顶掉基类注册）—— **2026-10-01 已把禁令写进 `04UI/Window.cs` 的 XML 注释**（`Awake` 的 `<remarks>` 含后果链 + 替代写法，类级 `summary` 一行指引）；`WndHub` 是 `internal`（`04UI` 成集后才真正拦住）
- ⚠ 往老文件插 Unity 特性前先确认有 `using UnityEngine;`（全文用全限定名的文件插短名会 CS0246）

## 玩法 / AI / 战斗
- 组件间避免 GetComponent 互取；AI 用 `AIController` 组合，不继承 `Actor`；`I_Entity.HalfHeight`(0=不过滤)、`HalfRange>=1`=大型单位
- AI = `EnemyController`+`EnemyMobile`+`DetectionModule`；team：玩家 0、敌人 2
- `PatrolPos` 到达即自毁（`HomePoint` 才是待命）；巡逻队只能 `BattleManager.CreatPatrol`；`EnemyMobile.Start` 只读一次 `PatrolPos`；`Actor.IsFixed` 只表示"不会因长期不动被删"
- 枪声警惕：需 `BewarePoint.HasValue` 且点距 `<= Max(HearingRange,DetectionRange)`；取点后立刻 `ClearBeware()`；到达或 20s→Return；冷却 10s/半径 10m，收在 `ShouldInvestigateBeware()`
- AI 状态机内核 `02Game/AI/StateMachine/StateMachineCore.cs`（`Switch()`=派发钩子、`SetCurrent()`=纯赋值）；AI 技能 `UnitSkill_Base : TickBehaviour`(1s) 契约=`GetComponent<EnemyController>()`（纯事件型被动不继承）
- 伤害入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸→冲击波→地形破坏→警告→特效；`DamagePacket.isDirect=true` 绕开爆炸抗性/遮挡；⚠ `FpsHelper` 无 `using System;`、`in` 字段不能 lambda 捕获
- 噪声体系：`NoiseData{source,pos,radius}`+`OnNoise`；枪口 `FireNoiseRadius`=20、弹着点 `GetImpactSoundRadius`=8；消费 `DetectionModule.OnNoise`（要求 source 与 `Actor` 同物体、Team 不同、`Distance(pos,Logic3Pos)-radius < HearingRange`）；`OnBulletHit` 是表现层，与噪声两套
- 统一入口：任务进度走 `MissionBase.AddProgress/TryAddProgress`；子弹命中包走 `ProjectileBase.BuildHitData`；before-fog 手绘 Pass 走 `BeforeFogHandDrawPassBase`
- 波次：`BattleManager.CreatWave(WaveCreateParams)`；`centerGetter` 只影响未落点单位；`extraWave=true` 跳过冷却
- 上帝类已 partial 拆分：`Tool`→`_Geometry/_Math/_Vector/_String/_Color/_Enum/_Animation/_Skinning/_Coordinate/_MPB/_Draw/_Exchange`；`FpsHelper`→`_Hit/_UnitQuery/_Extension/_Utility/_Controller`

## 组件存活 / 回收 / 表现
- `VFXManager.Release` 只认根上 ParticleSystem/LimitedLife，皆无=空转；`LimitedLife` 延时回收：到寿先 `InvokeEnd()` 再等 `EndDelay`；`allowRelease` 不走延时，非池化实例不被回收
- 池化对象"本次状态"字段必须在 OnEnable/OnDisable 复位且在早退之前；⚠ `Invoke`/协程在宿主 `SetActive(false)` 时不执行 ⇒ 延时恢复的组件必须与被隐藏物体分开
- ⚠⚠ Unity 不能序列化接口字段 ⇒ 存 `GameObject` + 运行时 `GetComponent<IHealth>()`
- 表现层 FX 三分：`FxControllerBase`(抽象)/`EnemyControllerFX`(AI 绑定)/`HitDeathFX`(只订 `IHealth` 事件)；敌人特效 MPB 所有权在"渲染槽位"（帧末统一 Flush）
- 组件级"复用+不丢 prefab 数据"范式：逻辑抽到新组件，**原组件保留全部序列化字段名**，运行时 `AddComponent` 后 `Configure(...)` 注入。⚠ Unity 不支持挂泛型 MonoBehaviour
- ⚠⚠ **判 MonoBehaviour 是否被使用只能用 `.cs.meta` 的 guid 搜 `*.prefab`/`*.unity`/`*.asset`**（类名搜索无效）；删脚本 `.cs`+`.meta` 要么一起留要么一起恢复
- ⚠ 删脚本前必查：`HealthShield`(ShieldBag/Dec_Colossus Boss/ArtifactPlane)、`HealthSpecUnit`(Kei.prefab)、`ProjectileChargeParameters`、`TimedSelfDestruct`、`WeaponFuelCellHandler`(3 个武器 prefab) 都**不是**零引用

## 家具 / 装备 / 任务
- 窗口：`WndManager` 挂 `public XxxWnd` + `Init()` 由 UnityEvent 调；家具入口 `Furniture_*.furnData` 按 Id 分发
- 家具：`Furniture_Attached : BaseMono, IFurniture`（不改基类静态字典）；非 Actor 必须 override `ShowName/Id/Icon`；被携带家具 `enabled=false` ⇒ 监听输入必须写在 `IEquippable` 里；E=Operate、X=Equip
- 手持范式：`Furniture_HandEquip`（不调 `base.Operate()`）+`HandEquip : IEquippable`；事件 `OnPicked`/`OnSubmitted`；提交点只按 Id 白名单
- 计时器：`TickBehaviour.Tick()` 1s 1次（返回 false 移出）；`CreateTimer(cb,秒,次数,endcb)`；`CreatePerTimer`=每帧。⚠ `GameRootBase<T>` 的静态成员（如 `CreateTimer`）**不能从下层调用**（调用要写 `GameRoot.CreateTimer`，它在 01Manager）⇒ 下层要么委托反转（`03_Audio` 的 `TimerRequest`/`ClipLoader` 范式），要么把能力搬进 `00_Core`
- 任务/战备：`Authorize(id,state)` → `+= state?1:-1`(0=隐藏不可用)；台词只能走 `WndManager.CreatNotice(角色, groupName)`（键=`NoticeTree_SO.ID`）；`SubFlag*…SubEggHunt=200~204`（显式值，别在中间插隐式值）
- 静态事件该放哪一层：**层 = `min(发布者层, 订阅者层)`** ⇒ 3 条总线：`UnitEventSub`(随 `05_UnitCore` 下沉)/`GlobalEventSub`/`BattleEventSub`(均 `06Gameplay`)。⚠ 与其抽象签名（`Actor`→`I_Actor` 要改 20 个处理函数），**不如把总线搬到与发布者同层**（省掉全部签名改造）

## 地形 / 渲染
- NavMesh 在 `MapRoot`；asmdef 内引用不到 Assembly-CSharp ⇒ 跨层只传 `GameObject[]`/基础类型
- ToonLit：5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` 是 albedo 唯一入口；未进 CBUFFER=死属性；不投影首因=`disabledShaderPasses: - SHADOWCASTER`；纹理跟随数据用 MPB 或 `Shader.SetGlobalTexture`+`#define`（全局纹理不可写进 Properties）
- ToonLit 溶解：`_UseAlphaClipping/_UseAlphaUV`+`_AlphaMap`+`_DissolveValue(.r)`+`_EdgeWidth`+`_EdgeColor`；`dissolve=_DissolveValue.r*1.2-0.1`；`clip(step(dissolve,v)-dissolve+_EdgeWidth)`；阴影/深度 pass 必须复用 `DoClipTestToTargetAlphaValue`

## 编辑器扩展
- 需读 propertyPath 才用 `PropertyDrawer`（装饰特性用 `DecoratorDrawer`）；`EditorOverride` 会被专属 `[CustomEditor]` 顶掉
- ⚠⚠ 专属 `[CustomPropertyDrawer]` 手列字段 ⇒ 新增字段不会自动出现（行高硬编码常量，加行要同步改）
- ⚠ EditorWindow 私有字段会被存档并在域重载后恢复；`DisplayProgressBar` 会派发编辑器事件 ⇒ 长任务窗口要加锁
- `[Compare]` 按同层字段切换（隐藏=高 0）；单参=Equal+0、双参默认 Greater；找不到控制字段→隐藏+每帧 LogError
- ⚠ `[InspectorName]`=`UnityEngine.InspectorName`；`[Foldout]`/`[DisplayField]` 在 `FPSGame.Attribute` 且**只对字段有效**；组件类注释 `[AddComponentMenu("中文分类/中文名")]` 必须写在类级 `/// <summary>` **之后**（否则 XML 注释失效）
- ⚠ `replace_in_file` 会丢 UTF-8 BOM ⇒ 手改后要补回（或改用字节级 Python 脚本）

## 联机（KCPNet，试验中）
- 两级结构：预编译库 `Assets/Plugins/KCPNet/{KCPNet,Kcp}.dll`（无源码，用 skill 脚本反射核对）+ 适配层 `Assets/Scripts/NetTmp/`（`NetSvc` 成员端 / `NetHostSvc` 房主端 / `ClientSession`·`HostSession` / `MessageCenter` / `CmdId` + MessagePack DTO）
- ✅ 依赖已装齐（6 个 DLL：MessagePack 3.1.8 + Annotations + Microsoft.Bcl.AsyncInterfaces 8.0.0 + StringTools + Immutable + Unsafe 6.0.0；`install_deps.py`）；`System.Memory/System.Buffers/Numerics.Vectors` 由 Unity 自带**不要自备**
- 已实测：Console 0 error、运行时加载 KCPNet/MessagePack、`LoginReqMsg` 序列化往返 OK。模型=房主权威 + 局域网 P2P（UDP 广播发现→KCP 回连）；端口：房主 `NetConfig.HostGamePort`=17666、广播/发现 29800、同机双实例 29801；Standalone 是 IL2CPP ⇒ MessagePack 有 AOT 风险
- ⚠ 插件加载失败看 `%LOCALAPPDATA%\Unity\Editor\Editor.log` 的 `Unable to resolve reference 'X'`（Console 只给标题行）

## MCP
- 先 `set_active_instance`；写前确认 `projectRoot`；⚠⚠ **新建 `.cs` 必须先 `refresh_unity(mode=force, scope=assets)`**；改已存在文件用 `AssetDatabase.ImportAsset(path, ForceUpdate)`
- ⚠ `execute_code` 报 `No result found` 但**实际已执行**（超时/域重载）⇒ 先查实际效果再决定是否重试；`refresh_unity(wait_for_ready:true)` 必报该错，用 `false`
