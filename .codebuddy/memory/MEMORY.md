# Bluedivers 记忆 · 热区 + 路由表

> **常驻热缓存 + 目录。** 新知识写 `topics/*.md`；本文件 **≤3500**、topic ≤8000，超了下沉 `archive/` 留指针；daily 是流水、**不注入**。多窗口并发写 ⇒ 改前重读；体检 `memory_gate.py`。

## 热区 1 · 协作红线
- 写脚本用 Python 3.12 放 `.codebuddy/plans/`；**改文件名/移动由用户手动做**；不动无关 using；`const`/`static` 放类顶部
- 口径：先结论+证据(文件:行)再列选项；诊断序 = 谁调它 → prefab 数值 → 逻辑（能反射就别只读代码）
- ⚠ 外部编辑器会**静默覆盖**工作区文件（编译还全过）⇒ 大批改动收尾跑 `audit_markers.py` + 逐行回查；单点优先 `replace_in_file`，批量/编码敏感才写字节级脚本
- ⚠ BOM 用 `add_bom.py`；**严禁** `open(p,'w').write(open(p).read())`（先截断后读 ⇒ 清空）；**空 .cs 也 0 error** ⇒ 改完跑 `check_sources.py`
- ⚠ 注释口径：默认 **1~3 行**（为什么 + 陷阱）；长文档放 `topics/*.md`
- ⚠ **token 口径**：每轮重发全历史 ⇒ 五杠杆（换窗口 / 输出收敛 / 少往返 / 注入瘦身 / 探索丢 subagent）→ `topics/tooling.md`
- ⚠ **Play 模式规矩**：用户进 Play 就是为了让我读运行中真实数据 ⇒ 先取数据、**数据到手即可导入/编译**
- 调试用数据说话 ⇒ 同步/子弹/AI 日志走 `NetSyncLog`（⚠ 编辑器默认**关**，实测前先开）
- ⭐ 伤害两口径：`DamageOrigin`（统计）/ `DamageCaused`（显示·AI）—— 新路径先选对 → `topics/gameplay.md`
- ⭐ 两套圆**绝不能混存**：`AreaCircles`（地形覆盖物）/ `ReservedCircles`（任务点/走廊）；跨层避让走后者（06 不能引用 09）
- ⭐ 美化 SO Inspector = 专属 `[CustomEditor]`（继承 `Editor/Drawer/DataSOEditorBase.cs`），**禁用**全局 `EditorOverride` → `topics/tooling.md`
- ⭐ **少写兜底**：MCP 配好的引用直接用，禁 `Resources.Load`/`Find` 二级取路径；缺前置条件就一条日志 + return → `rules/UnityCSharp编码规范.md §5`

## 热区 2 · 环境
- Unity **2022.3.62f3** / URP 14.0.12 / C#9 / netstandard2.0；单机 PvE + 局域网联机；随机源 `BattleRandom` + `SeedUtil.Derive`
- 副本 `D:\Pro\Bluedivers`、`D:\Project\RTSClient`；MCP 实例 hash 不稳 ⇒ 开局读 `mcpforunity://instances`
- ⚠ `Editor.log` 常被别的工程覆盖（RTSClient）⇒ 别读它查本工程编译
- `Lib/PEMaths.dll`：PEInt/PEVector3 = 米标量（`RawInt==RawFloat`），换算后不可再运算
- 规范 `.codebuddy/rules/UnityCSharp编码规范.md`；skills 见 `.codebuddy/skills/`

## 热区 3 · 编译验证（细则 → `topics/tooling.md`）
- ⭐ 首选**离线编译** `plans/offline_compile.py <asm>...`（~0.5s、不触发域重载）；编不到新文件 ⇒ `tmp_compile_newfile.py`
- 必须经 Unity 时 **三证齐** = dll mtime + Console 0 error + `is_compiling:false`；⚠「假空转」「假缺失 meta」⇒ 关键改动**反射核验**
- ⚠ 编辑器失焦时重编卡住（只翻 `isCompiling`、dll 不动）⇒ 让用户聚焦 Unity；`read_console` 可能返回 0 条，别当判据

## 路由表（按触发词读对应文件）
| 触发词 | 去哪 |
|---|---|
| asmdef / 命名空间 / 类型归属 / 搬 .cs | `topics/architecture.md` |
| 契约层 / 能力下沉 / BattleHub / 接缝判据 / 枚举序列化 | `topics/contract.md` |
| UI 开窗 / Wnd 槽 / 新增窗口 SOP | `topics/wnd-slot.md` |
| 家具身份 / Identity / BaseObject | `topics/furniture-identity.md` |
| 战斗 / AI / 伤害管线 / 组件存活 | `topics/gameplay.md` |
| 武器升级 / 模组 / WUD_ 通用池复用（禁复制） | `skills/weapon-upgrade/SKILL.md` §4.1 |
| 玩家 / 相机 / 武器子弹 / 战备 / 寻路 | `topics/gameplay-player.md` |
| 家具交互 / 任务 / 逻辑帧 / 存档 / 地形 | `gameplay-world.md` · `terrain-render.md` |
| 拦截火车 / 轨道 / 路基 / 炮塔 / 卫队 | `mission-intercept-*.md` |
| 联机总览 / 传输 / KCPNet / 心跳 / NetSyncLog | `topics/net.md` |
| 房间 / 开房 / 阶段 / 名单 / 会话 / 房主掉线 | `topics/net-flow.md` |
| 搜不到 / 连不上 / 版本门槛 | `topics/net-connect.md` |
| 战斗同步 / 世界模型 / 种子 / 波次 / 消息号 CmdId | `topics/net-battle.md` · `net-battle-msg.md` |
| NetId / 号段 4052 / 生成器复刻 / 任务炮塔 | `topics/net-netid.md` · `net-spawn.md` |
| 伤害同步 / DamageMsg / 起源 vs 直接来源 | `topics/net-damage.md` |
| 两端各算一次 / 伤害翻倍 / 支援单位同步 | `topics/net-dup-damage.md` |
| 载具 4053/4054 / 位姿 / 外观 | `topics/net-vehicle*.md` |
| 编辑器 / Drawer / MCP / 编译验证 | `topics/tooling.md` |
| plans 脚本清单 | `topics/plans-scripts.md` |
| UI 预制体 / 面板布局 / 素材选图 | `topics/ui-wnd.md` |
| 粒子特效 / VFX 预制体规范 / 特效材质坑 | `topics/vfx.md` |
| 人设 / 语气 / 诺亚设定 | `rules/人设.md`（行为约束）+ `topics/persona.md`（背景库） |
| 技术债 / 待改架构 | `Assets/Scripts/00Tools/TechnicalDebt.cs` |
| 哪天做过什么 / 已下沉原文 | `archive/` |

## 待办 / 待拍板
- ✅ **N 倍伤害已修（2026-10-27，待数值实测）** → `topics/net-dup-damage.md`
- ⏳ Wnd 槽收尾：剩余清单 → `topics/wnd-slot.md`
- ⏳ `mission-intercept-train` 超预算待拆
