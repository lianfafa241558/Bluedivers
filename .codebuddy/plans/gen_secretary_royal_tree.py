# -*- coding: utf-8 -*-
"""SecretaryDecision（书记的表决）/ RoyalFriend（皇室红茶）整棵升级树 + 模组。

设计依据（DRG 对标）：
  * SecretaryDecision ~ Subata 120：高射速半自动手枪，档位按「手感/容量 → 伤害/弹药 → 机制
    → 专用乘区 → 签名（全自动）」铺，回收件遍布五档。
  * RoyalFriend ~ BRT7 Burst Fire Gun：单发重、弧线弹（Gravity=10），档位按「伤害/散布/弹仓 →
    后坐/装填/射速 → 弹仓/弹速/稳定 → 穿甲/备弹/弱点 → 签名（全自动/重弹头）」铺。

两条硬规则（见 skill §0.1 / §0.3）：
  * WUD 只准强化（脚本按属性方向判，弱化项直接 ERROR）；
  * WMD 的 desc 只写方向与量级，**出现数字直接报错**。

跑法：
    python .codebuddy/plans/gen_secretary_royal_tree.py --dry-run
    python .codebuddy/plans/gen_secretary_royal_tree.py
    python .codebuddy/plans/gen_secretary_royal_tree.py --verify
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

WMD_NONE = "a19bae1a0785866409ae31f377bde9cf"
MAX_DESC = 4          # SelectRoleWnd 的 moduleLayout 子项数有限，多写的行会被静默丢弃

# ---------------------------------------------------------------- 图标（Resources/Images/SkillIcon/DRG）
ICON = {
    "acc": "0eac3611eea675245987e6db3bf8eb3b",          # 散布
    "clip": "7c07f1f9a69294949ae48e2c9a29eafb",         # 弹匣
    "mag": "ce13b8ed35a2e1f4e8030653c35ea7eb",          # 弹仓
    "ammo": "999805b81db170343924d256a6aa3a5b",         # 备弹
    "dmg": "c01ee1f9f5e5aeb41b9508f38a67cdeb",          # 直击伤害
    "dmgG": "f7729a4d515396f4aa47fb90ae5ad11a",         # 伤害（通用）
    "rate": "5fbbe63e1c448df459304aca3025ab19",         # 射速
    "reload": "963e16d12a501264e8c8bd6de0e42d95",       # 装填
    "recoil": "c118e6fab661f0740a14eccad529580c",       # 后坐力
    "stable": "fea1eaa19c9a8ba4eba678fcf7750a47",       # 稳定
    "speed": "56b1d7fd68bef674c8d188f0a328c98e",        # 弹速
    "ap": "10d0a2555de979341839503f09b9aeba",           # 破甲
    "ricochet": "d2d5c92cfbdd08645924e2b1c4be72c5",     # 跳弹
    "capacity": "589a9beb3d7609044b30506b277e51cb",     # 容量
    "special": "678d408e5f4e687488611606db9f06d9",      # 特殊机制
    "weak": "c9a8c0b4e81364041bd422336e4b5f38",         # 弱点
    "bounce": "0b4707d6f5a129440b65d233cf0b6c32",       # 弹射
    "reserve": "09b2164270d5192469590d258cf8ff79",      # 备弹（通用图标）
}

# 根目录通用池（多枪适用只留这一份，禁复制）
COMMON = {
    "Accuracy": "b9d3f676c862ea0dc18bd826f77163ea",       # 散布 Factor -0.25
    "AmmoBag": "d46dbdfaa5e25b94e807d673ab3ea835",        # 备弹 Factor +0.4
    "BulletSpeed": "ed5cd4fa22185244c3efcd4d7a41124d",   # 弹速 Factor +0.3
    "Damage": "0a01454bb348bcd40a0e10403c33c443",         # 直伤 Factor +0.3
    "HardenedRounds": "a272edbbe7721e9aa8f00416d53ffcf2", # 穿甲 Base +1
}


def m(attr, modifier, value, name=None):
    return dict(attr=attr, modifier=modifier, value=value, name=name)


# ================================================================ SecretaryDecision
SECRETARY = dict(
    prefab="Assets/Resources/Weapons/SecretaryDecision.prefab",
    wudDir="Assets/Resources/GameData/WeaponUpgrade/SecretaryDecision",
    wmdDir="Assets/Resources/GameData/WeaponModule/SecretaryDecision",
    salt="Bluedivers.SecretaryDecision",
    # 已有的专有项：不重建，只引用（guid 稳定，m_UniqueAttr 里已注册「全自动模式」）
    reuse={"WUD_Automatic": "0eee8c85a713b0b458a7c72ed6cac1a3"},
    unique=[dict(name="全自动模式", value=0, tag=32)],
    showAttr=["DirectDamage", "ShootInterval", "Magazine", "Ammo", "ReloadTime",
              "BulletsSpreadAngle", "WeaknessBonus", "DirectAP", "Recoil"],
    tiers=[
        {"key": 2, "items": [
            dict(common="Accuracy"),
            dict(asset="ExtendedClip", name="加长弹匣", category="弹匣", icon="clip", cost=1000,
                 desc="弹匣加长了一截，能多塞几发再换。多出来的那截正好顶在手心，握感因人而异。",
                 modifys=[m("Magazine", "Base", 4)]),
            dict(asset="LightTrigger", name="轻量化扳机", category="射速", icon="rate", cost=1000,
                 desc="扳机组换成轻量件，扣下去更快回位。连点的时候手指不容易先累。",
                 modifys=[m("ShootInterval", "Factor", -0.1)]),
        ]},
        {"key": 6, "items": [
            dict(common="AmmoBag"),
            dict(asset="CaliberRounds", name="加大口径弹", category="伤害", icon="dmg", cost=1500,
                 desc="换了更大口径的弹药，单发更疼。枪管寿命的事工程部说不用你操心。",
                 modifys=[m("DirectDamage", "Base", 3)]),
            dict(asset="RecoilBuffer", name="后坐缓冲", category="后坐力", icon="recoil", cost=1500,
                 desc="枪身后部塞了一段缓冲件，连射时枪口压得住一些。至于那点重量，训练手册里不提。",
                 modifys=[m("Recoil", "Factor", -0.3)]),
        ]},
        {"key": 10, "items": [
            dict(asset="QuickRelease", name="快拆弹匣井", category="装填", icon="reload", cost=2000,
                 desc="弹匣井改成快拆结构，换弹动作短了一截。掉地上的空弹匣记得捡，报销要凭据。",
                 modifys=[m("ReloadTime", "Base", -0.25)]),
            dict(common="BulletSpeed"),
            dict(asset="FloatingBarrel", name="浮动枪管", category="散布", icon="stable", cost=2000,
                 desc="枪管改成浮动安装，连续开火时散布更听话，枪口跳动也小一些。听起来很讲究，装起来就两颗销钉。",
                 modifys=[m("BulletsSpreadAngle", "Factor", -0.3),
                          m("Recoil", "Factor", -0.2)]),
        ]},
        {"key": 14, "items": [
            dict(common="Damage"),
            dict(asset="HollowPoint", name="空尖弹", category="弱点", icon="weak", cost=2500,
                 desc="弹头命中后展开，专挑软的地方下手，打弱点收益更高。展开之后的样子战地报告没配图。",
                 modifys=[m("WeaknessBonus", "Extra", 0.35)]),
            dict(common="HardenedRounds"),
        ]},
        {"key": 18, "items": [
            dict(reuse="WUD_Automatic"),
            dict(asset="OverpressureLoad", name="超压装药", category="伤害", icon="dmg", cost=3000,
                 desc="装药压过了验收标准，每一发都更狠，扳机也跟得更顺。安全裕度那东西是写给报告看的。",
                 modifys=[m("DirectDamage", "Factor", 0.3),
                          m("ShootInterval", "Factor", -0.1)]),
            dict(asset="DeepMagazine", name="深仓弹匣", category="弹匣", icon="capacity", cost=3000,
                 desc="弹匣加深，一次能带更多发，换弹也顺手了些。这玩意儿在西装里藏不住，好在没人敢搜。",
                 modifys=[m("Magazine", "Base", 8),
                          m("ReloadTime", "Factor", -0.2)]),
        ]},
    ],
    modules=[
        dict(asset="MatchTrigger", name="竞赛级扳机", quality="Clean", icon="acc",
             describe="厂队调校过的扳机组，扣下去那一下干脆得不像话。赛场上没资格用，这里没人管。",
             desc=[(True, "散布显著减小"), (True, "射速略微提升")],
             modifys=[m("BulletsSpreadAngle", "Factor", -0.3), m("ShootInterval", "Factor", -0.08)]),
        dict(asset="HeavySlide", name="重型套筒", quality="Balanced", icon="recoil",
             describe="整块钢的套筒，把枪口按得很稳，代价是循环慢了一点。",
             desc=[(True, "后坐力大幅降低"), (False, "射速略微下降")],
             modifys=[m("Recoil", "Factor", -0.5), m("ShootInterval", "Factor", 0.08)]),
        dict(asset="OversizedMagazine", name="超规格弹匣", quality="Balanced", icon="clip",
             describe="比枪身还长一截的弹匣，插进去有点难看，但确实能多打一会儿。",
             desc=[(True, "弹匣容量大幅提升"), (False, "装填明显变慢")],
             modifys=[m("Magazine", "Base", 8), m("ReloadTime", "Base", 0.4)]),
        dict(asset="ExperimentalRounds", name="实验弹头", quality="Balanced", icon="dmg",
             describe="试制批次，威力扎实，就是产线还没跟上，能带的总量少了。",
             desc=[(True, "直击伤害显著提升"), (False, "备弹量减少")],
             modifys=[m("DirectDamage", "Factor", 0.3), m("Ammo", "Factor", -0.25)]),
        dict(asset="ChainRounds", name="链式弹头", quality="Unstable", icon="bounce",
             describe="弹头里塞了颗会自己找活的引信。被打中的那个倒霉蛋只是开始。",
             desc=[(True, "命中后弹头跳向附近的另一个敌人"), (False, "直击伤害下降")],
             modifys=[m("Catapult", "Extra", 1), m("DirectDamage", "Factor", -0.18)]),
        dict(asset="BeltFeed", name="弹链供弹", quality="Unstable", icon="reserve",
             describe="整套供弹结构都换掉了，从此不用算着子弹打，但换一次弹要很久。",
             desc=[(True, "弹匣容量大幅提升"), (True, "备弹量大幅增加"), (False, "装填显著变慢")],
             modifys=[m("Magazine", "Base", 16), m("Ammo", "Factor", 0.5), m("ReloadTime", "Base", 0.8)]),
    ],
)

# ================================================================ RoyalFriend
ROYAL = dict(
    prefab="Assets/Resources/Weapons/RoyalFriend.prefab",
    wudDir="Assets/Resources/GameData/WeaponUpgrade/RoyalFriend",
    wmdDir="Assets/Resources/GameData/WeaponModule/RoyalFriend",
    salt="Bluedivers.RoyalFriend",
    reuse={},
    unique=[dict(name="全自动模式", value=0, tag=32)],
    showAttr=["DirectDamage", "ShootInterval", "Magazine", "Ammo", "ReloadTime",
              "BulletsSpreadAngle", "WeaknessBonus", "DirectAP", "Recoil", "BulletSpeed"],
    tiers=[
        {"key": 2, "items": [
            dict(asset="CaliberRounds", name="加大口径弹", category="伤害", icon="dmg", cost=1000,
                 desc="换更大口径的弹药，单发更疼。这把枪本来就不是拿来省弹的。",
                 modifys=[m("DirectDamage", "Base", 3)]),
            dict(common="Accuracy"),
            dict(asset="ExtendedMag", name="加长弹仓", category="弹匣", icon="clip", cost=1000,
                 desc="弹仓加长一截，多两发的余量。开枪的节奏一下就松了。",
                 modifys=[m("Magazine", "Base", 2)]),
        ]},
        {"key": 6, "items": [
            dict(asset="RecoilDampener", name="后坐阻尼", category="后坐力", icon="recoil", cost=1500,
                 desc="枪身后部加了一段阻尼，重弹打出去枪口不再抬那么高。",
                 modifys=[m("Recoil", "Factor", -0.4)]),
            dict(asset="QuickRelease", name="快拆弹仓", category="装填", icon="reload", cost=1500,
                 desc="弹仓改成快拆，换弹动作短了一截。掉出来的空仓记得收，管家会念叨。",
                 modifys=[m("ReloadTime", "Base", -0.3)]),
            dict(asset="FastAction", name="加速击发", category="射速", icon="rate", cost=1500,
                 desc="击发机构换成短行程，扣扳机的节奏可以更密。至于枪管热不热，那是另一回事。",
                 modifys=[m("ShootInterval", "Factor", -0.12)]),
        ]},
        {"key": 10, "items": [
            dict(asset="HighCapacity", name="大容量弹仓", category="弹匣", icon="capacity", cost=2000,
                 desc="弹仓再扩一倍，坐下来慢慢打完一轮都不用起身。",
                 modifys=[m("Magazine", "Base", 4)]),
            dict(common="BulletSpeed"),
            dict(asset="StabilizedStock", name="稳定枪托", category="散布", icon="stable", cost=2000,
                 desc="枪托里灌了配重，连打时枪身更听话，弹着点也收得拢。拎起来是真的沉。",
                 modifys=[m("BulletsSpreadAngle", "Factor", -0.3),
                          m("Recoil", "Factor", -0.2)]),
        ]},
        {"key": 14, "items": [
            dict(common="HardenedRounds"),
            dict(common="AmmoBag"),
            dict(asset="HollowPoint", name="空尖弹", category="弱点", icon="weak", cost=2500,
                 desc="弹头命中就展开，专挑软处下手。对这种慢速重弹来说，这是最划算的改法。",
                 modifys=[m("WeaknessBonus", "Extra", 0.4)]),
        ]},
        {"key": 18, "items": [
            dict(asset="Automatic", name="全自动击发", category="击发方式", icon="special", cost=3000,
                 desc="击发机构改成连发，按住扳机就能把弹仓打空。红茶要凉了，你得先把它打完。",
                 modifys=[m("Special", "Base", 1, name="全自动模式")]),
            dict(asset="HeavySlug", name="重弹头", category="伤害", icon="dmg", cost=3000,
                 desc="弹头换成一整块实心料，单发威力又上了一档。抛物线也更好看了，反正都打不远。",
                 modifys=[m("DirectDamage", "Factor", 0.35)]),
            dict(asset="DeepMagazine", name="长弹仓", category="弹匣", icon="capacity", cost=3000,
                 desc="弹仓接着加长，边上多挂了一截备弹。插进枪里的时候会露出来一点，不算失礼。",
                 modifys=[m("Magazine", "Base", 6), m("Ammo", "Factor", 0.25)]),
        ]},
    ],
    modules=[
        dict(asset="CompositeCasings", name="复合药筒", quality="Clean", icon="ammo",
             describe="药筒换成复合材料，同样重量能多带一些，循环也顺了一点。",
             desc=[(True, "备弹量显著增加"), (True, "射速略微提升")],
             modifys=[m("Ammo", "Factor", 0.35), m("ShootInterval", "Factor", -0.08)]),
        dict(asset="FullChamberSeal", name="全膛密封", quality="Clean", icon="dmgG",
             describe="膛室做了密封处理，燃气一点不浪费，换弹也顺手。",
             desc=[(True, "直击伤害提升"), (True, "装填加快")],
             modifys=[m("DirectDamage", "Factor", 0.12), m("ReloadTime", "Base", -0.2)]),
        dict(asset="CompactMags", name="紧凑药筒", quality="Balanced", icon="reserve",
             describe="药筒做小了一号，能塞的地方就多了，代价是每次循环要多等一点。",
             desc=[(True, "备弹量大幅增加"), (False, "射速下降")],
             modifys=[m("Ammo", "Factor", 0.6), m("ShootInterval", "Factor", 0.12)]),
        dict(asset="ExperimentalRounds", name="实验弹", quality="Balanced", icon="dmg",
             describe="试制装药，威力扎实，产线还没跟上，能装的总量就少了。",
             desc=[(True, "直击伤害大幅提升"), (False, "弹匣容量减少"), (False, "备弹量减少")],
             modifys=[m("DirectDamage", "Factor", 0.35), m("Magazine", "Base", -2), m("Ammo", "Factor", -0.2)]),
        dict(asset="HeavyHitter", name="重击机匣", quality="Unstable", icon="special",
             describe="整套机匣换成重装件，一发顶过去三发，节奏也慢下来三倍。",
             desc=[(True, "直击伤害极大提升"), (False, "射速大幅下降"), (False, "弹匣容量减少")],
             modifys=[m("DirectDamage", "Factor", 0.6), m("ShootInterval", "Factor", 0.3),
                      m("Magazine", "Base", -2)]),
        dict(asset="RicochetLoad", name="跳弹装填", quality="Unstable", icon="ricochet",
             describe="弹头被改成会自己找下一个目标，代价是打中第一个时没那么疼。",
             desc=[(True, "命中后弹头跳向附近的另一个敌人"), (False, "直击伤害下降")],
             modifys=[m("Catapult", "Extra", 1), m("DirectDamage", "Factor", -0.2)]),
    ],
)

SPECS = [SECRETARY, ROYAL]


# ---------------------------------------------------------------- 渲染

def render_wud(it, info):
    """同 gen.render_wud，多一件事：Special 条目要写 name（字典键）。"""
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
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % gen.WUD_SCRIPT,
        "  m_Name: WUD_%s" % it["asset"],
        "  m_EditorClassIdentifier: ",
        "  name: %s" % gen.q(it["name"]),
        "  type: %s" % gen.q(it["category"]),
        "  icon: {fileID: 21300000, guid: %s, type: 3}" % it["icon"],
        "  desc: %s" % gen.q(it["desc"]),
        "  cost:",
        "  - Key: 0",
        "    Value: %d" % it["cost"],
        "  modifys:",
    ]
    for mod in it["modifys"]:
        lines.append("  - name: %s" % (gen.q(mod["name"]) if mod.get("name") else ""))
        lines.append("    type: %d" % info.num(mod["attr"]))
        lines.append("    modifier: %d" % attr_whitelist.MOD_NAMES[mod["modifier"]])
        lines.append("    value: %s" % gen.num_text(mod["value"]))
    return "\n".join(lines) + "\n"


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
        lines.append("  - name: %s" % (gen.q(mod["name"]) if mod.get("name") else ""))
        lines.append("    type: %d" % info.num(mod["attr"]))
        lines.append("    modifier: %d" % attr_whitelist.MOD_NAMES[mod["modifier"]])
        lines.append("    value: %s" % gen.num_text(mod["value"]))
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------- 校验

def validate(spec, info):
    errors, warnings = [], []
    # 真实基础值来源：prefab 的 cfg._fakeAttrs ∪ InitAttribute 自动补（见 attr_whitelist.has_base）
    rev = {v: k for k, v in info.enum_num.items()}
    cfg_nums = set(attr_whitelist.parse_prefab_cfg(os.path.join(ROOT, spec["prefab"])))
    cfg_names = {rev.get(n, str(n)) for n in cfg_nums}

    def mods_of(owner, mods, only_buff):
        for mod in mods:
            if mod["attr"] == "Special":
                if not mod.get("name"):
                    errors.append("%s: Special 条目缺 name" % owner)
                elif mod["name"] not in {u["name"] for u in spec["unique"]}:
                    errors.append("%s: Special `%s` 没在 m_UniqueAttr 注册（会静默失效）"
                                  % (owner, mod["name"]))
                continue
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

    keys = [t["key"] for t in spec["tiers"]]
    if keys != sorted(keys):
        errors.append("tiers 的 Key 必须按解锁等级升序：%s" % keys)
    for tier in spec["tiers"]:
        if len(tier["items"]) != 3:
            errors.append("Key=%s 有 %d 项 ⇒ 存档 selectIndex 会悬空（本表按 3 项铺）"
                          % (tier["key"], len(tier["items"])))
        for it in tier["items"]:
            if it.get("common") or it.get("reuse"):
                continue
            it["icon"] = ICON[it["icon"]]
            mods_of("WUD_" + it["asset"], it["modifys"], True)

    for n in spec["showAttr"]:
        ok, why = info.has_base(n, cfg_names)
        if not ok:
            errors.append("showAttr: " + why)

    seen = set()
    for md in spec["modules"]:
        if md["asset"] in seen:
            errors.append("模组资产名重复: " + md["asset"])
        seen.add(md["asset"])
        md["icon"] = ICON[md["icon"]]
        mods_of("WMD_" + md["asset"], md["modifys"], False)
        if len(md["desc"]) > MAX_DESC:
            errors.append("%s: desc 有 %d 行，超过 UI 槽位 %d（多出的行会被静默丢弃）"
                          % (md["asset"], len(md["desc"]), MAX_DESC))
        goods = sum(1 for g, _ in md["desc"] if g)
        if md["quality"] == "Clean" and goods != len(md["desc"]):
            errors.append("%s 是 Clean 却写代价文案" % md["asset"])
        if md["quality"] != "Clean" and goods == len(md["desc"]):
            errors.append("%s 是 %s 却只有增益文案" % (md["quality"], md["asset"]))
        for _, text in md["desc"]:
            if re.search(r"\d", text):
                errors.append("%s: 模组文案出现数字「%s」⇒ 只写方向与量级（skill §0.3）"
                              % (md["asset"], text))
    return errors, warnings


# ---------------------------------------------------------------- 生成

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


def guid_free(g, rel, used):
    owner = used.get(g)
    return owner is None or owner == rel


def build(spec, info, used):
    files, guids = {}, {}
    for d in (spec["wudDir"], spec["wmdDir"]):
        if not os.path.exists(os.path.join(ROOT, d + ".meta")):
            g = gen.guid_of(spec["salt"], d)
            if not guid_free(g, d, used):
                return None, ["目录 guid 撞车: " + d]
            files[d + ".meta"] = gen.render_folder_meta(g)

    for tier in spec["tiers"]:
        for it in tier["items"]:
            if it.get("common") or it.get("reuse"):
                continue
            owner = "WUD_" + it["asset"]
            g = gen.guid_of(spec["salt"], owner)
            p = "%s/WUD_%s.asset" % (spec["wudDir"], it["asset"])
            if not guid_free(g, p, used):
                return None, ["guid 撞车: " + p]
            guids[owner] = g
            files[p] = render_wud(it, info)
            files[p + ".meta"] = gen.render_meta(g)

    for md in spec["modules"]:
        owner = "WMD_" + md["asset"]
        g = gen.guid_of(spec["salt"], owner)
        p = "%s/WMD_%s.asset" % (spec["wmdDir"], md["asset"])
        if not guid_free(g, p, used):
            return None, ["guid 撞车: " + p]
        guids[owner] = g
        files[p] = render_wmd(md, info)
        files[p + ".meta"] = gen.render_meta(g)

    tiers = []
    for tier in spec["tiers"]:
        row = []
        for it in tier["items"]:
            if it.get("common"):
                row.append(COMMON[it["common"]])
            elif it.get("reuse"):
                row.append(spec["reuse"][it["reuse"]])
            else:
                row.append(guids["WUD_" + it["asset"]])
        tiers.append((tier["key"], row))
    return dict(files=files, tiers=tiers,
                modules=[guids["WMD_" + md["asset"]] for md in spec["modules"]]), []


def render_unique(unique):
    if not unique:
        return "  m_UniqueAttr: []"
    lines = ["  m_UniqueAttr:"]
    for u in unique:
        lines.append("  - name: %s" % gen.q(u["name"]))
        lines.append("    value: %s" % gen.num_text(u["value"]))
        lines.append("    tag: %d" % u["tag"])
    return "\n".join(lines)


def patch_prefab(spec, info, tiers, modules_guids):
    path = os.path.join(ROOT, spec["prefab"])
    with open(path, encoding="utf-8", newline="") as f:
        src = f.read()
    nl = "\r\n" if "\r\n" in src else "\n"
    text = src.replace("\r\n", "\n")
    old = text
    errors = []

    # 1) showAttr
    hexstr = gen.show_attr_hex(spec["showAttr"], info)
    text, n = re.subn(r"(?m)^  showAttr: [0-9a-fA-F]*$", "  showAttr: " + hexstr, text)
    if n != 1:
        errors.append("showAttr 命中 %d 次" % n)

    # 2) m_UniqueAttr（[] 与已有块都要能换）
    pattern = r"^  m_UniqueAttr: \[\]$|^  m_UniqueAttr:\n(?:  - name: [^\n]*\n(?:    [^\n]*\n)+)+"
    repl = render_unique(spec["unique"])
    text, n = re.subn(pattern,
                      lambda mm: repl + ("\n" if mm.group(0).endswith("\n") else ""),
                      text, count=1, flags=re.M)
    if n != 1:
        errors.append("m_UniqueAttr 命中 %d 次" % n)

    # 3) Upgrade 整块
    lines = ["  Upgrade:"]
    for key, row in tiers:
        lines.append("  - Key: %d" % key)
        lines.append("    Value:")
        for g in row:
            lines.append("    - {fileID: 11400000, guid: %s, type: 2}" % g)
    text, n = re.subn(r"(?ms)^  Upgrade:\n.*?(?=^  Modules:)", "\n".join(lines) + "\n", text)
    if n != 1:
        errors.append("Upgrade 块命中 %d 次" % n)

    # 4) Modules + ActiveModule
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


# ---------------------------------------------------------------- 回读

def verify(spec, info):
    path = os.path.join(ROOT, spec["prefab"])
    with open(path, encoding="utf-8", newline="") as f:
        src = f.read()
    rev = {v: k for k, v in info.enum_num.items()}
    ok = True

    hexstr = re.search(r"(?m)^  showAttr: ([0-9a-fA-F]+)$", src).group(1)
    raw = bytes.fromhex(hexstr)
    nums = [int.from_bytes(raw[i:i + 4], "little") for i in range(0, len(raw), 4)]
    print("  showAttr -> %s" % ", ".join(rev.get(n, "?%d" % n) for n in nums))
    if [rev.get(n) for n in nums] != spec["showAttr"]:
        print("  [FAIL] showAttr 与 spec 不一致")
        ok = False

    uniq = re.search(r"(?ms)^  m_UniqueAttr:(.*?)(?=^  Upgrade:)", src).group(1)
    # YAML 里 name 是 \\uXXXX 转义串，直接比转义后的文本
    want = [gen.q(u["name"])[1:-1] for u in spec["unique"]]
    got = re.findall(r'name: "([^"]+)"', uniq)
    if got != want:
        print("  [FAIL] m_UniqueAttr=%s，期望 %s" % (got, want))
        ok = False
    else:
        print("  m_UniqueAttr OK: %s" % ", ".join(u["name"] for u in spec["unique"]))

    body = re.search(r"(?ms)^  Upgrade:\n(.*?)^  Modules:", src).group(1)
    tiers = re.findall(r"  - Key: (\d+)\n    Value:\n((?:    - [^\n]*\n)*)", body)
    print("  Upgrade 档位: %s" % ", ".join("%s(%d项)" % (k, v.count("fileID")) for k, v in tiers))
    if [int(k) for k, _ in tiers] != [t["key"] for t in spec["tiers"]]:
        print("  [FAIL] 档位 Key 与 spec 不一致")
        ok = False

    mods = re.search(r"(?ms)^  Modules:\n((?:  - [^\n]*\n)*)", src).group(1).strip().splitlines()
    print("  Modules: %d（含 None）" % len(mods))
    if len(mods) != len(spec["modules"]) + 1:
        print("  [FAIL] Modules 数量不对")
        ok = False

    seg = src[src.index("  Upgrade:"):src.index("  ActiveModule:")]
    for g in set(re.findall(r"guid: ([0-9a-f]{32})", seg)):
        if not _guid_exists(g):
            print("  [FAIL] prefab 引用的 guid 找不到资产: %s" % g)
            ok = False
    print("VERIFY(%s):" % spec["prefab"].split("/")[-1], "OK" if ok else "FAILED")
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


# ---------------------------------------------------------------- main

def main():
    dry = "--dry-run" in sys.argv
    info = attr_whitelist.load(ROOT)
    if "--verify" in sys.argv:
        all_ok = True
        for spec in SPECS:
            all_ok = verify(spec, info) and all_ok
        return 0 if all_ok else 1

    used = existing_guids()
    for spec in SPECS:
        print("== %s" % spec["prefab"])
        errors, warnings = validate(spec, info)
        for w in warnings:
            print("  [WARN]", w)
        if errors:
            for e in errors:
                print("  [ERROR]", e)
            print("RESULT: FAILED（未写入）")
            return 1

        built, errs = build(spec, info, used)
        if errs:
            for e in errs:
                print("  [ERROR]", e)
            return 1

        for rel, text in sorted(built["files"].items()):
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

        pf = patch_prefab(spec, info, built["tiers"], built["modules"])
        if pf["errors"]:
            for e in pf["errors"]:
                print("  [ERROR] prefab:", e)
            return 1
        if pf["changed"]:
            print("  [%s] %s" % ("DIFF" if dry else "WRITE", spec["prefab"]))
            if not dry:
                shutil.copyfile(pf["path"], pf["path"] + ".bak")
                with open(pf["path"], "w", encoding="utf-8", newline="") as f:
                    f.write(pf["new"].replace("\n", pf["nl"]))
        else:
            print("  [SAME] prefab 无需改动")
        print("  tiers=%d modules=%d" % (len(built["tiers"]), len(built["modules"])))

    print("RESULT:", "OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
