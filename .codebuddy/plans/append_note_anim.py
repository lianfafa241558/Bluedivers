# -*- coding: utf-8 -*-
"""追加今日记忆：蹲/趴动画片段与动画器接线。"""
import io

PATH = r"e:\Bluedivers\.codebuddy\memory\2026-11-13.md"

NOTE = """
## 蹲/趴动画：程序化派生 + 动画器接线（已落地）

- 资源：`Assets/Art/Anim/Student/Noa/CrouchWalk.anim`、`ProneWalk.anim`（**三个角色共用**，靠 humanoid 重定向；控制器原本就都引用 Noa 的 Idle/Walk/Ready/Death）。
  - 由 `Walk.anim` 派生：改 `m_FloatCurves` 与 `m_EditorCurves` **两处镜像**的 `value:`（只加偏移，不动结构），脚本 `plans/gen_stance_clips2.py`。
  - 蹲：`RootT.y -0.45`、`Upper Leg Front-Back +0.40`、`Lower Leg Stretch -1.00`、`Foot Up-Down +0.25`、`Spine Front-Back -0.15`。
  - 趴：`RootQ` 四条通道写**常量俯仰 84°**（⚠ 四条通道关键帧时间不统一，按索引对齐会错位 ⇒ 只能写常量）+ `RootT.y -0.58` + 腿摆幅 `flatten 0.35`（趴着腿不能继续大摆）。
  - 实测（眼睛高度对齐用）：Noa 站/蹲/趴 头顶 1.29 / 0.85 / 0.34；Aris、Nagisa 重定向后偏差 < 0.05 ⇒ 共享片段可行。
- ⚠ **关键结论**：`m_ApplyRootMotion = 0` 时，`RootT.y` 与 `RootQ` **仍然作用于姿势**（它们的 clip 设置是 Bake Into Pose：`m_KeepOriginalPositionY/Orientation = 1`）⇒ 想压低/放平模型必须改这两组曲线，不是改 GameObject。
- ⚠ 肌肉方向实测（Walk 基线，脚 y=0.188）：`Upper Leg Front-Back +0.3` ⇒ 脚 +0.061y / -0.099z；`Lower Leg Stretch +0.3` ⇒ 脚 **-0.083y**（**正的 Stretch = 伸直**，想让膝盖弯要**负值**）；`Foot Up-Down` 基本只转脚背；`Spine Front-Back` 只影响上半身。
- ⚠ 别再把肌肉值夹到 [-1,1]：Walk 里 Upper Leg Front-Back 本来就到 1.11，夹了会把摆腿压平。
- 动画器（Aris/Noa/Nagisa 各 +348 行，纯追加）：Base Layer 加 `CrouchIdle/CrouchMove/ProneIdle/ProneMove` 四状态 + 4 条 AnyState（`IsCrouch&&!IsMove` 等，**CanTransitionToSelf=0**，排在 Ready/Death 之后）+ 4 条回站立过渡（`IsCrouch=false`→Idle/Move）。
  - **Idle 状态用 `m_Speed: 0` 冻结同一片段**当蹲/趴静止姿（省两份大资源，代价：没有呼吸起伏）。
  - 移动状态速度按移速倍率调低：CrouchMove 0.7、ProneMove 0.45（减滑步）。
  - 脚本 `plans/patch_stance_controllers.py`（重跑会跳过已打过的）、`plans/tune_stance_state_speed.py`；Nagisa 的默认状态名是 **`Idle 1`**（带空格），不是 `Idle`。
- 相机高度改为按实测头顶给：`_crouchCameraHeight=0.82`、`_proneCameraHeight=0.30`（**不按胶囊等比**：胶囊下限 radius*2=0.8，等比算出来趴姿相机 0.58，比头还高半米）。
- 验证：MCP `execute_code` + `AnimationMode.SampleAnimationClip` 量骨骼高度；`Animator.Update` 推进状态机实测 crouchMove/proneMove/standup 全对；Console 0 error；`offline_compile 06_Gameplay` 0 error。
- 已知不足：蹲/趴无手部姿势（游戏里手臂由 AimIK 接管，影响有限）；趴姿爬行时小腿略抬起（0.3 左右）。
"""

with io.open(PATH, "a", encoding="utf-8", newline="\n") as f:
    f.write(NOTE)
print("appended", len(NOTE))
