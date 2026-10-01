# 主题 · 编辑器扩展 / plans 脚本 / MCP 与编译验证细则

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。

## 编辑器扩展
- 需要读 `propertyPath` 才用 `PropertyDrawer`；⚠ 专属 `[CustomPropertyDrawer]` 手列字段 ⇒ **新增字段不会自动出现**（行高是硬编码的）
- `[Compare]` 按同层字段切换显隐；`[InspectorName]` = `UnityEngine.InspectorName`
- `[Foldout]` / `[DisplayField]` 在 `FPSGame.Attribute` **只对字段有效**（属性无效）；`[AddComponentMenu]` 写在类级 `///` 之后
- `EditorWindow` 的私有字段在域重载后能恢复；给特性类补 `Attribute` 后缀零成本
- 数据编辑器（`Tools/数据编辑器`）：`DataEditorWindow` + `DataTabs/`，扩展方式见 skill `data-editor`
- ⚠ 运行时脚本**不得** `using UnityEditor`（除 `#if UNITY_EDITOR` 守卫内）；asmdef 下的 `Editor/` 无特权，见 `topics/architecture.md`

## 搜索与判据的可信度
- ⚠ 文本搜索**会假阳性**（块注释 `/* */` 里的代码、异常字节漏检）⇒ 结论性判据必须用**剥注释脚本** + 反射/Console 交叉复核
- ⚠ `svc_usage.py` **会漏报**（只抓 `== null` / 类名写法）；权威消费点清单**以 ripgrep 为准**
- ⚠ linter 结果滞后于外部编辑（见热区 1"编辑"那条），关键删除必须 grep 交叉核对

## MCP 操作细则
- ⚠⚠⚠ **绝不主动触发编译/刷新**（`AssetDatabase.Refresh` / `ImportAsset` / `RequestScriptCompilation` / `refresh_unity` 全算）：一旦触发脚本编译，`com.unity.ide.visualstudio` 一定会重新生成 `.csproj`/`.sln`，**VS 的自动重载开关挡不住**，会打断用户。要编译先问，或让用户自己切回 Unity；**验证只用只读手段**（`read_console`、`editor/state`、dll mtime、反射 `execute_code`——只写 `%TEMP%`，安全）
- 开工先 `set_active_instance`（用 `mcpforunity://instances` 的**实时** ID）；写操作前确认 `projectRoot`
- 新建 `.cs` 先 `refresh_unity(force, assets)`；改已存在文件只对**单个文件**做 `ImportAsset(path, ForceUpdate)`
- ⚠ `execute_code` 报 `No result found` 但实际可能已执行 ⇒ 先查实际效果再决定是否重试；`refresh_unity(wait_for_ready:true)` 必报该错，用 `false`
- ⚠ 超 30s 预算的调用（`execute_code`、长导入、`run_tests`）失败后**不要盲目重试**，先查实际效果
- ⚠ 改名实录：用户手动 `ArchiveSvc.cs → ArchiveLoader.cs` 未走 Unity ⇒ GUID 变 ⇒ prefab 变 Missing Script，Console 只报一条极易忽略。修复 = 旧 guid 写回新 `.meta` + **强制重导入 prefab**；任何改名后立刻查该组件的所有 prefab

## 编译验证
- ⭐⭐ **首选 = 离线编译 `python -X utf8 .codebuddy/plans/offline_compile.py <AsmName>...`**：借 Unity 自带 Roslyn（`D:\UnityHub\Editor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll` + `NetCoreRuntime\dotnet.exe`）按 Unity 生成的 `.csproj` 编译，**~0.5s/程序集、完全不触发 Unity 域重载**（彻底绕开下面那条红线）。要点：① 兄弟程序集走 `<ProjectReference><Name>` → 映射 `Temp/offline_compile/<Name>.dll`（**优先**，本轮新产物）或 `Library/ScriptAssemblies/<Name>.dll`；② 必须**按依赖顺序**传参，下游才能吃到新 dll；③ `-nostdlib+` + csproj 的 `HintPath` 引用（netstandard + System shims + UnityEngine 模块）；④ 本机**没装 .NET SDK**（`dotnet --list-sdks` 空）⇒ 只能借 Unity 自带 Roslyn，`dotnet build` 走不通；⑤ 输出只落 `Temp/offline_compile/`，安全。**用途：改 ns / using / 搬文件后立刻验证，红了按报错补 using 再跑（循环 <1s）**
- **三证齐**（必须经 Unity 时）：`Library/ScriptAssemblies/*.dll` mtime 前进 + Console 0 error（**先清空再读**）+ `is_compiling:false`
- **空转判据**：有错 + dll mtime 不动 + 错误行号比源码少 1 行 ⇒ 编译器读的是旧快照；治法 = `ImportAsset + Refresh + RequestScriptCompilation()`，或 Python `os.utime()`
- ⚠⚠ 某程序集编译失败 ⇒ **依赖它的程序集既不报错也不编译** ⇒ 错误会逐层暴露，必须迭代；决定性判据 = "哪个 dll 没更新" + `GetAssemblies().sourceFiles` 是否含新文件
- ⚠ `editor/state` 的 `external_changes_dirty` **不可信** ⇒ 只看 dll mtime vs 源文件 mtime（`dll_mtime.py`）
- `refresh` 之后不能立刻读 Console；`isCompiling=True` 时读 dll mtime 太早

## `.codebuddy/plans/` 脚本清单
| 脚本 | 用途 |
|---|---|
| `audit_markers.py` | **标志物审计**：标志物字符串 + 期望次数跨文件计数，输出"缺失项"；大批改动收尾必跑 |
| `bom_vs_head.py` | 对比工作区 vs git HEAD 的 BOM，`--fix` 只补"本次误删的"；收尾验 `regressions=0` |
| `dll_mtime.py` | 判"是否已编译"（dll mtime vs 源文件 mtime） |
| `asmdef_refs.py` | asmdef 的 GUID references → 程序集名 |
| `prune_using.py` / `fix_using_ns.py` / `fix_missing_using.py` | using 剪枝 / 误删恢复 / 补缺失 using |
| `sr_ns_audit.py` / `sr_ns_repair.py --apply` | 命名空间改动对 `[SerializeReference]` 的审计与修复 |
| `p53_guid_repair.py` / `p53_guid_check.py` / `scan_missing_scripts.py` | 搬 `.cs` 后的 GUID / Missing Script 审计 |
| `offline_compile.py <asm>...` | ⭐ **离线编译验证**（Unity 自带 Roslyn 按 csproj 编译，~0.5s，不碰 Unity）；须按依赖顺序传参 |
| `ns_move.py <moves.json> [--no-self] [--apply]` | 通用「命名空间搬迁引擎」：改 `namespace` + 同步 `using` + 修 `using static <ns>.<Type>`；⚠ `--no-self` 跳过会误判的自省补 using（交离线编译兜底） |
| `add_usings.py <pairs.json> [--apply]` | 按清单补 `using <ns>;`（字节级保 BOM） |
| `find_dangling_guids.py` / `fix_guid_dangling.py [--apply]` | 悬空引用**权威判定** / 修复（内容哈希→唯一同名→类名三路匹配） |
| `p2_ns_plan.py` | 生成 06Gameplay「目录↔命名空间」对齐计划 → `p2_ns_plan.md` |
| `env_probe.py` / `find_unity.py` | 探测离线编译工具链 / 定位 Unity 安装目录（`Editor.log`） |
| `tidy_contract_enums.py` | 枚举目录字节规范化 + 与 HEAD 逐枚举比对「成员名序列」 |
| `memory_gate.py` | 记忆分层体检（各层字符数 vs 预算、超限与候选下沉段落） |
| `memory_archive_legacy.py` | 过期 daily 归档：生成标题索引 `--cut <日期>`，加 `--delete` 删正文 |
| `memory_orphan_scan.py` | 删旧 daily 前的"知识漏搬体检"：找出只在旧 daily 出现、长期记忆里查无此名的实体 |
| `svc_usage.py` / `battle_usage.py` / `contract_use.py` | 服务/契约消费点统计（⚠ `svc_usage.py` **会漏报**，权威清单以 ripgrep 为准） |
| `apply_*.py` | 一次性批量替换脚本（字节级 + count 校验），用完即历史 |
