# -*- coding: utf-8 -*-
"""向 2026-10-02 记忆文件追加一条记录（保原编码/换行/BOM 状态，append-only）。"""
import io

p = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-02.md"
raw = open(p, "rb").read()
bom = raw[:3] == b"\xef\xbb\xbf"
txt = raw.decode("utf-8-sig")
nl = "\r\n" if "\r\n" in txt else "\n"

note = """
## 第三人称「未瞄准时武器也对齐视野中心」（2026-10-02 第四轮）

- **需求**：第三人称下不按瞄准键时，武器不跟随视野中心 —— 枪口停在切视角时的旧俯仰角，子弹也沿枪口 forward 飞、不收敛到准星。
- **根因（证据）**：
  - `Assets/Scripts/06Gameplay/Player/PlayerWeaponsManager.cs` 原 `UpdateWeaponThirdPersonAim()` 条件为 `IsThirdPerson && IsAiming` ⇒ 未瞄准时 `ThirdPersonAimTarget = default`，`WeaponPlayerController.GetShotDirectionWithinSpread()`（`WeaponPlayerController.cs:233`）退回枪口 forward。
  - `Assets/Scripts/06Gameplay/Player/Controller/PlayerController.cs` 第三人称分支（原 :394-401）只在 `WeaponsManager.IsAiming` 时写 `FirstPersonSocket.transform.rotation = Euler(m_CameraVerticalAngle, transform.eulerAngles.y, 0)`；未瞄准时插座保留第一人称残留的 local pitch（切视角时冻结）。
- **改法（2 文件，未动 prefab/资产）**：
  - `PlayerWeaponsManager` 新增 `public bool ThirdPersonAimAtViewCenter = true`（InspectorName「第三人称不瞄准也瞄准视野中心」），`UpdateWeaponThirdPersonAim` 条件改为 `IsThirdPerson && (IsAiming || ThirdPersonAimAtViewCenter)`。
  - `PlayerController.LateUpdate` 用同一条件同步插座世界旋转，公式不变（相机俯仰 + 角色 yaw）：瞄准时角色已 slerp 到 `_cameraYaw`，故等价于完全对齐相机。
- **⚠ 关键取舍**：未瞄准分支的 yaw 取 `transform.eulerAngles.y`（角色朝向）而非 `_cameraYaw`。因为非瞄准移动时角色朝移动方向（侧移/倒退时与相机 yaw 可差 180°），若硬用 `_cameraYaw` 会让枪口与身体撕裂、手臂 FBBIK 崩。要「yaw 也硬对齐」只需把该行换成 `_cameraYaw`（副作用已知）。
- **边界**：散布仍按 `m_InAiming` 区分（未瞄准散布系数 1 vs 瞄准 0.3），本改动只让方向收敛到准星；第一人称不受影响（`ThirdPersonAimTarget` 仍为 `default` ⇒ 走枪口 forward）。
- **验证**：`plans/offline_compile.py 06_Gameplay` = **0 错误**（未触发 Unity 编译，等用户自行刷新/Play 验证手感）。
"""

if not txt.endswith(nl):
    txt += nl
txt += note.replace("\n", nl)
open(p, "wb").write((b"\xef\xbb\xbf" if bom else b"") + txt.encode("utf-8"))
print("appended, new bytes =", len(open(p, "rb").read()))
