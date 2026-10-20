---
description: Bluedivers Unity C# 编码规范（精简口径；细则与权衡见 memory/topics）
alwaysApply: true
enabled: true
provider: 
updatedAt: 2026-10-20T00:00:00.000Z
---

# Bluedivers C# 编码规范

> 【必须】= 违反即缺陷；【推荐】= 可例外但须注明原因。
> 环境：Unity **2022.3.62f3** / URP **14.0.12** / C# 9 / .NET Standard 2.1。联机走 **KCPNet + NetTmp**，**不用 Photon**。

## 1 目录与程序集

- 【必须】数字前缀 = 依赖顺序，**只许大编号引用小编号**，由 asmdef 强制：
  `00Attributes` `00Core`(框架/接口) → `00GameContract`(契约) → `00Tools` → `04Data`(SO)
  → `05UnitCore` `05_EffectComp` `06Gameplay` `07NetGame` `NetTmp`
  → `08Map` `09Manager` → `10UI` `10_Effect` `02Rendering` `03Audio` `DayNightSystem`
- 【必须】运行时程序集**不得** `using UnityEditor`；Editor 逻辑放 `Editor/` 并用 `#if UNITY_EDITOR` 包裹。⚠ 这类错**只在打包时报 CS0246**，离线编译查不出。
- 【推荐】有 `.asmdef` 的目录补 `rootNamespace`。

## 2 文件与命名

- 【必须】一个 `.cs` 一个主类，文件名 == 主类名（含大小写）；文件编码 UTF-8 **with BOM**。
- 【必须】类型 / 方法 / 属性 / 公共字段 / 常量 / 枚举值 → PascalCase；私有与受保护字段 → `_camelCase`（`m_` 是遗留写法，新代码不用）；局部变量与参数 → camelCase。
- 【必须】接口 = `I` + PascalCase（`IHealth` / `IDrivable` / `IActor`）。存量 `I_Entity` / `I_Login` 这类不强制重构，但**新增一律不用下划线**。
- 【必须】SO 脚本与资产用 `_SO` 后缀；partial 分部文件用 `主类名_分部名.cs`（如 `Health_AboState.cs`），与主类同目录、同文件名前缀。
- 【必须】场景 / 预制体 / 材质 / 动画等资源名 PascalCase。

## 3 类与成员

- 【必须】`const` 与 `static` 字段放类顶部；所有成员显式写访问修饰符；生命周期方法（`Awake`/`Start`/`Update`）不得为 `public`/`protected`。
- 【必须】**`[InspectorName]` / `[DisplayField]` 只对字段有效**，贴到属性（`get; set;`）上无效 —— 要显示就改成 `[SerializeField] private` + 属性封装。
- 【必须】自定义序列化类 / 结构体标 `[Serializable]`。
- 【推荐】Inspector 字段用 `[SerializeField] private` 而非 `public`（存量 public 逐步迁移），配 `[InspectorName("中文名")]`。
- 【推荐】`Awake` 取组件引用，`Start` 做依赖他人对象的初始化；不需要就不声明 `Update`。

## 4 性能（高频路径）

- 【必须】`Update`/`FixedUpdate`/`LateUpdate` 内**禁止** `GetComponent` / `Find` / `FindObjectOfType`，**禁止** `new`（含闭包、临时集合、字符串拼接）—— 引用在 `Awake`/`Start` 缓存。
- 【推荐】频繁创建销毁的对象走 `00Core/ObjectPool<T>`；标签比较用 `CompareTag`；大集合遍历用 `for`。

## 5 兜底克制（2026-10-18 定）

- 【必须】**MCP 已亲手配好的引用直接用**（prefab 已赋值、资产已存在、组件已挂），不再补 `Resources.Load` / `Find` 二级取路径 —— 同一件事只留**一条**取得路径。
- 【必须】前置条件不成立 ⇒ **一条日志 + return / 直接不做**，不写降级链；不给序列化字段写 `?? new`（旧存档兼容除外并注释）。
- 【必须】内部自洽的数据不重复校验：自己刚 `new` 的对象不判空，流程保证的顺序不判"是否已初始化"。
- 【推荐】只对**真不可控输入**判空（玩家输入 / 网络包含不可信 NetId / 文件读写 / 第三方 API / 策划可改的 SO 表），且**失败必须记日志**；必须兜时集中成一个 `ResolveXxx()` 并注明原因。

## 6 异步 / 网络 / 资源

- 【必须】异步优先协程（`IEnumerator` + `StartCoroutine`）；`async void` 仅限事件处理器。
- 【必须】事件订阅在 `OnDisable`/`OnDestroy` 退订；非托管资源与 AssetBundle 在 `OnDestroy` 释放。
- 【推荐】联机逻辑与本地逻辑解耦，同步字段评估带宽、优先事件驱动。细则见 skill `kcpnet-online`。

## 7 注释与格式

- 【必须】注释讲**为什么 + 陷阱 + 别怎么写**，**1~3 行**；长说明与权衡放 `memory/topics/*.md`。
- 【必须】提交前删掉被注释的死代码；调试日志（`NetSyncLog`）默认两端开，发布构建须关闭。
- 【必须】大括号**另起新行**；`Library/` `Temp/` `Logs/` `Build/` 不进 git。
- 【推荐】公共 API 加 XML 注释；行宽 ≤ 120；非直观逻辑才写行内注释。
