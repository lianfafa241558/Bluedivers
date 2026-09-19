# Bluedivers 项目长期记忆

> 细节见各日 `YYYY-MM-DD.md`。最后整理：2026-09-20（压缩去重）

## 环境与工具链
- 双机副本：`D:\Pro\Bluedivers`（主用）/`E:\Bluedivers`；另装 `D:\Project\RTSClient`。路径、MCP 实例 hash、uvx 路径**不可跨机照抄**
- Unity 2022.3.62f3；URP 14.0.12；C# 9 / .NET Std 2.1；TMP 3.0.9；Navigation 1.1.6；单机 PvE（FPS Sample 二次开发，`Unity.FPS.*`）
- 随机源统一 `BattleRandom`；确定性计算用 `PEMaths`；Python 3.12.10（pip 清华源，`uvx` 在其 Scripts 下）
- git `core.autocrlf=true`；GitHub 直连被重置 → 抓包走 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore` 里 Unity 生成目录规则必须 `/` 锚根（未锚定的 `[Bb]uild/` 曾吞掉 `Packages/**/Tools/Build/`、`Assets/Art/{Anim,Modle/Enemy}/Build/`）
- 工具坑：`search_content` 的 glob 不支持 `*.{a,b}`；`findstr` 读某些 fbx 报 Cannot open → 改 `select-string`；无 `Debug.DrawWireSphere` → 用 `Tool.DrawWireSphere(pos,size,color,time)`
- 全仓规模参考：prefab 841 / mat 677 / fbx 276 / .asset 374 / .unity 40 / controller 154 / png 1387 / shader 59

## 结构与命名
- `Assets/Scripts/` 数字前缀＝依赖顺序（00Core→00GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering）；上层可引下层，反之禁止
- asmdef 仅 14 个且在子目录；`01Manager/`、`02Data/`、`02Game/`、`04UI/` 根脚本属 **Assembly-CSharp**（故 `WndManager`/`BattleManager`/`MissionBase` 可直接互相引用）；asmdef 不能被 Assembly-CSharp 反向引用（`00_Utils`/`08_Map`/`04_UI`/`EffectComp` 调不到 `SnowController`/`BattleManager`）
- 存在 **01Manager↔02Game↔02Data 三角循环**（02Data 引 `FpsGame.Mission`／`Unity.FPS.Game`）→ 拆解须先断开 02Data→02Game，再逐层补 asmdef
- 跨模块优先事件 `GlobalEventSub`/`BattleEventSub`；接口主流 `I_` 前缀；SO 用 `_SO`；partial 用 `主类名_分部名.cs` 同目录；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 仅对字段有效（`InspectorNameAttribute` 就是 Unity 自带、`using UnityEngine` 即可）
- 规范见 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`

## 架构与数据分层
- 组件间避免 `GetComponent` 互取；AI 用 `AIController` 组合 + 接口代理，**不继承 `Actor`**
- 默认操作视角：`ArchivesData_SO.settingDic["默认操作视角"]` → `PlayerController.ApplyViewMode()`
- 竖直占位 `I_Entity.HalfHeight`（`CenterPos.y±HalfHeight`，0=未配置→不过滤）；批量填充 `Assets/Editor/ActorHalfHeightTool.cs`
- 窗口注册：`WndManager` 挂 `public XxxWnd` 字段（GameRoot.prefab 填 0），窗口 `Init()` 由场景/prefab 的 UnityEvent 调；家具入口 `Furniture_General.furnData` / `Furniture_AttachedGeneral.furnData` 按 `Id` 分发（**无匹配 Id 时 `action` 为空，`Operate()` 什么都不做**）
- **SO 不能持有 prefab/场景实例引用**：子物体/挂点在 prefab 的 `[SerializeField]`；SO 只放与实例无关项（音效/粒子/材质/颜色/数值）。判定「改这条希望同类单位一起吃？」是→SO；语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；仅需"任意组件任意方法"才用 UnityEvent（存方法名，改名静默失效）；共享 + 需引用实例层级 → 间接引用（key/相对路径 + 运行时解析 + 编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- 家具系统：`Furniture_Attached : BaseMono, IFurniture`，`OnEnable/OnDisable` 维护静态 `Furniture_Attached.list`（交互扫描源）；`Handle(user)`→`CanOperate`+`Operate`；`ShowName`/`Id`/`Icon` 默认取自 `_actor`（`GetComponent<I_Actor>()`），**非 Actor 物体上挂家具必须 override 这三项，否则空引用**
- 计时器：`TickBehaviour.Tick()` **1 秒 1 次**（`TickTime` 默认 1）；`GameRoot.CreateTimer(cb,秒,次数,endcb)`；`GameRoot.CreatePerTimer(percb,秒,endcb)`＝**每帧回调**持续 `秒`（适合插值），到点后调 `endcb`

### 组件存活/回收（易踩）
- `VFXManager.Release(GameObject)` **只认根物体**上的 `ParticleSystem`(Stop) 或 `LimitedLife`(allowRelease=true)，两者皆无＝纯空转；`ProjectileBase` 走另一重载（回池 + `Template`）
- `LimitedLife.IsAlive()` **只被 VFXManager 池的 `Update()` 轮询** → 非池化实例（含 Nest 预置）不会被回收；`HealthOther.AutoDestroy` 才是"死亡即销毁"开关
- 池化对象"本次状态"字段必须在 `OnDisable`/`OnEnable` 复位，且放在早退 `return` 之前

### UI 展示模型（`ArmamentWnd`/`SettingWnd`/`AirdropConfigWnd`）
- 流程：实例化 prefab → 禁 MonoBehaviour + Collider、Rigidbody 设 kinematic → `SetChildLayer`；独立相机 + RenderTexture 预览，`RectTransformUtility.RectangleContainsScreenPoint` 判拖动区
- ⚠ 要**连 Awake 都不执行**：`enabled=false` 无效（Awake 只看 `activeInHierarchy`），须在**未激活挂点**下实例化 → 移除逻辑组件 → 再激活（`Destroy` 帧末生效，需等一帧；同帧用 `DestroyImmediate`）。现成实现 `AirdropConfigWnd.ShowModel`
- `FitModelScale`：相机视野半高/半宽 + 包围盒 XZ 半对角线 / Y 半高求缩放（**双向**）；包围盒中心对齐**相机视线中心**（`focus = camPos + fwd*dot(root-camPos,fwd)`）；朝向走 `_modelBaseEuler`（默认 180）+ `ApplyModelRotation()`；保留预制体自身缩放 → 世界旋转 = 挂点(默认+拖动) ∘ 预制体自身旋转
- ⚠ 量包围盒四前提：① `Renderer.bounds` 未激活时为 0 ② 带 Animator 必须先算姿势（`animator.Update(0f)` 再等一帧，可差 1.7 米）③ 蒙皮 bounds 随骨骼姿势变 ④ 量前把模型自身旋转临时归零（炮台自带 45° 会让水平尺寸虚高四成）
- ⚠ **不能直接 Encapsulate 全部 Renderer**：战备预制体混着 ① 零体积未播放粒子 ② 几百米 LineRenderer ③ `BombRange`/`EvaRange`/`Range_Sphere`/`TaskShowRange`/`DistGround*`/`Decal*` 范围网格。过滤顺序：画不出来的（`!enabled||!activeInHierarchy`）→ 特效类（仅当还有实体网格）→ 材质名关键字（`RangeMaterialKeys`）→ 零体积；全模型只剩特效才退回按全部量

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath/serializedObject 才用 `PropertyDrawer`（数组场景 `.Array.data[` 回退普通绘制）
- `InlineFieldDrawer`＝`[Singleline]` 单行内联的**唯一实现**（`DrawInlineListLayout/DrawInlineObjectLayout/DrawDecorators`；Rect 版 `DrawInlineListRect/GetInlineListHeight/DrawElementRow/DrawFooterButtons/DrawAddButton`；`InlineListStyle` 可插 `ChildLabelProvider`/`ChildDrawer`）
- `EditorOverride`（`[CustomEditor(typeof(Object),true,isFallback=true)]`）＝全局兜底 Inspector：`[Foldout]`/`[InspectorName]`/`[Compare]`/单行内联数组全靠它；**被专属 `[CustomEditor]` 完全顶掉**（全仓仅 `SoundGroup_SOEditor`、`AirdropData_SOEditor` 两个非 fallback）
- 反射自建 Drawer 须手动注入 `m_Attribute`；特性类标 `CustomPropertyDrawer`；取特性目标类型用 `GetCustomAttributesData().ConstructorArguments[0].Value as Type`
- 复用：`SOPickerPopup<T>`（`confirmMode` 控单击即选/确认）、`PrefabBatchToolBase`（遍历全部 prefab 的批量管线）；Drawer 集中 `Drawer/`
- `[DisplayField]`：默认编辑期不画、运行期只读；只对已序列化字段生效（私有须配 `[SerializeField]`）；`readonly`/Dictionary 无效；白名单 `IsSupportedType`＝Integer/Float/Boolean/String/ObjectReference/Color/Vector2/Vector3；绘制/高度共用 `ShouldDraw`
- 数据编辑器：`Editor/DataEditorWindow.cs` + `DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须显式补 `DrawField("新字段")`
- `Assets/Editor/MaterialUsageFinder.cs`（`Tools/材质引用查询`）：材质→使用者列表（只看资产，不看场景，不要嵌套引用）。做法＝`GetDependencies(path,false)` 建「材质/模型→引用者」反查表 + 「预制体/模型→依赖它的预制体」表（5865 资产 ~2s），再逐候选加载定位到 `Renderer.sharedMaterials[i]`/`Terrain.materialTemplate`/`Graphic.m_Material`/任意组件序列化字段，并沿表 BFS 出**预制体变体**（只收 `PrefabAssetType.Variant`，嵌套容器丢弃且不继续遍历；变体没覆盖材质时依赖表里查不到，只能靠这层）。同类"反查"工具可直接复用此骨架
- ⚠ **进度条会派发编辑器事件**（`DisplayProgressBar`/`DisplayCancelableProgressBar`）→ 会让本窗口 `OnGUI` 在长任务中途重入，把半成品状态清掉。写长任务的编辑器窗口必须加重入锁（`_busy`），并"先建局部数据、最后一次性替换字段"
- ⚠ **EditorWindow 的私有字段会被 Unity 存档并在域重载后恢复**：曾出现范围开关 `_scanPrefab` 恢复成 false 导致结果静默为空。UI 开关应在 `OnEnable` 复位成默认值，且"空结果"要有可见原因提示
- asmdef 限制：`Editor/Tool/EditorTools.asmdef` 的 `references: []` → 看不到 `UnityEngine.UI`（UGUI 独立程序集）；要用 UGUI/URP/项目类型就放 `Assets/Editor/`（Assembly-CSharp-Editor）

## 渲染
### URP / ToonLit / 阴影
- 附加光阴影需同时成立：变体带 `_ADDITIONAL_LIGHT_SHADOWS` + 3 参 `GetAdditionalLight(i,posWS,shadowMask)`；`LIGHT_LOOP_BEGIN` 处须先声明 `InputData inputData`
- URP Asset：附加光 Realtime、`m_AdditionalLightShadowsSupported:1`、`m_ShadowDistance:100`、级联 3；URP14 `USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA` 硬编码 0
- 未写进 CBUFFER 的属性＝死属性（`FakeAreaLight._FresnelWave` 只在 CBUFFER、没进 Properties → 恒 0）；新增 CBUFFER 字段须同步 Properties；toggle 局部关键字名＝**原名**（无 `_ON` 后缀）
- ToonLit `_ShadowMapColor`＝**暗面里该光源的乘数**；`_MAIN_LIGHT_SHADOWS` 管**接收**；URP 投影剔除走 `DrawShadows`，与 Render Queue 无关；**材质不投影头号原因**＝`disabledShaderPasses: - SHADOWCASTER`
- URP14 pass 时序：Opaques 300 / BeforeSkybox 350 / AfterSkybox 400 / BeforeTransparents 450 / AfterTransparents 500 / BeforePostProcessing 550；UI 相机勾 Clear Depth 会致贴花消失
- ToonLit 5 pass：ForwardLit(`ZWrite On`+`Blend One Zero`+`Cull Back`+Stencil Replace)/Outline/ShadowCaster/DepthOnly/DepthNormalsOnly；**透明完全靠 `clip()`**（`_UseAlphaClipping`→`col.a=step(_DissolveValue.r*1.2-0.1,v)` 再 `clip(alpha-threshold+_EdgeWidth)`），被 clip 的片元既不写色也不写深度 → 屏幕空间叠加类效果（积雪/贴花/任何重画 pass）仍会画出幽灵轮廓
- `FakeAreaLight.shader` 菲涅尔：`_FresnelScale` 取符号定方向、绝对值定强度（`fresnelDir = s>=0 ? fresnel : 1-fresnel; a = lerp(a,a*fresnelDir,abs(s))`），负值＝边缘透明/中心实

### 屏幕空间贴花（`Assets/Shader/Decal/`，源自 NiloCat）
- `SimpleDecal`（fog 恒开）/`SimpleDecal_Colour`（三平面视角颜色，有 `_UnityFogEnable`）；cube 罩住目标 + `ZTest Off`/`ZWrite off`/`Queue Transparent-499`，frag 按 `_CameraDepthTexture` 重建场景位置并 `clip` 出体积
- ⚠ **`_Cull` 必须 Front(1) 或 Off(0)，绝不能 Back(2)**：相机进 cube 内部时所有面成背面 → 全剔掉、贴花消失（`PylonPower.mat` 曾设成 2）
- ⚠ **雾与混合模式必须匹配**：`MixFog()` 适配 `SrcAlpha/OneMinusSrcAlpha`；加法式混合（`_DecalDstBlend=DstAlpha(7)`）会在纹理全黑处**额外叠一份雾色** → 足迹变可见方块。`SimpleDecal` 已加 `[Toggle(_DecalAdditiveFog)]`（默认 0，仅 `PylonPower.mat` 开）：`col.rgb = MixFog(col.rgb,f) - MixFog(half3(0,0,0),f)`
- ⚠ `col.rgb *= col.a` 预乘 + 硬件再乘 `SrcAlpha` ＝ alpha² → 雾只按 alpha² 生效，出同源色块；要么删该行，要么 `Blend One OneMinusSrcAlpha`
- 顶点雾＝`ComputeFogFactor(positionHCS.z)` + `MixFog()`；屏幕空间/投影式（贴花、抓屏、全屏 quad）＝`ComputeFogFactorZ0ToFar(max(sceneDepthVS-_ProjectionParams.y,0))`

### 自定义 UI Shader（`Assets/Shader/UIImageChannelMix.shader` 等）
- `Mask` 走模板缓冲（`_Stencil*`+Stencil 块）；`RectMask2D` **不用模板**，靠 `MaskableGraphic.SetClipRect` → `CanvasRenderer.EnableRectClipping(rect)` 注入 `_ClipRect` 并开 `UNITY_UI_CLIP_RECT`
- 自定义 UI shader 必须自带：`#pragma multi_compile_local _ UNITY_UI_CLIP_RECT`（非 global）、`float4 _ClipRect`（**不写进 Properties**）、顶点局部坐标传片元、`UnityGet2DClipping`；`_ClipRect` 空间＝顶点同一对象/Canvas 局部空间。常见失效点：Graphic 未勾 `Maskable`；图标挂在**子 Canvas** 下

### 积雪（`Scripts/Rendering/SnowRendererFeature.cs` + `Shader/Feature/SnowOverlay.shader`）
- `SnowController`（`_SnowEnabled`/`_GlobalSnowAmount`/`_SnowMask` 全局纹理阵列 + `_SnowMaskRect`/`_SnowMaskTiles`）+ RF(300) + `SnowVolume`；`RemoveSnow/AddSnow/ResetMask/FlushMask`；`_SnowMaskTiles=0` 视为满雪；挂钩 `FpsHelper.Hit`、`BattleManager.InitTerrain`
- **积雪不是后处理，是"重画一遍"**：`SnowRenderPass.Execute` 用 `DrawRenderers(cullResults,{overrideMaterial=雪材质},{RenderQueueRange.all, snowLayerMask})` —— 原材质是谁、有没有被溶解/透明，这条通道**完全不知道**
- ⚠ 故"材质 clip 成完全透明"的物体会留**积雪幽灵轮廓**：像素被丢弃→没写深度→深度缓冲里是它后面的地面；SnowOverlay（Transparent/`ZTest LEqual`/`ZWrite Off`）按几何真实位置重画→深度测试通过→雪色混上去。改真 Alpha 混合也一样（ToonLit ForwardLit 写死 `ZWrite On`，同深度 LEqual 仍会过）
- 层配置在 `Assets/Setting/New Universal Render Pipeline Asset_Renderer.asset`：`snowEntries[0]` mask=65（Default+Unit）→ `SnowOverlay_Unit.mat`；`snowEntries[1]` mask=8（Ground）→ `SnowOverlay_Ground.mat`。**Default 层默认就会积雪**；排除某物体只能靠**层**（整批共用一份 override 材质、不接受 MPB，拿不到逐物体 `_DissolveValue`）

## 任务系统
- 抽象链：`MissionBase : TickBehaviour`（`Tick()` 1s 一次；`UpdateText/UpdateTip/UpdateMission/CompleteMission/FailMission/EndMission/Uninit/Link/Activation`）→ `MissionEvacuateBase`（`InitMission` 里建 `Kei` + **插入用** `NeoNimbus`(MedivacState.Land)）→ 撤离分「静态」（终端 `KeyScreen` 触发）与「动态」（凯伊 `ReturnBag` 触发，见 09-20 日记）
- `MedivacController`：`Land`＝插入下机（全员离开 → 起飞自毁）；`Evacuate`＝撤离接人（`EvacuateTick` 全员在箱内 → `Play("Evacuate")`+`Complete` 事件+`EndGame(14)`）；`LandUpdate` 用 SmoothDamp 下降并把位移同步给玩家
- 「创建后」走 `InitMission`，「全部任务初始化后」走 `StartMission`；`Link(mission)` 订阅 `mission.OnMissionEnd += Activation`
- 主任务 `MissionCompleteKeySceern`（类名拼写如此，文件 `MissionCompleteKeyScreen.cs`）＝终端流程；子任务通过 `OnMissionCompleted` 事件汇总到父任务
- `MissionOilRefining`（炼油）：Init 空投平台(id15)/连接点(id14) → Wait 等 `MissionSubConnectPipes` 全完成 → Start(Load 180s + `errorTimes` 波次) → Repair → 回 Start → End
- 管道状态是 `Furniture_Pipe.Id` 字符串：`Pipe`→`PipeLink`→`PipeWait`→`PipeComplete`/`PipeError`；**没有"已修复"标志位**，修好＝Id 变回 `PipeComplete`；`Operate()` 里 `base.Operate()`(发 OnOperate)**先**、`case "PipeError": Complete()` **后** → 不能在 `OnOperate` 回调里读 Id 判完成，要 Tick 轮询
- 巡逻队只能 `BattleManager.CreatPatrol(Vector3)`（`WaveCreateParams` 无巡逻字段）；`EnemyController.PatrolPos` 是**到达即自毁**哨兵点（`HomePoint` 才是到达待命）；生成点要离目标足够远
- 波次：`BattleManager.CreatWave(WaveCreateParams)`；`WaveCreateParams.Default/Extra/Defensive` + 扩展 `.Set(pos[,points])`/`.Scale(f)`；`extraWave=false` 会受 `WaveCool` 限流。台词只能 `WndManager.CreatNotice(角色, groupName)`，**角色键＝`NoticeTree_SO.ID`**（"Ayane"/"Yuuka"），groupName 必须是该资产里真实存在的（Ayane：Hover/Landing/CountDownEnd/Suspend/WarnArea/TakeOff/CountDownBegins/Vehicle；Yuuka：Ready/MissionStart/Evacuate/EvacuateFail/EndKaiser/Fail/End/Warning/WaveStart_Zerg/WaveEnd_Zerg），键写错会 `data.Get()` 空引用

## 战备系统
- 资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；运行时 `ResSvc.airdropDic`
- `AirdropData_SO`：`type`（Red0轰炸/Blue1装备/Greed2炮台/Orange3载具/Yellow4补给）、`labels`（`[Flags]` Bag1/Drone2/Mine4/Jet8/Medivac16）、`opter` 方向序列（Left0/Up1/Right2/Down3，LE uint 数组）、`subAirdrop`、`creatObect`（部署 prefab，兼配置界面展示模型）、`coolGroup`、`isHide`
- 按键序列规律（已核对）：首键严格对应 →轨道轰炸(R_*)/↑鹰与空中支援/↓炮台地雷装备补给/←载具；尾键"伤害类型指纹"基本未贯彻；`Y_Machine`/`Y_OilPlane`/`Y_EvacuationBeacon` 的 opter 为空
- ⚠ `labels` 是后加字段：除 `ADSO_R_Railgun` 外其余资产 YAML 无该行（=0），需逐个填
- ⚠ `AirdropData_SOEditor` 显式列字段，漏列不显示；`DrawField` 传显式 GUIContent 会盖掉字段 `[InspectorName]`，`LabelOf` 中文名 switch 须同步补
- UI：`AirdropWnd`（HUD）、`AirdropConfigWnd`（列表分组由静态 `GroupRules` 驱动，首个命中即归类，组内偏好优先；表头第二文本＝已拥有/总数）、`ArmamentWnd`；`ArmamentButton.prefab` 子1＝偏好标记；`ArchivesData_SO`：`AirdropBuyDic`（已购）、`AirdropPreferList`（偏好/排序）
- 部署链路：`VFXAirdropEffect` 按 `deliveryType` 分支（Pod 用 `Instantiate(creatObect)`；`ImpactVfx` 指向 prefab 时由 `FpsHelper.Hit` 走 `VFXManager.Creat` + `SetOwner`）；强化 `SetOwner` 武器参数须从 **`weaponRoot`** 取；只改运行时 `AirdropData`（以 cfg 为基准）；改 `arriveTime` 须同步 `time`

## 敌人特效（`02Game/AI/FxCont`）
- `EnemyControllerFX`（抽象 partial；`EnemyFXControllerUnit`/`BuildingFXController`）；`Start` 订阅 `I_AIController` 事件，`OnDestroy` 退订；`Update`→`UpdateRS()`；`TriggerFX(type,pos,rot,parent,ignoreAudio)` 是统一入口
- 三层配置：渲染模板 SO `EnemyFxData_SO.rendererSet`（按 `sharedMaterials[i]==mat` 匹配槽位）／事件 SO `EnemyFxEventData_SO.fxDic`（`OccasionTypeEnum`→`FxSetConfig`）／组件 `fxMaterial`/`BirthMaterial`/`Animator`；资产 `Resources/GameData/EnemyFx/`；SO 不存实例引用 → `EVT_*.go` 全空；`_HitColor` 为自发光叠加（默认黑＝无闪光）
- **MPB 所有权在"渲染槽位"**（`RendererSlot{Renderer,MaterialIndex,mpb,dirty}`）：条目只 `SetColor`，帧末 `UpdateRS()` 统一 `Flush()`；同槽位只能一块；收尾必须写回"无效果值"

## 其他功能
- 天气：`WeatherSystem`（BattleManager 开局 `BattleRandom` 抽取后 Create）+ `WeatherEffect` 抽象基类（Rain/Desert/Snow）；`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")` 按 `Type` 匹配；预制体默认值须中性
- `WaveManager`：`KaiserWave` 每 `360f/creats.Count` 秒建 PhoenixEagle + 6 单位（±2 网格，大型单位单独实例化）；`CampTemplate.patrolTemplate`＝`List<SKVP<string,int>>`（队名+权重）
- 寻路：`PathRequestManager` 禁止 `pathPending` 期间"超时重试"（曾致 40 单位"跟着移动不前进"），只在 `pathPending=false` 后判 `PathInvalid/PathPartial` 并投影重试 + 10m 兜底；`RequestPath` 的 `log` 由 `EnemyController.SetNavDestination(isImportant)` 控制
- 音频：`AudioManaqerBase` 的 `sourcePool` 工厂须 `SetActive(false)`；NPC 语音家具 `Furniture_NPCChat` 用 `SoundGroup_SO` + 协程等 `AudioSource.isPlaying` 播完
- `PlayerWeaponsManager.OnWeaponSwitched`：`isSec=true` 时只设左手 IK，**不要**覆盖主武器右手 IK，否则双持判定出错
- `PhoenixEagleController` 旋转乱跳：阶段切换不要改 `lastPos.y`，改在 `Update` 里按阶段 flatten y
- 项目全景：伤害模型（弱点/护甲/护盾/抗性）、属性、武器、投射物、玩家（视角/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次、`UnitQueryGrid`、噪声地形、任务、昼夜+夜间敌袭、战备全类别、战略大地图、撤离、购买与偏好
- 改进优先级：① 随机源统一 `BattleRandom` ② `I_Damagable`/`I_Entity` 解耦为纯 DTO ③ 统一命名空间 + rootNamespace ④ God Class 审查 ⑤ `UnitQueryGrid` List 池化

## UI 图片/配色词汇表（`Assets/Images/`，参照 `SelectRoleWnd.prefab`）
- 按钮底 `FX_TEX_Lock.png`（Sliced，`ppu=2`，亮青 `(0.65,0.87,0.87)`）；锁定框 `FX_TEX_Lock_Frame.png`；列表条目/中面板 `frame_panel13.png`（Sliced）：未选中 `(0.84,1,1,0.40)`(RoleButton)/`(0.88,0.96,1,0.40)`(WeaponPreviewItem)，选中 `(1,0.97,0.84,0.40)`(SelectBtn)
- 大面板框 `SelectFrame3/4.png`（Sliced，`ppu=0.5`，`(0.67,0.96,1,0.70)`）；气泡 `SelectFrame2.png`；小图标框 `UI_Frame4.png`（Simple，alpha 0.5）；图标底 `Common_Main_SkillBG.png`；六边形 `Hexagonal_Frame(2).png`
- 进度/连接条 `LinkBar.png`（Filled）；细边框 `frame5/6.png`；箭头 `Arrow_Left/Right/Left2/Right2/Down.png`；全屏底 `99997.png`；纯色深底惯例：无 sprite + `(0,0,0,0.2~0.55)`

## MCP for Unity
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（已入库、**含汉化**）；手册＝skill `.codebuddy/skills/unity-mcp/`；**写操作前先 git commit**
- 实例 ID/hash 随环境变化（曾见 `Bluedivers@3d9f2357`、`Bluedivers@4a3e6a7b`，桥 `127.0.0.1:6400`）——用时先读 `mcpforunity://instances`；活动实例只存内存，重开会话必须重设 `set_active_instance`；写操作前核对 `projectRoot`（本机＝`D:/Pro/Bluedivers`）
- 客户端配置两份互不相通：IDE 读 `%USERPROFILE%\.codebuddy\mcp.json`；Unity 窗口 Configure 只写 CLI 的 `%USERPROFILE%\.codebuddy.json`；Unity 侧 Transport 必须 **Stdio**
- `execute_code` 只有 CodeDom（C# 6 语法）——`Material.SetKeyword(ref LocalKeyword,true)`/`IsKeywordEnabled(ref k)` **必须显式 `ref`**；用 `AssetDatabase.LoadAssetAtPath` + `GetComponentsInChildren<Renderer>(true)` 打印层/材质/属性是最快排查手段
- 改材质关键字/属性后 `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`；⚠ `Editor/Tools/Build/*.cs` 曾被 `.gitignore` 误忽略清除 → Editor 程序集编译失败（MCP 全挂），升级该包前先 `git add -f` 汉化
- 改完脚本：`validate_script`（`level=standard` 给语义级诊断，**errors 才是硬指标**，warning 多为启发式）→ `refresh_unity`（`mode=if_dirty, compile=request`）→ 桥短暂失联属域重载，等 ~10s 再 `read_console`；`read_console` 的 `types` 必须传**数组**，`count`/`include_stacktrace` 传字符串
- ⚠ **`refresh_unity` 可能检测不到 .cs 变更**（返回 `refresh_triggered:false`）。可靠兜底：`execute_code` 里 `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` + `CompilationPipeline.RequestScriptCompilation()`，等 ~20-25s 后**用 `System.AppDomain.CurrentDomain.GetAssemblies()` 遍历反射确认新类型/新方法已在 `Assembly-CSharp`**，再继续测试（比只看 console 更硬）

## 协作偏好与通用坑
- 不确定时先询问；不主动纠结/移除 using；文件夹/文件改名等结构性修改由用户**手动**做，AI 只给方案
- 上游包文件丢失排查法：`git ls-files` 与镜像目录清单比对；Unity 报"meta 存在但文件夹不存在"常意味着目录内文件被忽略/删除
- 双机/换机后第一件事：确认 MCP 连的是**当前这份副本**，再动写操作
- 编辑器特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 会顶掉全局 fallback
- Unity 资产 YAML 中**整行缺失的字段**＝脚本新增后资产尚未重新保存（按默认值处理）
- 诊断"某组件没生效"先看**是不是压根没人调它**（`LimitedLife` 只被池轮询、`VFXManager.Release` 只对池化/粒子对象生效）
- 排查视觉问题先看**实际材质/资产的属性值**（`_Cull`/blend/纹理格式/层），不要只看 shader 默认值：材质可覆盖成与默认完全不同的语义
- 需要"新增交互家具"时：新建 `Furniture_Attached` 子类（**不改** `Furniture_General`/`Furniture_AttachedGeneral` 的静态字典）→ override `ShowName`/`Id`/`Desc`/`Icon`；可用 `AddComponent` 运行时补挂，避免动他人 prefab
- ⚠ **别什么都往 `BattleEventSub` 塞**（用户明确要求，2026-09-20）：一方已能拿到另一方实例时，就 `GetComponent` + 订阅**实例事件**。先例：`MissionEvacuateBase.InitMission` 建凯伊时存下 `protected SpecUnitKei kei`，动态撤离任务直接订阅 `kei.OnReturnBagShow` 与 `kei.ReturnBag.OnRequestEvacuate`（家具自暴露 `event Action<GameObject>`，把 `owner` 带出来）。全局事件层只留给**真广播**（多个未知订阅者，如 `OnEvacuate` 同时驱动巡逻队收缩）；跨实例的状态也别用 `static` 字段（曾用 `Furniture_ReturnBag.Available` 全局闸门 → 已改实例 `Usable`）
