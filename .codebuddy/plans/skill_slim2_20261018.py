# -*- coding: utf-8 -*-
"""2026-10-18 skill 瘦身 第二刀（kcpnet-online）。

第一刀（skill_slim_20261018.py）把"部署状态/部署清单/库 API/§D/14 条长尾坑"搬进 references。
这一刀继续砍两处**本该在 reference 里、却常驻注入**的内容：
  1) 踩坑清单里那条 500 字的"两种假象"（判据已在 `memory/topics/tooling.md §编译验证`，这里只留指针）
  2) 整节「库 API 速查」→ 追加进 `references/kcpnet-api.md`，SKILL 只留"端口/心跳 + 指针"

为什么用脚本：那条"假缺失"里含 `\\x00` 字面量（反斜杠写法），用文本编辑工具做 old_str 极易因转义层数不匹配而失败；脚本按前缀锚点整行搬运最稳。
自带断言：锚点必须唯一、搬走的行必须能在 references 里找到（防"删了但没搬"）。

用法：python -X utf8 .codebuddy/plans/skill_slim2_20261018.py [--apply]
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIR = os.path.join(ROOT, ".codebuddy", "skills", "kcpnet-online")
SKILL = os.path.join(DIR, "SKILL.md")
APPLY = "--apply" in sys.argv

API_STUB = [
    "## 库 API 速查（完整签名 / 依赖矩阵 → `references/kcpnet-api.md`）",
    "",
    "- **端口与心跳的唯一来源 `NetConfig`**（⚠ 它在**库内部**，工程里没有 .cs ⇒ 常量改不了，只能逐会话设 `TimeoutMs`）：",
    "  `HostGamePort=17666`、`LanBroadcastPort=29800`、`LanSelfBroadcastPort=29801`、`HeartbeatIntervalMs=3000`、`DefaultTimeoutMs=15000`。",
    "- **`KCPSession<T>` 的心跳坑**：库基类自己每 3s 发「心跳裸字节」（带魔数、**不进 `MessageCenter`**，超时 15s）⇒ **替代不了应用层 `PingReq/Rsp`**",
    "  （1001/1002 才是 `RttMs` 唯一来源）。`KCPNet<T,K>` 的 ctor 是 `KCPNet(bool isUnityEnvironment = true)`（可选参数，别误判成要传参）。",
    "- 其余签名（`KCPNet<T,K>` 成员、`LanDiscoveryConfig`/`LanRoomInfo`/`LanBroadcaster`/`LanDiscoverer`、信封与日志出口）→ `references/kcpnet-api.md`。",
]

FAKE_POINTER = (
    "- **\"改了却没编上\"的两种假象**（假空转 / 假缺失 meta）：判据与解药见 "
    "`memory/topics/tooling.md §编译验证`；**决定性判据永远是反射**（`GetType`/`GetMethod` 是否为 null），"
    "别只看 Console 的 0 error。"
)


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


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    lines = read(SKILL)
    before = len("\n".join(lines))

    # ---- 1) 两种假象：整行搬到 pitfalls.md，原位留指针
    anchor = '- **"改了内容却没编上"的两种假象**'
    hit = [i for i, l in enumerate(lines) if l.startswith(anchor)]
    assert len(hit) == 1, "锚点不唯一: %d" % len(hit)
    fake = lines[hit[0]]
    lines[hit[0]] = FAKE_POINTER
    pit_path = os.path.join(DIR, "references", "pitfalls.md")
    pit = read(pit_path)
    pit += ["", fake]
    write(pit_path, pit)
    assert fake.strip() in "\n".join(pit), "搬运丢失"

    # ---- 2) 库 API 整节搬进 kcpnet-api.md
    starts = [i for i, l in enumerate(lines) if l.startswith("## 库 API 速查")]
    assert len(starts) == 1, "库 API 小节不唯一"
    s = starts[0]
    e = next(i for i in range(s + 1, len(lines)) if lines[i].startswith("## "))
    api_block = lines[s:e]
    del lines[s:e]
    api_path = os.path.join(DIR, "references", "kcpnet-api.md")
    api = read(api_path)
    api += ["", "---", "", "## 原 SKILL.md「库 API 速查」全文（2026-10-18 搬入）", ""] + api_block
    write(api_path, api)
    assert api_block[-1].strip() in "\n".join(api), "库 API 搬运丢失"

    lines[s:s] = API_STUB + [""]

    after = len("\n".join(lines))
    print("SKILL.md: %d → %d 字符（本刀 -%.0f%%）" % (before, after, (before - after) * 100.0 / before))
    print("  pitfalls.md %d 字符；kcpnet-api.md %d 字符" % (len("\n".join(pit)), len("\n".join(api))))
    write(SKILL, lines)
    if not APPLY:
        print("（自检模式，未落盘）")


if __name__ == "__main__":
    main()
