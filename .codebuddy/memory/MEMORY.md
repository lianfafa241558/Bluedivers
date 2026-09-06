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
- **AI 控制器（2026-08-20 确认）**：`AIController`（02Game/AI/Controller）**不继承** `Actor`，组合+接口代理（`m_Actor = GetComponent<Actor>()`）。全项目无类继承 `Actor`
- **单位竖直占位（2026-08-29）**：`I_Entity.HalfHeight`，占位区间 = `[CenterPos.y±HalfHeight]`；**0=未配置退化不过滤**。编辑器工具 `Assets/Editor/ActorHalfHeightTool.cs` 批量填充。区分空中/地面的判定必须叠加竖直检测

## 协作偏好
- 不确定时先暂停询问，不自行大量搜索推断
- 不主动纠结/移除 using 语句，用户自行管理
- 文件夹/文件改名等结构性修改由用户**手动**完成，AI 只给方案

## 编辑器特性约定（2026-09-07 确立）
- **纯装饰特性（分割线/标题等）一律 `DecoratorDrawer`**：数组头部/嵌套类/任意 Inspector 自动生效，不作用于数组元素
- 需读 propertyPath/serializedObject 的特性才用 `PropertyDrawer`；数组场景对 `.Array.data[` 路径回退 `EditorGUI.PropertyField` 普通绘制
- **EditorOverride 回退 Inspector**（`Assets/Editor/Drawer/EditorOverride.cs`）：唯一需补画 Decorator 的位置是内联数组自绘头部（`DrawInlineArrayNative`），由 `DrawDecorators` + `DecoratorDrawerCache` 处理，新 Decorator 特性零修改生效
- **反射自建 Drawer 必须手动注入 `m_Attribute` 私有字段**（Unity 只在自己的创建流程设置），且注入每次绘制的实际特性实例，否则 attribute NRE 连锁打断 GUILayout
- Unity API 坑：特性类名是 `UnityEditor.CustomPropertyDrawer`（无 `Attribute` 后缀）；取 Drawer 目标特性类型用 `type.GetCustomAttributesData()` 找 `CustomPropertyDrawer` 条目读 `ConstructorArguments[0].Value as Type`

## 可复用编辑器基础设施
- **SO 选择弹窗**：`Assets/Editor/SOPickerPopup.cs` 的 `SOPickerPopup<T>`（PopupWindowContent，全局命名空间），优先复用勿重复造轮子
- **预制体组件批量工具基类**：`Assets/Editor/PrefabBatchToolBase.cs` 的 `PrefabBatchToolBase<TComponent>`（namespace `Unity.FPS.EditorExt`），做批量检查/写字段窗口时继承它

## 项目全景（2026-08 调研）
- **已实现**：伤害模型（弱点/护甲/护盾/抗性）、属性系统、武器体系（30+ 脚本）、投射物、玩家（视角切换/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次（WaveManager Zerg/Robot）、UnitQueryGrid 空间网格、噪声地形（fBm+NavMesh）、任务系统、昼夜循环+夜间敌袭、战备全类别、战略大地图、撤离
- **未实现**：机甲/炮塔等重型战备、联机、矿洞体素
- **架构改进优先级**：① 统一确定性随机源（BattleRandom 与 WaveManager 的 System.Random 收口）② I_Damagable/I_Entity 解耦为纯 DTO ③ 统一命名空间+asmdef rootNamespace ④ God Class 审查（BattleManager/PlayerController）⑤ UnitQueryGrid List 池化
- **程序集拆解（2026-08-09 诊断）**：仅 9 个 asmdef，01Manager/02Game/02Data 大部及 04UI 落入 Assembly-CSharp；01Manager↔02Game↔02Data 三角循环。拆解须先打破 02Data 对 02Game 的 SO 配置引用。Assembly-CSharp 内 UnityEditor 引用均在 #if 守卫内，打包无风险

## 修复记录要点（详见各日 .md）
- ModifyTerrain 地形修改后贴地；WaveManager tier 权重 TryGetValue 降级；ObjectPool Release 误调 _Pop、UnInit 崩溃；TerrainMainUtils 分辨率缓存；Health 死亡僵尸单位（m_IsDead）；BaseSelfMoveableController 陡坡卡死投影；DeployableMine 高空单位误引爆（HalfHeight 3D 判定）；PhoenixEagleController 旋转乱跳（过渡帧 lastPos.y）；PlayerWeaponsManager OnWeaponSwitched 忽略 isSec 破坏 IK；PathRequestManager 假超时重试风暴（pathPending 期间不超时重试，EnemyController.SetNavDestination isImportant 控日志）；AudioManaqerBase sourcePool 初始 SetActive(false)
- DividerAttribute 数组不生效（2026-09-07）→ DecoratorDrawer 方案，见「编辑器特性约定」

## 其他功能记录
- `Furniture_NPCChat.cs`：NPC 语音家具交互，SoundGroup_SO + AudioSvc.PlaySound，协程 WaitForVoiceEnd 保证播完再播
- `CampTemplate.patrolTemplate`：List<SKVP<string,int>>（队名+权重），Drawer 下拉框+权重 IntField
- **天气系统**：`01Manager/Battle/WeatherSystem.cs`（基类，晴天直接用）+ 子类 `WeatherRain/WeatherDesert/WeatherSnow`（路径配置在各子类 `EffectPath`）。`WeatherSystem.Create(type, parent)` 工厂按天气建子类，`BattleManager.RandomWeather()` 用 BattleRandom 开局抽取（`BattleManager.Weather` 供查询）。基类 Update 周期风暴：平静 45s→风暴 20s 循环（沙漠=沙尘暴开关物体；下雪=暴雪物体+SnowController.SetGlobalAmount 1↔0.3）；雨常显无周期；雪时 SetEnabled(true) 其余 false
- WaveManager 加 `KaiserWave`（参考 ZergWave）：每 360f/creats.Count 秒在 center 创建 PhoenixEagle，从栈取 6 单位挂子对象（相对坐标 ±2 网格）；HalfRange>=1 的大型单位单置 (0,-5,0)；创建时禁用 EnemyControllerFX.Animator，鹰 waitTime=6s，onWait 后第 4 秒单位 Y=鹰Y-40 并启用 Animator
- `Assets/Shader/UIImageChannelMix.shader`：UI/Image 专用（R*Image色，G*白，B忽略，A用贴图Alpha，响应 Stencil）
- **积雪渲染（2026-09）**：`Assets/Scripts/Rendering/SnowRendererFeature.cs` — 静态类 `SnowController`（Shader.SetGlobalFloat 控制 `_SnowEnabled`/`_GlobalSnowAmount`）+ `SnowRendererFeature`（URP RendererFeature，AfterRenderingOpaques 用雪材质重画配置层）+ SnowOverlay.shader + SnowVolume（Volume 控制）

## 程序集与文档
- `.codebuddy/rules/UnityCSharp编码规范.md`：C# 编码规范，自动加载
- `.codebuddy/skills/bluedivers-unity/`：项目专用 skill（SKILL.md + references/module-guide.md），处理项目 C# 代码时自动触发
