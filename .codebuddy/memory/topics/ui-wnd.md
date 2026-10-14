# 主题 · UI 预制体 / 窗口（Wnd）面板布局

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。
> ⚠ 原文（含每条实测细节）已下沉：`archive/topics-snapshot-20261011/ui-wnd.md`。

## 素材词表（做新面板先从这里挑，别新造图）
- 面板底 `Assets/Images/AlphaFrame.png`（⚠ 边框 + **透明中心**，见下）
- 分隔线 `GradLine_L/R.png`；关闭 `CloseBtn_Icon.png`；箭头 `Arrow_Down.png`
- 按钮底 `FX_TEX_Lock.png`（Sliced，`Cancel`/`Server`/下拉框都用它）
- 格子/图标框 `Hexagonal_Frame3.png`；勾点 `circular_128.png`；实底 `TEX_White_16x16.png`；条 `Bar5.png`(Sliced)
- 任务图标 `Resources/Images/Objectives/*`（`ResSvc.LoadSprite("Objectives/…", true)`；ResSvc 未就绪降级 `Resources.Load`）
- 字体 `Assets/Art/Font/fSimpleRound SDF.asset`（TMP；项目里 `fontSize` 16~32 混用）
- 配色：主色 `(0.651,0.867,0.867)`、面板黑 `(0,0,0,0.88)`、正文 `(0.92,0.94,0.96)`、次要 `(0.62,0.66,0.70)`、选中行 `(0.42,0.26,0.05,0.96)`

## 硬经验（踩过的坑）
- ⚠ **`AlphaFrame` 是"边框 + 透明中心"** ⇒ 当整屏/大面板时必须在里面垫一层 `TEX_White_16x16` 实底（范例 `SelectMapWnd/Rooms/Panel/Body`，四边内缩 12 保留边框），否则中间透出场景背景
- ⚠ 代码填 `VerticalLayoutGroup` 列表后**刷画布顺序**：`Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); Canvas.ForceUpdateCanvases();` —— 最后一次**不能省**（重排会把图元标脏，不刷则新克隆的行要等下一帧）
- ⚠ 表头与数据行逐列对齐 ⇒ **共用同一组列宽 + 同一套 HLG 参数**（`childControlWidth=true`、`childForceExpandWidth=false`，每列挂 `LayoutElement.preferredWidth`），改一处必须同步另一处
- 列表结构：`Scroll(ScrollRect)` → `Viewport`(`Image`+`Mask(showMaskGraphic=false)`) → `Content`(`VerticalLayoutGroup`+`ContentSizeFitter(VerticalFit=PreferredSize)`) → 首子物体放 **inactive 行模板**（VLG 忽略 inactive，不占位）
- 非交互图元（文本/格子/装饰）一律 `raycastTarget=false`；交互靠行/按钮自身的 `Button`
- ⚠⚠ **跨根 Canvas 同排序 ⇒ 射线被"另一个画布"抢走（点不动但不报错）**：两个根 Canvas 若同为 `ScreenSpaceCamera`+同相机+同 `sortingOrder`，
  `EventSystem.RaycastComparer`（`EventSystem.cs:201-256`）会比到 `index`（= `GraphicRaycaster.cs:276` 的 `resultAppendList.Count`，
  即 raycaster 注册顺序）⇒ **先注册的画布恒排前**，其全屏图元吃掉落点（`depth` 只对**同** rootRaycaster 才参与比较）。
  实测（2026-10-15）：菜单 `Canvas`(FrontWnd 全屏 RawImage) vs `CanvasRoot/WndUI` 同为排序 0 ⇒ WndUI 上所有按钮/输入框都点不动。
  ⇒ 每个根画布必须给**显式且互不相同**的 `sortingOrder`（它跨 raycaster 会被比，且同时决定渲染序）
- ⚠ **别用 MCP 截图判"UI 有没有画出来"**：新建 additive 场景里的 `ScreenSpaceOverlay` Canvas 头 1~2 次截图是空的 ⇒ 先用反射读 `rect`/`color` 取证
- ⚠ **`Invalid AABB inAABB`（+ `Canvas.SendWillRenderCanvases`）** = 某 UI 元素 AABB 含 NaN/±Inf（原生日志、**不带对象名**）⇒ 根因是"世界→屏幕/地图"算出非法坐标/尺寸后写进 RectTransform。已加兜底：`Tool.IsFinite(float/Vector2/Vector3)`（`00Tools/Tool_Geometry.cs`）+ `WndRootTool.SetSizeDelta` 丢弃非有限写入 + `SubtitleBase/SubtitleMark.Follow` 非有限就跳过 + `MiniMapWnd.Zoom/MapSizeValid`（除零夹成 1）。⚠ **同款写法的其余位置未加**：`SubtitleWnd`(152/171)、`HitFlashWnd`(249)、`SightLockToTarget`(70)。

## 批量搭 prefab 结构的套路
- `PrefabUtility.LoadPrefabContents(path)` → 就地建树 → `SaveAsPrefabAsset(root, path)` → `UnloadPrefabContents(root)`：全程在隔离预览场景，不污染当前场景，嵌套 prefab 实例保留
- 回填 `[SerializeField]` 用 `SerializedObject` + `ApplyModifiedPropertiesWithoutUndo()`；⚠ **承载回填引用的容器必须是 class，不能 struct**（按值传递 ⇒ 引用静默全丢、Inspector 里一半 null，且不报错）
- 验证不用进 Play：`LoadPrefabContents` 后把目标节点 `SetActive(true)` + `LayoutRebuilder.ForceRebuildLayoutImmediate`，直接读 `rect`/`localPosition` 逐项核对；数据填充可反射调组件的 `Open()` 后读文本与颜色
- ⚠ **别假设"做好的项节点带 Button"**：`SelectMapWnd/Rooms` 的下拉项（`FilterMapItem`/`FilterDiffItem`）只有 `Title` 有 Button ⇒ 代码里要 `TryGetOrAddComponent<Button>()` + `targetGraphic = GetComponentInChildren<Image>()` 且 `raycastTarget=true`，否则 `SetCilck` 空引用
- ⚠ 从模板**克隆**的列表项，每次重建前必须先销毁上一批（模板本体保持 inactive 且永不当项用），否则刷新一次项数翻倍；离屏验证里销毁用 `DestroyImmediate`
- ⚠ **从 inactive 模板克隆出来的项会继承"关闭"状态** ⇒ 必须显式 `SetActive(true)`，否则下拉弹出来是空的
- **要"下拉框感"（浮层、不撑布局）**：项包进容器节点 → 容器挂 `LayoutElement.ignoreLayout=true`（组高恒为 Title）+ `ContentSizeFitter(VerticalFit=PreferredSize)` + `VerticalLayoutGroup`；外层加 `DropdownLayer`（必须是该栏**末位**子物体，否则被后面的兄弟盖住），展开时 `container.SetParent(layer,false)` 后抄组的 `anchoredPosition`；再加透明 `Image`+`Button` 做"点空白收起"拦截层（⚠ 别盖住筛选栏本身）。范例 `SelectMapWnd/Rooms/Panel/{Filter/DropdownLayer, Blocker}`。
  ⚠ 面板内做可展开下拉还要留意：项是 VLG 的直接子物体会撑开外层布局（外层没滚动条就限项数）；⚠ 组件里若要"组 → 项容器"的映射，**必须在 Build 时抓一次存成字段**（展开时会被 `SetParent` 挪走）
- ⚠ **`Animator.Play` 打在未激活物体上会静默失败**（Console 只有一条 `Game object with animator is inactive`，动画停在原状态）。实例：`GameStateController(state: 4 = Ready)` 调 `BridgeWnd.DisplayTask()` 播 `Entry`，而成员端那一刻窗口还占着（`WindowState = UI` ⇒ 隐藏）⇒ 面板永远停在 Idle。⇒ 三条规矩：① **"收界面"必须排在"切状态"之前**；② 播之前判状态早退（`nowTask == null || !nowTask.activeTask`；⚠ `TaskCfg` 是**结构体**，不能判 null）；③ 给"同一局只展开一次"加 key（状态表 + "窗口重新显示"会调同一入口）
- ⚠ **注册成"外部 sink"的窗口，UI 容器字段必须判空**：`ArmamentWnd.Init()` 里 `BridgeSys.Instance.armament = this`（随场景加载就跑），而 `ready`/`animators`/`buttons` 是 `FirstShowWnd()` 才 new ⇒ 窗口没显示过时收到联机同步直接 NRE（2026-10-06 打包版实测）。同理 `ready`/`animators` 数组长度 = `Constants.MaxPlayer`，但只有当时有人在的槽位才挂模型 ⇒ 元素可能是 null。⇒ 三个 `Receive*` 一律先"容器 == null"早退，数据落点放常驻管理器、显示时再回填
- ⚠⚠ **行内 3D 模型 = `RawImage` + 同物体上的 `Camera` 渲到 RenderTexture**（`ArmamentItem` child1）：预制体里 `RawImage.texture` 与 `Camera.targetTexture` 写死成**同一张** `PlayerRole1`，而多行是同一个预制体实例化的 ⇒ **所有行都是同一份画面**（名字/等级却各取自正确的模型 ⇒ 症状「名字对、大模型全一样」）⇒ 修法 `RenderTexture[] roleViews` 一行一张（`Assets/Images/PlayerRole1..4` 早就备好）。⚠ 同一块还有"叠罗汉"：`ShowWnd()` 每次往 `item.GetChild(1)` 重挂模型而 `HideWnd()` 不清理 ⇒ 旧角色盖在前面，同样表现为"模型跟名字对不上"
- ⚠⚠ **不许拿空 `roleName` 去 `CreatPrefab`**（2026-10-07 打包端崩溃）：名册里会有"**还没上报资料的成员占位项**"（空 `RoleName`）⇒ 空串拼出 `"Prefabs/StudentModle/"` ⇒ `CreatPrefab` 返回 **null** ⇒ 紧接着 `showModle.transform.parent = …` NRE。触发时机很典型：**撤离倒计时走完**回"配置战备"（`MedivacController.SortieTick → RequestGameState(Armament)`，名册常是占位态）。护栏三件套：① 空 roleName ⇒ 清掉该行身份（**不记"已绑定"**，资料到了会自动补）；② `CreatPrefab` 返回 null ⇒ 只警告 + 留空行；③ `Animator`/`BaseObject` 判空后再取 `ShowName/Portrait`
- ⚠ `WndRootTool.SetActive(Transform,bool)` **不判空**（内部直接 `trans.gameObject`）⇒ 调它前自己判。另外 `Close()` 可能在 `Build()` 之前被调到（`SelectMapWnd.ShowWnd()` 一进来就 `serverPanel.Close()`），而 `Rooms` 在预制体里是 **active** ⇒ 任何"一次性初始化在 Build"的组件都要能承受"Build 还没跑过"
- **列表行 = 独立预制体**：`Assets/Resources/UI/SelectMap/{Row,PipItem,FilterMapItem,FilterDiffItem,MapButton,MapTaskButton}.prefab`；面板里的 `Row`/`PipItem`/下拉项都是它们的**嵌套实例** ⇒ 改行的结构/观感要改 **`Row.prefab`**（只改实例是覆盖，会和 prefab 后续修改打架）。⚠ 取节点要按实际层级：行里的地图背景图是 **`Task/AreaMask/Area`**（`AreaMask` 是 `RectMask2D`，把 600×300 的图裁成一行高），写 `item.Find("Area")` 会拿到 null 而**静默不显示**
- 行的观感参数（底色/行高/列宽/图标框）一律**读 prefab 的**：底色在 `Build` 时从行模板 Image 抓一次当常态色，只有"选中态"才由代码染色 —— 代码写死颜色会盖掉作者调好的观感
- ⚠ **"行数按名册"的窗口必须能收到名册变化**：`ArmamentWnd` 的行只在 `ShowWnd()` 那一刻按 `teamManager.players.Count` 建/显，**别人中途退出那一行不会消失**。修法 = 契约加 `IBridgeArmamentSink.ReceiveRosterChanged()`（`BridgeSys` 转发 + `TeamNetBridge.HandlePlayerList` 在 `ReplacePlayers` 之后叫醒）→ 窗口 `RefreshPlayerRows()`：多余槽位隐藏 + **在座的行整行按数据回填**（⚠ 名册重排会让下标漂移：`players[0]` 恒为自己 ⇒ 只隐藏尾巴不够）
- ⚠⚠ **"槽位数 = `Constants.MaxPlayer`，但数据列表可能更短"** ⇒ **任何 `players[i]` 都要用 `i < players.Count` 守卫**（2026-10-07 实测：1 人局撤离完回战备界面直接 `ArgumentOutOfRangeException`，整个窗口打不开）
- ⚠ **`Camera.main` 不是"永远有"**：没有 tag=MainCamera 的激活相机时返回 **null**（实测：撤离结束切到 `Armament` 后战斗相机已失效，此时按 ESC 开设置窗）⇒ `FpsHelper.CameraCaptureToSprite(Camera.main)` 抛 NRE 把 `ShowWnd` 打断。修法 = helper 判空返回 null + 调用点 `if (sprite != null)` 保留上一张背景（`SettingWnd`/`AirdropConfigWnd` 都是这个模式）。同窗口里 `ActorsManager.Player`、`CreatPrefab` 的返回值也要判（一个 NRE 会遮住后面一串）
- ⚠ **`IsValidMono()` 是 `IMonoVaild`（接口）上的扩展**（`00Core/Interfaces/CoreInterfaces.cs`），**不是** `UnityEngine.Object`/`MonoBehaviour` 上的 ⇒ 对 `Animator`/`Transform` 调它会 `CS1061`（编译不过），那些地方用 Unity 自带的 `x != null`。同族还有个 `IsValid(this UnityEngine.Object)`（`00Tools/ObjectIsValid.cs`），别混。
- **`MsgWnd.prefab`（消息窗，2026-10-12）**：结构 = 面板 + `VerticalLayoutGroup` + `ContentSizeFitter(PreferredSize)`；`Text (TMP)` 是**消息行模板**（已设 inactive，`_itemTemplate` 指它）；⚠ prefab 里那个 **`InputField (TMP)` 是半成品遗留（聊天未实现）⇒ 已 `SetActive(false)` 停用**，要接玩家聊天时再启用并接 `onEndEdit`。逻辑在 `10UI/Wnd/MsgWnd.cs`（`Push/Clear`，过期自毁、空则自隐，`_lifeTime=6`、`_maxCount=8`）。
- ⚠ 仿 `InformationWnd` 的列表窗**不要定义 `Awake`**；列表刷新的 `Canvas.ForceUpdateCanvases() → RefreshLayoutImmediate → Canvas.ForceUpdateCanvases()` 末尾那次不能省。
