# -*- coding: utf-8 -*-
"""给 Rationality.prefab 挂 7 个模组（不含 MoveSpeedToShoot）。

刻意只改两处：`Modules`（空 → WMD_None + 7 个）与 `ActiveModule`。
**不碰 Upgrade / showAttr / cfg** —— 那棵树是用户手配的，不能被 spec 覆盖。

复用 skill 的渲染与校验（gen_weapon_tree.render_wmd / attr_whitelist），
但自己走一遍 WMD 校验（模组允许正负成对，不走"只强化"那条）。
"""
import io
import os
import shutil
import sys

ROOT = r"d:/Pro/Bluedivers"
SCRIPTS = os.path.join(ROOT, ".codebuddy/skills/weapon-upgrade/scripts")
sys.path.insert(0, SCRIPTS)
import gen_weapon_tree as g          # noqa: E402
import attr_whitelist                # noqa: E402

for _s in (sys.stdout, sys.stderr):
    try:
        _s.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

PREFAB = os.path.join(ROOT, "Assets/Resources/Weapons/Rationality.prefab")
OUT_DIR = os.path.join(ROOT, "Assets/Resources/GameData/WeaponModule/Rationality")
SALT = "Bluedivers.RationalityModules"
BACKUP_DIR = os.path.join(ROOT, ".codebuddy/plans/backup")

ICONS = {
    "DMG": "1b3193f68ca866943bada632271653b6",
    "AMMO": "09b2164270d5192469590d258cf8ff79",
    "RATE": "da2ddc2b67324234daa8bdf435957132",
    "ACC": "d31f7dc298d350a4395888c6839bf608",
}

# (asset, 中文名, 品质, icon, [(增益?, 文案)], [(attr, modifier, value)])
MODULES = [
    ("SteadyAim", "稳态瞄准", "Clean", "ACC",
     [(True, "散布大幅降低"), (True, "装填略微加快")],
     [("BulletsSpreadAngle", "Factor", -0.30), ("ReloadTime", "Base", -0.35)]),

    ("PenetratorLoad", "穿甲装填", "Balanced", "DMG",
     [(True, "穿甲等级明显提升"), (False, "总备弹量减少")],
     [("DirectAP", "Base", 2), ("Ammo", "Factor", -0.20)]),

    ("OverclockedChamber", "超频弹膛", "Balanced", "RATE",
     [(True, "射速显著提升"), (False, "装填明显变慢")],
     [("ShootInterval", "Factor", -0.18), ("ReloadTime", "Base", 0.6)]),

    ("ArmorPuncher", "破甲弹头", "Balanced", "DMG",
     [(True, "护甲破坏大幅提升"), (False, "直击伤害略微降低")],
     [("DirectDestruction", "Base", 1.5), ("DirectDamage", "Factor", -0.12)]),

    ("HairTrigger", "轻扳机", "Unstable", "RATE",
     [(True, "射速大幅提升"), (False, "弹匣容量明显减少"), (False, "散布显著变差")],
     [("ShootInterval", "Factor", -0.35), ("Magazine", "Base", -12),
      ("BulletsSpreadAngle", "Factor", 0.6)]),

    ("HeavyPayload", "重型弹芯", "Unstable", "DMG",
     [(True, "直击伤害大幅提升"), (False, "射速显著降低"), (False, "总备弹量减少")],
     [("DirectDamage", "Base", 6), ("ShootInterval", "Factor", 0.25),
      ("Ammo", "Factor", -0.15)]),

    ("FullFeed", "全自动供弹", "Unstable", "AMMO",
     [(True, "总备弹量相当多地增加"), (False, "弹匣容量大幅减少"), (False, "装填明显变慢")],
     [("Ammo", "Factor", 0.6), ("Magazine", "Factor", -0.5), ("ReloadTime", "Base", 0.5)]),
]


def main():
    info = attr_whitelist.load(ROOT)
    cfg_nums = attr_whitelist.parse_prefab_cfg(PREFAB)
    rev = {v: k for k, v in info.enum_num.items()}
    cfg_names = {rev.get(n, str(n)) for n in cfg_nums}

    errors = []
    for asset, name, quality, icon, desc, mods in MODULES:
        if "MoveSpeedToShoot" in [m[0] for m in mods]:
            errors.append(asset + ": 这套模组不允许涉及 MoveSpeedToShoot")
        for attr, modifier, value in mods:
            ok, why = info.check(attr, modifier)
            if not ok:
                errors.append("%s: %s" % (asset, why))
            ok, why = info.has_base(attr, cfg_names)
            if not ok:
                errors.append("%s: %s" % (asset, why))
    if errors:
        for e in errors:
            print("  [ERROR]", e)
        print("RESULT: FAILED（未写入）")
        return 1

    os.makedirs(OUT_DIR, exist_ok=True)
    guids = {}
    for asset, name, quality, icon, desc, mods in MODULES:
        mods_obj = [{"attr": a, "modifier": m, "value": v} for a, m, v in mods]
        text = g.render_wmd(asset, name, quality, ICONS[icon],
                            [{"good": ok, "text": t} for ok, t in desc], mods_obj, info)
        guid = g.guid_of(SALT, "WMD_" + asset)
        guids[asset] = guid
        p = os.path.join(OUT_DIR, "WMD_%s.asset" % asset)
        with io.open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        with io.open(p + ".meta", "w", encoding="utf-8", newline="\n") as f:
            f.write(g.render_meta(guid))

    # ---- 只改 Modules / ActiveModule ----
    with io.open(PREFAB, encoding="utf-8", newline="") as f:
        src = f.read()
    nl = "\r\n" if "\r\n" in src else "\n"
    text = src.replace("\r\n", "\n")
    old = text

    lines = ["  Modules:", "  - {fileID: 11400000, guid: %s, type: 2}" % g.WMD_NONE_GUID]
    for asset, _, _, _, _, _ in MODULES:
        lines.append("  - {fileID: 11400000, guid: %s, type: 2}" % guids[asset])

    def repl(m):
        return "\n".join(lines) + ("\n" if m.group(0).endswith("\n") else "")

    text, n = g.re.subn(r"(?m)^  Modules:(?: \[\]|\n(?:  - [^\n]*\n)+)", repl, text)
    assert n == 1, "Modules 块命中 %d 次" % n
    text, n = g.re.subn(r"(?m)^  ActiveModule: [^\n]*$",
                        "  ActiveModule: {fileID: 11400000, guid: %s, type: 2}" % g.WMD_NONE_GUID,
                        text)
    assert n == 1, "ActiveModule 命中 %d 次" % n

    if text != old:
        os.makedirs(BACKUP_DIR, exist_ok=True)
        shutil.copyfile(PREFAB, os.path.join(BACKUP_DIR, "Rationality.prefab.bak"))
        with io.open(PREFAB, "w", encoding="utf-8", newline="") as f:
            f.write(text.replace("\n", nl))
        print("prefab 已挂模组（备份 .codebuddy/plans/backup/Rationality.prefab.bak）")
    else:
        print("prefab 无变化（可能已挂过）")

    print("生成 %d 个模组到 %s" % (len(MODULES), os.path.relpath(OUT_DIR, ROOT)))
    print("RESULT: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
