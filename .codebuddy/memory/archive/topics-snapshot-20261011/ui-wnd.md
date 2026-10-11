# 主题 · UI 预制体 / 窗口（Wnd）面板布局

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。

## 素材词表（做新面板先从这里挑，别新造图）
- 面板底 `Assets/Images/AlphaFrame.png`（⚠ 边框 + **透明中心**，见下）
- 分隔线 `GradLine_L.png` / `GradLine_R.png`；关闭 `CloseBtn_Icon.png`；箭头 `Arrow_Down.png`
- 按钮底 `FX_TEX_Lock.png`（Sliced，`Cancel`/`Server`/下拉框都用它）
- 格子/图标框 `Hexagonal_Frame3.png`；勾点 `circular_128.png`；实底 `TEX_White_16x16.png`；条 `Bar5.png`(Sliced)
- 任务图标 `Resources/Images/Objectives/*`（`ResSvc.LoadSprite("Objectives/…", true)`；ResSvc 未就绪时降级 `Resources.Load`）
- 字体 `Assets/Art/Font/fSimpleRound SDF.asset`（TMP；项目里 `fontSize` 16~32 混用）
- 配色：主色 `(0.651,0.867,0.867)`、面板黑 `(0,0,0,0.88)`、正文 `(0.92,0.94,0.96)`、次要 `(0.62,0.66,0.70)`、选中行 `(0.42,0.26,0.05,0.96)`

## 硬经验（踩过的坑）
- ⚠ **`AlphaFrame` 是"边框 + 透明中心"** ⇒ 拿它当**整屏/大面板**时必须在里面垫一层 `TEX_White_16x16` 实底（范例：`SelectMapWnd/Rooms/Panel/Body`，四边内缩 12 保留边框），否则面板中间会透出场景背景
- ⚠ 代码填 `VerticalLayoutGroup` 列表后，**刷画布顺序**：`Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); Canvas.ForceUpdateCanvases();` —— 最后一次不能省（重排会把图元标脏，不刷则新克隆的行要等下一帧才出来）。相关：`2026-10-04`「TMP 度量滞后」那条
- ⚠ 表头与数据行要逐列对齐 ⇒ **共用同一组列宽 + 同一套 HLG 参数**（`childControlWidth=true`、`childForceExpandWidth=false`，每列挂 `LayoutElement.preferredWidth`），改一处必须同步另一处
- 列表结构：`Scroll(ScrollRect)` → `Viewport`(`Image`+`Mask(showMaskGraphic=false)`) → `Content`(`VerticalLayoutGroup`+`ContentSizeFitter(VerticalFit=PreferredSize)`) → 首子物体放 **inactive 行模板**（VLG 会忽略 inactive，不占位）
- 非交互图元（文本、格子、装饰）一律 `raycastTarget=false`；交互靠行/按钮自身的 `Button`
- ⚠ **别用 MCP `manage_camera screenshot` 判"UI 有没有画出来"**：新建 additive 场景里的 `ScreenSpaceOverlay` Canvas 头 1~2 次截图是空的（多截几次 / 先改一次颜色才出）⇒ 先用反射读 `rect`/`color` 取证

## 批量搭 prefab 结构的套路
- `PrefabUtility.LoadPrefabContents(path)` → 就地建树 → `SaveAsPrefabAsset(root, path)` → `UnloadPrefabContents(root)`：全程在隔离预览场景，不污染当前场景，嵌套 prefab 实例会保留
- 回填 `[SerializeField]` 用 `SerializedObject` + `ApplyModifiedPropertiesWithoutUndo()`；⚠ **承载回填引用的容器必须是 class，不能 struct**（`BuildFilter(panel, refs)` 这种按值传递 ⇒ 引用静默全丢、Inspector 里一半是 null，且不报错）
- 验证不用进 Play：`LoadPrefabContents` 后把目标节点 `SetActive(true)` + `LayoutRebuilder.ForceRebuildLayoutImmediate`，直接读 `rect` / `localPosition` 逐项核对；数据填充可反射调组件的 `Open()` 后读文本与颜色
- 一次性生成器范例（62 节点，幂等）：`.codebuddy/plans/BuildServerListPanel_生成服务器列表面板.cs.txt`
- ⚠ **别假设"做好的项节点带 Button"**：`SelectMapWnd/Rooms` 的下拉项（`FilterMapItem`/`FilterDiffItem`）只有 `Title` 有 Button，项本身没有 ⇒ 代码里要 `TryGetOrAddComponent<Button>()` + `targetGraphic = GetComponentInChildren<Image>()` 且 `raycastTarget=true`，否则 `SetCilck` 直接空引用
- ⚠ 从模板**克隆**出来的列表项，每次重建前必须先销毁上一批（模板本体保持 inactive 且永不当项用），否则刷新一次项数翻倍；编辑器/离屏验证里销毁用 `DestroyImmediate`
- 面板内做**可展开下拉**时要留意：项是 VLG 的直接子物体 ⇒ 展开会撑开外层布局；如果外层没有滚动条，就得限制项数（只列"数据里实际出现的值"是最省事的上限）
- **要"下拉框感"（浮层、不撑布局）就这么做**：把组的项包进一层容器节点 → 容器挂 `LayoutElement.ignoreLayout=true`（LayoutGroup 按 `ILayoutIgnorer` 过滤子物体，组高恒为 Title）+ `ContentSizeFitter(VerticalFit=PreferredSize)` + `VerticalLayoutGroup`，项挪进去；外层再加一个 `DropdownLayer`（必须是该栏的**末位**子物体，否则被后面的兄弟盖住），展开时 `container.SetParent(layer, false)` 后抄组的 `anchoredPosition`（组与 layer 同为父级的直接子物体、锚点都取父级左上角 ⇒ 坐标数值可直传，实测浮层上边与 Title 下边差 0.0）。再做一层**点击拦截层**实现"点空白收起"：透明 `Image`(`raycastTarget=true`) + `Button`，放在会挡住的那一侧，⚠ 别盖住筛选栏本身，否则点别的 Title 要先收当前下拉再点一次。范例：`SelectMapWnd/Rooms/Panel/{Filter/DropdownLayer, Blocker}`
- ⚠ **从 inactive 模板克隆出来的项会继承"关闭"状态**：就地展开的旧实现靠"逐个把子物体 `SetActive(true)`"顺带把它们点亮，改成只点亮浮层容器后，克隆项必须显式 `SetActive(true)`，否则下拉弹出来是空的（固定项的组不受影响，容易漏测）
- ⚠ 组件里若要「组 → 项容器」的映射，**必须在 Build 时抓一次存成字段**：容器展开时会被 `SetParent` 挪走，之后 `group.Find("...")` 就找不到了
- ⚠ **`Animator.Play` 打在未激活物体上会静默失败**（Console 只有一条 `Game object with animator is inactive`，动画停在原状态、不报错不中断）。实例：大厅的 `GameStateController` 在 `state: 4 = Ready` 调 `BridgeWnd.DisplayTask()` 播 `taskRoot` 的 `Entry`，而**成员端**那一刻选图窗还占着（`WindowState = UI` ⇒ BridgeWnd 隐藏）⇒ 任务面板永远停在 Idle。⇒ 三条规矩：① **"收界面"必须排在"切状态"之前**（谁要在某阶段播动画，就得先把窗口/物体弄可见）；② 播之前判状态早退（如 `nowTask == null || !nowTask.activeTask`；⚠ `TaskCfg` 是**结构体**，不能判 null）；③ 给"同一局只展开一次"加 key（状态表 + "窗口重新显示"都会调同一个入口）。
- ⚠ **注册成"外部 sink"的窗口，UI 容器字段必须判空**：`ArmamentWnd.Init()` 里 `BridgeSys.Instance.armament = this`（随场景加载就跑，早于任何显示），而 `ready`/`animators`/`buttons` 是 `FirstShowWnd()` 才 new 的 ⇒ 窗口**没显示过**时收到联机同步（`ReceivePlayerReady` / `ReceivePlayerSelect*`）直接 NRE（2026-10-06 打包版两人联机实测：消息 4002/4004 双双"处理失败:Object reference not set"）。同理：`ready`/`animators` 数组长度 = `Constants.MaxPlayer`，但**只有当时有人在的槽位**才在 `ShowWnd()` 里挂模型 ⇒ 元素可能是 null（下标合法也 NRE）。⇒ 三个 `Receive*` 一律先"容器 == null"早退，数据落点放常驻管理器、窗口显示时再回填
- ⚠ **行内 3D 模型 = `RawImage` + 它自己身上那个 `Camera` 渲到 RenderTexture**（`ArmamentItem` 的 child1）：预制体里 `RawImage.texture` 与 `Camera.targetTexture` 写死成**同一张** `PlayerRole1`，而多行是**同一个预制体实例化**的 ⇒ 多台相机渲进同一张 RT、多个 RawImage 显示同一张 ⇒ **所有行都是同一份画面**。名字/等级/小头像却是各自由**正确那个模型**取的（`BaseObject.ShowName` / `Portrait`）⇒ 症状是「**名字对、大模型全一样**」，1 个玩家时看不出来（`ArmamentWnd`，2026-10-07 两人联机实测）。修法：`public RenderTexture[] roleViews` 一行一张 —— `Assets/Images/PlayerRole1..4` 早就备好了，只是从没被配到预制体上。⚠ 同一块还有个"叠罗汉"：`ShowWnd()` 每次都往 `item.GetChild(1)` 重挂模型而 `HideWnd()` 不清理 ⇒ 旧角色留在原地盖在前面，同样表现为"模型跟名字对不上"。
- ⚠⚠ **`ArmamentWnd.BindRowIdentity` 不许拿空 `roleName` 去 `CreatPrefab`**（2026-10-07 打包端实测崩溃）：
  名册里可能有"**还没上报资料的成员占位项**"（`NetHostSvc.BuildProfileArray` 的注释里写明"角色名空 ⇒ 消费方要靠 `HasProfile` 判空，
  别拿空 RoleName 去 CreatPrefab"）⇒ 空串会拼出 `"Prefabs/StudentModle/"`，`ResSvc.CreatPrefab` 报"没有找到…"并**返回 null**，
  紧接着 `showModle.transform.parent = …` 直接 NRE。
  触发时机很典型：**撤离倒计时走完**（`MedivacController.SortieTick` → `GlobalEventSub.RequestGameState(GameStateEnum.Armament)`）
  ⇒ 回"配置战备"是**设计如此**，此时名册可能正是占位状态 ⇒ 窗口一刷就崩（`MedivacController.Tick → GameState setter → SceneChange → ShowWnd`）。
  护栏三件套：①空 roleName ⇒ 清掉该行身份（**不记"已绑定"**，资料到了 `ReceiveRosterChanged → RefreshPlayerRows` 会自动补上）；
  ②`CreatPrefab` 返回 null（资源真缺）⇒ 只警告 + 留空行；③`Animator`/`BaseObject` 判空后再取 `ShowName/Portrait`。
- ⚠ `WndRootTool.SetActive(Transform,bool)` **不判空**（内部直接 `trans.gameObject`）⇒ 调它之前自己判；另外 `Close()` 可能在 `Build()` 之前被调到（`SelectMapWnd.ShowWnd()` 一进来就 `serverPanel.Close()`），而 `Rooms` 在预制体里是 **active** 的，所以任何"一次性初始化在 Build"的组件都要能承受"Build 还没跑过"
- **列表行 = 独立预制体**：`Assets/Resources/UI/SelectMap/{Row,PipItem,FilterMapItem,FilterDiffItem,MapButton,MapTaskButton}.prefab`；面板里的 `Row`/`PipItem`/下拉项都是它们的**嵌套实例** ⇒ 改行的结构/观感要改 **`Row.prefab`**（只改实例是覆盖，会和 prefab 后续修改打架）。⚠ 取节点要按实际层级：行里的地图背景图是 **`Task/AreaMask/Area`**（`AreaMask` 是 `RectMask2D`，把 600×300 的图裁成一行高），写 `item.Find("Area")` 会拿到 null 而**静默不显示**
- 行的观感参数（底色 / 行高 / 列宽 / 图标框）一律**读 prefab 的**：底色在 `Build` 时从行模板 Image 抓一次当常态色，只有"选中态"才由代码染色 —— 代码写死颜色会把作者调好的观感盖掉（`NormalColor` → `_rowNormal` 就是这个原因）
- ⚠ **"行数按名册"的窗口必须能收到名册变化**：`ArmamentWnd` 的行只在 `ShowWnd()` 那一刻按 `teamManager.players.Count` 建/显，**别人中途退出时那一行不会消失**（旧模型/旧名字/旧战备图标都留着）。修法 = 契约接口加一条 `IBridgeArmamentSink.ReceiveRosterChanged()`（`BridgeSys` 转发 + `TeamNetBridge.HandlePlayerList` 在 `ReplacePlayers` 之后叫醒）→ 窗口 `RefreshPlayerRows()`：多余槽位隐藏 + **在座的行整行按数据回填**（⚠ 名册重排会让下标漂移：`players[0]` 恒为自己 ⇒ 只隐藏尾巴不够）。抽了 `BindRowIdentity`/`ApplyRowAirdrop` 给 `ShowWnd` 与新刷新共用，避免两份会走样的副本。
- ⚠⚠ **"槽位数 = `Constants.MaxPlayer`，但数据列表可能更短"**：`ArmamentWnd.FirstShowWnd` 的行是按 `MaxPlayer`(4) 建的，而 `teamManager.players` 可能只有 1~2 人 ⇒ **任何 `players[i]` 都要用 `i < players.Count` 守卫**（2026-10-07 实测：1 人局撤离完回到战备界面，`FirstShowWnd` 直接 `ArgumentOutOfRangeException: List.get_Item`，整个窗口打不开）。`ShowWnd` 里那个 `if (i < players.Count)` 不是装饰，新代码别绕开它。
- ⚠ **`Camera.main` 不是"永远有"**：没有 tag=MainCamera 的激活相机时它返回 **null**（实测：撤离结束切到 `Armament` 后战斗相机已失效，此时按 ESC 开设置窗）⇒ `FpsHelper.CameraCaptureToSprite(Camera.main)` 在第一句 `targetCamera.targetTexture = rt` 抛 NRE，把整个 `ShowWnd` 打断。修法 = helper 判空返回 null + 调用点 `if (sprite != null)` 保留上一张背景（`SettingWnd` / `AirdropConfigWnd` 两处都是这个模式）。同窗口里 `ActorsManager.Player`、`CreatPrefab` 的返回值也要判（一个 NRE 会遮住后面的一串）。
- ⚠ **`IsValidMono()` 是 `IMonoVaild`（接口）上的扩展**（`00Core/Interfaces/CoreInterfaces.cs`），**不是** `UnityEngine.Object`/`MonoBehaviour` 上的 ⇒ 对 `Animator`、`Transform` 之类调它会 `CS1061`（编译不过）。那些地方用 Unity 自带的 `x != null`（销毁过的对象会判成 false，语义就是要的）。同文件里还有个 `IsValid(this UnityEngine.Object)`（`00Tools/ObjectIsValid.cs`）可用，别把两者搞混。
- ⚠ **`Invalid AABB inAABB`（+ `Canvas.SendWillRenderCanvases`）**= 某个 UI 元素的 AABB 含 NaN/±Inf（原生日志、**不带对象名**）⇒ 根因是"世界→屏幕/地图"算出的坐标或尺寸非法后写进了 RectTransform。
  2026-10-10 已加兜底：`Tool.IsFinite(float/Vector2/Vector3)`（`00Tools/Tool_Geometry.cs`）+ `WndRootTool.SetSizeDelta` 丢弃非有限写入
  + `SubtitleBase.Follow`/`SubtitleMark.Follow` 非有限就跳过 + `MiniMapWnd` 的 `Zoom`/`MapSizeValid`（除零一律夹成 1）。
  ⚠ 同款写法的**其余位置未加**：`SubtitleWnd`(152/171 行 `WorldToScreenPoint`→`transform.position`)、`HitFlashWnd`(249)、`SightLockToTarget`(70)。
