# Bluedivers 项目长期记忆

## 项目环境
- Unity 2022.3.62f2c1（2022 LTS），URP 14.0.12，C# 9.0 / .NET Standard 2.1
- 依赖：TextMeshPro 3.0.9、Navigation 1.1.6、Timeline 1.7.7、ugui
- **Photon PUN 已弃用**；KCPNet（自研）未完成，当前为**单机版 demo**
- 逻辑数学：`PEMaths`（PEInt/PEVector 固定点数库）用于逻辑层确定性计算
- 单机 PvE 第三人称/第一人称射击割草 demo（对标绝地潜兵2+深岩银河），代码由 Unity FPS Sample 二次开发（命名空间 `Unity.FPS.*`）

## 代码结构约定
- 脚本在 `Assets/Scripts/`，按数字前缀编号划分（编号体现依赖顺序，上层可引用下层）：
  `00Core` → `00GameContract` → `00Tools` → `01Manager` → `02Data` → `02Game` → `04UI` → `08Map` → `Effect/Feature/Rendering`
- 事件约定：跨模块通知优先事件机制；全局事件在 `GlobalEventSub`，战斗相关在 `BattleEventSub`
- 规范文件：`.codebuddy/rules/UnityCSharp编码规范.md`（自动加载）；skill：`.codebuddy/skills/bluedivers-unity/`

## 命名约定（项目实际）
- 接口主流 `I_` 前缀（`I_Entity`/`I_Actor`/`I_Damagable` 等）；ScriptableObject 用 `_SO` 后缀
- partial 分部文件用下划线分隔（`主类名_分部名.cs`），同目录
- 字段：public 与 `[SerializeField] private` 并存，新增优先后者；私有/受保护用 `_` 前缀（历史有 `m_`）
- Inspector 中文化：`[InspectorName("中文")]` / `[DisplayField]`，**仅对字段有效，不能用于属性**
- 枚举后缀不统一（`GameStateEnum` vs `ActorState`）；协程以动词开头，几乎不用 async/await
- **Unity `Debug` 无 `DrawWireSphere`**：运行时画线框球必须用 `Tool.DrawWireSphere(pos, size, color, time)`（`00Tools/Test/Tool.cs`）

## 架构约定
- **降低耦合**：避免组件间直接 `GetComponent` 互取，跨模块优先事件
- **默认操作视角**：`ArchivesData_SO.settingDic["默认操作视角"]`（0=第一人称，1=第三人称），`PlayerController.ApplyViewMode()` 统一处理
- **AI 控制器**：`AIController`（02Game/AI/Controller）**不继承** `Actor`，组合+接口代理（`m_Actor = GetComponent<Actor>()`）。全项目无类继承 `Actor`
- **单位竖直占位**：`I_Entity.HalfHeight`，占位区间 = `[CenterPos.y±HalfHeight]`；**0=未配置退化不过滤**。工具 `Assets/Editor/ActorHalfHeightTool.cs` 批量填充。区分空中/地面的判定必须叠加竖直检测
- **asmdef 引用方向**：asmdef 永不被 Assembly-CSharp 反向引用。`00_Utils.asmdef`（`Assets/Scripts/00Tools/Test/`）内的 `TerrainUtils`、`08_Map`(FpsGame.MapUtils)、`04_UI`、`Effect/EffectComp` 都**不能直接调** `SnowController`/`BattleManager` 等 Assembly-CSharp 类型；需在 Assembly-CSharp 侧挂钩或把逻辑下沉到低层
- **程序集拆解现状**：仅 9 个 asmdef，01Manager/02Game/02Data 大部及 04UI 落入 Assembly-CSharp；01Manager↔02Game↔02Data 三角循环。拆解须先打破 02Data 对 02Game 的 SO 配置引用。Assembly-CSharp 内 UnityEditor 引用均在 #if 守卫内，打包无风险

## 协作偏好
- 不确定时先暂停询问，不自行大量搜索推断
- 不主动纠结/移除 using 语句，用户自行管理
- 文件夹/文件改名等结构性修改由用户**手动**完成，AI 只给方案

## 编辑器扩展基础设施
- **装饰特性（分割线/标题等）一律 `DecoratorDrawer`**：数组头部/嵌套类/任意 Inspector 自动生效；需读 propertyPath/serializedObject 才用 `PropertyDrawer`
- **EditorOverride 回退 Inspector**（`Assets/Editor/Drawer/EditorOverride.cs`）：内联数组自绘头部（`DrawInlineArrayNative`）由 `DrawDecorators` + `DecoratorDrawerCache` 补画，新 Decorator 特性零修改生效
- **反射自建 Drawer 必须手动注入 `m_Attribute` 私有字段**（Unity 只在自己的创建流程设置），否则 attribute NRE 连锁打断 GUILayout
- Unity API 坑：特性类名是 `UnityEditor.CustomPropertyDrawer`（无 `Attribute` 后缀）；取 Drawer 目标类型用 `type.GetCustomAttributesData()` 找 `CustomPropertyDrawer` 条目读 `ConstructorArguments[0].Value as Type`
- **复用**：SO 选择弹窗 `Assets/Editor/SOPickerPopup.cs` 的 `SOPickerPopup<T>`；预制体批量工具基类 `Assets/Editor/PrefabBatchToolBase.cs` 的 `PrefabBatchToolBase<TComponent>`

## 项目全景
- **已实现**：伤害模型（弱点/护甲/护盾/抗性）、属性系统、武器体系（30+ 脚本）、投射物、玩家（视角切换/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次（WaveManager Zerg/Robot/Kaiser）、UnitQueryGrid 空间网格、噪声地形（fBm+NavMesh）、任务系统、昼夜循环+夜间敌袭、战备全类别、战略大地图、撤离
- **未实现**：机甲/炮塔等重型战备、联机、矿洞体素
- **架构改进优先级**：① 统一确定性随机源（BattleRandom 与 WaveManager 的 System.Random 收口）② I_Damagable/I_Entity 解耦为纯 DTO ③ 统一命名空间+asmdef rootNamespace ④ God Class 审查（BattleManager/PlayerController）⑤ UnitQueryGrid List 池化

## 渲染系统（雾 / 云 / 雪）

### 雾（三层，易混）
1. **内置雾** `RenderSettings.fog`（`EnvironmentLightingModule` 每帧写 fogColor/fogDensity；URP 材质靠 `MixFog` 参与，**不影响天空盒**）＝当前真正在跑的。
2. 自研 `Feature/Fog`（FogFeature+FogVolme）**已删除**，`Shader/Feature/FogEffect.shader`、`Setting/Feature/Fog.mat` 为孤儿资产。
3. **Meryuhi `Packages/Fog` 的 `FullScreenFog`**（Volume + `FullScreenFogRendererFeature`）：
   - Renderer 资产 `Assets/Setting/New Universal Render Pipeline Asset_Renderer.asset` 中 `_injectionPoint: 450`（BeforeRenderingTransparents）、`_renderCamera: 3`
   - 注入点 450 = 不透明之后、**透明之前**；`550`(BeforeRenderingPostProcessing) 是**合法值**（曾误判为非法），但 550 会让云/雪/粒子按背后不透明物深度被误雾化，故项目固定用 450
   - `Depth`/`Distance` 模式会因"天空深度恒为远平面"把**整片天空刷成雾色**；`Height`/`HeightAndDistance` 模式下地平线以上天空天然不吃雾（项目强制用后者）
   - 三层开关：Renderer 特性 `m_Active` + `FullScreenFogController.Enabled`（默认 false，WeatherEffect 才开）+ Volume `intensity>0`
   - **透明队列的抓屏 shader（Warping/Stealth，Queue=Transparent+1）不吃 450 的雾**（雾在不透明后就合成，它随后整片覆盖）。解法：把它单独"提前到 445 手绘"——`Assets/Scripts/Rendering/WarpingBeforeFogRendererFeature.cs`（RF，445 = 400 生成 `_CameraOpaqueTexture` 之后、450 雾之前）+ `Warping.shader` 的 `LightMode = "WarpingEffect"`（必须改掉 UniversalForward，否则默认透明 pass 再画一遍照样盖掉雾；代价是 Warping 的显示依赖该 RF）
   - Overlay 相机已被 RF 内部过滤（`renderType != Base` 直接 return）
   - 教训（贴花被吞）：主相机堆叠中 UI 相机勾选 **Clear Depth** 会导致贴花消失；"Scene 正常 Game 异常"优先查 Scene 视图级开关与相机堆叠 Clear Depth

### 体积云（2026-09-11 重构）
- `Assets/Scripts/Rendering/DrawVolumetricCloud.cs` + `Assets/Shader/Environment/VolumetricCloud.shader`
- 架构 = **跟随相机的半球天穹（运行时自建 mesh）+ 每像素视线方向的"圆罩"有界投影采样**（`domePos = dir.xz/(dir.y + _DomeFlatten) * altitude`，`_DomeFlatten=0.35`；k→0 无限平面会在地平线出条纹），再按视线在层内路径长度做 Beer-Lambert 不透明度；真实视线距离 `rayLength = altitude/max(dir.y,_MinRayY)` 只用于雾/遮挡/掠射加厚
- 天穹顶点深度压到远平面（`w*1e-5`，靠 `UNITY_REVERSED_Z` 分支）→ 云在天空之上但被近处不透明物遮挡，**不需要 ZClip Off**
- 脚本内 `CloudLayer[]` 多层配置（高度/云量/尺度/密度/细节/风速/色调），每层用**独立 MaterialPropertyBlock** 绘制
- 旧"水平平板叠 N 层"方案已废弃（所有层世界 XZ 采样 = 同图叠 N 遍，到不了地平线且有硬边）

### 积雪
- `Assets/Scripts/Rendering/SnowRendererFeature.cs`：静态类 `SnowController`（`Shader.SetGlobalFloat` 控制 `_SnowEnabled`/`_GlobalSnowAmount`）+ `SnowRendererFeature`（AfterRenderingOpaques 用雪材质重画配置层）+ `SnowOverlay.shader` + `SnowVolume`
- **自定义 Shader 加雾模板**：`#pragma multi_compile_fog` + `Varyings.fogFactor` + `ComputeFogFactor(positionHCS.z)` + `MixFog()`
- **积雪局部遮罩**：`SnowController.RemoveSnow(worldPos, radius, softness)`/`AddSnow`/`ResetMask`/`FlushMask`；`_SnowMask` 纹理阵列 + `_SnowMaskRect=(origin.x, origin.z, 1/size.x, 1/size.x)` + `_SnowMaskTiles`（与 `TerrainUtils.WSToUV` 同换算）。R8 纹理阵列切片（`MaskTiles=2`×512，共 1024）；CPU `byte[][]` 累加，`FlushMask` 只重传脏切片；`DetectRawRowFlip()` 用 2x2 RGBA32 探针测 `SetPixelData` 行序（避免弹坑镜像）。`_SnowMaskTiles=0` 时按满雪处理。挂钩：`FpsHelper.Hit`（destructe>0）、`BattleManager.InitTerrain` 后 `ResetMask()`

## 其他功能记录
- **天气系统**：`01Manager/Battle/WeatherSystem.cs`（纯控制器，`BattleManager` 开局 `BattleRandom` 抽取后 `Create` 动态创建，`BattleManager.Weather` 供查询）+ `WeatherEffect` 抽象基类 + `WeatherEffectRain/Desert/Snow` **挂预制体**（配置在预制体 Inspector：_useStormCycle/_calmDuration/_stormDuration/_heightOffset）。`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")` 按 `Type` 匹配，Update 周期风暴、LateUpdate 跟随玩家；雪时 `SnowController.SetEnabled(true)`
- WaveManager `KaiserWave`（参考 ZergWave）：每 `360f/creats.Count` 秒创建 PhoenixEagle，挂 6 单位子对象（±2 网格）；HalfRange>=1 大型单位单置 `(0,-5,0)`；创建时禁用 `EnemyControllerFX.Animator`，鹰 waitTime=6s，onWait 后第 4 秒单位 Y=鹰Y-40 并启用 Animator
- `Furniture_NPCChat.cs`：NPC 语音家具，SoundGroup_SO + AudioSvc.PlaySound，协程 WaitForVoiceEnd
- `CampTemplate.patrolTemplate`：List<SKVP<string,int>>（队名+权重），Drawer 下拉框+权重 IntField
- `Assets/Shader/UIImageChannelMix.shader`：UI/Image 专用（R*Image色，G*白，B忽略，A用贴图Alpha，响应 Stencil）

## 修复记录要点（详见各日 .md）
ModifyTerrain 地形修改后贴地；WaveManager tier 权重 TryGetValue 降级；ObjectPool Release 误调 _Pop、UnInit 崩溃；TerrainMainUtils 分辨率缓存；Health 死亡僵尸单位（m_IsDead）；BaseSelfMoveableController 陡坡卡死投影；DeployableMine 高空单位误引爆（HalfHeight 3D 判定）；PhoenixEagleController 旋转乱跳（过渡帧 lastPos.y）；PlayerWeaponsManager OnWeaponSwitched 忽略 isSec 破坏 IK；PathRequestManager 假超时重试风暴（pathPending 期间不重试）；AudioManaqerBase sourcePool 初始 SetActive(false)
