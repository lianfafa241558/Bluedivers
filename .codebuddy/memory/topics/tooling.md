# 主题 · 编辑器扩展 / plans 脚本 / MCP 与编译验证

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。
> ⚠ 原文（含行尾/BOM 普查逐项数据、Play 域重载事故细节、脚本全文说明）已下沉：`archive/topics-snapshot-20261011/tooling.md`。

## 编辑器扩展
- 需读 `propertyPath` 才用 `PropertyDrawer`；⚠ 专属 `[CustomPropertyDrawer]` 手列字段 ⇒ **新增字段不会自动出现**（行高硬编码）
- `[Compare]` 按同层字段切换显隐；`[InspectorName]` = `UnityEngine.InspectorName`
- `[Foldout]`/`[DisplayField]`（`FPSGame.Attribute`）**只对字段有效**（属性无效）；`[AddComponentMenu]` 写在类级 `///` 之后
- `EditorWindow` 的私有字段域重载后能恢复；数据编辑器（`Tools/数据编辑器`）见 skill `data-editor`
- ⚠ 运行时脚本**不得** `using UnityEditor`（除 `#if UNITY_EDITOR` 守卫内）；asmdef 下的 `Editor/` 无特权 ⇒ `topics/architecture.md`

## 判据可信度
- ⚠ 文本搜索**会假阳性**（块注释 `/* */` 里的代码、异常字节漏检）⇒ 结论性判据必须"剥注释脚本 + 反射/Console"交叉复核
- ⚠ `svc_usage.py` **会漏报**（只抓 `== null`/类名写法）；权威消费点清单**以 ripgrep 为准**
- ⚠ linter 结果滞后于外部编辑；关键删除必须 grep 交叉核对

## MCP 操作细则
- ⚠⚠ Play 中触发编译/刷新 = **域重载** ⇒ 场景 MonoBehaviour 的**非序列化字段会被归零**（2026-10-07 疑似事故：`Actor.range` 全 null 而 `isInitialized` 为 true）⇒ **刷新前后都先读 `editor/state`，确认 `is_playing == false` 再动**。
- ⭐ **Play 模式规矩（2026-10-09 用户修订，取代旧的"绝对禁止"）**：用户进 Play **就是为了让我读运行中真实数据** ⇒ 先取数据、**数据到手就可以导入/编译**；只有"这局数据还要继续取"时才先不动编辑器。
- 开工先 `set_active_instance`（用 `mcpforunity://instances` 的**实时** ID）；写操作前确认 `projectRoot`。
- 新建 `.cs` 先 `refresh_unity(force, assets)`；改已存在文件只对**单个文件** `ImportAsset(path, ForceUpdate)`。
- ⚠ 反射写完字段**下一句就 `GetValue` 判"生效没"会误判**（`RequestScriptCompilation` 异步）⇒ 隔一次工具调用再读；判据用**签名/成员是否存在**，不要只看 mtime。
- ⚠ `execute_code` 报 `No result found` 但**可能已执行** ⇒ 先查实际效果再决定重试；`refresh_unity(wait_for_ready:true)` 必报该错，用 `false`。超 30s 预算的调用（`execute_code`/长导入/`run_tests`）失败后**不要盲目重试**。
- ⭐ **离屏验证需要"运行时 Manager"时的注入点**（比进 Play 安全）：`Singleton<T>` 只有 `private static T instance` + 只读属性 ⇒ `new GameObject()` + `AddComponent<T>()`，再反射写**基类字段** `typeof(T).BaseType.GetField("instance", NonPublic|Static).SetValue(null, comp)`（⚠ **别用** `typeof(T).GetProperty("Instance")`——它在泛型基类上取不到、返回 null，会把自己坑成 NRE），然后把 Manager 的数据字段反射塞进去；用完 `DestroyImmediate` 两个临时对象。
- ⚠ **改名/搬 `.cs` 必须走 Unity**：手动改名 ⇒ GUID 变 ⇒ prefab 变 Missing Script，而 Console 只报一条极易忽略。修复 = 旧 guid 写回新 `.meta` + **强制重导入该组件涉及的所有 prefab**；任何改名后立刻查该 `guid` 的命中。

## 行尾 / 编码（现状口径；普查数据在 archive）
- 仓库侧**已统一 LF**：`.gitattributes` = `* text=auto eol=lf`（**覆盖** `core.autocrlf`，跨机生效）+ 仓库级 `git config core.autocrlf false`；`.editorconfig` 也已 `end_of_line = lf`。
- ⚠ **工作树里仍有 ~2250 个 `w/crlf`/`w/mixed`**（只有重新签出/重存才会变 LF）。⚠ `w/mixed` 是 `replace_in_file` 的**高危面**（孤立 LF 躲在别处）⇒ 这类文件要么先归一、要么走**字节级脚本 + 命中数断言**。
  ⚠ 绝不能用 `git checkout -f` / `reset --hard` / 对**已修改**文件用 `git checkout-index -f`（会丢未提交改动）。
- **BOM 口径**：`.cs` **加** BOM（防工具按 GBK 猜）；`.unity`/`.prefab`/`.meta`/`.json`/**`.md` 绝不加**（解析器会当内容、`git diff` 首行多 `\ufeff`、正则 `^` 被挡）；编辑时**保持现状**（有就留、没有不加）。
  `.editorconfig` 的 `charset = utf-8-bom`：必须写在 `[*.cs]`/已有 `[*.{cs,vb}]` 段（写进 `[*]` 会给 json/md 也加 BOM），且**只管 "IDE 保存"**，管不到 Unity/脚本/git 写的文件。
- ⚠ 已知**不可逆损坏**：`TaskManager.cs` 内有 24 个 `U+FFFD`（GBK 解码 UTF-8 中文后回存）；另有 9 个 `.shader` 是 GBK 编码（非 BOM 且非法 UTF-8）——**任何人按 UTF-8 存一次即损坏**。

## 编译验证
- ⭐⭐ **首选 = 离线编译 `python -X utf8 .codebuddy/plans/offline_compile.py <AsmName>...`**：借 Unity 自带 Roslyn 按 `.csproj` 编译，**~0.5s、完全不触发域重载**。要点：① 兄弟程序集走 `ProjectReference` → `Temp/offline_compile/<Name>.dll`（**优先**）或 `Library/ScriptAssemblies/`；② **必须按依赖顺序传参**（下游才吃到新 dll）；③ 本机**没装 .NET SDK** ⇒ `dotnet build` 走不通，只能借 Unity 自带 Roslyn；④ 输出只落 `Temp/offline_compile/`（安全）；⑤ `ROOT` 自动推导（`BLUEDIVERS_ROOT`/`UNITY_EDITOR_DATA` 可覆盖）。
  ⚠ **它只认 csproj 的文件清单** ⇒ **刚新建、Unity 还没重生成 csproj 的文件编不到**（表现为 `源:N` 不变、报 0 错误，但 Unity 报 `CS0103` 甚至进 Safe Mode）⇒ **新增 `.cs` 的首次验证必须看 Unity Console**。
- **三证齐**（必须经 Unity 时）：dll mtime 前进 + Console 0 error（**先清空再读**）+ `is_compiling:false`。
- ⚠⚠ **某程序集编译失败 ⇒ 依赖它的程序集既不报错也不编译** ⇒ 错误逐层暴露、必须迭代；决定性判据 = "哪个 dll 没更新" + `GetAssemblies().sourceFiles` 是否含新文件。
- ⚠⚠ **「假空转」**（2026-10-02 实测）：内容已变但增量判定为空 ⇒ dll 不动、Console **也 0 error**、`refresh_unity` 照样返 `refresh_triggered:true`、`ImportAsset(ForceUpdate)` 也白搭 —— 三个"通过"全是假象。**判据改反射**（`GetType/GetMethod` 为 null = 没编上）。**解药** = `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)`（~30s）；⚠ 若 `external_changes_dirty:true`（Unity 未 focus），它单独调用仍空转 ⇒ 先 `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` 再请求编译。
- ⚠ **「假缺失」= `.cs.meta` 被写成等长全 `\x00`**（`git status` 干净、`read_file` 判 binary、肉眼无异常）⇒ Unity 导不出 guid ⇒ 该 `.cs` **不进编译** ⇒ 报 `CS0103: 名称"X"不存在`，而文件明明在磁盘上。判据 = `manage_asset get_info` 返回 `guid:""`/`assetType:"Unknown"`。修法 = 按同目录正常 meta 逐字节重写 + 换新 GUID（MonoBehaviour 要先查引用）。
  ⇒ 口径：**`CS0103` 但源文件确实存在 ⇒ 先查 meta/导入状态，别改代码**。
- ⚠ `editor/state` 的 `external_changes_dirty` **不可信**；`refresh` 之后不能立刻读 Console；`isCompiling=True` 时读 dll mtime 太早。

## 构建 / 启动画面
- 打 Windows 包输出到 `D:\Pro\Verion\BlueDivers`（StandaloneWindows64 + IL2CPP）；`BlueDivers_Data/RuntimeInitializeOnLoads.json` 是「`RuntimeInitializeOnLoadMethod` 回调有没有进包」的**权威证据**（`loadTypes`：0=AfterSceneLoad, 1=BeforeSceneLoad, 2=AfterAssembliesLoaded, 3=BeforeSplashScreen）。
- ⛔ `00Tools/SkipUnityLogo.cs` **注定无效**：本机 `HasProLicense()==False`（Personal）下 Unity 徽标由**原生播放器**强制绘制，`SplashScreen.Stop()` 被当 no-op；回调**已确认进包** ⇒ 与写法/`#if`/asmdef/IL2CPP 裁剪**都无关**，别再折腾。要真去掉只能升 Pro 或换 Unity 6。

## `.codebuddy/plans/` 脚本清单（常用）
| 脚本 | 用途 |
|---|---|
| `audit_markers.py` | **标志物审计**（标志串 + 期望次数跨文件计数，输出"缺失项"）；**大批改动收尾必跑**，须 `缺失项=0` |
| `offline_compile.py <asm>...` | ⭐ **离线编译验证**（Unity 自带 Roslyn 按 csproj，不碰 Unity）；须按依赖顺序传参 |
| `dll_mtime.py` | 判"是否已编译"（dll mtime vs 源文件 mtime） |
| `asmdef_refs.py` | asmdef 的 GUID references → 程序集名 |
| `bom_vs_head.py` | 对比工作区 vs git HEAD 的 BOM，`--fix` 只补"本次误删的" |
| `ns_move.py <moves.json> [--no-self]` | 通用「命名空间搬迁引擎」（改 ns + 同步 using + 修 `using static`） |
| `add_usings.py <pairs.json>` / `fix_missing_using.py` | 补 `using`（字节级保 BOM） |
| `find_dangling_guids.py` / `fix_guid_dangling.py` / `scan_missing_scripts.py` / `find_missing_script_refs.py [--under <路径>]` / `dep_closure_missing.py <根> [--max N]` / `missing_component_locate.py <资产> [guid]` | 悬空引用 / Missing Script / 缺组件的定位与修复（后两者做**依赖闭包 + 父链回溯** —— `Resources.LoadAll` 的报错常来自依赖链深处，不在目标文件夹里） |
| `sr_ns_audit.py` / `sr_ns_repair.py --apply` | 改 ns 对 `[SerializeReference]` 的审计与修复 |
| `tidy_contract_enums.py` | 枚举目录规范化 + 与 HEAD 逐枚举比对「成员名序列」（`语义差异项` 必须为 0） |
| `memory_gate.py` / `memory_archive_legacy.py --cut <日期> [--delete]` / `memory_orphan_scan.py` | 记忆分层体检 / 旧 daily 归档（先出索引再删正文）/ 删前"知识漏搬"体检 |
| `svc_usage.py` / `battle_usage.py` / `contract_use.py` | 服务/契约消费点统计（⚠ `svc_usage.py` **会漏报**，权威以 ripgrep 为准） |
| `p2_ns_plan.py` / `env_probe.py` / `find_unity.py` / `prefab_tree.py` / `rooms_hierarchy.py` | 目录↔命名空间对齐计划 / 探测离线编译工具链 / 定位 Unity 安装 / prefab 层级树 |

（其余 `apply_*.py`、`note_*.py`、`p*_*.py` 都是**一次性**替换/记录脚本，用完即历史；`keyscreen_stage_sync.py` / `net_sync_b1*.py` / `net_sync_b2.py` 是 2026-10-11 的联机同步批次脚本，可重跑（dry-run + `--apply`、行尾自适应、锚点唯一断言）。）
