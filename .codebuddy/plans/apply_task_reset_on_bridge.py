# -*- coding: utf-8 -*-
"""TaskManager：进入 GameStateEnum.Bridge 时清空上一局任务（2026-10-02）。

改动：
1) TaskManager.cs
   - Init() 订阅 GlobalEventSub.OnGameStateChange / UnInit() 退订（成对）
   - 新增 OnGameStateChange + ResetTask()：nowTask 换空 + 复用 SyncTaskState() 发布中性值
   - 同步两处已是「过期描述」的注释：SyncTaskState 文档里的发布点数量（2→3）、
     EnterTransition 注释里"不需要订阅 OnGameStateChange 广播"这句话
2) TaskState.cs
   - 写入约定文档里的发布点数量（2→3）

按行索引精确插入，保留各文件原有的 BOM 与行尾（TaskManager.cs 为混合 CRLF/LF，
TaskState.cs 为纯 LF + BOM）。
"""

import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # Windows 控制台默认 GBK，避免打印中文报错

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TM_PATH = os.path.join(ROOT, "Assets/Scripts/09Manager/Global/TaskManager.cs")
TS_PATH = os.path.join(ROOT, "Assets/Scripts/04Data/TaskState.cs")


def read_lines(path):
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    return bom, text.split("\n")


def write_lines(path, bom, lines):
    raw = "\n".join(lines).encode("utf-8")
    if bom:
        raw = b"\xef\xbb\xbf" + raw
    open(path, "wb").write(raw)


def find_unique(lines, needle, what):
    hits = [i for i, l in enumerate(lines) if needle in l]
    if len(hits) != 1:
        sys.exit("FAIL: 锚点不唯一 %s (%d 处) needle=%r" % (what, len(hits), needle))
    return hits[0]


def replace_in_line(lines, old, new, what):
    i = find_unique(lines, old, what)
    line = lines[i]
    ending = "\r" if line.endswith("\r") else ""
    body = line[:-1] if ending else line
    if old not in body:
        sys.exit("FAIL: %s 行尾处理异常" % what)
    lines[i] = body.replace(old, new) + ending
    print("  [%s] 行 %d 已更新：%s" % (what, i + 1, lines[i].rstrip()))


# ---------------------------------------------------------------- TaskManager
NEW_BLOCK = r"""
        /// <summary>
        /// 回到大厅（<see cref="GameStateEnum.Bridge"/>）时清空上一局任务：<c>nowTask</c> 换空 + 发布中性值。
        ///
        /// <para>▍为什么挂在阶段广播上：任务的"起"是 <see cref="SetTask"/>（选图开新局），"止"就是回大厅
        /// —— 撤离结算后由场景里的 <c>BridgeRoleManager</c>（或 EndGame 判负分支）把阶段落成 <c>Bridge</c>，
        /// 本方法在那儿收摊。不这么做，上一局的 <c>nowTask</c>（难度 / 收集表 / 战备表 / <c>activeTask</c>）
        /// 会一直留在大厅，让"还有没有任务"的判断读到过期值
        /// （<c>TaskState.HasTask</c>、<c>SettingWnd</c> 里按 <c>nowTask.activeTask</c> 决定显示/隐藏任务卡）。</para>
        ///
        /// <para>▍与 <see cref="SyncTaskState"/> 同一套发布口径：复位后直接复用它发布中性值，不另写一份。</para>
        /// </summary>
        private void OnGameStateChange(GameStateEnum exit, GameStateEnum entry)
        {
            if (entry != GameStateEnum.Bridge) return;
            ResetTask();
        }

        /// <summary>
        /// 清空当前任务：<c>nowTask</c> 换成一个空任务，并把中性值发布给下层（见 <see cref="SyncTaskState"/>）。
        ///
        /// <para>▍⚠ 换实例是安全的：<c>TaskState.CollectProperty</c> 是**活引用**，而所有消费点
        /// （<c>OOPart</c> / <c>BattleManager.SubmitOOPart</c> / <c>Furniture_KeiSubmit</c>）都是**当次读取**
        /// <c>TaskState</c> 或 <c>nowTask</c>，没有谁把老字典存下来；换完立刻由 <c>SyncTaskState</c>
        /// 把 <c>TaskState.CollectProperty</c> 改指到新空表。</para>
        ///
        /// <para>▍倒计时读数 <c>TaskState.Countdown</c> 不在这里复位：它归"开始新一局"
        /// （见 <see cref="SetTask"/> 里的 <c>TaskState.Countdown = 16</c>）。
        /// 这里只让 <c>HasTask=false</c>，把一局内的逻辑（撤离点 <c>MedivacController</c> 等）关掉 ——
        /// 与"还没选图"时的中性态完全一致。</para>
        /// </summary>
        private void ResetTask()
        {
            nowTask = new SelectTaskData();
            SyncTaskState();
        }
""".strip("\n").split("\n")


def patch_task_manager():
    print("taskmanager %s" % TM_PATH)
    bom, lines = read_lines(TM_PATH)

    # 1. SyncTaskState 文档：发布点 2 处 -> 3 处
    replace_in_line(
        lines,
        "目前 2 处：<c>Init()</c>（建任务）与 <c>SetTask()</c>（选图/难度）。",
        "目前 3 处：<c>Init()</c>（建任务）、<c>SetTask()</c>（选图/难度）与 <c>ResetTask()</c>（回大厅清空）。",
        "SyncTaskState 文档",
    )

    # 2. 在 SyncTaskState 的右花括号之后插入新方法
    j = find_unique(lines, "TaskState.CollectProperty = haseTask", "SyncTaskState 末尾定位")
    brace = j + 1
    if lines[brace].strip() != "}":
        sys.exit("FAIL: SyncTaskState 右花括号不在预期行：%r" % lines[brace])
    new_lines = [""] + [l + "\r" for l in NEW_BLOCK]
    lines[brace + 1:brace + 1] = new_lines
    print("  已插入 OnGameStateChange / ResetTask（插在行 %d 之后，共 %d 行）" % (brace + 1, len(new_lines)))

    # 3. Init() 订阅
    k = find_unique(lines, "            Awake();", "Init 里的 Awake()")
    lines.insert(
        k + 1,
        "            GlobalEventSub.OnGameStateChange += OnGameStateChange;//回大厅（Bridge）清空上一局任务（与 UnInit 的退订成对）\r",
    )
    print("  Init() 已订阅 OnGameStateChange（行 %d）" % (k + 2))

    # 4. UnInit() 退订
    u = find_unique(lines, "public void UnInit()", "UnInit 声明")
    end = None
    for i in range(u + 1, len(lines)):
        if lines[i].strip() == "}":
            end = i
            break
    if end is None:
        sys.exit("FAIL: 找不到 UnInit 右花括号")
    lines.insert(
        end,
        "            GlobalEventSub.OnGameStateChange -= OnGameStateChange;//与 Init 的订阅成对\r",
    )
    print("  UnInit() 已退订 OnGameStateChange（行 %d）" % (end + 1))

    # 5. EnterTransition 注释块里"不需要订阅 OnGameStateChange 广播"已过期
    replace_in_line(
        lines,
        "//   ⇒ 于是既不需要专属事件、也不需要订阅 `OnGameStateChange` 广播。",
        "//   ⇒ 于是不需要专属事件；`OnGameStateChange` 广播直到 2026-10-02 才被本类订阅 ——\r\n"
        "        //     只用于「回到大厅（Bridge）时清空上一局任务」（见 ResetTask()），与本段流程无关。",
        "EnterTransition 注释",
    )

    write_lines(TM_PATH, bom, lines)


def patch_task_state():
    print("taskstate %s" % TS_PATH)
    bom, lines = read_lines(TS_PATH)
    replace_in_line(
        lines,
        "—— 目前 2 处：`Init()`（建任务）与 `SetTask()`（选图/难度）。漏一处就会读到过期值。",
        "—— 目前 3 处：`Init()`（建任务）、`SetTask()`（选图/难度）与回大厅时的 `ResetTask()`（清空）。"
        "漏一处就会读到过期值。",
        "TaskState 写入约定",
    )
    write_lines(TS_PATH, bom, lines)


if __name__ == "__main__":
    patch_task_manager()
    patch_task_state()
    print("DONE")
