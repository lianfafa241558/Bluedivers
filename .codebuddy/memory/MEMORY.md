# Bluedivers 项目长期记忆

> 细节见各日 `YYYY-MM-DD.md`。最后整理：2026-09-20（v3 去重压缩）

## 环境与工具链
- 双机副本 `D:\Pro\Bluedivers`(主)/`E:\Bluedivers`；另有 `D:\Project\RTSClient`。路径、MCP 实例 hash、uvx 路径**不可跨机照抄**
- Unity 2022.3.62f3 / URP 14.0.12 / C#9 + .NET Std 2.1 / TMP 3.0.9 / Navigation 1.1.6；单机 PvE（FPS Sample 改，`Unity.FPS.*`）
- 随机源统一 `BattleRandom`；确定性计算 `PEMaths`；Python 3.12.10
- git `core.autocrlf=true`；GitHub 直连被重置 → 走 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore` 的 Unity 生成目录规则必须 `/` 锚根（未锚定的 `[Bb]uild/` 曾吞 `Packages/**/Tools/Build/`、`Assets/Art/{Anim,Modle/Enemy}/Build/`）
- 工具坑：`search_content` 的 `glob` 不可靠（`*grup*`/`*.{a,b}` 不行）→ 整目录搜或单文件 `path`；输出易爆 → 带 `headLimit`；`findstr` 读部分 fbx 报错 → `select-string`；无 `Debug.DrawWireSphere` → `Tool.DrawWireSphere`
- 规模：prefab 841 / mat 677 / fbx 276 / .asset 374 / .unity 40 / controller 154 / png 1387 / shader 59

## 结构与命名
- `Assets/Scripts/` 数字前缀=依赖顺序（00Core→00GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），上层可引下层，反之禁止
- asmdef 仅 10~14 个且在**子目录**；`01Manager/`、`02Data/`、`02Game/`、`04UI/` 根脚本、`00Tools/*.cs`、`Effect/*.cs` 全在 **Assembly-CSharp**。asmdef 均 `autoReferenced:true` → Assembly-CSharp **可**调它们，反向不行
- 存在 **01Manager↔02Game↔02Data 三角循环**（02Data 引 `FpsGame.Mission`/`Unity.FPS.Game`）→ 拆 asmdef 须先断 02Data→02Game
- 跨模块优先事件 `GlobalEventSub`/`BattleEventSub`；接口主流 `I_` 前缀；SO 用 `_SO`；partial 用 `主类名_分部名.cs` 同目录；新字段优先 `[SerializeField] private`；`[InspectorName]`/`[DisplayField]` 只对**字段**有效
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills：`bluedivers-unity/`、`data-editor/`、`unity-mcp/`

## 架构与数据分层
- 组件间避免 `GetComponent` 互取；AI 用 `AIController` 组合+接口代理，**不继承 `Actor`**；默认操作视角 `ArchivesData_SO.settingDic["默认操作视角"]`
- 竖直占位 `I_Entity.HalfHeight`（`CenterPos.y±HalfHeight`，0=未配置不过滤）；批量填充 `Assets/Editor/ActorHalfHeightTool.cs`
- 窗口注册：`WndManager` 挂 `public XxxWnd` 字段（GameRoot.prefab 填 0），窗口 `Init()` 由场景/prefab 的 UnityEvent 调；家具入口 `Furniture_General.furnData`/`Furniture_AttachedGeneral.furnData` 按 `Id` 分发
- **SO 不能持有 prefab/场景实例引用**：子物体/挂点放 prefab 的 `[SerializeField]`；SO 只放与实例无关项（音效/粒子/材质/颜色/数值）。「改这条希望同类单位一起吃?」是→SO；语义不同时**叠加**而非覆盖
- 只需"启用/禁用物体"用 `List<{GameObject,bool}>`；仅需"任意组件任意方法"才用 UnityEvent（存方法名，改名静默失效）；共享+需引用实例层级→间接引用（key/相对路径+运行时解析+编辑器校验）
- 价格/消耗统一 `List<SKVP<OOPartEnum,int>>` + `wndManager.CreatTip(new(){costs=...})`
- 家具：`Furniture_Attached : BaseMono, IFurniture`，OnEnable/OnDisable 维护静态 `Furniture_Attached.list`（交互扫描源）；`Handle(user)`→`CanOperate`+`Operate`；`ShowName`/`Id`/`Icon` 默认取自 `_actor`，**非 Actor 物体挂家具必须 override 这三项**
- 计时器：`TickBehaviour.Tick()` **1 秒 1 次**；`GameRoot.CreateTimer(cb,秒,次数,endcb)`；`GameRoot.CreatePerTimer(percb,秒,endcb)`=**每帧回调**持续 秒（适合插值），到点调 endcb
- 波次：`BattleManager.CreatWave(WaveCreateParams)` → `WaveManager` 建 `RobotWave`（凤凰鹰）/`ZergWave`（空降）；皆 `I_TickClass`、1s Tick，`End` 时 `Dispose()` 返回 false 被移出
  - `WaveCreateParams{center/points/range/scale/tip/onEnd/centerGetter}`：`onEnd` 在 `Dispose` 回调（续刷安全，`WaveManager.Update` 倒序遍历）；`centerGetter` 每 Tick 刷 `center`，**只影响尚未落点**的单位
  - ZergWave **重部署**：`centerGetter!=null` + 随机生成点 + Ongoing + 距 `anchorCenter` > 50m → `RedeployPods()`（Play"End"+`LimitedLife.ResetLift(6)`）→ 重算生成环重投；`WaveUseObject[0]`=空投舱
  - `KaiserWave` 每 `360f/creats.Count` 秒建 PhoenixEagle+6 单位（±2 网格，大型单位单独实例化）；`CampTemplate.patrolTemplate`=`List<SKVP<string,int>>`
  - 巡逻队只能 `BattleManager.CreatPatrol(Vector3)`；`EnemyController.PatrolPos` 是**到达即自毁**哨兵点（`HomePoint` 才是到达待命）

## 组件存活/回收（易踩）
- `VFXManager.Release(GameObject)` 只认根物体上的 `ParticleSystem`(Stop)/`LimitedLife`(置 `allowRelease`)，皆无=空转；`ProjectileBase` 走另一重载（回池+`Template`）
- `LimitedLife.IsAlive()` 只被 VFXManager 池 Update 轮询 → 非池化实例（含 Nest 预置）不会被回收；`HealthOther.AutoDestroy` 才是"死亡即销毁"
- `LimitedLife` 支持**延时回收** `EndDelay`：到寿先 `InvokeEnd()`（`endInvoked` 保证一次），再等 EndDelay 秒；`allowRelease` 强制回收**不走延时**；`AllowPreRelease` 阈值 `+EndDelay`；`OnShow`/`ResetLift`/延长寿命都复位 `endInvoked`。⚠ `SubtitleAirdrop.Update` 也用 `IsAlive()`
- 池化对象"本次状态"字段必须在 OnDisable/OnEnable 复位，且放在早退 return 之前

## 地形/植被
- 地形数据 `Assets/Art/TerrainData/MainMap.asset`（**二进制**读不了）；地形由 `BattleManager.InitTerrain` → `MapRoot`(Tag=MapRoot) → `GenerateNoiseTerrain.ApplyFractalNoiseToTerrain` 运行时重建（高度图/贴图/树/草/NavMesh）；贴图注入 `GenerateNoiseTerrain.SetTextures`，层索引 0 草/1 沙/3 巢穴(`CampData_SO.NestTerrainItem` 覆盖)/4 岩
- **草（Details）永远无碰撞体**；树碰撞体 = 树原型预制体自带 Collider + `TerrainCollider.m_EnableTreeColliders`（本场景=1），**只在运行时有**。本项目树素材（`Art/Nature/Tree/Prefab/tree_a..k`、`Assets/Tree.prefab`）均**无 Collider**
- `TerrainData` **无 `RemoveTreeInstance`**；只能 `SetTreeInstance(s)`（不可改 `position`/`prototypeIndex`，否则 `ArgumentException`）/`GetTreeInstance`/`treeInstanceCount`
- 树销毁 `08Map/TreeDestructor.cs`：索引→世界坐标表 + 圆柱射线 + 按帧合并提交；销毁=实例 scale 置 0（**索引不变、可 `RestoreAll`**），`SetTreeInstances(arr,false)`（**必须 false**）+`terrain.Flush()`，限流 4/s；接入 `GenerateNoiseTerrain`(自动 AddComponent)/`FpsHelper.Hit`/`ModifyTerrain`
- 两套入队语义（`TreeDestructor`）：① **摧毁** `DestroyInRadius(center,r,yTol)` 走"可被摧毁"白名单；② **清除** `ClearInRadius(center,r,protoMin,protoMax,yTol)` / `ClearInRectXZ(center,halfSize,protoMin,protoMax,yTol)` = 按原型区间清（**忽略白名单**），传 -1 表示不限。`ModifyTerrain` 用②：弹坑按 `data.outerRadius` 圆、附加地形按"角点+半尺寸"的轴对齐矩形（`additionTerrain.terrainData.size`），只清 `clearVegetationRange`（默认 7-10 = 树），石块 0-6 保留
- ⚠ **地形树碰撞体开关**（2026-09-21 MCP 实测为 **关**）：`TerrainCollider.m_EnableTreeColliders = 0` → 树原型上的 Collider **一律不生效**（子弹/玩家都穿树）。它是**全局**开关，打开后 0-6 石块也会一起变实心；且树原型上除树干胶囊外还挂着 r=2~5、高 7~12m 的"树冠碰撞体"（`PP_Birch_Tree_05 2`/`PP_Birch_Tree_06 4`/`PP_Tree_02 4`/`PP_Tree_10 2`），开启后会变成半空隐形墙 → **先删树冠碰撞体再开开关**
- 层掩码实测（`GameContract.LayerDefinition` @ 01_GameContract，运行时静态值）：子弹 `HittableLayers=73=Default+Ground+Unit`、玩家移动 `MoveableLayers=8265=Default+Ground+Unit+AirWall`、`AirWallLayers=8192`。Terrain 在 Ground 层且 `preserveTreePrototypeLayers=False`（树实例统一用 Terrain 的层）→ 开树碰撞体后无需改 LayerConfigInitializer。`Physics.queriesHitTriggers=True`（武器射线会命中 Trigger）
- "找不到的隐形碰撞体"三类来源：① 地形树碰撞体**不是 GameObject**（只能 Physics Debug 看）② `MapRoot.CreatAirWall()` 运行时创建 18 个 AirWall 层边界墙（编辑模式场景里没有）③ 建筑隐形碰撞代理（`OilPlane`/`Turret_Mission_Machine` 的 `PlaneA1..F`/`Spine2`/`StairsA1..C2`/`GunArmor`，Default 层、**无 Renderer**）+ `EntityRoot` 的 `KeyScreen`/2 个无名箱（Ground 层）。定位：Physics Debug 的 Collider Geometry + Hierarchy 按名字搜
- 细节（草/花）擦除 `08Map/TerrainDetailEraser.cs`：与 `TreeDestructor` 同款"入队 + 按帧合并提交(限流 8/s)"，`Rebuild(Terrain)` 由 `GenerateNoiseTerrain` 自动挂载；`ClearInRadius(center,r)` / `ClearInRectXZ(center,halfSize)` 逐 detail 层 `GetDetailLayer → 清零非零格 → SetDetailLayer`（rect 先 Clamp；**Terrain.position 是角点**；圆请求用格中心反算世界坐标判定）。接入：`FpsHelper.Hit` 的 `destructe>0`、`ModifyTerrain` 弹坑与附加地形。草的擦除不影响碰撞/寻路，换局 `SpawnDetails` 自动恢复
- NavMesh：MapRoot `NavMeshSurface` `m_UseGeometry:0`(RenderMeshes) + LayerMask bits 8 → **烘焙不含树**，AI 穿树
- 树种白名单：`TreeDestructor` 支持"只允许指定 `prototypeIndex`(0基) 被摧毁"——`Rebuild(terrain, IList<int>)`/`SetDestructiblePrototypes(params int[])`/`IsPrototypeDestructible(int)`；**空列表 = 全放行**；配置落点在 `GenerateNoiseTerrain._destructibleTreePrototypes`（TreeDestructor 是运行时 AddComponent，自身序列化字段不落盘）；Inspector 只读串 `树种统计` 显示 `[索引]x数量 可毁/免疫`
- **石块/树/草同构的逐格概率算法**（2026-09-20 改）：`SpawnVegetation(VegetationSpawnData cfg, string label, float rateOverride)` 遍历每个格子 `if (Random.value >= rate) continue;`（**先掷骰再查坡度/高度**，省掉 99% 的约束计算），命中才建 `TreeInstance`，按 `maxTimePerFrame` 每 8 行分帧。`probability` 语义 = **每格(=每㎡)出现概率**，`0.001 ≈ 每 1000㎡ 一处`，与地图尺寸无关
- ⚠ **地形 Tree Prototypes 索引：0-6 = 石块，7-10 = 真树**（用户口径）。`TerrainPresetData` 用 `rockSpawn` / `treeSpawn` **两份 `VegetationSpawnData`**（`probability/prototypeRange(含头含尾)/minSlope/maxSlope/minHeight/maxHeight`）分别生成，最后一次性 `SetTreeInstances`；石块 range (0,6) 约束更宽(坡度 35~60、高度范围大)，树 range (7,10)。概率：沙漠 0.0012/0.0002、高原 0.0015/0.0008、雨林 0.0008/0.003、丘陵 0.0025/0.0015、盆地 0.002/0.001、平原 0.0012/0.0006、山地 0.0025/0.0003（**石/树**，512 图名义 ≈ 300~650 石块 + 50~780 树，再被约束打折）
- 覆盖模式：`_overridePreset` 勾上后，组件上的 `覆盖用石块概率 _rockProbability` / `覆盖用树概率 treeProbability` 生效，**<0 = 沿用预设**（两者默认都是 -1）
- ⚠ 旧算法 `targetCount = FloorToInt(prob × heightmapRes² / 10000)` 在小图上会 FloorToInt 归零（沙漠/平原/山地必然 0 棵）——已废弃；旧的 `treeProbability/treePrototypeRange/treeMin*/treeMax*` 字段名已全部替换，全仓无残留

## UI 展示模型（ArmamentWnd/SettingWnd/AirdropConfigWnd）
- prefab→禁 MonoBehaviour+Collider、Rigidbody kinematic→`SetChildLayer`；独立相机+RenderTexture 预览，`RectTransformUtility.RectangleContainsScreenPoint` 判拖动区
- ⚠ 要**连 Awake 都不执行**：`enabled=false` 无效，须在未激活挂点下实例化→移除逻辑组件→再激活（Destroy 帧末生效）；现成实现 `AirdropConfigWnd.ShowModel`
- `FitModelScale`：相机视野半高/半宽 + 包围盒 XZ 半对角线/Y 半高求缩放；中心对齐 `focus=camPos+fwd*dot(root-camPos,fwd)`；朝向 `_modelBaseEuler`(默认 180)+`ApplyModelRotation()`
- ⚠ 量包围盒四前提：① 未激活 bounds=0 ② 带 Animator 先 `animator.Update(0f)` 再等一帧 ③ 蒙皮 bounds 随姿势变 ④ 量前把自身旋转归零
- ⚠ **不能 Encapsulate 全部 Renderer**：混零体积粒子/几百米 LineRenderer/范围网格（`BombRange`/`EvaRange`/`Range_Sphere`/`TaskShowRange`/`Decal*`）。过滤顺序：画不出的→特效类→材质名关键字(`RangeMaterialKeys`)→零体积；全只剩特效才退回全量

## 编辑器扩展
- 装饰特性一律 `DecoratorDrawer`；需读 propertyPath/serializedObject 才用 `PropertyDrawer`（数组 `.Array.data[` 回退普通绘制）
- `InlineFieldDrawer` 是 `[Singleline]` 内联的**唯一实现**；`EditorOverride`（全局 fallback Inspector，提供 `[Foldout]`/`[InspectorName]`/`[Compare]`）**被专属 `[CustomEditor]` 完全顶掉**（全仓仅 `SoundGroup_SOEditor`、`AirdropData_SOEditor`）
- 反射自建 Drawer 须手动注入 `m_Attribute`；特性类标 `CustomPropertyDrawer`；取目标类型用 `GetCustomAttributesData().ConstructorArguments[0].Value as Type`
- 复用：`SOPickerPopup<T>`、`PrefabBatchToolBase`；Drawer 集中 `Drawer/`
- `[DisplayField]`：编辑期不画、运行期只读；只对已序列化字段生效；readonly/Dictionary 无效；白名单 Integer/Float/Boolean/String/ObjectReference/Color/Vector2/Vector3；绘制/高度共用 `ShouldDraw`
- 数据编辑器：`Editor/DataEditorWindow.cs`+`DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须补 `DrawField("新字段")`
- `Assets/Editor/MaterialUsageFinder.cs`（`Tools/材质引用查询`）：`GetDependencies(path,false)` 建「材质/模型→引用者」表（5865 资产 ~2s），再定位 `Renderer.sharedMaterials[i]`/`Terrain.materialTemplate`/`Graphic.m_Material`/任意序列化字段，沿表 BFS 出预制体变体（只收 `PrefabAssetType.Variant`）
- ⚠ `DisplayProgressBar` 会派发编辑器事件 → 长任务窗口须加重入锁 `_busy`，并"先建局部数据、最后一次性替换字段"；⚠ **EditorWindow 私有字段会被 Unity 存档并在域重载后恢复**，UI 开关应在 OnEnable 复位
- asmdef 限制：`Editor/Tool/EditorTools.asmdef` 的 `references: []` → 看不到 `UnityEngine.UI`；要用 UGUI/URP/项目类型就放 `Assets/Editor/`

## 渲染
### URP / ToonLit
- 附加光阴影需同时：变体带 `_ADDITIONAL_LIGHT_SHADOWS` + 3 参 `GetAdditionalLight(i,posWS,shadowMask)`；`LIGHT_LOOP_BEGIN` 前先声明 `InputData inputData`
- URP Asset：附加光 Realtime、`m_AdditionalLightShadowsSupported:1`、`m_ShadowDistance:100`、级联 3；URP14 `USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA` 硬编码 0
- 未写进 CBUFFER 的属性=死属性；新增 CBUFFER 字段须同步 Properties；toggle 局部关键字名=**原名**（无 `_ON` 后缀）
- `_ShadowMapColor`=暗面里该光源的乘数；`_MAIN_LIGHT_SHADOWS` 管接收；**材质不投影首因**=`disabledShaderPasses: - SHADOWCASTER`
- URP14 pass 时序：Opaques 300 / BeforeSkybox 350 / AfterSkybox 400 / BeforeTransparents 450 / AfterTransparents 500 / BeforePostProcessing 550；UI 相机勾 Clear Depth 会致贴花消失
- ToonLit 5 pass（ForwardLit/Outline/ShadowCaster/DepthOnly/DepthNormalsOnly）**共用 `ToonLit_Shared.hlsl`**，`GetFinalBaseColor`(376) 是 albedo 唯一入口 → 改一处，描边/深度/阴影裁切自动一致；**透明完全靠 clip()**，被 clip 片元不写色也不写深度
- `ToonLit_Stone.shader`（guid `0032fd96...`）仅被 `Rocks and Boulders 2/Rocks/Prefabs/新建材质.mat` 一个材质引用；`Cull Back` 硬编码不支持 `Cull[_Cull]`；`_BaseMap`+`_BlendingMap` 双图混合
- **让材质纹理自动跟随数据（如 `MapData_SO.TerrainItem`）优先"脚本注入 + 保留 shader"**：shader 读不到 SO，新写 shader 不解决"自动取值"。三档：① MPB per-renderer（0 shader 改动）② `Shader.SetGlobalTexture` 全局纹理 + `#define` 支路（推荐，全自动，全局纹理**不可写进 Properties**）③ 真要"随脚下地形层变"才改 shader（烘地形权重小 RT + 全局 `_TerrainRect`，照抄 `SnowRendererFeature` 的 `_SnowMaskRect`）。⚠ `TerrainLayer.tileSize`(世界米) 与 `_BaseMap_ST`(uv 乘数) 量纲不同
- `ToonLit_Shared.hlsl` 被 **7 个** shader 共用（`ToonLit`/`_Colour`/`_Face`/`_Hair`/`_MouthEye`/`_Scene`/`_Stone`）→ 给它加东西的三条边界：① 宏只写在**目标 shader 自己的 `HLSLINCLUDE`**（一次覆盖其全部 pass；`ToonShaderIsOutline` 写在 pass 内是因为只有部分 pass 需要）② shared 里只**新增** `#ifdef` 块（既有语句语义不动）③ 新数据只走**全局 uniform**，不进 Properties/CBUFFER，也别动 `Varyings`
- **宏要按 pass 精确控制**：`GetFinalBaseColor` 被 5 个 pass 调用，但只有 ForwardLit/Outline 用 albedo，ShadowCaster/DepthNormalsOnly/DepthOnly 只取 `.a`（且 `_UseAlphaClipping=0` 时是 no-op）→ 宏只该写在需要的 pass 里
- **已实施**（2026-09-20）：`ToonLitStoneTerrainTex` 只在 Stone 的 **ForwardLit / Outline** 两个 pass 定义 + shared 里两处 `#ifdef` 支路（`_BaseMap` 支路用 `#ifdef/#else` 分别给 `backCol` 赋值，避免死采样）+ `GenerateNoiseTerrain.SetTextures` 里 `Shader.SetGlobalTexture("_TerrainBaseTex"/"_TerrainBlendTex", Texture[0]/[1])` → 石头 `_BaseMap`/`_BlendingMap` 自动用 `MapData_SO.TerrainItem[0]/[1]`，材质 Tiling/Offset 仍生效，其它 6 个 shader 0 影响（`GetShaderMessages` 实测 0 error、无新增诊断类型）。⚠ 材质上的贴图槽位对石头不再生效（别加 `[HideInInspector]`，会连仍生效的 Tiling/Offset 一起藏掉）；全局纹理未设置时是默认灰/白；若石头启用溶解，必须给 3 个深度/阴影 pass 补回宏
- ToonLit 家族 **4 个死参数**：`_IndirectLightMultiplier` / `_DirectLightMultiplier` / `_MainLightIgnoreCelShade` / `_AdditionalLightIgnoreCelShade` 在所有 `.hlsl` 里 **0 引用**（原版遗留）。2026-09-20 已从 `ToonLit_Stone.shader` 删除（同时删了当前值=0 的 `_ReceiveShadowMappingPosOffset`/`_FixOutlineColor`/`_UseAverNormal`，Properties 25→17，7 个 shader `GetShaderMessages` 全 0）；⚠ `ToonLit.shader`(main) 挂 `CustomEditor "ToonLitMainShaderGUI"`，`ToonLitShaderGUI.cs:325` 的 `FindProperty` 不判空 → 别在 main 上删
- `ToonLit_Shared.hlsl` 的 `GetFinalSpecular` 已改为 `if (_UseSpecular)` 内才采样（未声明 `_SpecularMap` 的 shader 不再每像素白采未绑定贴图，行为不变）
- 从 Properties 删一个**被代码读取**的属性 = CBUFFER 取 0（如 `_BaseColor`/`_BaseScale`/`_ShadowMapColor`/`_FogMaxValue` 删了就全黑/不吃雾）；`_BaseMap` 贴图槽位虽半死，但 `_BaseMap_ST`/`_BlendingMap_ST` 仍被顶点 uv 与混合 uv 使用 → 不能删
- `ToonLit_Shared.hlsl` 里的 `implicit truncation of vector type` 已清零：`half3 blendMap = tex2D(...)` 两分支补 `.rgb`、`_ColourColor.rgb`；改后 `GetShaderMessages` 7 个 shader 全部 0（2026-09-20）
- `FakeAreaLight.shader` 菲涅尔：`_FresnelScale` 符号定方向、绝对值定强度

### 屏幕空间贴花（Assets/Shader/Decal/，源自 NiloCat）
- `SimpleDecal`(fog 恒开)/`SimpleDecal_Colour`；cube 罩目标+`ZTest Off`/`ZWrite off`/`Queue Transparent-499`，frag 重建场景位置并 clip
- ⚠ `_Cull` 必须 Front(1) 或 Off(0)，绝不能 Back(2)（相机进 cube 内部全剔掉→消失）
- ⚠ 雾与混合模式必须匹配：加法混合(`_DecalDstBlend=DstAlpha`)会在纹理全黑处叠雾色→足迹变方块；`SimpleDecal` 已加 `[Toggle(_DecalAdditiveFog)]`（默认 0，仅 `PylonPower.mat` 开）
- ⚠ `col.rgb *= col.a` 预乘+硬件再乘 SrcAlpha=alpha²；顶点雾 `ComputeFogFactor(positionHCS.z)`，屏幕空间 `ComputeFogFactorZ0ToFar(...)`

### 自定义 UI Shader
- `Mask` 走模板缓冲；`RectMask2D` 走 `CanvasRenderer.EnableRectClipping`+`_ClipRect`
- 必须自带 `#pragma multi_compile_local _ UNITY_UI_CLIP_RECT`（非 global）、`float4 _ClipRect`（**不写进 Properties**）、顶点局部坐标传片元、`UnityGet2DClipping`。常见失效：Graphic 未勾 Maskable；图标在子 Canvas 下

### 积雪
- `SnowController`(`_SnowEnabled`/`_GlobalSnowAmount`/`_SnowMask`+`_SnowMaskRect`/`_SnowMaskTiles`) + RF(300) + `SnowVolume`；`RemoveSnow/AddSnow/ResetMask/FlushMask`；`_SnowMaskTiles=0` 视为满雪；挂钩 `FpsHelper.Hit`、`BattleManager.InitTerrain`
- **积雪不是后处理，是"用雪材质重画一遍"**（`DrawRenderers`+overrideMaterial+`RenderQueueRange.all`）：原材质 clip 全透明也会留**幽灵轮廓**（像素被丢弃没写深度）
- 层配置 `Assets/Setting/New Universal Render Pipeline Asset_Renderer.asset`：`snowEntries[0]` mask=65→`SnowOverlay_Unit.mat`，`snowEntries[1]` mask=8(Ground)→`SnowOverlay_Ground.mat`；**Default 层默认就积雪**，排除只能靠层

## 任务系统
- 抽象链 `MissionBase : TickBehaviour`（Tick 1s；`UpdateText/UpdateTip/UpdateMission/CompleteMission/FailMission/EndMission/Uninit/Link/Activation`；`Uninit` 由 `EndMission` 或 `OnDestroy(!end)` 调）→ `MissionEvacuateBase` → 静态(终端 KeyScreen)/动态(凯伊 ReturnBag)；`UseSceneStartPoint` 虚开关控快速模式是否顶替 StartPoint（Mobile 覆写 false）
- `MedivacController`：`Land`=插入下机；`Evacuate`=撤离接人；`TakeOff()`（Play"Evacuate"+开 cam+派发 `Complete`+**只隐藏 IsInBox 玩家**）/`IsInBox`/`ForceTakeOff()` 共用
- 「创建后」走 `InitMission`，「全部任务初始化后」走 `StartMission`；`Link(mission)` 订阅 `mission.OnMissionEnd += Activation`
- 主任务 `MissionCompleteKeySceern`（类名拼写如此）=终端流程；子任务 `OnMissionCompleted` 汇总给父任务
- `MissionOilRefining`：Init 空投平台(id15)/连接点(id14)→Wait 等 `MissionSubConnectPipes`→Start(Load 180s+`errorTimes` 波次)→Repair→End
- 管道状态是 `Furniture_Pipe.Id` 字符串：`Pipe`→`PipeLink`→`PipeWait`→`PipeComplete`/`PipeError`；**没有"已修复"标志位**；`Operate()` 里 `base.Operate()` 先、`Complete()` 后 → 不能在 `OnOperate` 回调里读 Id 判完成，要 Tick 轮询
- 台词只能 `WndManager.CreatNotice(角色, groupName)`，**角色键=NoticeTree_SO.ID**（Ayane/Yuuka/Kai），groupName 必须真实存在，写错会空引用

## 战备系统
- 资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；运行时 `ResSvc.airdropDic`
- `AirdropData_SO`：`type`(Red0轰炸/Blue1装备/Greed2炮台/Orange3载具/Yellow4补给)、`labels`(`[Flags]`)、`opter`（Left0/Up1/Right2/Down3，LE uint 数组）、`subAirdrop`、`creatObect`、`coolGroup`、`isHide`
- 按键序列：首键严格 →→轨道轰炸/↑鹰与空中支援/↓炮台地雷装备补给/←载具；尾键"伤害类型指纹"基本未贯彻；部分特殊战备 opter 为空
- ⚠ `labels` 是后加字段：除 `ADSO_R_Railgun` 外资产 YAML 无该行（=0），需逐个填；⚠ `AirdropData_SOEditor` 显式列字段，漏列不显示
- UI：`AirdropWnd`(HUD)、`AirdropConfigWnd`（分组由静态 `GroupRules` 驱动，首个命中即归类）、`ArmamentWnd`；`ArmamentButton.prefab` 子1=偏好标记；`ArchivesData_SO` 的 `AirdropBuyDic`/`AirdropPreferList`
- 部署链路：`VFXAirdropEffect` 按 `deliveryType` 分支（Pod 用 `Instantiate(creatObect)`；`ImpactVfx` 由 `FpsHelper.Hit` 走 `VFXManager.Creat`+`SetOwner`）；强化 `SetOwner` 武器参数从 `weaponRoot` 取；改 `arriveTime` 须同步 `time`；`permanentPod` 会 `LimitedLife.ResetLift(9999)`
- ⚠ 本仓库岩石/植被素材包（`Rocks and Boulders 2`、`Pure Poly` 等）基本**未被游戏场景引用**，只在各自 Demo 场景用；`Rock1LOD_grup*.prefab` 是孤儿

## 敌人特效（02Game/AI/FxCont）
- `EnemyControllerFX`（抽象 partial）+`EnemyFXControllerUnit`/`BuildingFXController`；Start 订阅 `I_AIController` 事件、OnDestroy 退订；`TriggerFX(type,pos,rot,parent,ignoreAudio)` 是统一入口
- 三层配置：渲染模板 SO `EnemyFxData_SO.rendererSet`（按 `sharedMaterials[i]==mat` 匹配槽位）/事件 SO `EnemyFxEventData_SO.fxDic`/组件 `fxMaterial`/`BirthMaterial`/`Animator`；SO 不存实例→`EVT_*.go` 全空；`_HitColor` 为自发光叠加
- **MPB 所有权在"渲染槽位"**（`RendererSlot{Renderer,MaterialIndex,mpb,dirty}`）：条目只 SetColor，帧末 `UpdateRS()` 统一 Flush；同槽位只能一块；收尾必须写回"无效果值"

## 其他功能
- 伤害总入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸(`BattleManager.FindUnits`/`GetExplosionDamage`/穿甲/拆毁)→冲击波→**地形破坏 `destructe>0`**（擦雪+`ModifyHeightMap` 协程+`TreeDestructor.DestroyInRadius`）→警告/特效/音效/弹痕
- 天气：`WeatherSystem`（开局按 `mapCfg.WeatherInfos` 用 `BattleRandom` 抽）+`WeatherEffect` 抽象（Rain/Desert/Snow），`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")` 按 Type 匹配；预制体默认值须中性
- 寻路：`PathRequestManager` 禁止 `pathPending` 期间"超时重试"（曾致 40 单位"跟着移动不前进"），只在 `pathPending=false` 后判 `PathInvalid/PathPartial` 并投影重试+10m 兜底；log 由 `EnemyController.SetNavDestination(isImportant)` 控
- 音频：`AudioManaqerBase.sourcePool` 工厂须 `SetActive(false)`；NPC 语音家具 `Furniture_NPCChat` 用 `SoundGroup_SO`+协程等 `AudioSource.isPlaying`
- `PlayerWeaponsManager.OnWeaponSwitched`：`isSec=true` 时只设左手 IK，**不要**覆盖主武器右手 IK；`PhoenixEagleController` 旋转乱跳=阶段切换别改 `lastPos.y`
- 项目全景：伤害模型（弱点/护甲/护盾/抗性）、属性、武器、投射物、玩家（视角/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次、`UnitQueryGrid`、噪声地形、任务、昼夜+夜间敌袭、战备、战略大地图、撤离、购买与偏好
- 改进优先级：① 随机源统一 `BattleRandom` ② `I_Damagable`/`I_Entity` 解耦为纯 DTO ③ 统一命名空间+rootNamespace ④ God Class 审查 ⑤ `UnitQueryGrid` List 池化

## UI 图片/配色词汇表（Assets/Images/，参照 SelectRoleWnd.prefab）
- 按钮底 `FX_TEX_Lock.png`（Sliced，ppu=2，亮青 0.65/0.87/0.87）；锁定框 `FX_TEX_Lock_Frame.png`；列表条目/中面板 `frame_panel13.png`（Sliced）：未选中 (0.84,1,1,0.40)(RoleButton)/(0.88,0.96,1,0.40)(WeaponPreviewItem)，选中 (1,0.97,0.84,0.40)(SelectBtn)
- 大面板框 `SelectFrame3/4.png`（Sliced，ppu=0.5，(0.67,0.96,1,0.70)）；气泡 `SelectFrame2.png`；小图标框 `UI_Frame4.png`；图标底 `Common_Main_SkillBG.png`；六边形 `Hexagonal_Frame(2).png`
- 进度/连接条 `LinkBar.png`(Filled)；细边框 `frame5/6.png`；箭头 `Arrow_Left/Right/Left2/Right2/Down.png`；全屏底 `99997.png`；纯色深底惯例：无 sprite+(0,0,0,0.2~0.55)

## MCP for Unity
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（已入库含汉化）；手册=skill `unity-mcp/`；**写操作前先 git commit**
- 实例 ID/hash **随环境变化**（2026-09-20 实测 `Bluedivers@4a3e6a7b`，旧 `3d9f2357` 失效）；用时先读 `mcpforunity://instances`；活动实例只存内存，重开会话必须重设 `set_active_instance`；写操作前核对 `projectRoot`（本机 `D:/Pro/Bluedivers`）
- 客户端配置两份互不相通：IDE 读 `%USERPROFILE%\.codebuddy\mcp.json`；Unity 窗口 Configure 只写 CLI 的 `%USERPROFILE%\.codebuddy.json`；Unity 侧 Transport 必须 Stdio
- ⚠ 会话里看不到 `mcp__`/`mcpforunity__` 工具时**不要假装能查编辑器**：只能读文件/资产（二进制 TerrainData、prefab 真实数值拿不到），要如实说"未证实"
- `execute_code` 只有 CodeDom(C#6)：`SetKeyword(ref LocalKeyword,true)` 必须显式 ref；排查用 `AssetDatabase.LoadAssetAtPath`+`GetComponentsInChildren<Renderer>(true)`
- 改材质关键字/属性后 `EditorUtility.SetDirty`+`AssetDatabase.SaveAssets`；⚠ `Editor/Tools/Build/*.cs` 曾被 .gitignore 误清→编译失败(MCP 全挂)，升级包前先 `git add -f` 汉化
- 改完脚本：`validate_script`(level=standard) → `refresh_unity`(if_dirty) → 桥短暂失联属域重载，等 ~10s 再 `read_console`（`types` 传**数组**）；⚠ `refresh_unity` 可能检测不到 .cs 变更，兜底 `AssetDatabase.Refresh(ForceUpdate)`+`CompilationPipeline.RequestScriptCompilation()`，~20-25s 后反射 `AppDomain.CurrentDomain.GetAssemblies()` 确认

## 协作偏好与通用坑
- 不确定时先询问；不主动纠结/移除 using；文件夹/文件改名等结构性修改由用户**手动**做，AI 只给方案
- 用户常连问"某功能能不能做/为什么没有"→先给**结论 + 证据（文件:行）**，再给选项与代价，最后才谈落地；不要反问他想要的方案
- 上游包文件丢失排查：`git ls-files` 与镜像目录清单比对；Unity 报"meta 存在但文件夹不存在"常意味文件被忽略/删除
- 双机/换机后第一件事：确认 MCP 连的是当前副本再动写操作
- 编辑器特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 会顶掉全局 fallback
- 资产 YAML 中**整行缺失的字段**=脚本新增后资产尚未重新保存（按默认值处理）
- 诊断"某组件没生效"先看**是不是压根没人调它**（`LimitedLife` 只被池轮询、`VFXManager.Release` 只对池化/粒子对象生效）
- 排查视觉问题先看**实际材质/资产属性值**，别只看 shader 默认值（材质可覆盖成完全不同语义）
- 需要"新增交互家具"时：新建 `Furniture_Attached` 子类（**不改** Furniture_General/AttachedGeneral 的静态字典）→override ShowName/Id/Desc/Icon；可 AddComponent 运行时补挂，避免动他人 prefab
- ⚠ **别什么都往 `BattleEventSub` 塞**（用户明确要求）：一方已能拿到另一方实例时就订阅**实例事件**；全局事件层只留给真广播（如 `OnEvacuate` 同时驱动凯伊带队+巡逻队收缩）；跨实例状态也别用 `static`
- 新增脚本尽量用**运行时自动挂载/自建组件**接入（如 `TreeDestructor.Rebuild` 内部 AddComponent），避免改场景/prefab YAML；需要手改 prefab 时明确告知用户去点
