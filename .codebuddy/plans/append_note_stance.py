# -*- coding: utf-8 -*-
"""追加今日记忆：玩家蹲/趴姿态系统。"""
import io

PATH = r"e:\Bluedivers\.codebuddy\memory\2026-11-13.md"

NOTE = """
## 玩家姿态系统：下蹲 / 卧倒（06_Gameplay + 07/09，已落地）

- 键位（用户已定）：下蹲 = **C(99)**、卧倒 = **Z(122)**、`Mule`(呼叫 Kei) 从 C 挪到 **V(118)**、`Equip`(丢弃装备) 保持 X(120)。改动落在 `Assets/Resources/Prefabs/Manager/InputManager.prefab` 的 `key:32 / key:23`。
  - ⚠ `InputManager.Init()` 用磁盘 `inputBindings.json` **整表覆盖** `inputList`，只按 (key,window) 补缺失项 ⇒ 老存档机器看不到新键位，必须设置界面"恢复默认"或删 json。
  - `InputState.Prone(31)/Crouching(32)` 枚举早就有、prefab 里也有条目（窗口=1），只是以前没人调用。
- 命名解耦：`PlayerInputHandler.GetCrouchDown/GetCrouch/GetCrouchUp` 实际读的是 `InputState.FlareGun`（左Ctrl 信号枪）⇒ 改名 `GetFlareGunDown/GetFlareGunHeld/GetFlareGunUp`（调用点仅 `PlayerWeaponsManager` 4 处）；新增 `GetCrouchToggleDown/GetProneToggleDown`（门控 `CanProcessInput(WindowStateEnum.Game)`）。
- 核心：`PlayerStance.cs`（枚举 Stand=0/Crouch=1/Prone=2，**联机按 byte 传，禁止插值改序**）+ `PlayerController_Stance.cs`（切换式状态机）。
  - 胶囊：`height = 蹲1.2 / 趴0.8`，**必须同时写 `center.y = height*0.5`** 才不漂脚（`GetCapsuleBottomHemisphere` 口径依赖）。
  - 第一人称镜头：`CameraBasePoint.y` 按 `height/standHeight` 等比缩放（基类 LateUpdate 的 Lerp 天然平滑）。
  - 第三人称/死亡相机：轨道中心从 `CenterPos` 换成 `StanceOrbitCenter = CenterPos + up*StanceOrbitOffsetY`（偏移=镜头基准差值）。
  - 移速：**不碰 `MoveSpeedScale`**（已被武器惩罚/手持装备/冲刺 `*/=` 共用），基类新增 `protected virtual float ExtraSpeedScale`，只在 GroundMove/AirMove 各乘一次；蹲 0.5x / 趴 0.2x。
  - 冲刺/跳跃：`UpdateSprint` 与 `TryJump` 里"先 `SetStance(Stand)` 再执行"⇒ 允许冲刺激活，但姿态自动回立姿。
  - 复位：`OnDie` / `OnRevive` / `OnDisable`（进载具）一律回立姿。
- 射击更稳：`WeaponBaseController.CurrentSpreadAngle` 折 `StanceSpreadScale`（AI 默认 1 不受影响）；`WeaponController.GetShotDirectionWithinSpread`（第一人称实际走的那条）在**末尾**乘一次（不动原有定点除法口径）；`WeaponPlayerController.RecoilForceFinal` 乘 `StanceRecoilScale`；倍率由 `PlayerWeaponsManager.LateUpdate → ApplyStanceWeaponScales()` 每帧写给主/副手（不装 Modifier，避免切枪漏装）。
- 动画占位：`Aris/Noa/Nagisa.controller` 各追加 `IsCrouch`/`IsProne` bool（脚本 `plans/add_stance_anim_params.py`，保留 BOM/换行：Aris/Nagisa 是 CRLF、Noa 是 LF）；玩家与盟友都用 `Animator.parameters` 缓存 HasParameter，缺参数静默跳过。
- 联机：`PoseSnapshot` 追加 `[Key(7)] byte Stance`（Key6 已是 Teleport，只追加不重排；旧端缺键=0=立姿）；上行 `NetFriendBridge.LocalStanceByte()`，下行 `FriendController.ApplyPose(pos,yaw,pitch,teleport,byte)` → `ApplyStance` 直写动画参数（盟友没 CC/相机，胶囊镜头不用还原）。
- 验证：`offline_compile.py` 06(含新文件，走 `tmp_compile_newfile.py`)/07/09/10 全 0 error；Unity MCP `refresh_unity` 后 Console 0 error、`is_compiling:false`、`Library/ScriptAssemblies/06_Gameplay.dll` 含 `PlayerStance/StanceSpreadScale/StanceOrbitCenter`；prefab 键位自检无重复 (window,KeyCode)；编辑过的 .cs 仍是 LF。
- ⚠ 未做（用户明确不选）：**起身头顶阻挡检测** ⇒ 在低矮结构下站起会与几何重叠，靠 CC 自行解算。
- ⚠ 已知表现缺口：角色没有蹲/趴动画资源，蹲下时模型仍全高站立（参数已下发，等美术接线）。
"""

with io.open(PATH, "a", encoding="utf-8", newline="\n") as f:
    f.write(NOTE)
print("appended", len(NOTE), "chars")
