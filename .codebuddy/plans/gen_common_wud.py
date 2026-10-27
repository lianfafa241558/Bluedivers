# -*- coding: utf-8 -*-
"""往 Assets/Resources/GameData/WeaponUpgrade/ 根目录补"多武器共用"的通用升级项。

只新增、**绝不覆盖**已存在的 WUD_*.asset（根目录那批是策划手调的）。
复用 skill `weapon-upgrade/scripts/gen_weapon_tree.py` 的 YAML 渲染与 guid 派生，
属性编号/修饰符白名单由 `attr_whitelist` 从 C# 源码解析，不维护第二份表。

用法：python .codebuddy/plans/gen_common_wud.py [--dry-run]
"""
import os
import sys

PLANS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(PLANS, "..", ".."))
SKILL_SCRIPTS = os.path.join(ROOT, ".codebuddy", "skills", "weapon-upgrade", "scripts")
sys.path.insert(0, SKILL_SCRIPTS)

for _s in (sys.stdout, sys.stderr):
    try:
        _s.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

import attr_whitelist                      # noqa: E402
import gen_weapon_tree as gen              # noqa: E402

WUD_DIR = "Assets/Resources/GameData/WeaponUpgrade"
GUID_SALT = "common"

# 图标 guid（与根目录现有通用项同一套 SkillIcon，保持视觉一致）
ICON_AMMO = "09b2164270d5192469590d258cf8ff79"       # COMMON_SKILLICON_ReserveAmmo
ICON_SPREAD = "da2ddc2b67324234daa8bdf435957132"     # COMMON_SKILLICON_FAN
ICON_BULLET = "1b3193f68ca866943bada632271653b6"     # COMMON_SKILLICON_BULLETTYPEBUFF
ICON_RADIUS = "d31f7dc298d350a4395888c6839bf608"     # COMMON_SKILLICON_CIRCLERADIUSINCREASE

# 只挑"所有枪的 cfg 都有基础值或 InitAttribute 会自动补"的属性：
# Ammo/Magazine/ShootInterval/ReloadTime 全武器都有；BulletSpeed/ExplosionDamage/ExplosionRange 由 Damages 补。
ITEMS = [
    dict(asset="Magazine", name="扩容弹匣", category="弹匣容量", icon=ICON_AMMO, cost=1000,
         desc="弹匣里多塞了两发。别问塞不塞得进去，工程部说能，那就是能。",
         modifys=[dict(attr="Magazine", modifier="Base", value=2)]),
    dict(asset="Accuracy", name="精密校准", category="散布", icon=ICON_SPREAD, cost=1000,
         desc="枪管重新校过，弹道收得更紧。代价是工程部占用了你下一次的保养时间，反正你也不保养。",
         modifys=[dict(attr="BulletsSpreadAngle", modifier="Factor", value=-0.25)]),
    dict(asset="BulletSpeed", name="高速弹头", category="弹速", icon=ICON_BULLET, cost=1000,
         desc="换了更猛的发射药，子弹飞得更快。提前量不用算了，看见就扣扳机。",
         modifys=[dict(attr="BulletSpeed", modifier="Factor", value=0.3)]),
    dict(asset="ExplosionDamage", name="高爆装药", category="爆炸伤害", icon=ICON_BULLET, cost=1000,
         desc="往装药里多添了两勺。爆起来更狠，至于添的是什么，安全手册上没写。",
         modifys=[dict(attr="ExplosionDamage", modifier="Factor", value=0.3)]),
    dict(asset="ExplosionRange", name="扩爆装药", category="爆炸范围", icon=ICON_RADIUS, cost=1000,
         desc="同样的装药摊得更开。站远点，这玩意儿现在连隔壁的隔壁都能照顾到。",
         modifys=[dict(attr="ExplosionRange", modifier="Factor", value=0.25)]),
]


def existing_guids():
    """收集工程内所有 .meta 的 guid，防止派生值撞车。"""
    used = set()
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                continue
            try:
                with open(os.path.join(dirpath, f), encoding="utf-8", errors="replace") as fh:
                    for line in fh:
                        if line.startswith("guid: "):
                            used.add(line.strip()[6:])
                            break
            except OSError:
                pass
    return used


def main():
    dry = "--dry-run" in sys.argv
    info = attr_whitelist.load(ROOT)
    used = existing_guids()
    written = skipped = 0

    for it in ITEMS:
        owner = "WUD_" + it["asset"]
        for m in it["modifys"]:
            ok, why = info.check(m["attr"], m["modifier"])
            if not ok:
                print("  [ERROR] %s: %s" % (owner, why))
                return 1

        path = os.path.join(ROOT, WUD_DIR, owner + ".asset")
        if os.path.exists(path):
            print("  [SKIP] 已存在，保持手调内容不动:", WUD_DIR + "/" + owner + ".asset")
            skipped += 1
            continue

        guid = gen.guid_of(GUID_SALT, owner)
        if guid in used:
            print("  [ERROR] guid 撞车:", guid)
            return 1
        used.add(guid)

        text = gen.render_wud(it["asset"], it["name"], it["category"], it["icon"],
                              it["desc"], it["cost"], it["modifys"], info)
        # 与 Unity 自己写出的 meta 对齐（fileFormatVersion 后空一行），免得每次导入多出一笔纯格式 diff
        meta = gen.render_meta(guid).replace("fileFormatVersion: 2\n", "fileFormatVersion: 2\n\n", 1)
        print("  [%s] %s/WUD_%s.asset" % ("DIFF" if dry else "WRITE", WUD_DIR, it["asset"]))
        if dry:
            written += 1
            continue
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        with open(path + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(meta)
        written += 1

    print("新增 %d / 跳过 %d；总量 %d" % (written, skipped, len(ITEMS)))
    print("RESULT: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
