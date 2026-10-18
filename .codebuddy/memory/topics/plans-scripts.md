# 主题 · `.codebuddy/plans/` 脚本清单

> 由 `tooling.md` 拆出（2026-10-18）。构建/MCP/编译验证口径见 `tooling.md`。

## `.codebuddy/plans/` 脚本清单（常用）
| 脚本 | 用途 |
|---|---|
| `audit_markers.py` | **标志物审计**（标志串 + 期望次数跨文件计数，输出"缺失项"）；**大批改动收尾必跑**，须 `缺失项=0` |
| `offline_compile.py <asm>...` | ⭐ **离线编译验证**（Unity 自带 Roslyn 按 csproj，不碰 Unity）；须按依赖顺序传参 |
| `dll_mtime.py` | 判"是否已编译"（dll mtime vs 源文件 mtime） |
| `tmp_compile_newfile.py <asm> <新.cs>...` | 新文件还没进 Unity 生成的 csproj 时，临时注入 csproj 编一次（用完自清理） |
| `recent_files.py` / `tail_lines.py` / `check_sources.py` | 列最近改动文件（判"到底编没编"）/ 取文件尾部（PowerShell 无 tail）/ 核 `.cs` 行数（防空文件"0 error 假通过"） |
| `memory_weekly.py [--archive [--delete]]` | ⭐ **记忆周检一条命令**：跑 gate + 列过期 daily + 给收尾提示；退出码 1 = 有超预算（可挂计划任务告警）。⚠ IDE automation 在本机不可用，故做成脚本 |
| `add_bom.py` / `ensure_cs_bom.py` | 给 `.cs` 补 BOM（⚠ 严禁 `open(p,'w').write(open(p).read())`，会先截断后读清空文件） |
| `log_ctx.py <log> <pattern> [before] [after] [--all\|--range]` | ⭐ **日志取上下文**（流式、能吃几百 MB）：`Editor.log` 里某条日志的**前后原文**；`--range <file> <起> <止>` 按行号直接打印。⚠ 客户端日志 = `%LOCALAPPDATA%\Unity\Editor\Editor.log`，**里面并入了打包端（`<i>WindowsPlayer "机器名"</i>` 前缀）那台的行** ⇒ 一份文件能对两端时间线；但每行日志带 10 行栈 ⇒ 别用大 before/after |
| `asmdef_refs.py` | asmdef 的 GUID references → 程序集名 |
| `bom_vs_head.py` | 对比工作区 vs git HEAD 的 BOM，`--fix` 只补"本次误删的" |
| `ns_move.py <moves.json> [--no-self]` | 通用「命名空间搬迁引擎」（改 ns + 同步 using + 修 `using static`） |
| `add_usings.py <pairs.json>` / `fix_missing_using.py` | 补 `using`（字节级保 BOM） |
| `find_dangling_guids.py` / `fix_guid_dangling.py` / `scan_missing_scripts.py` / `find_missing_script_refs.py [--under <路径>]` / `dep_closure_missing.py <根> [--max N]` / `missing_component_locate.py <资产> [guid]` | 悬空引用 / Missing Script / 缺组件的定位与修复（后两者做**依赖闭包 + 父链回溯** —— `Resources.LoadAll` 的报错常来自依赖链深处，不在目标文件夹里） |
| `sr_ns_audit.py` / `sr_ns_repair.py --apply` | 改 ns 对 `[SerializeReference]` 的审计与修复 |
| `tidy_contract_enums.py` | 枚举目录规范化 + 与 HEAD 逐枚举比对「成员名序列」（`语义差异项` 必须为 0） |
| `memory_gate.py` / `memory_archive_legacy.py --cut <日期> [--delete]` / `memory_orphan_scan.py` / `memory_split_20261018.py [--apply]` | 记忆分层体检 / 旧 daily 归档（先出索引再删正文）/ 删前"知识漏搬"体检 / topic 超预算按小节拆分（行号区间 + 覆盖断言 + 逐行回查，不带 `--apply` 只自检） |
| `svc_usage.py` / `battle_usage.py` / `contract_use.py` | 服务/契约消费点统计（⚠ `svc_usage.py` **会漏报**，权威以 ripgrep 为准） |
| `p2_ns_plan.py` / `env_probe.py` / `find_unity.py` / `prefab_tree.py` / `rooms_hierarchy.py` | 目录↔命名空间对齐计划 / 探测离线编译工具链 / 定位 Unity 安装 / prefab 层级树 |

（其余 `apply_*.py`、`note_*.py`、`p*_*.py` 都是**一次性**替换/记录脚本，用完即历史；`keyscreen_stage_sync.py` / `net_sync_b1*.py` / `net_sync_b2.py` 是 2026-10-11 的联机同步批次脚本，可重跑（dry-run + `--apply`、行尾自适应、锚点唯一断言）。）
