# Bluedivers 记忆 · 热区 + 路由表

> **这是"常驻热缓存 + 目录"，不是知识库本体。**
> **新知识一律写 `memory/topics/*.md`**（除非属于下面三个热区）；本文件 **≤3500 字符**、每篇 topic ≤8000，超了整段下沉、只留指针。
> daily 是过程流水、**不注入**；当天收尾就把判据抽进 topic；过期用 `memory_archive_legacy.py --cut <日期> --delete`（先出索引再删，正文 `git checkout` 可回）。
> ⚠ 多窗口会并发写本文件 ⇒ 改前重读，冲突以内容更全者为准。体检 `python .codebuddy/plans/memory_gate.py`。

## 热区 1 · 协作红线
- 真的要写脚本也用用 Python 3.12 放 `.codebuddy/plans/`而不是用 `.ps1`；**改名/移动文件由用户手动做**；不动无关 using；`const`/`static` 放类顶部
- 口径：先结论+证据(文件:行)再列选项；诊断序 = 谁调它 → prefab 数值 → 逻辑（能反射就别只读代码）
- ⚠ 编辑：`replace_all` 漏缩进不同的行且会改注释 ⇒ 改完 grep 原符号归零；**linter 滞后** ⇒ 关键删除必须 grep 核对
- ⚠ **编辑手段默认序**（用户 2026-10-02 口径）：单点/小改动**优先直接 `replace_in_file`**；只有批量（多文件/多锚点）、编码敏感（BOM / 混合行尾 / U+FFFD 乱码）、或需要「锚点唯一 + 命中 1 次才落盘」断言时才写字节级 Python 脚本 —— 别为一行改动绕脚本（脚本本身可留档复核，但要说明理由）
- ⚠⚠⚠ **外部编辑器缓冲区会静默覆盖工作区文件**（`BattleManager.cs` 丢 11 项，编译能过、功能全坏）⇒ 大批改动收尾跑 `audit_markers.py`，须 `缺失项 = 0`
- ⚠ **注释口径**：AI 写的多段式长篇 doc 注释会被用户精简掉（2026-10-07 `ActorsManager` 实测）⇒ 默认写 **1~3 行**、只讲"为什么 + 陷阱 + 别怎么写"；长文档/权衡放 `memory/topics/*.md`
- ⚠ **Play 模式规矩（用户 2026-10-09 修订）**：用户进 Play **就是为了让我读运行中的真实数据**（不再是"绝对禁止编译"）⇒ 先取数据、**数据到手就可以导入/编译**；只有"这一局的数据我还要继续取"时才先不动编辑器。另：调试要用数据说话 ⇒ 同步/子弹/AI 的诊断走 `FPSGame.Utils.NetSyncLog`（`00Tools/NetSyncLog.cs`，运行时可开关，打包端用 `-netsynclog` / `NETSYNC_LOG=1`）

## 热区 2 · 环境
- Unity **2022.3.62f3** / URP 14.0.12 / C#9 / netstandard2.0；单机 PvE；随机源 `BattleRandom`
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；**MCP 实例 hash 随路径变** ⇒ 开局读 `mcpforunity://instances` 实时值
- `Lib/PEMaths.dll`：PEInt/PEVector3 = 米标量（`RawInt==RawFloat`），换算后不可再运算
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills `bluedivers-unity` / `data-editor` / `unity-mcp` / `kcpnet-online`

## 热区 3 · MCP 与编译验证
- 开工 `set_active_instance`；写前确认 `projectRoot`；改已存在文件只对**单个文件** `ImportAsset(path, ForceUpdate)`
- **三证齐** = dll mtime 前进 + Console 0 error（先清空）+ `is_compiling:false`；❌ 空转 = 有错 + dll mtime 不动 + 错误行号比源码少 1 行；⚠ 程序集失败会连坐下游不编译 ⇒ 必须迭代；⚠ `external_changes_dirty` 不可信，只看 dll mtime
- ⚠⚠⚠ **「假空转」**（2026-10-02 实测）：脚本内容已变，但 Unity 增量判定为空（Editor.log 只出 `script compilation time: 0.0002s`）⇒ dll 不动、Console **也 0 error**、`refresh_unity` 照样返 `refresh_triggered:true`、`ImportAsset(ForceUpdate)` 也白搭 —— 三个"通过"全是假象。**判据改为反射**：`execute_code` 遍历 `AppDomain.CurrentDomain.GetAssemblies()` 取目标程序集，`GetMethod("<新成员>")` 为 null = 没编上。**解药**：`CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache)`（清缓存真重建，~30s，dll 必然进位）；⚠ 但若 `external_changes_dirty:true`（Unity 未 focus ⇒ 资产库不刷新），它单独调用**仍空转、dll 不产出** ⇒ 先 `execute_code`: `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)` 再请求编译（2026-10-03 实测）；另：`dll 里搜成员名`（python 读 `Library/ScriptAssemblies/<asm>.dll` 字节）比 mtime 更硬
- ⭐ **可选验证 = 离线编译** `plans/offline_compile.py <asm>...`（借 Unity 自带 Roslyn 按 `.csproj` 编译，~0.5s，**不触发域重载**，绕开上面那条红线）；改 ns/using/搬文件后必跑，按报错补 using 再跑
- 细则与 `execute_code` 坑 → `topics/tooling.md`

## 路由表（按触发词读对应文件）
| 触发词 | 去哪 |
|---|---|
| asmdef / 命名空间 / 类型归属 / using / 搬 .cs / 目录约定 | `topics/architecture.md` |
| 契约层 / 能力下沉 / BattleHub / 槽 / 接口判据 / 开窗 / 枚举与序列化 | `topics/contract.md` |
| 家具身份 / Identity / 家具 Id / BaseObject / Furniture_Base 已删 | `topics/furniture-identity.md` |
| 战斗 / AI / 伤害 / 波次 / 寻路 / 相机 / 武器 / 池化 VFX / 家具 / 任务 / 事件 / 地形 | `topics/gameplay.md` |
| 编辑器 / Drawer / 数据编辑器 / MCP 与编译细则 / plans 脚本清单 | `topics/tooling.md` |
| UI 预制体 / 面板布局 / 列表行 / 素材选图 | `topics/ui-wnd.md` |
| KCPNet / 联机 / MessagePack | `topics/net.md` |
| 技术债 / 半成品系统 | `Assets/Scripts/00Tools/TechnicalDebt.cs`（`[P1]~[P3]`，改架构前先读） |
| "哪天做过什么" | `archive/INDEX-legacy-*.md`（正文 `git checkout HEAD -- .codebuddy/memory/<日期>.md` 取回） |

## 待办 / 待拍板
- ⏳ 记忆治理：写成 `.codebuddy/rules/`（让每个窗口统一遵守）；建每周 automation 自动"抽提 + 归档 + 报告"
- ⏳ Wnd 槽收尾执行：架构已落地（`ServiceLocator` 删、`Wnd` 走事件总线 + `WindowRegistry` 委托）；剩余清单见 `topics/contract.md`「Wnd 槽收尾 · 剩余清单」（编译红/TMPro、悬空 asmdef、死代码链、注释清理）
