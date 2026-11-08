# -*- coding: utf-8 -*-
import io

TEXT = """
## 六连修（用户报的 6 条，2026-11-08）

1. **GuideController 到达后注视玩家**：`Update` 新增 `FacePlayerWhileWaiting()`（`!IsMoving && IsGuiding && _playerTransform != null` 时 `FaceTarget(player, _rotateSpeed)`）；`MoveRoutine` 末尾的 `RotateToRoutine(target)` 改为**只在没有玩家时**才跑（有玩家时它每帧会和 Update 抢朝向）。
2. **VehicleUI 透明度一直 0**：新增唯一出口 `RefreshAlpha()` = `_shouldShow && (Game||Airdrop) && !沉浸模式`。原来 `StateChange` 只在 `WindowState==Game` 时写 alpha ⇒ **在战备菜单(Airdrop)里上/下载具时那一拍不写**，而 alpha 早被 UI/自由相机置 0 ⇒ 之后永远 0。同时删掉 `SetAlpha(Canvas,0,1,1000)` 的定时淡入（它先把 0 写上再等 viewTimer，定时器没跑就永远 0）。
3. **ArmamentWnd 一进来就是"就绪"**：`ShowWnd` 里重置 airdrop/boosterId 时**漏了 `data.isReady`** ⇒ 上一局结束时的就绪带到新一局（战备被 SelfReady 锁死 + ready[] 全满直接推倒计时）。已补 `data.isReady = false`；并新增 `B` 键切换就绪（`Update` → `ToggleSelfReady()`，与就绪按钮同一条路，按钮那条原来硬用行号 0 当自己）。
4. **RoyalRose 蓄力时脚步声疯狂重复**（用户自己定位）：`PlayerWeaponsManager.SetWeaponState` 对 `OnShoot`/`OnWantShootChange` **只 `+=` 没先 `-=`**，同一把枪重复 SetWeaponState(true) 就重复订阅 ⇒ `MoveSpeedToShoot=0.5` 被扣两次 ⇒ `MoveSpeedScale = 0`；而 `BaseSelfMoveableController` 的脚步声是 `FootstepSfxFrequency / MoveSpeedScale` ⇒ **除零 ⇒ 频率 ∞ ⇒ 阈值 0 ⇒ 站桩也每帧一声**。修：三个事件统一先 `-=` 再 `+=`；`OnWantShootChange` 改按枪记账（`HashSet<WeaponBaseController> _shootSlowWeapons`，幂等，双持各算各的）；脚步声分母 `Mathf.Max(MoveSpeedScale, 0.01f)`。
5. **第一人称瞄准镜显隐反相**：`PlayerWeaponsManager.IsAiming` 的 setter **先 Invoke 后赋值**，而 `OnAiming` 回调里读 `IsAiming` ⇒ 读到上一帧值 ⇒ 瞄准镜"不瞄显示、瞄准隐藏"。修：先落值再抛事件；`ApplyScopeVisible(weapon, aiming)` 改成显式传参（换枪/切视角路径传 `IsAiming`）。
6. **Boss 死亡音乐不停**：`HpItemBoss.End()` 走 Death 动画、**不经过 `Hide()`** ⇒ `Show()` 里那次 `SetLockMusic(true)` 没人解锁。修：End 末尾按 `_visible` 还原 `PlayMusic(Game)` + `SetLockMusic(false)`（只在"确实是我们的音乐"时还原，避免血条早就 Hide 过又重放战斗 BGM）。

验证：`offline_compile.py 06_Gameplay 10_UI` 均 0 error；`read_lints` 0。
"""

p = 'd:/Pro/Bluedivers/.codebuddy/memory/2026-11-08.md'
with io.open(p, 'a', encoding='utf-8', newline='\n') as f:
    f.write(TEXT)
print('appended', len(TEXT))
