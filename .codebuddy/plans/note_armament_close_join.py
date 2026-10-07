# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：Armament 阶段关闸（不再收人），且关闸必须与冻名单分开。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 追加：进 Armament 就不再收人（关闸与冻名单必须分开）

- 用户口径：**收人窗口只有 `Ready`** —— 一进 `Armament`（人员已就位）就不能再有人加入。
- ⚠ 关键约束（决定了不能复用 `_rosterFrozen`）：`ArmamentWnd` 的"**全员就绪 → 进 Transition**"判定，
  靠的是 `PlayerListSync` 里同步的**准备状态**（房主 `OnReadyState` → `BroadcastPlayerList`）⇒
  在 Armament 冻名单会把准备状态一起冻住，**这个阶段就永远走不完**。所以关闸与冻名单是两件事。
- 落码：
  - `NetHostSvc` 新增 **`_joinClosed`**（独立于 `_rosterFrozen`）+ 公开方法 **`CloseJoin()`**；
    `OnJoinRoomReq` 顺序变成：`_rosterFrozen` ⇒ `"本局已开始"`；`_joinClosed` ⇒ **`"人员已就位"`**；然后才是满员/密码校验。
  - `TeamNetBridge.HandleGameStateChange` 现在挂两件事：`entry == Armament` ⇒ `CloseJoin()`（仅房主）；
    `entry == Transition` ⇒ `NotifyTransition()`（广播 + 冻名单，每局一次）。两处都只在 `flow.IsHost` 时执行。
  - 放行点：下一局 **`ConfirmTask`** 开头 `_rosterFrozen = false; _joinClosed = false;`；`StartHost`/`StopHost` 也复位。
- 验证：离线编译 `02_Net / 09_Managers` **0 错误**；Unity 重编译 Console **0 error**；
  反射断言 `NetHostSvc.CloseJoin`、私有字段 `_joinClosed`、`NotifyTransition`、`ConfirmTask`、
  `TeamNetBridge.HandleGameStateChange` 全在位。
- ⏳ 待双人实测：Armament 期间第三人点击加入应收到"人员已就位"的失败提示（而不是静默卡住）；
  同时 ArmamentWnd 的准备状态同步（至少两人）仍要正常，两端要能一起进 Transition。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
