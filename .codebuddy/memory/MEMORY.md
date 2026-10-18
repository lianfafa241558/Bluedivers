# Bluedivers 记忆 · 热区 + 路由表

> **常驻热缓存 + 目录，不是知识库本体。** 新知识写 `memory/topics/*.md`（除非属于下面三个热区）；
> 本文件 **≤3500**、topic ≤8000，超了整段下沉 `archive/`、只留指针。daily 是流水、**不注入**。
> ⚠ 多窗口并发写 ⇒ 改前重读；体检 `memory_gate.py`、拆分 `memory_split_*.py`。

## 热区 1 · 协作红线
- 写脚本用 Python 3.12 放 `.codebuddy/plans/`；**改文件名/移动由用户手动做**；不动无关 using；`const`/`static` 放类顶部
- 口径：先结论+证据(文件:行)再列选项；诊断序 = 谁调它 → prefab 数值 → 逻辑（能反射就别只读代码）
- ⚠ 外部编辑器缓冲区会**静默覆盖工作区文件**（`BattleManager.cs` 丢 11 项、编译还全过）⇒ 大批改动收尾跑 `audit_markers.py` + 逐行回查
- ⚠ 编辑手段默认序：单点改动优先 `replace_in_file`；批量/编码敏感/需"锚点唯一 + 命中数断言"时才写字节级脚本（留档可重跑）
- ⚠ BOM 用 `add_bom.py`；**严禁** `open(p,'w').write(open(p).read())`（先截断后读 ⇒ 清空）；**空 .cs 也 0 error** ⇒ 改完跑 `check_sources.py`
- ⚠ 注释口径：AI 长篇 doc 注释会被用户精简 ⇒ 默认 **1~3 行**，只讲"为什么 + 陷阱"；长文档放 `topics/*.md`
- ⚠ **token 口径**（2026-10-18 用户问过两次）：每轮都重发全历史 ⇒ 会话累计 ≈ 上下文 × 轮数（3.8M ≈ 20+ 轮 × ~150k）。
  五条杠杆（按收益排）：①**换任务开新窗口**（判据已沉 topics，续接只要几千 token）②工具输出收敛：大文件用 offset/limit 读片段、命令/脚本只打摘要行、**不重复整篇读同一文件**、不重复拉 tool description（带整段 server config）③少往返：一次写对 + 并行工具 + 只验证一次（别"改→编→错→改→编"）④常驻注入瘦身：rules/skills 精简、MCP 只激活要用的 tool group ⑤多文件探索丢给 `code-explorer` subagent（结果不进主上下文）。输出侧（回复长度/注释长度）是小头，别本末倒置
- ⚠ **Play 模式规矩**：用户进 Play 就是为了让我读运行中真实数据 ⇒ 先取数据、**数据到手即可导入/编译**
- 调试用数据说话 ⇒ 同步/子弹/AI 日志走 `NetSyncLog`（`00Tools/NetSyncLog.cs`，默认两端都开）
- ⭐ 伤害来源两口径：`DamageOrigin`（统计）/ `DamageCaused`（显示·AI）—— 新伤害路径先选对，细则 → `topics/gameplay.md`
- ⭐ "别占的地方"= 两套圆**绝不能混存**：`TerrainUtils.AreaCircles`（地形覆盖物，会被 `RockCoverDestructor` 重建）/ `ReservedCircles`（任务点/走廊）；跨层避让一律走后者（06 不能引用 09）
- ⭐ 美化 SO Inspector = 专属 `[CustomEditor]`（继承 `Editor/Drawer/DataSOEditorBase.cs`），**禁用**全局 fallback `EditorOverride`；细则 → `topics/tooling.md`
- ⭐ **少写兜底**（2026-10-18 用户口径）：**已用 MCP 配好的引用直接用**，禁 `Resources.Load`/`Find` 二级取路径；缺前置条件就一条日志 + return，不写降级链。判据 → `rules/UnityCSharp编码规范.md §6.3`

## 热区 2 · 环境
- Unity **2022.3.62f3** / URP 14.0.12 / C#9 / netstandard2.0；单机 PvE + 局域网联机；随机源 `BattleRandom` + `SeedUtil.Derive`
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；MCP 实例 hash 不稳 ⇒ 开局读 `mcpforunity://instances` 实时值；单实例自动选中
- ⚠ `Editor.log` 会被**另一个 Unity 工程**覆盖（本机末尾常是 RTSClient）⇒ 排查本工程编译别读它
- `Lib/PEMaths.dll`：PEInt/PEVector3 = 米标量（`RawInt==RawFloat`），换算后不可再运算
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills `bluedivers-unity` / `data-editor` / `unity-mcp` / `kcpnet-online`

## 热区 3 · 编译验证（细则 → `topics/tooling.md`）
- ⭐ 首选**离线编译** `plans/offline_compile.py <asm>...`（~0.5s、不触发域重载）；编不到新文件 ⇒ `tmp_compile_newfile.py`
- 必须经 Unity 时 **三证齐** = dll mtime 前进 + Console 0 error（先清空）+ `is_compiling:false`；⚠ 有「假空转」「假缺失 meta」两种假象 ⇒ 关键改动**反射核验成员**
- ⚠ 编辑器失焦时重编卡住（只翻 `isCompiling`、dll 不动）⇒ 让用户聚焦 Unity；`read_console` 可能返回 0 条，别当判据

## 路由表（按触发词读对应文件）
| 触发词 | 去哪 |
|---|---|
| asmdef / 命名空间 / 类型归属 / 搬 .cs | `topics/architecture.md` |
| 契约层 / 能力下沉 / BattleHub / 接缝判据 / 枚举序列化 | `topics/contract.md` |
| UI 开窗 / Wnd 槽 / 新增窗口 SOP | `topics/wnd-slot.md` |
| 家具身份 / Identity / BaseObject | `topics/furniture-identity.md` |
| 战斗 / AI / 伤害管线 / 组件存活 | `topics/gameplay.md` |
| 玩家 / 相机 / 武器子弹 / 战备 / 寻路 | `topics/gameplay-player.md` |
| 家具交互 / 任务 / 逻辑帧 / 存档 / 地形 | `topics/gameplay-world.md` |
| 拦截火车 / 轨道 / 路基 / 炮塔 | `topics/mission-intercept-train.md` |
| 联机总览 / 传输 / KCPNet / 心跳 / NetSyncLog | `topics/net.md` |
| 房间 / 开房 / 阶段 / 名单 / 会话 / 房主掉线 | `topics/net-flow.md` |
| 搜不到 / 连不上 / 版本门槛 | `topics/net-connect.md` |
| 战斗同步总览 / 世界模型 / 种子 / 波次 | `topics/net-battle.md` |
| 消息号 / CmdId 4001~4054·5001 | `topics/net-battle-msg.md` |
| NetId / 号段 / 生成入口 4052 / 成员端 LOD | `topics/net-netid.md` |
| 生成器复刻 / 任务炮塔联机 | `topics/net-spawn.md` |
| 伤害同步 / DamageMsg / 起源 vs 直接来源 | `topics/net-damage.md` |
| 载具 4053/4054 / 位姿 / 外观 | `topics/net-vehicle*.md` |
| 编辑器 / Drawer / MCP / 编译验证 | `topics/tooling.md` |
| plans 脚本清单 | `topics/plans-scripts.md` |
| UI 预制体 / 面板布局 / 素材选图 | `topics/ui-wnd.md` |
| 技术债 / 待改架构 | `Assets/Scripts/00Tools/TechnicalDebt.cs` |
| 哪天做过什么 / 已下沉原文 | `archive/`（INDEX-legacy / topics-snapshot） |

## 待办 / 待拍板
- ✅ 记忆周检 `plans/memory_weekly.py`（IDE automation 本机不可用，手动/计划任务跑）
- ⏳ Wnd 槽收尾：剩余清单 → `topics/wnd-slot.md`
