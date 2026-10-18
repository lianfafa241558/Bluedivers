# -*- coding: utf-8 -*-
"""2026-10-18 skill 瘦身 第三刀（bluedivers-unity）：压「伤害两个口径」与「asmdef」两节。

为什么要动 asmdef 那节：里面的"16 个 asmdef / 464 cs / 仍在 Assembly-CSharp 295 cs"是
**2026-09-30 的快照**，而今天 `plans/list_asmdefs.py` 实测是 **27 个**（含 06_Gameplay/07_NetGame/
09_Managers/10_UI）⇒ 表已经全是错的，留着比删掉更危险（模型会照着它判断类型归属）。
新写法则：**判据留 skill，名单/归属/执行清单一律指 memory topic + references**。

用法：python -X utf8 .codebuddy/plans/skill_slim3_20261018.py [--apply]
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SKILL = os.path.join(ROOT, ".codebuddy", "skills", "bluedivers-unity", "SKILL.md")
APPLY = "--apply" in sys.argv

NEW_DAMAGE = """### 伤害来源两个口径（**新增伤害路径必须先选对**）

`DamagePacket`（`00GameContract/Data/DamagePacket.cs`）带**两个**来源字段：

| 字段 | 含义 | 谁读 |
|---|---|---|
| `DamageOrigin` | **起源**：谁该记这笔账（武器持有者 / 召唤物·战备的呼叫者） | 数值与统计 —— 减伤·友伤·全队强化、护甲破坏、命中次数、击杀归属、`Actor.OnDie → UnitKill` |
| `DamageCaused` | **直接来源**：屏幕上该显示谁（玩家的枪 / **炮台本身** / 地雷 / 补给舱） | 显示与 AI —— 受击方向与闪变、倒地播报、`DetectionModule` 阵营过滤与受击改目标 |

- 生产者**一处收口**在 `ProjectileBase.BuildHitData`；`DamageCaused == null` ⇒ 内部回落成 `DamageOrigin`（新增构造点可只填 origin）；
  要"显示成物件自己"（地雷/空投舱）就显式 `caused = gameObject`。
- ⚠ 事件签名：`Health.OnDie(caused, origin)`、`Health.OnDamaged(value, caused, origin, collider, noSource)` —— **5 参超出 `UnityAction` 上限**（它最多 4 个泛型）⇒ 必须用 `System.Action<...>`。
- ⚠ `caused` 可能**没有 `Actor`** ⇒ 每处 `caused.GetComponent<Actor>()` 都要判空（口径：取不到就回落 origin 做阵营判定）。
- 逐点清单（哪个调用点读哪个）+ 联机 `DamageMsg.CausedNetId` → `memory/topics/gameplay.md §伤害管线`、`net-damage.md`。"""

NEW_ASMDEF = """### 程序集定义（asmdef）

> **判据留本节，名单不留**：当前真实 asmdef 名单（**27 个**，2026-10-18 实测）用 `python .codebuddy/plans/list_asmdefs.py` 现取；
> 类型归属/命名空间/目录约定 → `memory/topics/architecture.md`；目标分层表 + 6 个解耦手法 + Phase 0~7 执行清单 → `references/assembly-plan.md`。
> ⚠ 本文件历史版本的"16 个 asmdef / 464 cs / 仍在 `Assembly-CSharp` 295 cs"是 2026-09-30 快照，**已作废**。

- ⚠⚠ **铁律（双向）**：`autoReferenced:true` 只保证"`Assembly-CSharp` 看得见 asmdef"；**反向是硬墙 —— asmdef 永远看不到 `Assembly-CSharp` 的类型**。
  ⇒ 新程序集要用到的每一样东西，必须**已经**在某个 asmdef / 插件 dll 里。**扫依赖的两个盲区**：扩展方法（调用点不出现类名）、`internal` 成员。
- ⚠ **不要只加一个 asmdef 就完事**：`01Manager`/`09_Managers ↔ 02Game/06_Gameplay`（含 `02Data`/`04UI`/`Effect`）是一个几百文件的强连通分量
  ⇒ 必须先在 `references/assembly-plan.md` 里按顺序消回边；玩法层**不能逐个切**（07↔06 双向互引）⇒ 合并成一个 `06_Gameplay`。
- **新 asmdef 一律 `autoReferenced:false` + 显式 `references`**（历史遗留的 `true` 不要再扩散）；新增脚本前先确认目标程序集与允许的引用方向（编号大的可引用编号小的，反之禁止）。
- ⚠ 改 asmdef 会**触发全量重编译**（用户前台即时重编）⇒ 动手前先告知用户。"""


def read(p):
    with open(p, encoding="utf-8-sig") as fp:
        lines = fp.read().split("\n")
    while lines and lines[-1] == "":
        lines.pop()
    return lines


def write(p, lines):
    if APPLY:
        with open(p, "w", encoding="utf-8", newline="\n") as fp:
            fp.write("\n".join(lines).rstrip("\n") + "\n")


def replace_section(lines, prefix, new_text):
    level = len(prefix) - len(prefix.lstrip("#"))
    hit = [i for i, l in enumerate(lines) if l.startswith(prefix)]
    assert len(hit) == 1, "锚点不唯一: %r (%d)" % (prefix, len(hit))
    s = hit[0]
    e = len(lines)
    for i in range(s + 1, len(lines)):
        if lines[i].startswith("#"):
            lv = len(lines[i]) - len(lines[i].lstrip("#"))
            if lv <= level:
                e = i
                break
    old = len("\n".join(lines[s:e]))
    new = new_text.split("\n") + [""]
    lines[s:e] = new
    return old, len("\n".join(new))


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    lines = read(SKILL)
    before = len("\n".join(lines))

    o1, n1 = replace_section(lines, "### 伤害来源两个口径", NEW_DAMAGE)
    o2, n2 = replace_section(lines, "### 程序集定义（asmdef）", NEW_ASMDEF)
    after = len("\n".join(lines))
    print("伤害两口径: %d → %d" % (o1, n1))
    print("asmdef:     %d → %d" % (o2, n2))
    print("SKILL.md: %d → %d 字符（-%.1f%%）" % (before, after, (before - after) * 100.0 / before))
    write(SKILL, lines)
    if not APPLY:
        print("（自检模式，未落盘）")


if __name__ == "__main__":
    main()
