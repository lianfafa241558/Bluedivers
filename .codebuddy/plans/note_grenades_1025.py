"""把 2026-10-25 的"手雷全家桶配置"追加到当日 daily。可重跑（幂等靠标题判重）。"""
import io
import os

PATH = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "memory", "2026-10-25.md")
TITLE = "## 手雷全家桶配置"

NOTE = """
## 手雷全家桶配置（2026-10-25 已落地，prefab 全走 MCP 改）
- 新子弹类型 `06Gameplay/Projectile/ProjectileGrenade.cs`（: ProjectileStandard）：只覆写 `_OnShoot/TryHit/Move` ——
  撞到东西**不结算伤害**、按法线反射衰减（BounceDamping 0.5，弹 3 次后 `m_Resting` 趴下不再吃重力），
  爆点交给基类引信 `DelayedRelese(MaxLifeTime)` ⇒ 伤害配置里 `MaxRange` 必须留 -1（否则变成米制自爆引信）。
- 用它的子弹（MCP 手法：**改 m_Script 保留序列化值**，再补新字段默认值）：Flash/Incendiary/Kettle/Smoke/Spray；
  其余（Boomerang/CrystalWheel/GamePad/HolyWater/Mine/Pillow）仍是 ProjectileStandard 碰炸。
- 新组件：
  `Effect/HealOnImpact.cs`（IVfxEffect，按命中碰撞体的 `Actor.Type` 治疗，游戏机）；
  `Effect/HolyWaterEffect.cs`（: SustainedEffect，玩家/盟友 `IHealth.Heal` + 非友军走 `FpsHelper.HitAreaKinetic`）；
  `Interactable/Furniture_GrenadePickup.cs`（: Furniture_Attached + IVfxEffect；`target is TerrainCollider` 才留物，
  只有创建者可捡，捡起 `Ammo.CurrValue += 1`）。
- 新预制体 `Resources/Prefabs/Grenade/`：`Grenade_Boomerang_Attach`（25/0.5s 半径1 Burn+Destruction，LimitedLife 6s）、
  `Grenade_HolyWater_Pool`（Real 8/0.5s + 治疗 12，6s）、`Grenade_GamePad_Heal`（HealOnImpact 40，0.4s）、
  `Grenade_Pillow_Pickup`（BoxCollider + BaseObject + Pickup；用 BaseObject 当 identitySource 免得 Awake 报"缺少身份组件"）。
- 复用现成特效：`Vfx_SmokeIce`（冰）、`Vfx_GroundFlame4`（火，自带 GroundFireEffect 5/0.5s 半径4）、`Vfx_Smoke`、
  `Vfx_SmokeSpore`、`VFX_ AboVertigo`（闪光眩晕）、`Airdrop/Sub/Mine.prefab`（DeployableMine 自带 20/2~4 爆炸，
  所以武器侧伤害归零；`OnlyTerrain=1` 免得地雷被 parent 到敌人身上跟着走）。
- 伤害成分约定：`DamageGroup*` 的 Value 是**总伤害的系数**（`Damageable.InflictDamage` 里 `damage*Value`）；
  冻结/眩晕/恐惧等在 `Health.HandleDamage` 返回 0 伤害、只涨积蓄槽 ⇒ "无护甲破坏" = 组里不写 Destruction。
- ⚠ 项目没有"混乱"这个 `DamageTypeEnum`：Spray 暂按 **Terror(恐惧)** 配（Flash 按用户口径用 Vertigo）；
  要真"混乱"得加 AboState 资产 + 枚举值。
- ⚠ 这 10 把里除 Strafe **都还没进 `Resources/GameData/Role/RD_*.asset` 的投掷槽**（只有 Strafe 被 5 个角色引用）
  ⇒ 想进游戏实测必须先挂 RoleData。
- 工具：新增 `.codebuddy/plans/guid_find.py`（按 guid 反查资产、`--name` 搜 prefab；
  原 `guid_lookup.py` 硬编码 `e:\\Bluedivers`，在本工作区永远查不到）。
- 验证：`offline_compile.py 06_Gameplay` 0 error；Unity 侧编译 0 error；prefab 逐项复核（伤害/半径/成分/AP/子弹/碰撞特效 guid）。
"""

text = io.open(PATH, "r", encoding="utf-8").read()
if TITLE in text:
    print("already noted, skip")
else:
    io.open(PATH, "a", encoding="utf-8").write(NOTE)
    print("appended", len(NOTE))
