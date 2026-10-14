# Bluedivers 记忆 · 热区 + 路由表

> **这是"常驻热缓存 + 目录"，不是知识库本体。** 新知识一律写 `memory/topics/*.md`（除非属于下面三个热区）。
> 本文件 **≤3500 字符**、每篇 topic ≤8000；超了整段下沉到 `archive/topics-snapshot-<日期>/`、只留指针（2026-10-11 首次执行）。
> daily 是过程流水、**不注入**；当天收尾就把判据抽进 topic；旧 daily 用 `memory_archive_legacy.py --cut <日期> [--delete]`（先出索引再删，正文 `git checkout` 可回）。
> ⚠ 多窗口会并发写本文件 ⇒ 改前重读，冲突以内容更全者为准。体检 `python .codebuddy/plans/memory_gate.py`。

## 热区 1 · 协作红线
- 要写脚本就用 Python 3.12 放 `.codebuddy/plans/`（不用 `.ps1`）；**改名/移动文件由用户手动做**；不动无关 using；`const`/`static` 放类顶部
- 口径：先结论+证据(文件:行)再列选项；诊断序 = 谁调它 → prefab 数值 → 逻辑（能反射就别只读代码）
- ⚠ 外部编辑器缓冲区会**静默覆盖工作区文件**（`BattleManager.cs` 丢 11 项：编译能过、功能全坏）⇒ 大批改动收尾跑 `audit_markers.py`，须 `缺失项 = 0`
- ⚠ 编辑手段默认序：单点/小改动**优先 `replace_in_file`**；批量（多文件/多锚点）、编码敏感（BOM / 混合行尾 / 乱码）、需"锚点唯一 + 命中数断言"时才写字节级 Python 脚本（脚本留档、能重跑）
- ⚠ 注释口径：AI 写的多段式长篇 doc 注释会被用户精简掉 ⇒ 默认写 **1~3 行**、只讲"为什么 + 陷阱 + 别怎么写"；长文档/权衡放 `memory/topics/*.md`
- ⚠ **Play 模式规矩（2026-10-09 修订）**：用户进 Play **就是为了让我读运行中的真实数据** ⇒ 先取数据、**数据到手就可以导入/编译**；只有"这局数据还要继续取"时才先不动编辑器
- 调试要用数据说话 ⇒ 同步/子弹/AI 的日志走 `FPSGame.Utils.NetSyncLog`（`00Tools/NetSyncLog.cs`，**默认两端都开**，`-nonetsynclog` / `NETSYNC_LOG` 可静默）
- ⭐ 伤害来源两个口径：`DamageOrigin`（**统计**）/ `DamageCaused`（**显示·AI**）—— 新增伤害路径必须先选对，细则 → `topics/gameplay.md §伤害管线`

## 热区 2 · 环境
- Unity **2022.3.62f3** / URP 14.0.12 / C#9 / netstandard2.0；单机 PvE + 局域网联机；随机源 `BattleRandom` + `SeedUtil.Derive`
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；**MCP 实例 hash 不稳**（2026-10-14 同机两次会话分别报 `Bluedivers@3d9f2357` 与 `Bluedivers@4a3e6a7b`，端口恒 6400）⇒ 开局读 `mcpforunity://instances` 实时值、别背 hash；**只有 1 个实例时 server 自动选中**（多实例才必须 `set_active_instance`）
- ⚠ `%LOCALAPPDATA%\Unity\Editor\Editor.log` 会被**另一个 Unity 工程**覆盖（本机末尾常是 RTSClient 的日志）⇒ 排查 Bluedivers 编译不能读它
- `Lib/PEMaths.dll`：PEInt/PEVector3 = 米标量（`RawInt==RawFloat`），换算后不可再运算
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills `bluedivers-unity` / `data-editor` / `unity-mcp` / `kcpnet-online`

## 热区 3 · 编译验证（细则 → `topics/tooling.md`）
- ⭐ 首选**离线编译** `python -X utf8 .codebuddy/plans/offline_compile.py <asm>...`（~0.5s、不触发域重载）；⚠ 它编不到"刚新建、Unity 还没重生成 csproj"的文件
- 必须经 Unity 时 **三证齐** = dll mtime 前进 + Console 0 error（先清空）+ `is_compiling:false`；⚠ 别信 `external_changes_dirty`，也**别只信 Console 的 0 error**（有「假空转」与「假缺失 meta」两种假象）⇒ 关键改动**用反射核验成员是否存在**
- 改已存在文件只对**单个文件** `ImportAsset(path, ForceUpdate)`；新建 `.cs` 先 `refresh_unity`
- ⚠ **编辑器失焦时重编会被卡住**（2026-10-14 实测：`refresh_unity` / `AssetDatabase.Refresh(ForceUpdate)` / 逐文件 `ImportAsset` / `RequestScriptCompilation(CleanBuildCache)` / `Assets/Refresh` 菜单全试过，只让 `isCompiling` 翻真又落假，dll 与 Bee `artifacts` 都不动）⇒ 解药 = 让用户聚焦 Unity 或 Ctrl+R；判据仍是 **dll mtime + 反射**
- ⚠ MCP `read_console` 偶发**连 `types=["all"]` 都返回 0 条**（不可信，别只靠它判 "0 error"）
- 常用脚本：`plans/recent_files.py`（列最近改动文件 ⇒ 判"到底编没编"）、`plans/tail_lines.py`（PowerShell 无 tail）
- ⚠ **`offline_compile.py` 的依赖解析优先用 `Temp/offline_compile/<asm>.dll`**（本次自产的旧 dll）⇒
  只编"最上层那一个"时，会被**上一次留下的陈旧依赖**骗出假错（实测 2026-10-14：编 06_Gameplay 报
  `FpsHelper_Hit.cs: TargetCfg 未包含 All`，而契约层源码里明明有 —— 坏的是 `Temp/offline_compile/01_GameContract.dll`）。
  解药：报错涉及别的程序集的类型时，**把那条依赖链一起编**（如 `01_GameContract 05_UnitCore 06_Gameplay`）。

## 路由表（按触发词读对应文件）
| 触发词 | 去哪 |
|---|---|
| asmdef / 命名空间 / 类型归属 / using / 搬 .cs / 目录约定 | `topics/architecture.md` |
| 契约层 / 能力下沉 / BattleHub / 槽 / 接口判据 / 开窗 / 枚举与序列化 | `topics/contract.md` |
| 家具身份 / Identity / 家具 Id / BaseObject / Furniture_Base 已删 | `topics/furniture-identity.md` |
| 战斗 / AI / 伤害 / 波次 / 寻路 / 相机 / 武器 / 池化 VFX / 单位存活 | `topics/gameplay.md` |
| 家具 / 手持交互 / 任务系统 / 逻辑帧时序 / 存档设置 / 地形渲染 | `topics/gameplay-world.md` |
| 联机总览 / 传输 / KCPNet 库 / 心跳 / 幽灵会话 / NetSyncLog / 诊断 | `topics/net.md` |
| 房间 / 开房流程 / 阶段模型 / 名单队伍 / 会话生命周期 / 房主掉线 | `topics/net-flow.md` |
| 战斗同步 / 种子确定性 / 消息号表 / NetId / 未同步清单 | `topics/net-battle.md` |
| 编辑器 / Drawer / 数据编辑器 / MCP 与编译细则 / plans 脚本清单 | `topics/tooling.md` |
| UI 预制体 / 面板布局 / 列表行 / 素材选图 | `topics/ui-wnd.md` |
| 技术债 / 半成品系统 | `Assets/Scripts/00Tools/TechnicalDebt.cs`（`[P1]~[P3]`，改架构前先读） |
| "哪天做过什么" | `archive/INDEX-legacy-*.md`（正文 `git checkout HEAD -- .codebuddy/memory/<日期>.md` 取回） |
| 已下沉的 topic 全文（57k） | `archive/topics-snapshot-20261011/*.md` |

## 待办 / 待拍板
- ⏳ 记忆治理：把这套流程写成 `.codebuddy/rules/`（让每个窗口统一遵守）；建 weekly automation 自动"抽提 + 归档 + 报告"
- ⏳ Wnd 槽收尾执行：架构已落地（`ServiceLocator` 删、`Wnd` 走事件总线 + `WindowRegistry` 委托）；剩余清单见 `topics/contract.md`「Wnd 槽收尾 · 剩余清单」
