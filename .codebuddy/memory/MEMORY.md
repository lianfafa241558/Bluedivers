# Bluedivers 项目长期记忆

> 详细复盘见各日 `YYYY-MM-DD.md`；本文件只保留跨会话结论。最后整理：2026-09-12

## 项目环境
- Unity 2022.3.62f2c1，URP 14.0.12，C# 9.0/.NET Standard 2.1；TMP 3.0.9、Navigation 1.1.6、Timeline 1.7.7
- Photon PUN 已弃用、KCPNet 未完成 → 单机 PvE demo（Unity FPS Sample 二次开发，命名空间 `Unity.FPS.*`）
- 确定性计算用 `PEMaths`；随机源应统一到 `BattleRandom`

## 结构与命名
- `Assets/Scripts/` 数字前缀＝依赖顺序，上层可引用下层、反之禁止：
  `00Core→00GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering`
- 跨模块优先事件：`GlobalEventSub`（全局）/ `BattleEventSub`（战斗）
- 接口主流 `I_` 前缀；SO `_SO` 后缀；partial 分部文件 `主类名_分部名.cs` 同目录；字段 `m_`（历史）/新增优先 `[SerializeField] private`
- `[InspectorName("中文")]`/`[DisplayField]` **仅对字段有效**（属性无效）；协程动词开头，几乎不用 async/await
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skill `.codebuddy/skills/bluedivers-unity/`
- 无 `Debug.DrawWireSphere`：用 `Tool.DrawWireSphere(pos,size,color,time)`（`00Tools/Test/Tool.cs`）

## 架构约定
- 避免组件间直接 `GetComponent` 互取，跨模块优先事件
- 默认操作视角：`ArchivesData_SO.settingDic["默认操作视角"]`（0/1）→ `PlayerController.ApplyViewMode()`
- AI：`AIController` 不继承 `Actor`，组合 + 接口代理；全项目无类继承 `Actor`
- 竖直占位 `I_Entity.HalfHeight`（区间 `[CenterPos.y±HalfHeight]`，**0=未配置退化不过滤**）；批量填充 `Assets/Editor/ActorHalfHeightTool.cs`；区分空中/地面须叠加竖直检测
- asmdef 永不被 Assembly-CSharp 反向引用：`00_Utils`/`08_Map`/`04_UI`/`Effect/EffectComp` 不能直接调 `SnowController`/`BattleManager` 等 Assembly-CSharp 类型，需在 Assembly-CSharp 侧挂钩或下沉逻辑
- 程序集拆解现状：仅 9 个 asmdef，其余落在 Assembly-CSharp；**01Manager↔02Game↔02Data 三角循环**（02Data 的 SO 引用 `FpsGame.Mission`/`Unity.FPS.Game`/`FpsGame.MapUtils`）→ 须先打破 02Data 对 02Game 的 SO 配置引用。Assembly-CSharp 内 UnityEditor 引用都在 `#if` 守卫内，打包无风险

## 数据分层原则（SO / 实例）
- SO **不能持有 prefab/场景实例引用**（子物体不是资产，拖不进 Project 引用槽）→ 子物体/挂点引用留在 prefab 组件的 `[SerializeField]`；SO 只放与实例无关的（音效、粒子预制体、材质、颜色、数值）
- 判定：「这条配置改的时候，是否希望同类单位一起吃？」是→SO；否→实例。两者语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`（可校验、改名编译期报错）；UnityEvent 只在需调用"任意组件的任意方法"时用（存方法名字符串，改名/移脚本会静默失效）
- 共享 + 需引用实例层级 → 间接引用（key/相对路径 + 运行时本地解析，可加编辑器校验）

## 协作偏好
- 不确定时先暂停询问；不主动纠结/移除 using；文件夹/文件改名等结构性修改由用户**手动**完成，AI 只给方案

## 编辑器扩展基础设施
- 装饰特性一律 `DecoratorDrawer`（数组头/嵌套类/任意 Inspector 自动生效）；需读 propertyPath/serializedObject 才用 `PropertyDrawer`（数组场景须对 `.Array.data[` 回退普通绘制）
- `EditorOverride` 回退 Inspector（`Assets/Editor/Drawer/EditorOverride.cs` + `EditorOverrideInLine.cs`）：`[Foldout]`/`[InspectorName]`/`[Compare]`/单行内联数组；内联数组头部由 `DrawDecorators`+`DecoratorDrawerCache` 补画
- 反射自建 Drawer 必须手动注入 `m_Attribute` 私有字段（注入实际特性实例，空构造会丢参数）
- Unity API：特性类名是 `UnityEditor.CustomPropertyDrawer`（无 `Attribute` 后缀）；取 Drawer 目标类型用 `GetCustomAttributesData()` 读 `ConstructorArguments[0].Value as Type`
- 复用：`SOPickerPopup<T>`（SO 选择弹窗，`confirmMode` 控制单击即选/确认）、`PrefabBatchToolBase`（预制体批量工具基类）、Drawer 集中放 `Assets/Editor/Drawer/CustomLabelDrawer.cs`

## 项目全景
- 已实现：伤害模型（弱点/护甲/护盾/抗性）、属性系统、武器体系（30+ 脚本）、投射物、玩家（视角切换/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次（Zerg/Robot/Kaiser）、`UnitQueryGrid`、噪声地形（fBm+NavMesh）、任务系统、昼夜循环+夜间敌袭、战备全类别、战略大地图、撤离
- 未实现：机甲/炮塔等重型战备、联机、矿洞体素
- 架构改进优先级：① 随机源统一 `BattleRandom` ② `I_Damagable`/`I_Entity` 解耦为纯 DTO ③ 统一命名空间 + rootNamespace ④ God Class 审查（BattleManager/PlayerController）⑤ UnitQueryGrid List 池化

## 战斗结算
- 爆炸伤害落点（`DamagePacket.Pos` → `Health.TakeDamage` → `Tool.DrawLabel`/`OnHit`）应为**碰撞体表面朝向爆心的最近点**：`Collider.ClosestPoint(爆心)`；非凸 MeshCollider 退化为 `ClosestPointOnBounds`；爆心在内部或 collider 为空回落爆心（`FpsHelper.GetExplosionHitPoint`）。用爆心→数字全堆一处；用 `bounds.center`→巨型护盾显示到球心
- 爆炸伤害衰减内半径判定仍用 `bounds.center` 距离，巨型单位几乎吃不到内圈满伤（数值平衡待议）
- 穿透型投射物"多次命中同一目标"：`HashSet<GameObject>` 记 `I_Damagable.ActorGo` + `List<Collider>` 忽略列表；`SphereCast` 相邻帧扫掠段必然重叠，须命中时先登记再结算（`ProjectileStandard.m_hasHits`、`AirdropPod.m_HitUnits` 套路）

## 敌人特效（`02Game/AI/FxCont`）
- `EnemyControllerFX`（抽象 partial，实现类 `EnemyFXControllerUnit`/`EnemyControllerFX_AboState`）；`Start` 订阅 `I_AIController` 的 Attack/DetectedTarget/LostTarget/Damaged/Die，`OnDestroy` 退订
- 三块配置：① 渲染模板 SO `EnemyFxData_SO.rendererSet`（按 material 匹配槽位 + MPB 颜色，运行时构建实例私有 `RendererSet`）② 事件特效 SO `EnemyFxEventData_SO.fxDic`（`OccasionTypeEnum`→`FxSetConfig`：音效组/音频/粒子/挂点/物体开关）③ 组件上的 `fxMaterial`/`BirthMaterial`/`Animator`
- `TriggerFX(type,pos,rot,parent,ignoreAudio)` 统一入口；`ArmorBreakEffect{go,state,scale}` 做物体启用/缩放
- 资产：`Assets/Resources/GameData/EnemyFx/`（`Fxs/EFD_*`、`Events/<派系>/EVT_*`）；因 SO 不能存实例引用，`EVT_*.go` 全为空

## 渲染系统
### 雾（三层并存）
1. 内置 `RenderSettings.fog`（`EnvironmentLightingModule` 每帧写法，URP 材质靠 `MixFog` 参与，**不影响天空盒**）＝当前真正在跑的
2. 自研 `Feature/Fog` 已删除；`Shader/Feature/FogEffect.shader`、`Setting/Feature/Fog.mat` 为孤儿资产
3. Meryuhi `Packages/Fog` 的 `FullScreenFog`：
   - `_injectionPoint` 固定 **450**（BeforeRenderingTransparents）；550 会让云/雪/粒子按背后不透明物深度误雾化
   - 强制 `Height`/`HeightAndDistance`；`Depth`/`Distance` 会把整片天空刷成雾色
   - 三层开关：Renderer 特性 `m_Active` + `FullScreenFogController.Enabled`（默认 false，`WeatherEffect` 才开）+ Volume `intensity>0`
   - 雾层高度：天气预制体 `WeatherEffect._calmFogHeightAdd/_stormFogHeightAdd`（默认 0=不干预）→ `WeatherAtmosphereController.FogHeightAdd` → `EnvironmentLightingModule.UpdateFullscreenFog`
   - 透明队列抓屏 shader（Warping/Stealth，Queue=Transparent+1）不吃 450 的雾 → 提前到 **445** 手绘：`Rendering/WarpingBeforeFogRendererFeature.cs` + `Warping.shader` 的 `LightMode="WarpingEffect"`（必须改掉 UniversalForward；副作用：Warping 显示依赖该 RF）
   - 教训：相机堆叠中 UI 相机勾 Clear Depth 会导致贴花消失；"Scene 正常 Game 异常"先查 Scene 视图级开关与相机堆叠 Clear Depth

### 体积云（`Rendering/DrawVolumetricCloud.cs` + `Shader/Environment/VolumetricCloud.shader`）
- 跟随相机的半球天穹（运行时自建 mesh ~200m，`HideFlags.DontSave`）+ 每像素视线方向的"圆罩"有界投影采样：`domePos = dir.xz/(dir.y+_DomeFlatten)*altitude`（`_DomeFlatten=0.35`，→0 退化无限平面出条纹），按层内路径长度做 Beer-Lambert；`rayLength = altitude/max(dir.y,_MinRayY)` 只用于雾/遮挡/掠射加厚
- 遮挡用 `ZTest Always` + shader 内手比 `_CameraDepthTexture`（山更近则 discard；`_OcclusionBias≈3m`；整片消失先设 `_OcclusionEnabled=0`）
- 远景雾由**仰角**驱动（`_CloudHazeStart/_CloudHazeEnd` 换算正弦仰角）+ `_HazeAlphaFade` 淡出 → 无地平线硬边
- `CloudLayer[]` 多层（高度/云量/尺度/密度/细节/风速/色调），每层用独立 MaterialPropertyBlock（MPB 会帧间串数据；属性须声明在 CBUFFER 之外）
- 风是世界空间米/秒（8~10）；噪声用 Hoskins 哈希（`frac(sin(dot))*43758` 在 20km 尺度精度崩坏出条纹）
- 云色昼夜由**太阳**方向 Y + `AnimationCurve` 推算（不能用当前主光源，月亮升起会算成白天）；`_SkyTint`/`_Exposure` 归一化后写入；**禁止逐帧累积相乘**
- 主相机 far clip：`Resources/Prefabs/BattleBase/Player.prefab` near 0.01 / far 300

### 积雪（`Rendering/SnowRendererFeature.cs`）
- 静态 `SnowController`（`Shader.SetGlobalFloat` 控 `_SnowEnabled`/`_GlobalSnowAmount`）+ `SnowRendererFeature`(AfterRenderingOpaques=300，用雪材质重画配置层) + `SnowOverlay.shader` + `SnowVolume`
- 局部遮罩：`RemoveSnow/AddSnow/ResetMask/FlushMask`；`_SnowMask` 纹理阵列 + `_SnowMaskRect` + `_SnowMaskTiles`（与 `TerrainUtils.WSToUV` 同换算），CPU `byte[][]` 累加只重传脏切片；`DetectRawRowFlip()` 探针测 `SetPixelData` 行序；`_SnowMaskTiles=0` 视为满雪。挂钩：`FpsHelper.Hit`(destructe>0)、`BattleManager.InitTerrain` 后 `ResetMask()`

### 自定义 Shader 加雾模板（按几何类型二选一）
- 物体自身几何（雪/角色/地形）→ 顶点雾：`#pragma multi_compile_fog` + `ComputeFogFactor(positionHCS.z)` + `MixFog()`
- 屏幕空间/投影式（贴花、抓屏、全屏 quad）**禁止**用顶点 `positionCS.z`，必须 `ComputeFogFactorZ0ToFar(max(sceneDepthVS-_ProjectionParams.y,0))`（对齐 URP `InitializeInputDataFog`；URP14 强制片元雾）
- 已修：`Decal/SimpleDecal_Colour.shader`、`Decal/SimpleDecal.shader`、`Decal/URP_NiloCatExtension_ScreenSpaceDecal_Unlit.shader`；贴花材质保持 `_Cull:1`(Cull Front)
- 遗留：`GlowTexture2.shader:66` 传对象空间 z（无材质开启 `_MY_FOG_ENABLE`，暂无表现）
- URP 14 pass 时序：Opaques 300 / BeforeSkybox 350 / AfterSkybox 400 / BeforeTransparents 450 / AfterTransparents 500 / BeforePostProcessing 550

## 其他功能记录
- 天气：`01Manager/Battle/WeatherSystem.cs`（纯控制器，BattleManager 开局用 `BattleRandom` 抽取后 `Create`）+ `WeatherEffect` 抽象基类 + `WeatherEffectRain/Desert/Snow` 挂预制体（氛围参数全在预制体）；`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")` 按 `Type` 匹配
  - 给已有预制体加可选字段时默认值必须中性（0/false），否则旧预制体静默继承（曾让雨/雪套用沙尘参数）
  - 天空响应沙尘：`SkyDust` 染天空盒 `_SkyTint`/`_GroundColor` + `_AtmosphereThickness` + 高度雾抬升 + 云远雾转沙尘色；不动 `FullScreenFog.mode`（切 keyword 必突跳）
- `WaveManager.KaiserWave`（参考 ZergWave）：每 `360f/creats.Count` 秒建 PhoenixEagle，挂 6 单位子对象（±2 网格）；`HalfRange>=1` 大型单位单置 `(0,-5,0)` 其余重新入栈；创建时禁用 `EnemyControllerFX.Animator`，鹰 `waitTime=6s`，onWait 后第 4 秒单位 Y=鹰Y-40 并启用 Animator
- `CampTemplate.patrolTemplate`：`List<SKVP<string,int>>`（队名+权重），Drawer 下拉+权重（`CampData_SODrawer.cs` / `WaveManager` Patrol 构建）
- `Furniture_NPCChat.cs`：NPC 语音家具，SoundGroup_SO + `AudioSvc.PlaySound`，协程 `WaitForVoiceEnd` 逐条播放
- `Assets/Shader/UIImageChannelMix.shader`：UI/Image 专用（R×Image色，G×白，B忽略，A用贴图 Alpha，响应 Stencil）
- `Effect/EffectComp/VoidSpread.cs`：虚空扩散（Idle/Spreading/Shrinking，**面积驱动**：速度单位 ㎡/秒，半径=√(面积/π)，驱动 localScale 直径；`SwitchToShrink()` 供 UnityEvent）
- 战备强化：`VFXAirdropEffect.SetOwner` 里武器参数必须从 **`weaponRoot`** 取（信标特效物体 `VFX_AirdropPoint.prefab` 不含武器组件，用 `gameObject` 取不到 → 死代码）；强化只改运行时 `AirdropData`（以 `cfg` 为基准，不写 SO）；改 `data.arriveTime` 必须同步 `data.time`（State 已在 `OnRelease` 进 Arrive）

## 通用坑与教训
- 运行时不要写 `RenderSettings.*` 引用的资产对象（如天空盒材质），要写运行时副本；"读一次当基准 + 每帧乘系数"不能让被写对象当基准（编辑器 Play 会持久化 → 跨局指数衰减）
- 池化对象的"本次状态"字段必须在 `OnDisable`/`OnEnable` 复位，且复位语句要放在早退 `return` **之前**（`VFXManager` = 对象池 + `LimitedLife` 回收）

## 修复记录索引（详见各日 .md）
ModifyTerrain 地形修改后贴地；WaveManager tier 权重 `TryGetValue` 降级；ObjectPool Release 误调 `_Pop`、UnInit 崩溃；TerrainMainUtils 分辨率缓存；Health 死亡僵尸单位（`m_IsDead`）；BaseSelfMoveableController 陡坡卡死投影；DeployableMine 高空单位误引爆（HalfHeight 3D 判定）；PhoenixEagleController 旋转乱跳（过渡帧 `lastPos.y`）；PlayerWeaponsManager `OnWeaponSwitched` 忽略 `isSec` 破坏 IK；PathRequestManager 假超时重试风暴（`pathPending` 期间不重试）；AudioManaqerBase `sourcePool` 初始 `SetActive(false)`；ArchiverDataHandle 中文注释乱码（UTF-8 损坏）；AirdropPod 同一单位重复伤害；爆炸伤害点用碰撞体表面最近点
