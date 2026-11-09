# -*- coding: utf-8 -*-
"""追加 2026-11-09 换弹反向加弹修复的记忆条目（UTF-8 + LF，不改动已有内容）。"""
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
P = os.path.join(ROOT, '.codebuddy', 'memory', '2026-11-09.md')

NOTE = """## 换弹"反向加弹" / 三连发来回换弹（WeaponReloadController.ReloadEnd）
- 现象：SecretaryDecision 装 `WUD_OverpressureLoad`（二轮爆发/三连发：BulletsPerShot 1→3、ShootCost 1→3、BurstInterval 0→0.06）后，弹匣2/后备1 会一直换弹，2/1 → 1/2 → 2/1 死循环。
- 根因（`WeaponReloadController.ReloadEnd` 旧式）：`newAmmo = Min(Ammo.CurrValue, Magazine.FinalValue)` 把**后备弹量**当成了换弹后的**弹匣总弹量**；弹匣没打空时 newAmmo < oldAmmo ⇒ `Ammo.CurrValue -= (newAmmo-oldAmmo)` 变成**反向加弹**（总弹数不变、两格互换）。循环的触发前提是 `WeaponController.LogicTick` 那条"残弹 < ShootCost 就自动换弹"兜底（ShootCost=3 ⇒ 弹匣 1~2 时每帧重试）。
- 已修：只补空位 —— `space = FinalValue - CurrValue; take = Min(Ammo.CurrValue, space); Magazine.CurrValue += take; Ammo.CurrValue -= take;`（`WeaponReloadController.cs:233-242`）。修后 2/1 → 3/0 一次到位，ammo=0 自然停止换弹。
- 口径：`ReloadEnd` 三个调用点（换弹计时到点 / AutomaticReload 的 speed>99 / `ReloadComplete`）都是"补满弹匣"语义，统一受益。同类**增量式**自动装弹 `WeaponController.UpdateAmmo`（Magazine += speed*dt，Ammo -= speed*dt）未改 —— 接近满匣时会短暂多抽后备，留待实际表现再定。
- 验证：`offline_compile.py 06_Gameplay` 0 error；文件仍 LF + BOM。

"""

with open(P, 'rb') as f:
    old = f.read()
if '换弹"反向加弹"'.encode('utf-8') in old:
    print('already appended, skip')
else:
    with open(P, 'a', encoding='utf-8', newline='\n') as f:
        f.write(NOTE)
    print('appended', len(NOTE.encode('utf-8')), 'bytes')
