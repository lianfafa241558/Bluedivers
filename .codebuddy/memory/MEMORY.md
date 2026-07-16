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
- Inspector 中文化：广泛使用 `[InspectorName("中文")]`
- 枚举后缀不统一（`GameStateEnum` vs `ActorState`）
- 协程命名以动词开头（`InitGameState`/`WaitSetPos`），不用 `Coroutine` 后缀
- 几乎不用 async/await，异步全用协程

## 已知问题
- 运行时脚本误引 UnityEditor：`00Core/ObjectPool.cs`（`using static UnityEditor.Progress;`）、`02Data/` 下多个 `_SO` 脚本 —— 非 Editor 平台会编译失败，需清理

## 修复记录
- ModifyTerrain.cs：根物体悬浮 bug — Modify() 协程中先在旧地形高度贴地，再 yield return ModifyHeightMap 修改地形，地形改变后根物体 Y 未更新。修复：在地形修改和 AdditionTerrain 完成后重新采样高度并贴地。
- WaveManager.cs：CreatUnit 访问 `TierItemWeight[tier]` 时，若 tier（如 Giant）在当前模板配置中不存在，抛出 KeyNotFoundException。修复：改用 TryGetValue + FirstOrDefault 降级兜底。
- ObjectPool.cs：`AutoObjectPool<K,V>.Release(K key)` 错误调用 `_Pop` 而非 `_Push`，导致 HpWnd 血条回收时 `SetActive(false)` 不执行，血条 UI 卡在屏幕上不消失。修复：`_Pop?.Invoke(item)` → `_Push?.Invoke(item)`。

## 协作偏好
- 遇到不确定/有疑问的情况时，应先暂停、总结当前状态并向用户询问，而不是自行做大量全局搜索去推断。优先问清楚再行动。
- 输出代码时不要主动纠结/移除 using 语句。用户会自行管理 using 的增删，无需助手操心。

## 文档与 Skill
- `.codebuddy/rules/UnityCSharp编码规范.md`：C# 编码规范（必须/推荐/可选三级），自动加载为项目规则
- `.codebuddy/skills/bluedivers-unity/`：项目专用 skill，含 SKILL.md（架构约定、命名速查、开发工作流、Photon 约定、避坑指南、关键基础设施表）和 references/module-guide.md（各模块详细职责与关键类清单）。处理项目 C# 代码时自动触发
