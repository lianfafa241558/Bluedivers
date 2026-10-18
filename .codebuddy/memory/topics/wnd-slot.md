# 主题 · UI / 窗口（Wnd 槽架构 + 收尾清单）

> 由 `contract.md` 拆出（2026-10-18）。契约层其余（能力下沉 / 接缝判据 / IPlayerUnit / 枚举序列化）在 `contract.md`。
> 预制体与面板布局另见 `ui-wnd.md`。

## UI / 窗口（Wnd 槽收尾 · 2026-10-01）
- **`ServiceLocator` 已整体删除**（含 `Wnd` 槽），`IWindowService` 契约也删。原 `WndManager` 持有的 10 个具体窗口字段（`operationWnd`…`settingWnd`，属 UI 类型 ⇒ 反向依赖）已迁出。
- **当前架构**：
  - 玩法层开窗 = 发 `GlobalEventSub.OpenWnd(WndType)`（无返回值命令走事件，事件层 = `06_Gameplay`）
  - `WndManager`（09_Managers）订阅 → 转调 `WindowRegistry`（契约层委托插槽）
  - 窗口实例在 `WndHub`（10UI 的 `internal static class`）`map<WndType,Window>` 字典；`Window.Awake` 自注册 + `Scan(true)` 兜底（`Scanner` 经静态构造 + `[RuntimeInitializeOnLoadMethod]` 双保险）
  - `WndHub` 把能力（SetWndState / IsOpen / CreatNotice / CreatCountDown / ClearNotice / Information / **Msg**）以**委托**登记进 `WindowRegistry`
  - `WindowState` 状态**数据自持**到 `04Data/UIState.cs`（玩法层输入门控读 `UIState.WindowState`，初值必须 `Game`，枚举 0 值是 `All`）
  - 弹提示 = `GlobalEventSub.Notice`（同链路）；倒计时 = `WindowRegistry.CountDown`
  - ⚠ `WndHub` 是 `internal`：现 `10_UI` 已是独立 asmdef ⇒ **编译期牙齿已生效**（`WndHub.cs:31-33` 注释写"在 Assembly-CSharp 里"已过时）
- **层判据**：`09_Managers` 不能引用 `10_UI` ⇒ 经契约层 `WindowRegistry` 委托中转切断反向依赖；`WindowRegistry` 的"退出条件（删类）"**未到**（`10_UI` 仍不可反向被 `09` 引用）
- **`WndType`（契约层枚举，原 `WndTypeEnum`）**：跨层开窗唯一钥匙；具体 UI 类型**只在 `WndHub`/`10UI`**；新增窗口**只改 `WndHub.TypeOf` 一处**映射（`WndType.cs` 注释写"WndManager.SetWndState 的映射"已过时）
- **新增窗口 SOP（2026-10-03 实例 `InformationWnd`）**：①`WndType` **末尾**追加（枚举重度序列化）②`WndHub.TypeOf` 加一行（**漏了 = 窗口注册不进 map，能力静默失效**）③窗口脚本（**禁 Awake**，初始化放 `FirstShowWnd`）④跨层动词：`WindowRegistry` 加 `Action` 插槽 + `WndHub.Bind()` 登记 + `WndHub.Xxx` 强类型属性；新增 .cs **Unity 不 import 就不进编译**（`manage_asset action=import` 显式导入）
- ⚠⚠ Unity 不为 `SetActive(false)` 对象调 `Awake` ⇒ 自注册死锁；兜底见上；**`Window` 子类禁定义 `Awake`**（替代 = `FirstShowWnd()`）
- 往老文件插 Unity 特性前先确认有 `using UnityEngine;`
- **消息播报接缝（2026-10-12 落地，第 3 个 `Information` 同款）**：玩法层 `GameMsg.*`（06_Gameplay，文案/颜色唯一出处）→ `GlobalEventBus.OnMsg` → `WndManager.OnMsgRequest` → `WindowRegistry.MsgImpl`（UI 侧 `WndHub.Bind()` 登记）→ `MsgWnd.Push(text)`；`WndType.Msg` 追加在枚举**末尾**（=13）。
  落点：`Furniture_PlayerDown.ApplyRemoteDown(true)`（倒地）、`MissionView/InterestPoint.TryDiscovered`（发现）、`TeamNetBridge.AnnounceRoster`（加入/退出任务 = **玩家名单变化**，见下）。
  ⚠ "首次发现"要自己用 `bool first = !discovered` 守卫：`RevealAll`/`EventStart` 会对已发现的任务重调 `TryDiscovered`（那里**不能**加早退，会打断小地图暴露）。
  **⚠ 加入/退出任务 = 名单（4004），不是任务生命周期**（2026-10-12 用户纠正过一次）：唯一落点是 `09Manager/TeamNetBridge.AnnounceRoster`（在 `HandlePlayerList` 的两条分支后各调一次）+ `HandlePlayerLeft`（4029，战斗期名单冻结时唯一信号）。键 = 权威 sid、跳过自己；`_lastRoster`(sid→PlayerData 活引用，走掉的人据此留名) + `_announcedJoin`(已报过加入) 做差异与去重；**加入要等 `roleName` 到齐才报**（否则没有角色色）；退出与加入对称。
  **"是谁" = `GameMsg.Who{Name,Color}` + `GameMsg.Of(IEntity)`**：玩家颜色取 `IActor.Color`（= `BaseObject.Color`，与 HUD/小地图同源）；**入房/舰桥阶段还没有 Actor** ⇒ 名单那条用 `TeamNetBridge.RoleColor(roleName)`（读 `Prefabs/StudentModle/<role>` 预制体上的 `BaseObject.Color`，带静态缓存 + `ResSvc` 未就绪降级 `Resources.Load`；实测 Aris `#A1FFFF`/Hare `#FFF764`/Nagisa `#CCB08E`/Noa `#64BDFF`/Yuuka `#726DCC`）；倒地来源取 `Health.OnDie(source)`（`Furniture_PlayerDown` 原本把它丢了，现走私有 `ApplyDown(down, source)`，`ApplyRemoteDown(bool)` 旧签名保留给 `FriendController`，那条路没有来源）。触发者：任务点 = 本机玩家，兴趣点 = `ActorsManager.NearestPlayer(pos)`。
  文案口径（用户 2026-10-12 定稿）：`XX发现黑匣子` / `XX发现补给箱`（`GameMsg.Discover`，任务点名与兴趣点名共用一条）、`XX加入任务` / `XX退出任务`（**不带任务名**，故 `JoinMission/ExitMission` 只吃 `Who`）；倒地 = `XX`(玩家色)`被<来源>`+`击倒`(**只有"击倒"两字红**，用户 2026-10-12 拍板)`了`，来源认不出 ⇒ 省掉"被谁"。
  **名字口径（2026-10-12 下半场定稿）**：`XX` = **玩家名**，取 `IPlayerUnit.PlayerName`（本机 = `ArchivesData_SO.Current.playerName`；盟友 = `FriendController.PlayerName`），空 ⇒ 回落 `IEntity.ShowName`（角色名）。
  ⚠ `IPlayerUnit.PlayerName` 与 `BaseSelfController.PlayerName`（= `Actor.ShowName`，**角色名**）**不是一回事** ⇒ `PlayerController` 用**显式实现**，否则被基类那个顶掉、播报印角色名。给 `IPlayerUnit` 加成员是破坏性变更，实现者只有 `PlayerController`/`FriendController` 两处（后者隐式满足）。
  `GameMsg` 另有 `Of(GameObject)`（自身→父物体找 `IEntity`）与 `NameOf(GameObject)`（倒地"被谁"）。⚠ 编辑器里 `ArchivesData_SO.Current == null`（存档运行时才加载）⇒ 编辑模式回落角色名，属预期。

## Wnd 槽收尾 · 剩余清单（待执行，尚未改代码）
1. ~~**编译红（阻塞）**：`00Tools/WndRootTool.cs` 用 `TMPro` 但 `00_Utils.asmdef` 未引用 `Unity.TextMeshPro` ⇒ 2×CS0246。修：给 `00_Utils.asmdef` references 加 `"Unity.TextMeshPro"`。~~ ✅ **2026-10-13 复核为已解决**：`offline_compile 00_Utils 00_Core` = **0 error**，该阻塞项不存在。
2. ~~asmdef 悬空引用~~ ✅ **已执行（2026-10-01）**：从 `10_UI`、`06_Gameplay`、`09_Managers`、`10_Effect` 的 references 删 `00_WndTools`，从 `10_UI` 删 `04_UI`；asmdef 内 `00_WndTools`/`04_UI` 已归零，`.cs` 无残留引用（仅 `VehicleWeaponsManager.cs:3` 一行过时注释提及，非代码）。
3. **死代码链**：`WndManager.IsWndOpen`（0 调用）+ `WindowRegistry.GetOpen`/`IsOpen` + `WndHub.Bind()` 的 `IsOpen` 登记 → 整条删（无跨层消费者）。✅ **2026-10-13 复核：仍在** —— `WndManager.cs:200` 的 `IsWndOpen` 全仓 **0 调用者**；`WindowRegistry.cs:31` 的 `IsOpen` 只被 `GetOpen:70` 与排查用 `Dump():108` 读 ⇒ 整条可删。
4. **UI 层直连 `WndManager`**（合法但风格不统一，可选改事件）：行号已漂 —— `BridgeWnd.cs:131`、`DeathUI.cs:74`、`SettingWnd.cs:743` 与 `:767`（`CreatNotice`/`CreatCountDown`/`ClearNotice`）+ `10_Effect/VFX/VFXAirdropEffect.cs:709`（`CreatNotice` → 可改 `GlobalEventBus.OnNotice`，注意**旧名 `GlobalEventSub.Notice` 已作废**）。✅ 仍在
5. **过时注释清理**：`WndHub.cs:31`（仍写"`04UI/` 根目录在 `Assembly-CSharp` 里、还没有牙齿" ⇒ 实际 `10_UI` 早已独立、牙齿已生效）；`WndType.cs:4-9`（仍写 `GlobalEventSub.OpenWnd` / `WndManager.SetWndState 的映射` / `04UI` ⇒ 应为 `GlobalEventBus.OnOpenWnd` + `WndHub.TypeOf`；枚举值注释里的 `WndManager.xxxWnd` 字段早已删除）；`WndManager.cs`（`01Manager`→`09Manager`、`04UI`→`10UI`、`SetWndState 的映射`→`WndHub.TypeOf`）。✅ 仍在
6. **`WndManager` 死代码**：空 `Start()`（`WndManager.cs:76-79` 方法体只有一句注释）+ 注释掉的 `CreatSpeech` 块（`:165-168`）→ 删。✅ 仍在
- ⚠ 玩法层残留 `WndManager` 引用**全是注释/被删代码**（`Furniture_General`/`Furniture_AttachedGeneral`/`MissionEvacuateStatic` 的注释；`WeaponPlayerController:266` 在 `/* */` 块；`InputManager:39` 已改 `UIState.WindowState`）→ 不需改。
- ⚠ **改 asmdef 会触发 Unity 重编译**（用户前台即时重编）→ 执行前须告知用户。
