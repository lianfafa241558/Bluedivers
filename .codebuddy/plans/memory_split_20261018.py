# -*- coding: utf-8 -*-
"""2026-10-18 记忆超预算整理：把超 8000 字符的 topic 按小节拆成多篇。

为什么用脚本：拆分必须"原文件每一行恰好被搬走一次、顺序不变"。手工复制粘贴
漏一段就是静默丢知识（事后没人能发现）。这里用行号区间 + 覆盖断言 + 逐行回查
三重保证。⚠ 行号区间是 2026-10-18 那版原文的快照：源文件已被拆过之后重跑会
在覆盖断言处直接失败（不会二次破坏），要再拆新版请改 NB/GP/CT/NF/TL 的区间。

用法：python -X utf8 .codebuddy/plans/memory_split_20261018.py [--apply]
不带 --apply 只做覆盖自检 + 打印预算，不落盘。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MEM = os.path.join(ROOT, ".codebuddy", "memory")
TOP = os.path.join(MEM, "topics")
BUDGET = 8000
APPLY = "--apply" in sys.argv


def read_lines(path):
    with open(path, encoding="utf-8-sig") as fp:
        lines = fp.read().split("\n")
    while lines and lines[-1] == "":
        lines.pop()
    return lines


def write_md(path, lines):
    text = "\n".join(lines).rstrip("\n") + "\n"
    with open(path, "w", encoding="utf-8", newline="\n") as fp:
        fp.write(text)


def build(src, plans):
    """plans: (dest, header_lines, [(a,b), ...][, "append"])，a/b 为 1 基行号、闭区间。

    dest 可写 "../xxx.md" 表示落到 memory/ 下（append 模式用于把一段并进别的 topic）。
    """
    lines = read_lines(os.path.join(TOP, src))
    n = len(lines)
    cover = [0] * (n + 1)
    for plan in plans:
        dest, segs = plan[0], plan[2]
        for a, b in segs:
            assert 1 <= a <= b <= n, "%s: 区间越界 %s-%s (共 %d 行)" % (dest, a, b, n)
            for i in range(a, b + 1):
                assert cover[i] == 0, "%s: 第 %d 行被重复搬运" % (dest, i)
                cover[i] = 1
    miss = [i for i in range(1, n + 1) if not cover[i]]
    assert not miss, "%s: 这些行没被任何目标接收 -> %s" % (src, miss[:20])

    outs = {}
    for plan in plans:
        dest, header, segs = plan[0], plan[1], plan[2]
        mode = plan[3] if len(plan) > 3 else "new"
        out = list(header) + [""]
        for a, b in segs:
            out += lines[a - 1:b] + [""]
        if mode == "append":
            out = read_lines(os.path.join(TOP, dest)) + [""] + out
        outs[dest] = out

    # 逐行回查：原文每个非空行都必须在新文件集合里出现（防止 header 拼接时被吞）
    new_lines = set()
    for out in outs.values():
        new_lines.update(l.strip() for l in out if l.strip())
    lost = [l for l in lines if l.strip() and l.strip() not in new_lines]
    assert not lost, "%s: 丢失 %d 行，例如 %r" % (src, len(lost), lost[0][:80])

    print("== %s (%d 行)" % (src, n))
    for dest, out in outs.items():
        if APPLY:
            write_md(os.path.join(TOP, dest), out)
        cnt = len("\n".join(out)) + 1
        flag = "ok" if cnt <= BUDGET else "超预算 +%d" % (cnt - BUDGET)
        print("   %-28s %6d / %5d  %s" % (dest, cnt, BUDGET, flag))
    return outs


# ---------------------------------------------------------------- net-battle
NB = [
    ("net-battle.md", [
        "# 主题 · 联机 · 战斗同步总览（世界模型 / 种子 / 波次 / 世界状态判据）",
        "",
        "> 2026-10-18 由原 38.8k 字符的 `net-battle.md` 按小节拆分（每篇 ≤8000）。子篇：",
        "> `net-battle-msg.md`（消息号表 4001~4054 / 5001）· `net-netid.md`（号空间 / 生成入口 / 成员端 LOD）·",
        "> `net-spawn.md`（生成器复刻实例 4052）· `net-damage.md`（伤害通道 / DamageMsg / 起源拆分）·",
        "> `net-vehicle.md` + `net-vehicle-remote.md`（载具 4053/4054）· 家具字段 §7.2d → `gameplay-world.md`。",
        "> 原文（含每条实测与行号）已下沉：`archive/topics-snapshot-20261011/net.md`。",
    ], [(1, 10), (57, 92), (93, 98)]),

    ("net-battle-msg.md", [
        "# 主题 · 联机 · 消息号表（`07NetGame/CmdId.cs`）",
        "",
        "> 由 `net-battle.md` 拆出（2026-10-18）。新号只许追加、旧号常量保留（停发不等于删号）；",
        "> 号段占用：4001~4054 房间/战斗、5001/5002 位姿。各号落地细节去对应子篇。",
    ], [(11, 56)]),

    ("net-netid.md", [
        "# 主题 · 联机 · 号空间（NetId）/ 生成入口 / 成员端 LOD",
        "",
        "> 由 `net-battle.md` 拆出（2026-10-18），含原 §7.1 / §7.2a / §7.2b / §7.2c。",
        "> 一句话判据：**\"两端能不能算出同一个出生坐标？\"** 能 ⇒ 派生号；不能 ⇒ 房主分配（4052）。",
    ], [(99, 177)]),

    ("net-spawn.md", [
        "# 主题 · 联机 · 生成器复刻实例（房主唯一创建 + 4052）",
        "",
        "> 由 `net-battle.md` 拆出（2026-10-18），含原 §7.2f 上半（生成侧复核）+ §7.2x（生成器第 4 例 = 任务车厢炮塔）。",
        "> 号空间与 `CreatUnit` 签名见 `net-netid.md`；拦截火车全貌见 `mission-intercept-train.md`。",
        "> ⚠ 原 §7.2f 下半（两族合并 / 伤害落地 / 验证）已拆到 `net-damage.md`。",
    ], [(206, 219), (412, 426)]),

    ("net-damage.md", [
        "# 主题 · 联机 · 伤害通道（DamageMsg / 伤害成分 / 起源拆分）",
        "",
        "> 由 `net-battle.md` 拆出（2026-10-18），含原 §7.2e + §7.2f 下半（两族合并 / 伤害落地 / 验证）。",
        "> 原 §7.2f 上半（生成侧复核）见 `net-spawn.md`；本地护甲/AP/抗性结算见 `gameplay.md §伤害管线`。",
        "",
        "#### 7.2f 复核结论（下半 · 序号承接 `net-spawn.md`）",
    ], [(189, 205), (220, 247), (248, 271)]),

    ("gameplay-world.md", [
        "",
        "> 以下由 `net-battle.md` §7.2d 并入（2026-10-18）：一个 float 被 5 处按 5 种语义复用；",
        "> 结论是**有意不同步**（结果早已由 4036/4046/4049 过网）。联机口径见 `net-battle.md` / `net-damage.md`。",
    ], [(178, 188)], "append"),

    ("net-vehicle.md", [
        "# 主题 · 联机 · 载具（号一致 / 上下机 4053 / 炮位 / P2 位姿）",
        "",
        "> 由 `net-battle.md` §7.3 拆出（2026-10-18），本篇 = 号一致 + 上下机 + 炮位 + 位姿推导。",
        "> 远端表现 / 外观 / 血量权威 / 开火 4054 / 断线 → `net-vehicle-remote.md`。",
        "> 计划原文：`plans/联机_载具系统同步_执行计划.md`。",
    ], [(272, 345)]),

    ("net-vehicle-remote.md", [
        "# 主题 · 联机 · 载具远端侧（外观 / 血量权威 / 开火 4054 / 断线）",
        "",
        "> 由 `net-battle.md` §7.3 拆出（2026-10-18），承接 `net-vehicle.md`（号一致 / 上下机 / 位姿）。",
        "> 含：骑手随车销毁、运输机投放、房主强关、皮肤被表现系统盖掉、P3 血量权威、P4/P5 收尾。",
    ], [(346, 411)]),
]

# ------------------------------------------------------------------ gameplay
GP = [
    ("gameplay.md", [
        "# 主题 · 战斗 / AI / 伤害管线 / 组件存活",
        "",
        "> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。",
        "> 2026-10-18 拆出：玩家/相机/武器子弹/战备/寻路 → `gameplay-player.md`；家具任务时序地形 → `gameplay-world.md`。",
        "> ⚠ 原文（含每条实测细节与行号）已下沉：`archive/topics-snapshot-20261011/gameplay.md`。",
    ], [(1, 53), (71, 77)]),

    ("gameplay-player.md", [
        "# 主题 · 玩家侧（相机 / 武器子弹配置 / 战备 / 寻路）",
        "",
        "> 由 `gameplay.md` 拆出（2026-10-18）。战斗/AI/伤害管线/组件存活仍在 `gameplay.md`。",
        "> 联机侧的战备/位姿/载具口径见 `net-*.md`。",
    ], [(54, 70), (78, 87)]),
]

# ------------------------------------------------------------------ contract
CT = [
    ("contract.md", [
        "# 主题 · 契约层 / 能力下沉 / 服务接缝",
        "",
        "> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。",
        "> 2026-10-18 拆出：UI/窗口与 Wnd 槽收尾清单 → `wnd-slot.md`；家具身份 → `furniture-identity.md`。",
    ], [(1, 36), (72, 91)]),

    ("wnd-slot.md", [
        "# 主题 · UI / 窗口（Wnd 槽架构 + 收尾清单）",
        "",
        "> 由 `contract.md` 拆出（2026-10-18）。契约层其余（能力下沉 / 接缝判据 / IPlayerUnit / 枚举序列化）在 `contract.md`。",
        "> 预制体与面板布局另见 `ui-wnd.md`。",
    ], [(37, 71)]),
]

# ------------------------------------------------------------------ net-flow
NF = [
    ("net-flow.md", [
        "# 主题 · 联机 · 房间 / 开房流程 / 名单队伍 / 会话生命周期",
        "",
        "> 由 `net.md` 拆出（2026-10-11）。预算 ≤ 8000 字符。同伴：`net.md`（传输/库/诊断）、`net-battle.md`（战斗同步）。",
        "> 跨机「搜不到 / 连不上」排查 + 版本门槛已拆到 `net-connect.md`（2026-10-18）。",
        "> ⚠ 原文（含每条实测）已下沉：`archive/topics-snapshot-20261011/net.md`。",
    ], [(1, 66)]),


    ("net-connect.md", [
        "# 主题 · 联机 · 跨机「搜不到 / 连不上」排查口径 + 版本门槛",
        "",
        "> 由 `net-flow.md` §8 拆出（2026-10-18）。传输层与库侧诊断见 `net.md`。",
    ], [(67, 81)]),
]

# -------------------------------------------------------------------- tooling
TL = [
    ("tooling.md", [
        "# 主题 · 编辑器扩展 / MCP 与编译验证",
        "",
        "> 由 `MEMORY.md` 路由表按触发词加载。预算 ≤ 8000 字符。",
        "> 2026-10-18 拆出：`.codebuddy/plans/` 脚本清单 → `plans-scripts.md`。",
        "> ⚠ 原文（行尾/BOM 普查逐项数据、Play 域重载事故细节、脚本全文说明）已下沉：`archive/topics-snapshot-20261011/tooling.md`。",
    ], [(1, 49)]),

    ("plans-scripts.md", [
        "# 主题 · `.codebuddy/plans/` 脚本清单",
        "",
        "> 由 `tooling.md` 拆出（2026-10-18）。构建/MCP/编译验证口径见 `tooling.md`。",
    ], [(50, 68)]),
]


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    if "--apply" not in sys.argv:
        print("（自检模式，不落盘；加 --apply 才写文件）")
    build_all = [("net-battle.md", NB), ("gameplay.md", GP), ("contract.md", CT),
                 ("net-flow.md", NF), ("tooling.md", TL)]
    for src, plans in build_all:
        print("")
        build(src, plans)


if __name__ == "__main__":
    main()
