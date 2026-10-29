# -*- coding: utf-8 -*-
"""LightSword（光之剑）模组补齐 —— 对标 Shard Diffractor 的六个超频（**只做模组**）。

LightSword 的升级树已经是对着 Shard Diffractor 的 mods 铺的（ImpactSplash / SoftTissue…），
这里只补它的 overclocks 那一组：

| DRG 超频 | 本工程模组 | 落点 |
|---|---|---|
| Efficiency Tweaks | WMD_EfficiencyTweaks（**已存在，不复写**） | Ammo +50 / Magazine +25 |
| Automated Beam Controller | WMD_AutoBeamController | Ammo +100、放电更快、电容 -20% |
| （充能节奏类） | WMD_FastCharge | AutoReloadSpeed +60%、AutoReloadTime Extra -1s |
| Feedback Loop | WMD_FeedbackLoop | 范围伤害/半径 +40%、Ammo -33% |
| Overdrive Booster | WMD_OverdriveBooster | 直伤/范围伤害 +80%、耗电暴增、电容 -35% |
| Volatile Impact Reactor | WMD_VolatileReactor | 半径 +70%、范围伤害 +50%、弱点 +0.5、直伤 -40% |

⚠ 只碰 `Modules` / `ActiveModule`；`Upgrade` / `showAttr` / `m_UniqueAttr` 一律不动。
⚠ 发光武器是 `AutomaticReload | NoManualReload`：`ReloadTime` 没有消费端，充能只能走
   `AutoReloadSpeed`(All) 或 `AutoReloadTime`(**只吃 Extra**)。

跑法：
    python .codebuddy/plans/gen_lightsword_modules.py --dry-run
    python .codebuddy/plans/gen_lightsword_modules.py
    python .codebuddy/plans/gen_lightsword_modules.py --verify
"""
import os
import re
import shutil
import sys

PLANS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(PLANS, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, ".codebuddy", "skills", "weapon-upgrade", "scripts"))

for _s in (sys.stdout, sys.stderr):
    try:
        _s.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

import attr_whitelist                      # noqa: E402
import gen_weapon_tree as gen              # noqa: E402

PREFAB = "Assets/Resources/Weapons/LightSword.prefab"
WMD_DIR = "Assets/Resources/GameData/WeaponModule/LightSword"
SALT = "Bluedivers.LightSword"
WMD_NONE = "a19bae1a0785866409ae31f377bde9cf"
EXISTING = [("EfficiencyTweaks", "bf45066281c172e44ac8362305631285")]   # 已在用，保留原位
MAX_DESC = 4                   # SelectRoleWnd.moduleLayout 子项数

ICON = {
    "acc": "0eac3611eea675245987e6db3bf8eb3b",
    "clip": "7c07f1f9a69294949ae48e2c9a29eafb",
    "ammo": "999805b81db170343924d256a6aa3a5b",
    "dmg": "c01ee1f9f5e5aeb41b9508f38a67cdeb",
    "dmgG": "f7729a4d515396f4aa47fb90ae5ad11a",
    "rate": "5fbbe63e1c448df459304aca3025ab19",
    "reload": "963e16d12a501264e8c8bd6de0e42d95",
    "ap": "10d0a2555de979341839503f09b9aeba",
    "area": "cea3b2c8cfc35284082f183ff6b57509",
    "explosion": "4338eea68fc4aa545ba7a2fa867208e6",
    "weak": "c9a8c0b4e81364041bd422336e4b5f38",
    "capacity": "589a9beb3d7609044b30506b277e51cb",
    "special": "678d408e5f4e687488611606db9f06d9",
    "cooldown": "e51d8bd68db57a94781c1fdd3ad54982",
}


def m(attr, modifier, value):
    return dict(attr=attr, modifier=modifier, value=value)


MODULES = [
    dict(asset="AutoBeamController", name="自动光束控制器", quality="Balanced", icon="capacity",
         describe="装了一套会自动调节放电节奏的控制器。省心，代价是对电容的要求更高。",
         desc=[(True, "电池总容量大幅提升"), (True, "放电速度提升"), (False, "电容容量减少")],
         modifys=[m("Ammo", "Base", 100), m("ShootInterval", "Factor", -0.15),
                  m("Magazine", "Factor", -0.2)]),
    dict(asset="FastCharge", name="快充电容", quality="Clean", icon="cooldown",
         describe="换上快充电路，打完一轮几乎不用等，电容自己就满了。",
         desc=[(True, "电容充电速度大幅提升"), (True, "充能延迟缩短")],
         modifys=[m("AutoReloadSpeed", "Factor", 0.6), m("AutoReloadTime", "Extra", -1)]),
    dict(asset="FeedbackLoop", name="反馈回路", quality="Balanced", icon="explosion",
         describe="把泄放出来的能量绕回主回路，越打越放得开，电池也见底得更快。",
         desc=[(True, "范围伤害显著提升"), (True, "作用半径扩大"), (False, "电池总容量大幅下降")],
         modifys=[m("ExplosionDamage", "Factor", 0.4), m("ExplosionRange", "Factor", 0.4),
                  m("Ammo", "Factor", -0.33)]),
    dict(asset="OverdriveBooster", name="过载增幅器", quality="Unstable", icon="special",
         describe="整套输出被推到远超额定功率，代价是电容一瞬间就见底。",
         desc=[(True, "直击伤害极大提升"), (True, "范围伤害极大提升"),
               (False, "每秒耗电量大幅增加"), (False, "电容容量显著减少")],
         modifys=[m("DirectDamage", "Factor", 0.8), m("ExplosionDamage", "Factor", 0.8),
                  m("ShootInterval", "Factor", -0.3), m("Magazine", "Factor", -0.35)]),
    dict(asset="VolatileReactor", name="挥发性撞击反应堆", quality="Unstable", icon="area",
         describe="撞击点会炸开一团挥发性等离子，范围大得离谱，直击反而变钝了。",
         desc=[(True, "作用半径极大扩大"), (True, "范围伤害大幅提升"),
               (True, "弱点伤害提升"), (False, "直击伤害大幅下降")],
         modifys=[m("ExplosionRange", "Factor", 0.7), m("ExplosionDamage", "Factor", 0.5),
                  m("WeaknessBonus", "Extra", 0.5), m("DirectDamage", "Factor", -0.4)]),
]


# ---------------------------------------------------------------- 渲染 / 校验

def render_wmd(md, info):
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % gen.WMD_SCRIPT,
        "  m_Name: WMD_%s" % md["asset"],
        "  m_EditorClassIdentifier: ",
        "  name: %s" % gen.q(md["name"]),
        "  describe: %s" % gen.q(md["describe"]),
        "  type: %d" % gen.QUALITY[md["quality"]],
        "  icon: {fileID: 21300000, guid: %s, type: 3}" % md["icon"],
        "  desc:",
    ]
    for good, text in md["desc"]:
        lines.append("  - Key: %d" % (1 if good else 0))
        lines.append("    Value: %s" % gen.q(text))
    lines.append("  modifys:")
    for mod in md["modifys"]:
        lines.append("  - name: ")
        lines.append("    type: %d" % info.num(mod["attr"]))
        lines.append("    modifier: %d" % attr_whitelist.MOD_NAMES[mod["modifier"]])
        lines.append("    value: %s" % gen.num_text(mod["value"]))
    return "\n".join(lines) + "\n"


def validate(info):
    errors = []
    rev = {v: k for k, v in info.enum_num.items()}
    cfg_names = {rev.get(n, str(n))
                 for n in attr_whitelist.parse_prefab_cfg(os.path.join(ROOT, PREFAB))}
    for md in MODULES:
        md["icon"] = ICON[md["icon"]]
        for mod in md["modifys"]:
            ok, why = info.check(mod["attr"], mod["modifier"])
            if not ok:
                errors.append("%s: %s" % (md["asset"], why))
                continue
            ok, why = info.has_base(mod["attr"], cfg_names)
            if not ok:
                errors.append("%s: %s" % (md["asset"], why))
        if len(md["desc"]) > MAX_DESC:
            errors.append("%s: desc %d 行 > UI 槽位 %d" % (md["asset"], len(md["desc"]), MAX_DESC))
        goods = sum(1 for g, _ in md["desc"] if g)
        if md["quality"] == "Clean" and goods != len(md["desc"]):
            errors.append("%s 是 Clean 却写代价文案" % md["asset"])
        if md["quality"] != "Clean" and goods == len(md["desc"]):
            errors.append("%s 是 %s 却只有增益文案" % (md["quality"], md["asset"]))
        for _, text in md["desc"]:
            if re.search(r"\d", text):
                errors.append("%s: 模组文案出现数字「%s」" % (md["asset"], text))
    return errors


def existing_guids():
    used = {}
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, f[:-5]), ROOT).replace("\\", "/")
            try:
                with open(os.path.join(dirpath, f), encoding="utf-8", errors="replace") as fh:
                    for line in fh:
                        if line.startswith("guid: "):
                            used[line.strip()[6:]] = rel
                            break
            except OSError:
                pass
    return used


# ---------------------------------------------------------------- prefab

def patch_prefab(guids):
    path = os.path.join(ROOT, PREFAB)
    with open(path, encoding="utf-8", newline="") as f:
        src = f.read()
    nl = "\r\n" if "\r\n" in src else "\n"
    text = src.replace("\r\n", "\n")
    old = text
    errors = []

    lines = ["  Modules:", "  - {fileID: 11400000, guid: %s, type: 2}" % WMD_NONE]
    lines += ["  - {fileID: 11400000, guid: %s, type: 2}" % g for _, g in EXISTING]
    lines += ["  - {fileID: 11400000, guid: %s, type: 2}" % guids[md["asset"]] for md in MODULES]
    text, n = re.subn(r"(?m)^  Modules:(?: \[\]|\n(?:  - [^\n]*\n)+)",
                      lambda mm: "\n".join(lines) + ("\n" if mm.group(0).endswith("\n") else ""), text)
    if n != 1:
        errors.append("Modules 块命中 %d 次" % n)

    # ActiveModule 原来是 {fileID: 0}（null）⇒ 编辑器里"当前模组"没有指向，改成 WMD_None
    text, n = re.subn(r"(?m)^  ActiveModule: [^\n]*$",
                      "  ActiveModule: {fileID: 11400000, guid: %s, type: 2}" % WMD_NONE, text)
    if n != 1:
        errors.append("ActiveModule 命中 %d 次" % n)

    return dict(path=path, new=text, nl=nl, changed=text != old, errors=errors)


def verify():
    with open(os.path.join(ROOT, PREFAB), encoding="utf-8", newline="") as f:
        src = f.read()
    ok = True
    body = re.search(r"(?ms)^  Modules:\n((?:  - [^\n]*\n)*)", src).group(1)
    gs = re.findall(r"guid: ([0-9a-f]{32})", body)
    print("  Modules: %d 项（含 None）" % len(gs))
    if len(gs) != 1 + len(EXISTING) + len(MODULES):
        print("  [FAIL] Modules 数量不对")
        ok = False
    if gs[:1] != [WMD_NONE]:
        print("  [FAIL] Modules[0] 必须是 WMD_None")
        ok = False
    if gs[1:1 + len(EXISTING)] != [g for _, g in EXISTING]:
        print("  [FAIL] 原有模组顺序被打乱")
        ok = False
    am = re.search(r"(?m)^  ActiveModule: .*guid: ([0-9a-f]{32})", src)
    print("  ActiveModule=%s" % (am.group(1) if am else "null/0"))
    if not am or am.group(1) != WMD_NONE:
        print("  [FAIL] ActiveModule 应为 WMD_None")
        ok = False
    for g in set(gs):
        if not _guid_exists(g):
            print("  [FAIL] guid 找不到资产: %s" % g)
            ok = False
    print("VERIFY:", "OK" if ok else "FAILED")
    return ok


def _guid_exists(guid):
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                continue
            try:
                with open(os.path.join(dirpath, f), encoding="utf-8", errors="replace") as fh:
                    if ("guid: %s" % guid) in fh.read(400):
                        return True
            except OSError:
                pass
    return False


def main():
    dry = "--dry-run" in sys.argv
    info = attr_whitelist.load(ROOT)
    if "--verify" in sys.argv:
        return 0 if verify() else 1

    errors = validate(info)
    if errors:
        for e in errors:
            print("  [ERROR]", e)
        print("RESULT: FAILED（未写入）")
        return 1

    used = existing_guids()
    files, guids = {}, {}
    for md in MODULES:
        owner = "WMD_" + md["asset"]
        g = gen.guid_of(SALT, owner)
        p = "%s/WMD_%s.asset" % (WMD_DIR, md["asset"])
        owner_now = used.get(g)
        if owner_now is not None and owner_now != p:
            print("  [ERROR] guid 撞车:", p, "已被", owner_now, "占用")
            return 1
        guids[md["asset"]] = g
        files[p] = render_wmd(md, info)
        files[p + ".meta"] = gen.render_meta(g)

    for rel, text in sorted(files.items()):
        p = os.path.join(ROOT, rel)
        cur = None
        if os.path.exists(p):
            with open(p, encoding="utf-8", newline="") as f:
                cur = f.read()
        if cur == text:
            continue
        print("  [%s] %s" % ("DIFF" if dry else "WRITE", rel))
        if not dry:
            os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)

    pf = patch_prefab(guids)
    if pf["errors"]:
        for e in pf["errors"]:
            print("  [ERROR] prefab:", e)
        return 1
    if pf["changed"]:
        print("  [%s] %s" % ("DIFF" if dry else "WRITE", PREFAB))
        if not dry:
            shutil.copyfile(pf["path"], pf["path"] + ".bak")
            with open(pf["path"], "w", encoding="utf-8", newline="") as f:
                f.write(pf["new"].replace("\n", pf["nl"]))
    else:
        print("  [SAME] prefab 无需改动")

    print("RESULT:", "OK", "| modules=%d（+ 已有 %d + None）" % (len(MODULES), len(EXISTING)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
