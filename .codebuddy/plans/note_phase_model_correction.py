# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：阶段模型纠正（StartTask = Ready，仍在舰桥）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ⚠ 更正（用户纠正）：`StartTask` ≠ 立刻进战斗，阶段模型是 Ready → Armament → 加载战斗

- **我上一条记述里的"房间广播与开局同瞬发生 ⇒ 别人几乎没有加入窗口"是错的**，作废。
- 正确模型（用户口径 + 代码核对）：`SelectMapWnd.StartTask` **只**把 `GameState` 推到 **Ready**，**这时还在舰桥**；
  **所有人都就位**才进 **Armament 阶段（仍在舰桥，各自选战备）**；**该阶段结束才加载战斗场景**。
  加载的触发点在**大厅的家具状态机**：`Assets/Scene/Utnapishitim.unity:15614-15621`（`state: 8` →
  `m_Target: TransSceneController.StartLoad()`；同组 `state: 128` → `Animator.Play("StartArmament")`），**不在 StartTask 里**
  —— 这也解释了为什么 `TransSceneController.StartLoad` 全仓只有这一个调用点。
- ⇒ 本轮"公开房：`StartTask` 时才静默建服"的改动**是对的**：建服后进入 Ready/Armament，全程还在舰桥，成员有充足窗口加入。
- ⚠⚠ 但顺着这个模型发现**两处衔接确实是错的**（已写进 `topics/net.md`，尚未动代码）：
  1. `NetHostSvc.StartGame`（房主，被 `SelectMapWnd.StartTask` 调用）里 `_rosterFrozen = true` **在 StartTask 就冻结名单**
     ⇒ Ready/Armament 期间后加入的成员不会进名单广播；房主自己的 `TeamManager` 也拿不到新人（本地名单靠"广播 + 自派发"）
     ⇒ **公开房"后加入的人"整条链不通**。修法：冻结推迟到"真的进战斗"，或直接依赖 `TeamNetBridge.HandlePlayerList`
     里已有的 `BattleState.IsStartBattle` 门（那里已经做到了"战斗中只更新字段不重排"）。
  2. `TeamNetBridge.HandleStartGame` 收到 `StartGameNtf` 直接 `AsyncLoadScene(Constants.BattleSceneName)`
     ⇒ **成员在 Ready 阶段就跳进战场，而房主还留在舰桥**（两端不同步）。修法：`StartGameNtf` 只当"本局配置已定"
     （`SetSeed` + `SetTask` + 收起选图界面），"进战斗"另发一个信号（房主在 Armament 结束/`StartLoad` 那一刻广播）驱动成员加载。
- 待用户拍板后再动协议（涉及新消息/新语义）。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
