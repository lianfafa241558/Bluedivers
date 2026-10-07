# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：传输层会话回调跑在后台线程 → 必须先过队列到主线程。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 修「Load can only be called from the main thread」—— 传输层会话回调在后台线程

- 现象（打包版，房主侧，成员**断开**时）：
  `消息 4004 处理失败:UnityEngine.UnityException: Load can only be called from the main thread.`
  栈：`ResSvc.LoadRes` ← `NetFriendBridge.AttachRoleModel` ← `EnsureFriend` ← `HandleRoster`
  ← `MessageCenter.Dispatch` ← `NetHostSvc.BroadcastPlayerList` ← `NetHostSvc.OnMemberLeft`
  ← `KCPNet.KCPSession.UpdateAsync`（**ThreadPool 线程**）。
- 根因：**消息**路径有线程切换（`HostSession.OnReciveMsg → NetHostSvc.AddMsgQue` → `Update()` 主线程派发 ✅），
  但 **`host.OnSessionConnected/OnSessionDisconnected` 是直连回调**，KCP 在 `UpdateAsync` 的续体里调，
  所以 `OnMemberJoined/OnMemberLeft` 跑在后台线程；而 `OnMemberLeft` 原来直接改 `_players/_profiles`
  并 `BroadcastPlayerList()`（→ 本地 `Dispatch` → Unity API）⇒ 抛 UnityException。
  ⚠ 更阴的是：异常发生在**广播名单中途** ⇒ `NetFriendBridge.HandleRoster` 的后半段（清退 `_stale`）没跑到
  ⇒ 掉线的人**留在场上变幽灵盟友**（日志里同一个名字 3 个不同 sid 的盟友实例就是这么来的）。
- 修法（3 处）：
  1. `NetHostSvc`：新增 `Queue<SessionEvent> _sessionEvents`（事件 = `{Sid, Joined}`，`lock (pkgque_lock)` 入队）；
     `OnMemberJoined/OnMemberLeft` **只入队**（不再动任何游戏状态 / 不发 Unity 调用）；新增主线程
     `ProcessSessionEvents()`（在 `Update()` 开头、消息派发**之前**跑，按到达顺序出队、**锁外**处理），
     原清理逻辑搬进 `HandleMemberLeft(sid)`。
  2. `NetFriendBridge.HandleRoster`：改成**先清退、再新建/挂模型**（原来是先建后清）——
     这样即使挂模型那步抛异常/提前 return，"该走的人"也已经被清掉，不会再留幽灵。
  3. `NetFriendBridge.AttachRoleModel`：新增 `if (fc.RoleId == profile.RoleName) return;`（`FriendController` 补
     只读属性 `RoleId`）—— 名单每次同步（入房/离开/准备切换/资料变更）都会走这里，
     原来每次都 `LoadRes` + `Instantiate` + 让 `AttachModel` 丢掉多余实例，纯浪费。
- 已核对：其余传输层回调 `HostSession.OnConnected/OnDisConnected`、`ClientSession.OnConnected/OnDisConnected`
  **只打日志**（`Debug.Log` 跨线程安全）⇒ 没有第二处同类问题。
- 验证：离线编译 `02_Net / 06_Gameplay / 09_Managers` **0 错误**；Unity 重编译 Console **0 error**；
  反射断言 `ProcessSessionEvents` / `HandleMemberLeft` / `_sessionEvents` / `FriendController.RoleId` 全在位。
- ⏳ 待双人实测：成员断开（关客户端/断网）时房主不再报 UnityException，且该成员的盟友实例被清掉。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
