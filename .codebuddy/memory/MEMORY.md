# Bluedivers 项目长期记忆

> 细节见各日 `YYYY-MM-DD.md`。最后整理：2026-09-21（v6 精简去重）

## 环境与工具链
- 同名副本多份：本机可能是 `D:\Pro\Bluedivers`，规则文件里 MCP 钉的是 `E:\Bluedivers`（另一台/另一副本）；另一项目 `D:\Project\RTSClient`。**实例 hash 随项目路径变化**，路径/hash/uvx 路径不可跨机照抄
- Unity 2022.3.62f3 / URP 14.0.12 / C#9 + .NET Std 2.1 / TMP 3.0.9 / Navigation 1.1.6；单机 PvE（FPS Sample 改，`Unity.FPS.*`）
- 随机源统一 `BattleRandom`；确定性计算 `PEMaths`；Python 3.12.10
- git `core.autocrlf=true`；GitHub 直连被重置 → 走 `cdn.jsdelivr.net/gh/<repo>@<ref>/<path>`
- `.gitignore` 的 Unity 生成目录规则必须 `/` 锚根（未锚定的 `[Bb]uild/` 曾吞 `Packages/**/Tools/Build/`、`Assets/Art/{Anim,Modle/Enemy}/Build/`）
- 工具坑：`search_content` 的 `glob` 不可靠（`*grup*`/`*.{a,b}` 不行）→ 整目录搜或单文件 `path`；输出易爆 → 带 `headLimit`；`findstr` 读部分 fbx 报错 → `select-string`；无 `Debug.DrawWireSphere` → `Tool.DrawWireSphere`
- 规模：prefab 841 / mat 677 / fbx 276 / .asset 374 / .unity 40 / controller 154 / png 1387 / shader 59

## 结构与命名
- `Assets/Scripts/` 数字前缀=依赖顺序（00Core→00GameContract→00Tools→01Manager→02Data→02Game→04UI→08Map→Effect/Feature/Rendering），上层可引下层，反之禁止
- asmdef 实测 10 个（`00Attribute/00_Attribute`、`00GameContract/01_GameContract`、`00Core/00_Core`、`08Map/08_Map`=FpsGame.MapUtils、`DayNightSystem/`、`00Tools/Test/00_Utils`←**TerrainUtils 在这里**、`04UI/Assembly/04_UI`、`04UI/WndTool/00_WndTools`、`Effect/EffectComp/05_EffectComp`、`Plugins/NavMeshComponents/Scripts/`）；`01Manager/`、`02Data/`、`02Game/`、`04UI/` 根脚本、`Effect/*.cs`、`Rendering/*.cs` 在 **Assembly-CSharp**；asmdef 均 `autoReferenced:true` → Assembly-CSharp **可**调它们，反向不行
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
  - `KaiserWave` 每 `360f/creats.Count` 秒建 PhoenixEagle+6 单位（±2 网格，大型单位单独实例化）；`CampTemplate.patrolTemplate`=`List<SKVP<string,int>>`；`EnemyController.PatrolPos` 是**到达即自毁**哨兵点（`HomePoint` 才是到达待命），巡逻队只能 `BattleManager.CreatPatrol(Vector3)`

## 场景加载与事件时序（易踩）
- `ResSvc.AsyncLoadScene(name, cb, ...)` 用 `LoadSceneMode.Single`，且 `completed` 只置标记，**回调被 `DelayedInvoke` 延后一帧**（"让场景中所有 Awake/Start 先完成"）→ **场景内组件的 `Start` 一定早于回调里 `new GameObject().AddComponent<BattleManager>()`**
- ⇒ 凡是"在场景物体 Start 里只抛一次"的初始事件（如 `DayNightBrain.Start`→`DayNightTriggerModule.Initialize`→`GlobalEventSub.DaySwitch`），**BattleManager 永远收不到开局那一次**。修法：事件总线缓存最后值（`GlobalEventSub.LastDaySwitchIsNoon`），BattleManager 在依赖组件就绪后补发（`ApplyInitDaySwitch()` 在 `ADCont.Init()+CacheReinforceAd()` 之后调）
- ⚠⚠ **补发的坑：`authorizeCounter` 是裸计数、无下限**（`_Authorize` 里 `+= state?1:-1`，`IsAuthorize => !cfg.authorize || counter > 0`；`ADSO_Y_Lamp`(ID16)/`ADSO_Y_Llluminator`(ID17) 都是 `authorize:1`）⇒ **"白天"= 计数 0 的自然状态，绝不能在白天做 `-1`**：开局白天若照搬状态补发一次就变 -1，之后入夜 +1 只回到 0 ⇒ 这两个战备**永久锁死**（旧代码只是"恰好漏掉开局事件"才没暴露）。正确写法：`ApplyDaySwitch()` 只在"入夜 +1 / 从夜晚回白天 -1"这种**翻转**时动计数（`_nightAuthorized` 标志去重），白天什么都不做
- 同理：订阅方在 `Awake` 订阅的场景组件（`NightLight`）没问题；在 `Start`/回调之后才创建的（`BattleManager`、战备 UI）必须"补初始状态"
- `BattleManager._initQueue`（`EnqueueInit`）给"场景里早于 BattleManager 存在的物体"排队，`Init/InitSpecial` 末尾 `DrainInitQueue()` 兑现；`Authorize` 之类依赖 `ADCont` 的调用前必须判空

## 组件存活/回收（易踩）
- `VFXManager.Release(GameObject)` 只认根物体上的 `ParticleSystem`(Stop)/`LimitedLife`(置 `allowRelease`)，皆无=空转；`ProjectileBase` 走另一重载（回池+`Template`）
- `LimitedLife.IsAlive()` 只被 VFXManager 池 Update 轮询 → 非池化实例（含 Nest 预置）不会被回收；`HealthOther.AutoDestroy` 才是"死亡即销毁"
- `LimitedLife` **延时回收** `EndDelay`：到寿先 `InvokeEnd()`（`endInvoked` 保证一次），再等 EndDelay 秒；`allowRelease` 强制回收**不走延时**；`AllowPreRelease` 阈值 `+EndDelay`；`OnShow`/`ResetLift`/延长寿命都复位 `endInvoked`。⚠ `SubtitleAirdrop.Update` 也用 `IsAlive()`
- 池化对象"本次状态"字段必须在 OnDisable/OnEnable 复位，且放在早退 return 之前

## 地形/植被/破坏
- 地形数据 `Assets/Art/TerrainData/MainMap.asset`（**二进制**读不了）；地形**每局运行时重建**：`BattleManager.InitTerrain`(184-232) 先按 `taskCfg` 改 `heightmapResolution=MapRes+1`/`alphamapResolution=MapRes`(512/1024)/`size=(MapSize,MapHeight,MapSize)` → `MapRoot.Init(true)` → `SetTextures` → `ApplyFractalNoiseToTerrain`。**→ 场景里预摆的物体 Y 坐标每局都失效**
- `GenerateNoiseTerrain` 内部顺序：基础地形→后处理→材质→分块提交高度图(128)→分块提交 alphamap→`SpawnVegetation`(石块/树)→`SetTreeInstances`→`TreeDestructor.Rebuild`→`TerrainDetailEraser.Rebuild`→`SpawnDetails`(草)→NavMesh。要插"落位/撒物"放在 660-710（NavMesh 之前）
- 贴图注入 `SetTextures`，层索引 0 草/1 沙/3 巢穴(`CampData_SO.NestTerrainItem` 覆盖)/4 岩；实测 terrainLayers 6 层（Desert_Crater/Plains/Hollow/Nest/Slopes/City_Hollow）
- **Tree Prototypes 索引约定：0-6=石块、7-10=真树**（`Art/Nature/Rocks/Lod/` 的 7 个 LOD 石 + `Pure Poly` 的 4 棵树）；`TerrainPresetData` 用 `rockSpawn`/`treeSpawn` 两份 `VegetationSpawnData`（`probability/prototypeRange(含头含尾)/minSlope/maxSlope/minHeight/maxHeight`）分别生成。概率（石/树）：沙漠 .0012/.0002、高原 .0015/.0008、雨林 .0008/.003、丘陵 .0025/.0015、盆地 .002/.001、平原 .0012/.0006、山地 .0025/.0003
- `SpawnVegetation`：先 `Random.value >= rate` 早退，再查高度/坡度；`probability`=**每格（高度图索引）**概率，格边长=`MapSize/mapRes`；`widthScale/heightScale=Random.Range(0.7,1.5)`，**`tree.rotation`/`tree.color` 没设** → 石头朝向全一致；旧算法 `FloorToInt(prob×res²/10000)` 已废弃
- 覆盖模式：`_overridePreset` 勾上后 `_rockProbability`/`treeProbability` 生效，**<0 = 沿用预设**（默认 -1）；**地图级倍率** `MapData_SO.TreeSpawnMultiplier`/`RockCoverMultiplier`（默认 1、0=不长树/不放悬崖）→ `InitTerrain` 传 `ApplyFractalNoiseToTerrain(type, treeMul, rockMul)`，**只作用于树密度与悬崖数量**（石块/草不吃），ContextMenu 路径 = 1
- ⚠ **碰撞体/隐形墙定案**：草永远无碰撞体；树/石碰撞体 = 原型 prefab 上的 **CapsuleCollider**（Unity 只认 Capsule，原型上的 Box/Sphere/Mesh **不参与**）+ `TerrainCollider.m_EnableTreeColliders`（全局开关，运行时生成）。实测量到：树原型（`PP_Birch_Tree_06 4` r=5 h=12 c=(0,12,0) 等）经实例 `widthScale/heightScale`(0.7~1.5) 放大 ⇒ **半径 7.4m、从地下 3.75m 到地上 19.8m 的隐形巨胶囊**；它属于 `TerrainCollider`、**不是 GameObject**（场景视图找不到，只能 Physics Debug）。表现=有起跳音效却跳不起来 / 子弹打到看不见的墙；`BaseSelfMoveableController.HaveObstacle()` 只查"当前胶囊是否被穿透"、**不做前探** ⇒ 不报"有障碍物"。诊断=从"脚底+1.8m"向上射线，命中 `TerrainCollider` 且命中点远高于地表 ⇒ 必是树/石碰撞体。`ProjectileStandard.cs:192-193` 打 debug **查不出**（`collider.name` 永远 "Terrain"）；`SphereCastAll` 起点已在碰撞体内时会**漏报该碰撞体**。修复=删掉原型上除树干胶囊外的所有 Collider。判断树碰撞体有无请用物理探测，**别用 `SerializedObject` 读 `m_EnableTreeColliders`（读数不可信）**
- ⚠⚠ **树碰撞体不随 `SetTreeInstances` 更新**（旧位置仍挡、新位置没碰撞体，`terrain.Flush()` 无效）：必须显式 `TerrainUtils.RebuildTreeColliders(terrain)`（切一次 `enabled`，唯一廉价有效手段，代价=重建整张地形碰撞体，别频繁调）→ 已接入 `TerrainClearer`（`CommitVersion` 版本号门控 + 同帧去重 + `ColliderSyncInterval=0.5f`）
- 悬崖（地形覆盖石）落点必须**按网格最低点对齐**：`Instantiate` 后量 `GetRendererMinY(go) - go.transform.position.y`，再把 pivot 放到 `地表 - sinkDepth - 该偏移`（项目 CliffA/C/E 网格在 pivot 之上 8~25m，按 pivot 摆就浮空）。`TerrainUtils.GetInterpolatedHeight(u,v)+origin.y` == `SampleHeight+origin.y`（差 0.00）
- `TerrainData` **无 `RemoveTreeInstance`**；只能 `SetTreeInstance(s)`（不可改 `position`/`prototypeIndex`）；`TreeInstance.rotation`（X-Z 弧度）可正常赋值
- 树销毁 `08Map/TreeDestructor.cs`：索引→世界坐标表 + 圆柱射线 + 按帧合并提交（限流 4/s）；销毁=实例 scale 置 0（索引不变、可 `RestoreAll`），`SetTreeInstances(arr,false)`（**必须 false**）+`terrain.Flush()`。两套入队语义：① **摧毁** `DestroyInRadius` 走"可被摧毁"白名单（空列表=全放行）② **清除** `ClearInRadius`/`ClearInRectXZ` 按原型区间（**忽略白名单**，-1=不限）。白名单配置落点 `GenerateNoiseTerrain._destructibleTreePrototypes`
- 细节（草/花）擦除 `08Map/TerrainDetailEraser.cs`：同款"入队+按帧合并提交(限流 8/s)"，`Rebuild(Terrain)` 自动挂载；逐层 `GetDetailLayer→清零→SetDetailLayer`（rect 先 Clamp；**Terrain.position 是角点**）
- **地表物清除统一门面 `08Map/TerrainClearer.cs`**（静态类 + `[Flags] TerrainClearTarget{Tree,Rock,Vegetation,Detail,VegetationAndDetail,Cover,All}`）：`ClearInRadius/ClearInRectXZ`（清除语义）/`DestroyInRadius`（摧毁语义，爆炸用）/`MarkTerrainChanged()`/`Flush()`（立刻处理跳过限流）/`Rebuild(terrain,destructibleProtos)`（⚠ 含 `RockCoverDestructor.Rebuild`=ClearAll，**只能在放置覆盖物之前调**，故 `GenerateNoiseTerrain` 仍分别调三个 Rebuild）。请求只入队 → 帧末隐藏驱动器（`[RuntimeInitializeOnLoadMethod]` 自建，零场景改动）统一下发 → 昂贵操作（树/草提交→树碰撞体重建→NavMesh 重烘）按 `RefreshInterval`(0.25s) 合并，**顺序必须先生效再重烘**
- **弹坑/地形破坏链路**：伤害总入口 `FpsHelper.Hit`（`destructe>0` → 擦雪+`ModifyHeightMap(...,refresh:false)`+`MarkTerrainChanged()`）；`ModifyTerrain`（弹坑=圆按 `outerRadius`；附加地形=角点+半尺寸矩形 **+`transitionDistance` 过渡带**；清除范围 `clearVegetationRange`(默认(7,10)=树)+`clearRockRange`(默认(0,6)=石块，-1=不限)——**石块必须一起清**，否则地面挖低后原地石块悬空）挂在 **31 个任务/兴趣点 prefab** 上、传 `refresh:false`（自己不重烘），实战兜底 = `MissionController.InitializeAsync` 末尾 `AsyncRefresh(true)`，但 `CreatMission` 只等 `IsInitialized` **不等 `Modify()` 协程** → 顺序不保证
- 弹坑三件事都必须做（改高度图+清树/石+清草），但**不必这么频繁**。最贵：① `ModifyHeightMap` 的 `SetHeightsDelayLOD`+**`SyncHeightmap`**（生效点）+`ModifyAlphaMap` ② 树实例整表提交 ③ 树碰撞体重建 ④ NavMesh `UpdateNavMesh`。优化顺序：Ⅰ 把"写 patch"与"Sync 生效"拆开由 `TerrainClearer` 一点统一 Ⅱ "最近已清除圆"区域去重 Ⅲ 树/草 0.1~0.25s、碰撞体 0.5s、NavMesh 1s+ 且"上次没烘完不发" Ⅳ NavMesh 阈值化或 `NavMeshObstacle(carve)` Ⅴ 树实例真删 Ⅵ 关 `Enable Tree Colliders` 改"玩家附近独立 Collider 池"
- **实测数据（2026-09-21 桥在 Play 中量）**：`SetTreeInstances(3650)`=0.08ms、`terrain.Flush`=0.69ms、**`RebuildTreeColliders`=10.75ms**、高度图 写+Sync=2~5ms、NavMesh `UpdateNavMesh`=5.6~8.8ms(仅发起)。→ 日志里"树提交 11ms"=0.08+0.69+10.75。版本门控后碰撞体重建 4/s→2/s；`RebuildCollidersOnTerrainChange` 默认 false。计时入口：`TerrainClearer.LogTiming/TimingLogEverySamples/TimingSamples/ResetTimingStats`，高度图链路 `TerrainUtils.LastHeightReadMs/LastHeightWriteMs/HeightModifyCount/AvgHeightReadMs/AvgHeightWriteMs/ResetHeightTimingStats`
- `TerrainUtils.AreaCircles`（纯数据"地表占地圆"，`00Tools/Test/TerrainMainUtils.cs`）：唯一写入者=`08Map/RockCoverDestructor`（`Rebuild/Register/RemoveInRadius/RemoveInRectXZ/Clear`）；生产者=`GenerateNoiseTerrain.PlaceRockCovers`；消费者=`MissionController`（每个任务点现读，不用事件）与 `ModifyTerrain`。换地形自动清空
- `TerrainUtils`（partial static，`Main`=TerrainUtils.Main）：`WSToHeight(pos)`（世界→地形高度，**prefab 落位贴地就用它**）、`WRToHR/WRToAR/ARToHR`、`ModifyHeightMap`、`AdditionTerrain`、`Refresh/AsyncRefresh(refreshNav)`
- 层掩码实测（`GameContract.LayerDefinition`）：子弹 `HittableLayers=73`、高速 `601`、玩家移动 `MoveableLayers=8265`、`UnitSeeLayers=16393`、`AirWallLayers=8192`。Terrain 在 Ground(3)、`preserveTreePrototypeLayers=False`；`Physics.queriesHitTriggers=True`
- **NavMesh**：MapRoot 上 `NavMeshSurface`，实测 `collectObjects=Volume(1)`、`m_UseGeometry=0`(RenderMeshes)、`LayerMask=8`(Ground) → **树/石块（Default 层）都不进 NavMesh**，AI 直接穿过。想让运行时摆的石头挡 AI：放 Ground 层或在生成后再烘一次
- "找不到的隐形碰撞体"三类来源：① 地形树/石碰撞体（非 GameObject）② `MapRoot.CreatAirWall()` 运行时 18 个边界墙 ③ 建筑隐形代理（`OilPlane`/`Turret_Mission_Machine` 的 `PlaneA1..F`/`Spine2`/`StairsA1..C2`/`GunArmor`，Default 层无 Renderer）+ `EntityRoot` 的 `KeyScreen`/2 个无名箱

## 渲染
### URP 时序与自定义 Pass
- URP14 pass 时序：Opaques 300 / BeforeSkybox 350 / AfterSkybox 400（CopyColor 生成 `_CameraOpaqueTexture`）/ BeforeTransparents **450** / AfterTransparents 500 / BeforePostProcessing 550；**同 event 时 Renderer Feature 排在 URP 内置 Pass 之前** → 全屏雾(450) 先于透明 Pass(450) → "队列 Transparent 的物体永远不吃雾"
- 背景层物体（`Environment/SkyboxLayer`：星星/月亮/云，尺度 13000~15000，靠 `ZClip Off` 越过远裁剪面）：队列 <2500 时在不透明 Pass 画 → 被天空盒整片擦掉；队列 Transparent 时能看到但不吃雾
- **解法（已落地）**：`SkyboxLayer.shader` Pass 改自定义 `LightMode = "SkyboxLayerBeforeFog"`，`Rendering/SkyboxLayerBeforeFogRendererFeature.cs` 在 **445** 单独绘制并注册。要点：① pass 必须 `CoreUtils.SetRenderTarget(cmd, colorHandle, depthHandle)` 同时绑深度，否则整片盖住地面 ② FilteringSettings 用 `RenderQueueRange.transparent` + `SortingCriteria.CommonTransparent`
- 同款先例 `WarpingBeforeFogRendererFeature`(445)：**自定义 LightMode 的代价 = 特性被禁用/移除后该特效完全不显示**（无默认 Pass 兜底）
- URP 资产：`Assets/Setting/New Universal Render Pipeline Asset.asset` → `..._Renderer.asset`；`m_RequireDepthTexture/OpaqueTexture=1`；已挂 5 个 Feature：Outline(300)/Snow(300)/SkyboxLayerBeforeFog(445)/WarpingBeforeFog(445)/FullScreenFog(450)
- ⚠ 手改 renderer 资产别动 `m_RendererFeatureMap`；用 `SerializedObject`+`AddObjectToAsset` 加 Feature 后调 `ValidateRendererFeatures()`（反射）自动重写 map

### URP / ToonLit
- 附加光阴影需同时：变体带 `_ADDITIONAL_LIGHT_SHADOWS` + 3 参 `GetAdditionalLight(i,posWS,shadowMask)`；`LIGHT_LOOP_BEGIN` 前先声明 `InputData inputData`
- URP Asset：附加光 Realtime、`m_AdditionalLightShadowsSupported:1`、`m_ShadowDistance:100`、级联 3；URP14 `USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA` 硬编码 0
- 未写进 CBUFFER 的属性=死属性；新增 CBUFFER 字段须同步 Properties；toggle 局部关键字名=**原名**（无 `_ON` 后缀）
- `_ShadowMapColor`=暗面里该光源的乘数；`_MAIN_LIGHT_SHADOWS` 管接收；**材质不投影首因**=`disabledShaderPasses: - SHADOWCASTER`；UI 相机勾 Clear Depth 会致贴花消失
- ToonLit 5 pass（ForwardLit/Outline/ShadowCaster/DepthOnly/DepthNormalsOnly）**共用 `ToonLit_Shared.hlsl`**（被 7 个 shader 共用：`ToonLit`/`_Colour`/`_Face`/`_Hair`/`_MouthEye`/`_Scene`/`_Stone`）；`GetFinalBaseColor`(376) 是 albedo 唯一入口；透明完全靠 `clip()`
- 给 shared 加东西三条边界：① 宏只写在**目标 shader 自己的 `HLSLINCLUDE`** ② shared 里只**新增** `#ifdef` 块 ③ 新数据只走**全局 uniform**，不进 Properties/CBUFFER，也别动 `Varyings`；**宏要按 pass 精确控制**（`GetFinalBaseColor` 被 5 个 pass 调用，只有 ForwardLit/Outline 用 albedo）
- `ToonLit_Stone.shader`（guid `0032fd96…`）`Cull Back` 硬编码、`_BaseMap`+`_BlendingMap` 双图混合
- **让材质纹理自动跟随数据 = "脚本注入 + 保留 shader"**：① MPB per-renderer ② `Shader.SetGlobalTexture` 全局纹理 + `#define` 支路（推荐，全局纹理**不可写进 Properties**）③ 真随脚下地形层变才改 shader。**已实施**：`ToonLit_Stone` ForwardLit/Outline + shared 两处 `#ifdef` + `GenerateNoiseTerrain.SetTextures` 里 `SetGlobalTexture("_TerrainBaseTex"/"_TerrainBlendingTex")` → 石头贴图自动取 `MapData_SO.TerrainItem[0]/[1]`。⚠ 材质贴图槽位对石头不再生效
- 从 Properties 删**被代码读取**的属性 = CBUFFER 取 0（`_BaseColor`/`_BaseScale`/`_ShadowMapColor`/`_FogMaxValue`）；`_BaseMap_ST`/`_BlendingMap_ST` 仍被 uv 使用不能删；`ToonLit.shader`(main) 挂 `CustomEditor "ToonLitMainShaderGUI"`（`ToonLitShaderGUI.cs:325` 的 `FindProperty` 不判空）→ 别在 main 上删属性
- `FakeAreaLight.shader` 菲涅尔：`_FresnelScale` 符号定方向、绝对值定强度

### 屏幕空间贴花（Assets/Shader/Decal/，源自 NiloCat）
- `SimpleDecal`(fog 恒开)/`SimpleDecal_Colour`；cube 罩目标+`ZTest Off`/`ZWrite off`/`Queue Transparent-499`
- ⚠ `_Cull` 必须 Front(1) 或 Off(0)，绝不能 Back(2)；⚠ 雾与混合模式必须匹配（加法混合会在纹理全黑处叠雾色），已加 `[Toggle(_DecalAdditiveFog)]`；⚠ 预乘 `col.rgb *= col.a` + 硬件 SrcAlpha = alpha²

### 自定义 UI Shader
- `Mask` 走模板缓冲；`RectMask2D` 走 `CanvasRenderer.EnableRectClipping`+`_ClipRect`
- 必须自带 `#pragma multi_compile_local _ UNITY_UI_CLIP_RECT`、`float4 _ClipRect`（**不写进 Properties**）、顶点局部坐标传片元、`UnityGet2DClipping`。常见失效：Graphic 未勾 Maskable；图标在子 Canvas 下

### 积雪
- `SnowController`(`_SnowEnabled`/`_GlobalSnowAmount`/`_SnowMask`+`_SnowMaskRect`/`_SnowMaskTiles`) + RF(300) + `SnowVolume`；挂钩 `FpsHelper.Hit`、`BattleManager.InitTerrain`（换局 `ResetMask`）
- **积雪不是后处理，是"用雪材质重画一遍"**（`DrawRenderers`+overrideMaterial+`RenderQueueRange.all`）：原材质 clip 全透明也会留**幽灵轮廓**
- 层配置：`snowEntries[0]` mask=65→`SnowOverlay_Unit.mat`，`snowEntries[1]` mask=8(Ground)→`SnowOverlay_Ground.mat`；**Default 层默认就积雪**

## 任务系统
- 抽象链 `MissionBase : TickBehaviour`（Tick 1s；`UpdateText/UpdateTip/UpdateMission/CompleteMission/FailMission/EndMission/Uninit/Link/Activation`；`Uninit` 由 `EndMission` 或 `OnDestroy(!end)` 调）→ `MissionEvacuateBase` → 静态(终端 KeyScreen)/动态(凯伊 ReturnBag)；`UseSceneStartPoint` 虚开关控快速模式是否顶替 StartPoint（Mobile 覆写 false）
- `MedivacController`：`Land`=插入下机；`Evacuate`=撤离接人；`TakeOff()`（Play"Evacuate"+开 cam+派发 `Complete`+**只隐藏 IsInBox 玩家**）/`IsInBox`/`ForceTakeOff()`
- 「创建后」走 `InitMission`，「全部任务初始化后」走 `StartMission`；`Link(mission)` 订阅 `mission.OnMissionEnd += Activation`；子任务 `OnMissionCompleted` 汇总给父任务
- 主任务 `MissionCompleteKeySceern`（类名拼写如此）=终端流程；`MissionOilRefining`：Init 空投平台(id15)/连接点(id14)→Wait 等 `MissionSubConnectPipes`→Start(Load 180s+`errorTimes` 波次)→Repair→End
- 管道状态是 `Furniture_Pipe.Id` 字符串：`Pipe`→`PipeLink`→`PipeWait`→`PipeComplete`/`PipeError`；**没有"已修复"标志位**；`Operate()` 里 `base.Operate()` 先、`Complete()` 后 → 不能在 `OnOperate` 回调里读 Id 判完成，要 Tick 轮询
- 台词只能 `WndManager.CreatNotice(角色, groupName)`，**角色键=NoticeTree_SO.ID**（Ayane/Yuuka/Kai），groupName 必须真实存在

## 战备系统
- 资产 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`；运行时 `ResSvc.airdropDic`
- `AirdropData_SO`：`type`(Red0轰炸/Blue1装备/Greed2炮台/Orange3载具/Yellow4补给)、`labels`(`[Flags]`)、`opter`（Left0/Up1/Right2/Down3，LE uint 数组）、`subAirdrop`、`creatObect`、`coolGroup`、`isHide`
- 按键序列：首键严格 →→轨道轰炸/↑鹰与空中支援/↓炮台地雷装备补给/←载具；尾键(伤害类型指纹)规律基本未贯彻
- ⚠ `labels` 是后加字段：除 `ADSO_R_Railgun` 外资产 YAML 无该行（=0）；⚠ `AirdropData_SOEditor` 显式列字段，漏列不显示
- UI：`AirdropWnd`(HUD)、`AirdropConfigWnd`、`ArmamentWnd`；`ArmamentButton.prefab` 子1=偏好标记；`ArchivesData_SO` 的 `AirdropBuyDic`/`AirdropPreferList`
- 部署链路：`VFXAirdropEffect` 按 `deliveryType` 分支（Pod 用 `Instantiate(creatObect)`；`ImpactVfx` 由 `FpsHelper.Hit` 走 `VFXManager.Creat`+`SetOwner`）；改 `arriveTime` 须同步 `time`；`permanentPod` 会 `LimitedLife.ResetLift(9999)`
- **授权**：`AirdropController.Authorize(id,state)` → `authorizeCounter += state?1:-1`（**0=隐藏且不可用**，`IsAuthorize=counter>0`）；任务必需战备 `TaskManager.RequiredAD = {SupplyId, HealBag, IlluminatorId(17), LampTowerId(16)}`
- **昼夜解锁照明战备**：`BattleManager.OnDatSwitch(isNoon)` 暂存 `_dayIsNoon` → `ApplyDaySwitch()` → `Authorize(LampTowerId/IlluminatorId, isNight)`，**只在"入夜/回白天"翻转时动计数**（白天不做减法，见"场景加载与事件时序"的计数坑）。开局那次事件会漏 → `GlobalEventSub.LastDaySwitchIsNoon` 缓存 + `ADCont` 就绪后 `ApplyInitDaySwitch()` 补发
- ⚠ 本仓库植被/岩石素材包基本**未被游戏场景引用**（只在各自 Demo/TestScene 用）；`Rock1LOD_grup*.prefab` 在 Lod/ 下才被地形原型引用

## 敌人特效（02Game/AI/FxCont）
- `EnemyControllerFX`（抽象 partial）+`EnemyFXControllerUnit`/`BuildingFXController`；Start 订阅 `I_AIController` 事件、OnDestroy 退订；`TriggerFX(type,pos,rot,parent,ignoreAudio)` 是统一入口
- 三层配置：渲染模板 SO `EnemyFxData_SO.rendererSet`（按 `sharedMaterials[i]==mat` 匹配槽位）/事件 SO `EnemyFxEventData_SO.fxDic`/组件 `fxMaterial`/`BirthMaterial`/`Animator`；SO 不存实例→`EVT_*.go` 全空；`_HitColor` 为自发光叠加
- **MPB 所有权在"渲染槽位"**（`RendererSlot{Renderer,MaterialIndex,mpb,dirty}`）：条目只 SetColor，帧末 `UpdateRS()` 统一 Flush；同槽位只能一块；收尾必须写回"无效果值"

## 其他功能
- 伤害总入口 `FpsHelper.Hit(ProjectileHitData)`：直击→爆炸(`BattleManager.FindUnits`/`GetExplosionDamage`/穿甲/拆毁)→冲击波→**地形破坏**→警告/特效/音效/弹痕
- 地雷 `02Game/Gameplay/Projectile/DeployableMine.cs`（`Mine`/`RobotMine`/`spore` 三个 prefab 用）：`Actor`(队伍)/`Health`(被摧毁→`OnDie`)/`LimitedLife`(到寿) 三件套；触发链路 = 自动命中(`TryHit`→`FindUnits`+`InTriggerRange` 按"水平间隙+竖直间隙"合成 3D 距离)/被摧毁/外部直接调**幂等的** `TriggerExplosion()` → `OnTriggered`(UnityEvent「触发爆炸时」) → `ExplosionDelay` 秒协程 → `DoExplosion()`(伤害 → `OnExploded`(UnityEvent「爆炸时」，**在 `VFXManager.Release` 之前**派发) → 回池)；`LimitedLife` 到寿时"已触发则停协程立即引爆、未触发才 `Tool.Destroy`"。⚠ prefab 里没有 `m_MethodName: Explosion` 之类回调，改名/拆方法安全；新序列化字段缺 YAML 行时用 C# 初始化器默认值（`ExplosionDelay=0`=旧的"触发即爆"）
- 天气：`WeatherSystem`（开局按 `mapCfg.WeatherInfos` 用 `BattleRandom` 抽）+`WeatherEffect` 抽象（Rain/Desert/Snow），`Resources.LoadAll<WeatherEffect>("Prefabs/Weather")`；预制体默认值须中性
- 昼夜：`DayNightBrain` + Modules（`TimeProgressionModule`/`CelestialRotationModule`/`CelestialVisualsModule`/`EnvironmentLightingModule`/`DayNightTriggerModule`）；预制体 `Assets/Art/Prefabs/Day-Night-Manager.prefab`，**Utnapishitim（桥）与 TestScene（战斗）各有一个实例**；天空盒运行时建副本写值（`GetSkyboxForWrite`），别写材质资产。旧 `Effect/DayNightCycle.cs` 已不参与玩法场景
- 寻路：`PathRequestManager` 禁止 `pathPending` 期间"超时重试"，只在 `pathPending=false` 后判 `PathInvalid/PathPartial` 并投影重试+10m 兜底；log 由 `EnemyController.SetNavDestination(isImportant)` 控
- 音频：`AudioManaqerBase.sourcePool` 工厂须 `SetActive(false)`；NPC 语音家具 `Furniture_NPCChat` 用 `SoundGroup_SO`+协程等 `AudioSource.isPlaying`
- `PlayerWeaponsManager.OnWeaponSwitched`：`isSec=true` 时只设左手 IK，**不要**覆盖主武器右手 IK；`PhoenixEagleController` 旋转乱跳=阶段切换别改 `lastPos.y`
- 项目全景：伤害模型（弱点/护甲/护盾/抗性）、属性、武器、投射物、玩家（视角/载具/喷气背包/护盾）、AI（状态机/BOSS）、波次、`UnitQueryGrid`、噪声地形、任务、昼夜+夜间敌袭、战备、战略大地图、撤离、购买与偏好
- 改进优先级：① 随机源统一 `BattleRandom` ② `I_Damagable`/`I_Entity` 解耦为纯 DTO ③ 统一命名空间+rootNamespace ④ God Class 审查 ⑤ `UnitQueryGrid` List 池化

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
- `[DisplayField]`：编辑期不画、运行期只读；只对已序列化字段生效；白名单 Integer/Float/Boolean/String/ObjectReference/Color/Vector2/Vector3
- 数据编辑器：`Editor/DataEditorWindow.cs`+`DataTabs/DataTabModule<T>`；SO 加字段且带专属 Editor 须补 `DrawField("新字段")`
- `Assets/Editor/MaterialUsageFinder.cs`（`Tools/材质引用查询`）：`GetDependencies(path,false)` 建「材质/模型→引用者」表（5865 资产 ~2s），再定位 `Renderer.sharedMaterials[i]`/`Terrain.materialTemplate`/`Graphic.m_Material`/任意序列化字段，沿表 BFS 出预制体变体（只收 `PrefabAssetType.Variant`）
- ⚠ `DisplayProgressBar` 会派发编辑器事件 → 长任务窗口须加重入锁 `_busy`；⚠ **EditorWindow 私有字段会被 Unity 存档并在域重载后恢复**，UI 开关应在 OnEnable 复位
- asmdef 限制：`Editor/Tool/EditorTools.asmdef` 的 `references: []` → 看不到 `UnityEngine.UI`；要用 UGUI/URP/项目类型就放 `Assets/Editor/`

## UI 图片/配色词汇表（Assets/Images/，参照 SelectRoleWnd.prefab）
- 按钮底 `FX_TEX_Lock.png`（Sliced，ppu=2，亮青 0.65/0.87/0.87）；锁定框 `FX_TEX_Lock_Frame.png`；列表条目/中面板 `frame_panel13.png`（Sliced）：未选中 (0.84,1,1,0.40)(RoleButton)/(0.88,0.96,1,0.40)(WeaponPreviewItem)，选中 (1,0.97,0.84,0.40)(SelectBtn)
- 大面板框 `SelectFrame3/4.png`（Sliced，ppu=0.5，(0.67,0.96,1,0.70)）；气泡 `SelectFrame2.png`；小图标框 `UI_Frame4.png`；图标底 `Common_Main_SkillBG.png`；六边形 `Hexagonal_Frame(2).png`
- 进度/连接条 `LinkBar.png`(Filled)；细边框 `frame5/6.png`；箭头 `Arrow_Left/Right/Left2/Right2/Down.png`；全屏底 `99997.png`；纯色深底惯例：无 sprite+(0,0,0,0.2~0.55)

## MCP for Unity
- 本地嵌入包 `Packages/com.coplaydev.unity-mcp`（已入库含汉化）；手册=skill `unity-mcp/`；**写操作前先 git commit**
- 本机双实例：`Bluedivers@3d9f2357`(port 6400, `E:/Bluedivers/Assets`) / `RTSClient@6365de15`(6401, `D:/Project/RTSClient`)。实例 hash **随项目路径变化**，用时先读 `mcpforunity://instances`；活动实例只存内存，重开会话必须重设 `set_active_instance`；**写操作前核对 `projectRoot`**
- 客户端配置两份互不相通：IDE 读 `%USERPROFILE%\.codebuddy\mcp.json`；Unity 窗口 Configure 只写 CLI 的 `%USERPROFILE%\.codebuddy.json`；Unity 侧 Transport 必须 Stdio
- ⚠ 会话里看不到 `mcp__`/`mcpforunity__` 工具时**不要假装能查编辑器**：只能读文件/资产（二进制 TerrainData、prefab 真实数值拿不到），要如实说"未证实"
- `execute_code` 只有 CodeDom(C#6)：不能写现代语法；`set_active_instance` 后 `refresh_unity` + 轮询 `mcpforunity://editor/state` 的 `advice.ready_for_tools`
- 改材质关键字/属性后 `EditorUtility.SetDirty`+`AssetDatabase.SaveAssets`；⚠ `Editor/Tools/Build/*.cs` 曾被 .gitignore 误清→编译失败(MCP 全挂)，升级包前先 `git add -f` 汉化
- 改完脚本：`validate_script`(level=standard) → `refresh_unity` → 桥短暂失联属域重载，等 ~10s 再 `read_console`（`types` 传**数组**）；⚠ `refresh_unity` 可能检测不到 .cs 变更，兜底 `AssetDatabase.Refresh(ForceUpdate)`+`CompilationPipeline.RequestScriptCompilation()`，~20-25s 后反射确认
- ⚠ 新 `.cs` 文件**不会**被 `refresh_unity` 导入；编辑器里 `AddComponent` 不调 `Awake`（`Instance` 为 null）

## 协作偏好与通用坑
- 不确定时先询问；不主动纠结/移除 using；文件夹/文件改名等结构性修改由用户**手动**做，AI 只给方案
- 用户常连问"某功能能不能做/为什么没有"→先给**结论 + 证据（文件:行）**，再给选项与代价，最后才谈落地；不要反问他想要的方案
- 上游包文件丢失排查：`git ls-files` 与镜像目录清单比对；Unity 报"meta 存在但文件夹不存在"常意味文件被忽略/删除
- 双机/换机后第一件事：确认 MCP 连的是当前副本再动写操作
- 编辑器特性失效先问「谁在画这个 Inspector」：专属 `[CustomEditor]` 会顶掉全局 fallback
- 资产 YAML 中**整行缺失的字段**=脚本新增后资产尚未重新保存；⚠ 已澄清：Unity **保留 C# 字段初始化器里的值** → 新增带非 0 默认值的序列化字段**安全、无需批量改资产**
- 诊断"某组件没生效"先看**是不是压根没人调它**（`LimitedLife` 只被池轮询、`VFXManager.Release` 只对池化/粒子对象生效）
- 排查视觉问题先看**实际材质/资产属性值**，别只看 shader 默认值；也要先确认物体落在哪个 Pass/队列
- 需要"新增交互家具"时：新建 `Furniture_Attached` 子类（**不改** Furniture_General/AttachedGeneral 的静态字典）→override ShowName/Id/Desc/Icon；可 AddComponent 运行时补挂
- ⚠ **别什么都往 `BattleEventSub` 塞**（用户明确要求）：一方已能拿到另一方实例时就订阅**实例事件**；全局事件层只留给真广播；跨实例状态也别用 `static`。**例外**：需要"记住最后一次值给晚订阅者"的初始状态广播（如 `GlobalEventSub.LastDaySwitchIsNoon`）正是全局事件层的合理用法
- 新增脚本尽量用**运行时自动挂载/自建组件**接入（如 `TreeDestructor.Rebuild` 内部 AddComponent），避免改场景/prefab YAML；需要手改 prefab 时明确告知用户去点；改 URP renderer 资产可走 MCP `SerializedObject`
