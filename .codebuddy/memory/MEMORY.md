# Bluedivers 项目长期记忆

## 项目环境
- Unity 2022.3.62f2c1（2022 LTS），URP 14.0.12，C# 9.0 / .NET Standard 2.1
- 依赖：TextMeshPro 3.0.9、Navigation 1.1.6、Timeline 1.7.7、ugui
- **Photon PUN 已弃用**（旧方案），`Assets/Photon/` 将逐步移除；KCPNet（自研）未完成，当前为**单机版 demo**
- 逻辑数学：`PEMaths`（PEInt/PEVector 固定点数库，`Assets/Scripts/Lib/PEMaths.xml`），用于逻辑层确定性计算
- 产品名 BlueDivers，companyName GameDevelopmentDepartmentFaFa

## 项目定位
单机 PvE 第三人称/第一人称**射击割草** demo，对标《绝地潜兵2》（战备空投/轨道打击/飞鹰/SOS/探照灯、战略大地图、平坦地形）+《深岩银河》（撤离、采矿/欧帕兹、巢穴破坏、波次防守）。
- 代码由 Unity FPS Sample 二次开发（命名空间 `Unity.FPS.*`）
- 网络策略：轻服务器（仅房间列表），其余全存本地，暂无多人在线进度同步

## 代码结构约定
- 脚本在 `Assets/Scripts/`，按**数字前缀编号 + 功能**划分模块，编号体现程序集依赖顺序：
  `00Core` → `00GameContract` → `00Tools` → `01Manager` → `02Data` → `02Game` → `04UI` → `08Map` → `Effect/Feature/Rendering`
- asmdef 分层（8 个），但仅 `00_Core` 设 rootNamespace=Core；跨层依赖只能上层指向下层

## 命名约定（项目实际）
- 接口主流 `I_` 前缀（`I_Entity`/`I_Actor`/`I_Damagable` 等），少量历史标准 `I`（`IRecyclable`/`IPhysical`）
- ScriptableObject 用 `_SO` 后缀；partial 分部文件用下划线分隔（`主类名_分部名.cs`），同目录
- 字段：public 与 `[SerializeField] private` 并存，新增优先后者；私有/受保护用 `_` 前缀（历史有 `m_`）
- Inspector 中文化：广泛用 `[InspectorName("中文")]` / `[DisplayField]`，**仅对字段有效，不能用于属性**
- 枚举后缀不统一（`GameStateEnum` vs `ActorState`）；协程以动词开头，几乎不用 async/await
- **Unity `Debug` 无 `DrawWireSphere`**：运行时画线框球必须用 `Tool.DrawWireSphere(pos, size, color, time)`（`00Tools/Test/Tool.cs`），`Debug` 只有 `DrawLine`/`DrawRay`

## 已知问题
- ~~运行时脚本误引 UnityEditor~~：已于 2026-08-07 修复（`EnemyMobile.cs`/`RoleData_SO.cs`/`SoundGroup_SO.cs`/`TaskManager.cs` 加 `#if UNITY_EDITOR` 守卫）

## 修复记录（汇总）
- ModifyTerrain.cs：地形修改后根物体 Y 未更新导致悬浮 — 在地形修改与 AdditionTerrain 完成后重新采样高度贴地。
- WaveManager.cs：`TierItemWeight[tier]` 对不存在 tier 抛 KeyNotFoundException — 改用 TryGetValue + FirstOrDefault 降级。
- ObjectPool.cs：`Release(K key)` 误调 `_Pop` 而非 `_Push`，导致血条 UI 回收不隐藏 — 修复后回收正常。
- TerrainMainUtils.cs：SetAlphamaps 断言失败 — 根因 `TerrainUtils.Main` 缓存 alphamapRes 未随分辨率修改同步。修复：改分辨率后重新 `TerrainUtils.Main = terrain`，GetAlphas/GetHeights 直接读 `data.alphamapResolution`/`heightmapResolution`。
- EndGame 场景切换 NRE 连锁崩溃 — 修复：1) EndGame 加载前切 WindowState=UI；2) `ObjectPool.UnInit` 改 `while(Count>0)` 且 Remove 加 null 检查；3) VFXManager.ClearPool 加 null 检查。
- Health.cs 死亡僵尸单位 — Heal() 加 `m_IsDead` 检查；HandleDeath() 强制血量归零；Revive() 将 `m_IsDead=false` 移到 OnRevive 之前并补 showHealth 更新。
- Furniture_General.cs Supply：owner 为 null 时 NRE — 加 `if (furn.owner != null)` 判空。
- PlayerOperationController.cs 第三人称 target 选择：`OverlapSphere` 取最近而非第一个有效 Collider。
- SubtitleWnd.cs 第三人称 UI 提示：改为直接使用 `playerOp.target` 保持一致。
- BaseSelfMoveableController.cs 陡坡卡死：新增 `_isOnSteepSlope` 标志，Move() 前用 `Vector3.ProjectOnPlane` 将位移投影到坡面法线平面，重力沿坡滑下。
- DeployableMine.cs 高空单位误引爆（2026-08-31）：飞行单位（如 `HealdroneBase.prefab`）halfHeight=0，原竖直过滤对 `HalfHeight<=0` 放行。改为 `InTriggerRange` 实际 3D 距离判定：竖直间隙按 [CenterPos.y±HalfHeight]（未配置退化为中心点），水平间隙 = 中心距 - HalfRange，合成后与触发距离比较。注意：halfHeight=0 的地面单位若 AimPoint 离地过高会炸不到，需批量工具补值。

## 资源文件
- `Assets/Shader/UIImageChannelMix.shader`：UI/Image 专用 Shader（`UI/ImageChannelMix`），R通道*Image颜色，G通道*白色，B忽略，A用贴图Alpha，支持 Stencil 响应 Mask

## 架构约定
- **降低耦合度**：避免组件间直接 `GetComponent` 互取引用，跨模块通知优先事件机制。全局事件在 `GlobalEventSub`，战斗相关在 `BattleEventSub`。
- **默认操作视角**：`ArchivesData_SO.settingDic["默认操作视角"]` 存 `ArchSettingData`（0=第一人称，1=第三人称）。`PlayerController.Start()` 读取并 `ApplyViewMode()` 初始化；`ApplyViewMode()` 同时被 `HandleToggleView()` 复用，封装 Camera cullingMask/WeaponCamera/LookAt 统一处理。
- **AI 控制器架构（2026-08-20 确认）**：`AIController`（`Assets/Scripts/02Game/AI/Controller/AIController.cs`）**不继承** `Actor`，采用"组合 + 接口代理"（`m_Actor = GetComponent<Actor>()` 代理 AimPoint/CenterPos/Pos/HpPos/ID）。理由：① `Actor` 是身份/标识组件，被大量系统 `GetComponent<Actor>()` 单实例取用，继承会导致双 Actor 行为未定义；② 同物体挂 Actor + 控制器是既定组合约定；③ 避免生命周期耦合。`AIController` 抽象基类，子类 `EnemyController`/`OtherController`。全项目无类继承 `Actor`。

## 协作偏好
- 遇到不确定/有疑问时先暂停、总结状态并向用户询问，不自行做大量全局搜索推断。
- 输出代码不主动纠结/移除 using 语句，用户自行管理。
- 文件夹重命名/移动、文件改名等结构性修改由用户**手动**完成，AI 只给方案不执行。

## 项目全景总结（2026-08 调研，2026-08-07 修正）
> 详细版见 `.codebuddy/skills/bluedivers-unity/references/module-guide.md` 末尾「项目全景总结」。

**已实现**：伤害模型（弱点/多部位护甲/关联护甲/护盾/抗性）、属性系统（Modifier/双属性链）、武器体系（30+ 脚本，过热/蓄力/升级模块）、投射物、玩家（视角切换/倒地呼叫/载具/喷气背包/护盾背包）、AI（探测/状态机/技能/BOSS）、波次（`WaveManager` Zerg/Robot，人口权重刷怪，3 文件）、`UnitQueryGrid` 空间网格、平坦噪声地形（fBm+植被+NavMesh）、任务系统（主/支/巢穴/撤离）、昼夜循环+夜间敌袭、**战备全类别**、**战略大地图**、撤离（类似 DRG 全员上船起飞）。

**未实现**：绝地潜兵（重型部署类战备如机甲/炮塔、大战略任务链解放度、联机）；深岩银河（洞穴/矿洞体素，地图平坦不做挖掘、矿物转运货运链）；通用（Roguelite 成长闭环待确认）。

**规划中的系统**：矿物提交（`SpecUnitKei` 加 furniture 交互 + 玩家 OOPart 拾取组件，有携带上限）；联机（KCPNet 完成后再迁移）；波次规模维持现状。

**架构改进优先级**：① 统一确定性随机源（`BattleRandom` 与 `WaveManager` 的 `System.Random` 收口）② `I_Damagable`/`I_Entity` 与 Unity 类型解耦为纯逻辑 DTO ③ 统一命名空间 + asmdef rootNamespace ④ 重写 SingletonNet RPC 封装 ⑤ God Class 审查（`BattleManager`/`PlayerController`/`WeaponPlayerController`）⑥ `UnitQueryGrid.FindUnits` List 分配池化。

## 单位竖直占位约定（2026-08-29）
- `I_Entity.HalfHeight`：单位竖直占位区间 = `[CenterPos.y - HalfHeight, CenterPos.y + HalfHeight]`；**0 = 未配置，消费方退化不做高度过滤**（保证存量行为不变）
- `Actor.halfHeight` 由编辑器工具 `Tools/单位半高度批量设置`（`Assets/Editor/ActorHalfHeightTool.cs`）按 `AimPoint` 相对 Actor 的局部 Y 批量填充（未旋转时区间底部正好落在 `Pos.y` 脚底）
- 需要区分空中/地面单位的逻辑（如 `DeployableMine` 触发判定 `InVerticalRange`）必须叠加竖直检测，不能只做平面距离

## 可复用编辑器基础设施（优先复用，勿重复造轮子）
- **SO 选择弹窗**：`Assets/Editor/SOPickerPopup.cs` 的 `SOPickerPopup<T>`（全局命名空间，`PopupWindowContent`）。做"从数据里选一个并回调"的可搜索列表弹窗，**优先用** `PopupWindow.Show(rect, new SOPickerPopup<T>(items, onPick, getIcon, getName, getType?, getTypeColor?, getFrame?, confirmMode))`。
  - `confirmMode=false`（默认）单击即回调关闭；`confirmMode=true` 单击选中+确定/取消确认。
  - 数据过滤由调用方注入前完成；现有调用方：`WeaponUpgradeEditorWindow.cs`、`RoleSpeechGroupDrawer.cs`。
- **预制体组件批量工具基类**：`Assets/Editor/PrefabBatchToolBase.cs` 的 `PrefabBatchToolBase<TComponent> : EditorWindow`（namespace `Unity.FPS.EditorExt`，同目录同程序集）。以后做「遍历 Assets 下所有预制体里某类组件 → 批量检查/批量写字段」的编辑器窗口时，**继承它**并最小实现钩子即可得到两步式(1.扫描/2.应用)窗口、进度条、报告与复制：必填 `protected abstract BatchItem CreateItem(TComponent, prefabPath, objectPath)`（纯判定+报告文本）；选填 `WriteValue(TComponent)→WriteResult{Modified/NoChange/Skipped}`（默认只读，`SupportsApply=false` 只扫不写）、`DrawOptions()`（base 已含"跳过嵌套预制体"开关，判定用 `SkipNestedPrefab`）、文案虚属性与汇总/行格式虚方法（BuildScanSummary/BuildApplySummary/BuildReportLine/BuildChangedLine/BuildChangeText）。`[MenuItem]` 静态入口与写 prefab 走 `LoadPrefabContents`+`SaveAsPrefabAsset`（finally 必 Unload）。现调用方：`ActorHalfHeightTool.cs`（半高度批量设置）。

## 文档与 Skill
- `.codebuddy/rules/UnityCSharp编码规范.md`：C# 编码规范（必须/推荐/可选三级），自动加载为项目规则
- `.codebuddy/skills/bluedivers-unity/`：项目专用 skill，含 SKILL.md（架构约定、命名速查、开发工作流、KCP 网络约定、避坑指南、关键基础设施表）和 references/module-guide.md（模块职责、关键类清单、项目全景总结）。处理项目 C# 代码时自动触发
