---
name: data-editor
description: Bluedivers 项目「数据编辑器」（Tools/数据编辑器，DataEditorWindow + DataTabs）专用开发指南。当用户编写或修改 Assets/Editor/DataEditorWindow.cs、Assets/Editor/DataTabs/ 下任意 TabModule / IDataTab / TabType 枚举、需要在数据编辑器左列表或右面板展示/编辑 SO 数据、新增一个数据编辑 Tab 页、为某类 SO 编写影响编辑器右面板的自定义检视器（CustomEditor）、或复用在编辑器里挑选 SO 的 SOPickerPopup 时触发。提供窗口架构、TabModule 扩展点、面板绘制流程、保存/刷新机制与避坑清单。
---

# Bluedivers 数据编辑器（DataEditorWindow / DataTabs）开发指南

## Overview

Bluedivers 通过一个自研的 IMGUI 编辑器窗口「数据编辑器」（菜单 `Tools/数据编辑器`）批量查看、编辑 `Assets/Resources/GameData/` 下的全部 `_SO` 配置资产。窗口由左侧**数据列表** + 右侧**字段编辑区**组成，左右两侧的展示与交互逻辑由一组 `DataTabModule<TData>` 分页承载，`DataEditorWindow` 只负责窗口框架、Tab 注册、选中项缓存与键盘导航。

该 skill 沉淀这套框架的全部知识，使修改/新增一个数据编辑页时无需重新通读代码。

## 核心架构（速览）

- 入口与框架：`Assets/Editor/DataEditorWindow.cs`（`EditorWindow`），菜单 `Tools/数据编辑器`。
- 分页契约：`Assets/Editor/DataTabs/IDataTab.cs` —— `TabType` 枚举 + `IDataTab` 接口。
- 泛型基类：`Assets/Editor/DataTabs/DataTabModule.cs` —— `DataTabModule<TData> : IDataTab`，封装左侧列表、右侧面板全部模板逻辑，子类只声明"差异点"。
- 数据页实现：`Assets/Editor/DataTabs/*TabModule.cs`，共 11 个，与 `TabType` 一一对应。
- 右面板字段编辑 = 用 `Editor.CreateEditor(asset)` 创建缓存 Editor 后调用 `OnInspectorGUI()`，因此**为 SO 写专属 `CustomEditor` 会直接影响右面板**。
- SO 选择弹窗：`Assets/Editor/SOPickerPopup.cs`（`SOPickerPopup<T>`，泛型 PopupWindowContent）。

详细 API 参考、各 TabModule 对照表、绘制流程与新增页面步骤见 `references/data-editor-guide.md`。

## 主要任务与工作流

### 1. 新增一个数据编辑 Tab 页

1. 在 `IDataTab.cs` 的 `TabType` 枚举中追加新类型。
2. 新建 `Assets/Editor/DataTabs/XxxTabModule.cs`，继承 `DataTabModule<XxxData_SO>`，类名用 `XxxTabModule`、`sealed`、Global namespace（与现有模块一致）。
3. 实现抽象成员：`TabType`、`DisplayName`（中文）、`RootPath`（资产目录，约定 `Assets/Resources/GameData/<XXX>`）、`TypeName`（如 `"t:XxxData_SO"`）、`SortComparison`、`GetEmptyMessage`、`GetSelectedTitle`、`FilterItems`、`DrawListItemContent`。
4. 在 `DataEditorWindow.RegisterTabs()` 中 `AddTab(new XxxTabModule(this))` 注册。
5. 若资产分布在子目录，重写 `IncludeSubDirs => true`；若列表项需要标题/类型缓存，重写 `OnItemLoaded` 填充 `LabelCache`。

列表项行内容与选中项标题颜色等具体模板见 `references/data-editor-guide.md` 的「新增页面模板」。

### 2. 提升某类 SO 的编辑体验（右面板）

右面板实际渲染的是资产自己的 Inspector。给 SO 写 `[CustomEditor]` 自定义检视器（放在 `Assets/Editor/`，如 `AirdropData_SOEditor`）即可**同时**作用于标准 Inspector 与数据编辑器右面板，推荐优先于此路线而不是硬改 `DataTabModule`。

注意：项目中所有没有专属 CustomEditor 的对象由全局 fallback `EditorOverride`（`Assets/Editor/Drawer/EditorOverride.cs`）绘制，它负责 `[InspectorName]` 中文标签与 `[Compare]` 条件显隐。**替换成专属 CustomEditor 后这些能力不再自动生效**，必须自行复刻：
- 中文字段名：反射字段上的 `InspectorNameAttribute.displayName`（参考 `AirdropData_SOEditor.LabelOf`）。
- `[Compare]` 条件显隐：调用 `CustomLabelDrawer.ShouldDisplayField(prop, compareAttr)`（`Assets/Editor/Drawer/CustomLabelDrawer.cs`）。

### 3. 在右面板/列表中展示关联数据或做数据校验

- 数据编辑器面板属于 IMGUI，可在 `DrawListItemContent`（左列表项）或自定义检视器（右面板）内实时扫描全部同类型资产（`AssetDatabase.FindAssets("t:Xxx_SO")`）做关联展示/冲突检测。
- 参考实现：`AirdropTabModule`（列表项 ID/操作序列冲突标红）+ `AirdropData_SOEditor`（同冷却组成员展示、同 ID/同操作序列红卡、`subAirdrop` 用 `SOPickerPopup` 挑选）。
- 全量资产扫描应加缓存与节流（如 `EditorApplication.timeSinceStartup` 1s 刷新），避免每帧 FindAssets。

### 4. 在编辑器中挑选某个 SO

使用 `SOPickerPopup<T>`：收集候选 List，把按钮绘制后的 `Rect`（`GUILayoutUtility.GetLastRect()`）作为锚点 `PopupWindow.Show(rect, new SOPickerPopup<T>(items, onPick, getIcon, getName, getType, getTypeColor, getFrame, confirmMode))`。`confirmMode=false`（默认）单击即回调；列表展示委托返回 `Sprite/名称/类型行/类型色/边框`。

### 5. 保存与刷新

- `DataEditorWindow.DrawCachedInspector` 在编辑字段后执行 `ApplyModifiedProperties()` + `AssetDatabase.SaveAssets()`。
- 自定义检视器若在 `OnInspectorGUI` 内部自行 `Apply`，外层检测会失效，必须自行 `EditorUtility.SetDirty(target)` + `SaveAssets()`（参考 `AirdropData_SOEditor.ApplyDirty` 的去重 delayCall 保存）。
- 数据资产修改应经 `serializedObject` 属性而非直接改字段，以保证 Inspector 一致性。

## 避坑清单

- TabType 枚举新增后**必须**在 `RegisterTabs` 注册，否则 `Current` 字典访问抛 `KeyNotFoundException`。
- `DataTabModule` 的 SO 泛型上限是 `Object`；`WeaponTabModule` 以 `GameObject`（Prefab）为数据源、配合 `GetEditorTarget` 返回其中的 `WeaponPlayerController` 组件——当数据源不是 SO 时用这两处扩展点。
- 右面板选中对象切换由 `OnSelect` -> `Host.SetCachedEditor` 负责，旧 Editor 会先销毁；重写列表/面板时不要绕过它。
- IMGUI 每帧触发的 `FindAssets`/加载操作会拖慢鼠标移动时的重绘，务必节流。
- `[Compare]`/`[InspectorName]` 的处理只属于编辑器框架，运行时不需要。
- 全局命名空间无 `namespace`，与 `Assets/Editor` 其余编辑器一致；不要引入会与运行时冲突的类型。

## Resources

- `references/data-editor-guide.md`：完整架构说明、全部类与方法参考、11 个 TabModule 对照表、新增/改造页面的详细步骤与示例、常见避坑。
