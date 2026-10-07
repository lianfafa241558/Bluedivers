# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：成员端任务面板不显示（animator inactive）+ 双方看不见彼此（资料没上报）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 修两处联机表现问题（成员端任务面板 / 双方看不见彼此）

- 现象：加入者的 `BridgeWnd.taskRoot` 停在 **Idle**（不播 Entry），虽然状态确实到了 `Ready`；且**双方看不见彼此**。
- **① 任务面板：真凶是 Console 里播放器传来的最后一条 `Game object with animator is inactive`。**
  先把调用链查清了：`DisplayTask` **不是**某个按钮的 UnityEvent 调的，而是**大厅场景里的 `GameStateController`**
  （guid `566bc32b…`，`Utnapishitim.unity:12157`）在 **`state: 4 = GameStateEnum.Ready`** 那张表里调的（`:12250-12258`）
  ⇒ 两端本来都该触发。差别在**下手顺序**：
  - 房主：`SelectMapWnd.ConfirmTask` 里先 `SetWndState(false)`（关选图窗 ⇒ BridgeWnd 变可见）→ 再 `SetTask`（→Ready）⇒ `DisplayTask` 打到激活物体 ✅
  - 成员：`TeamNetBridge.HandleTaskConfirm` 里**先 `SetTask`（→Ready）**、后 `WindowRegistry.SetState(SelectMap,false)`
    ⇒ Ready 那一刻 BridgeWnd 还隐藏（`WindowState = UI`）⇒ `taskRoot.GetComponent<Animator>().Play("Entry")` 静默失败 ❌
  ⇒ 修法：成员侧改成**先收界面、再 SetTask**（与房主同序）；并给 `BridgeWnd` 加两层保险：
  `DisplayTask` 前置 `task == null || !task.activeTask` 早退（`taskCfg` 是**结构体**、不能判 null，用 `activeTask`）
  + `_shownTaskKey`（"地图|任务名|难度"）**同一局只展开一次**；`ShowWnd` 时若已在 `Ready` 就补一次
  （解决"Ready 时窗口还隐藏"），不在 Ready 则清 key（下一局重新展开）。
- **② 看不见彼此：盟友实体其实建出来了，但**没有身体模型**——资料从没上报过。**
  `PlayerProfile` 原先只在**换角色**时上报（`BridgeRoleManager.SetPlayerRole`），入房后双方资料都停在
  "只有名字、`RoleName` 为空"的占位 ⇒ `NetFriendBridge.AttachRoleModel` 因 `RoleName` 为空直接 return
  ⇒ 盟友只有空壳（`PlayerFriend.prefab` 本身不含身体，身体是运行时挂 `StudentModle/<RoleName>`）。
  ⇒ 修法：`TeamNetBridge` 新增 `HandleJoinResult`（订阅 `NetRoomFlow.OnJoinResult`）：**入房成功就 `SendSelfProfile()`**；
  房主侧在 `HandleGameStateChange` 的 `entry == Ready` 分支里也 `SendSelfProfile()`（⇒ `NetHostSvc.SetLocalProfile`
  ⇒ `HostProfile` 有值 ⇒ `BuildProfileArray` 第 0 项带 RoleName ⇒ 成员端才给房主挂得上身体）。
  ⚠ 排查线索：日志里 `[NetFriendBridge] 创建盟友实例 sid=…`（实体建了）/ `[NetFriendBridge] 找不到角色模型 …`（路径问题），
  以及 `NetFriendBridge.friendPrefab` 在 `GameRoot.prefab:16598` 已回填（guid `f5fd0a39…`）。
- 验证：离线编译 `09_Managers / 10_UI` **0 错误**（中途踩过一次：`TaskCfg` 是结构体，`== null` 报 CS0019，改判 `activeTask`）；
  Unity 重编译 Console **0 error**；反射断言 `HandleJoinResult` / `BridgeWnd.DisplayTask` / `_shownTaskKey` / `CloseJoin` 全在位。
- ⏳ 待双人实测：①加入者能看到任务面板展开；②双方都能看到对方的角色模型（且能被位姿驱动移动）。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
