---
name: memory-hygiene
description: Bluedivers 项目 `.codebuddy/memory/` 的治理与整理操作指南。当需要体检记忆分层（memory_gate.py 报超预算）、给超 8000 字符的 topic 拆分、把 MEMORY.md 热区里过长的段落下沉到 topic、归档 30 天以上的 daily、判断"该写进 MEMORY.md 还是 topics/还是 daily"、或发现两个文件在复述同一份知识需要合并时触发。也用于回答"这条知识该记在哪 / 记忆是不是过期了"。
---

# 记忆治理（Bluedivers）

> 红线（每轮都要守的）在 `.codebuddy/rules/记忆治理.md`；**本文只管"整理时怎么做"**。

## 分层与预算

| 层 | 文件 | 预算 | 何时读 |
|---|---|---|---|
| L0 | `memory/MEMORY.md` | **≤3500** | 每轮注入（热区 1/2/3 + 路由表 + 待办） |
| L1 | `memory/topics/*.md` | **≤8000 / 篇** | 按 `MEMORY.md` 路由表的触发词按需读 |
| L2 | `memory/YYYY-MM-DD.md` | 不设（**不注入**） | 只在"哪天做过什么"时按 `archive/INDEX-legacy-*` 检索 |
| L3 | `memory/archive/` | — | 已下沉的原文（`topics-snapshot-*`、daily 索引） |

判定口径：**会天天变的是实现细节 ⇒ topic；不会变的是判据/口径 ⇒ 可以留 L0 或 skill**。

## 标准流程

### 1) 体检

```bash
python -X utf8 .codebuddy/plans/memory_weekly.py            # ⭐ 周检一条命令：gate + 过期 daily + 收尾提示（退出码 1 = 有超预算）
python -X utf8 .codebuddy/plans/memory_weekly.py --archive [--delete]   # 顺带归档 daily（先确认已 git 跟踪）
python -X utf8 .codebuddy/plans/memory_gate.py              # 只看预算
python -X utf8 .codebuddy/plans/memory_gate.py --candidates # 额外列 L0 里像"过程细节"的行
```

> IDE 的 automation 在本机不可用（`automation_update` 返回空、不落盘）⇒ 周期任务用 `memory_weekly.py`：
> 手动跑，或挂 Windows 计划任务（`schtasks /create /tn BluediversMemoryWeekly /tr "python -X utf8 <路径>" /sc WEEKLY /d SUN /st 10:00`）。

先看数字再动手：**只有超预算才需要整理**，别为了美观去改没超的文件（每次改动都是丢知识的机会）。

### 2) topic 超预算 ⇒ 按小节拆

用 `plans/memory_split_20261018.py` 的**模式**（行号区间 + 覆盖断言 + 逐行回查），不要手工复制粘贴：

1. 先列出 `##` 标题与字符偏移，按语义分组（**别按长度硬切**）；
2. 写/改脚本里的 `[(dest, header_lines, [(起,止)…])]`，`--apply` 前先跑自检；
3. 脚本断言三件事：区间不越界、每行只被搬一次、原文每个非空行都在新文件里出现；
4. 拆完补两件事：**父文件头部列子篇指针**、**`MEMORY.md` 路由表加触发词行**。

⚠ 行号区间是"当次快照"，源文件被拆过之后重跑会在断言处**安全失败**（不是幂等脚本）。

### 3) L0 超预算 ⇒ 下沉，不减知识

- 先搬"能自成一节"的：整段 → 对应 topic，L0 留 **1 行指针**（写清"细则 → `topics/xxx.md`"）。
- 再压措辞：长括号、重复限定词、"（2026-xx 更新）"这类时间戳能删就删；
- **别**把不同主题压成一句（读者要靠触发词定位）。
- 路由表是 L0 最占地方的一块：触发词只留"会被真实说出口的词"，别把文件里的每个名词都列上。

### 4) daily 归档

```bash
python -X utf8 .codebuddy/plans/memory_archive_legacy.py --cut 2026-09-18          # 只出索引
python -X utf8 .codebuddy/plans/memory_archive_legacy.py --cut 2026-09-18 --delete  # 出索引并删正文
```

⚠ 删之前确认**这些 daily 已被 git 跟踪**（`git ls-files .codebuddy/memory | …`）：正文靠 `git checkout HEAD -- …` 找回。
⚠ 归档前先把"判据"抽进 topic —— 索引只留标题，抽不出来就等于丢。

### 5) 复检

```bash
python -X utf8 .codebuddy/plans/memory_gate.py     # 全部 ok 才算完
```

收尾顺手核：新 topic 全是**无 BOM 的 UTF-8 + LF**、`MEMORY.md` 路由表能指到每个新文件（`grep` 一遍文件名）。

## 保真校验（拆分/搬家后必做）

```bash
# 原文件（git HEAD 版）每个非空行，必须能在新文件集合里找到
git show HEAD:.codebuddy/memory/topics/<旧文件>.md
```

把"丢失行"逐条打出来，**只允许**是：被改写的旧标题/说明、被压缩成指针的长句、被更新的断链文件名。
出现别的行 ⇒ 立刻从 `git show` 补回去。

## 避坑清单

- ⚠ **`open(p,'w').write(open(p).read())` = 先截断后读 ⇒ 文件清空**；写记忆一律 `encoding='utf-8'`、`newline='\n'`、先读全量再写。
- ⚠ `.md` **绝不加 BOM**（BOM 口径只管 `.cs`）。
- ⚠ 多窗口并发写 `MEMORY.md`：**改前重读**，用 `replace_in_file` 逐段改，别 `write_to_file` 整篇覆盖。
- ⚠ 拆分脚本删掉旧标题后常留下 `## 标题` 前缺空行 / 重复标题块：拆完扫一遍（"第二个 `# 主题` 标题"= 残留）。
- ⚠ 拆完必须改**跨文件引用**：其它 topic 里的 `§7.2x`、`net-battle.md` 之类会变成断链（`grep` 旧文件名一遍）。
- ⚠ 别把"实现细节"从 topic 搬进 skill 来腾地方 —— 那只是把问题转移（skill 整篇注入 + 更少人改）。
