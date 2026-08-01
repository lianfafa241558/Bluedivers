# Bluedivers 项目长期记忆

## 项目环境
- Unity 2022.3.62f2c1（2022 LTS），URP 14.0.12，C# 9.0 / .NET Standard 2.1
- 依赖：Photon PUN、TextMeshPro 3.0.9、Navigation 1.1.6、Timeline 1.7.7、ugui
- 产品名 BlueDivers，companyName GameDevelopmentDepartmentFaFa

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

## 已知问题
- 运行时脚本误引 UnityEditor：`00Core/ObjectPool.cs`（`using static UnityEditor.Progress;`）、`02Data/` 下多个 `_SO` 脚本 —— 非 Editor 平台会编译失败，需清理

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

## 文档与 Skill
- `.codebuddy/rules/UnityCSharp编码规范.md`：C# 编码规范（必须/推荐/可选三级），自动加载为项目规则
- `.codebuddy/skills/bluedivers-unity/`：项目专用 skill，含 SKILL.md（架构约定、命名速查、开发工作流、Photon 约定、避坑指南、关键基础设施表）和 references/module-guide.md（各模块详细职责与关键类清单）。处理项目 C# 代码时自动触发
