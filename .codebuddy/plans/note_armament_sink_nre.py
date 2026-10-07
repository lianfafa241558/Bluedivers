# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：联机名单同步 NRE（ArmamentWnd sink 在窗口未显示时被调用）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 修掉联机「消息 4002 / 4004 处理失败：NRE」—— ArmamentWnd 在窗口未显示时被当成 sink 调用

- 现象（**打包版**、两人联机）：房主收到成员入房、广播名单时报 `消息 4004 处理失败:Object reference not set to an instance of an object`；
  成员侧同时报 `消息 4002 处理失败:<同>`。前一条上一轮的 `PlatformNotSupportedException`（MessagePack/IL2CPP）**已不再出现** ⇒ AOT 修复在真机生效。
- **共同凶手是 `ArmamentWnd.ReceivePlayerReady`**：两条报错都汇进 `NetRoomFlow.OnPlayerList`
  （4002 = `HandleJoinRoomRsp → FinishJoin` 里 `OnPlayerList?.Invoke(players, null)`；4004 = `HandlePlayerListSync`），
  而它的两个订阅者 `TeamNetBridge.HandlePlayerList` / `NetFriendBridge.HandleRoster` 里，**唯一会写 UI 的就是
  `TeamNetBridge.NotifyReadyChanged → BridgeSys.ReceivePlayerReady → ArmamentWnd.ReceivePlayerReady`**。
  `ArmamentWnd` 在 `Init()`（:51，随场景加载就跑）里就把自己登记成 `BridgeSys.Instance.armament`，
  但 `ready` / `animators` / `buttons` 是 **`FirstShowWnd()`**（:57-58）才 new 的 ⇒ **只要测试时没打开过战备窗口**，
  `ready[playerIndex] = state` 就是 NullReferenceException（两大报错同时命中，正好两台的战备窗都没开过）。
- 另有一处同源隐患：`ready`/`animators` 数组长度是 `Constants.MaxPlayer`，而**只有当时有人在的槽位**才在 `ShowWnd()` 里
  `animators[i] = ...`（:185，无人的槽位只 `SetActive(false)`）⇒ `animators[playerIndex]` 可能是 **null**（下标合法也会 NRE）。
- 修法（4 个文件）：
  1. `10UI/Wnd/ArmamentWnd.cs` 三个 `Receive*`（`ReceivePlayerReady` / `ReceivePlayerSelectTeamEnhance` / `ReceivePlayerSelectAemament`）：
     前置 `ready/animators/buttons == null` 早退（窗口没显示过时不碰 UI；数据落点已在常驻的 `TeamNetBridge`，不会丢）、
     下标上下界、`animators[i] != null` 才 `SetBool`、`airdrop` 为 null 时惰性补 `new int[4]`。
  2. `09Manager/Global/TeamNetBridge.cs`：`NotifyReadyChanged` 从"建表循环内"移到 **`ReplacePlayers` 之后**——
     否则新成员的 `PlayerData.index` 还是默认 **0**（= 自己那一行！），既点亮错行，又让 `_lastReady` 提前记下旧下标导致后续 UI 不刷新。
  3. `09Manager/Global/NetFriendBridge.cs`：`HandlePoses` 加 `batch == null || batch.Items == null` 早退。
  4. `NetTmp/Client/MessageCenter.cs`：`Dispatch` 的 catch 改打 **`e.ToString()`（完整异常 + 调用栈）**——
     原来只打 `e.Message`，导致这种"处理失败"日志**完全看不出是哪个处理器哪一行**（本轮就是被它拖了一轮）。
- 验证：离线编译 `02_Net / 09_Managers / 10_UI` **0 错误**；`read_lints` 0；Unity `refresh_unity(scripts, request)` 后
  `Library/ScriptAssemblies/{02_Net,09_Managers,10_UI}.dll` mtime 前进到 15:51、Console 无 CS 错误
  （残留的两条 error 是**播放器**（`<i>WindowsPlayer ...</i>`）传来的运行期日志，正是本次要修的那两条）。
- ⏳ 待用户复测：两人联机（房主/成员**都不打开战备窗口**也应无报错）；若仍报，日志现在会带完整调用栈，可直接定位。
- 顺带记录：`ArmamentWnd` 的"窗口未显示"状态下，早退会让准备状态的**文字/动画**不刷新（`_lastReady` 已记过就不会再通知）。
  这是纯显示问题（数据在 `TeamNetBridge`/`TeamManager` 里是对的），要修的话应在 `ShowWnd` 里按表回填一次。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
