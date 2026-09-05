# 数据编辑器架构详解（DataEditorWindow / DataTabs）

Bluedivers 的配置资产（`_SO`，通常位于 `Assets/Resources/GameData/`）通过自研 IMGUI 编辑器窗口统一查看与编辑。本文按源码整理该框架的完整结构、扩展点与常见任务步骤。

## 1. 文件与类型地图

| 文件 | 内容 | 职责 |
|---|---|---|
| `Assets/Editor/DataEditorWindow.cs` | `DataEditorWindow : EditorWindow` | 窗口框架：Tab 注册/切换、缓存选中项 Editor、键盘导航、静态 UI 工具方法 |
| `Assets/Editor/DataTabs/IDataTab.cs` | `enum TabType` + `interface IDataTab` | 分页契约与枚举定义（所有页面清单） |
| `Assets/Editor/DataTabs/DataTabModule.cs` | `abstract class DataTabModule<TData> : IDataTab` | 泛型模板基类：左列表 + 右面板 + 数据加载逻辑 |
| `Assets/Editor/DataTabs/<X>TabModule.cs` | `XxxTabModule : DataTabModule<Xxx_SO>` | 具体页面：路径、排序、过滤、列表项渲染等"差异点" |
| `Assets/Editor/SOPickerPopup.cs` | `SOPickerPopup<T> : PopupWindowContent` | 可搜索 SO 选择弹窗（复用基础设施） |
| `Assets/Editor/Drawer/EditorOverride.cs` | `EditorOverride : Editor`（fallback） | 全局默认检视器：`[InspectorName]` 中文化、`[Compare]` 显隐、Foldout |
| `Assets/Editor/Drawer/CustomLabelDrawer.cs` | `CustomLabelDrawer`、`DisplayFieldDrawer` | `CompareAttribute`/`DisplayField` 的 PropertyDrawer，含 `ShouldDisplayField` 静态判定 |

菜单入口：`Tools/数据编辑器`。默认打开 `Mission` 页。

## 2. TabType 与 IDataTab

`TabType` 枚举（顺序即标签页顺序，`CycleTab` 依赖 `Enum.GetValues`）：

```csharp
public enum TabType { Mission, Camp, Airdrop, Map, Update, WeaponModule,
                     WeaponUpgrade, AboState, Weapon, Role, Booster }
```

`IDataTab` 契约：`TabType`、`DisplayName`、`Count`、`HasSelection`、`SearchFilter`、`LeftScroll/RightScroll`、`Refresh()`、`DrawLeftPanel(Action drawFooter)`、`DrawRightPanel()`、`MoveSelection(int)`、`GetSelectedAssetPath()`、`GetSelectedData()`。

## 3. DataEditorWindow 详解

### 3.1 状态与生命周期

- `_tabs : Dictionary<TabType, IDataTab>`，`_currentTab`，`_cachedEditor : Editor`。
- `OnEnable`：`RegisterTabs()` + `RefreshAll()`（先 `DestroyCachedEditor()`，再逐个 `tab.Refresh()`）。
- `OnDisable`：置空 `_cachedEditor`。

### 3.2 注册

`RegisterTabs()` 中依次 `AddTab(new XxxTabModule(this))`。新增页面后必须在此注册，否则 `Current`（`_tabs[_currentTab]`）会抛 `KeyNotFoundException`。

### 3.3 缓存 Editor 机制（右面板核心）

- `SetCachedEditor(Object target)`：销毁旧 Editor → `UnityEditor.Editor.CreateEditor(target)`。
- `DestroyCachedEditor()`：判空后销毁（带 `try/catch`，防止目标已释放导致的异常）。
- `HasCachedEditor`。
- `DrawCachedInspector()`：

```csharp
EditorGUI.BeginChangeCheck();
_cachedEditor.OnInspectorGUI();
if (EditorGUI.EndChangeCheck() && _cachedEditor.serializedObject != null
    && _cachedEditor.serializedObject.hasModifiedProperties)
{
    _cachedEditor.serializedObject.ApplyModifiedProperties();
    AssetDatabase.SaveAssets();
}
```

含义：右面板字段编辑 = 调用资产的自带 Inspector。**选中某个数据项后，编辑器会自动创建该对象的 `Editor`**——因此：
- 若该类没有专属 `CustomEditor`，走 fallback `EditorOverride`；
- 若有专属 `CustomEditor`（如 `AirdropData_SOEditor`），右面板直接显示自定义检视器。

`OnSelect` 切换选中项时由 TabModule 调 `Host.SetCachedEditor(GetEditorTarget(data))`。

### 3.4 键盘导航

`HandleKeyboardNavigation()`（`KeyDown` 且非文本编辑状态）：
- `W/S`、`↑/↓`：`Current.MoveSelection(±1)`（Tab 内左列表上下选择）。
- `A/D`、`←/→`：`CycleTab(±1)`（循环切换 Tab，会 `DestroyCachedEditor`）。
- 处理完 `Event.current.Use()`。

### 3.5 静态工具方法

- `ColoredLabel(GUIStyle baseStyle, Color color)`：返回 textColor 在所有状态（normal/hover/active/focused/on*）都保持 `color` 的样式副本——列表标题着色常用。
- `GetEnumLabel(MissionEnum value)`：优先取枚举成员上的 `CustomLabelAttribute`（按特性类型名反射），其次取 `InspectorNameAttribute.displayName`。
- `GetInspectorName<TEnum>(TEnum value)`：泛型版，只认 `InspectorNameAttribute`。
- `ResetFocus()`：清 GUI 焦点（`GUIUtility.hotControl/keyboardControl`、`editingTextField`），用于切换选中项后避免残留输入焦点。

### 3.6 布局

`OnGUI` = `HandleKeyboardNavigation` → `DrawToolbar`（刷新按钮 + 搜索框 `Current.SearchFilter`）→ `DrawTabs`（横排 tab 按钮）→ `BeginHorizontal`：`Current.DrawLeftPanel(DrawLocateButton)` + 分隔条 + `Current.DrawRightPanel()`。
`DrawLocateButton`（"定位到选中文件"）通过 `PingSelectedFile()` 用 `Selection/Ping` 定位资产。

## 4. DataTabModule<TData> 详解

泛型基类集中全部公共样板逻辑。子类只需实现"差异点"。

### 4.1 成员

- 数据：`Host`、`Items : List<TData>`、`Selected : TData`、`LabelCache`（可选 label 缓存，如 Mission 的类型中文名）。
- 界面状态：`SearchFilter`、`LeftScroll`、`RightScroll`。
- 只读：`Count`、`HasSelection`。

### 4.2 抽象成员（必须实现）

| 成员 | 含义 | 典型实现 |
|---|---|---|
| `TabType` | 枚举值 | `=> TabType.Xxx;` |
| `DisplayName` | 标签页中文名 | `=> "战备数据";` |
| `RootPath` | 资产目录 | `"Assets/Resources/GameData/Airdrop"` |
| `TypeName` | 搜索类型过滤串 | `"t:AirdropData_SO"` |
| `SortComparison` | 排序比较器（null=不排序） | `(a, b) => a.ID.CompareTo(b.ID)` |
| `GetEmptyMessage()` | 无选中时右面板提示 | `"请从左侧列表中选择…"` |
| `GetSelectedTitle()` | 右面板标题文本 | `$"{x.showName}[{x.ID}]"` |
| `FilterItems(list)` | 搜索过滤 | `Where(name.Contains(SearchFilter))` |
| `DrawListItemContent(data, isSelected)` | 左列表单项内容 | 图标 + 名称 + 次级信息 |

### 4.3 虚成员（按需重写）

| 成员 | 作用 |
|---|---|
| `IncludeSubDirs => false` | 是否把 `RootPath` 一级子目录并入搜索 |
| `OnItemLoaded(data)` | 单项加载后的钩子（填充 `LabelCache` 等） |
| `GetSelectedTitleStyle()` | 标题样式（默认粗体；可返回 `DataEditorWindow.ColoredLabel(..., color)` 着色） |
| `DrawRightPanelExtra()` | 标题下方补充行（常用：灰字资产路径） |
| `GetEditorTarget(TData)` | 返回给 Inspector 的目标对象，默认即数据本身；**数据源是 GameObject 时返回其中组件**（见 Weapon 页） |
| `RefreshData()` | 数据加载全流程，重写场景：搜索条件比 FindAssets 复杂（如 Weapon 页过滤组件） |

### 4.4 加载流程

`Refresh()` → 清 `LabelCache` → `RefreshData()`。模板实现：
1. 校验 `RootPath` 有效；
2. `GetSearchPaths()`（含 `IncludeSubDirs` 时加 `AssetDatabase.GetSubFolders`）→ `AssetDatabase.FindAssets(TypeName, paths)`；
3. 逐个 `LoadAssetAtPath<TData>`，非空加入 `Items` 并回调 `OnItemLoaded`；
4. `SortComparison` 排序。

左栏底部工具栏的「刷新列表」会执行 `Host.DestroyCachedEditor()` + `Refresh()`。

### 4.5 选中与移动

- `OnSelect(data)`：`Selected = data` → `Host.SetCachedEditor(GetEditorTarget(data))` → `DataEditorWindow.ResetFocus()` → `Host.Repaint()`。**子类做跳转/联动选择时直接调用它。**
- `IsSelected(data)`：引用相等比较。
- `MoveSelection(direction)`：基于 `FilterItems` 后的索引上下移动并 `OnSelect`。

### 4.6 左侧列表绘制模板

`DrawLeftPanel`：宽度 `max(窗口宽*0.35, 200)`；标题"共 N 项"；`BeginScrollView` 内对 `FilterItems` 逐项 `DrawItem`；底部 toolbar（刷新列表 + `drawFooter` 回调 = 定位按钮）。`DrawItem` 在内容绘制后取 `GUILayoutUtility.GetLastRect()` 做整行点击 + `Host.DrawSelectionHighlight(rect, isSelected)` 高亮。

### 4.7 右侧面板绘制模板

`DrawRightPanel`：无选中或缓存 Editor 缺失 → 居中 `GetEmptyMessage()` HelpBox；否则 ScrollView 内 = `GetSelectedTitle()`（用 `GetSelectedTitleStyle`）+ `DrawRightPanelExtra()` + 间隔 + `Host.DrawCachedInspector()`。

## 5. 现有 11 个 TabModule 对照表

| 模块 | 数据源 | RootPath | 排序 | 特点 / 可借鉴点 |
|---|---|---|---|---|
| `MissionTabModule` | `MissionData_SO` | `GameData/Mission` | type | `IncludeSubDirs`；`LabelCache` 存类型中文名；主线/支线样式；列表项右下按钮按 `MissionEnum` 跳到对应类型的数据 |
| `CampTabModule` | `CampData_SO` | `GameData/Camp` | 阵营枚举 | 图标 `GUI.DrawTexture(..., tint)` |
| `AirdropTabModule` | `AirdropData_SO` | `GameData/Airdrop` | ID | 操作序列文本化（`←↑→↓`）；图标用 R/G 通道遮罩纹理上色；`RefreshData` 后两两比较重算 ID/操作冲突并**列表标红** |
| `MapTabModule` | `MapData_SO` | `GameData/Map` | name | 图标 tint |
| `UpdateTabModule` | `UpdateData_SO` | `GameData/Update` | time+name | 次要信息用 9 号斜体灰字 |
| `WeaponModuleTabModule` | `WeaponModuleData_SO` | `GameData/WeaponModule` | type+name | `IncludeSubDirs`；列表项图标带 Frame 底框（`Assets/Resources/Images/Icon/Frame_Module{n}.png`）；使用 `SOPickerPopup` 场景之一 |
| `WeaponUpgradeTabModule` | `WeaponUpgradeData_SO` | `GameData/WeaponUpgrade` | 子目录+name | 按父目录名分组排序显示 |
| `AboStateTabModule` | `AboStateData_SO` | `GameData/AboState` | 状态枚举 | `GetInspectorName` 过滤/显示；标题与列表着色用 `ColoredLabel` |
| `WeaponTabModule` | **`GameObject`（Prefab）** | `Assets/Resources/Weapons` | 武器类型 | **非 SO 数据源范例**：`TypeName = "t:Prefab"`，重写 `RefreshData` 过滤含 `WeaponPlayerController` 的预制体；`GetEditorTarget` 返回组件对象供右面板编辑 |
| `RoleTabModule` | `RoleData_SO` | `GameData/Role` | ID | ID 为字符串；次级信息展示武器槽统计 |
| `BoosterTabModule` | `Booster_SO` | `GameData/Booster` | ID | 常规 |

（`RootPath` 前缀均为 `Assets/Resources/`；运行时 `ResSvc.LoadObjects("GameData/<XXX>")` 从同目录加载，编辑器资产目录必须与运行时保持一致。）

## 6. 右面板检视器机制

### 6.1 fallback EditorOverride

`Assets/Editor/Drawer/EditorOverride.cs`：`[CustomEditor(typeof(Object), true, isFallback = true)]`，项目里**没有专属 CustomEditor 的 SO** 默认由它绘制。能力：
- 字段名中文化：反射收集字段上的 `InspectorNameAttribute.displayName`；
- `[Compare]` 条件显隐：`ShouldDisplayField(prop)` 内部读字段上的 `CompareAttribute` 并委托 `CustomLabelDrawer.ShouldDisplayField(prop, attr)`；
- `FoldoutAttribute` 分组（项目里该特性的字段极少）；
- 每帧 `serializedObject.Update()` + 末尾 `ApplyModifiedProperties()`。

### 6.2 用专属 CustomEditor 接管

在 `Assets/Editor/` 增加 `[CustomEditor(typeof(Xxx_SO))]`（精确类型匹配优先于 fallback）。范例 `AirdropData_SOEditor.cs`，可同时服务标准 Inspector 与数据编辑器右面板。接管后要自己实现（否则退化）：
- 中文 label（反射 `InspectorNameAttribute` 或手写字典映射）；
- `[Compare]` 隐藏（`CustomLabelDrawer.ShouldDisplayField(prop, attr)`，prop 是顶层字段路径时直接可用）；
- `desc` 的 `[TextArea]`、`icon` 的 `[SpritePreview]`、`[Range]` 等由 `EditorGUI.PropertyField` 自动套用 PropertyDrawer，无需特殊处理。

### 6.3 保存语义（重点）

- 默认流程：字段改动积累到 `serializedObject.hasModifiedProperties`，由 `DrawCachedInspector`（或 `EditorOverride` 结尾）统一 `ApplyModifiedProperties()` + `SaveAssets()`。
- **专属 CustomEditor 如果自己在 `OnInspectorGUI` 内 `ApplyModifiedProperties()`，外层 `DrawCachedInspector` 的 EndChangeCheck/`hasModifiedProperties` 检测就失效、不会 SaveAssets**。此时必须在修改后自行 `EditorUtility.SetDirty(target)` 并调度 `AssetDatabase.SaveAssets()`（参考 `AirdropData_SOEditor.ApplyDirty`：delayCall 去重）。
- 直改数据/数组的按钮操作建议经 `SerializedProperty` 而非直接改 C# 字段，并即时 `ApplyModifiedProperties`。

## 7. SOPickerPopup 复用指南

`SOPickerPopup<T>`（`T : UnityEngine.Object`）是可搜索的弹窗列表，委托驱动展示：
- 构造函数：`(List<T> items, Action<T> onPicked, Func<T,Sprite> getIcon, Func<T,string> getName, Func<T,string> getType = null, Func<T,Color> getTypeColor = null, Func<T,(string path,Color color)> getFrame = null, bool confirmMode = false)`。
- 交互：`confirmMode=false` 单击即回调关闭；`confirmMode=true` 单击选中、双击或「确定」提交、「取消」放弃。
- 搜索：按 `getName` 忽略大小写过滤。

典型调用（在点击按钮的事件分支里）：

```csharp
if (GUILayout.Button("添加", EditorStyles.miniButton))
{
    Rect anchor = GUILayoutUtility.GetLastRect();
    PopupWindow.Show(anchor, new SOPickerPopup<AirdropData_SO>(
        candidates,
        picked => { /* 将 picked 的信息写入目标字段 */ },
        so => so.icon,
        so => string.IsNullOrEmpty(so.showName) ? so.name : so.showName,
        so => $"ID:{so.ID} · {so.TypeName}",
        so => so.Color));
}
```

参考调用方：`RoleSpeechGroupDrawer.cs`（数组追加元素）、`WeaponUpgradeEditorWindow.cs`、`AirdropData_SOEditor.cs`（附属战备添加/替换）。

## 8. 复杂示例解析

### 8.1 AirdropTabModule（列表内冲突标红）

- 重写 `RefreshData()`：`base.RefreshData()` 后两两比较 `Items`，把"同 ID 或同操作序列（均非空、逐元素相等）"的战备加入 `HashSet<AirdropData_SO> _conflictSet`。
- `DrawListItemContent` 中：`_conflictSet.Contains(data)` 为真时名称标签用 `Color.red` 并追加 `⚠ ID/操作冲突` 文本。

要点：冲突检测放刷新时而非每帧 Draw 内，避免 O(n²) 在重绘中反复执行；右键面另由自定义检视器提供明细。

### 8.2 AirdropData_SOEditor（右面板关联展示 + 冲突红卡）

- 全量扫描静态缓存：`AssetDatabase.FindAssets("t:AirdropData_SO")` + `EditorApplication.timeSinceStartup` 节流（1s）刷新，避免每次 OnInspectorGUI 重扫。
- 自身"编辑中未落盘"的值一律从 `serializedObject` 的 Property 读取（数组用 `arraySize`+`GetArrayElementAtIndex`），避免与 C# 字段不同步。
- `opter` 单行编辑器：每一步是一个下拉框（`EditorGUI.Popup`，选项为纯箭头 `←/↑/→/↓`），直接选择方向写回 `enumValueIndex`；`＋/－` 改 `arraySize`。
- `subAirdrop`（`int[]` 存战备 ID）：行内解析 `ID → AirdropData_SO`，无效 ID 红字；添加/替换走 `SOPickerPopup`；重复项忽略；排除自身。
- `coolGroup` 同组成员列表：`s_allSO.Where(o => o.coolGroup == group && o != self)`；绘制时含当前自身，全体按 ID 升序，当前项黄色「（当前）」标记（`DrawSoIcon` + `GUI.Label` 图标文本行），其余成员单击行 `EditorGUIUtility.PingObject` + `Selection.activeObject` 定位。
- 冲突红卡：同 ID / 同操作序列对象集合非空时在顶部画红底 `BeginVertical` 卡片。

## 9. 常见任务速查

### 让某页选中项标题带类型色
```csharp
protected override GUIStyle GetSelectedTitleStyle()
    => HasSelection ? DataEditorWindow.ColoredLabel(EditorStyles.boldLabel, Selected.Color) : EditorStyles.boldLabel;
```

### 列表项右侧放次级信息行（灰字小号）
在 `DrawListItemContent` 的 `EditorGUILayout.BeginVertical` 内追加：
```csharp
GUILayout.Label(..., new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = Color.gray } });
```

### 在 DrawRightPanelExtra 显示资产路径（绝大多数页面已做）
```csharp
GUILayout.Label(AssetDatabase.GetAssetPath(Selected), new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
```

### 让右面板编辑"预制体内的组件"
数据源 `GameObject` + `GetEditorTarget` 返回组件（`WeaponTabModule` 先例），组件字段即可在右面板序列化编辑。

## 10. 避坑与注意事项

- 新增枚举值未注册 → 字典 `KeyNotFoundException`；注册顺序影响标签页顺序。
- 数据路径约定 `Assets/Resources/GameData/<X>` 同时被运行时 `ResSvc.LoadObjects` 使用；`ToDictionary(item => item.ID)` 要求**同类型 ID 全局唯一**（ID=0 也占字典键，重复即抛异常）。
- 自定义检视器接管后必须复刻 `[InspectorName]`/`[Compare]`，否则字段名变英文且条件隐藏失效（不可逆地丢失 EditorOverride 行为）。
- 数据修改与"关联检测/展示"尽量读同一来源：左列表内全部 SO 在一次 `RefreshData` 中已加载完成，直接用 `Items`；右面板自定义检视器则维护静态缓存+节流。
- IMGUI 列表点击高亮依赖 `Host.DrawSelectionHighlight`，自行绘制列表项时不要破坏 `DrawItem` 的 GetLastRect 顺序。
- 编辑器代码放 `Assets/Editor/`（Editor 程序集），不要引用运行时才有的单例状态。
