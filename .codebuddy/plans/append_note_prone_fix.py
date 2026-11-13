# -*- coding: utf-8 -*-
"""追加今日记忆：趴姿修正 + 武器挂点随姿态下沉。"""
import io

PATH = r"e:\Bluedivers\.codebuddy\memory\2026-11-13.md"

NOTE = """
### 趴姿修正 + 武器挂点随姿态下沉（同日，用户实机反馈）

- 实机问题：趴下时「头朝下 + 腿浮空」，且枪仍停在站立胸口高度。
- 趴姿根因：`RootQ` 前倾 84° 后，**膝盖仍是弯的**（当时 LLS 用 +0.10 且 flatten 0.5）⇒ 小腿在旋转后的身体坐标系里朝上翘。
  - 修法（`plans/gen_stance_clips2.py`，新增 `consts` 参数直接写常量而非加法）：`RootQ` 88°、`RootT.y -0.62`、
    `Upper Leg Front-Back = 0.02`（大腿与身体同线）、`Lower Leg Stretch = 0.95`（**伸直**，膝盖不弯）、`Foot Up-Down = -0.35`。
  - 蹲姿**不用改**：edit 模式实测脚在地面（foot 0.06~0.22），是采样方法不同导致我一度误判它下沉。
- 武器挂点：`Player.prefab` 里 `FirstPersonSocket`(0,1.15,0.1) 挂在 **Player 根节点**，`WeaponParentSocket`/`DefaultWeaponPosition`/`AimingWeaponPosition`/`DownWeaponPosition` 全是它子节点 ⇒ **不吃相机高度**。
  - 修法：`PlayerController_Stance` 记录站立挂点坐标，切姿态时按 `_crouchSocketDrop=0.60` / `_proneSocketDrop=1.00` 改 `FirstPersonSocket.localPosition.y`
    （`PlayerController.Awake` 里把 `WeaponsManager` 赋值挪到 `InitStance()` 之前，否则拿不到挂点）。
  - 盟友侧同口径：`FriendWeaponView.SetStanceSocketDrop()`（把 `aimSocketOffset.y` 减掉下沉量），由 `FriendController.ApplyStance` 按同步姿态调用；`ResetAnimState` 归零。
- ⚠ **采样方法教训**（重要）：Play 模式下用 `PlayableGraph + AnimationClipPlayable + anim.runtimeAnimatorController=null` 采样，**只应用了 RootT/RootQ，肌肉曲线没生效**（腿永远保持绑定姿势），据此判定"蹲姿陷地"是错的。
  可靠路径是 **edit 模式 + `AnimationMode.SampleAnimationClip`**；Play 模式下 `Animator.Update` / PlayableGraph 都不可靠（还会受控制器自身播放覆盖）。
  另外 `EditorSceneManager.NewPreviewScene()` 里创建的 Animator **完全不求值**（两种方式都是绑定姿势）。
- 想要"看"姿势：edit 模式下 `AnimationMode` 采样后，用临时 Camera + RenderTexture 渲 PNG 写进 `Temp/`，再 `read_file` 读图（Play 模式同样不可靠）。
"""

with io.open(PATH, "a", encoding="utf-8", newline="\n") as f:
    f.write(NOTE)
print("appended", len(NOTE))
