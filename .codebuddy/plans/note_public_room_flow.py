# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：地图界面「公开房」改为选完任务后静默建服。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 「公开房」流程改造：不再立刻开房 / 不掀服务器列表，选完任务后静默建服（用户口径）

- 需求原文：创建一个公开房间**不打开服务器列表界面**，而是和单人一样调整难度等，**直到选择完任务后，静默创建这个服务器**。
- 原来：`SelectMapWnd.pubilc` → `SelectPlayMode=1` → `CreateRoom()` —— 当场 `flow.Host()` 开房、弹「房间已创建」、
  并且末尾 `serverPanel.Open()` **把服务器列表顶上来**（就是用户要去掉的行为）。
- 现在（4 处改动，都在 `Assets/Scripts/10UI/Wnd/SelectMapWnd.cs`）：
  1. `taskPublic` 按钮 = 只记 `SelectPlayMode = 1` + 走 `ExpandCfg()`（与「单人」完全同一条界面：调难度 / 选任务 / 选加成）。
  2. `CreateRoom(bool silent = false)`：`silent` 时跳过「房间已创建」提示与 `serverPanel.Open()`；
     **失败提示保留**（"联机不可用"/"开房失败"不静默）。
  3. 新增 `private static bool IsHosting()`：判据 = `NetHostSvc.RoomInfo != null`（`StartHost` 里设、`StopHost` 里清）。
  4. `StartTask()` 开头：`if (SelectPlayMode == 1 && !IsHosting()) { CreateRoom(true); if (!IsHosting()) return; }`
     —— 位置刻意放在 **`SetWndState(false)` 之前**（建服失败就停在选图界面重试，不先把界面关掉），
     且必须在下面的 `StartGame` 之前（那段广播靠 `RoomInfo` 判断"我是不是房主"，没房间就轮不到广播本局配置）。
- ⚠ **已告知用户的后果**：房间在"按下准备"那一瞬才广播，而同一瞬 `SetTask` 就进准备/开局（大厅里那枚按钮的 UnityEvent
  会 `TransSceneController.StartLoad()`）⇒ **别人几乎没有可加入的窗口**。要真正能被加入，得把建服提前到"选完任务但还没开局"，
  或建服后停在舰桥等成员齐了再 `StartGame`。
- 验证：离线编译 `10_UI` **0 错误**；Unity `refresh_unity(scripts, request)` 后 `10_UI.dll` mtime 前进（16:00:04）、Console **0 error**。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
