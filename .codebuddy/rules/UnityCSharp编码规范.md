---
description: 
alwaysApply: true
enabled: true
updatedAt: 2026-07-01T14:40:13.732Z
provider: 
---

# Bluedivers 项目 Unity C# 编码规范

> 本规范基于项目实际代码风格调研制定，等级定义如下：
> - **【必须】**：代码必须遵循，违反即视为缺陷
> - **【推荐】**：代码应当遵循，特殊情况下可例外并注明原因
> - **【可选】**：可参考，按具体情况决定是否采用
>
> 项目环境：Unity **2022.3.62f2c1**（2022 LTS），URP **14.0.12**，C# 9.0 / .NET Standard 2.1，使用 Photon PUN、TextMeshPro、Navigation。

---

## 1. C# 版本与 Unity 环境规范

- **【必须】** 使用当前 Unity 版本支持的 C# 版本（本项目 Unity 2022.3 LTS 支持 C# 9.0 / .NET Standard 2.1），不使用更高版本独占语法（如 `record`、`init`、顶级语句、`required` 等）。
- **【必须】** 不使用实验性 Unity 包或预览版 API。当前生产依赖以 `manifest.json` 中稳定版本为准（URP 14.0.12、TMP 3.0.9、Navigation 1.1.6 等）。
- **【推荐】** 了解目标平台（PC/移动端/主机）的 API 兼容性限制，尤其涉及 IL2CPP、AOT 的反射/动态代码限制。
- **【必须】** 避免使用 Unity API 文档中标记为过时（Obsolete）或遗留的功能，编译警告中的 Obsolete 警告必须处理。

## 2. 脚本文件与结构规范

- **【必须】** 每个 `MonoBehaviour`/`ScriptableObject` 脚本文件只包含一个主类定义（伴生的轻量私有内部类/枚举可接受）。
- **【必须】** 文件名必须与主类名完全一致（包括大小写）。
- **【必须】** 脚本文件编码统一使用 UTF-8 with BOM。
- **【必须】** 遵循项目既有的**数字前缀编号 + 功能**目录划分约定，编号体现程序集依赖顺序：
  - `00Core/` 核心框架（单例、对象池、FSM、定时器、接口、特性）
  - `00GameContract/` 游戏契约层（接口、共享数据结构）
  - `00Tools/` 工具类
  - `01Manager/` 全局管理器（GameRoot、ResSvc、Battle 等）
  - `02Data/` ScriptableObject 数据定义
  - `02Game/` 游戏逻辑（Player、AI、Gameplay、Interactable、Mission 等）
  - `04UI/` UI 窗口与工具
  - `08Map/` 地图生成
  - `Effect/`、`Feature/`、`Rendering/` 视觉与渲染
  - 新增模块按依赖层级选择编号，底层在前、上层在后。
- **【必须】** 跨层依赖只能由上层指向下层（编号大的可引用编号小的，反之禁止），通过 asmdef 程序集定义强制约束。
- **【推荐】** 为每个 asmdef 设置 `rootNamespace`，命名空间与程序集/目录对应（目前仅 `00_Core` 设置了 `rootNamespace = Core`，其余应补齐统一）。
- **【必须】** 编辑器脚本放入 `Editor/` 文件夹，运行时脚本不得引用 `UnityEditor` 命名空间。
- **【必须】** 仅编辑器使用的代码必须用 `#if UNITY_EDITOR` 预处理指令包裹。
- **【推荐】** 文件夹按功能/模块进一步细分，避免单目录堆积过多文件。

> 项目现状提醒：当前存在运行时脚本误引 `UnityEditor` 的情况（如 `00Core/ObjectPool.cs` 的 `using static UnityEditor.Progress;`、`02Data/` 下多个 `_SO` 脚本 `using UnityEditor`），会导致非 Editor 平台编译失败，需逐步清理。

## 3. 命名规范

### 3.1 通用命名

- **【必须】** 类型/类名使用帕斯卡命名法（PascalCase）。
- **【必须】** 公共方法名、私有方法名使用帕斯卡命名法。
- **【必须】** 公共字段/属性名使用帕斯卡命名法。
- **【必须】** 私有/受保护字段名使用驼峰命名法，以下划线开头（如 `_controller`、`_health`）。项目历史代码中存在的 `m_` 前缀属遗留写法，新代码统一用 `_` 前缀。
- **【必须】** 局部变量名、参数名使用驼峰命名法。
- **【必须】** 常量名（`const`/`static readonly`）使用帕斯卡命名法。
- **【必须】** 枚举类型名使用帕斯卡命名法，枚举值使用帕斯卡命名法。
- **【必须】** 接口名以 `I` 前缀开头（带下划线），使用帕斯卡命名法，与项目主流约定一致（如 `IRecyclable`、`IPhysical`、`IEquippable`、`IDrivable` ）。
  - 例外：历史代码中少量标准 `I_` 前缀接口（`I_Entity`、`I_Actor`、`I_Damagable`、`I_GlobaManager`、`I_TickClass`等）暂不强制重构，但**新增接口必须使用 `I` 前缀**。
- **【推荐】** 使用有意义且自文档化的名称，避免无意义缩写。
- **【推荐】** 枚举类型命名统一是否添加 `Enum` 后缀。项目当前两种写法并存（`GameStateEnum`/`ActorState`），新代码建议**不加 `Enum` 后缀**，存量按模块逐步统一。

### 3.2 Unity 特有命名

- **【必须】** 序列化字段遵循私有字段命名规范（`_` 加驼峰），用 `[SerializeField]` 暴露。
- **【推荐】** 组件/资源类型字段在命名中包含类型信息（如 `_animator`、`_renderer`、`_audioSource`）。
- **【推荐】** Unity 事件回调方法命名以 `On` 开头（如 `OnEnable`、`OnDeath`、`OnHealthChanged`）。
- **【可选】** 协程方法命名以动词开头描述行为（如 `InitGameState`、`WaitSetPos`、`DelayedRelese`），项目主流**不**使用 `Coroutine` 后缀，故不强制该约定。

### 3.3 文件与资源命名

- **【必须】** 场景文件名、资源文件名（预制体、材质、动画等）使用帕斯卡命名法。
- **【必须】** ScriptableObject 脚本与资产文件使用 `_SO` 后缀（如 `RoleData_SO`、`SoundGroup_SO`、`NoticeTree_SO`、`ArchivesDataBase_SO`）。
- **【必须】** partial 类的分部文件命名使用下划线分隔（而非点 `.`），格式为 `主类名_分部名.cs`（如 `Health_AboState.cs`、`WeaponController_Upgrade.cs`），与主类文件 `Health.cs` 同目录放置。
- **【推荐】** Shader 文件命名使用帕斯卡命名法，Shader 内变量名使用驼峰命名法。

## 4. 类与继承规范

### 4.1 类设计

- **【必须】** 纯数据容器使用 `struct` 或可序列化 `class`，并标注 `[Serializable]`。
- **【推荐】** 使用 `ScriptableObject` 管理共享数据和配置（项目 `02Data/` 已广泛采用）。
- **【推荐】** 组合优于继承，避免过深的继承层次。
- **【推荐】** 不需要被继承的类使用 `sealed` 关键字。

### 4.2 访问修饰符
- **【必须】** `const`常量和`static`静态变量要在类顶部
- **【必须】** 明确指定所有成员的访问修饰符（`public`/`private`/`protected`/`internal`）
- **【推荐】** 默认使用 `private`，仅在必要时开放为 `public` 或 `protected`。
- **【推荐】** 需要在 Inspector 暴露的字段**优先使用 `[SerializeField] private`**，而非 `public` 字段。
  - 项目现状：`public` 字段与 `[SerializeField] private` 并存且 `public` 字段较多。**新增字段优先用 `[SerializeField] private`**；存量 `public` 字段在重构时逐步迁移，避免无理由新增 `public` 字段。
- **【推荐】** 使用 `[InspectorName("中文名")]` 为 Inspector 字段提供中文显示名（项目已有约定，如 `Actor.cs`、`Damageable.cs`），保持 UI 字段可读。
- **【必须】** `[InspectorName]` 和 `[DisplayField]` 特性**仅对字段（field）声明有效**，不能用于属性（`get; set;` / `get; private set;`）。若属性需要在 Inspector 中显示，应改为 `[SerializeField] private` 字段 + 属性封装。

### 4.3 生命周期方法

- **【必须】** `Awake`、`Start`、`Update` 等 Unity 生命周期方法使用 `private` 访问修饰符（可不显式写出，但不得标为 `public`/`protected`）。
- **【必须】** `Awake` 中获取组件引用，`Start` 中执行依赖其他对象的初始化。
- **【推荐】** 避免在 `Update`/`FixedUpdate` 中使用 `Find`、`GetComponent` 等耗时操作，引用应在 `Awake`/`Start` 中缓存。
- **【推荐】** 仅在确有需要时声明 `Update`/`FixedUpdate`/`LateUpdate`，不写空方法体。

## 5. 方法与函数规范

- **【推荐】** 方法保持单一职责，长度过长时拆分为 `private` 方法。
- **【推荐】** 方法参数不超过 5 个，过多时封装为结构体或类。
- **【必须】** 使用 `ref`/`out` 参数时必须有明确理由（性能或需多值返回）。
- **【推荐】** 优先使用返回值而非 `out` 参数。
- **【必须】** 异步逻辑**优先使用协程**（`IEnumerator` + `StartCoroutine`），与项目主流一致。项目当前几乎不使用 `async`/`await`。
- **【可选】** 若确需 `async`/`await`（如网络请求、UniTask），遵循 Unity 最佳实践，避免 `async void`（事件处理器除外），并注意 `CancellationToken` 的传递。
- **【必须】** 可能抛出异常的方法在 XML 注释或注释中说明异常条件。

## 6. Unity 特有规范

### 6.1 性能相关

- **【必须】** `Update`/`FixedUpdate`/`LateUpdate` 中避免使用 `new` 分配内存（包括闭包、临时集合、字符串拼接）。
- **【必须】** `Update`/`FixedUpdate`/`LateUpdate` 等高频调用方法中**禁止**使用 `GetComponent`、`FindObjectOfType`、`Find` 等查找操作，引用应在 `Awake`/`Start` 中缓存。
- **【必须】** 频繁调用的方法中避免字符串拼接，使用 `StringBuilder` 或字符串缓存。
- **【推荐】** 频繁创建/销毁的对象使用对象池（项目 `00Core/ObjectPool<T>` 已提供基础设施）。
- **【推荐】** 标签比较使用 `CompareTag` 而非字符串相等比较。
- **【推荐】** 缓存组件引用，避免重复 `GetComponent`。
- **【推荐】** 大数组/列表遍历使用 `for` 循环而非 `foreach`，减少迭代器分配。
- **【推荐】** 避免在 Inspector 暴露不必要的字段以减少反射开销。

### 6.2 Editor 与运行时分离

- **【必须】** 使用 `#if UNITY_EDITOR` 预处理指令将编辑器代码与运行时代码隔离。
- **【必须】** 运行时程序集（非 `Editor` 目录下的脚本）**不得** `using UnityEditor` 或调用 Editor 专属 API，否则将导致非 Editor 平台构建失败。
- **【推荐】** 自定义 Inspector / PropertyDrawer 放入对应模块的 `Editor/` 或 `Editor/Drawer/` 文件夹下。
- **【推荐】** 使用 `ExecuteInEditMode`/`ExecuteAlways` 时，编辑器专属逻辑用 `#if UNITY_EDITOR` 保护。

## 7. 数据与序列化规范

### 7.1 序列化

- **【必须】** 自定义序列化类/结构体使用 `[Serializable]` 标记。
- **【推荐】** 使用 `[SerializeField]` 显式标记需要序列化的私有字段。
- **【推荐】** 使用 `[HideInInspector]` 隐藏不应在 Inspector 中显示的字段。
- **【推荐】** 使用 `[Range]` 为数值字段添加滑动条约束。
- **【推荐】** 使用 `[Header]`/`[Space]` 对 Inspector 字段分组。
- **【推荐】** 使用 `[InspectorName("中文名")]` 为字段提供中文 Inspector 显示名（项目已有约定）。

### 7.2 ScriptableObject

- **【推荐】** 使用 `ScriptableObject` 定义游戏配置数据，统一放在 `02Data/` 下，文件名使用 `_SO` 后缀。
- **【推荐】** `ScriptableObject` 实例作为共享配置资源使用，运行时通过资源加载（`ResSvc`）获取。
- **【必须】** `ScriptableObject` 中的运行时状态不应持久化回资产文件（编辑器下 `EditorUtility.SetDirty` 仅限编辑器代码）。

## 8. 网络与 Photon（可选）

- **【可选】** Photon PUN 的 RPC 方法使用 `[PunRPC]` 标记，方法名使用帕斯卡命名法，建议以 `Rpc` 前缀或 `On` 前缀区分（如 `RpcSyncHealth`）。
- **【可选】** 跨网络同步的字段需评估带宽，避免每帧同步大对象，优先使用事件驱动同步。
- **【可选】** 网络相关逻辑与本地逻辑解耦，便于离线测试。

## 9. 注释与文档（推荐）

- **【推荐】** 公共 API 使用 XML 文档注释（`///`）说明用途、参数、返回值。
- **【推荐】** 复杂算法或非直观逻辑添加行内注释说明意图（why，而非 what）。
- **【推荐】** TODO 使用 `// TODO: 描述` 格式，便于全局检索。
- **【必须】** 提交前移除被注释掉的死代码或无意义注释。

---

## 附：与原草稿的主要调整说明

| 调整项 | 原草稿 | 调整后 | 依据 |
|--------|--------|--------|------|
| 接口前缀 | 标准 `I` 前缀 | `I_` 带下划线前缀（项目主流） | 项目主流接口为 `I_Entity`/`I_Actor`/`I_Damagable` 等 |
| 字段暴露 | 【必须】用 `[SerializeField] private` 替代 public | 【推荐】优先 `[SerializeField] private`，存量 public 逐步迁移 | 项目大量使用 public 字段，强 制迁移成本过高 |
| 协程命名 | 以 `Coroutine` 结尾 | 不强制，动词开头即可 | 项目几乎无 `Coroutine` 后缀命名 |
| 异步 | async/await 遵循最佳实践 | 优先协程，async/await 为可选 | 项目以协程为主，几乎不用 async/await |
| 目录结构 | 按功能/模块划分 | 数字前缀编号 + 功能（项目强约定） | 项目 `00Core`/`01Manager`/`02Game`/`04UI`/`08Map` 等 |
| ScriptableObject | 通用建议 | 明确 `_SO` 后缀约定 | 项目 `RoleData_SO`/`SoundGroup_SO` 等已遵循 |
| Editor 隔离 | 通用 | 强调运行时不得 using UnityEditor | 项目存在运行时误引 UnityEditor 的实际问题 |
| Inspector 中文化 | 无 | 纳入 `[InspectorName("中文")]` 约定 | 项目已广泛使用该特性 |
8. 事件与委托规范
【必须】在OnDisable或OnDestroy中取消所有事件订阅
【推荐】使用Action/Func委托时考虑GC影响
【推荐】使用弱引用事件模式避免内存泄漏
【必须】UnityEvent字段使用帕斯卡命名法

9. 注释规范
【必须】公共方法、公共API需要有XML文档注释
【推荐】复杂算法或非直观逻辑添加行内注释
【推荐】使用TODO标记待完成工作
【推荐】使用Tooltip属性为Inspector字段添加提示
【推荐】使用InspectorName属性为Inspector字段添加中文标签


10. 格式规范
【必须】大括号另起新行
【必须】每行代码不超过120个字符
【必须】使用using语句管理资源释放
【推荐】使用表达式体方法简化单行方法
【推荐】使用字符串插值替代字符串拼接

11. 资源管理规范
【必须】在OnDestroy中释放非托管资源
【必须】动态加载的AssetBundle在不再使用时调用卸载方法

12. 设计模式与架构规范
【推荐】使用MVC/MVP/MVVM模式组织UI代码
【推荐】使用单例模式时谨慎，考虑使用ScriptableObject或Service Locator
【推荐】使用事件驱动架构降低耦合
【推荐】使用工厂模式创建复杂游戏对象
【推荐】使用状态机管理游戏状态/角色状态
【必须】避免God Class，遵循单一职责原则

13. 版本控制规范
【必须】Library/、Temp/、Logs/、Build/目录不提交版本控制

14. 测试规范
【推荐】核心游戏逻辑编写单元测试
【推荐】关键交互流程编写PlayMode测试
【推荐】在PlayMode测试中使用Setup和TearDown方法
【必须】测试代码不污染正式游戏数据

15. 性能分析规范
【推荐】使用Profiler定期分析性能瓶颈
【推荐】在Editor中开启Deep Profile定位具体问题
【推荐】使用条件编译属性包装调试代码
【必须】发布版本中移除调试日志或控制日志输出