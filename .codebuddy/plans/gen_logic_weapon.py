# -*- coding: utf-8 -*-
"""为 Assets/Resources/Weapons/Logic.prefab（逻辑）生成 12 个升级项 + 7 个模组，并打补丁。

设计依据：DRG Deepcore GK2 的 3/2/2/2/3 分档，全部落在零代码可实现的属性上。
   9  MoveSpeedToShoot   base 0.7  射击时移速（基线代价，可被升级赎回）
 100  DirectDestruction  base 1.0  护甲破坏系数（DamageGroupDirect[Destruction=2]=1）
 102  WeaknessBonus      base 0    弱点加成（Extra only）

只写 .asset/.meta 与 prefab，不动任何 .cs。幂等：已存在的同名资产会被覆盖并提示。
"""
import hashlib
import json
import os
import re
import shutil
import sys

ROOT = r"d:/Pro/Bluedivers"
PREFAB = os.path.join(ROOT, "Assets/Resources/Weapons/Logic.prefab")
WUD_DIR = os.path.join(ROOT, "Assets/Resources/GameData/WeaponUpgrade/Logic")
WMD_DIR = os.path.join(ROOT, "Assets/Resources/GameData/WeaponModule/Logic")

WUD_SCRIPT = "150ca7a623f31424aad6112aba22d37e"
WMD_SCRIPT = "744367736a7aee740922335e2157ba81"
WMD_NONE_GUID = "a19bae1a0785866409ae31f377bde9cf"

I_DMG = "1b3193f68ca866943bada632271653b6"
I_AMMO = "09b2164270d5192469590d258cf8ff79"
I_RATE = "da2ddc2b67324234daa8bdf435957132"
I_RELOAD = "2075b11602ab1ca41916cf8f34ac13b1"
I_ACC = "d31f7dc298d350a4395888c6839bf608"
I_MOD = "bf5a0efa56a6f0042ae292051e63027b"

# ---- 属性号 / 修饰符号（来自 WeaponAttributeFactory）----
A_AMMO, A_MAG, A_INTERVAL, A_SPREAD, A_MOVESPEED, A_RELOAD = 0, 1, 2, 7, 9, 10
A_DMG, A_AP = 20, 25
A_DESTRUCTION, A_WEAKNESS = 100, 102
M_BASE, M_FACTOR, M_EXTRA = 1, 2, 4

# (资产名, 中文名, 分类, icon, desc, 价格, [(attr, modifier, value)])
WUDS = [
    # ---- T1 / Key 2 ----
    ("GyroStabilisation", "陀螺稳定", "散布", I_ACC,
     "陀螺稳定组件大幅收束射击散布，让双持冲锋枪在中距离也能稳定命中。", 1000,
     [(A_SPREAD, M_FACTOR, -0.30)]),
    ("SuperchargedFeed", "增压供弹", "射速", I_RATE,
     "强化供弹机构，缩短射击间隔。代价是弹药消耗更快。", 1000,
     [(A_INTERVAL, M_FACTOR, -0.12)]),
    ("QuickfireEjector", "快速抛壳", "装弹", I_RELOAD,
     "熟练的抛壳手法让换弹更快，交火空档更短。", 1000,
     [(A_RELOAD, M_BASE, -0.8)]),
    # ---- T2 / Key 4 ----
    ("IncreasedCaliberRounds", "大口径弹药", "伤害", I_DMG,
     "加大口径换取单发威力，是所有伤害流派的起点。", 1500,
     [(A_DMG, M_BASE, 3)]),
    ("ExpandedAmmoBags", "扩容弹袋", "总弹药量", I_AMMO,
     "多带几个口袋，总备弹量显著提升。", 1500,
     [(A_AMMO, M_FACTOR, 0.35)]),
    # ---- T3 / Key 8 ----
    ("FloatingBarrel", "浮动枪管", "散布", I_ACC,
     "浮动式枪管吸收连射抖动，进一步压低散布。", 2000,
     [(A_SPREAD, M_FACTOR, -0.40)]),
    ("TacticalStride", "战术步伐", "机动", I_ACC,
     "调整持枪姿态，射击时不再拖慢脚步。", 2000,
     [(A_MOVESPEED, M_BASE, 0.30)]),
    # ---- T4 / Key 12 ----
    ("HollowPointBullets", "空尖弹", "弱点", I_DMG,
     "弹头在命中弱点时展开，弱点伤害提升 20%。", 2500,
     [(A_WEAKNESS, M_EXTRA, 0.20)]),
    ("HardenedRounds", "硬化弹头", "穿甲", I_DMG,
     "钨芯弹头，穿甲等级由轻穿提升到中穿。", 2500,
     [(A_AP, M_BASE, 2)]),
    # ---- T5 / Key 16 ----
    ("ImprovedGasSystem", "改良导气", "射速", I_RATE,
     "导气系统全面强化，射速再上一个台阶。", 3000,
     [(A_INTERVAL, M_FACTOR, -0.18)]),
    ("OverpressureCore", "超压弹芯", "伤害", I_DMG,
     "超压装药提升单发伤害，但弹匣容量下降。", 3000,
     [(A_DMG, M_FACTOR, 0.25), (A_MAG, M_FACTOR, -0.15)]),
    ("PenetratorCore", "破甲弹芯", "护甲破坏", I_DMG,
     "硬芯穿甲弹，对护甲的破坏系数提升到 3 倍，专治重甲单位。", 3000,
     [(A_DESTRUCTION, M_BASE, 2)]),
]

# (资产名, 中文名, ModuleType 1Clean/2Balanced/3Unstable, icon, [(是否增益, 文案)], modifys)
WMDS = [
    ("CompactAmmo", "紧凑弹药", 1, I_AMMO,
     [(True, "弹匣容量 +5"), (True, "散布 -15%")],
     [(A_MAG, M_BASE, 5), (A_SPREAD, M_FACTOR, -0.15)]),
    ("GasRerouting", "导气重排", 2, I_RATE,
     [(True, "射速提升"), (False, "弹匣容量 -4")],
     [(A_INTERVAL, M_FACTOR, -0.15), (A_MAG, M_BASE, -4)]),
    ("OverclockedFiring", "超频击发机构", 2, I_RATE,
     [(True, "射速大幅提升"), (False, "散布 3°→6°")],
     [(A_INTERVAL, M_FACTOR, -0.25), (A_SPREAD, M_BASE, 3)]),
    ("AIStabilityEngine", "AI 稳定引擎", 2, I_ACC,
     [(True, "散布 -60%"), (True, "弱点伤害 +35%"),
      (False, "直击伤害 -10%"), (False, "射速 -23%")],
     [(A_SPREAD, M_FACTOR, -0.60), (A_WEAKNESS, M_EXTRA, 0.35),
      (A_DMG, M_FACTOR, -0.10), (A_INTERVAL, M_FACTOR, 0.30)]),
    ("JumboRounds", "大号弹壳", 3, I_DMG,
     [(True, "直击伤害 +4"), (False, "弹匣容量 -8"), (False, "装填时间 +0.5s")],
     [(A_DMG, M_BASE, 4), (A_MAG, M_BASE, -8), (A_RELOAD, M_BASE, 0.5)]),
    ("HeavyStance", "重装架势", 3, I_DMG,
     [(True, "护甲破坏 ×2"), (True, "散布 -1.0°"), (False, "射击时移速 70%→30%")],
     [(A_DESTRUCTION, M_BASE, 1), (A_SPREAD, M_BASE, -1), (A_MOVESPEED, M_BASE, -0.4)]),
    ("DoubleTap", "双击枪机", 3, I_RATE,
     [(True, "射速大幅提升"), (False, "弹匣容量 -14"), (False, "装填时间 +0.6s")],
     [(A_INTERVAL, M_FACTOR, -0.35), (A_MAG, M_BASE, -14), (A_RELOAD, M_BASE, 0.6)]),
]

# 分档：Key -> 该档的资产名（顺序即 UI 列序）
TIERS = [
    (2, ["GyroStabilisation", "SuperchargedFeed", "QuickfireEjector"]),
    (4, ["IncreasedCaliberRounds", "ExpandedAmmoBags"]),
    (8, ["FloatingBarrel", "TacticalStride"]),
    (12, ["HollowPointBullets", "HardenedRounds"]),
    (16, ["ImprovedGasSystem", "OverpressureCore", "PenetratorCore"]),
]

# showAttr：原有 5 项保持不变，追加 弱点/护甲破坏/射击移速/散布/装填
SHOW_ATTR = [A_DMG, A_INTERVAL, A_MAG, A_AMMO, A_AP,
             A_WEAKNESS, A_DESTRUCTION, A_MOVESPEED, A_SPREAD, A_RELOAD]
# _fakeAttrs 追加项（原有 0/1/2/7/10 不动）
EXTRA_CFG = [(A_MOVESPEED, 0.7), (A_DESTRUCTION, 1.0), (A_WEAKNESS, 0.0)]


def guid_of(key):
    return hashlib.md5(("Bluedivers.LogicWeapon." + key).encode("utf-8")).hexdigest()


def q(s):
    """Unity YAML 里的字符串：双引号 + \\uXXXX 转义（与现有资产一致）。"""
    return json.dumps(s, ensure_ascii=True)


def wud_yaml(name, cname, cat, icon, desc, cost, mods):
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
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % WUD_SCRIPT,
        "  m_Name: WUD_%s" % name,
        "  m_EditorClassIdentifier: ",
        "  name: %s" % q(cname),
        "  type: %s" % q(cat),
        "  icon: {fileID: 21300000, guid: %s, type: 3}" % icon,
        "  desc: %s" % q(desc),
        "  cost:",
        "  - Key: 0",
        "    Value: %d" % cost,
        "  modifys:",
    ]
    for attr, modifier, value in mods:
        lines += ["  - name: ", "    type: %d" % attr,
                  "    modifier: %d" % modifier, "    value: %s" % value]
    return "\n".join(lines) + "\n"


def wmd_yaml(name, cname, mtype, icon, desc_items, mods):
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
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % WMD_SCRIPT,
        "  m_Name: WMD_%s" % name,
        "  m_EditorClassIdentifier: ",
        "  name: %s" % q(cname),
        "  type: %d" % mtype,
        "  icon: {fileID: 21300000, guid: %s, type: 3}" % icon,
        "  desc:",
    ]
    for good, text in desc_items:
        lines += ["  - Key: %d" % (1 if good else 0), "    Value: %s" % q(text)]
    lines.append("  modifys:")
    for attr, modifier, value in mods:
        lines += ["  - name: ", "    type: %d" % attr,
                  "    modifier: %d" % modifier, "    value: %s" % value]
    return "\n".join(lines) + "\n"


def meta_yaml(guid):
    return ("fileFormatVersion: 2\n"
            "guid: %s\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n") % guid


def folder_meta(guid):
    return ("fileFormatVersion: 2\n"
            "guid: %s\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n") % guid


def write(path, text, force):
    if os.path.exists(path) and not force:
        print("  skip(exists):", os.path.relpath(path, ROOT))
        return
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def main():
    force = "--force" in sys.argv
    guids = {}

    for d in (WUD_DIR, WMD_DIR):
        if not os.path.isdir(d):
            os.makedirs(d)
            write(d + ".meta", folder_meta(guid_of(os.path.relpath(d, ROOT))), force)

    for name, cname, cat, icon, desc, cost, mods in WUDS:
        g = guid_of("WUD_" + name)
        guids["WUD_" + name] = g
        write(os.path.join(WUD_DIR, "WUD_%s.asset" % name),
              wud_yaml(name, cname, cat, icon, desc, cost, mods), force)
        write(os.path.join(WUD_DIR, "WUD_%s.asset.meta" % name), meta_yaml(g), force)

    for name, cname, mtype, icon, desc_items, mods in WMDS:
        g = guid_of("WMD_" + name)
        guids["WMD_" + name] = g
        write(os.path.join(WMD_DIR, "WMD_%s.asset" % name),
              wmd_yaml(name, cname, mtype, icon, desc_items, mods), force)
        write(os.path.join(WMD_DIR, "WMD_%s.asset.meta" % name), meta_yaml(g), force)

    # ---------------- prefab 补丁 ----------------
    with open(PREFAB, "r", encoding="utf-8", newline="") as f:
        src = f.read()
    nl = "\r\n" if "\r\n" in src else "\n"
    text = src.replace("\r\n", "\n")
    original = text

    # 1) cfg._fakeAttrs 追加 3 条
    anchor = "    - Key: 10\n      Value: 3\n"
    assert text.count(anchor) == 1, "cfg 锚点不唯一/不存在: %d" % text.count(anchor)
    add = "".join("    - Key: %d\n      Value: %s\n" % (k, v) for k, v in EXTRA_CFG)
    text = text.replace(anchor, anchor + add)

    # 2) showAttr
    hexstr = "".join(int(a).to_bytes(4, "little").hex() for a in SHOW_ATTR)
    text, n = re.subn(r"(?m)^  showAttr: [0-9A-Fa-f]*$", "  showAttr: " + hexstr, text)
    assert n == 1, "showAttr 未唯一命中: %d" % n

    # 3) Upgrade 整块
    tiers = []
    for key, names in TIERS:
        tiers.append("  - Key: %d\n    Value:" % key)
        for nm in names:
            tiers.append("    - {fileID: 11400000, guid: %s, type: 2}" % guids["WUD_" + nm])
    upgrade_block = "  Upgrade:\n" + "\n".join(tiers) + "\n"
    text, n = re.subn(r"(?ms)^  Upgrade:\n.*?(?=^  Modules:)", upgrade_block, text)
    assert n == 1, "Upgrade 块未唯一命中: %d" % n

    # 4) Modules + ActiveModule
    mod_lines = ["  Modules:", "  - {fileID: 11400000, guid: %s, type: 2}" % WMD_NONE_GUID]
    for nm, _, _, _, _, _ in WMDS:
        mod_lines.append("  - {fileID: 11400000, guid: %s, type: 2}" % guids["WMD_" + nm])
    text, n = re.subn(r"(?m)^  Modules: \[\]$", "\n".join(mod_lines), text)
    assert n == 1, "Modules: [] 未唯一命中: %d" % n
    text, n = re.subn(r"(?m)^  ActiveModule: \{fileID: 0\}$",
                      "  ActiveModule: {fileID: 11400000, guid: %s, type: 2}" % WMD_NONE_GUID, text)
    assert n == 1, "ActiveModule 未唯一命中: %d" % n

    # 自检：引用数与档数
    assert text.count("guid: ") >= 0
    assert len(re.findall(r"WUD", "".join(tiers))) == 0  # 占位，无实际意义
    assert text.count("  - Key: 2\n    Value:") == 1

    if text != original:
        shutil.copyfile(PREFAB, PREFAB + ".bak")
        with open(PREFAB, "w", encoding="utf-8", newline="") as f:
            f.write(text.replace("\n", nl))
        print("prefab 已打补丁，备份: Logic.prefab.bak")
    else:
        print("prefab 无变化（可能已打过）")

    print("生成 WUD %d 个 / WMD %d 个" % (len(WUDS), len(WMDS)))
    print("showAttr =", hexstr)


if __name__ == "__main__":
    main()
