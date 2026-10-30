# -*- coding: utf-8 -*-
"""CWC（科莱特微波炉 / Colette Wave Cooker）整把枪的配置生成器。

来源与对标：
  * 数值与 13 个升级 + 6 个模组全部对标 DRG 的 Colette Wave Cooker
    （`.codebuddy/notes/drg-weapons-upgrades.json` / wiki.gg）。
  * 本工程没有"光束宽度 / 温度球 / 状态扩散"这些机制，凡是映射不到的都在下面的
    ADAPT 注释里写明换成了哪个属性（宁可说得清，也不要留"能看见但不生效"的 Special）。

这份脚本做三件事（幂等，重复跑是 no-op）：
  1. prefab 里**什么都没有** ⇒ 注入 GunMuzzle 节点 + WeaponPlayerController 组件（含全部基础配置）。
  2. 生成 WUD_/WMD_ 资产 + .meta（guid 由 salt 派生，撞车会拒写）。
  3. 回填 prefab 的 cfg / showAttr / Upgrade / Modules / ActiveModule。

跑法：
    python .codebuddy/plans/gen_cwc_tree.py --dry-run
    python .codebuddy/plans/gen_cwc_tree.py
    python .codebuddy/plans/gen_cwc_tree.py --verify
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

PREFAB = "Assets/Resources/Weapons/CWC.prefab"
WUD_DIR = "Assets/Resources/GameData/WeaponUpgrade/CWC"
WMD_DIR = "Assets/Resources/GameData/WeaponModule/CWC"
SALT = "Bluedivers.CWC"
WMD_NONE = "a19bae1a0785866409ae31f377bde9cf"
ROLE_ASSET = "Assets/Resources/GameData/Role/RD_Hare.asset"

# ---- prefab 里注入用的新 fileID（必须与文件内现有 id 不冲突，脚本会断言）----
ID_MUZZLE_GO = 8143529679005128674
ID_MUZZLE_TR = 4590018445078361226
ID_CONTROLLER = -6729451863174521330

# 根目录通用池：多把枪都能用的收益只留那一份（SKILL §4.1）
COMMON = {
    "Damage": "0a01454bb348bcd40a0e10403c33c443",   # 直伤 Factor +0.3
}

# 复用的外部资源（激光光束 / 命中特效 / 准星 / 连续射击音效）
# 激光必须用 Pro_P_Laser（ProjectilePlayerLaser：LineRenderer + 每 ShootInterval 结算一次伤害）；
# 普通球形弹在 Laser 模式下只在起手生成一颗，其余时间光束是"打空枪"。
BULLET_PREFAB = "{fileID: -7807231914088266234, guid: 06542f76b95ea874f929fd17392f74bc,\n      type: 3}"   # Pro_P_Laser
BULLET_SPHERE = "{fileID: 6427322605795972283, guid: 38aa19c9c455044469210cff8480ccb7,\n      type: 3}"   # Pro_Sphere_Blue（改装成激光前用的）
IMPACT_VFX = "{fileID: 7655095426451682942, guid: c0b2ad8985e3e6d40bb13d04f56c6649,\n      type: 3}"
IMPACT_SFX = "{fileID: 8300000, guid: dc0335c8764cec842b7e0fa572fc0a61, type: 3}"
MUZZLE_FLASH = "{fileID: 748781637555137343, guid: a342e37561382d744ba2a23497329771,\n    type: 3}"
SIGHT = "{fileID: 1123618372036048713, guid: b72539764b0dbef469852ef17c048765, type: 3}"
SFX_START = "{fileID: 8300000, guid: dff37a303c908e846b3137a637cbbf7a,\n    type: 3}"
SFX_LOOP = "{fileID: 8300000, guid: 62e9686756f5a394db66ca01ce110bfe,\n    type: 3}"
SFX_END = "{fileID: 8300000, guid: b2804c2e99f02d54c9cd8b36ab673f9e,\n    type: 3}"
CHANGE_SFX = "{fileID: 8300000, guid: 7b83d61917ac1224f942a0609b1221f2, type: 3}"

# ------------------------------------------------------------------ 基础数值
# DRG 实测（wiki）：伤害 7 · 弹匣 300 · 射速 7/s · 冷却 3/s · 满热 3.1 · 过热 2s · 束宽 1.2m
# 本工程换算：热量槽固定 100 ⇒ 3.1 单位 = 100，1 单位/秒 ≈ 32 热量/秒，7 发/秒 ⇒ 每发 4.5
# 激光口径（2026-10-30 改）：ShootInterval 同时是**伤害节拍**（ProjectileLaser.ShootSpeed 读的就是它）
#   ⇒ DPS = 直伤 / 间隔（与自动射击同口径），热量 = 每个伤害节拍一份 HeatPerShot（32 热/秒）
#   ⇒ 弹药也是每个伤害节拍扣 1（UpdateLaser 被 AllowShoot 卡着）⇒ 300 发 ≈ 42 秒光束，与 DRG 300/7≈43 秒吻合
CFG = [
    ("Ammo", 0),                # 热量武器没有后备弹量，发数全进能量池
    ("Magazine", 300),          # DRG 单一弹池 300 ⇒ 约 42 秒连续光束
    ("ShootInterval", 0.14),    # 伤害节拍 7/s（也是热量/弹药的结算节拍）
    ("BulletsSpreadAngle", 2),  # 束宽 1.2m 的近似
    ("HeatPerShot", 4.5),       # 每个伤害节拍一份 ⇒ 32 热/秒，≈3.1s 满热
    ("CoolDelay", 0.25),
    ("CoolSpeed", 100),         # DRG 3/s ⇒ 满热约 1s 退完
    ("OverheatDuration", 2),    # DRG 2s
    ("Recoil", 1),
    ("NoiseScale", 1),
]

SHOOT_TYPE = 3     # Laser：持续光束，热量由 ApplyHeatTick 按逻辑帧折算
WEAPON_FLAG = 2    # NoManualReload：能量池打空靠补给，不手动换弹（同 ArcPistol）

# 换激光时要改的整块：普通球形弹 → 激光束；激光靠 SphereCast 结算，必须给真实射程
SWAPS = [
    ("  - BulletPrefab: " + BULLET_SPHERE, "  - BulletPrefab: " + BULLET_PREFAB),
    ("    MaxRange: -1\n", "    MaxRange: 120\n"),
]

SHOW_ATTR = ["DirectDamage", "ShootInterval", "Magazine", "HeatPerShot", "CoolDelay", "CoolSpeed",
             "OverheatDuration", "ExplosionDamage", "ExplosionRange", "BulletsSpreadAngle", "DirectAP",
             "VulnerabilityToxicity", "VulnerabilityBurn", "VulnerabilityFreeze"]

WUD_DESC = (
    "工业级的微波发生装置，本来是用来给矿洞里的岩层加热的。\r\n"
    "现在它被指向了会动的东西。效果一样显著，只是味道差了一点。"
)

RENAME = [("能量储量", "Magazine"), ("射速", "ShootInterval")]


def m(attr, modifier, value):
    return dict(attr=attr, modifier=modifier, value=value)


# ------------------------------------------------------------------ 升级树
# 档位 Key = 解锁等级（副武器节奏 2/6/10/14/18，与 Rationality 一致）
# 每档 ≤3（buyIndex 只有 3 位）；WUD 只准强化，代价全部落到模组
TIERS = [
    {"key": 2, "items": [
        # DRG T1 Convex Lens：正向收益，与根目录通用 WUD_Damage 内容一致 ⇒ 直接复用
        dict(common="Damage"),
        dict(asset="MagnetronTube", name="磁控管", category="能量", icon="Icon_Upgrade_Capacity.tga", cost=1000,
             desc="换上了规格更大的磁控管，能量池能多存一截。\r\n"
                  "它不负责把多出来的那点热也顺手带走，别指望。",
             modifys=[m("Magazine", "Base", 100)]),
        # DRG T1 Concave Lens（束宽×3）：本工程没有"束宽"，映射成命中点的崩解范围/伤害
        dict(asset="ConcaveLens", name="凹透镜", category="崩解", icon="Icon_Upgrade_Area.tga", cost=1000,
             desc="把聚焦镜换成凹面之后，微波在命中点散得更开，一次能扫到一小片。\r\n"
                  "工程部管这叫覆盖优化。你只需要知道它炸得更好看。",
             modifys=[m("ExplosionRange", "Base", 0.6), m("ExplosionDamage", "Base", 2)]),
    ]},
    {"key": 6, "items": [
        dict(asset="HeatSink", name="散热鳍片", category="散热", icon="Icon_Upgrade_Cooling.tga", cost=1500,
             desc="枪身两侧加了一排散热鳍片，同样的输出产生的热量更低。\r\n"
                  "刚停火的时候别用手摸，那不叫温的。",
             modifys=[m("HeatPerShot", "Factor", -0.2)]),
        dict(asset="LargerPowerSupply", name="大功率电源", category="射速", icon="Icon_Upgrade_RateOfFire.tga", cost=1500,
             desc="供能单元换了功率更大的一档，扣扳机的手感明显跟得上了。\r\n"
                  "电池仓比原来鼓一圈，握久了会硌手。",
             modifys=[m("ShootInterval", "Factor", -0.25)]),
        dict(asset="ThermoelectricCooler", name="热电冷却器", category="散热", icon="Icon_Upgrade_TemperatureCoolDown.tga", cost=1500,
             desc="塞进一节热电冷却器，停火之后热量退得更快，过热罚站也短了一截。\r\n"
                  "它自己发的那点热没算进总账，账是工程部记的。",
             modifys=[m("CoolSpeed", "Factor", 0.5), m("OverheatDuration", "Factor", -0.25)]),
    ]},
    {"key": 10, "items": [
        # DRG T3 Densification Ray（命中减速）：本工程没有减速 debuff ⇒ 换成"射线继续掠到下一个目标"
        dict(asset="DensificationRay", name="致密化射线", category="穿透", icon="Icon_Upgrade_BulletPenetration.tga", cost=2000,
             desc="微波被压得更紧，穿过一个目标之后还会继续掠到旁边那个。\r\n"
                  "为什么偏偏是旁边那个，问就是波的脾气。",
             modifys=[m("Catapult", "Extra", 1)]),
        # DRG T3 Temperature Amplifier：靶子身上已经有温度时再吃一层乘区
        dict(asset="TemperatureAmplifier", name="温度放大器", category="状态", icon="Icon_Upgrade_Temperature.tga", cost=2000,
             desc="对已经在烧、或者已经被冻住的目标，这套放大器能把伤害再往上顶一档。\r\n"
                  "数据是工程部给的，样本是它们自己提供的。",
             modifys=[m("VulnerabilityBurn", "Base", 0.25), m("VulnerabilityFreeze", "Base", 0.25)]),
    ]},
    {"key": 14, "items": [
        dict(asset="WideLens", name="广角镜组件", category="崩解", icon="Icon_Upgrade_Area 1.tga", cost=2500,
             desc="广角镜再把束宽摊开一点，崩解的那一圈更大，围上来的也吃得到。\r\n"
                  "视角没变，是你打得更松了。",
             modifys=[m("ExplosionRange", "Base", 0.35), m("ExplosionDamage", "Base", 2)]),
        dict(asset="PowerSupplyOverdrive", name="供能超载", category="射速", icon="Icon_Upgrade_SpeedUp.tga", cost=2500,
             desc="把供能单元推过额定线，射速直接上一个台阶。\r\n"
                  "超出的部分不进保修范围，工程部连表格都懒得填。",
             modifys=[m("ShootInterval", "Factor", -0.3)]),
    ]},
    {"key": 18, "items": [
        dict(asset="ContagionTransmitter", name="传染发射器", category="状态", icon="Icon_Upgrade_Poison.tga", cost=3000,
             desc="对已经中毒、被腐蚀的目标，微波能把伤害推到它们最难受的位置。\r\n"
                  "扩散出去的东西归医疗部管，工程部只负责发射。",
             modifys=[m("VulnerabilityToxicity", "Base", 0.3)]),
        dict(asset="BoilerRay", name="锅炉射线", category="崩解", icon="Icon_Upgrade_Splash.tga", cost=3000,
             desc="命中点会真正炸开，崩解伤害和作用范围都上了一大截。\r\n"
                  "说明书上写着请勿在密闭空间使用。我们这里基本没有密闭空间。",
             modifys=[m("ExplosionDamage", "Base", 8), m("ExplosionRange", "Base", 0.6)]),
        dict(asset="ExothermicReactor", name="放热反应堆", category="状态", icon="Icon_Upgrade_TemperatureShock.tga", cost=3000,
             desc="对燃烧或冰冻的目标，这套反应堆能让整笔伤害更有分量。\r\n"
                  "温度被强行拉平的那一刻，它们会明白发生了什么。",
             modifys=[m("VulnerabilityBurn", "Base", 0.3), m("VulnerabilityFreeze", "Base", 0.3)]),
    ]},
]

# ------------------------------------------------------------------ 模组
# ⚠ 每个模组的效果行 ≤4（SelectRoleWnd.moduleLayout 只有 4 个坑，多了静默丢弃）
MODULES = [
    dict(asset="LiquidCooling", name="液态冷却系统", quality="Clean", icon="Icon_Upgrade_Cooling.tga",
         describe="把枪管泡进一套液态回路里。凉是凉了，但每次打完都能听见它在里面咕嘟。",
         desc=[(True, "散热速度提升"), (True, "单发热量略微下降"), (True, "过热恢复更快")],
         modifys=[m("CoolSpeed", "Factor", 0.2), m("HeatPerShot", "Factor", -0.1),
                  m("OverheatDuration", "Factor", -0.1)]),
    dict(asset="SuperFocusLens", name="超级聚焦透镜", quality="Balanced", icon="Icon_Upgrade_Focus.tga",
         describe="镜片被磨到只剩一个焦点，打出去的东西更狠，也更窄。工程部管这叫专注。",
         desc=[(True, "直击伤害大幅提升"), (False, "崩解范围明显缩小")],
         modifys=[m("DirectDamage", "Factor", 0.5), m("ExplosionRange", "Factor", -0.35)]),
    dict(asset="DiffusionRay", name="扩散射线", quality="Balanced", icon="Icon_Upgrade_BulletPenetration.tga",
         describe="射线不再在第一个目标身上停下，而是顺着最近的缝隙继续走。代价是每一段都薄了一点。",
         desc=[(True, "射线命中后继续穿向附近目标"), (False, "直击伤害下降")],
         modifys=[m("Catapult", "Extra", 2), m("DirectDamage", "Base", -3)]),
    dict(asset="MegaPowerSupply", name="巨型供能", quality="Unstable", icon="Icon_Upgrade_Magazine_Size.tga",
         describe="整块供能单元被换成了货舱级的规格，射速和储量一起暴涨。代价是这玩意儿真的会烫。",
         desc=[(True, "能量储量大幅提升"), (True, "射速显著提升"),
               (False, "散热速度大幅下降"), (False, "过热恢复更慢")],
         modifys=[m("Magazine", "Base", 150), m("ShootInterval", "Factor", -0.3),
                  m("CoolSpeed", "Factor", -0.45), m("OverheatDuration", "Factor", 0.5)]),
    dict(asset="BlisteringNecrosis", name="起泡坏死", quality="Unstable", icon="Icon_Upgrade_Heat.tga",
         describe="过载的微波会在目标表面烧出连片的水疱。伤得更重，也热得更快，你得学会打一阵、停一阵。",
         desc=[(True, "直击伤害大幅提升"), (False, "每发热量显著增加"), (False, "散热速度下降")],
         modifys=[m("DirectDamage", "Factor", 0.45), m("HeatPerShot", "Factor", 0.6),
                  m("CoolSpeed", "Factor", -0.25)]),
    dict(asset="GammaContamination", name="伽马污染", quality="Unstable", icon="Icon_Upgrade_ChemicalExplosion.tga",
         describe="把射线的频谱推到伽马段，命中点会留下持续崩解的污染。代价是枪本身被削了一圈。",
         desc=[(True, "崩解伤害与范围大幅提升"), (False, "直击伤害下降"), (False, "能量储量减少")],
         modifys=[m("ExplosionDamage", "Base", 8), m("ExplosionRange", "Base", 0.4),
                  m("DirectDamage", "Base", -3), m("Magazine", "Base", -100)]),
]


# ------------------------------------------------------------------ prefab 注入
def damage_block():
    """BeamRifle 的 Damages[0] 结构 + CWC 的数值/成分（燃烧 50% + 辐射 50%，DRG 原设定）。"""
    return """  Damages:
  - BulletPrefab: """ + BULLET_PREFAB + """
    LandingPrefab: {fileID: 0}
    Speed: 70
    Gravity: 0
    InheritWeaponSpeed: 0
    MaxLifeTime: 1.6
    MaxRange: 120
    MinRange: 1
    NoSource: 0
    SoundRadius: 20
    ImpactSoundRadius: 8
    DamageExplosion: 4
    DamageDirect: 14
    DamageGroupDirect:
    - Key: 5
      Value: 0.5
    - Key: 10
      Value: 0.5
    WeaknessBonus: 0
    ExplosionInnerRange: 0.4
    ExplosionRange: 0.9
    DestructeRadius: 0
    ShockwaveRadius: 0
    ShockwaveForce: 10
    DamageGroupExplosion:
    - Key: 5
      Value: 1
    AboVulnerabilityGroup:
    - Key: 4
      Value: 1
    - Key: 5
      Value: 1
    - Key: 6
      Value: 1
    UseCollisionDirection: 1
    ImpactVfxSpawnOffset: 0.1
    ImpactVfx: """ + IMPACT_VFX + """
    ImpactSfx: """ + IMPACT_SFX + """
    UseHole: 0
    Hole: {fileID: 0}
    OnlyTerrain: 0
    DirectAP: 1
    DirectAPChargeScale: 1
    ExplosionAP: 0
    ExplosionAPChargeScale: 1
    demolishValue: 0
    UseCharge: 0
    ChargeDamageScale: 1
    ChargeAOERangeScale: 1
    ChargeSpeedScale: 1
    ChargeGravityScale: 1
    ChargeSoundScale: 1
    ChargeHeatScale: 1
    ChargeSpreadScale: 1
"""


def controller_block(info):
    cfg_lines = "".join("    - Key: %d\n      Value: %s\n" % (info.num(n), gen.num_text(v)) for n, v in CFG)
    rename = "".join('  - name: %s\n    type: %d\n    addTag: 0\n    removeTag: 0\n'
                     % (gen.q(n), info.num(t)) for n, t in RENAME)
    return """--- !u!114 &%d
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 5488129417787703396}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: e03ea2fb50b06ab468194bc5b83375d6, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  WeaponRoot: {fileID: 2493733636534413584}
  UseManyMuzzle: 0
  WeaponMuzzle: {fileID: %d}
  WeaponMuzzle2: {fileID: 0}
  WeaponManyMuzzles: []
  cfg:
    _fakeAttrs:
%s  BulletFlag: 0
%s  UseDamageIndex: 0
  MuzzleFlashPrefab: %s
  FlashSizeScale: 1
  UnparentMuzzleFlash: 0
  SFXRange: 50
  FireNoiseRadius: 30
  LastMuzzleIndex: 0
  ShootSfx: {fileID: 0}
  UseContinuousShootSound: 1
  ContinuousShootStartSfx: %s
  ContinuousShootLoopSfx: %s
  ContinuousShootEndSfx: %s
  _currentHeat: 0
  wantsToShoot: 0
  RandomSeed: 0
  showAmmo: 0
  showInReload: 0
  showReloadTime: 0
  showCool: 0
  ShootType: %d
  WeaponFlag: %d
  AutomaticReleaseOnCharged: 0
  Lockshape: 0
  ReloadSfx: {fileID: 0}
  ChangeWeaponSfx: %s
  ChargeVfx: {fileID: 0}
  WeaponAnimator: {fileID: 0}
  PlayerIndex: 0
  WeaponName: %s
  WeaponType: %s
  WeaponTypeEnum: 1
  WeaponIcon: {fileID: 0}
  Sight: %s
  ScopeGo: {fileID: 0}
  LHand: {fileID: 6725602698128973255}
  RHand: {fileID: 390647978284266955}
  ShowRoot: {fileID: 8396473177785606159}
  RecoilForce: 0.35
  RecoilProfile:
    PitchKick: 0.5
    YawKick: 0.3
    YawStep: 0.06
    YawRandom: 0.35
    KickSharpness: 50
    RecoveryDelay: 0.06
    RecoverySpeed: 10
    YawRecoveryRatio: 0.7
    MaxPitch: 12
    MaxYaw: 5
    AdsScale: 0.8
    WeaponKickBack: 0.025
    WeaponKickPitch: 1
    WeaponKickRoll: 0.8
    WeaponSpring: 18
    SpreadBloomPerShot: 0.08
    SpreadBloomMax: 2
    SpreadBloomDecay: 10
    ShakePerShot: 0.02
  AimingHideCrosshair: 1
  AimZoomRatio: 0.6
  AimOffset: {x: -0.05, y: 0.12, z: 0}
  playerAngle: 0
  _showAttrInfo: 
  desc: %s
  m_RenameAttr:
%s  showAttr: 
  m_UniqueAttr: []
  Upgrade:
  - Key: 2
    Value:
    - {fileID: 11400000, guid: a19bae1a0785866409ae31f377bde9cf, type: 2}
  Modules:
  - {fileID: 11400000, guid: %s, type: 2}
  ActiveModule: {fileID: 11400000, guid: %s, type: 2}
""" % (ID_CONTROLLER, ID_MUZZLE_TR, cfg_lines, damage_block(), MUZZLE_FLASH, SFX_START, SFX_LOOP, SFX_END,
       SHOOT_TYPE, WEAPON_FLAG, CHANGE_SFX, gen.q("科莱特微波炉"), gen.q("重型武器"), SIGHT, gen.q(WUD_DESC),
       rename, WMD_NONE, WMD_NONE)


MUZZLE_BLOCK = """--- !u!1 &%d
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: %d}
  m_Layer: 0
  m_Name: GunMuzzle
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &%d
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0.375}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 1916352467902790717}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
""" % (ID_MUZZLE_GO, ID_MUZZLE_TR, ID_MUZZLE_TR, ID_MUZZLE_GO)

GUNROOT_CHILDREN_OLD = """  m_Children:
  - {fileID: 1358937586967691109}
  - {fileID: 6942944788152594732}
  - {fileID: 4131763164992525731}
  m_Father: {fileID: 3116627713775205873}"""

ROOT_COMPONENTS_OLD = """  m_Component:
  - component: {fileID: 3116627713775205873}"""


def inject_component(text, info):
    """还没加过组件时：注入 GunMuzzle + WeaponPlayerController。"""
    if "guid: e03ea2fb50b06ab468194bc5b83375d6" in text:
        return text, ["组件已存在，跳过注入"]
    notes = []
    for cid in (str(ID_MUZZLE_GO), str(ID_MUZZLE_TR), str(ID_CONTROLLER)):
        if re.search(r"^--- !u!\d+ &%s$" % re.escape(cid), text, re.M):
            return text, ["新 fileID %s 与文件内已有 id 冲突 ⇒ 换一组" % cid]

    if text.count(GUNROOT_CHILDREN_OLD) != 1:
        return text, ["GunRoot.m_Children 锚点命中 %d 次（应为 1）" % text.count(GUNROOT_CHILDREN_OLD)]
    text = text.replace(GUNROOT_CHILDREN_OLD,
                        GUNROOT_CHILDREN_OLD.replace("  m_Father: {fileID: 3116627713775205873}",
                                                     "  - {fileID: %d}\n  m_Father: {fileID: 3116627713775205873}"
                                                     % ID_MUZZLE_TR), 1)

    if text.count(ROOT_COMPONENTS_OLD) != 1:
        return text, ["根对象 m_Component 锚点命中 %d 次（应为 1）" % text.count(ROOT_COMPONENTS_OLD)]
    text = text.replace(ROOT_COMPONENTS_OLD,
                        ROOT_COMPONENTS_OLD + "\n  - component: {fileID: %d}" % ID_CONTROLLER, 1)

    text = text.rstrip("\n") + "\n" + MUZZLE_BLOCK + controller_block(info)
    notes.append("已注入 GunMuzzle（z=0.375，模型透镜前缘）与 WeaponPlayerController")
    return text, notes


# ------------------------------------------------------------------ 资产
def icon_guid(filename):
    p = os.path.join(ROOT, "Assets/Resources/Images/SkillIcon/DRG", filename + ".meta")
    with open(p, encoding="utf-8") as f:
        for line in f:
            if line.startswith("guid: "):
                return line.strip()[6:]
    raise SystemExit("图标没有 guid: %s" % filename)


def render_wmd(md, info):
    """在 gen 的 render_wmd 基础上补 describe（悬浮气泡的风味文案）。"""
    lines = gen.render_wmd(md["asset"], md["name"], md["quality"], icon_guid(md["icon"]),
                           [dict(good=g_, text=t) for g_, t in md["desc"]], md["modifys"], info).splitlines()
    out = []
    for ln in lines:
        out.append(ln)
        if ln.startswith("  name: "):
            out.append("  describe: %s" % gen.q(md["describe"]))
    return "\n".join(out) + "\n"


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


def build(info, used):
    files, guids, errors = {}, {}, []
    for d in (WUD_DIR, WMD_DIR):
        if not os.path.exists(os.path.join(ROOT, d + ".meta")):
            g = gen.guid_of(SALT, d)
            if not guid_free(g, d, used):
                errors.append("目录 guid 撞车: " + d)
            files[d + ".meta"] = gen.render_folder_meta(g)

    for tier in TIERS:
        for it in tier["items"]:
            if "common" in it:
                continue
            owner = "WUD_" + it["asset"]
            g = gen.guid_of(SALT, owner)
            p = "%s/WUD_%s.asset" % (WUD_DIR, it["asset"])
            if not guid_free(g, p, used):
                errors.append("guid 撞车: " + owner)
            guids[owner] = g
            files[p] = gen.render_wud(it["asset"], it["name"], it["category"], icon_guid(it["icon"]),
                                      it["desc"], it["cost"], it["modifys"], info)
            files[p + ".meta"] = gen.render_meta(g)

    for md in MODULES:
        owner = "WMD_" + md["asset"]
        g = gen.guid_of(SALT, owner)
        p = "%s/WMD_%s.asset" % (WMD_DIR, md["asset"])
        if not guid_free(g, p, used):
            errors.append("guid 撞车: " + owner)
        guids[owner] = g
        files[p] = render_wmd(md, info)
        files[p + ".meta"] = gen.render_meta(g)

    tiers = []
    for tier in TIERS:
        row = []
        for it in tier["items"]:
            row.append(COMMON[it["common"]] if "common" in it else guids["WUD_" + it["asset"]])
        tiers.append((tier["key"], row))
    return dict(files=files, tiers=tiers,
                modules=[guids["WMD_" + md["asset"]] for md in MODULES]), errors


# ------------------------------------------------------------------ prefab 补丁
def patch_prefab(text, info, tiers, modules_guids):
    errors = []

    # 0) 换弹种：普通球形弹 → 激光束；MaxRange 从 -1 改成真实米数（SphereCast 用 -1 等于打不到东西）
    for old, new in SWAPS:
        if old in text:
            text = text.replace(old, new, 1)

    block = "    _fakeAttrs:\n" + "".join(
        "    - Key: %d\n      Value: %s\n" % (info.num(n), gen.num_text(v)) for n, v in CFG)
    text, n = re.subn(r"(?m)^    _fakeAttrs:\n(?:    - Key: \d+\n      Value: [^\n]*\n)*", block, text)
    if n != 1:
        errors.append("cfg._fakeAttrs 命中 %d 次" % n)

    for field, val in (("ShootType", SHOOT_TYPE), ("WeaponFlag", WEAPON_FLAG)):
        text, n = re.subn(r"(?m)^  %s: \d+$" % field, "  %s: %d" % (field, val), text)
        if n != 1:
            errors.append("%s 命中 %d 次" % (field, n))

    hexstr = gen.show_attr_hex(SHOW_ATTR, info)
    text, n = re.subn(r"(?m)^  showAttr: [0-9a-fA-F]*$", "  showAttr: " + hexstr, text)
    if n != 1:
        errors.append("showAttr 命中 %d 次" % n)

    lines = ["  Upgrade:"]
    for key, row in tiers:
        lines.append("  - Key: %d" % key)
        lines.append("    Value:")
        for g in row:
            lines.append("    - {fileID: 11400000, guid: %s, type: 2}" % g)
    text, n = re.subn(r"(?ms)^  Upgrade:\n.*?(?=^  Modules:)", "\n".join(lines) + "\n", text)
    if n != 1:
        errors.append("Upgrade 块命中 %d 次" % n)

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

    return text, errors


def patch_role():
    """把 CWC 加进 RD_Hare 的副武器列表（不然这把枪在任何角色身上都选不到）。"""
    p = os.path.join(ROOT, ROLE_ASSET)
    with open(p, encoding="utf-8", newline="") as f:
        src = f.read()
    nl = "\r\n" if "\r\n" in src else "\n"
    text = src.replace("\r\n", "\n")
    entry = "      - {fileID: %d, guid: e3ab7508fb5bb1f49944f9eb102aa239, type: 3}" % ID_CONTROLLER
    if "guid: e3ab7508fb5bb1f49944f9eb102aa239" in text:
        return dict(path=p, new=text, nl=nl, changed=False, errors=[])
    anchor = """    - Key: 1
      Value:
      - {fileID: 7364469512513166795, guid: 784aceafffb38064eaf4ca873c04af06, type: 3}"""
    if text.count(anchor) != 1:
        return dict(path=p, new=text, nl=nl, changed=False,
                    errors=["RD_Hare 副武器锚点命中 %d 次" % text.count(anchor)])
    text = text.replace(anchor, anchor + "\n" + entry, 1)
    return dict(path=p, new=text, nl=nl, changed=text != src, errors=[])


# ------------------------------------------------------------------ 校验
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
        if len(tier["items"]) > 3:
            errors.append("Key=%s 超过 3 项" % tier["key"])
        for it in tier["items"]:
            if "common" in it:
                continue
            mods_of("WUD_" + it["asset"], it["modifys"], True)

    for md in MODULES:
        mods_of("WMD_" + md["asset"], md["modifys"], False)
        goods = sum(1 for g_, _ in md["desc"] if g_)
        if md["quality"] == "Clean" and goods != len(md["desc"]):
            errors.append("%s 是 Clean 却写代价文案" % md["asset"])
        if md["quality"] != "Clean" and goods == len(md["desc"]):
            errors.append("%s 是 %s 却只有增益" % (md["asset"], md["quality"]))
        if len(md["desc"]) > 4:
            errors.append("%s 效果行 %d 条，UI 只显示 4 条" % (md["asset"], len(md["desc"])))

    for n in SHOW_ATTR:
        if n not in cfg_names and n not in info.auto_fill:
            errors.append("showAttr 里的 `%s` 既不在 cfg、也不由 InitAttribute 补" % n)
    return errors, warnings


def verify(info):
    with open(os.path.join(ROOT, PREFAB), encoding="utf-8") as f:
        src = f.read()
    ok = True
    rev = {v: k for k, v in info.enum_num.items()}

    cfg = attr_whitelist.parse_prefab_cfg(os.path.join(ROOT, PREFAB))
    want = {info.num(n): v for n, v in CFG}
    if cfg != want:
        print("  [FAIL] cfg 与 spec 不一致\n    disk: %s\n    spec: %s" % (cfg, want))
        ok = False
    else:
        shots = 100.0 / want[info.num("HeatPerShot")]
        print("  cfg OK；HeatPerShot=%s ⇒ 约 %.1f 发满热（第 %d 发过热）"
              % (want[info.num("HeatPerShot")], shots, int(shots) + 1))

    for field, val in (("ShootType", SHOOT_TYPE), ("WeaponFlag", WEAPON_FLAG)):
        mt = re.search(r"(?m)^  %s: (\d+)$" % field, src)
        got = int(mt.group(1)) if mt else -1
        print("  %s = %d %s" % (field, got, "" if got == val else "  [FAIL]"))
        ok = ok and got == val

    mb = re.search(r"(?m)^  - BulletPrefab: \{fileID: (-?\d+), guid: ([0-9a-f]{32}),", src)
    gu = re.search(r"(?m)^    MaxRange: (-?\d+)$", src)
    bpath = AssetDatabasePath(mb.group(2)) if mb else "?"
    print("  BulletPrefab -> %s (%s)  MaxRange=%s" % (bpath, mb.group(2) if mb else "?", gu.group(1) if gu else "?"))
    ok = ok and mb is not None and gu is not None and gu.group(1) != "-1"

    mt = re.search(r"(?m)^  showAttr: ([0-9a-fA-F]*)$", src)
    raw = bytes.fromhex(mt.group(1)) if mt else b""
    nums = [int.from_bytes(raw[i:i + 4], "little") for i in range(0, len(raw), 4)]
    print("  showAttr -> %s" % ", ".join(rev.get(n, "?%d" % n) for n in nums))

    body = re.search(r"(?ms)^  Upgrade:\n(.*?)^  Modules:", src).group(1)
    tiers = re.findall(r"  - Key: (\d+)\n    Value:\n((?:    - [^\n]*\n)*)", body)
    print("  Upgrade 档位: %s" % ", ".join("%s(%d项)" % (k, v.count("fileID")) for k, v in tiers))
    print("  Modules: %d（含 None）" % len(re.search(r"(?ms)^  Modules:\n((?:  - [^\n]*\n)*)", src).group(1).strip().splitlines()))

    seg = src[src.index("  Upgrade:"):src.index("  ActiveModule:")]
    for g in set(re.findall(r"guid: ([0-9a-f]{32})", seg)):
        if not _guid_exists(g):
            print("  [FAIL] prefab 引用的 guid 找不到资产: %s" % g)
            ok = False

    for field in ("WeaponMuzzle", "WeaponRoot", "LHand", "RHand", "ShowRoot"):
        mt = re.search(r"(?m)^  %s: \{fileID: (-?\d+)\}$" % field, src)
        print("  %s -> %s" % (field, mt.group(1) if mt else "缺失"))
        ok = ok and mt is not None and mt.group(1) != "0"

    print("VERIFY:", "OK" if ok else "FAILED")
    return 0 if ok else 1


def AssetDatabasePath(guid):
    """guid -> 资产相对路径（只读 meta 前 400 字节，不依赖 Unity）"""
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
        for f in files:
            if not f.endswith(".meta"):
                continue
            try:
                with open(os.path.join(dirpath, f), encoding="utf-8", errors="replace") as fh:
                    if ("guid: %s" % guid) in fh.read(400):
                        return os.path.relpath(os.path.join(dirpath, f[:-5]), ROOT).replace("\\", "/")
            except OSError:
                pass
    return "<未知 guid>"


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


# ------------------------------------------------------------------ main
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

    # prefab
    pf_path = os.path.join(ROOT, PREFAB)
    with open(pf_path, encoding="utf-8", newline="") as f:
        raw = f.read()
    text = raw.replace("\r\n", "\n")
    text, notes = inject_component(text, info)
    for n in notes:
        print("  [NOTE]", n)
    if any("锚点" in n or "冲突" in n for n in notes):
        print("RESULT: FAILED（prefab 注入失败）")
        return 1
    text, perrs = patch_prefab(text, info, built["tiers"], built["modules"])
    if perrs:
        for e in perrs:
            print("  [ERROR] prefab:", e)
        return 1
    if text != raw.replace("\r\n", "\n"):
        print("  [%s] %s" % ("DIFF" if dry else "WRITE", PREFAB))
        if not dry:
            shutil.copyfile(pf_path, pf_path + ".bak")
            with open(pf_path, "w", encoding="utf-8", newline="") as f:
                f.write(text)
    else:
        print("  [SAME] prefab 无需改动")

    # 角色副武器列表
    role = patch_role()
    if role["errors"]:
        for e in role["errors"]:
            print("  [ERROR] role:", e)
        return 1
    if role["changed"]:
        print("  [%s] %s（追加副武器）" % ("DIFF" if dry else "WRITE", ROLE_ASSET))
        if not dry:
            shutil.copyfile(role["path"], role["path"] + ".bak")
            with open(role["path"], "w", encoding="utf-8", newline="") as f:
                f.write(role["new"].replace("\n", role["nl"]))
    else:
        print("  [SAME] RD_Hare 无需改动")

    print("资产: 一致 %d / 变更 %d；tiers=%s modules=%d"
          % (same, new, [len(t["items"]) for t in TIERS], len(MODULES)))
    print("RESULT:", "OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
