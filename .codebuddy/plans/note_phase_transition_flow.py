# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：按阶段模型落地 TaskConfirm/Transition（重命名 + 三处衔接修正）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 按阶段模型落地：`TaskConfirm` / `Transition` + 三处衔接修正（用户选 A）

- 用户口径（纠正我两次）：`Bridge`（选任务）→ **`Ready`（等所有人就位，仍在舰桥）** → **`Armament`**（`GameStateEnum.Armament = 1<<7 = 128`；仍在舰桥各自选战备）
  → **`Transition`（同时加载战斗场景）** → `Load` → `Game`。⇒ `StartTask` 只是"选完任务"、`StartGame` 不是"开局"，**命名都得改**。
- **重命名（脚本 `plans/rename_task_confirm_tokens.py`，52 处 / 自带"旧 token 清零"断言）**：
  `StartGameNtf`→**`TaskConfirmNtf`**、`CmdId.StartGameNtf`→**`CmdId.TaskConfirm`**（**保持 4006 不变**）、
  `NetHostSvc.StartGame`→**`ConfirmTask`**、`NetRoomFlow.OnStartGame/HandleStartGameNtf`→**`OnTaskConfirm/HandleTaskConfirmNtf`**、
  `TeamNetBridge.HandleStartGame`→**`HandleTaskConfirm`**、`SelectMapWnd.StartTask`→**`ConfirmTask`**。
- ⚠⚠ **一次误伤（已还原，值得记）**：token 级替换把 `10_Effect/ShootMapPreView.cs` 里**与联机无关**的
  `public void StartTask()` 也改了名 —— 而它是被**场景 UnityEvent 按方法名调用**的
  （`Assets/Scene/Teach.unity:24319`、`Assets/Scene/BattleScene.unity:2392` 的 `m_MethodName: StartTask`）⇒ 改名即静默失效。
  已用 `plans/restore_shootmap_preview.py`（`git show HEAD:` 取内容后原字节写回）整份还原。
  ⇒ **教训：批量改方法名前，先 grep 场景/prefab 的 `m_MethodName`**（`.cs` 里搜不到调用点 ≠ 没人调）。
  顺带发现：该文件 HEAD 版本就有 **49 个 NUL 字节**（乱码注释里，git 因此判其为二进制），属既有现象。
- **新增 `CmdId.Transition = 4012` + `TransitionNtf { MatchId }`**（房主权威"进战斗"通知）：
  房主侧 `TeamNetBridge.HandleGameStateChange` 监听 `GlobalEventSub.OnGameStateChange`，`entry == Transition` 且自己是房主
  且本局没发过（`_transitionSent`，回 `Ready` 复位）⇒ `NetHostSvc.NotifyTransition()`（广播 + **冻结名单** + `_started = true`）；
  成员侧 `HandleTransition` **只把 `GameRoot.GameState` 推到 `Transition`** ⇒ 由**大厅既有的**
  `GameStateController(state: 8) → TransSceneController.StartLoad()` 完成加载（转场音乐 + `BattleManager.Creat(true)`）。
  ⚠ 成员**绝不能**在这里自己 `AsyncLoadScene`（会双加载）；`GameRoot.GameState` 的 setter 自带"同值不发事件" ⇒ 两条路径天然幂等。
- **三处衔接修正**（都源于"StartTask 之前被当成开局"）：
  1. `TeamNetBridge.HandleTaskConfirm` **去掉 `AsyncLoadScene`/`_loadingBattleScene`**：只落配置（`SetSeed` + `SetTask` + 收起选图窗）
     —— 旧实现让成员在 Ready 阶段就跳进战场而房主还在舰桥。
  2. 名单冻结从 `ConfirmTask` 挪到 `NotifyTransition`：Ready/Armament 期间**仍可进人**（公开房后加入的人现在能进名单）。
  3. `OnJoinRoomReq`：**冻结时**回 `SendJoinFail("本局已开始")`；**未冻结时补发** `_lastTaskConfirm`（新人拿得到地图/任务/种子才能进 Ready）。
     `StartHost`/`StopHost` 一并复位 `_rosterFrozen`/`_lastTaskConfirm`/`_matchId`。
- **验证**：离线编译 `02_Net / 09_Managers / 10_UI` **0 错误**；Unity 重编译后 Console **0 error**；
  MCP 全 DTO 往返 **22/22 通过**（含新增 `TransitionNtf=OK(2B)`）；反射断言：`NotifyTransition`/`ConfirmTask`/`OnTransition`/
  `HandleTransition`/`HandleGameStateChange` 在位，`CmdId.Transition=4012`、`CmdId.TaskConfirm=4006`（旧值保留），
  `CmdId.StartGameNtf` 与 `_loadingBattleScene` 残留 = **False**。
- ⏳ 待双人实测：Ready/Armament 期间新成员能否加入并拿到配置；两端是否**同时**进 Transition（本地路径 + 房主通知二选一先到）。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
