# -*- coding: utf-8 -*-
import io

TEXT = """
### 更正：#2 载具 HUD 看不见的真凶是**根 CanvasGroup**（2026-11-08 二轮，MCP 实测）

- 现场（Play 模式 `FindObjectOfType<VehicleUI>`，实例 `Turret_Manual_AT(Clone)`）读数：
  - `VehicleUI.Canvas`（=`Root` 子组）`alpha=1`；`_shouldShow=True`；`WindowState=Game`；`anim.IsActive=True`、当前 clip=`Vehicle_Entry`；
  - **但 `VehicleCanvas`（HUD 根）自己的 `CanvasGroup.alpha = 0`** ⇒ 整块 HUD 全黑。上一轮只写 `Canvas` 字段所以白改。
- 预制体 `Assets/Resources/UI/Vehicle/VehicleCanvas.prefab` 里三个 CanvasGroup：`Root`=1、`Exosuit_Sight`=1、**`VehicleCanvas`=0**。
  设计意图是 `Vehicle_Entry` 把根组写成 1（Export 曲线 path='' keys=1 value=1）、`Vehicle_Exit` 写回 0。
- ⚠ **但这两条动画曲线运行时不生效**：把根组 / `Exosuit_Sight` 改成 0.5 再 `Animator.Update(dt)`（play 内、多个 50ms 帧）
  值一动不动；连 `RectTransform.localScale` / `anchoredPosition` 都不会被写回 ⇒ Animator 只是推进了 normalizedTime，不写任何属性
  （controller=VehicleCanvas、avatar=null、enabled=true、activeInHierarchy=true 全都正常）。
  推论：任何依赖这套 UI 动画的显隐都不成立，根组只能由代码写。
- 修法：`VehicleUI` 在 `Awake` 缓存 `_rootGroup = GetComponent<CanvasGroup>()`，`RefreshAlpha()` 同时写 `_rootGroup.alpha` 与 `Canvas`(Root 子组)。
  显隐判据仍是 `_shouldShow && (Game||Airdrop) && 非沉浸模式`（存档实测沉浸模式=0，不是它）。
- 调试纪律提醒：`Animator.Update(0.05f)` 在**暂停的 Play** 里能推进 normalizedTime 但可能不应用曲线，
  想验证"动画有没有写属性"必须真的跑帧；本次是靠"改值再看有没有被写回"确认的。
"""

p = 'd:/Pro/Bluedivers/.codebuddy/memory/2026-11-08.md'
with io.open(p, 'a', encoding='utf-8', newline='\n') as f:
    f.write(TEXT)
print('ok')
