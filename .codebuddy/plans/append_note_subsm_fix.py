# -*- coding: utf-8 -*-
"""更正：AnyState→子状态机 会每帧重入。"""
import io

PATH = r"e:\Bluedivers\.codebuddy\memory\2026-11-13.md"

NOTE = """
### 更正（同日）：**AnyState → 子状态机 会每帧重入，`CanTransitionToSelf=0` 对它无效**

- 现象：Animator 窗口里 `Any State → Entry → CrouchIdle` 一直高亮（蓝线），像在反复进入同一个状态。
- 量化（`execute_code` 逐帧采样）：`CrouchMove` 60 帧里 **50 帧处于 `IsInTransition`**，`normalizedTime` 0.13→0.22 又跳回 0.11（周期约 5 帧）；对照组 `Move`（AnyState 指向**普通状态**）0 帧过渡、时间正常累加。
  ⇒ 结论：`canTransitionToSelf` 只对"目标是 AnimatorState"生效；目标是 AnimatorStateMachine 时 Unity 每帧都判定为可进入，**蹲/趴动画等于一直在重启**。
  （上一节"实测 OK"是误判：当时我用 `t2 > t1` 判断，重启周期恰好让末值略大于初值。）
- 修法（已落地，脚本 `plans/sm_stance_fix_entry.py`，幂等）：
  - 删掉 `AnyState → Crouch/Prone`；
  - 改由**具体状态**进入：`Idle/Move --IsCrouch--> CrouchSM`、`Idle/Move --IsProne--> ProneSM`（源状态是实状态 ⇒ 进去后源不再激活，不可能自循环）；
  - 蹲↔趴直接互切：`CrouchIdle/CrouchMove --IsProne--> ProneSM`、`ProneIdle/ProneMove --IsCrouch--> CrouchSM`；
  - 回站立仍由 `AnyState → Idle/Move`（普通状态目标，安全）兜住。
- 校验方法（可复用）：逐帧 `anim.IsInTransition(0)` 计数 + `normalizedTime` 是否单调；**只看最终停在哪个状态是查不出重入的**。
- 另：`AnimatorStateTransitionInspector.DoErrorAndWarning` 的 NRE 是 **Unity 自身检视面板**在"选中一条目标为子状态机/目标状态被删掉的过渡"时抛的，只影响 Inspector 绘制，不影响运行；重选一次过渡即消失。
- ⚠ 用 `execute_code` 验证状态机必须在**非 Play** 下做（Play 里 `InstantiatePrefab` 会往运行场景塞物体）；进 Play 时 `refresh_unity` 会超时，属正常。
"""

with io.open(PATH, "a", encoding="utf-8", newline="\n") as f:
    f.write(NOTE)
print("appended", len(NOTE))
