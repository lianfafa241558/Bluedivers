# -*- coding: utf-8 -*-
"""追加今日记忆：动画器改成子状态机 + AnyState 中枢。"""
import io

PATH = r"e:\Bluedivers\.codebuddy\memory\2026-11-13.md"

NOTE = """
### 动画器重构：蹲/趴收进子状态机（同日）

- 结构（三个控制器一致，Base Layer）：
  - 顶层状态只剩 `Idle / Move / Ready / Death`；新增两个**子状态机** `Crouch{CrouchIdle,CrouchMove}`、`Prone{ProneIdle,ProneMove}`（含 Entry 过渡各 2 条，内部 Idle↔Move 互切）。
  - 站立/移动的 `Idle↔Move` 直连线**全删**，改成 **AnyState 中枢** 6 条：`Ready / Death / SM:Crouch / SM:Prone / Idle / Move`（后两条条件 `!IsCrouch && !IsProne && ±IsMove`，`CanTransitionToSelf=0`）。
  - 图上只剩 AnyState 一个源点 + 2 个状态机盒子，没有交叉线。
- 脚本：`plans/sm_stance_submachines.py`（幂等：已有 `m_Name: Crouch` 就跳过）。改前先备份到 `_bak_controllers/`。
- ⚠ **踩过的坑**：重写 Base Layer 的 `m_ChildStates` 时只写了 Idle/Move，把 Ready/Death 漏掉 ⇒ 它们虽仍有 doc，但不再是该状态机的子状态 ⇒ 运行时报
  `Transition 'AnyState -> Ready' ... transitions to state 'Ready' which does not exist in controller`，死亡/瞄准动画**整个失效**（而且编辑器看着一切正常）。重排任何 `m_ChildStates` 都必须把原有全部状态列回去。
- ⚠ `AnimatorStateTransition.destinationState / destinationStateMachine` 在 Unity 2022 是**只读**（`AddStateMachine` 也直接返回 `AnimatorStateMachine`，没有 `.stateMachine`）⇒ **AnyState→子状态机只能用 YAML 手写** `m_DstStateMachine: {fileID: <1107>}` + `m_DstState: {fileID: 0}`。若要权威格式，先用编辑器 API 造一个样本子状态机、`SaveAssets` 后读 YAML 抄（Entry 过渡是 `!u!1109`，只有 `serializedVersion: 1` + 条件，没有 duration/exitTime）。
- 实测（`Animator.Update` 推进 + `execute_code`）：Idle→Move→CrouchMove→CrouchIdle→ProneIdle→ProneMove→Move→Death→复活 全通，Aris/Nagisa 同；Console 干净（清空后重新实例化三个模型 0 条）。
- 注意：AnyState 过渡里有 `m_TransitionDuration: 1.0` 的（Death），测状态切换要至少推 1 秒以上帧，否则 `GetCurrentAnimatorStateInfo` 还停在源状态（我因此误判过一次）。
"""

with io.open(PATH, "a", encoding="utf-8", newline="\n") as f:
    f.write(NOTE)
print("appended", len(NOTE))
