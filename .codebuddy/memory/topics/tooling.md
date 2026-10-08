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
- ⚠ **`offline_compile.py` 按 Unity 生成的 `.csproj` 取源文件** ⇒ **刚新建的文件不在里面**，表现为莫名 CS0103/CS0117（"类型不存在"）。
  验证办法：正常编译一遍后，把新文件路径**追加进 `Temp/offline_compile/<asm>.rsp`** 再跑一次 `csc @rsp`（同引用集 ⇒ 结论可信，2026-10-07 实测有效）。
- ⚠ **`Library/ScriptAssemblies/*.dll` 的 mtime 不可靠**：2026-10-07 实测"域重载确实发生了（`editor/state` 的 `last_domain_reload_after` 前移），但 dll 时间戳纹丝不动" ⇒ 别用它判断"有没有编译"，**要看加载进来的类型**（反射 `execute_code`）。
- ⚠ **Unity 可能静默不重编译**：`external_changes_dirty:false` + 编辑器未聚焦（`is_focused:false`）时，`refresh_unity`（`compile:"request"`）会返回成功但**什么都没做**。
  可靠触发：`execute_code` 里对目标文件逐个 `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)`（之后 `EditorApplication.isCompiling` 立刻为 true）。
- ⭐ **诊断日志开关 = `FPSGame.Utils.NetSyncLog`（`Assets/Scripts/00Tools/NetSyncLog.cs`，2026-10-09 建）**：默认全关（2026-10-09 晚调整：**打包端也默认关**了，原来"打包端默认开"会把 `Player.log` 刷爆）；
  `Enabled` 总开关 + `Bullet`/`Ai`/`Sync` 三路分管 + `Warn`（"不正常"用 Warning，便于在 Console 筛）。
  **Play 里开**：`execute_code` → `FPSGame.Utils.NetSyncLog.Enabled = true;`（跑到关键操作后用 `read_console` 读）。
  **打包端开**（它没有 execute_code，日志只落自己的 `Player.log`）：命令行 `-netsynclog` 或环境变量 `NETSYNC_LOG=1` 或 `PlayerPrefs["NetSyncLog"]=1`。
  ⚠ 放 `00_Utils` 是因为 05/06/09/10 都引用它；**`07_NetGame` 只引用 `02_Net`，看不见它** ⇒ 传输层日志点要放在 09 的桥里。
  已下的点：子弹（`FriendWeaponView.PlayShoot` / `WeaponBaseController.SpawnVisualBullet` / `ProjectileStandard.Update`+`Hit` / `FpsHelper.PlayImpactFx`）、
  AI（`EnemyController.SetNavDestination` / `EnemyNetBridge` 收发暂存补发 / `NPCWalk` / `SceneUnitMoveSink` 的 `RemoteDriven` 总闸）、
  收尾（`NetFriendBridge.HandlePlayerLeft` / `MiniMapWnd.FriendLeave` / `PlayerWnd` 盟友行数）。
- ⚠ **Play 模式规矩（2026-10-09 用户修订，取代旧的"绝对禁止"）**：用户进 Play **就是为了让我读运行中的真实数据**
  ⇒ 先取数据、**数据到手就可以导入/编译**；只有"这局的数据我还需要继续取"时才先不动编辑器（旧口径 = 一律中止，会白白浪费他的一次复现）。：那会触发 Play 中的**域重载**（MCP 表现为 `refresh recovered after Unity disconnect/retry`），场景里 MonoBehaviour 的**非序列化字段会被归零**。2026-10-07 疑似实例：几次 `refresh_unity` 落在用户 Play 大厅期间，随后 Console 刷屏 `NullReferenceException @ Actor.cs:388`（`Range.GetXY()`），反射读 11 个 Actor 的 `range` **全 null** 而 `isInitialized` 却为 true（未能复证：Play 已停 + Console 被清空）。⇒ **刷新前后都先读 `mcpforunity://editor/state`，确认 `play_mode.is_playing == false` 再动**
- 开工先 `set_active_instance`（用 `mcpforunity://instances` 的**实时** ID）；写操作前确认 `projectRoot`
- 新建 `.cs` 先 `refresh_unity(force, assets)`；改已存在文件只对**单个文件**做 `ImportAsset(path, ForceUpdate)`
- ⚠ `execute_code` 报 `No result found` 但实际可能已执行 ⇒ 先查实际效果再决定是否重试；`refresh_unity(wait_for_ready:true)` 必报该错，用 `false`
- ⚠ 超 30s 预算的调用（`execute_code`、长导入、`run_tests`）失败后**不要盲目重试**，先查实际效果
- ⚠ 改名实录：用户手动 `ArchiveSvc.cs → ArchiveLoader.cs` 未走 Unity ⇒ GUID 变 ⇒ prefab 变 Missing Script，Console 只报一条极易忽略。修复 = 旧 guid 写回新 `.meta` + **强制重导入 prefab**；任何改名后立刻查该组件的所有 prefab
- ⭐ **离屏验证需要"运行时 Manager"时的注入点**（2026-10-06 实测，比进 Play 安全）：`Singleton<T>`（`00Core/Singleton.cs`）只有一个 `private static T instance` 字段 + 只读属性 `Instance => instance` ⇒ 想在编辑模式喂数据，就 `new GameObject()` + `AddComponent<T>()`，再用反射写 **基类字段** `typeof(T).BaseType.GetField("instance", NonPublic|Static).SetValue(null, comp)`（⚠ 别用 `typeof(T).GetProperty("Instance")`，它在泛型基类上**取不到、返回 null**，会把自己坑成 NRE），然后把 Manager 的数据字段（如 `TaskManager.MapData`）反射塞进去。这样离屏就能跑通依赖 Manager 的显示逻辑（本会话用它验证了行背景图取 `MapData_SO`），用完 `DestroyImmediate` 两个临时对象。
- ⚠ 反射写完字段后**下一句就 GetValue 判"生效没"会误判**（`RequestScriptCompilation` 是异步：dll mtime 已变、类型还是旧的）⇒ 隔一次工具调用再读；判据用**签名/成员是否存在**（如 `BuildSubTitle` 的参数个数），不要只看 mtime。

## 行尾 / 编码基线（2026-10-02 实测 `git ls-files --eol`，14538 个受控文件）
- **索引侧已统一 LF**（`i/lf` 一片、`i/crlf` = **0**）：`.gitattributes` 只有 `* text=auto`；`.editorconfig` 也已写 `end_of_line = lf`
- **冲突源 = `core.autocrlf=true`**（本机全局）⇒ 签出把文本转 CRLF，而 IDE 按 `.editorconfig` 存 LF ⇒ 工作树混：`w/crlf` **2107**、`w/mixed` **60**（`.cs` 53 / `.shader` 3 / `.md` 1 …），`.cs` 另有 344 个 `w/lf`
- Unity 自写资产偏 LF：`.meta` 6346 LF vs 1017 CRLF、`.prefab` 834 vs 210、`.anim` 254 vs 6、`.mat` 635 vs 51
- ⇒ **已统一为 LF（2026-10-02 执行）**：`.gitattributes` = `* text=auto eol=lf`（attributes **覆盖** `core.autocrlf`，跨机生效）+ 仓库级 `git config core.autocrlf false`（原值来自 **system** 配置的 `true`，`--global` 本就没有；仓库级配置不随库走，但 `eol=lf` 会兜住）
- **探针实证**（`plans/gitattr_probe.py`，`git hash-object --path=<路径>` 走 clean 过滤器 + `--no-filters` 取原始哈希）：文本 `a-CRLF-b` 过滤后 == 纯 LF blob ⇒ 归一生效；含 NUL 的二进制在 `.cs`/`.png`/`.bin` 路径下过滤前后哈希**完全一致** ⇒ 二进制未被转换（`text=auto` 保留了自动判定）；`git status` 未因此冒出一片 modified（25 条 = 既有改动 + 本次编辑，**没有 mass-modified**）
- ⚠ **仍未做**：工作树里那 ~2250 个 `w/crlf`/`w/mixed` 文件在磁盘上还是老样子 —— 只有**重新签出**或**重存**才会变 LF。安全做法是原地 EOL 转换（EOL-only，不影响 `git status`）；⚠ 绝不能用 `git checkout -f` / `reset --hard` / 对**已修改**文件用 `git checkout-index -f`（会丢未提交改动）
- ⚠ `w/mixed` 的文件是 `replace_in_file` 的高危面（`TaskManager.cs` 就是：孤立 LF 躲在 `using` 块 7-12、`SyncTaskState` 方法体 39/40、末尾 26 行注释块）⇒ 这类文件的编辑要么先归一，要么走字节级脚本 + 命中数断言

## 编码基线 · BOM（2026-10-02 实测）
- **`.cs`：BOM 516 / 无 BOM 256**，且**全部是合法 UTF-8**（⇒ Roslyn 先严格试 UTF-8、失败才回退 ANSI ⇒ **无 BOM 不影响编译**）。规范要求 with BOM，未达标根因 = `.editorconfig` 缺 `charset` ⇒ **已修（2026-10-02）**：在末段 `[*.{cs,vb}]` 末尾加 `charset = utf-8-bom`（原文件纯 CRLF、`insert_final_newline = false`，插入时保持该风格）；**只管 IDE 保存**，256 个存量文件仍无 BOM（打开→保存即收敛，或跑批量补 BOM 脚本）
- **`.unity` 40 / `.prefab` 1044 / `.meta` 7165 / `.json`：带 BOM = 0（正确，别加）**；`*.md` 也别加
- **已发生不可逆损坏**：`TaskManager.cs` 内有 **24 个 `U+FFFD`**（`EF BF BD`，如"选择的敌人类�?"）—— 机制 = 用 GBK 解码 UTF-8 中文 → 回存，信息永久丢失
- ⚠ **9 个 `.shader` 是 GBK 编码**（`Blur`/`ReBloom`/`GlowTexture`/`Holography`/`MaskTexture`/`NewOldTV`/`WarningWall`/`VolumetricCloud_URP_Hemisphere`/`FogEffect`）：非 BOM 且非法 UTF-8，目前只伤注释（`NewOldTV.shader` 按 GBK 能解出"全局系数/屏幕扭曲程度"），任何人按 UTF-8 存一次即损坏
- 结论口径：**`.cs` 加 BOM（防工具按 GBK 猜）**；**Unity 资产/JSON 绝不加**（解析器会把 BOM 当内容、`git diff` 首行多 `\ufeff`、正则 `^` 会被 BOM 挡住 —— `ns_move.py` 踩过）；编辑文件时**保持现状**（有就留、没有不加）
- ⚠ `.editorconfig` 的 `charset = utf-8-bom` 的**作用边界**：只对"**IDE 保存**"生效（新建文件 + 已有文件下次保存都带 BOM），**管不到** Unity 自己写的、脚本/PowerShell/git 写的文件（那些要单独补）；**必须写在 `[*.cs]` 或已有的 `[*.{cs,vb}]` 段，别写进 `[*]`**（否则 VS 存 `.json`/`.md` 也会带 BOM）；EditorConfig 规范自己把 `utf-8-bom` 标为 discouraged，但 .NET/本项目规范要它

## 编译验证
- ⭐⭐ **首选 = 离线编译 `python -X utf8 .codebuddy/plans/offline_compile.py <AsmName>...`**：借 Unity 自带 Roslyn（`D:\UnityHub\Editor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll` + `NetCoreRuntime\dotnet.exe`）按 Unity 生成的 `.csproj` 编译，**~0.5s/程序集、完全不触发 Unity 域重载**（彻底绕开下面那条红线）。要点：① 兄弟程序集走 `<ProjectReference><Name>` → 映射 `Temp/offline_compile/<Name>.dll`（**优先**，本轮新产物）或 `Library/ScriptAssemblies/<Name>.dll`；② 必须**按依赖顺序**传参，下游才能吃到新 dll；③ `-nostdlib+` + csproj 的 `HintPath` 引用（netstandard + System shims + UnityEngine 模块）；④ 本机**没装 .NET SDK**（`dotnet --list-sdks` 空）⇒ 只能借 Unity 自带 Roslyn，`dotnet build` 走不通；⑤ 输出只落 `Temp/offline_compile/`，安全。**用途：改 ns / using / 搬文件后立刻验证，红了按报错补 using 再跑（循环 <1s）**；⑥ 2026-10-08 已改成**自动推导**：`ROOT` = 脚本位置的上两级（可用环境变量 `BLUEDIVERS_ROOT` 覆盖）、Unity 安装路径按候选表探测（本机 = `D:\Unity Hub\Version\2022.3.62f3\Editor\Data`，可用 `UNITY_EDITOR_DATA` 覆盖）⇒ 换机器/换盘符不用改脚本
- **三证齐**（必须经 Unity 时）：`Library/ScriptAssemblies/*.dll` mtime 前进 + Console 0 error（**先清空再读**）+ `is_compiling:false`
- **空转判据**：有错 + dll mtime 不动 + 错误行号比源码少 1 行 ⇒ 编译器读的是旧快照；治法 = `ImportAsset + Refresh + RequestScriptCompilation()`，或 Python `os.utime()`
- ⚠⚠ 某程序集编译失败 ⇒ **依赖它的程序集既不报错也不编译** ⇒ 错误会逐层暴露，必须迭代；决定性判据 = "哪个 dll 没更新" + `GetAssemblies().sourceFiles` 是否含新文件
- ⚠ **「假缺失」= `.cs.meta` 被写成等长全 `\x00`**（2026-10-08 `TimerHost.cs.meta`/`FlowState.cs.meta`，各 243B = 正常长度 ⇒ `git status` 干净、`read_file` 判 binary、肉眼无异常）⇒ Unity 导入不出 guid ⇒ 该 .cs **不进编译** ⇒ 报 `CS0103: 名称"X"不存在`，而文件明明在磁盘上。
  判据：`manage_asset get_info` 返回 `guid:""` / `assetType:"Unknown"` / `instanceID:0`；扫描 `plans/fix_zeroed_metas_1008.py`（全项目 7215 个 meta 只此 2 个）。修法 = 按同目录正常 meta 逐字节重写（LF/无 BOM/243B）+ 换新 GUID（`static class` 无引用可安全换，MonoBehaviour 要先查 prefab/scene 是否引用旧 guid）。
  ⇒ 口径：**CS0103 但源文件确实存在 ⇒ 先查 meta/导入状态，别改代码**
- ⚠ `editor/state` 的 `external_changes_dirty` **不可信** ⇒ 只看 dll mtime vs 源文件 mtime（`dll_mtime.py`）
- `refresh` 之后不能立刻读 Console；`isCompiling=True` 时读 dll mtime 太早

## UI 预制体 / 窗口（Wnd）
- ⚠ **口径冲突待定**：本文件 §MCP「绝不主动触发编译/刷新」 vs `MEMORY.md` 热区 3「假空转解药 = `AssetDatabase.Refresh(ForceUpdate)` + 请求编译」。后者更晚（2026-10-03），2026-10-04 实测按它执行
- 面板布局 / 素材词表 / 批量搭 prefab 的套路 ⇒ 见 `topics/ui-wnd.md`

## 构建 / 启动画面
- 打 Windows 包的输出去向 `D:\Pro\Verion\BlueDivers`（StandaloneWindows64 + IL2CPP）；`BlueDivers_Data/RuntimeInitializeOnLoads.json` 是「`RuntimeInitializeOnLoadMethod` 回调有没有进包」的权威证据（`loadTypes`：0=AfterSceneLoad, 1=BeforeSceneLoad, 2=AfterAssembliesLoaded, 3=BeforeSplashScreen）
- ⛔ `00Tools/SkipUnityLogo.cs` **注定无效**（2026-10-07 诊断）：本机 `Application.HasProLicense()==False`（Personal）下 Unity 徽标由**原生播放器**强制绘制，`SplashScreen.Stop()` 只被当 no-op 忽略（2025 社区同款代码实测"该显示的还是正常显示"）；顺带查实**回调已进包**（`RuntimeInitializeOnLoads.json` 有 `00_Utils / FPSGame.Utils.SkipUnityLogo.BeforeSplashScreen`，`global-metadata.dat` 含 `SkipUnityLogo/BeforeSplashScreen/AsyncSkip`）⇒ 与写法、`#if !UNITY_EDITOR`、asmdef、IL2CPP 裁剪**都无关**，别再折腾这段代码
- Personal 下 Player 设置里 `show` / `showUnityLogo` 锁死 ON（编辑器实测 + 手册：`Overlay Opacity` 最低 0.5）；要真去掉只能升 Pro（直接取消勾选）或换 Unity 6（社区称个人版可关，未经官方文档证实）

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
| `find_missing_script_refs.py [--under <路径>]` / `dep_closure_missing.py <根路径> [--max N]` | **Missing Script 定位**：前者按 guid 索引（含 `PackageCache` 去假阳性）定向/全量扫 `m_Script`；后者做**依赖闭包 + 父链回溯**（`Resources.LoadAll` 的报错常来自依赖链深处的 prefab，不在目标文件夹里） |
| `missing_component_locate.py <资产> [guid]` | 列出某个 prefab/场景里 **Missing 组件**的宿主 GameObject 名（Console 只说 (Unknown)，不说挂在哪） |
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
| `BuildServerListPanel_生成服务器列表面板.cs.txt` | 一次性：在 `SelectMapWnd/Rooms` 下生成「服务器列表」子界面（拷回 `Assets/Editor/` 用菜单跑，幂等，跑完即删） |
