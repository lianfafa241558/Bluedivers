# -*- coding: utf-8 -*-
import io

p = r'e:/Bluedivers/.codebuddy/memory/2026-11-09.md'
add = """

## 空投舱"落在天上"排查（VFXAirdropEffect / AirdropPod）
- 结构事实：舱与内容物的**最终 y 完全由信标 transform 决定** —— `VFXAirdropEffect.cs:537`（`m_creatObject.position = transform.position`）与 `:574`（内容物在 `transform.position+0.2up`）。舱自己算的 `m_LandingHeight` 只是过程量，落地时被这两句覆盖 ⇒ "落在天上" = 信标本身没贴到真地面。
- 信标贴地只有两条射线：`:227`（SetOrigin，`point+10` 向下 200m）与 `:301`（TmpAirdrop，`point+100` 向下 150m）。**失败时静默保留原 point**（空中/墙面/单位身上），无日志无兜底 ⇒ 整发悬空。
- 贴地射线只认 GroundLayers 的**第一个命中面**；而 `Pod.prefab` 的 column_*/Boxc*/bone_root 碰撞体 **layer=3=Ground**（为让子弹能打中）⇒ 永久舱（`ResetLift(9999)`）/上一个舱/悬空载具/货箱/树冠都会被当成"地面"。
- `AirdropPod.Move()` 贴地公式错：`:164-165` 写成 `y = m_LandingHeight + extraHeight`，正确应为 `transform.position.y += extraHeight` ⇒ 多顶"一物理帧下落距离"（200m 自由落体末速 89 m/s × 0.02 = 1.8m；"空投降落速度"×2 ⇒ 3.6m）。
- `AirdropPod.ResolveHit` 的 `:281` `+0.5f` 容差会提前 `Stop()` 且不重定位；若这次 PodHit 因非 Ground 层已早退，则既不出内容物、舱也停在离地 ≤0.5m。
- `Launch()` 的 `:138` 落地射线不排除自身碰撞体（m_IgnoredColliders 收集了但 Raycast 用不上）⇒ Root 在碰撞体上方的 prefab（Artillery Player / OilPlane / Turret_Mission_Machine）会"出生即落地"，全靠信标传送兜底。
- 待拍板：是否加统一 `ResolveAirdropGround()`（排除 AirdropPod 子树 / AirWall，失败记 Warn 并取消该发）+ 修 Move 公式 + 去掉 ResolveHit 的 0.5 容差。
"""

f = io.open(p, 'a', encoding='utf-8')
f.write(add)
f.close()
print('ok')
