# -*- coding: utf-8 -*-
"""追加两块记忆（2026-10-16）：
  1) daily：Healdrone_HaloBomb 子弹预制体丢失 → VFXManager LogError + GetChargeBullet NRE 的定位与硬化
  2) topics/gameplay.md：武器 DamageData 迁移遗漏清单 + ShootFromMuzzle 拦 null 的口径
行尾跟随目标文件（均为 CRLF），先断言锚点存在、幂等（已写过就不再写）。
"""
import io
import os

ROOT = r"E:/Bluedivers"
DAILY = os.path.join(ROOT, r".codebuddy\memory\2026-10-16.md")
TOPIC = os.path.join(ROOT, r".codebuddy\memory\topics\gameplay.md")

DAILY_ANCHOR = "## 卫队改走 HomePoint"
DAILY_NOTE = """## 定位：`IVfxSink.Creat<T> 只支持 ProjectileBase 模板` + `GetChargeBullet` NRE（Healdrone_HaloBomb 没子弹）

- 报错栈：`VFXManager.IVfxSink.Creat<T>`(:31 报错) → `VfxPool.Creat<T>` → `WeaponBaseController.ShootFromMuzzle`(:594)
  → `OnBulletShoot` → `WeaponController.GetChargeBullet`(:485) NRE。**两条是同一个根因**：模板为 null。
- 判据：`VFXManager` 那条 `template is ProjectileBase` 失败只在**模板为空**时才会命中（`DamageData.BulletPrefab` 是
  `ProjectileBase` 类型 ⇒ 类型不符不可能发生）；空模板 ⇒ VfxPool 返回 null ⇒ 订阅方对 null 调 `TryGetComponent` NRE。
- 定位手段（新增，可复用）：`plans/audit_weapon_bulletprefab.py` 扫全工程 118 个武器组件，按 `ShootType` 列出
  `Damages[].BulletPrefab` 为空/列表为空者。★ 踩坑：fileID 可能是**负数**，正则写 `(\\d+)` 会把 `-2543320375086203945`
  漏掉 ⇒ 假阳性 3 个（RoyalRose/SecretarySanction/Healdrone_HaloBomb 里的前两个）。
- 真凶：`Assets/Resources/Prefabs/Airdrop/Healdrone_HaloBomb.prefab`（战备 `ADSO_BD_Bomb.asset` 引用），
  `WeaponEnemyController` ShootType=2(Charge) + `AutomaticReleaseOnCharged=1` + `WeaponFlag=4(AutoDeath)`，
  `Damages[0].BulletPrefab: {fileID: 0}`（其余数值齐全：DamageExplosion 1000 / ExplosionRange 20 / MaxLifeTime 0.1）。
- **回归来源（git 取证）**：`4c3c569f`(07-01) 该武器用的是**武器级** `ProjectilePrefab = Pro_Empty.prefab`
  (guid 3f9f9ac86f4261047b5b1b8ccd1bb5a6, fileID 8924682559891268397)；`00f4c1c1`(09-11) 把子弹迁进 `DamageData` 时
  写成 `- BulletPrefab: {fileID: 0}` 并删掉了旧字段 ⇒ **值就是在这次迁移里丢的**；`fefd7716`(10-02) 代码里
  `//public ProjectileBase ProjectilePrefab;` 被注释掉 ⇒ 连兜底读取都没了。同类丢失：`BattleBase/HealdroneBase`
  （原 `Projectiles/Weapon/Pro_Normal_Blue.prefab`）、`Enemy/Robot/Royal/Droid_RL` 上第二个武器组件 `Damages: []`、
  旧武器 `Weapon_Shotgun/Blaster/Launcher`（`Damages: []` + 只剩废弃 ProjectilePrefab）。
- 代码硬化（已改 2 文件，未动任何资产数据）：
  1. `WeaponBaseController.ShootFromMuzzle`：`bullet == null` 就地 `continue` + **每把武器只报一次错**
     （`_noBulletPrefabWarned`，连发武器否则每发一条）。⚠ 这条兜底不只为"少一发子弹"——异常会穿出
     `LogicFrameHost.TickOnce` 的 `for`，**该帧后面的单位全不 tick，且每帧刷一条异常栈**。
  2. `VFXManager.IVfxSink.Creat<T>`：空模板与"类型不支持"分开报（原来两条都打成
     `只支持 ProjectileBase 模板：{null}`，模板为空时后半截是空的，看不出错因）。
- 验证：`offline_compile.py 00_Core 01_GameContract 05_UnitCore 06_Gameplay 09_Managers 10_UI` 全 0 错误；
  `dll_members.py 06_Gameplay:FPSGame.Weapon.WeaponBaseController` 反射确认 `_noBulletPrefabWarned` /
  `WarnBulletPrefabMissing` 已进 dll；`check_sources.py` 行数/BOM 正常（VFXManager 本来就无 BOM，未动编码）。
- ⏳ **未做（待用户拍板）**：给上面几个预制体把 `Damages[].BulletPrefab` 指回去（HaloBomb → Pro_Empty 即恢复原行为）。
  Unity 端未编译（编辑器失焦，`external_changes_dirty=true`）⇒ 下次聚焦/Ctrl+R 后需核 dll mtime + 反射。

"""

TOPIC_ANCHOR = "## 组件存活 / 回收 / 表现"
TOPIC_NOTE = """## 武器 · 子弹与伤害配置（`DamageData` 迁移遗漏）
- ⚠ **`Damages[UseDamageIndex].BulletPrefab` 为空 = 这发子弹不存在**：`VfxPool.Creat<T>` 返回 null（VFXManager 打"空模板"错），
  旧代码会把 null 交给 `OnBulletShoot`（`GetChargeBullet`/`GetLaserBullet`）⇒ NRE。⚠ 异常穿出 `LogicFrameHost.TickOnce` 的 for ⇒
  **该帧后面所有单位不 tick + 每帧刷异常栈**，不是"少一发子弹"那么简单。现已由 `ShootFromMuzzle` 拦 null（每武器只报一次错）兜底。
- ⚠ 空 `Damages`（列表本身为空）更早就会炸：`CurrentDamgeData => Damages[i]` 直接 ArgumentOutOfRange，`CurrentSpeed/CurrentGravity` 同源。
- 已知漏配（`plans/audit_weapon_bulletprefab.py` 可重跑全量）：`Airdrop/Healdrone_HaloBomb`（ShootType=Charge，
  原值 `Projectiles/Airdrop/Pro_Empty.prefab`）、`BattleBase/HealdroneBase`（原 `Projectiles/Weapon/Pro_Normal_Blue.prefab`）、
  `Enemy/Robot/Royal/Droid_RL` 第二个武器组件（`Damages: []`）、旧武器 `Weapon_Shotgun/Blaster/Launcher`（`Damages: []`）。
- 根因是 2026-09-11 把**武器级** `ProjectilePrefab` 迁进 `DamageData.BulletPrefab` 时漏迁（值丢），
  2026-10-02 代码里武器级字段又整体注释掉 ⇒ 连兜底都没了。查这类"字段搬家"必须比对迁移前那一版的 prefab YAML。

"""


def append(path, anchor, note):
    raw = io.open(path, "rb").read()
    nl = "\r\n" if raw.count(b"\r\n") > 10 else "\n"
    text = raw.decode("utf-8")
    head = note.strip().splitlines()[0]
    if head in text:
        print("SKIP(已存在)", path, "->", head)
        return
    assert anchor in text, "锚点未找到: %s @ %s" % (anchor, path)
    body = nl.join(note.splitlines()) + nl
    io.open(path, "ab").write(body.encode("utf-8"))
    print("APPEND", path, len(note), "chars, nl=%r" % nl)


append(DAILY, DAILY_ANCHOR, DAILY_NOTE)
append(TOPIC, TOPIC_ANCHOR, TOPIC_NOTE)
