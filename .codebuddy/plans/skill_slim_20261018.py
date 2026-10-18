# -*- coding: utf-8 -*-
"""2026-10-18 skill 瘦身：把 KCPNet skill 里"会随实现变化"的长尾细节搬进 references/，
SKILL.md 只留判据 + 指针。

为什么用脚本而不是手改：
  1) 要"搬走"的段落必须**原样进 references/**（一个字都不能丢），手工复制易漏；
  2) 要按小节整体替换，行号会随编辑漂移 ⇒ 这里全部用**标题/首句锚点**定位；
  3) 搬走的行必须从 SKILL 删除，且不能连带删到续行 ⇒ 每条都断言"下一行不是缩进续行"。

用法：python -X utf8 .codebuddy/plans/skill_slim_20261018.py [--apply]
不带 --apply 只打印瘦身前后的体积与将要搬走的条目，不落盘。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SKILL_DIR = os.path.join(ROOT, ".codebuddy", "skills", "kcpnet-online")
SKILL = os.path.join(SKILL_DIR, "SKILL.md")
REFS = os.path.join(SKILL_DIR, "references")
APPLY = "--apply" in sys.argv


def read(path):
    with open(path, encoding="utf-8-sig") as fp:
        lines = fp.read().split("\n")
    while lines and lines[-1] == "":
        lines.pop()
    return lines


def write(path, lines):
    if APPLY:
        with open(path, "w", encoding="utf-8", newline="\n") as fp:
            fp.write("\n".join(lines).rstrip("\n") + "\n")


def section_range(lines, prefix):
    """按标题前缀定位小节，返回 [start, end)：end = 其后第一个层级 <= 自身的标题。

    prefix 的 '#' 个数即层级 ⇒ '## X' 会吃到它的所有 '###' 子节，'### D.' 只吃到下一个 '###'/'##'。
    """
    level = len(prefix) - len(prefix.lstrip("#"))
    try:
        start = next(i for i, l in enumerate(lines) if l.startswith(prefix))
    except StopIteration:
        raise AssertionError("找不到小节: %r" % prefix)
    for i in range(start + 1, len(lines)):
        s = lines[i]
        if s.startswith("#"):
            lv = len(s) - len(s.lstrip("#"))
            if lv <= level:
                return start, i
    return start, len(lines)


def cut_section(lines, prefix):
    a, b = section_range(lines, prefix)
    block = lines[a:b]
    del lines[a:b]
    return block, a


def cut_bullet(lines, anchor):
    idx = [i for i, l in enumerate(lines) if l.startswith(anchor)]
    assert len(idx) == 1, "锚点未唯一命中: %r (%d)" % (anchor, len(idx))
    i = idx[0]
    nxt = lines[i + 1] if i + 1 < len(lines) else ""
    assert not nxt.startswith((" ", "\t")), "锚点 %r 是多行条目，脚本不支持，请手工搬" % anchor
    return lines.pop(i)


# ------------------------------------------------------------------ 新文案
HDR_NOTE = [
    "> ⚠ **2026-10-18 瘦身**：实现细节与长尾坑移入 `references/`（**不随 skill 注入**），本文件只留判据/口径/工作流。",
]

NEW_STATUS = """## 现状（2026-10-09）与两条安装点硬约束

- **编译 / 加载 / 挂载 / 接入主流程都已通**：DLL 在 `Assets/Plugins/KCPNet/`（Any Platform）、MessagePack 依赖闭包已补齐、
  运行时加载 `Kcp 2.0.0.0` / `KCPNet 1.0.0.0` / `MessagePack 3.1.8.0`，适配层落 **`02_Net.dll`**（2026-10-09 起**不再在 `Assembly-CSharp`**）。
  各条证据（Console / 反射 / 字节扫描）→ `references/deployment.md`。
- **挂载**：`GameRoot.prefab` 的子物体 `NetRoot` 下 6 个组件（`NetSvc` / `NetHostSvc` / `NetRoomFlow` / `NetTransformFlow` / `TeamNetBridge` / `NetFriendBridge`）。
- ⚠⚠ **两条安装点硬约束**：① `GameRootBase.Awake/OnDestroy` 收集 `I_GlobaManager` **只扫根 + 一级子物体、不递归** ⇒ 挂更深会被**静默漏掉** Init/UnInit；
  ② `NetManager`（现 `LogicFrameHost`）**与网络无关**（50Hz 逻辑帧宿主，26 个 `LogicBehaviour` 靠它跑）⇒ 别把它和网络组件一起挪。
- 任何 KCP 改造前先 `read_console` 确认当前状态；剩下的是"把还没同步的玩法补齐"。"""

NEW_DEPLOY = """## 部署清单（从零到可编译）

1. **放库**：`KCPNet.dll` + `Kcp.dll` 放同一目录 `Assets/Plugins/KCPNet/`，保持默认 Any Platform，不设 `defineConstraints`，不放 `Editor/`。
2. **补依赖闭包**：`python .../scripts/install_deps.py`（`--check` = 只读自检：6 个 DLL 是否齐 + meta 的 Any Platform + 有没有误放 Unity 同名程序集 +
   `02_Net`/`07_NetGame`/`Assembly-CSharp` 是否引到 + Editor.log 最近一次域重载的未解析引用）。
   ⚠ 只要 **6 个**（`MessagePack` / `MessagePack.Annotations` / `Microsoft.Bcl.AsyncInterfaces` / `Microsoft.NET.StringTools` /
   `System.Collections.Immutable` / `System.Runtime.CompilerServices.Unsafe`）；**不要**再放 `System.Memory` / `System.Buffers` 等 Unity 已自带的 BCL（会"同名重复"）。
   逐个 DLL 的版本、为什么需要、Nupkg 取哪个 target → `references/deployment.md`。
3. **刷新**：`refresh_unity(mode=force, scope=all, compile=request)` → `read_console` 看 `CS0246` 残留。
4. **验证四连**（缺一不可，只看 Console 会漏"程序集加载失败"）：① `read_console(types=["error"])` = 0；
   ② `execute_code` 打印 `AppDomain.CurrentDomain.GetAssemblies()` 能看到 `KCPNet`/`MessagePack`；③ 做一次真实序列化往返
   （`MessagePackSerializer.Serialize/Deserialize<LoginReqMsg>`）；④ 仍报 `Assembly '…' will not be loaded due to errors` ⇒ 去 `Editor.log` 看紧随其后的
   `Unable to resolve reference 'X'`（Console 只显示标题行）。
5. **挂载**：只有**新建**常驻管理器才动 prefab，且必须挂**根或一级子物体**。
6. **AOT（IL2CPP）已修好（2026-10-06），别再重走**：Standalone 是 `IL2CPP`，MessagePack 默认走 `DynamicObjectResolver`（`Reflection.Emit`）⇒
   打包版一发消息就 `PlatformNotSupportedException`，**编辑器（Mono）完全看不出来**。现行 = 适配层改走 `NetTmp/Services/NetMsgCodec.cs`
   （只用 `MessagePackWriter/Reader`，`[MessagePackObject]` 写 array 格式、按 `[Key]` 升序）⇒ **新增消息仍只需"CmdId + `[MessagePackObject]` + `[Key(n)]`"**。
   ⚠ 三条红线：保留 `Assets/link.xml`（否则打包后反序列化全是默认值）；新字段类型要在 `NetMsgCodec.WriteValue/ReadValue` 补分支（编辑器里当场抛 `NotSupportedException`）；
   **别**引 `MessagePack.GeneratedMessagePackResolver`（库内那份是 `internal` ⇒ `CS0122`）。备选路与原理 → `references/deployment.md`。"""

NEW_API = """## 库 API 速查（完整签名 / 依赖矩阵 → `references/kcpnet-api.md`）

- **端口与心跳的唯一来源 `NetConfig`**（⚠ 它在**库内部**，工程里没有 .cs ⇒ 常量改不了，只能逐会话设 `TimeoutMs`）：
  `HostGamePort=17666`、`LanBroadcastPort=29800`、`LanSelfBroadcastPort=29801`、`HeartbeatIntervalMs=3000`、`DefaultTimeoutMs=15000`。
- **`KCPNet<T,K>`**（`T`=会话类型、`K`=消息类型）：`StartAsClient/StartAsServer`、`ConnectServer(interval, maxintervalSum=5000)`、`SendToAll(K)`、
  `TryGetSession(uint, out T)`、`GetSessions()`、`CloseClient/CloseServer`、事件 `OnSessionConnected/OnSessionDisconnected/OnServerRestartDetected(Action<uint>)`、
  `ClientId`（持久会话标识，2026-10-09 版新增）。⚠ ctor 是 `KCPNet(bool isUnityEnvironment = true)`（可选参数，别误判成"应该传参"）。
- **`KCPSession<T>`**：必须 override `Serializer` + 至少 `OnReciveMsg`。⚠ 库基类**自己每 3s 双向发"心跳裸字节"**（带魔数、**不进 `MessageCenter`**），
  超时看 `_lastRecvTimeUtcTicks`（15s、任意 UDP 包都刷新）⇒ **它替代不了应用层 `PingReq/Rsp`**（1001/1002 是 `RttMs` 唯一来源，"谁是玩家"的时钟口径也不同）。
- **发现层**：`LanDiscoveryConfig.Setup(...)`（参数全可选，改广播参数的唯一入口）；`LanRoomInfo` 是房间模型（13 字段，`Difficulty/TaskMain/InGame/Source` 已是真字段，
  `FromJson` **忽略未知 key、缺 key 给默认值** ⇒ 新旧房主可混联）；`LanBroadcaster`（ANNOUNCE + 应答）/ `LanDiscoverer`（`Scan()` 是**异步**的）。
- **信封**：`NetMessage : KCPMsg`（`int CmdId` + `byte[] Data`）+ `NetMessageSerializer.Instance`；`KCPTool.Log/ColorLog/WarnLog/ErrLog` 是库日志出口。"""

NEW_D = """### D. 升级/替换库 DLL 后核对 API

库无源码 ⇒ **换版本必须重新核对签名，不要凭记忆改代码**：

1. **Unity 在跑 → 用 Unity MCP 反射（首选）**：`unity_reflect(action="search"|"get_type"|"get_member", …)`
   / `execute_code` 读 `NetConfig` 常量。
2. **Unity 没跑 → `scripts/inspect_dll.py --refs-only`**（打依赖矩阵，缺包就靠它）/ 不带参数打 `TypeDef` 清单（签名与默认值不在静态解析范围内）。

升级后重点确认三件：① `KCPNet.dll` 是否仍只引用 `MessagePack 3.1.8`（版本变了就得换 DLL）；② `NetConfig`/`LanDiscoveryConfig` 的静态默认值；
③ `KCPNet<T,K>` 的 ctor 与 `ConnectServer` 签名。命令与本轮实测记录 → `references/deployment.md`。"""

NEW_PIT_HEAD = """## 踩坑清单

> 长尾细节（MessagePack 版本写法 / `Assembly 'X' will not be loaded` 连锁 / API 兼容级别 Std2.0 / BCL 重复 / `dll.meta` 的 `Editor: enabled: 0` /
> 改已有 DLL 后重导入 / 反复重握手 / 未入房会话循环重连 / `memberIdleTimeout` 8s 比库 15s 更严 / 后进房补发配置 / `Dispatch` 异常要打全 / PowerShell 三个坑）
> → `references/pitfalls.md`（**只有真踩到再去读**）。下面留的是每次改联机代码都要过的判据。
"""


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
    lines = read(SKILL)
    before = len("\n".join(lines))
    moved = []

    # 1) 顶部加一行瘦身说明（插在第一个 '## ' 之前）
    first_h = next(i for i, l in enumerate(lines) if l.startswith("## "))
    lines[first_h:first_h] = HDR_NOTE + [""]

    # 2) 两个整节搬进 references/deployment.md（原样留档，再换紧凑版）
    old_status, _ = cut_section(lines, "## 当前部署状态")
    old_deploy, _ = cut_section(lines, "## 部署清单")
    old_api, _ = cut_section(lines, "## 库 API 速查")
    old_d, _ = cut_section(lines, "### D. 升级/替换库 DLL")
    moved += old_status + old_deploy + old_api + old_d

    # 3) 长尾条目搬到 references/pitfalls.md
    pit_anchors = [
        "- **MessagePack 版本写法**",
        "- **`*.dll.meta` 里 `Editor: enabled: 0` 是无害的**",
        "- **`Assembly 'X' will not be loaded due to errors:` 怎么查**",
        "- **API 兼容级别是 .NET Standard 2.0（不是 2.1）**",
        "- **别重复提供 Unity 自带的 BCL**",
        "- **改已有 DLL 文件后**",
        "- **「同一条 socket 被库反复重握手」**",
        "- **「未入房会话循环重连」**",
        "- **`memberIdleTimeout` 默认 8s，比库自己的 15s 更严**",
        "- **那条\"后进房的人也能拿到本局配置\"靠的是补发**",
        "- **`Dispatch` 的异常要打全**",
        "- 不得不用 PowerShell 时注意两个坑",
        "- ⚠ 本机 PowerShell 另有 **Safe delete bulk-guard**",
        "- ⚠ PowerShell 的 `Select-String -Path \"**/*.cs\"` **不支持递归通配**",
    ]
    pit = []
    for a in pit_anchors:
        pit.append(cut_bullet(lines, a))

    # 4) 把紧凑版插回原位：三段（状态/部署/API）都贴在"适配层"之前，§D 贴在 §E 之前
    def insert_before(prefix, blocks):
        a, _ = section_range(lines, prefix)
        lines[a:a] = [x for blk in blocks for x in (blk.split("\n") + [""])]

    insert_before("## 适配层结构与职责", [NEW_STATUS, NEW_DEPLOY, NEW_API])
    insert_before("### E. 同步一条新东西", [NEW_D])
    # 踩坑清单：指针块插在标题之后的空行位置
    a, _ = section_range(lines, "## 踩坑清单")
    pit_note = NEW_PIT_HEAD.split("\n")[2:]  # 跳过标题行
    lines[a + 1:a + 1] = pit_note + [""]

    after = len("\n".join(lines))
    print("SKILL.md: %d → %d 字符（-%.0f%%）" % (before, after, (before - after) * 100.0 / before))
    print("搬走 %d 行（部署状态/部署清单/库API/§D + %d 条长尾坑）" % (len(moved), len(pit)))

    # 5) 写 references（原样留档）
    dep = [
        "# KCPNet 部署 / 取证 / API 原文留档",
        "",
        "> 从 `SKILL.md` 搬出（2026-10-18 瘦身）。**只在排障、重装、升级 DLL 时读**。",
        "",
    ] + moved
    pitf = [
        "# KCPNet 长尾坑（原文留档）",
        "",
        "> 从 `SKILL.md` 的「踩坑清单 / 工具约定」搬出（2026-10-18 瘦身）。按症状搜，不必通读。",
        "",
    ] + pit
    write(os.path.join(REFS, "deployment.md"), dep)
    write(os.path.join(REFS, "pitfalls.md"), pitf)
    print("references/deployment.md: %d 字符；references/pitfalls.md: %d 字符" % (
        len("\n".join(dep)), len("\n".join(pitf))))
    write(SKILL, lines)
    if not APPLY:
        print("（自检模式，未落盘；加 --apply 才写）")


if __name__ == "__main__":
    main()
