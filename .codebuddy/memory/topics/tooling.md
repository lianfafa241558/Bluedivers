# 主题 · 编辑器扩展 / MCP 与编译验证

> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。
> 2026-10-18 拆出：`.codebuddy/plans/` 脚本清单 → `plans-scripts.md`。
> ⚠ 原文（行尾/BOM 普查逐项数据、Play 域重载事故细节、脚本全文说明）已下沉：`archive/topics-snapshot-20261011/tooling.md`。

## 编辑器扩展
- 需读 `propertyPath` 才用 `PropertyDrawer`；⚠ 专属 `[CustomPropertyDrawer]` 手列字段 ⇒ **新增字段不会自动出现**（行高硬编码）
- `[Compare]` 按同层字段切换显隐；`[InspectorName]` = `UnityEngine.InspectorName`
- `[Foldout]`/`[DisplayField]`（`FPSGame.Attribute`）**只对字段有效**（属性无效）；`[AddComponentMenu]` 写在类级 `///` 之后
- `EditorWindow` 的私有字段域重载后能恢复；数据编辑器（`Tools/数据编辑器`）见 skill `data-editor`
- ⚠ 运行时脚本**不得** `using UnityEditor`（除 `#if UNITY_EDITOR` 守卫内）；asmdef 下的 `Editor/` 无特权 ⇒ `topics/architecture.md`
- ⭐ **美化某 SO 的 Inspector = 写专属 `[CustomEditor]`**（照 `AirdropData_SOEditor`：色条 + SectionTitle + 自绘块），**禁止**挂进全局 fallback `EditorOverride`（会波及所有 SO，2026-10-15 被叫停）；特性 Drawer 挂数组字段只逐元素生效，数组级自绘只能由 Editor 显式分支驱动。
  公共件下沉在基类 `Assets/Editor/Drawer/DataSOEditorBase.cs`（`EnsureStyles`/`SectionTitle`/`ColorBar`/`TagLabel`/`BeginBlockHeader`/`RowLabel`/`DrawTintedIcon`/`LabelOf`/`DrawField`/`RefreshAssetCache<T>`/`ShowSoPicker<T>`/`ApplyDirty`），新写 SO 检视器一律继承它。

## 判据可信度
- ⚠ 文本搜索**会假阳性**（块注释 `/* */` 里的代码、异常字节漏检）⇒ 结论性判据必须"剥注释脚本 + 反射/Console"交叉复核
- ⚠ `svc_usage.py` **会漏报**（只抓 `== null`/类名写法）；权威消费点清单**以 ripgrep 为准**
- ⚠ linter 结果滞后于外部编辑；关键删除必须 grep 交叉核对
- ⚠ `audit_markers.py` 的清单停在 `BattleEventSub→BattleEventBus` 改名 / `Services/BattleHub.cs`→`BattleHub.cs` 换路径之前（实测 14 MISS 全是这类，不是真丢内容）⇒ 要判真丢没丢，用"旧文件每行是否存在于新文件集合"的逐行覆盖检查。

## MCP 操作细则
- ⚠⚠ Play 中触发编译/刷新 = **域重载** ⇒ 场景 MonoBehaviour 的**非序列化字段会被归零**（2026-10-07 疑似事故：`Actor.range` 全 null 而 `isInitialized` 为 true）⇒ **刷新前后都先读 `editor/state`，确认 `is_playing == false` 再动**。
- ⭐ **Play 模式规矩（2026-10-09 用户修订，取代旧的"绝对禁止"）**：用户进 Play **就是为了让我读运行中真实数据** ⇒ 先取数据、**数据到手就可以导入/编译**；只有"这局数据还要继续取"时才先不动编辑器。
- 开工先 `set_active_instance`（用 `mcpforunity://instances` 的**实时** ID）；写操作前确认 `projectRoot`。
  ⚠ **实例 hash 不稳**（2026-10-14 同机两次会话分别报 `Bluedivers@3d9f2357` 与 `Bluedivers@4a3e6a7b`，端口恒 6400）⇒ 别背 hash；**只有 1 个实例时 server 自动选中**（多实例才必须 `set_active_instance`）。
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
- ⚠ **新建 `.cs` 编不到 ⇒ 用临时 csproj 注入**：`plans/tmp_compile_newfile.py <asm> <新.cs>...`（用完自清理）；离线编译只认 Unity 已生成的 csproj。
- ⚠ **编辑器失焦时重编会被卡住**（2026-10-14 实测：`refresh_unity` / `AssetDatabase.Refresh(ForceUpdate)` / 逐文件 `ImportAsset` / `RequestScriptCompilation(CleanBuildCache)` / `Assets/Refresh` 菜单全试过，只让 `isCompiling` 翻真又落假，dll 与 Bee `artifacts` 都不动）⇒ 解药 = 让用户聚焦 Unity 或 Ctrl+R；判据仍是 **dll mtime + 反射**。
- ⚠ `offline_compile.py` 的依赖解析**优先**用 `Temp/offline_compile/<asm>.dll`（本次自产的旧 dll）⇒ 只编"最上层那一个"会被上一次留下的陈旧依赖骗出假错（实测 2026-10-14 编 06_Gameplay 报 `FpsHelper_Hit.cs: TargetCfg 未包含 All`，坏的是 `Temp/offline_compile/01_GameContract.dll`）。解药：报错涉及别的程序集的类型时，把那条依赖链一起编（`01_GameContract 05_UnitCore 06_Gameplay`）。
- ⚠ MCP `read_console` 偶发**连 `types=["all"]` 都返回 0 条**（不可信，别只靠它判"0 error"）⇒ 关键改动必须用反射核验成员是否存在。
- 常用小工具：`plans/recent_files.py`（列最近改动文件 ⇒ 判"到底编没编"）、`plans/tail_lines.py`（PowerShell 无 tail）、`plans/check_sources.py`（核 `.cs` 行数，防"空文件也 0 error 假通过"）。

## 构建 / 启动画面
- 打 Windows 包输出到 `D:\Pro\Verion\BlueDivers`（StandaloneWindows64 + IL2CPP）；`BlueDivers_Data/RuntimeInitializeOnLoads.json` 是「`RuntimeInitializeOnLoadMethod` 回调有没有进包」的**权威证据**（`loadTypes`：0=AfterSceneLoad, 1=BeforeSceneLoad, 2=AfterAssembliesLoaded, 3=BeforeSplashScreen）。
- ⛔ `00Tools/SkipUnityLogo.cs` **注定无效**：本机 `HasProLicense()==False`（Personal）下 Unity 徽标由**原生播放器**强制绘制，`SplashScreen.Stop()` 被当 no-op；回调**已确认进包** ⇒ 与写法/`#if`/asmdef/IL2CPP 裁剪**都无关**，别再折腾。要真去掉只能升 Pro 或换 Unity 6。

## AI 协作成本口径（2026-10-18 用户问过两次；原在 `MEMORY.md` 热区，2026-10-26 下沉）

- 事实：**每轮都重发全历史** ⇒ 会话累计 ≈ 上下文 × 轮数（实测 3.8M ≈ 20+ 轮 × ~150k）。
- 五条杠杆（按收益排）：
  1. **换任务开新窗口** —— 判据已沉 topics，续接只要几千 token；
  2. **工具输出收敛** —— 大文件用 `offset/limit` 读片段、命令/脚本只打摘要行、**不重复整篇读同一文件**、不重复拉 tool description（带整段 server config）；
  3. **少往返** —— 一次写对 + 并行工具 + 只验证一次（别"改→编→错→改→编"）；
  4. **常驻注入瘦身** —— rules/skills 精简、MCP 只激活要用的 tool group；
  5. **多文件探索丢给 `code-explorer` subagent** —— 结果不进主上下文。
- 输出侧（回复长度 / 注释长度）是**小头**，别本末倒置。
