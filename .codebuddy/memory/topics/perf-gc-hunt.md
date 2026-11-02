# 性能线：GC 分配 / 帧率优化（会话交接 · 2026-11-02）

## 一、曲线（实测，用户 Profiler 截图 + MCP 反射）

| 阶段 | 全帧 GC | CPU |
|---|---|---|
| 起点（交战 20~50fps） | 157 KB | 18.19 ms |
| 修天空盒 + 地雷 + 巡逻调用点后 | 113 KB | — |
| 再修老接口 / 装箱后 | 85.6 KB | — |
| 再修子弹命中链路后 | 9.5 KB | — |
| 最终 | **~4 KB**（4.2+2.9+1.2） | **14.62 ms** |

`DoRenderLoop_Internal` 5.43 → **3.12 ms**（区块剔除生效）。

## 二、已修且已验证

1. **天空盒每帧重建（250KB→0）**：`EnvironmentLightingModule.GetSkyboxForWrite()` 脏检查改成
   `if (_skyboxInstance != null && source == _skyboxInstance) return _skyboxInstance;`，
   `_skyboxSourceAsset` 只记真资产。旧写法每帧 `new Material` + 名字无限追加 `" (Runtime)"`
   ⇒ 每帧 GC ≈ `4 × name.Length`，**随游玩时长线性恶化**（用户后来确认真实好了）。
   顺带消失的还有编辑器侧 `MaterialEditor.ApplyMaterialPropertyDrawersFromNative()` 19.7KB。

## 三、已修、编译 0 error、**待 Play 验证**

2. **地雷**（88 颗 × 1.3KB/帧）：`HitCheckInterval=0.1f` 降到 10Hz + 缓存委托 `m_Filter` + 静态 `HitBuffer` + 按 `GetInstanceID()%10` 打散相位（否则同批雷同一帧一起查）。
3. **Damageable 流血**：缓存 `_bleedGroup`/`_bleedGroups`（`SKVP` 是 class，原来每拍 new）。前提：`Health.TakeDamage` 只在调用期间读 `Groups`。
4. **零分配范围查询**：`UnitQueryGrid.QueryUnits(range|targetCfg, cfg, filter, List<IActor> result)` + `UnitQuery.FindUnits(...)` + `IUnitQuerySink`（只有 `BattleManager` 实现）。内部复用 `_overlapsUnits`/`_unitsInRange`；类型过滤抽成共享的 `AddUnits()`。
   ⚠ **新重载不能嵌套**（`customFilter` 里再查会覆盖共享草稿缓冲），老接口不受影响。
   已迁移调用点：`DetectionModule.CheckNewTarget`、`FpsHelper.Hit` 爆炸分支、`HitAreaKinetic`、`HolyWaterEffect`、`ProjectileBase.FindCatapultTarget`、`GuidedShelling.SearchEnemy`、`WeaponController.GetLockActor`。
5. **`Enum.HasFlag` 装箱系列**（单帧上千次 24B 分配）：`GameAttribute.HasFlag(AttrTag)`（`FinalValue` 每次读都调）、`GameCurrentAttribute.CurrValue` setter、`allowModifier.HasFlag`、`AttrInfo.HasFlag`、`WeaponController.HasFlag(WeaponFlag)`、`Furniture_Attached.HaveFlag`、`Actor.HasFlag(ActorFlag)`、`ProjectileBase.HasBulletFlag()`（子弹命中 13 处）、`UnitQueryGrid`/`FpsHelper.IsTarget` 的 `ActorState`。
   ⚠ 位运算必须写 `(a & b) == b`，**不能写 `!= 0`**（对组合掩码不等价）。
6. **Animator 裁剪**：场景 349 个 Animator 里 **346 个是 `AlwaysAnimate`**（Unity 默认），常驻 **200 个在视野外**仍全量求值。`Actor.Awake → InitAnimatorCulling()` 改成 `CullUpdateTransforms`（离屏跳 transform 写入，状态机/动画事件照跑）。⚠ 不能用 `CullCompletely`。
7. **子弹命中链路**（曾 51.8KB/帧，占 BehaviourUpdate 60%）：`SphereCastAll`→`SphereCastNonAlloc` + 静态 `RaycastHit[32]`；`GetComponent<IDamageable>()` 只在**穿透单位**时调（接口 GetComponent 每次都分配）；`BulletFlag.HasFlag` 全改 `HasBulletFlag`；`Hit()` 的 `BulletLog` 加开关守卫。
8. **`NetSyncLog` 调用点守卫**：插值字符串在**调用点**就分配，子开关挡不住 ⇒ 一律 `if (NetSyncLog.Enabled && NetSyncLog.Xxx)`。已改 `ProjectileStandard`（3 处）、`ProjectileBase`、`WeaponBaseController.SpawnVisualBullet`。
9. **UI/HUD**：`PlayerWnd.UpdateTime`（`string.Format`→复用 `char[8]`）、弹药 5 次 `SetText(int)`→值不变不写、`WeaponUIComponent` 热量文本、`Furniture_Door` 去 LINQ、`AirdropWnd` 去 `FindAll`、`InterestPoint` 去 `Min(lambda)`。
10. **`UnitQueryGrid`**：`_nodeRanges` 缓存格子 rect 装箱（`Overlaps(IPERange)` 只吃接口）；`UpdateNodes` 用 x/y 手写比较替代 `SequenceEqual`（两个 List 枚举器装箱 + 结构体反射 Equals）。

## 四、新增两个手动挂载组件（`Assets/Scripts/06Gameplay/Perf/`）

- **`CameraCullDistance`** → 挂 Main Camera。按 layer 设 `Camera.layerCullDistances`（引擎侧零成本）。默认 `Default=180m`、`Unit=250m`，没列出的层按 far（Ground 大地/悬崖照看）。⚠ 数组里 **0 = 该层完全不渲染**，必须先整表填 far。
- **`RenderChunkCuller`** → 挂常驻物体。32m 区块 + **三档**（`Active` 渲染+投影 / `Ready` 渲染但关投影 / `Lazy` forceRenderingOff）+ **双边界滞回**（`OverBoundary`，默认 10m，防边界闪烁）。只切 `forceRenderingOff`，**不关 GameObject、不动 enabled**。包围半径 > 32m 的物体进常驻表永不剔除（场景里有半径 25240 的天空球、7071 的云层）。排除 Ground/Unit 层与 `Actor` 底下的单位。分帧重建（`RebuildBatchSize=256`），`BeginRebuild` 前先 `RestoreAll()`。

## 五、场景实测规模（BattleScene 战斗中）

`287 Actor` / `653 TickBehaviour`（`Damageable` 728 类） / `349 Animator`（346 AlwaysAnimate，200 不可见） / `2328 Renderer`（**2094 投影体** / 782K 三角面） / `NavMeshAgent 41` / `Collider 1164` / `粒子 70`。
距离：`<30m 81`、`60-100m 123`、`100-200m 327`、**`>200m 1777`**（相机 far=600）。
静态标记 `139 / 非static 2189` ⇒ **静态合批几乎没生效**；`LODGroup` 仅 9 个；材质 `RobotTower_ncl` 出现两份（有材质实例破坏合批）。
Ground 层 = `CliffA~E 37 / Platform1 22 / Stairs1 30 / SampleCabinet1 14 / AbydosPlane1 3` ≈113。

## 六、待办（按优先级）

1. **验证第三节全部改动**（重编译会退出 Play；`external_changes_dirty`）。
2. `ScriptRunBehaviourUpdate` **4.58ms**（最大项）：怀疑是旧版 `Rebuild()` 的 30s 尖峰，已改分帧；若仍高需展开子节点。
3. 渲染剩余 **3.12ms**：`RenderSingle` 跑了 2 遍（`UIFxCamera.renderPostProcessing=1` 可关）；投影体 799→目标 300（关装饰投影）；shadowDistance 70 已经不大。
4. `DirectorUpdateAnimationBegin` **1.11ms**：远处单位可考虑 `Animator.enabled=false`。
5. `PlayerUpdateCanvases` **0.36ms**：还有 UI 每帧标脏。
6. `LogicFrameHost` 曾有 100KB（已定位到 HasFlag 装箱，本轮已修，待重测）。
7. **GPU 未知**：编辑器 `GPU: --`，早前 FrameTiming 采样 **15.27ms @640×480** ⇒ 需 Development Build 复测，否则 CPU 优化到 14.6 后可能被 GPU 卡住。
8. `NetSyncLog.cs:83` `Enabled = true` 写死 → 发布前改。
9. 788 个静态道具补勾 **Batching Static**（只勾这一项，不触发 GI 重烘焙）；修材质实例。
10. 单位数量（287）是设计层成本，LOD/降频是下一步量级杠杆。

## 七、避坑（重要）

- **编辑器内测 GC 的三条死路**：`GC.GetAllocatedBytesForCurrentThread()` 恒 0；`GC.GetTotalMemory(false)` 只按 256KB 块跳；`GarbageCollector.GCMode=Disabled` 编辑器不支持。`ProfilerRecorder("GC Allocated In Frame")` 只在 `Profiler.SetAreaEnabled(Memory,true)` 瞬间读到一次。
- **读 Profiler 的技巧**：看 `GC.Alloc` 子行的**次数**，`总字节 ÷ 次数 ≈ 24` 就是**装箱**；几百/上千字节且次数少才是字符串/List。
- 值类型传**接口参数**必装箱（`IPERange`/`IDamageable`）；Unity 的 `GetComponent<接口>()` 内部会枚举组件，**每次都分配**。
- `FpsHelper` 用**静态**缓冲 ⇒ 查完立刻拷快照再 `InflictDamage`（连锁爆炸会覆盖）。
- `Camera.layerCullDistances` 数组里 **0 = 不渲染**。

## 八、Play 实测剖面（2026-11-02，BattleScene，MCP 层级 dump 150 帧）

取数手法（可复用）：`execute_code` + `UnityEditorInternal.ProfilerDriver.GetHierarchyFrameDataView(frame,0,MergeSamplesWithTheSameName,columnTotalTime,false)`
遍历 `GetRootItemID/GetItemChildren/GetItemColumnDataAsFloat`，跨帧按名字累加求 avg/p50/p95/max。
⚠ 坑：`HierarchyFrameDataView` **没有 `HasChildren`**；`EditorApplication.update` 要声明成 `CallbackFunction` 而非 `Action`；
`execute_code` 默认走 CodeDom（**C# 6**，无元组/局部函数），Roslyn 未装。

帧预算（含编辑器开销）：FRAME `18.06/17.33/23.12/28.73`；PlayerLoop `14.49/13.87/18.53/22.39`；EditorLoop `2.90`。
FrameTiming：CPU 13.79 / 主线程 11.34 / 渲染线程 3.39 / **GPU 8.04** ⇒ 主线程是瓶颈，GPU 尚有余量。
vSync=0、targetFrameRate=-1（编辑器 ≈72fps）；场景 `MonoBehaviour 5818`、`Renderer 1880`（投影 1398、forceRenderingOff 255）。

- **ScriptRunBehaviourUpdate 5.23**：`BehaviourUpdate` self ≈ **2.5ms 是纯引擎 dispatch**（≈3.9k 次 Update/帧）。
  `Actor.Update` 0.96ms/1006 次；`Health.Update` 0.16ms/1006（只做 `Time.time>=m_Time` 判定，纯空转）；
  `TickBehaviour.Update` 0.17ms/1243（只做一次 float 比较）。这两族 ≈2250 次空 dispatch ≈1.3ms ⇒ 最划算的刀。
  业务体：`PlayerOperationController` 0.38 / `LogicFrameHost` 0.35 / `SubtitleWnd` 0.22 / `EnemyControllerFX` 0.22(408) /
  `AIInputBaseController` 0.21(113) / `EnemyFXControllerUnit` 0.18(80) / `Infrared` 0.16(14)+`Physics.Raycast` 0.09 / `EnemyController` 0.10(112)。
- **DoRenderLoop_Internal 6.04**：`Inl_UniversalRenderTotal` 5.63 → `RenderCameraStack` 5.59，
  其中 Main Camera `RenderSingleCameraInternal` 2.19、**UIFxCamera 0.84（cullingMask=32=UI、post=False，纯二次 cull+setup 开销）**、
  `ScriptableRenderer.Execute` 2.30、**`ScriptableRenderContext.Submit` 2.63**、`CullScriptable` 0.83（`TerrainManager.CullAllTerrains` 0.33 + `SceneCulling` 0.19）、`PrepareDrawShadows` 0.15。
  相机栈实测 `Main Camera -> WeaponCamera(disabled) -> UIFxCamera`，真正渲的是 2 个。
- 已生效确认：`CameraCullDistance` 在 Main Camera（Default 180 / Unit 250 / LightLayer+AirWall+Smoke 140，far 600）；
  `RenderChunkCuller` 1 个且 enabled；`DirectorUpdateAnimationBegin` 1.11 → **0.59**（CullUpdateTransforms 起效）。
- ⚠ MCP `mcpforunity://rendering/stats` 在编辑器里 `draw_calls/batches/triangles` **恒 0**（只在渲染帧内有效），
  拿真实 draw call 只能用 Frame Debugger。

### 8.1 已实施：TickDriver 统一逐帧驱动（2026-11-02，离线编译 0 error，待 Play 复测）

**思路**：三大空转型家族（`Health` 1006 / `TickBehaviour` 1243 / `Actor` 1006）各自挂 `Update`，每次只做一次时间/位置比较，
引擎每次"调进去"≈0.6µs ⇒ 收进一条 for 循环，省掉 dispatch。

- 新增 `00Core/Timer/TickDriver.cs`：`ITickDriverTarget.TickFrame(float now)` + 静态 `Register/Unregister/Pump`。
  `Pump` **倒序遍历**（目标在 TickFrame 里销毁自己也不越界；帧内新注册的本帧不跑）；
  销毁靠 `target is Object o && o == null` 自愈摘除（这些家族子类大量覆写 `OnDestroy` 且不调 base，不能用 OnDestroy 注销）。
- 泵点：`LogicFrameHost.Update` 首行 `TickDriver.Pump(Time.time)`（⚠ 全工程只能有这一处，多一处 = 节拍翻倍）。
- `TickBehaviour`：删 `protected virtual void Update()`，改显式实现 `TickFrame`，`Start` 里 `Register`。
  ⚠ 有 4 个子类原本 `override void Update(){ base.Update(); ... }` —— `WaveManager` / `AirdropController` /
  `MedivacController` / `MissionInterceptTrain` 改成普通 `protected void Update()` 并删掉 `base.Update()`（节拍已由驱动发）。
- `Health`：删 `Update`，`TickFrame` 放 `Health_AboState`，`InitAboState/UninitState` 里注册/注销。
- `Actor`：删 `Update`，改 `TickFrame`；**角度检查降到 1/4 帧错峰**（`(Time.frameCount+indexID)&3`），
  因 `OnAngleChange` 唯一订阅者是 `MiniMapWnd`（实测 `eulerAngles` ~145ns × 1006 ≈ 0.15ms/帧）。
- 语义保持：各 `TickFrame` 首行判 `isActiveAndEnabled`（≈40ns/次），等同"禁用/对象池回收后引擎不再调 Update"。
- 预期：`ScriptRunBehaviourUpdate` 5.23 → ~3.3ms（dispatch 2.5ms 里约 2.1ms 消失，剩 ~0.4ms 是驱动循环本身）。

**复测（同日换了一场规模大得多的战斗，不能与上一场横比）**：
| 指标 | 改前那场 | 改后这场 |
|---|---|---|
| Actor / Animator / Renderer / 投影 | 1006 / 349 / 1880 / 1398 | 1318 / **862** / **3069** / **2321** |
| `BehaviourUpdate` **self** | 2.50ms | **0.93ms** |
| `Actor/Health/TickBehaviour.Update` marker | 0.96/0.16/0.17 | **全部消失** |
| `LogicFrameHost.Update`（含 Pump 4277 目标） | 0.35ms | **2.36ms** |
| `ScriptRunBehaviourUpdate` | 5.23ms | 5.42ms |
| `DirectorUpdateAnimationBegin` | 0.59ms | 2.42ms（Animator 2.5 倍，与本次改动无关） |

- 归一化：三个家族"dispatch + body"合计 **1.16µs/目标 → 0.47µs/目标**（省约 60%）。总帧没降是因为这场动画体/渲染体大得多。
- 功能自检全过：1318 个 Actor 的 `Range.GetXY() == LogicPos` **0 不一致**（位置同步仍在跑）；1641 个 TickBehaviour `TickCount` 全部 >0；`TickDriver.Count` 4187→4277（随刷怪/死亡浮动，无泄漏）；Console 无新增 error。
- ⚠ `Pump` 实测 ≈450ns/目标（骨架 cast+null 33ns、`isActiveAndEnabled` 104ns，其余是各 TickFrame 体 + 缓存miss）⇒ **下一步不是优化循环，是减少被驱动对象数**：
  `Health` 按需注册（只在 `AboGauge` 有 >0 时进表，1298→约 50）、`TickBehaviour` 改截止时间队列（TickTime 多为 1s，每帧到期仅约 27 个，1641→约 30），
  那样 pump 只剩 Actor 1318 ≈0.6ms，还能再省 ~1.4ms。

### 8.2 已实施：按需驱动 + 动画距离剔除（2026-11-02，离线编译 0 error，待 Play 复测）

**TickDriver 拆成两条通道**（`00Core/Timer/TickDriver.cs`）：
- `ITickDriverTarget` 平铺（每帧必看，实测 ~450ns/目标）→ 现在只剩 Actor。
- `IScheduledTickTarget` + **二叉最小堆**（`NextDue` 为键）→ 只碰到期项，1s 定时器在 60fps 下每帧仅 1/60 命中。
  ⚠ 实现要点：**先弹完本轮到期的再统一回调 + 回堆**（否则回调把 NextDue 设回 <=now 会同一帧死循环）；
  弹出与回调之间可能被别的 Tick 销毁 ⇒ 回调前要再判 `Object == null`；重入堆不加 `_scheduledSet` 去重会重复入堆。
- `TickBehaviour` 改走堆：`Start` 里 `_nextDue = lastTickTime`（**不是** `+TickTime`，子类常在 `base.Start()` 之后才改 TickTime）。
  ⚠ 语义变化：禁用/池中期间**不再追赶**（原实现会连跑若干帧补 Tick），改为推迟一个周期。
- `Health` 改**按需注册**：`InitAboState` 不注册；`AddAboGauge` 首次积蓄>0 才 `EnsureAboTick`；
  `AboTick` 末尾 `!HasActiveGauge()` 就 `ReleaseAboTick`。⇒ 千余 Health 里常驻进表的只有几十个。

**动画距离剔除**（`06Gameplay/Perf/UnitAnimatorCuller.cs`，需手动挂 Main Camera）：
- `Actor` 缓存 `Animator[]`（`InitAnimatorCulling`）+ `SetAnimatorsEnabled(bool)` / `AnimatorsOff`（状态没变不遍历）。
- 组件每 `Interval`(0.2s) 扫 `ActorsManager.Actors`，远于 `FarDistance`(120m) 关、近于 `NearDistance`(100m) 开（滞回），
  `ActorState.Dead` 跳过，`OnDisable` 全场还原。
- ⚠ **不用 UnitQueryGrid**：网格擅长"给我范围内的"，而这里要的是**范围外的那一批**，用网格反而多一次差集；
  0.2s 一次全扫 1300 个 ≈ 0.01ms/帧。
- ⚠ 关 Animator 会冻状态机 + **动画事件不再触发**（`Actor.InitAnimatorCulling` 的注释同款警告）⇒ 距离别调太近。

预期：Pump 4277 → ~1350，`LogicFrameHost` 2.36 → ~0.7ms；`DirectorUpdateAnimationBegin` 2.42 → ~1.2~1.6ms（取决于 120m 外的占比）。
