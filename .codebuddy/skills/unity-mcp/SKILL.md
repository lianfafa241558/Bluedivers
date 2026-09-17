---
name: unity-mcp
description: 通过 MCP 直接操控运行中的 Unity 编辑器 —— 读写 GameObject/场景/Prefab/材质/资产/ScriptableObject、切 Play 模式、截图、读 Console、执行编辑器 C#、跑测试、生成图片/音频/模型。当任务需要「看或改 Unity 编辑器里的真实状态」而不只是编辑文本文件时使用，例如：核对 prefab 层级与 Inspector 字段真实数值、批量校验 SO 资产交叉引用、进 Play 模式截图验证表现、读取编译报错、批量改资产。也用于排查连接问题（No Unity Editor instances found / 桥未启动 / 会话里看不到 mcp__ 工具）。
---

# Unity MCP（MCP for Unity）操控指南

本项目已接入社区版 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)（Unity 包 `com.coplaydev.unity-mcp`，本地嵌入在 `Packages/`）。它以 MCP 形式把 **35 个工具 + 19 个资源** 暴露给 AI 客户端，**直接操作运行中的 Unity 编辑器**：建改 GameObject / 场景 / Prefab / 材质 / 资产 / SO，切 Play 模式、截图、读 Console、执行编辑器 C#、跑测试。

纯文本编辑器做不了、但有了这层桥很轻松的事：**读 Inspector 里真实配好的数值**（而不是猜 YAML）、**批量体检资产交叉引用**、**进 Play 模式截图验证表现**。

> 完整工具 / 动作 / 资源清单见同目录 **`references/tools-reference.md`**（写调用前先查它，别猜参数）。

---

## 1. 前置条件（不满足则所有工具都会失败）

> 📌 **本工程有两份工作副本**：`D:\Pro\Bluedivers`（一台设备）与 `E:\Bluedivers`（另一台）。
> 本文档一律用 **`<项目根>`** 指代「当前会话所在的那一份」，**任何盘符 / hash / 端口都不要当成固定值**。
> 换设备后，本文涉及的路径、实例 hash、uvx 路径都要按本机实际情况重新核对。

1. **Unity 编辑器开着并已打开本工程**（即当前 `<项目根>`）。
2. **Unity 侧 Transport = `Stdio`**：菜单 `Window → MCP for Unity` → Connection 区域。
   - 原因（源码级）：`EditorConfigurationCache.UseHttpTransport` 默认 **true**（走 HTTP），而 `StdioBridgeHost.ShouldAutoStartBridge()` 返回 `!useHttpTransport` —— **HTTP 模式下 stdio 桥不会自启**；同时 `AutoStartOnLoad` 默认 **false** 且只对 HTTP 生效。默认配置下两个桥都不起，客户端必然报「找不到实例」。
   - 切成 Stdio 后桥随编辑器自动启动，**无需每次手点**。
   - ⚠ 这项设置存在**本机本工程的 EditorPrefs**（`MCPForUnity.*`）里，**不随 git 走** —— 两台设备各设一次。
3. Unity 侧 stdio 桥监听 `127.0.0.1:<port>`（默认 **6400**），并把发现文件写到
   `%USERPROFILE%\.unity-mcp\unity-mcp-status-<projectHash>.json`。
   - `<projectHash>` **由项目绝对路径算出**：D 盘与 E 盘两份副本会生成**两个不同**的发现文件（端口也可能不同）。查的时候用 `unity-mcp-status-*.json` 通配，读里面的 `project_path` 认准是哪一份。
   - 文件内容形如 `{"unity_port":6400,"reason":"ready","project_name":"Bluedivers","unity_version":"2022.3.62f3"}`，即本机当前状态。

---

## 2. 会话里看不到 `mcp__` 工具？（最常见的坑）

**CodeBuddy IDE 与 CodeBuddy CLI 读的是两个不同的配置文件**：

| 客户端 | 配置文件 | 谁写它 |
|---|---|---|
| **CodeBuddy IDE**（我所在的这个） | `%USERPROFILE%\.codebuddy\mcp.json` | 需手动写（或按本技能的既定配置） |
| CodeBuddy CLI | `%USERPROFILE%\.codebuddy.json` | Unity 窗口 → Configure 按钮（`CodeBuddyCliConfigurator`）自动写 |

> ⚠️ Unity 窗口里的「配置 / Configure」按钮走的是 `CodeBuddyCliConfigurator`，**只会写 `~/.codebuddy.json`**。IDE 侧**不会**因此生效 —— 表现为「Unity 里显示已配置，但 AI 一个 `manage_*` 工具都调不出来」。

IDE 侧应包含的条目（对照用；版本号以本机 Unity 窗口生成的结果为准）：

```jsonc
{
  "mcpServers": {
    "unityMCP": {
      "type": "stdio",
      "command": "<本机 uvx.exe 绝对路径>",
      "args": ["--prerelease", "explicit", "--from", "mcpforunityserver>=0.0.0a0",
               "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```

- `command` 必须用**本机**的 uvx 路径，别照抄另一台设备 —— 先 `Get-Command uvx` 确认（典型 `%LOCALAPPDATA%\Programs\Python\Python3xx\Scripts\uvx.exe`）。
- 这份文件**不随 git 走**，两台设备各配一次；`~/.codebuddy.json` 同样是每台机器各自一份。

改完 **需要重启 CodeBuddy 会话 / 重新加载 MCP**，当前会话内不热更。

---

## 3. 连通性自检（怀疑断了先跑这三步）

| 层次 | 检查方式 | 期望 |
|---|---|---|
| Unity 桥端口 | `Get-NetTCPConnection -State Listen \| ? LocalPort -eq <发现文件里的 unity_port>` | 有 Unity PID 监听 |
| Unity 项目身份 | 读 `%USERPROFILE%\.unity-mcp\unity-mcp-status-*.json` | `project_name: Bluedivers`、`reason: ready` |
| 客户端 ↔ Python server | 资源 `get_project_info` | 返回本工程根路径 |
| Python server ↔ Unity | 资源 `get_editor_state` | `unity.instance_id` 非 null、`advice.ready_for_tools: true` |

- 报 **`No Unity Editor instances found`** = Unity 侧桥没起 → 回去检查 Transport 是否为 Stdio / 重开 Unity。
- `%USERPROFILE%\.unity-mcp` 目录不存在 = 桥**从未**启动过（不是暂时断线）。
- 实例标识形如 **`Bluedivers@<projectHash>`**（hash 与发现文件名一致，**两台设备不同**）。同一台机器上同时开着多份工程时，用 Python server 的 `--default-instance` 指定连哪一份。

---

## 4. ⚠️ 写操作会真实改工程 —— 动手前先 commit

这些工具**直接落盘**修改 `Assets/`（场景 / prefab / SO / 脚本 / 材质），没有沙盒、没有预览：

- 调任何**写**工具之前，确认工作区已 `git commit`（至少 `git stash` 存档）。
- `execute_code` 能在编辑器里执行**任意 C#**；`manage_editor` 能切 Play 模式、执行菜单项、Undo/Redo。权限极大，只做想清楚了的事。
- 写操作会让场景变 `isDirty`。回滚优先用 `manage_editor` 的 `undo`，必要时 `git checkout`。
- 开工前先看一眼场景是否已经 dirty：`manage_scene(action="get_active")` 的 `isDirty`。

---

## 5. 工具名前缀

客户端注入后，工具名形如 `mcp__unityMCP__manage_scene`（前缀随客户端/服务器名而定）。本技能正文与参考文档一律**只写工具名后段**（`manage_scene`），按后段识别即可。

部分工具（`manage_vfx` / `manage_animation` / `manage_ui` / `manage_profiler` / `manage_probuilder` / `manage_physics` / `manage_graphics` / `manage_build` / `manage_packages` / `run_tests` / `generate_*` 等）属于**可开关的 tool group**；会话里不显示时，去 `Window → MCP for Unity → Tools` 勾选对应分组再重连。

---

## 6. 只读 vs 会写（安全分级）

**只读安全**
- `read_console`、`find_gameobjects`、`manage_script(action="read"|"validate"|"get_sha")`
- `manage_scene` 的 `get_active` / `get_hierarchy` / `get_loaded_scenes` / `get_build_settings` / `scene_view_frame`
- `manage_asset` 的 `search` / `get_info` / `get_components`
- `unity_reflect`、`refresh_unity`、`get_test_job`、`manage_profiler`
- **全部 19 个资源**（`get_editor_state`、`get_project_info`、`get_gameobject_components`、`get_cameras`、`get_volumes`、`get_rendering_stats`、`get_renderer_features`、`get_layers`、`get_tags`、`get_windows`、`get_selection`、`get_tests` …）

**会写工程**
- 对象/场景：`manage_gameobject`、`manage_components`、`manage_prefabs`、`manage_scene`(create/load/save/close_scene/set_active_scene/move_to_scene/modify_build_settings)
- 资产/数据：`manage_asset`(import/create/modify/delete/duplicate/move/rename/create_folder)、`manage_scriptable_object`、`manage_material`、`manage_shader`、`manage_texture`、`manage_ui`、`manage_camera`、`manage_physics`、`manage_graphics`、`manage_animation`、`manage_vfx`、`manage_probuilder`
- 脚本：`manage_script`(create/update/delete/apply_text_edits/edit)
- 编辑器/构建：`manage_editor`、`execute_code`、`execute_menu_item`、`manage_build`、`manage_packages`、`run_tests`
- 资产生成：`generate_image`、`generate_audio`、`generate_model`、`import_model`、`import_model_file`（长任务，`RequiresPolling`，靠 `status` 轮询）
- 批量：`batch_execute`

> **优先 `batch_execute`**：一次连发多条命令（本工程上限 25，见 EditorPrefs `MCPForUnity.BatchExecute.MaxCommands`），比逐条调用快得多，也少踩中间态竞态。

---

## 7. 任务 → 工具 决策表

| 想做的事 | 用哪个 |
|---|---|
| 看当前场景有没有未保存改动 | `manage_scene(get_active)` → `isDirty` |
| 读 scene 层级树 / 已加载场景列表 | `manage_scene(get_hierarchy / get_loaded_scenes)` |
| 按名字找物体拿 instance id | `find_gameobjects`（配 `search_method: "by_name"`、`include_inactive: true`） |
| 读出某物体上组件的真实字段值 | 资源 `get_gameobject_components`（比翻 YAML 可靠） |
| 改 Inspector 上的 `[SerializeField]` 引用/数值 | `manage_components` + `manage_gameobject(modify)` |
| 读/改 SO 资产字段 | `manage_scriptable_object`（资源 `search` 定位，动作 `set` / `array_resize`） |
| 读编译报错 / 运行时日志 | `read_console(types=["error","warning"])` |
| 改脚本内容（不用文本编辑器） | `manage_script(apply_text_edits / edit)`，改完 `refresh_unity` |
| 进 Play 模式看表现 | `manage_editor(play)` → `manage_camera(screenshot / screenshot_multiview)` → `manage_editor(stop)` |
| 跑测试 | `run_tests` + `get_test_job`（长任务） |
| 查 API / 反射类成员 | `unity_reflect` |
| 生成图片/音频/模型资产 | `generate_image` / `generate_audio` / `generate_model`（长任务，轮询 `status`） |

---

## 8. 拿来干什么最划算（本项目）

1. **核对 prefab 层级与 Inspector 真实数值**
   项目大量依赖「子物体下标约定」与 `[SerializeField]` 引用：`ArmamentButton.prefab` 子0=icon、**子1=偏好标记**；`ArmamentWnd.armamentRoot` 的 GetChild 约定；`WndManager` 上的 `public XxxWnd` 字段在 `GameRoot.prefab` 里填 0。
   → `find_gameobjects` 拿 instance id，再读 `get_gameobject_components`。

2. **批量校验 SO 资产交叉引用**
   扫 `Assets/Resources/GameData/Airdrop/ADSO_*.asset`：`subAirdrop` 指向的战备 ID 是否存在、`coolGroup` 是否配错、`isHide` 是否漏配。用 `manage_scriptable_object` 读改。

3. **进 Play 模式看真机表现**
   `manage_editor(play)` → `manage_camera(action="screenshot")` → 验证渲染 / UI / 展示模型（例如 `AirdropConfigWnd` 那套「禁用全部 MonoBehaviour + Collider、Rigidbody 置 kinematic、独立相机 + RenderTexture」是否真的生效）→ `manage_editor(stop)`。

4. **编译/报错闭环**
   `read_console(types=["error","warning"])` 直接拿编译结果，不要让用户手抄 Console。改完脚本后配合 `refresh_unity` + `manage_script(action="validate")`。

5. **资产体检**
   `manage_asset(action="search")` 找丢失引用、找孤儿资产（历史上 `Feature/Fog` 删除后遗留的 `Shader/Feature/FogEffect.shader`、`Setting/Feature/Fog.mat` 就是这种）。

---

## 9. 本项目注意事项（踩坑点）

- **`Packages/com.coplaydev.unity-mcp/` 是本地嵌入包，且已被汉化**：不要用 `manage_packages` 去升级/卸载它（会冲掉汉化，见文末「升级」）。包 Editor 窗口里的中文文案是**有意为之的成果**，别当成脏数据"清理"回去。
- **SO 不能持有 prefab / 场景实例引用**：用 `manage_scriptable_object` 往 SO 里塞引用时，只能塞**资产**（材质、粒子预制体、音效、颜色）。子物体 / 挂点这类实例层级引用必须留在 prefab 组件的 `[SerializeField]` 里。
- **给 SO 加字段后**：若该 SO 有专属 Editor（如 `AirdropData_SOEditor`、`CampData_SODrawer`），新字段需显式补 `DrawField("字段名")`，否则检视器和数据编辑器里都不显示。
- **`[InspectorName]` / `[DisplayField]` 只对字段（field）有效**，不能加到属性上；且 `[DisplayField]` 是 PropertyDrawer，只对"已序列化字段"生效（私有字段还得配 `[SerializeField]`）。
- **新脚本的落位**：`Assets/Scripts/` 是数字前缀分层（`00Core → 00GameContract → 00Tools → 01Manager → 02Data → 02Game → 04UI → 08Map`），跨层引用受 asmdef 约束。`manage_script(action="create")` **不会**帮你判断归属 —— 建完脚本自己核对 asmdef，别让下层被上层引用。
- **窗口注册范式**：新窗口要在 `WndManager` 上挂 `public XxxWnd` 字段（`GameRoot.prefab` 里填 0），由窗口自己的 `Init()` 赋值，而 `Init()` 挂在**场景/prefab 的 UnityEvent** 上（参见 `Assets/Scene/Utnapishitim.unity`）。用 `manage_components` 配窗口时按这个套路走。
- **不要通过 MCP 把运行态调试值持久化进资产**：`RenderSettings.*` 引用的资产对象（如天空盒材质）、MPB 之类的运行时数据一旦写盘，编辑器 Play 会持久化 → 跨局累积衰减。要改就在运行时改副本。
- **UI 展示模型通用做法**：实例化 prefab → 禁用全部 MonoBehaviour + Collider、Rigidbody 置 kinematic → `SetChildLayer(layer, true)`；独立相机 + 运行时 `RenderTexture` 预览，拖动判定用 `RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos, Overlay ? null : canvas.worldCamera)`。

---

## 10. 常用调用范例

```jsonc
// 读活动场景（含 isDirty）
{ "action": "get_active" }                       // manage_scene

// 找物体 → 再读组件
{ "search_term": "ArmamentButton", "search_method": "by_name", "include_inactive": true }   // find_gameobjects
// 然后读资源 get_gameobject_components（带 instance id）

// 读编译错误（count 传字符串兼容性最好）
{ "action": "get", "types": ["error", "warning"], "count": "20" }   // read_console

// 截图
{ "action": "screenshot" }                       // manage_camera

// 批量执行（上限 25）：把上面若干条合并成一次调用 → batch_execute
```

更细的参数（每个工具的 action 枚举、必填字段）见 **`references/tools-reference.md`**。

---

## 11. 排障速查

| 症状 | 原因 | 处理 |
|---|---|---|
| 会话里没有任何 `manage_*` 工具 | IDE 的 `~/.codebuddy/mcp.json` 没配 / 没重载 | 按 §2 配好并**重启会话** |
| Unity 窗口显示"已配置"但 AI 无工具 | Configure 写的是 CLI 的 `~/.codebuddy.json` | 同上（两份文件不是一个） |
| `No Unity Editor instances found` | Unity 桥没起 | 检查 Transport = **Stdio**；重开 Unity |
| 同上，且 `~/.unity-mcp` 目录不存在 | 桥从未启动过 | 同上（是配置问题，不是断线） |
| `get_editor_state` 字段全 null | 同上 | 同上 |
| Unity 侧 Transport 是 HTTP | 默认值，两边不匹配 | 切 Stdio（推荐）或客户端改直连 `http://127.0.0.1:8080/mcp` |
| 工具调用超时 | 长任务 + 帧 I/O 超时 | 环境变量 `UNITY_MCP_STDIO_COMMAND_TIMEOUT_MS` 调整（默认 300000） |
| uvx 拉不到 server | pypi 直连不通 | 见 MEMORY：用清华镜像 / uv 缓存 |

---

## 12. 升级 unity-mcp（当前 `10.2.1-beta.6`）

> 🛑 **升级前必读 —— 本包的汉化会被冲掉**
>
> 本包 Editor 窗口**已被全面汉化**（2026-09-15）：11 个 UXML、主窗口 / Setup / EditorPrefs 窗口、6 个 Section 控制器、HealthStatus 常量、23 个客户端配置器（含 `CodeBuddyCliConfigurator`）、Windows 依赖检测。
>
> 覆盖式更新 = 汉化全丢。正确姿势：
> 1. 先备份 `Packages/com.coplaydev.unity-mcp/Editor/`（或 `git add -f` 提交进版本库）；
> 2. 再用新版覆盖；
> 3. 最后把汉化 diff 回灌。
>
> 同理：**只做汉化时不要动上游文件结构**，只改 UXML/USS/文本，方便下次三方合并。

升级走镜像覆盖：

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

---

## 13. 双机环境注意（`D:\Pro\Bluedivers` / `E:\Bluedivers` 两份副本）

同一份工程在两台设备上各有一份工作副本，**以下东西都是「每台一份」，绝不通用**：

| 项 | 为什么 | 换机后怎么做 |
|---|---|---|
| MCP 实例 hash / 端口 | 发现文件的 `<projectHash>` 由**项目绝对路径**算出，D 盘与 E 盘必然不同 | 读 `%USERPROFILE%\.unity-mcp\unity-mcp-status-*.json`，按 `project_path` 认副本 |
| `~/.codebuddy/mcp.json` 里的 `command` | 本机 Python / 安装位置可能不同 | `Get-Command uvx` 取本机路径后再填 |
| Unity 侧 Transport 设置 | 存在本机本工程的 EditorPrefs，不随 git 走 | 每台机器设一次 `Stdio` |
| `unity-mcp` 包的汉化 | 汉化在 `Packages/com.coplaydev.unity-mcp/`；该目录是否被 git 跟踪，决定另一台能否自动拿到 | `git ls-files Packages/com.coplaydev.unity-mcp` 核对；未入库则 `git add -f` 纳入版本库 |
| 连接关系 | 桥是「本机 Unity ↔ 本机客户端」，默认不跨机 | 只操作**当前设备上开着的**那个 Unity |

**换设备后的第一件事**：跑 §3 自检，确认 `project_path` 指向当前这份副本，再动任何写操作 —— 否则容易把 A 机器上的改动预期套到 B 机器上。

---

## 14. 让**另一个 Unity 工程**也用上（多工程接入）

**关键认识：客户端配置只有一份，工程侧才要各装一份。**

- **客户端 `mcp.json` 不用改**：`uvx mcp-for-unity` 这**一个** server 进程会自动发现**所有**正在运行的 Unity 实例 —— 每个工程在 `%USERPROFILE%\.unity-mcp\` 下写自己的 `unity-mcp-status-<工程路径hash>.json`。
- **每个 Unity 工程要各做两件事**：① 装包 ② 把 Transport 切 `Stdio`。

本机已知的其他 Unity 工程：

| 路径 | Unity 版本 | 备注 |
|---|---|---|
| `D:\Project\RTSClient` | 2022.3.62f3 | 与 Bluedivers 同版本；Packages 里只有 `com.arongranberg.astar` |
| `E:\rtsclient` | 2022.3.34f1c1 | Unity 中国版；Packages 里有 astar、NB_FX |

### 步骤

1. **复制包**（务必从本工程复制，才带上汉化）：
   ```powershell
   Copy-Item -Recurse "e:\Bluedivers\Packages\com.coplaydev.unity-mcp" "D:\Project\RTSClient\Packages\com.coplaydev.unity-mcp"
   ```
   ❗ 不要改用 git URL 从上游装 —— 那样**拿不到汉化**。

2. **改目标工程的 `Packages/manifest.json`**，加两行：
   ```json
   "com.coplaydev.unity-mcp": "file:com.coplaydev.unity-mcp",
   "com.unity.nuget.newtonsoft-json": "3.0.2",
   ```
   （`com.unity.test-framework` 若已有 ≥ 1.1.31 则不必动）

3. **打开目标工程 → `Window → MCP for Unity` → Connection → Transport = `Stdio`**
   ⚠️ **每个工程都要单独切一次**：`MCPForUnity.UseHttpTransport` 是 **EditorPrefs、按工程隔离**，新工程会回到默认 HTTP → 桥不自启 → 照样报 `No Unity Editor instances found`。

4. **多实例路由**（同时开多个工程时，server 需要知道发给谁）：
   - 会话内切换：`set_active_instance`，参数支持 `Name@hash`、hash 前缀、或**端口号**
   - 或另配一个服务器条目：
     `"args": ["--from","mcpforunityserver","mcp-for-unity","--default-instance","RTSClient@xxxxxxxx"]`

5. **端口不打架**：stdio 桥默认 `6400`，被占用时 `PortManager.GetPortWithFallback()` 自动让到 6401、6402…

6. **验证**：`Get-ChildItem "$env:USERPROFILE\.unity-mcp"` 会随打开的工程数出现多个 status 文件。

### 注意
- **汉化会分叉**：包是各工程一份副本、各改各的。日后升级要逐个工程覆盖，别拿旧副本覆盖新副本。
- **多工程 ≠ 双机（§13）**：同一台机器上两个 CodeBuddy 窗口 = **两个 uvx 进程 = 两个独立会话**（`mcp.json` 文件共用，但 server 进程与实例钉选状态**不共用**）。所以两边互不干扰，但**每个窗口都要各自钉一次**。
- **活动实例只存在内存里**（FastMCP 会话态 + `set_active_instance`），不落盘 → 重开窗口 / 重启 IDE / 重启 server 都要**重设**。

### 多实例路由：server 会拒绝猜测（本机已落地规则）

源码 `transport/legacy/unity_connection.py:578-596`（`mcpforunityserver` 10.2.x）明确：

| 运行中的实例数 | 行为 |
|---|---|
| 1 个 | 自动选中，无需任何操作 |
| ≥ 2 个且未钉选 | **直接抛错** `Multiple Unity instances are connected and none is selected...`，并列出可选 ID |

注释点明了原因：旧的「路由到最近心跳的编辑器」会让未绑定会话**串到别的项目**，已按上游 issue **#1023** 删除。所以最坏情况是"报错让你选"，不会"偷偷改错项目"。

**免掉"每次设一次"的正解 = 工作区规则**（不是全局 env）：

| 工程 | 规则文件 | 钉的实例 |
|---|---|---|
| `E:\Bluedivers` | `.codebuddy/rules/UnityMCP_多实例路由.md` | `Bluedivers@3d9f2357`（端口 6400） |
| `D:\Project\RTSClient` | `.codebuddy/rules/UnityMCP_多实例路由.mdc` | `RTSClient@6365de15`（端口 6401） |

规则随会话自动加载，天然按窗口隔离；且**每次调用都带 `unity_instance="..."`**（单次路由，不改会话默认值），比依赖 `set_active_instance` 的隐含状态更稳。

❗ **不要用全局 `~/.codebuddy/mcp.json` 的 `env.UNITY_MCP_DEFAULT_INSTANCE`**：那是**全局**的，两个窗口会一起被钉到同一个实例，反而制造交叉。
