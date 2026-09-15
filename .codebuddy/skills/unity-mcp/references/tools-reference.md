# Unity MCP 工具 / 资源速查

> 对应 Unity 包 `com.coplaydev.unity-mcp` **10.2.1-beta.6**（`Packages/com.coplaydev.unity-mcp/`）。
> 工具名以 `[McpForUnityTool("...")]` 特性为准，资源以 `[McpForUnityResource("...")]` 为准。
> 客户端里工具全名形如 `mcp__unityMCP__<tool>`；本文档只写后段。
> **动作（action）不多做猜测——未标"已核实"的，先调一次或看 Unity 窗口 `Tools` 页的描述。**

---

## 1. 工具总表（35 个）

| 工具 | 组 | 读/写 | 一句话 |
|---|---|---|---|
| `find_gameobjects` | — | 读 | 按名字/标签/组件等找物体，拿 instance id |
| `read_console` | — | 读 | 读 Console 日志（编译错误、运行时异常） |
| `refresh_unity` | — | 读/轻写 | 触发 AssetDatabase 刷新（改脚本后常用） |
| `batch_execute` | — | — | 批量连发命令（本工程上限 25） |
| `unity_reflect` | docs | 读 | 反射查类/成员签名（查 API 用） |
| `manage_scene` | — | 读写 | 场景：载入/保存/层级/构建列表/截图/校验 |
| `manage_gameobject` | — | 写 | 物体的增删改/复制/相对移动/朝向 |
| `manage_components` | — | 写 | 组件增删 + `set_property` |
| `manage_prefabs` | — | 写 | prefab 创建/查层级/改内容/Prefab Stage |
| `manage_asset` | — | 读写 | 资产导入/创建/改/删/复制/移动/搜索 |
| `manage_scriptable_object` | scripting_ext | 写 | SO 资产：create / modify（SerializedProperty 补丁） |
| `manage_script` | — | 读写 | 脚本：读/建/改/删/校验/取 sha/结构化编辑 |
| `execute_code` | scripting_ext | 写 | 在编辑器里执行任意 C#（权限极大） |
| `execute_menu_item` | — | 写 | 执行菜单项（按路径） |
| `manage_editor` | — | 写 | Play/Pause/Stop、tag、layer、撤销重做等 |
| `manage_material` | — | 写 | 材质属性 |
| `manage_shader` | vfx | 写 | Shader 相关 |
| `manage_texture` | vfx | 写 | 贴图导入设置等 |
| `manage_animation` | animation | 写 | 动画/clip |
| `manage_vfx` | vfx | 写 | 粒子/VFX |
| `manage_ui` | ui | 写 | UGUI/UI 元素 |
| `manage_camera` | — | 写 | 相机、Cinemachine、截图 |
| `manage_physics` | core | 写 | 物理设置（层碰撞等） |
| `manage_graphics` | core | 写 | 图形/质量设置 |
| `manage_probuilder` | probuilder | 写 | ProBuilder 建模 |
| `manage_profiler` | profiling | 读 | Profiler 数据 |
| `manage_packages` | core | 写 | 包管理（⚠ 别动 unity-mcp 自身） |
| `manage_build` | core | 写 | 构建（轮询 status） |
| `run_tests` | testing | 写 | 跑测试，返回 job id |
| `get_test_job` | testing | 读 | 查测试任务进度/结果 |
| `generate_image` | asset_gen | 写 | AI 生成图片资产（长任务，轮询） |
| `generate_audio` | asset_gen | 写 | AI 生成音频资产（长任务，轮询，上限 600s） |
| `generate_model` | asset_gen | 写 | AI 生成模型（长任务，轮询） |
| `import_model` | asset_gen | 写 | 导入模型（长任务，轮询） |
| `import_model_file` | asset_gen | 写 | 从文件导入模型 |

---

## 2. 已核实的动作清单

### `manage_scene`（源码 `Tools/ManageScene.cs`）
`create` / `load` / `save` / `get_hierarchy` / `get_active` / `get_build_settings` / `screenshot` /
`scene_view_frame` / `close_scene` / `set_active_scene` / `get_loaded_scenes` / `move_to_scene` /
`modify_build_settings` / `validate`
- 新建场景的模板参数：`empty` / `default` / `3d_basic` / `2d_basic`
- `get_active` 返回含 `isDirty`

### `manage_editor`（源码 `Tools/ManageEditor.cs`）
`play` / `pause` / `stop` / `set_active_tool` / `add_tag` / `remove_tag` / `add_layer` / `remove_layer` /
`deploy_package` / `restore_package` / `undo` / `redo`
- ⚠ `set_resolution` / `set_quality` 在源码里被**注释掉**，不要用。

### `manage_script`（源码 `Tools/ManageScript.cs`）
`create` / `read` / `update` / `delete` / `apply_text_edits` / `validate` / `edit` / `get_sha`
- `edit` 的 structured edits 子操作：`replace_class` / `delete_class` / `replace_method` /
  `delete_method` / `insert_method` / `anchor_insert` / `anchor_delete` / `anchor_replace`

### `manage_asset`（源码 `Tools/ManageAsset.cs`）
`import` / `create` / `modify` / `delete` / `duplicate` / `move` / `rename` / `search` /
`get_info` / `create_folder` / `get_components`

### `manage_gameobject`（源码 `Tools/GameObjects/ManageGameObject.cs`）
`create` / `modify` / `delete` / `duplicate` / `move_relative` / `look_at`

### `manage_components`（源码 `Tools/ManageComponents.cs`）
`add` / `remove` / `set_property`
> 错误信息里会带 `Supported actions: add, remove, set_property`，参数需 `target`（物体 token）+ 组件名。

### `manage_prefabs`（源码 `Tools/Prefabs/ManagePrefabs.cs`）
`create_from_gameobject` / `get_info` / `get_hierarchy` / `modify_contents` /
`open_prefab_stage` / `save_prefab_stage` / `close_prefab_stage`
- 路径参数用 `prefabPath` 或 `path`；`close_prefab_stage` 支持 `saveBeforeClose`

### `manage_scriptable_object`（源码 `Tools/ManageScriptableObject.cs`）
`create`（别名 `createso`） / `modify`（别名 `modifyso`）
- `modify` 通过 **SerializedObject 属性路径**打补丁：`patches: [{ propertyPath, op, value }]`，
  `op` 为 `set` / `array_resize`
- 编译中会返回 `compiling_or_reloading` + `hint: retry`（客户端可重试）

### `manage_camera`（源码 `Tools/Cameras/ManageCamera.cs`）
`ping` / `create_camera` / `set_target` / `set_lens` / `set_priority` / `list_cameras` /
`screenshot` / `screenshot_multiview` / `ensure_brain` / `get_brain_status` / `set_body` / `set_aim` /
`set_noise` / `add_extension` / `remove_extension` / `set_blend` / `force_camera` / `release_override`

### `read_console`（源码 `Tools/ReadConsole.cs`）
`get`（`format`: `plain` / `json` / `detailed`）

---

## 3. 资源（19 个，全部只读）

| 资源名 | 内容 |
|---|---|
| `get_editor_state` | 编辑器状态（`unity.instance_id`、`advice.ready_for_tools`）— **连不上时第一个查它** |
| `get_project_info` | 工程根路径、项目名 |
| `get_gameobject` | 单个物体信息（按 instance id） |
| `get_gameobject_components` | **该物体全部组件的字段真实值**（核 Inspector 的神器） |
| `get_gameobject_component` | 单个组件 |
| `get_selection` | 当前选中项 |
| `get_windows` | 已打开的编辑器窗口 |
| `get_active_tool` | 当前激活的工具 |
| `get_tool_states` | 各 MCP 工具启用状态（查某工具是否被 group 关掉） |
| `get_prefab_stage` | 当前 Prefab Stage |
| `get_cameras` | 场景相机列表 |
| `get_volumes` | Volume / 后处理 |
| `get_rendering_stats` | 渲染统计 |
| `get_renderer_features` | URP Renderer Features（查 Snow RF 用得上） |
| `get_layers` | 层列表 |
| `get_tags` | 标签列表 |
| `get_menu_items` | 菜单项路径（配 `execute_menu_item`） |
| `get_tests` / `get_tests_for_mode` | 测试清单 |

---

## 4. 调用约定与坑

- **参数扁平传**：`{"action": "get_active"}` 这类键直接放在工具参数里。
- **`count` 传字符串**兼容性最好（如 `"count": "20"`）。
- **`batch_execute` 优先**：一次多条（上限 25，`EditorPrefs: MCPForUnity.BatchExecute.MaxCommands`），少踩中间态竞态。
- **长任务**（`manage_build`、`run_tests`、`generate_*`、`import_model`）标了 `RequiresPolling`，
  用其 `PollAction`（通常 `status`）轮询；`generate_audio` 上限 600s，其余 300s。
- **编译中调用**会返回 `compiling_or_reloading` + `hint: retry` → 稍等重试。
- **JSON 字符串参数**：部分工具接受把 object/array 以 JSON 字符串传入（源码里有 `CoerceJsonStringParameter`）。
- 工具不显示 → 多为 tool group 未启用，用 `get_tool_states` 查，或去 Unity 窗口 `Tools` 页勾选。
- 想确认某工具的真实动作：读 `Packages/com.coplaydev.unity-mcp/Editor/Tools/<Tool>.cs`，
  或直接触发一次错误参数，错误信息会回列 `Supported actions`。
