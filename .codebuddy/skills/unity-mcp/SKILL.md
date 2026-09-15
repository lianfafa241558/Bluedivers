---
name: unity-mcp
description: 通过 MCP 直接操控 Bluedivers 的 Unity 编辑器 —— 创建/修改 GameObject、场景、Prefab、材质、资产、ScriptableObject，控制 Play 模式、截图、读取 Console、执行编辑器 C#、跑测试。当任务需要「看或改 Unity 编辑器里的真实状态」而不只是编辑文本文件时使用，例如：核对 prefab 层级与 Inspector 字段、批量校验 SO 资产、进 Play 模式验证表现/截图、读取编译报错、批量改资产。也用于排查 MCP 连接问题（No Unity Editor instances found / 桥未启动）。
---

# Unity MCP（MCP for Unity）操控指南

本项目已接入 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)，可用 40+ 工具**直接操作运行中的 Unity 编辑器**：建改 GameObject / 场景 / Prefab / 材质 / 资产 / SO，切 Play 模式、截图、读 Console、执行编辑器 C#、跑测试。

一个 Unity 原生 AI 助手做不了、但有了这层桥就很轻松的事：**读 Inspector 里真实配好的数值**（而不是猜 YAML）、**批量体检资产交叉引用**、**进 Play 模式截图验证表现**。

## 前置条件（不满足则所有工具都会失败）

1. **Unity 编辑器必须开着并打开本工程**（`E:\Bluedivers`）。
2. **Unity 侧传输必须设为 `Stdio`**：菜单 `Window → MCP for Unity` → **Connection** 区域 → Transport = `Stdio`。
   - 原因（源码级）：v10 的 `EditorConfigurationCache.UseHttpTransport` **默认为 true**（走 HTTP），而 `StdioBridgeHost.ShouldAutoStartBridge()` 返回 `!useHttpTransport` —— 即 **HTTP 模式下 stdio 桥不会自启**；同时 `AutoStartOnLoad` 默认为 **false** 且只对 HTTP 生效。默认配置下 Unity 端两个桥都不起，客户端必然报「找不到实例」。
   - 切成 Stdio 后，桥随编辑器自动启动，**无需每次手点**。
3. Unity 侧 stdio 桥监听 `127.0.0.1:6400`，并把发现文件写到
   `%USERPROFILE%\.unity-mcp\unity-mcp-status-<projectHash>.json`。

## 连通性自检（怀疑断了先跑这三步）

| 层次 | 检查方式 | 期望 |
|---|---|---|
| 客户端 ↔ Python server | 资源 `mcpforunity://project/info` | 返回 `projectRoot: E:/Bluedivers` |
| Python server ↔ Unity | 资源 `mcpforunity://editor/state` | `unity.instance_id` 非 null、`advice.ready_for_tools: true` |
| Unity 桥端口 | `Get-NetTCPConnection -State Listen \| ? LocalPort -eq 6400` | 有 Unity 进程 PID 监听 |

- 报 **`No Unity Editor instances found`** = Unity 侧桥没起 → 回去检查 Transport 是否为 Stdio / 重开 Unity。
- `%USERPROFILE%\.unity-mcp` 目录不存在 = 桥**从未**启动过（不是暂时断线）。
- 本机实例标识：**`Bluedivers@3d9f2357`**；多实例时用 Python server 的 `--default-instance` 指定。

## ⚠️ 写操作会真实改工程 —— 动手前先 commit

这些工具**直接落盘**修改 `Assets/`（场景 / prefab / SO / 脚本 / 材质），没有沙盒、没有预览：

- 调任何**写**工具之前，确认工作区已 `git commit`（至少 `git stash` 存档）。
- `execute_code` 能在编辑器里执行**任意 C#**；`manage_editor` 能切 Play 模式、执行菜单项、Undo/Redo。权限极大，只做想清楚了的事。
- 写操作会让场景变 `isDirty`。回滚优先用 `manage_editor` 的 undo，必要时 `git checkout`。
- 开工前先看一眼场景是否已经 dirty：`manage_scene(action="get_active")` 的 `isDirty`。

## 工具分类（只读 / 会写）

**只读安全**
- `read_console`(action=get)、`manage_scene`(get_active / get_hierarchy / get_loaded_scenes / get_build_settings / scene_view_frame)
- `find_gameobjects`、`find_in_file`、`get_sha`、`unity_reflect`、`unity_docs`、`manage_script_capabilities`、`manage_tools`、`manage_profiler`
- `manage_asset` 的 search 类动作
- 全部 `mcpforunity://*` 资源（`editor/state`、`project/info`、`scene/cameras`、`scene/volumes`、`project/layers`、`project/tags`、`rendering/stats`、`pipeline/renderer-features`、`editor/windows`、`editor/selection`、`tests`、`instances` …）

**会写工程**
- 对象/场景：`manage_gameobject`、`manage_components`、`manage_prefabs`、`manage_scene`(create / load / save / close_scene / set_active_scene / move_to_scene / validate)
- 资产/数据：`manage_asset`(create / modify / delete)、`manage_scriptable_object`、`manage_material`、`manage_shader`、`manage_texture`、`manage_animation`、`manage_vfx`、`manage_ui`、`manage_camera`、`manage_graphics`、`manage_physics`、`manage_probuilder`
- 脚本：`manage_script`、`create_script`、`delete_script`、`script_apply_edits`、`apply_text_edits`、`validate_script`
- 编辑器/构建：`manage_editor`、`execute_code`、`execute_custom_tool`、`execute_menu_item`、`manage_build`、`manage_packages`、`run_tests`、`refresh_unity`
- 批量：`batch_execute`

> **优先 `batch_execute`**：一次连发多条命令（本工程上限 25，见 `EditorPrefs` 的 `MCPForUnity.BatchExecute.MaxCommands`），比逐条调用快得多，也少踩中间态竞态。

> **工具分组**：部分工具（profiling / vfx / animation / ui / testing 等）属于可开关的 tool group，会话里不显示时用 `manage_tools` 激活。

## 拿来干什么最划算

1. **核对 prefab 层级与 Inspector 真实数值**
   项目大量依赖「子物体下标约定」与 `[SerializeField]` 引用：`ArmamentButton.prefab` 子0=icon、**子1=偏好标记**；`ArmamentWnd.armamentRoot` 的 GetChild 约定；`WndManager` 上的 `public XxxWnd` 字段在 `GameRoot.prefab` 里填 0。
   → `find_gameobjects` 拿到 instance id，再读 `mcpforunity://scene/gameobject/{id}/components`，比翻 YAML 靠谱得多。

2. **批量校验 SO 资产交叉引用**
   扫 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`：`subAirdrop` 指向的战备 ID 是否存在、`coolGroup` 是否配错、`isHide` 是否漏配。用 `manage_scriptable_object` 读改。

3. **进 Play 模式看真机表现**
   `manage_editor` 切 Play → `manage_camera(action="screenshot")` 截图 → 验证渲染 / UI / 展示模型（例如 `AirdropConfigWnd` 那套「禁用全部 MonoBehaviour + Collider、Rigidbody 置 kinematic、独立相机 + RenderTexture」是否真的生效）→ 退出 Play。

4. **编译/报错闭环**
   `read_console(types=["error","warning"])` 直接拿编译结果，不要让用户手抄 Console。改完脚本后配合 `refresh_unity` + `validate_script`。

5. **资产体检**
   `manage_asset(search)` 找丢失引用、找孤儿资产（历史上 `Feature/Fog` 删除后遗留的 `Shader/Feature/FogEffect.shader`、`Setting/Feature/Fog.mat` 就是这种）。

## 本项目注意事项（踩坑点）

- **`Packages/com.coplaydev.unity-mcp/` 是本地嵌入包，且已被汉化**：不要用 `manage_packages` 去升级/卸载它（会冲掉汉化，见文末「升级」）。包 Editor 窗口里的中文文案是**有意为之的成果**，别当成脏数据"清理"回去。
- **SO 不能持有 prefab / 场景实例引用**：用 `manage_scriptable_object` 往 SO 里塞引用时，只能塞**资产**（材质、粒子预制体、音效、颜色）。子物体 / 挂点这类实例层级引用必须留在 prefab 组件的 `[SerializeField]` 里。
- **给 SO 加字段后**：若该 SO 有专属 Editor（如 `AirdropData_SOEditor`、`CampData_SODrawer`），新字段需显式补 `DrawField("字段名")`，否则检视器和数据编辑器里都不显示。
- **`[InspectorName]` / `[DisplayField]` 只对字段（field）有效**，不能加到属性上；且 `[DisplayField]` 是 PropertyDrawer，只对"已序列化字段"生效（私有字段还得配 `[SerializeField]`）。
- **新脚本的落位**：`Assets/Scripts/` 是数字前缀分层（`00Core → 00GameContract → 00Tools → 01Manager → 02Data → 02Game → 04UI → 08Map`），跨层引用受 asmdef 约束。`manage_script(action="create")` **不会**帮你判断归属 —— 建完脚本自己核对 asmdef，别让下层被上层引用。
- **窗口注册范式**：新窗口要在 `WndManager` 上挂 `public XxxWnd` 字段（`GameRoot.prefab` 里填 0），由窗口自己的 `Init()` 赋值，而 `Init()` 挂在**场景/prefab 的 UnityEvent** 上（参见 `Assets/Scene/Utnapishitim.unity`）。用 `manage_components` 配窗口时按这个套路走。
- **不要通过 MCP 把运行态调试值持久化进资产**：`RenderSettings.*` 引用的资产对象（如天空盒材质）、MPB 之类的运行时数据一旦写盘，编辑器 Play 会持久化 → 跨局累积衰减。要改就在运行时改副本。
- **UI 展示模型通用做法**：实例化 prefab → 禁用全部 MonoBehaviour + Collider、Rigidbody 置 kinematic → `SetChildLayer(layer, true)`；独立相机 + 运行时 `RenderTexture` 预览，拖动判定用 `RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos, Overlay ? null : canvas.worldCamera)`。

## 常用调用范例

```jsonc
// 读活动场景（含 isDirty）
{ "action": "get_active" }                       // manage_scene

// 找物体 → 再读组件
{ "search_term": "ArmamentButton", "search_method": "by_name", "include_inactive": true }   // find_gameobjects
// 然后用资源 mcpforunity://scene/gameobject/{id}/components 取组件数值

// 读编译错误
{ "action": "get", "types": ["error", "warning"], "count": "20" }   // read_console（count 传字符串兼容性最好）

// 截图（PID/相机名按实际填）
{ "action": "screenshot" }                       // manage_camera

// 批量执行（上限 25）
// batch_execute：把上面若干条合并成一次调用
```

## 排障速查

| 症状 | 原因 | 处理 |
|---|---|---|
| `No Unity Editor instances found` | Unity 桥没起 | 检查 `Window → MCP for Unity` 的 Transport = **Stdio**；重开 Unity |
| 同上，且 `~/.unity-mcp` 目录不存在 | 桥从未启动过 | 同上（说明是配置问题，不是断线） |
| `editor/state` 字段全 null | 同上 | 同上 |
| Unity 侧 Transport 是 HTTP | 默认值，两边不匹配 | 切 Stdio（推荐）或客户端改直连 `http://127.0.0.1:8080/mcp` |
| 工具调用超时 | 长任务 + 帧 I/O 超时 | 可用环境变量 `UNITY_MCP_STDIO_COMMAND_TIMEOUT_MS` 调整（默认 300000） |

## 升级 unity-mcp（当前 v10.2.1-beta.6）

> 🛑 **升级前必读 —— 本包的汉化会被冲掉**
>
> 本包的 Editor 窗口**已被全面汉化**（2026-09-15）：11 个 UXML、主窗口 / Setup / EditorPrefs 窗口、6 个 Section 控制器、HealthStatus 常量、23 个客户端配置器的安装步骤、Windows 依赖检测。**汉化未纳入 git**（整个包目录 untracked）。
>
> 所以：**覆盖式更新 = 汉化全丢**。正确姿势是
> 1. 先备份 `Packages/com.coplaydev.unity-mcp/Editor/`（或 `git add -f` 把汉化提交进版本库）；
> 2. 再用新版覆盖；
> 3. 最后把汉化 diff 回灌（没有版本控制就只能用目录 diff 手工合）。
>
> 同理：**只做汉化时不要动上游文件结构**，只改 UXML/USS/文本，方便下次三方合并。

因为是本地嵌入包（GitHub 在国内不稳），升级走镜像覆盖：

```powershell
# 1. 经镜像拉 beta 分支 zip
Invoke-WebRequest -Uri "https://ghproxy.net/https://github.com/CoplayDev/unity-mcp/archive/refs/heads/beta.zip" -OutFile "$env:TEMP\unity-mcp.zip"
# 2. 解出 MCPForUnity，覆盖到 Packages/com.coplaydev.unity-mcp
tar -xf "$env:TEMP\unity-mcp.zip" -C "$env:TEMP\um" "unity-mcp-beta/MCPForUnity"
# 3. 回 Unity 等重编译（Console 无报错即可）
```

`Packages/manifest.json` 里对应两行（不要删）：
`"com.coplaydev.unity-mcp": "file:com.coplaydev.unity-mcp"`、
`"com.unity.nuget.newtonsoft-json": "3.0.2"`（后者是硬依赖，原项目没有）。

**卸载**：删 `Packages/com.coplaydev.unity-mcp` 目录 + 去掉上述两行。
