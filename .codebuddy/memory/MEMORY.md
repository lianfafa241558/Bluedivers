# Bluedivers 长期记忆（索引）

> 只留结论与坑；被压缩掉的明细归档在 `2026-09-30.md` 末尾。2026-09-30 第九次压缩。

## 协作与通用
- 不确定先问；不动无关 using；改名/移动用户手动做；`const`/`static` 放类顶部
- 问"能不能/为什么没有"→ 先结论+证据(文件:行)再列选项；诊断序：谁调它 → prefab 实际数值 → 逻辑（能反射就别只读代码）
- 资产 YAML 缺整行字段=脚本新增后未重存；新字段加结构体末尾最稳（Unity 保留字段初始化器的值）
- ⚠ 少塞 `BattleEventSub`：能拿实例就订实例事件；跨实例状态别用 static；新脚本尽量运行时自动挂载；prefab 变体只改基 prefab
- ⚠ 文本搜索不可全信（异常字节→git grep/Select-String 漏）→ 用反射/Console 复核

## 环境与工具链
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；MCP 实例 hash/端口随路径变，实时看 `mcpforunity://instances`（`Bluedivers@3d9f2357`:6400、`RTSClient@6365de15`:6402）
- Unity 2022.3.62f3 / URP 14.0.12 / C#9 / .NET Std2.1；单机 PvE；随机源 `BattleRandom`
- `Lib/PEMaths.dll`：PEInt/PEVector3=米标量（`RawInt==RawFloat`）；`RawFloat` 换算后不可再运算
- 判编译：目标程序集 mtime vs 源码 mtime（`00Tools/Test/*` 属 `00_Utils.dll`）；规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`

## 结构与命名
- `Assets/Scripts/` 数字前缀=依赖序（00Core→GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），只能上层引下层
- asmdef 14 个且 `autoReferenced:true`；`01Manager`/`02Data`/`02Game`/`04UI` 根脚本、`Effect/*.cs`、`Rendering/*.cs` 在 **Assembly-CSharp**
- ⚠ asmdef 内引用不到 Assembly-CSharp（`08_Map` 用不了 `MapData_SO`）→ 跨层只传 GameObject[]/基础类型
- 约定：跨模块优先事件；接口主流 `I_`；SO 用 `_SO`；partial 用 `主类名_分部名.cs`；`[InspectorName]`=`UnityEngine.InspectorName`，`[Foldout]`/`[DisplayField]` 在 `FPSGame.Attribute` 且只对字段有效

## 架构与数据
- 组件间避免 GetComponent 互取；AI 用 `AIController` 组合，不继承 `Actor`；`I_Entity.HalfHeight`(0=不过滤)
- 窗口：`WndManager` 挂 `public XxxWnd`，`Init()` 由 UnityEvent 调；家具入口 `Furniture_*.furnData` 按 Id 分发
- 家具：`Furniture_Attached : BaseMono, IFurniture`（不改基类静态字典）；非 Actor 必须 override `ShowName/Id/Icon`
- 装备：`IEquippable`+`EquipController.Equips`；被携带家具 `enabled=false` ⇒ 监听输入必须写在 `IEquippable` 里；E=Operate、X=Equip
- 手持范式：`Furniture_HandEquip`（不调 `base.Operate()`）+`HandEquip : IEquippable`；事件 `OnPicked`/`OnSubmitted`；提交点只按 Id 白名单（先例 `Furniture_Artillery.PlaceHeldItem`）
- 计时器：`TickBehaviour.Tick()` 1s 1 次(返回 false 移出)；`CreateTimer(cb,秒,次数,endcb)`；`CreatePerTimer`=每帧
- 波次：`BattleManager.CreatWave(WaveCreateParams)`；`centerGetter` 只影响未落点单位；`extraWave=true` 跳过冷却
- AI 技能 `UnitSkill_Base : TickBehaviour`(1s) 契约=`GetComponent<EnemyController>()`(无 EC 会 NRE)，纯事件型被动不继承

## AI / 寻路
- AI = `EnemyController`+`EnemyMobile`+`DetectionModule`；team：玩家 0、敌人 2
- `PatrolPos` 到达即自毁（`HomePoint` 才是待命）；巡逻队只能 `BattleManager.CreatPatrol`
- `EnemyMobile.Start` 只读一次 PatrolPos；`UpdateCurrentAiState` 在 `!BirthComplete` 早退；⚠ `Actor.IsFixed` 只表示"不会因长期不动被删"，不是位置固定
- 枪声警惕：需 `BewarePoint.HasValue` 且点距 `<= Max(HearingRange,DetectionRange)`；取点后立刻 `ClearBeware()`；到达或 20s→Return；冷却 10s/半径 10m，收在 `ShouldInvestigateBeware()`

## 组件存活 / 回收 / 护盾
- `VFXManager.Release` 只认根上 ParticleSystem/LimitedLife，皆无=空转；`LimitedLife` 延时回收：到寿先 `InvokeEnd()` 再等 `EndDelay`，`allowRelease` 不走延时，非池化实例不被回收
- 池化对象"本次状态"字段必须在 OnDisable/OnEnable 复位且在早退前
- ⚠ `Invoke`/协程 在宿主 `SetActive(false)` 时不执行 ⇒ 延时恢复的组件必须与被隐藏物体分开
- ⚠⚠ Unity 不能序列化接口字段 ⇒ 存 `GameObject _healthGo` + 运行时 `GetComponent<IHealth>()`
- 表现层 FX 三分：`FxControllerBase`(抽象) / `EnemyControllerFX`(AI 绑定) / `HitDeathFX`(只订 `IHealth` 事件)

## 任务 / 战备
- `Authorize(id,state)` → `+= state?1:-1`(0=隐藏不可用)；台词只能 `WndManager.CreatNotice(角色, groupName)`（键=`NoticeTree_SO.ID`）
- `SubFlag*…SubEggHunt=200~204`（显式值，别在枚举中间插隐式值）

## 战斗与伤害
- 伤害入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸→冲击波→地形破坏→警告→特效
- 噪声体系：`NoiseData{source,pos,radius}`+`OnNoise`；枪口 `FireNoiseRadius`=20、弹着点 `GetImpactSoundRadius`=8；消费 `DetectionModule.OnNoise`；⚠ `OnBulletHit` 是表现层（未被噪声取代），`FireNoiseRadius`(逻辑) 与 `SFXRange`(表现) 两套
- `OnNoise` 要求 `source` 与 `Actor` 同物体、Team 不同、`Distance(pos,Logic3Pos)-radius < HearingRange`
- `DamagePacket.isDirect=true` 绕开爆炸抗性/遮挡；骨架 `FpsHelper.HitAreaKinetic`；⚠ `FpsHelper` 无 `using System;`、`in` 字段不能 lambda 捕获
- 敌人特效 MPB 所有权在"渲染槽位"（帧末统一 Flush）；地雷 `DeployableMine` 三件套+幂等 `TriggerExplosion()`

## 地形 / 渲染 / UI（要点）
- NavMesh 在 `MapRoot`
- ToonLit：5 pass 共用 `ToonLit_Shared.hlsl`；`GetFinalBaseColor` 是 albedo 唯一入口；未进 CBUFFER=死属性；不投影首因=`disabledShaderPasses: - SHADOWCASTER`；纹理跟随数据用 MPB 或 `Shader.SetGlobalTexture`+`#define`（全局纹理不可写进 Properties）
- ToonLit 溶解：`_UseAlphaClipping/_UseAlphaUV`+`_AlphaMap`+`_DissolveValue`(.r)+`_EdgeWidth`+`_EdgeColor`；`dissolve=_DissolveValue.r*1.2-0.1`；`clip(step(dissolve,v)-dissolve+_EdgeWidth)`；阴影/深度 pass 必须复用 `DoClipTestToTargetAlphaValue`

## 编辑器扩展
- 需读 propertyPath 才用 `PropertyDrawer`（装饰特性用 `DecoratorDrawer`）；`EditorOverride` 被专属 `[CustomEditor]` 顶掉
- ⚠⚠ 专属 `[CustomPropertyDrawer]` 手列字段，新增字段不会自动出现（行高硬编码常量，加行须同步改）
- ⚠ EditorWindow 私有字段会被存档并在域重载后恢复；`DisplayProgressBar` 会派发编辑器事件 ⇒ 长任务窗口加锁
- `[Compare]` 按同层字段切换（隐藏=高 0）；单参=Equal+0、双参默认 Greater；找不到控制字段→隐藏+每帧 LogError

## MCP / 第三方素材 / Shader 坑
- MCP：先 `set_active_instance("Bluedivers@3d9f2357")`；写前确认 `projectRoot==E:/Bluedivers`
- ⚠⚠ 新建 `.cs` 必须先 `refresh_unity(mode=force, scope=assets)`；改已存在文件用 `AssetDatabase.ImportAsset(path, ForceUpdate)`
