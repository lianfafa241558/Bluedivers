# -*- coding: utf-8 -*-
"""追加 2026-11-14 日记忆：家具空引用 6 处修完（append 模式）。"""
import io
import os

P = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'memory', '2026-11-14.md')

NOTE = """
## 家具空引用 6 处已修（06Gameplay，offline_compile 0 error）
- `GuideController.Operate`：先判 `_guideInfos == null || _currentIndex < 0 || >= Count` 再索引（-1 时交互会 ArgumentOutOfRange）。**未加 base.Operate()**（同步是另一件事，没动行为）。
- `Furniture_Pipe.Place`：`root &&` 再 `TryGetComponent<CCDIK>`；`!lastPipeline || !lastPipeline.lastPipeline.IsValid()`。
- `Furniture_Pipe.Link`：`Temp`/父级/`GetComponentInChildren<Furniture_Pipe>()` 全判空，失败一条 LogError + return（不再造半截管道）。
- `Furniture_Artillery.Awake`：无 WeaponController ⇒ LogError + `enabled=false` + return；`OnDestroy` 退订补判空；`PlaceHeldItem` 首行补 `!m_weapon`。
- `Furniture_PlayerArtillery.Awake`：同上（无武器 ⇒ LogError + `enabled=false`，OnEnable 不跑 ⇒ StartSubmit/OnAirdrop 都不会碰 m_weapon）。
- `Furniture_WeaponPickup`：`ShowName => m_Weapon ? m_Weapon.WeaponName : name`；`FormatGroundState` 无武器时返回 `x,y,z,-1,-1`。
- `Furniture_General`：KeyScreen 分支 `GetComponent<KeyScreen>()` 判空 + LogError；`_InOperateUpdate` 的 relatedTrans、SignaTower 的 relatedTrans、BlackBox 的 relatedTrans 全部先判；Supply 的 `transform.parent.parent.GetComponent<Animator>()` 改成逐级判空取 `supplyAnim`。
- 口径：全部"缺前置条件 ⇒ 一条日志 + return/不做"，不加降级链；`enabled=false` 只用于"整个组件依赖的武器缺失"这种配置错。
"""

if __name__ == '__main__':
    with io.open(P, 'a', encoding='utf-8', newline='') as f:
        f.write(NOTE)
    print('appended', P)
