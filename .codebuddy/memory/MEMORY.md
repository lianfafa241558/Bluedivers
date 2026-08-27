# Bluedivers 项目长期记忆

## 项目环境
- Unity 2022.3.62f2c1（2022 LTS），URP 14.0.12，C# 9.0 / .NET Standard 2.1
- 依赖：TextMeshPro 3.0.9、Navigation 1.1.6、Timeline 1.7.7、ugui
- **Photon PUN 已弃用**（旧方案），`Assets/Photon/` 将逐步移除
- KCPNet（自研网络库）尚未完成，暂不接入；当前为**单机版 demo**
- 逻辑数学：`PEMaths`（Photon Engine 固定点数库 PEInt/PEVector，位于 `Assets/Scripts/Lib/PEMaths.xml`），用于逻辑层确定性计算
- 产品名 BlueDivers，companyName GameDevelopmentDepartmentFaFa

## 项目定位
单机版 PvE 第三人称/第一人称**射击割草** demo，对标《绝地潜兵2》+《深岩银河》融合。
- 绝地潜兵元素：战备空投/轨道打击/飞鹰/SOS/探照灯（全类别已实现）、主/副/特殊任务、战略大地图（`MapData_SO` + `SelectMapWnd` + `ArchivesData_SO`）、平坦地形生成
- 深岩银河元素：撤离（类似 DRG，所有人上船才起飞，不放弃任何人）、采矿/欧帕兹采集、巢穴破坏任务、波次防守
- 代码由 Unity FPS Sample 二次开发而来（命名空间 `Unity.FPS.*`）
- 网络策略：轻服务器（仅房间列表），其余全存本地；暂无多人在线进度同步

## 代码结构约定
- 脚本在 `Assets/Scripts/`，按**数字前缀编号 + 功能**划分模块，编号体现程序集依赖顺序：
  `00Core`（核心框架）→ `00GameContract`（契约）→ `00Tools` → `01Manager`（管理器）→ `02Data`（SO 数据）→ `02Game`（游戏逻辑）→ `04UI` → `08Map` → `Effect/Feature/Rendering`
- 使用 asmdef 分层管理（8 个），但仅 `00_Core` 设了 rootNamespace=Core，其余命名空间不统一
- 跨层依赖只能上层指向下层（编号大引用编号小）

## 命名约定（项目实际）
- 接口主流用 `I_` 带下划线前缀：`I_Entity`/`I_Actor`/`I_Damagable`/`I_GlobaManager`/`I_TickClass`；少量历史标准 `I` 前缀（`IRecyclable`/`IPhysical`/`IEquippable`）
- ScriptableObject 文件用 `_SO` 后缀：`RoleData_SO`/`SoundGroup_SO`/`NoticeTree_SO`/`ArchivesDataBase_SO`
- partial 类分部文件用下划线分隔（不用点）：`主类名_分部名.cs`（如 `Health_AboState.cs`），与主类文件同目录
- 字段：public 字段与 `[SerializeField] private` 并存，public 较多；受保护/私有字段用 `_` 前缀（历史有 `m_` 前缀）
- Inspector 中文化：广泛使用 `[InspectorName("中文")]` 和 `[DisplayField]`。注意：这两个特性仅对字段声明有效，不能用于属性（`get; set;`）
- 枚举后缀不统一（`GameStateEnum` vs `ActorState`）
- 协程命名以动词开头（`InitGameState`/`WaitSetPos`），不用 `Coroutine` 后缀
- 几乎不用 async/await，异步全用协程
- **Unity `Debug` 类没有 `DrawWireSphere`**：运行时调试画线框球必须用 `Tool.DrawWireSphere(pos, size, color, time)`（`00Tools/Test/Tool.cs`，内部走 Editor 的 `DrawLabelUtils`）。`Debug` 只有 `DrawLine`/`DrawRay`。误用 `Debug.DrawWireSphere` 编译失败。

## 已知问题
- ~~运行时脚本误引 UnityEditor~~：已于 2026-08-07 修复（`EnemyMobile.cs`、`RoleData_SO.cs`、`SoundGroup_SO.cs`、`TaskManager.cs` 添加 `#if UNITY_EDITOR` 守卫）

## 修复记录
- ModifyTerrain.cs：根物体悬浮 bug — Modify() 协程中先在旧地形高度贴地，再 yield return ModifyHeightMap 修改地形，地形改变后根物体 Y 未更新。修复：在地形修改和 AdditionTerrain 完成后重新采样高度并贴地。
- WaveManager.cs：CreatUnit 访问 `TierItemWeight[tier]` 时，若 tier（如 Giant）在当前模板配置中不存在，抛出 KeyNotFoundException。修复：改用 TryGetValue + FirstOrDefault 降级兜底。
- ObjectPool.cs：`AutoObjectPool<K,V>.Release(K key)` 错误调用 `_Pop` 而非 `_Push`，导致 HpWnd 血条回收时 `SetActive(false)` 不执行，血条 UI 卡在屏幕上不消失。修复：`_Pop?.Invoke(item)` → `_Push?.Invoke(item)`。
- TerrainMainUtils.cs：AdditionTerrain 中 SetAlphamaps 断言 `PixelAccessReturnCode::kOk` 失败 — 根因在 BattleManager.InitTerrain 中，先 `TerrainUtils.Main = terrain`（此时 alphamapRes 缓存了 terrainData 的旧分辨率），再 `terrainData.alphamapResolution = mapRes`（改了分辨率但 alphamapRes 未同步更新），导致跨局切换分辨率（1024→512）时 alphamapRes 仍为旧值，GetAlphas 计算出的 yBase 超出实际分辨率。修复：1) BattleManager.InitTerrain 在修改分辨率后重新 `TerrainUtils.Main = terrain` 同步缓存；2) GetAlphas/GetHeights 改为直接从 `data.alphamapResolution`/`data.heightmapResolution` 读取分辨率而非依赖静态缓存。
- EndGame 场景切换 NRE 连锁崩溃：EndGame() 中 AsyncLoadScene 完成后 ResSvc.Update 触发 SceneChange 事件 → VFXManager.ClearPool 清理对象池时 ObjectPool.UnInit 有 for 循环 Bug（Count 递减导致只清理一半）且 Remove() 无空值保护。同时 PlayerWnd/SubtitleWnd 的 Update 仍在运行但 m_Controller/ActorsManager.Player 已被场景卸载销毁。修复：1) EndGame 加载前先切 WindowState=UI 停止战斗 UI Update；2) ObjectPool.UnInit 改用 while (Count>0) 并 Remove 中加 null 检查；3) VFXManager.ClearPool 加 null 检查。
- Health.cs 死亡僵尸单位防御性修复：1) Heal() 增加 `m_IsDead` 检查，死亡后拒绝治疗；2) HandleDeath() 中 m_IsDead=true 后强制 CurrentHealth=0 + showHealth=0，防止异常恢复导致血>0但isDead=true的僵尸；3) Revive() 中 m_IsDead=false 移到 OnRevive?.Invoke() 之前，并补上 showHealth 更新。根因未完全确定，可能存在未发现的治疗/复活对敌人调用链路。

## 资源文件
- `Assets/Shader/UIImageChannelMix.shader`：UI Image 专用 Shader（`UI/ImageChannelMix`），R通道*Image颜色，G通道*白色(1,1,1)，B忽略，A用贴图Alpha，支持Stencil响应Mask组件

## 架构约定
- **降低耦合度**：避免组件之间直接互相获取引用（如 `GetComponent<PlayerController>()`）。跨模块/跨层通知优先使用事件机制。
- 全局事件（非战斗相关）在 `GlobalEventSub` 中定义；战斗相关事件在 `BattleEventSub` 中定义。
- 新增跨多组件通知的功能时，优先考虑在 `GlobalEventSub` 或 `BattleEventSub` 中加 `static event`，而非让每个组件各自 `FindObjectOfType` 或 `GetComponent` 获取目标实例。
- **默认操作视角设置**：在 `ArchivesData_SO.settingDic` 中以 key `"默认操作视角"` 存储，值为 `ArchSettingData`（Dropdown 类型，0=第一人称，1=第三人称）。`PlayerController.Start()` 中读取该设置并通过 `ApplyViewMode()` 初始化视角。`ApplyViewMode()` 同时被 `HandleToggleView()` 复用，封装了视角切换时的 Camera cullingMask、WeaponCamera、LookAt 组件的统一处理。
- **AI 控制器架构（2026-08-20 确认）**：`AIController`（`Assets/Scripts/02Game/AI/Controller/AIController.cs`）**不继承** `Actor`，保持"组合 + 接口代理"——通过 `m_Actor = GetComponent<Actor>()` 持有 `I_Actor` 并代理其 AimPoint/CenterPos/Pos/HpPos/ID。理由：① `Actor` 是身份/标识组件（团队/Flag/逻辑碰撞/ActorsManager 注册/事件广播），被大量系统 `GetComponent<Actor>()` 单实例取用（`ActorsManager`/`Damageable`/`WaveManager`/`HpItemBase` 等），继承会导致双 Actor 组件行为未定义；② `EnemyController`/`OtherController` 均 `[RequireComponent(typeof(Actor))]`，同一物体同时挂 Actor 与控制器是既定组合约定；③ `Actor` 有自己的 Awake/Update/OnDestroy 生命周期，避免耦合。`AIController` 是抽象基类，子类 `EnemyController`（`[RequireComponent(typeof(HealthEnemy), typeof(Actor))]`）、`OtherController`。全项目无任何类继承 `Actor`。

## 协作偏好
- 遇到不确定/有疑问的情况时，应先暂停、总结当前状态并向用户询问，而不是自行做大量全局搜索去推断。优先问清楚再行动。
- 输出代码时不要主动纠结/移除 using 语句。用户会自行管理 using 的增删，无需助手操心。
- 文件夹重命名/移动、文件改名等结构性修改由用户**手动**完成；AI 只负责分析、给出方案与依赖关系，**不主动执行**文件改动。

## 修复记录（2026-07-19）
- Furniture_General.cs Supply lambda：owner 为 null 时 NRE — 加 `if (furn.owner != null)` 判空保护整个补给逻辑（UseSupply + BaseOp + AddBattleDataItem）。
- PlayerOperationController.cs 第三人称 target 选择：`OverlapSphere` 取第一个有效 Collider 而非最近，导致密集 Supply 间选错目标。改为遍历所有 Collider 取距离 `checkPos` 最近的。
- SubtitleWnd.cs 第三人称 UI 提示：独立遍历 Furniture_Attached.list 找最近，与 PlayerOperationController.target 不同步。改为直接使用 `playerOp.target` 保持一致。

## 修复记录（2026-07-26）
- BaseSelfMoveableController.cs 陡坡卡死 bug：坡度超过 slopeLimit 时 CapsuleCast 能检测到地面但 IsNormalUnderSlopeLimit 返回 false，IsGrounded=false → AirMove() 加重力 → CharacterController.Move() 把陡坡当墙阻止移动，角色既不下落也不能移动。修复：1) 新增 `_isOnSteepSlope` 标志字段；2) GroundCheck() 中陡坡分支设置标志；3) HandleCharacterMovement() 中 Move() 前将位移用 Vector3.ProjectOnPlane 投影到坡面法线平面，使重力重定向为沿坡面滑下。

## 项目全景总结（2026-08 调研，2026-08-07 修正）
> 详细版见 `.codebuddy/skills/bluedivers-unity/references/module-guide.md` 末尾「项目全景总结」章节。

**已实现（核心战斗层扎实）**：伤害模型（弱点/多部位护甲/关联护甲/护盾/抗性）、属性系统（Modifier 叠加/双属性链）、武器体系（30+ 脚本，多伤害类型/过热/蓄力/升级模块）、投射物、玩家（视角切换/倒地呼叫/载具/喷气背包/护盾背包）、AI（探测/状态机/技能/BOSS）、波次（`WaveManager` Zerg/Robot，人口值权重刷怪，已拆分为 3 文件）、`UnitQueryGrid` 空间网格、平坦噪声地形（绝地潜兵式 fBm+植被+NavMesh）、任务系统（主/支/巢穴/撤离）、昼夜循环 + 夜间敌袭、**战备全类别**（空投/轨道打击/飞鹰/SOS/探照灯/照明弹）、**战略大地图**（`MapData_SO` + `SelectMapWnd` + `ArchivesData_SO`，单机值）、撤离（类似 DRG 所有人上船起飞，无撤离失败惩罚）。

**未实现（对标两款游戏）**：
- 绝地潜兵：机甲/哨戒/炮塔等重型战备（战备类别已全但缺重型部署类）、大战略任务链（无解放度进度推进）、联机（当前单机 demo）
- 深岩银河：洞穴/矿洞体素系统（地图是平坦地形，不做挖掘）、矿物转运货运链（规划中：给 `SpecUnitKei` 加 furniture + 玩家 OOPart 拾取组件）
- 通用：Roguelite 成长闭环待确认

**规划中的系统**：
- 矿物提交：给 `SpecUnitKei` 添加 furniture 交互 + 玩家添加 OOPart 拾取组件（有携带上限），必须交给 Kei
- 联机：KCPNet 未完成，暂不迁移；目标为轻服务器（仅房间列表）+ 本地存储
- 波次规模维持现状（担心单位多了确定性同步不一致）

**架构改进（按优先级）**：① 统一确定性随机源（`BattleRandom` 与 `WaveManager` 的 `System.Random` 收口）② `I_Damagable`/`I_Entity` 与 Unity 类型解耦为纯逻辑 DTO ③ 统一命名空间 + asmdef rootNamespace ④ 重写 SingletonNet RPC 封装（`nameof(action)` 取到参数名，未跑通）⑤ God Class 审查（`BattleManager`/`PlayerController`/`WeaponPlayerController`）⑥ `UnitQueryGrid.FindUnits` List 每帧分配改池化。

## 可复用编辑器基础设施（优先复用，勿重复造轮子）
- **SO 选择弹窗**：`Assets/Editor/SOPickerPopup.cs` 的 `SOPickerPopup<T>`（全局命名空间，`PopupWindowContent`）。凡是要做"从一堆数据里选一个并回调"的可搜索列表弹窗（选 SO/预制体/子资源等），**优先用** `PopupWindow.Show(rect, new SOPickerPopup<T>(items, onPick, getIcon, getName, getType?, getTypeColor?, getFrame?, confirmMode))`，不要再自实现。
  - `confirmMode=false`（默认）单击即回调关闭（武器升级/模组选择）；`confirmMode=true` 单击选中+确定/取消确认（speechGroups 子资源选择）。
  - 数据过滤/子资源筛选由调用方注入前完成（如 `LoadAllAssetsAtPath(...).OfType<SoundGroup_SO>()`）。
  - 现有调用方：`WeaponUpgradeEditorWindow.cs`、`RoleSpeechGroupDrawer.cs`。详细见 skill 的"复用约定：SO 选择弹窗"。

## 文档与 Skill
- `.codebuddy/rules/UnityCSharp编码规范.md`：C# 编码规范（必须/推荐/可选三级），自动加载为项目规则
- `.codebuddy/skills/bluedivers-unity/`：项目专用 skill，含 SKILL.md（架构约定、命名速查、开发工作流、KCP 网络约定、避坑指南、关键基础设施表、项目定位）和 references/module-guide.md（各模块详细职责、关键类清单、项目全景总结）。处理项目 C# 代码时自动触发
