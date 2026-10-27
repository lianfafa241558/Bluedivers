# -*- coding: utf-8 -*-
"""一次性：把 Logic 的 12 WUD / 7 WMD 描述改写为诙谐风格，直接改 spec。

WUD.desc = 卡面 tooltip，可整段风味；
WMD.desc = 绿↑/红↓ 效果行，必须保留增减语义，只换措辞。
"""
import io
import json
import os

SPEC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "skills/weapon-upgrade/scripts/examples/Logic.json")

WUD = {
    "GyroStabilisation": "给枪管装了一套陀螺仪。它不会让子弹飞得更快，只是让它们别再到处乱跑。对一把双持冲锋枪来说，这已经算奇迹。",
    "SuperchargedFeed": "供弹机构被调到「再快一点就要出事」的档位。工程师说这在安全范围内，我们选择相信他。",
    "QuickfireEjector": "经验、训练，外加那么亿丢丢见不得光的私下改动，让换弹快了将近一秒。",
    "IncreasedCaliberRounds": "弹头加大了一圈。后坐力变得更诚实，伤害也是。",
    "ExpandedAmmoBags": "优香的口袋永远装得下更多弹匣。这不是物理学问题，是决心问题。",
    "FloatingBarrel": "枪管不再死死焊在机匣上，而是学会了随波逐流——于是连射时的散布反而更小。",
    "TacticalStride": "射击时不再拖慢脚步。道理很简单：需要稳定的是狙击手，冲锋的人需要的是别停下。",
    "HollowPointBullets": "弹头会在命中瞬间展开，把「打中要害」的收益再放大两成。",
    "HardenedRounds": "钨芯弹头。写字楼那帮人管它叫「合规性穿甲」——终于能穿过那些本该穿过去的东西了。",
    "ImprovedGasSystem": "导气系统全面重做，射速再上一个台阶。别问耐久，问就是测试通过了。",
    "OverpressureCore": "装药加到弹匣开始抗议的程度：每一发都更疼，代价是每匣少几发。",
    "PenetratorCore": "硬芯穿甲弹。它不解决敌人的护甲，它解决的是「敌人有护甲」这个问题。",
}

WMD = {
    "CompactAmmo": [(True, "弹匣容量 +5，装得下就算你的"),
                    (True, "散布 -15%，枪口别乱晃")],
    "GasRerouting": [(True, "射速提升，快得有点不体面"),
                     (False, "弹匣容量 -4，快是要付账的")],
    "OverclockedFiring": [(True, "射速大幅提升，按住扳机就是泼"),
                          (False, "散布 3°→6°，雨露均沾")],
    "AIStabilityEngine": [(True, "散布 -60%，准得不像人打的"),
                          (True, "弱点伤害 +35%"),
                          (False, "直击伤害 -10%"),
                          (False, "射速 -23%，AI 也需要思考时间")],
    "JumboRounds": [(True, "直击伤害 +4，一发就是一份"),
                    (False, "弹匣容量 -8"),
                    (False, "装填时间 +0.5s，装得慢才装得进")],
    "HeavyStance": [(True, "护甲破坏 ×2，专治各种不服"),
                    (True, "散布 -1.0°，站定了就别抖"),
                    (False, "射击时移速 70%→30%，稳是有代价的")],
    "DoubleTap": [(True, "射速大幅提升，扳机都不敢松"),
                  (False, "弹匣容量 -14"),
                  (False, "装填时间 +0.6s")],
}

with io.open(SPEC, encoding="utf-8") as f:
    spec = json.load(f)

hits = 0
for tier in spec["tiers"]:
    for it in tier["items"]:
        if it["asset"] in WUD:
            it["desc"] = WUD[it["asset"]]
            hits += 1
for md in spec["modules"]:
    if md["asset"] in WMD:
        md["desc"] = [{"good": g, "text": t} for g, t in WMD[md["asset"]]]
        hits += 1

assert hits == 19, "只命中 %d 条，期望 19" % hits
with io.open(SPEC, "w", encoding="utf-8", newline="\n") as f:
    json.dump(spec, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("spec 描述已更新：%d 条（12 WUD + 7 WMD）" % hits)
