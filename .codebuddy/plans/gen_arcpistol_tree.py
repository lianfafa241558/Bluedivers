# -*- coding: utf-8 -*-
"""ArcPistol（星辰脉冲）v2：真正的热量武器 + 重建升级树 / 模组。

v2 与 v1 的差别（2026-10-28 用户定）：
  * 原来是「小弹匣 + 自动换弹」假装散热 ⇒ 现在走真热量：HeatPerShot 打满 100 就过热锁定，
    CoolDelay / CoolSpeed / OverheatDuration 才是散热手感。
  * **无后备弹量**：`Ammo = 0`，全部发数进 `Magazine`（= 能量池），热量决定"一轮能连射几发"。
  * `ShootType = Automatic`（按住才能把 6 发打进同一个热量循环）⇒ 原来的「全自动模式」Special 退役。
  * `WeaponFlag = 2`（只留禁止手动换弹）：没有自动换弹了，避免 AutoReloadTime 那个幽灵属性。
  * 通用池里 `WUD_AmmoBag`(按 Ammo 比例) / `WUD_Magazine`(+2，池子 128 里没意义) 对这把枪失效，不再挂。

跑法：
    python .codebuddy/plans/gen_arcpistol_tree.py --dry-run
    python .codebuddy/plans/gen_arcpistol_tree.py
    python .codebuddy/plans/gen_arcpistol_tree.py --verify
"""
import os
import re
import shutil
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

PREFAB = "Assets/Resources/Weapons/ArcPistol.prefab"
WUD_DIR = "Assets/Resources/GameData/WeaponUpgrade/ArcPistol"
WMD_DIR = "Assets/Resources/GameData/WeaponModule/ArcPistol"
SALT = "Bluedivers.ArcPistol"
WMD_NONE = "a19bae1a0785866409ae31f377bde9cf"

# 根目录通用池（多枪适用只留这一份）
COMMON = {
    "Accuracy": "b9d3f676c862ea0dc18bd826f77163ea",        # 散布 Factor -0.25
    "Damage": "0a01454bb348bcd40a0e10403c33c443",          # 直伤 Factor +0.3
    "Rate": "02dbc072bbc16d341beb7a3f1dd38485",            # 射击间隔 Factor -0.15
    "HardenedRounds": "a272edbbe7721e9aa8f00416d53ffcf2",  # 穿甲 Base +1
}

# 别的武器目录下、但本枪已经在用的资产（用户手挂过来的，沿用，别复制一份）
CROSS = {
    "FloatingBarrel": "2bd5e64115d6bd79ea00052c392b4204",  # Logic/WUD_FloatingBarrel：后坐力 Factor -0.3
}

# 图标（SkillIcon/DRG）
ICON_ACC = "0eac3611eea675245987e6db3bf8eb3b"
ICON_DMG = "c01ee1f9f5e5aeb41b9508f38a67cdeb"
ICON_WEAK = "c9a8c0b4e81364041bd422336e4b5f38"
ICON_RATE = "5fbbe63e1c448df459304aca3025ab19"
ICON_STABLE = "fea1eaa19c9a8ba4eba678fcf7750a47"
ICON_TEMP = "cea3b2c8cfc35284082f183ff6b57509"      # 温度
ICON_COOLDOWN = "e51d8bd68db57a94781c1fdd3ad54982"  # 温度下降
ICON_COOLING = "90aba9845b3cfc745ba83a15f365684d"   # 冷却
ICON_HEAT = "ded265a46563c73459ef9d84e7f49c96"      # 热量
ICON_SLOW = "2fd8b8d4c1cbf1246b09863d8ee99f8f"      # 温度骤变
ICON_CAPACITY = "589a9beb3d7609044b30506b277e51cb"
ICON_PEN = "efb01279ea57e4a4bbfb53ba2a0e2251"
ICON_DMGGEN = "f7729a4d515396f4aa47fb90ae5ad11a"

# 全量 cfg（顺序即序列化顺序）。100 热量 / 16.67 每发 = 6 发满热。
CFG = [
    ("Ammo", 0),               # 无后备弹量
    ("Magazine", 128),         # 全部发数进弹匣（能量池）
    ("ShootInterval", 0.1),
    ("BulletsSpreadAngle", 1),
    ("HeatPerShot", 16.67),
    ("CoolDelay", 0.35),
    ("CoolSpeed", 45),
    ("OverheatDuration", 2.2),
    ("Recoil", 1),
    ("WeaknessBonus", 0),
    ("NoiseScale", 1),
]

SHOOT_TYPE = 1     # Automatic：按住才能把 6 发打进同一轮热量
WEAPON_FLAG = 2    # NoManualReload（第 1 位）；不再有自动换弹

SHOW_ATTR = ["DirectDamage", "ShootInterval", "Magazine",
             "HeatPerShot", "CoolDelay", "CoolSpeed", "OverheatDuration",
             "DirectAP", "BulletsSpreadAngle", "WeaknessBonus", "Recoil", "NoiseScale"]

# 退役资产：v1 靠「自动换弹」的项，v2 已无对应属性，留着只会是死卡片
RETIRED = ["FanTheHammer", "QuickfireEjector", "FastFeed"]


def m(attr, modifier, value):
    return dict(attr=attr, modifier=modifier, value=value)


TIERS = [
    {"key": 2, "items": [
        dict(common="Accuracy"),
        dict(asset="HeatSink", name="散热鳍片", category="散热", icon=ICON_COOLING, cost=1000,
             desc="枪身两侧加了一排散热鳍片，打空之后凉得更快。"
                  "缺点是这排东西烫得能煎蛋，握枪的时候请自觉避开。",
             modifys=[m("CoolSpeed", "Factor", 0.25)]),
        dict(asset="LowResistanceCoil", name="低阻线圈", category="散热", icon=ICON_COOLDOWN, cost=1000,
             desc="线圈换成低阻材质，停火之后热量几乎立刻开始往外走。"
                  "工程部管这叫阻抗优化，你只需要知道它凉得更早。",
             modifys=[m("CoolDelay", "Factor", -0.3)]),
    ]},
    {"key": 6, "items": [
        dict(common="Damage"),
        dict(asset="HighCapacityCore", name="高热容核心", category="热量", icon=ICON_HEAT, cost=1500,
             desc="核心塞进更多储热材料，同样的热量摊到更多发弹上，满热之前能多扣几次扳机。"
                  "材料哪来的别问，问就是回收。",
             modifys=[m("HeatPerShot", "Factor", -0.18)]),
        dict(cross="FloatingBarrel"),
    ]},
    {"key": 10, "items": [
        dict(common="HardenedRounds"),
        dict(asset="HollowPointBullets", name="空尖弹", category="弱点", icon=ICON_WEAK, cost=2000,
             desc="弹头命中瞬间展开，专挑敌人的软肋下手，打中要害的收益再上一档。"
                  "至于展开之后长什么样，战地报告里没配图。",
             modifys=[m("WeaknessBonus", "Extra", 0.35)]),
        dict(asset="OverheatVent", name="过载泄压阀", category="过热", icon=ICON_TEMP, cost=2000,
             desc="过热之后泄压阀把残余热量一口气排掉，罚站时间短了一大截。"
                  "排出来那股白烟有毒没毒，报告里没写。",
             modifys=[m("OverheatDuration", "Factor", -0.3)]),
    ]},
    {"key": 14, "items": [
        dict(asset="HeavySlug", name="重弹头", category="伤害", icon=ICON_DMG, cost=2500,
             desc="弹头换成更重的一档，单发伤害扎实地往上抬。"
                  "弹道会往下沉，反正这把枪本来也不打算打那么远。",
             modifys=[m("DirectDamage", "Base", 12)]),
        dict(common="Rate"),
        dict(asset="SuppressorLining", name="抑制衬套", category="噪声", icon=ICON_STABLE, cost=2500,
             desc="枪膛里加了一圈抑制衬套，开枪的动静小了很多。"
                  "工程部坚持这不叫消音器，叫噪声治理，区别全在报销单怎么写。",
             modifys=[m("NoiseScale", "Factor", -0.4)]),
    ]},
    {"key": 18, "items": [
        dict(asset="CriticalMass", name="临界质量", category="热量", icon=ICON_SLOW, cost=3000,
             desc="核心压到临界点上，热量摊得更薄，一轮能多扣两发扳机。"
                  "安全裕度那东西本来就只是写给报告看的。",
             modifys=[m("HeatPerShot", "Factor", -0.25)]),
        dict(asset="ThermalReclaimer", name="余热回收", category="散热", icon=ICON_COOLING, cost=3000,
             desc="每发打完的余热被导回电容，散热速度直接翻上去。"
                  "工程部管这叫能量闭环，其实就是把原本浪费掉的东西捡回来。",
             modifys=[m("CoolSpeed", "Factor", 0.5)]),
        dict(asset="OverpressureCharge", name="超压装药", category="伤害", icon=ICON_DMGGEN, cost=3000,
             desc="装药压到了验收标准之上，每一发都更疼。"
                  "核心寿命？工程部已经给你备好了一颗新的，所以这不是问题。",
             modifys=[m("DirectDamage", "Factor", 0.2)]),
    ]},
]

# 资产名沿用 v1（guid / Modules 下标不变，老存档的 selectModuleIndex 仍然指得到东西）
MODULES = [
    dict(asset="QuickdrawCylinder", name="快拆散热组件", quality="Clean", icon=ICON_COOLING,
         desc=[(True, "散热速度大幅提升"), (True, "停火后更快开始散热")],
         modifys=[m("CoolSpeed", "Factor", 0.4), m("CoolDelay", "Factor", -0.15)]),
    dict(asset="MatchTrigger", name="竞赛级扳机", quality="Clean", icon=ICON_ACC,
         desc=[(True, "散布显著减小"), (True, "射速略微提升")],
         modifys=[m("BulletsSpreadAngle", "Factor", -0.25), m("ShootInterval", "Factor", -0.08)]),
    dict(asset="SixShooter", name="热缓冲阵列", quality="Balanced", icon=ICON_HEAT,
         desc=[(True, "满热前能多打几发"), (False, "散热速度下降")],
         modifys=[m("HeatPerShot", "Factor", -0.3), m("CoolSpeed", "Factor", -0.2)]),
    dict(asset="ElephantRounds", name="巨象弹", quality="Balanced", icon=ICON_DMGGEN,
         desc=[(True, "直击伤害大幅提升"), (False, "每发产生的热量显著增加")],
         modifys=[m("DirectDamage", "Factor", 0.6), m("HeatPerShot", "Factor", 0.45)]),
    dict(asset="MoonClipFeed", name="液态回路", quality="Unstable", icon=ICON_COOLING,
         desc=[(True, "散热速度极大提升"), (True, "过热后恢复更快"), (False, "直击伤害下降")],
         modifys=[m("CoolSpeed", "Factor", 0.8), m("OverheatDuration", "Factor", -0.4),
                  m("DirectDamage", "Factor", -0.2)]),
    dict(asset="OverboreChamber", name="临界过载", quality="Unstable", icon=ICON_PEN,
         desc=[(True, "直击伤害大幅提升"), (True, "穿甲等级提升"),
               (False, "每发热量大幅增加"), (False, "过热后恢复更慢")],
         modifys=[m("DirectDamage", "Factor", 0.45), m("DirectAP", "Base", 1),
                  m("HeatPerShot", "Factor", 0.6), m("OverheatDuration", "Factor", 0.5)]),
]


# ---------------------------------------------------------------- 渲染

def render_wud(it, info):
    return gen.render_wud(it["asset"], it["name"], it["category"], it["icon"],
                          it["desc"], it["cost"], it["modifys"], info)


def existing_guids():
    """guid -> 资产相对路径；带路径是为了让"自己上次写的那份"不算撞车。"""
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


def guid_free(g, rel, used):
    owner = used.get(g)
    return owner is None or owner == rel


# ---------------------------------------------------------------- 校验

def validate(info):
    errors, warnings = [], []
    cfg_names = {n for n, _ in CFG}

    def mods_of(owner, mods, only_buff):
        for mod in mods:
            ok, why = info.check(mod["attr"], mod["modifier"])
            if not ok:
                errors.append("%s: %s" % (owner, why))
                continue
            ok, why = info.has_base(mod["attr"], cfg_names)
            if not ok:
                errors.append("%s: %s" % (owner, why))
            if only_buff and info.is_buff(mod["attr"], float(mod["value"])) is False:
                errors.append("%s: `%s` 的 %s %s 是弱化项 ⇒ WUD 只允许强化"
                              % (owner, mod["attr"], mod["modifier"], mod["value"]))

    for tier in TIERS:
        if len(tier["items"]) != 3:
            warnings.append("Key=%s 有 %d 项（本枪按 3 项铺，selectIndex 才整齐）"
                            % (tier["key"], len(tier["items"])))
        for it in tier["items"]:
            if "common" in it or "cross" in it:
                continue
            mods_of("WUD_" + it["asset"], it["modifys"], True)

    for md in MODULES:
        mods_of("WMD_" + md["asset"], md["modifys"], False)
        goods = sum(1 for g, _ in md["desc"] if g)
        if md["quality"] == "Clean" and goods != len(md["desc"]):
            errors.append("%s 是 Clean 却写代价文案" % md["asset"])
        if md["quality"] != "Clean" and goods == len(md["desc"]):
            errors.append("%s 是 %s 却只有增益" % (md["asset"], md["quality"]))

    # showAttr 每项要么在 cfg，要么由 InitAttribute 从 Damages 补（DirectDamage/DirectAP 这类）
    for n in SHOW_ATTR:
        if n not in cfg_names and n not in info.auto_fill:
            errors.append("showAttr 里的 `%s` 既不在 cfg、也不由 InitAttribute 补 ⇒ 只会显示 0" % n)
    return errors, warnings


# ---------------------------------------------------------------- 生成

def build(info, used):
    files, guids = {}, {}
    for d in (WUD_DIR, WMD_DIR):
        if not os.path.exists(os.path.join(ROOT, d + ".meta")):
            g = gen.guid_of(SALT, d)
            if not guid_free(g, d, used):
                return None, ["目录 guid 撞车: " + d]
            files[d + ".meta"] = gen.render_folder_meta(g)

    for tier in TIERS:
        for it in tier["items"]:
            if "common" in it or "cross" in it:
                continue
            owner = "WUD_" + it["asset"]
            g = gen.guid_of(SALT, owner)
            p = "%s/WUD_%s.asset" % (WUD_DIR, it["asset"])
            if not guid_free(g, p, used):
                return None, ["guid 撞车: " + owner]
            guids[owner] = g
            files[p] = render_wud(it, info)
            files[p + ".meta"] = gen.render_meta(g)

    for md in MODULES:
        owner = "WMD_" + md["asset"]
        g = gen.guid_of(SALT, owner)
        p = "%s/WMD_%s.asset" % (WMD_DIR, md["asset"])
        if not guid_free(g, p, used):
            return None, ["guid 撞车: " + owner]
        guids[owner] = g
        files[p] = gen.render_wmd(md["asset"], md["name"], md["quality"], md["icon"],
                                  [dict(good=g_, text=t) for g_, t in md["desc"]],
                                  md["modifys"], info)
        files[p + ".meta"] = gen.render_meta(g)

    tiers = []
    for tier in TIERS:
        row = []
        for it in tier["items"]:
            if "common" in it:
                row.append(COMMON[it["common"]])
            elif "cross" in it:
                row.append(CROSS[it["cross"]])
            else:
                row.append(guids["WUD_" + it["asset"]])
        tiers.append((tier["key"], row))
    return dict(files=files, tiers=tiers,
                modules=[guids["WMD_" + md["asset"]] for md in MODULES]), []


def patch_prefab(info, tiers, modules_guids):
    path = os.path.join(ROOT, PREFAB)
    with open(path, encoding="utf-8", newline="") as f:
        src = f.read()
    nl = "\r\n" if "\r\n" in src else "\n"
    text = src.replace("\r\n", "\n")
    old = text
    errors = []

    # 1) cfg 整块重写（要改值、要删旧键，不能只追加）
    block = "    _fakeAttrs:\n" + "".join(
        "    - Key: %d\n      Value: %s\n" % (info.num(n), gen.num_text(v)) for n, v in CFG)
    text, n = re.subn(r"(?m)^    _fakeAttrs:\n(?:    - Key: \d+\n      Value: [^\n]*\n)*", block, text)
    if n != 1:
        errors.append("cfg._fakeAttrs 命中 %d 次" % n)

    # 2) 射击类型 / 标旗
    for field, val in (("ShootType", SHOOT_TYPE), ("WeaponFlag", WEAPON_FLAG)):
        text, n = re.subn(r"(?m)^  %s: \d+$" % field, "  %s: %d" % (field, val), text)
        if n != 1:
            errors.append("%s 命中 %d 次" % (field, n))

    # 3) showAttr
    hexstr = gen.show_attr_hex(SHOW_ATTR, info)
    text, n = re.subn(r"(?m)^  showAttr: [0-9a-fA-F]*$", "  showAttr: " + hexstr, text)
    if n != 1:
        errors.append("showAttr 命中 %d 次" % n)

    # 4) m_UniqueAttr：v2 没有 Special，清空（同时兼容"空数组"与"已有块"）
    pattern = r"^  m_UniqueAttr: \[\]$|^  m_UniqueAttr:\n(?:  - name: [^\n]*\n(?:    [^\n]*\n)+)+"
    text, n = re.subn(pattern,
                      lambda mm: "  m_UniqueAttr: []" + ("\n" if mm.group(0).endswith("\n") else ""),
                      text, count=1, flags=re.M)
    if n != 1:
        errors.append("m_UniqueAttr 命中 %d 次" % n)

    # 5) Upgrade 整块
    lines = ["  Upgrade:"]
    for key, row in tiers:
        lines.append("  - Key: %d" % key)
        lines.append("    Value:")
        for g in row:
            lines.append("    - {fileID: 11400000, guid: %s, type: 2}" % g)
    text, n = re.subn(r"(?ms)^  Upgrade:\n.*?(?=^  Modules:)", "\n".join(lines) + "\n", text)
    if n != 1:
        errors.append("Upgrade 块命中 %d 次" % n)

    # 6) Modules + ActiveModule
    lines = ["  Modules:", "  - {fileID: 11400000, guid: %s, type: 2}" % WMD_NONE]
    lines += ["  - {fileID: 11400000, guid: %s, type: 2}" % g for g in modules_guids]
    text, n = re.subn(r"(?m)^  Modules:(?: \[\]|\n(?:  - [^\n]*\n)+)",
                      lambda mm: "\n".join(lines) + ("\n" if mm.group(0).endswith("\n") else ""), text)
    if n != 1:
        errors.append("Modules 块命中 %d 次" % n)
    text, n = re.subn(r"(?m)^  ActiveModule: [^\n]*$",
                      "  ActiveModule: {fileID: 11400000, guid: %s, type: 2}" % WMD_NONE, text)
    if n != 1:
        errors.append("ActiveModule 命中 %d 次" % n)

    return dict(path=path, new=text, nl=nl, changed=text != old, errors=errors)


# ---------------------------------------------------------------- 回读校验

def verify(info):
    with open(os.path.join(ROOT, PREFAB), encoding="utf-8", newline="") as f:
        src = f.read()
    ok = True
    rev = {v: k for k, v in info.enum_num.items()}

    cfg = attr_prefab_cfg(os.path.join(ROOT, PREFAB))
    want = {info.num(n): v for n, v in CFG}
    if cfg != want:
        print("  [FAIL] cfg 与 spec 不一致\n    disk: %s\n    spec: %s" % (cfg, want))
        ok = False
    else:
        shots = 100.0 / want[info.num("HeatPerShot")]
        print("  cfg OK；HeatPerShot=%s ⇒ 约 %.1f 发满热（第 %d 发触发过热）"
              % (want[info.num("HeatPerShot")], shots, int(shots) + 1))

    for field, val in (("ShootType", SHOOT_TYPE), ("WeaponFlag", WEAPON_FLAG)):
        got = int(re.search(r"(?m)^  %s: (\d+)$" % field, src).group(1))
        print("  %s = %d %s" % (field, got, "" if got == val else "  [FAIL]"))
        ok = ok and got == val

    hexstr = re.search(r"(?m)^  showAttr: ([0-9a-fA-F]+)$", src).group(1)
    raw = bytes.fromhex(hexstr)
    nums = [int.from_bytes(raw[i:i + 4], "little") for i in range(0, len(raw), 4)]
    print("  showAttr -> %s" % ", ".join(rev.get(n, "?%d" % n) for n in nums))

    body = re.search(r"(?ms)^  Upgrade:\n(.*?)^  Modules:", src).group(1)
    tiers = re.findall(r"  - Key: (\d+)\n    Value:\n((?:    - [^\n]*\n)*)", body)
    print("  Upgrade 档位: %s" % ", ".join("%s(%d项)" % (k, v.count("fileID")) for k, v in tiers))

    mods = re.search(r"(?ms)^  Modules:\n((?:  - [^\n]*\n)*)", src).group(1).strip().splitlines()
    print("  Modules: %d（含 None）" % len(mods))

    seg = src[src.index("  Upgrade:"):src.index("  ActiveModule:")]
    for g in re.findall(r"guid: ([0-9a-f]{32})", seg):
        if not _guid_exists(g):
            print("  [FAIL] prefab 引用的 guid 找不到资产: %s" % g)
            ok = False
    print("  m_UniqueAttr: %s" % re.search(r"(?m)^  m_UniqueAttr: .*$", src).group(0).strip())
    print("VERIFY:", "OK" if ok else "FAILED")
    return 0 if ok else 1


def attr_prefab_cfg(path):
    with open(path, encoding="utf-8") as f:
        s = f.read()
    m_cfg = re.search(r"(?ms)^  cfg:\n(.*?)(?=^  \w+:)", s)
    if not m_cfg:
        return {}
    return {int(k): float(v) for k, v in
            re.findall(r"(?m)^\s*- Key: (\d+)\n\s*Value: ([-\d.]+)\s*$", m_cfg.group(1))}


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
        return verify(info)

    errors, warnings = validate(info)
    for w in warnings:
        print("  [WARN]", w)
    if errors:
        for e in errors:
            print("  [ERROR]", e)
        print("RESULT: FAILED（未写入）")
        return 1

    built, errs = build(info, existing_guids())
    if errs:
        for e in errs:
            print("  [ERROR]", e)
        return 1

    same = new = 0
    for rel, text in sorted(built["files"].items()):
        p = os.path.join(ROOT, rel)
        cur = None
        if os.path.exists(p):
            with open(p, encoding="utf-8", newline="") as f:
                cur = f.read()
        if cur == text:
            same += 1
            continue
        new += 1
        print("  [%s] %s" % ("DIFF" if dry else "WRITE", rel))
        if not dry:
            os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)

    pf = patch_prefab(info, built["tiers"], built["modules"])
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

    # 退役资产：按**打完补丁后**的 prefab 判断是否还被引用（否则会误判成"仍在用"）
    for name in RETIRED:
        p = "%s/WUD_%s.asset" % (WUD_DIR, name)
        if not os.path.exists(os.path.join(ROOT, p)):
            continue
        g = gen.guid_of(SALT, "WUD_" + name)
        if g in pf["new"]:
            print("  [SKIP] 仍被 prefab 引用，不删: %s" % p)
            continue
        print("  [%s] 删除退役项 %s(+.meta)" % ("DIFF" if dry else "DEL", p))
        if not dry:
            for q in (p, p + ".meta"):
                if os.path.exists(os.path.join(ROOT, q)):
                    os.remove(os.path.join(ROOT, q))

    print("资产: 一致 %d / 变更 %d；tiers=%d modules=%d"
          % (same, new, len(TIERS), len(MODULES)))
    print("RESULT:", "OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
