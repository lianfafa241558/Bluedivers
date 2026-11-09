
## 终端交互时开窗（ESC 设置）关窗后"又能走又能控终端"（修复，2026-11-09）
- 现象：操作 KeyScreen 终端时按 ESC 打开设置窗、关掉之后玩家既被 WASD 移动、终端也在吃同一批键。
- 根因（三处叠在一起）：① `PlayerOperationController.OnWindowStateChange` 在窗口离开 Game 态时把 `PlayerInputHandler.InOperation` 清成 false（本意是别让关窗后还被交互锁住），但**家具自己的 `inOperate`/`owner` 没人收**；② KeyScreen 的 `owner` 仍在 ⇒ `Update` 照旧跑阶段、读输入；③ 方向键 Left/Up/Right/Down 在 `Assets/Resources/Prefabs/Manager/InputManager.prefab` 里 `window: 0`（= `WindowStateEnum.All`）⇒ 与窗口状态无关，UI 里照样有效。终端的 `flags: 2`（SwitchState）⇒ 正常操作时正是靠 InOperation 挡住移动，所以一被清掉就"双吃"。
- 修法（`Assets/Scripts/06Gameplay/Interactable/KeyScreen.cs`）：`OnEnable/OnDisable` 订阅 `GlobalEventBus.OnWindowStateChange`；离开 Game 态且 `furn.inOperate && owner != null && IsStageDriver()` 时调 `furn.Operate()` —— 与玩家再按一次 E 完全同一条退出路径（`SetOwener(null)` + inOperate 翻转 + 4059 上报）。阶段进度不重置，回终端再按 E 可继续。
- ⚠ 关键门：`IsStageDriver()` 不能省 —— 联机时非操作端的 `owner` 是盟友镜像，跟着收会派发 `FurnitureOperate` ⇒ 4059 上报，反把真正在操作的一端关掉。
- ⚠ `inOperate` 也要判：`eject` 阶段（Load/Wait）家具已 `Operate()` 掉、`inOperate` 为假，而 KeyScreen.owner 是**陈旧非空**（eject 不会清它）⇒ 只看 owner 会把无人值守阶段误打断/甚至把 inOperate 翻回 true。
- 验证：`python .codebuddy/plans/offline_compile.py 06_Gameplay` → 0 error。⚠ 该文件 HEAD 是 **LF + 无 BOM**（同目录兄弟文件是 BOM+LF），`replace_in_file` 会整份写成 CRLF ⇒ 已用字节级脚本还原 LF，diff 只剩 +29/-0。
