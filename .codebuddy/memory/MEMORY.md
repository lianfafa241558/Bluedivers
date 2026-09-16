# Bluedivers 项目长期记忆

> 详细复盘见各日 `YYYY-MM-DD.md`，本文件只留跨会话结论。最后整理：2026-09-16

## 环境与工具链
- 双机：`D:\Pro\Bluedivers`（本机 Administrator）/ `E:\Bluedivers`（用户 admin）。绝对路径、MCP 实例 hash、uvx 路径不可跨机照抄
- Unity 2022.3.62f3（`D:\UnityHub\Editor\`）；URP 14.0.12；C# 9 / .NET Std 2.1；TMP 3.0.9；Navigation 1.1.6；脚本实际 UTF-8 无 BOM；单机 PvE demo（Unity FPS Sample 二次开发，命名空间 `Unity.FPS.*`）
- 随机源统一 `BattleRandom`；确定性计算用 `PEMaths`
- Python 3.12.10：pypi 需 `-i https://pypi.tuna.tsinghua.edu.cn/simple --trusted-host pypi.tuna.tsinghua.edu.cn`；`uvx` 在其 Scripts 下
- git `core.autocrlf=true`（工作区 CRLF / 仓库 LF）；GitHub 直连被重置 → 抓包用 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore`：Unity 生成目录规则必须 `/` 锚根（未锚定的 `[Bb]uild/` 曾吞掉 `Packages/**/Tools/Build/`、`Assets/Art/{Anim,Modle/Enemy}/Build/`）
- 工具坑：`search_content` glob 不支持 `*.{a,b}`，须分开写；PowerShell 管道里 `Get-Content` 不带 `-Encoding` 会被拦，改用检索/阅读工具

## 结构与命名
- `Assets/Scripts/` 数字前缀＝依赖顺序（`00Core→00GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering`）；上层可引下层，反之禁止
- asmdef 仅 14 个且都在子目录；`04UI/`、`02Data/`、`01Manager/` 根脚本属 **Assembly-CSharp**（故 `WndManager` 可直接引用 `VehicleWnd`/`AirdropConfigWnd`）
- asmdef 不可被 Assembly-CSharp 反向引用：`00_Utils`/`08_Map`/`04_UI`/`Effect/EffectComp` 不能直接调 `SnowController`/`BattleManager`
- 跨模块优先事件 `GlobalEventSub`/`BattleEventSub`；接口主流 `I_` 前缀；SO 用 `_SO` 后缀；partial 用 `主类名_分部名.cs` 放同目录；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 仅对字段有效
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`
- 无 `Debug.DrawWireSphere` → `Tool.DrawWireSphere(pos,size,color,time)`

## 架构与数据分层
- 组件间避免直接 `GetComponent` 互取；AI 用 `AIController` 组合 + 接口代理，**不继承 `Actor`**
- 默认操作视角：`ArchivesData_SO.settingDic["默认操作视角"]` → `PlayerController.ApplyViewMode()`
- 竖直占位 `I_Entity.HalfHeight`（`CenterPos.y±HalfHeight`，0=未配置→不过滤）；批量填充 `Assets/Editor/ActorHalfHeightTool.cs`
- 程序集存在 **01Manager↔02Game↔02Data 三角循环**（02Data 引 `FpsGame.Mission` 等）→ 拆解须先断开 02Data→02Game
- 窗口注册：`WndManager` 挂 `public XxxWnd` 字段（GameRoot.prefab 填 0），窗口 `Init()` 由场景/prefab 的 UnityEvent 调；家具入口 `Furniture_General.furnData` 按 Id 分发
- SO **不能持有 prefab/场景实例引用**：子物体、挂点留在 prefab 的 `[SerializeField]`；SO 只放与实例无关项（音效、粒子、材质、颜色、数值）。判定「改这条时希望同类单位一起吃？」是→SO；语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；仅需"任意组件任意方法"才用 UnityEvent（存方法名，改名静默失效）
- 共享 + 需引用实例层级 → 间接引用（key/相对路径 + 运行时解析 + 编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- UI 展示模型（`ArmamentWnd`/`SettingWnd`/`AirdropConfigWnd`）：实例化 prefab → 禁 MonoBehaviour + Collider、Rigidbody 设 kinematic → `SetChildLayer`；独立相机 + RenderTexture 预览，`RectTransformUtility.RectangleContainsScreenPoint` 判拖动区
- ⚠ 展示模型要**连 Awake 都不执行**时：`enabled=false` 无效（Awake 只看 `activeInHierarchy`），必须在**未激活的挂点**下实例化 → 移除逻辑组件 → 再激活（`Destroy` 帧末生效，需等一帧；想同帧则用 `DestroyImmediate`）。`AirdropConfigWnd.ShowModel` 是现成实现
- 展示模型自适应大小：`AirdropConfigWnd.FitModelScale` 用「相机视野半高半宽（到挂点距离处）+ 包围盒 XZ 半对角线（绕 Y 旋转安全）/ Y 半高」求缩放（**双向**，小体型也放大），并把包围盒中心对齐**相机视线中心**（`focus = camPos + fwd*dot(root-camPos, fwd)`，挂点不在视线中心上，本预制体差 0.5）；朝向走 `_modelBaseEuler`（默认 180）+ `ApplyModelRotation()`
- ⚠ 量展示模型包围盒的四个前提：① `Renderer.bounds` **未激活时为 0**，必须激活后量 ② **带 Animator 的模型必须先让动画算姿势**（激活后 `animator.Update(0f)` 再等一帧），绑定姿势与显示姿势能差 1.7 米（Healdrone 默认动画下移机身），否则模型会整体偏上/偏下 ③ 蒙皮网格 bounds 随骨骼姿势变化 ④ **量的时候要把模型自身旋转临时归零**，否则世界 AABB 被旋转撑大（炮台自带 45° 会让水平尺寸虚高四成）
- 展示模型保留预制体自身的旋转/缩放（`_modelBaseScale`），默认朝向只加在挂点上 → 世界旋转 = 挂点(默认+拖动) ∘ 预制体自身旋转（8 个炮台预制体根节点自带 45°，180+45=225）
- ⚠ 量展示模型的包围盒**不能直接 Encapsulate 全部 Renderer**：战备预制体里混着 ① 零体积包围盒的未播放粒子（会把原点/远处那个点并进盒子，模型被缩成 0.002）② 几百米的 LineRenderer 光束 ③ `BombRange`/`EvaRange`/`Range_Sphere`/`TaskShowRange`/`DistGround*`/`Decal*` 等范围贴花示意网格。`FitModelScale` 的过滤顺序：画不出来的（`!enabled || !activeInHierarchy`）→ 特效类（Particle/Line/Trail，仅在还有实体网格时）→ 材质名关键字（`RangeMaterialKeys`）→ 零体积；全模型只剩特效时才退回按全部 Renderer 量

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath/serializedObject 才用 `PropertyDrawer`（数组场景 `.Array.data[` 回退普通绘制）
- `InlineFieldDrawer`（`Assets/Editor/Drawer/`）＝`[Singleline]` 单行内联的**唯一实现**（Layout 版 `DrawInlineListLayout/DrawInlineObjectLayout/DrawInlineList/DrawDecorators`；Rect 版 `DrawInlineListRect/GetInlineListHeight/DrawElementRow/DrawFooterButtons/DrawAddButton`，样式 `InlineListStyle` 可插 `ChildLabelProvider`/`ChildDrawer`）；含 `ResolvePropertyType/ResolveField/GetFieldLabel` 反射工具。要单行内联就调它，不要手写
- `EditorOverride`（`[CustomEditor(typeof(Object),true,isFallback=true)]`）是全局兜底 Inspector：`[Foldout]`/`[InspectorName]`/`[Compare]`/单行内联数组全靠它；**被专属 `[CustomEditor]` 完全顶掉**（全仓仅 `SoundGroup_SOEditor`、`AirdropData_SOEditor` 两个非 fallback），目标类型有专属编辑器时须自己补 `[Header]`/`[Space]`/内联
- 反射自建 Drawer 须手动注入 `m_Attribute`；特性类标 `UnityEditor.CustomPropertyDrawer`；取特性目标类型用 `GetCustomAttributesData().ConstructorArguments[0].Value as Type`
- 复用：`SOPickerPopup<T>`（`confirmMode` 控单击即选/确认）、`PrefabBatchToolBase`、`DamageDataDrawer.DrawSinglelineList`；Drawer 集中 `Drawer/`
- `[DisplayField]`（`00Attribute/CustomAttribute.cs` + `CustomLabelDrawer.DisplayFieldDrawer`）：默认编辑期不画、运行期只读；只对已序列化字段生效（私有须配 `[SerializeField]`）；`readonly`/Dictionary 无效；支持类型白名单 `IsSupportedType`＝Integer/Float/Boolean/String/ObjectReference/**Color/Vector2/Vector3**，不在白名单的类型不画也不占位（扩展加一个 `case`）；高度与绘制共用 `ShouldDraw`（`isPlaying ? run : editor`）
- 数据编辑器：`Editor/DataEditorWindow.cs` + `DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须显式补 `DrawField("新字段")`

## 协作偏好
- 不确定时先询问；不主动纠结/移除 using；文件夹/文件改名等结构性修改由用户**手动**做，AI 只给方案

## 项目全景
- 已实现：伤害模型（弱点/护甲/护盾/抗性）、属性、武器、投射物、玩家（视角/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次、`UnitQueryGrid`、噪声地形、任务、昼夜+夜间敌袭、战备全类别、战略大地图、撤离、购买与偏好
- 改进优先级：① 随机源统一 `BattleRandom` ② `I_Damagable`/`I_Entity` 解耦为纯 DTO ③ 统一命名空间 + rootNamespace ④ God Class 审查 ⑤ `UnitQueryGrid` List 池化

## 战备系统
- 资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；运行时 `ResSvc.airdropDic`
- `AirdropData_SO`：`type`（Red0轰炸/Blue1装备/Greed2炮台/Orange3载具/Yellow4补给）、`labels`（`AirdropLabelEnum` `[Flags]`：Bag1/Drone2/Mine4/Jet8/Medivac16）、`opter` 方向序列（Left0/Up1/Right2/Down3，LE uint 数组）、`subAirdrop`、`creatObect`（部署 prefab，兼配置界面展示模型）、`coolGroup`、`isHide`
- ⚠ `labels` 是后加字段：除 `ADSO_R_Railgun`（`labels: 0`）外其余资产 YAML 里**根本没有该行**（=0），需逐个填
- ⚠ `AirdropData_SOEditor` 为显式列字段，SO 新增字段漏列就不显示；`DrawField` 传显式 GUIContent 会盖掉字段自带 `[InspectorName]`，`LabelOf` 的中文名 switch 必须同步补
- `ArchivesData_SO`：`AirdropBuyDic`（已购）、`AirdropPreferList`（偏好/排序标记）
- UI：`AirdropWnd`（HUD）、`AirdropConfigWnd`（购买/偏好；列表分组由静态 `GroupRules` 数组驱动：轰炸→轨道打击/凤鹰空袭、装备→战术背包/无人机/支援武器、炮台→地雷发射器/哨戒炮、载具整组、补给不显示；首个命中即归类，组内偏好优先排序；分组表头第二文本＝已拥有/总数）、`ArmamentWnd`（配置）；`ArmamentButton.prefab` 子1=偏好标记
- 强化：`VFXAirdropEffect.SetOwner` 武器参数须从 **`weaponRoot`** 取；只改运行时 `AirdropData`（以 cfg 为基准）；改 `arriveTime` 须同步 `time`

## 敌人特效（`02Game/AI/FxCont`）
- `EnemyControllerFX`（抽象 partial；`EnemyFXControllerUnit`/`BuildingFXController`）；`Start` 订阅 `I_AIController` 事件，`OnDestroy` 退订；`Update`→`UpdateRS()`
- 三层配置：渲染模板 SO `EnemyFxData_SO.rendererSet`（按 `sharedMaterials[i]==mat` 匹配槽位）／事件 SO `EnemyFxEventData_SO.fxDic`（`OccasionTypeEnum`→`FxSetConfig`）／组件 `fxMaterial`/`BirthMaterial`/`Animator`
- `TriggerFX(type,pos,rot,parent,ignoreAudio)` 是统一入口；资产 `Resources/GameData/EnemyFx/`；SO 不存实例引用 → `EVT_*.go` 全空；`_HitColor` 为自发光叠加（默认黑＝无闪光）
- **MPB 所有权在"渲染槽位"**（`RendererSlot{Renderer,MaterialIndex,mpb,dirty}`）：条目只 `SetColor`，帧末 `UpdateRS()` 统一 `Flush()`；同槽位只能一块；收尾必须写回"无效果值"

## 渲染
### URP / ToonLit / 阴影
- 附加光阴影需同时成立：变体带 `_ADDITIONAL_LIGHT_SHADOWS` + 3 参 `GetAdditionalLight(i,posWS,shadowMask)`；`LIGHT_LOOP_BEGIN` 调用处须先声明 `InputData inputData`
- URP Asset：附加光 Realtime、`m_AdditionalLightShadowsSupported:1`、`m_ShadowDistance:100`、级联 3；URP14 `USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA` 硬编码 0
- ToonLit `_ShadowMapColor`＝**暗面里该光源的乘数**；未写 CBUFFER 的属性＝死属性；新增 CBUFFER 字段须同步写进每个 .shader 的 Properties
- `_MAIN_LIGHT_SHADOWS` 管**接收**；URP 投影剔除走 `DrawShadows`，与 Render Queue 无关
- **材质不投影头号原因**：`disabledShaderPasses: - SHADOWCASTER`；`ToonLitShaderGUI` 整个 class 被注释
- URP14 pass 时序：Opaques 300 / BeforeSkybox 350 / AfterSkybox 400 / BeforeTransparents 450 / AfterTransparents 500 / BeforePostProcessing 550；UI 相机勾 Clear Depth 会致贴花消失

### 自定义 UI Shader（`Assets/Shader/UIImageChannelMix.shader` 等）
- `Mask` 与 `RectMask2D` 是两套机制：前者走模板缓冲（要 `_Stencil*` + Stencil 块）；后者**不用模板**，靠 `MaskableGraphic.SetClipRect` → `CanvasRenderer.EnableRectClipping(rect)` 注入 `_ClipRect` 并打开 `UNITY_UI_CLIP_RECT`，由 shader 自己软裁剪
- 自定义 UI shader 必须自带：`#pragma multi_compile_local _ UNITY_UI_CLIP_RECT`（非 global）、`float4 _ClipRect`（**不写进 Properties**）、顶点局部坐标传片元、`UnityGet2DClipping`；`_ClipRect` 空间＝顶点同一对象/Canvas 局部空间
- 常见失效点：Graphic 上 `Maskable` 未勾选；图标挂在**子 Canvas** 下（RectMask2D 只作用于同一 Canvas 后代）

### 积雪与雾
- `SnowController`（`_SnowEnabled`/`_GlobalSnowAmount`）+ RF(300) + `SnowOverlay.shader` + `SnowVolume`；`RemoveSnow/AddSnow/ResetMask/FlushMask`；`_SnowMaskTiles=0` 视为满雪；挂钩 `FpsHelper.Hit`、`BattleManager.InitTerrain`
- 物体自身几何 → 顶点雾 `ComputeFogFactor(positionHCS.z)` + `MixFog()`；屏幕空间/投影式（贴花、抓屏、全屏 quad）→ `ComputeFogFactorZ0ToFar(max(sceneDepthVS-_ProjectionParams.y,0))`，贴花保持 `_Cull:1`

## 其他功能
- 天气：`WeatherSystem`（BattleManager 开局 `BattleRandom` 抽取后 Create）+ `WeatherEffect` 抽象基类（Rain/Desert/Snow）；`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")` 按 `Type` 匹配；预制体默认值须中性
- `WaveManager.KaiserWave`：每 `360f/creats.Count` 秒建 PhoenixEagle + 6 单位（±2 网格）；创建时禁用 `EnemyControllerFX.Animator`
- `CampTemplate.patrolTemplate`：`List<SKVP<string,int>>`（队名+权重）
- 寻路：`PathRequestManager` 禁止在 `pathPending` 期间"超时重试"，只在 `pathPending=false` 后判 `PathInvalid/PathPartial` 并投影重试；`RequestPath` 的 `log` 由 `EnemyController.SetNavDestination(isImportant)` 控制

## MCP for Unity
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（10.2.1-beta.6，已入库，**含汉化**）；**操作手册＝skill `.codebuddy/skills/unity-mcp/`**；**动写操作前先 git commit**
- Unity 侧 Transport 必须设 **Stdio**；桥监听 `127.0.0.1:6400`；发现文件 `%USERPROFILE%\.unity-mcp\unity-mcp-status-<hash>.json`；实例 hash 由项目绝对路径算出（本机 D 盘＝`4a3e6a7b`）
- 客户端配置两份、互不相通：IDE 读 `%USERPROFILE%\.codebuddy\mcp.json`；Unity 窗口的 Configure 只写 CLI 的 `%USERPROFILE%\.codebuddy.json`
- ⚠ `Editor/Tools/Build/*.cs` 曾因 `.gitignore` 误忽略被清理 → Editor 程序集编译失败（MCP 全挂）；升级该包前必须先把汉化 `git add -f`
- 改完脚本要验证：`validate_script` → `refresh_unity`（`mode=if_dirty, compile=request`）→ 桥短暂失联属域重载，等 ~10s 再 `read_console`
- 运行态核对类型/反射逻辑可用 `execute_code` 跑只读片段，比开窗口肉眼快

## UI 图片/配色词汇表（`Assets/Images/`，参照 `SelectRoleWnd.prefab`）
- 按钮底 `FX_TEX_Lock.png`（Sliced，`ppu=2`，亮青 `(0.65,0.87,0.87)`）；锁定框 `FX_TEX_Lock_Frame.png`
- 列表条目/中面板 `frame_panel13.png`（Sliced）：未选中 `(0.84,1,1,0.40)`(RoleButton) / `(0.88,0.96,1,0.40)`(WeaponPreviewItem)；选中 `(1,0.97,0.84,0.40)`(SelectBtn)
- 大面板框 `SelectFrame3/4.png`（Sliced，`ppu=0.5`，`(0.67,0.96,1,0.70)`）；气泡 `SelectFrame2.png`；小图标框 `UI_Frame4.png`（Simple，alpha 0.5）；图标底 `Common_Main_SkillBG.png`；六边形 `Hexagonal_Frame(2).png`
- 进度/连接条 `LinkBar.png`（Filled）；细边框 `frame5/6.png`；箭头 `Arrow_Left/Right/Left2/Right2/Down.png`；全屏底 `99997.png`；纯色深底惯例：无 sprite + `(0,0,0,0.2~0.55)`

## 通用坑与教训
- 池化对象"本次状态"字段必须在 `OnDisable`/`OnEnable` 复位，且放在早退 `return` **之前**
- 上游包文件丢失排查法：`git ls-files` 与镜像目录清单比对；Unity 报"meta 存在但文件夹不存在"常意味着目录内文件被忽略/删除（空目录 git 不存）
- 双机/换机后第一件事：确认 MCP 连的是**当前这份副本**（`project_path`），再动写操作
- 编辑器特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 会顶掉全局 fallback
- Unity 资产 YAML 中**整行缺失的字段**＝脚本新增后资产尚未重新保存（按默认值处理），排查数据时须留意
