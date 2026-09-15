# Bluedivers 项目长期记忆

> 详细复盘见各日 `YYYY-MM-DD.md`，本文件只留跨会话结论。最后整理：2026-09-15

## 环境
- Unity **2022.3.62f3**（正版，`D:\Unity Hub\Version\2022.3.62f3\Editor`，**非团结引擎**）；URP 14.0.12；C# 9 / .NET Std 2.1；TMP 3.0.9；Navigation 1.1.6；Timeline 1.7.7
- 单机 PvE demo（Unity FPS Sample 二次开发，命名空间 `Unity.FPS.*`）；KCPNet 未完成
- 确定性计算用 `PEMaths`；随机源应统一到 `BattleRandom`；脚本实际 UTF-8 **无 BOM**
- 本机 Python 3.12.10（pip 有 uv/uvx；清华源 SSL 失败，须 `-i https://pypi.org/simple`）

## 结构与命名
- `Assets/Scripts/` 数字前缀＝依赖顺序，上层可引用下层、反之禁止：`00Core→00GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering`
- asmdef 仅 14 个且都在子目录；`04UI/`、`02Data/`、`01Manager/` 根目录脚本属 **Assembly-CSharp**（`WndManager` 可直接引用 `VehicleWnd`/`AirdropConfigWnd`）
- 跨模块优先事件 `GlobalEventSub`/`BattleEventSub`；接口主流 `I_` 前缀；SO `_SO` 后缀；partial 同目录 `主类名_分部名.cs`；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 仅对**字段**有效
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`、skill `.codebuddy/skills/bluedivers-unity/`
- 无 `Debug.DrawWireSphere`，用 `Tool.DrawWireSphere(pos,size,color,time)`

## 架构约定
- 避免组件间直接 `GetComponent` 互取，跨模块用事件
- 默认操作视角：`ArchivesData_SO.settingDic["默认操作视角"]` → `PlayerController.ApplyViewMode()`
- AI：`AIController` 组合 + 接口代理，**不继承 `Actor`**（全项目无类继承 Actor）
- 竖直占位 `I_Entity.HalfHeight`（`CenterPos.y±HalfHeight`，**0=未配置→不过滤**）；批量填充 `Assets/Editor/ActorHalfHeightTool.cs`
- asmdef 不能被 Assembly-CSharp 反向引用：`00_Utils`/`08_Map`/`04_UI`/`Effect/EffectComp` 不能直接调 `SnowController`/`BattleManager` 等
- 程序集 **01Manager↔02Game↔02Data 三角循环**（02Data 引用 `FpsGame.Mission` 等）→ 须先断开 02Data→02Game
- 窗口注册：`WndManager` 挂 `public XxxWnd` 字段（GameRoot.prefab 填 0），窗口 `Init()` 由场景/prefab 的 UnityEvent 调用；家具入口 `Furniture_General.furnData` 按 Id 分发

## 数据分层（SO / 实例）
- SO **不能持有 prefab/场景实例引用** → 子物体/挂点引用留在 prefab 的 `[SerializeField]`，SO 只放与实例无关的（音效、粒子、材质、颜色、数值）
- 判定：「改这条时希望同类单位一起吃？」是→SO，否→实例；语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；仅需调用"任意组件任意方法"才用 UnityEvent（存方法名，改名静默失效）
- 共享 + 需引用实例层级 → 间接引用（key/相对路径 + 运行时解析 + 编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- UI 展示模型（`ArmamentWnd`/`SettingWnd`/`AirdropConfigWnd`）：实例化 prefab → 禁全部 MonoBehaviour + Collider、Rigidbody kinematic → `SetChildLayer`；独立相机 + RenderTexture 预览，`RectTransformUtility.RectangleContainsScreenPoint` 判拖动区

## 协作偏好
- 不确定时先询问；不主动纠结/移除 using；文件夹/文件改名等结构性修改由用户**手动**做，AI 只给方案

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath/serializedObject 才用 `PropertyDrawer`（数组场景 `.Array.data[` 回退普通绘制）
- `EditorOverride`（`Assets/Editor/Drawer/`）回退 Inspector：`[Foldout]`/`[InspectorName]`/`[Compare]`/单行内联数组
- 反射自建 Drawer 必须手动注入 `m_Attribute` 私有字段（注入实际特性实例）
- 特性类名是 `UnityEditor.CustomPropertyDrawer`；取目标类型用 `GetCustomAttributesData().ConstructorArguments[0].Value as Type`
- 复用：`SOPickerPopup<T>`（`confirmMode` 控单击即选/确认）、`PrefabBatchToolBase`；Drawer 集中 `Drawer/CustomLabelDrawer.cs`
- `[DisplayField]`（`00Attribute/CustomAttribute.cs`）：默认**编辑期不画（高度 0）、运行期只读显示**；是 PropertyDrawer，只对已序列化字段生效 → 私有字段须再配 `[SerializeField]`；`readonly`/Dictionary 无效
- 数据编辑器：`Editor/DataEditorWindow.cs` + `DataTabs/DataTabModule<T>`；SO 加字段且有专属 Editor（如 `AirdropData_SOEditor`）需显式补 `DrawField("新字段")`
- 工具坑：`search_content` 的 glob **不支持 `*.{a,b}`**，须分开写

## 项目全景
- 已实现：伤害模型（弱点/护甲/护盾/抗性）、属性系统、武器体系、投射物、玩家（视角/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次、`UnitQueryGrid`、噪声地形、任务、昼夜循环+夜间敌袭、战备全类别、战略大地图、撤离、购买与偏好
- 未实现：联机
- 架构改进优先级：① 随机源统一 `BattleRandom` ② `I_Damagable`/`I_Entity` 解耦为纯 DTO ③ 统一命名空间 + rootNamespace ④ God Class 审查 ⑤ `UnitQueryGrid` List 池化

## 战备系统
- 资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；运行时 `ResSvc.airdropDic`
- `AirdropData_SO`：`opter` 方向序列（Left0/Up1/Right2/Down3，序列化是 LE uint 数组）、`subAirdrop` 附属、`creatObect` 部署 prefab（兼配置界面展示模型）、`coolGroup` 冷却组、`isHide`
- `ArchivesData_SO`：`AirdropBuyDic`（已购）、`AirdropPreferList`（偏好，用于排序与标记）
- UI：`AirdropWnd`（战斗内 HUD）、`AirdropConfigWnd`（购买/偏好）、`ArmamentWnd`（战备配置）；`ArmamentButton.prefab` 子1=偏好标记
- 强化：`VFXAirdropEffect.SetOwner` 武器参数须从 **`weaponRoot`** 取（信标 prefab 无武器组件）；只改运行时 `AirdropData`（以 cfg 为基准）；改 `arriveTime` 须同步 `time`

- 
## 敌人特效（`02Game/AI/FxCont`）
- `EnemyControllerFX`（抽象 partial；`EnemyFXControllerUnit`/`BuildingFXController`）；`Start` 订阅 `I_AIController` 事件，`OnDestroy` 退订；`Update`→`UpdateRS()`
- 三层配置：渲染模板 SO `EnemyFxData_SO.rendererSet`（按 `sharedMaterials[i]==mat` 匹配槽位）／事件 SO `EnemyFxEventData_SO.fxDic`（`OccasionTypeEnum`→`FxSetConfig`）／组件 `fxMaterial`/`BirthMaterial`/`Animator`
- `TriggerFX(type,pos,rot,parent,ignoreAudio)` 统一入口；资产 `Resources/GameData/EnemyFx/`；SO 不存实例引用 → `EVT_*.go` 全空
- `_HitColor` 是**自发光叠加**（默认黑＝无闪光）
- **MPB 所有权在"渲染槽位"**（`RendererSlot{Renderer,MaterialIndex,mpb,dirty}`）：条目只 `SetColor`，帧末 `UpdateRS()` 统一 `Flush()`；同槽位只能一块；收尾必须写回"无效果值"，否则残留亮斑

## 渲染
### URP / ToonLit
- 附加光阴影需同时成立：变体带 `_ADDITIONAL_LIGHT_SHADOWS` + 3 参 `GetAdditionalLight(i,posWS,shadowMask)`；`LIGHT_LOOP_BEGIN` 调用处须先声明 `InputData inputData`
- URP Asset：附加光 Realtime、`m_AdditionalLightShadowsSupported:1`、`m_ShadowDistance:100`、级联 3；URP14 `USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA` 硬编码 0
- ToonLit `_ShadowMapColor`＝**暗面里该光源的乘数**（非影色叠加）；`_DirectLightMultiplier` 等未写 CBUFFER＝死属性；**新增 CBUFFER 字段须同步写进每个 .shader 的 Properties**
- `_MAIN_LIGHT_SHADOWS` 管**接收**；URP 投影剔除走 `DrawShadows`，与 Render Queue 无关
- **材质不投影头号原因**：`disabledShaderPasses: - SHADOWCASTER`（跟材质走，换 shader 不清空）；全仓无代码调 `SetShaderPassEnabled`
- `ToonLitShaderGUI.cs` 整个 class 被注释，主 shader 实际用默认 Inspector
- URP14 pass 时序：Opaques 300 / BeforeSkybox 350 / AfterSkybox 400 / BeforeTransparents 450 / AfterTransparents 500 / BeforePostProcessing 550
- UI 相机勾 Clear Depth 会致贴花消失

### 积雪（`Rendering/SnowRendererFeature.cs`）
- `SnowController`（全局 `_SnowEnabled`/`_GlobalSnowAmount`）+ RF(300) + `SnowOverlay.shader` + `SnowVolume`
- 遮罩 `RemoveSnow/AddSnow/ResetMask/FlushMask`；`_SnowMask` 纹理阵列 + `_SnowMaskRect/_SnowMaskTiles`；`_SnowMaskTiles=0` 视为满雪；挂钩 `FpsHelper.Hit`、`BattleManager.InitTerrain`

### 自定义 Shader 加雾
- 物体自身几何 → 顶点雾：`ComputeFogFactor(positionHCS.z)` + `MixFog()`
- 屏幕空间/投影式（贴花、抓屏、全屏 quad）→ 必须 `ComputeFogFactorZ0ToFar(max(sceneDepthVS-_ProjectionParams.y,0))`；贴花保持 `_Cull:1`

## 其他功能
- 天气：`WeatherSystem`（BattleManager 开局 `BattleRandom` 抽取后 Create）+ `WeatherEffect` 抽象基类（Rain/Desert/Snow）；`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")` 按 `Type` 匹配；给预制体加字段默认值须中性
- `WaveManager.KaiserWave`：每 `360f/creats.Count` 秒建 PhoenixEagle + 6 单位（±2 网格）；创建时禁用 `EnemyControllerFX.Animator`
- `CampTemplate.patrolTemplate`：`List<SKVP<string,int>>`（队名+权重）


## MCP for Unity 插件（本地嵌入包）
- 路径 `Packages/com.coplaydev.unity-mcp/`（10.2.1-beta.6，**untracked**，改源码立即生效）；入口 `Window → MCP for Unity`、Setup 向导、`Tools → EditorPrefs`
- 默认传输为 **HTTP**（`UseHttpTransport` 默认 true）→ HTTP 模式下 stdio 桥不自启，`AutoStartOnLoad` 默认 false 且只对 HTTP 生效。连不上 Unity 时先切 Stdio，或点 Start / 勾 Auto-Start
- **Editor 窗口已全面汉化**（2026-09-15）：11 个 UXML + 主/Setup/EditorPrefs 窗口 + 6 个 Section 控制器 + HealthStatus 常量 + 23 个客户端配置器的安装步骤 + 依赖检测（Windows）。未汉化：工具/资源 Description、McpLog 日志、URL/Prefs key、mac/linux 检测器文本
- ⚠ 重新下载/更新该包会**覆盖全部汉化**（未纳入 git）→ 更新前备份 `Editor/` 目录
- 客户端配置 `C:\Users\admin\.codebuddy\mcp.json`（stdio，走 `uvx --from mcpforunityserver mcp-for-unity`）；stdio 桥 `127.0.0.1:6400`，发现文件 `%USERPROFILE%\.unity-mcp\unity-mcp-status-<hash>.json`，实例 `Bluedivers@3d9f2357`
- 操作手册见 skill **`.codebuddy/skills/unity-mcp/`**（三层自检、工具只读/会写分类、本项目避坑、升级/卸载）；用前先 git commit

## 通用坑与教训
- 池化对象"本次状态"字段必须在 `OnDisable`/`OnEnable` 复位，且放在早退 `return` **之前**
