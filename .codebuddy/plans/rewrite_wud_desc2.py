# -*- coding: utf-8 -*-
"""重写 Logic 的 12 条 WUD 描述：去掉「不是…而是」类对称句式，贴近 DRG 原版口吻。

DRG 原文的套路（见 .codebuddy/notes/drg-weapons-upgrades.md 里抓到的原句）：
  平铺直叙说效果 + 一句自嘲/吐槽，常用「工程部/管理层/别问/见不得光/安全去他的/容易崩到眼睛」。
"""
import io
import json
import os

SPEC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "skills/weapon-upgrade/scripts/examples/Logic.json")

WUD = {
    "GyroStabilisation": "陀螺稳定组件让枪口不再跟着你的手一起抖。中距离终于能打中你想打的地方了。",
    "SuperchargedFeed": "供弹机构调到了安全上限。射得更快，弹匣也空得更快。工程师签了字，那就没问题。",
    "QuickfireEjector": "经验、训练，再加上一些见不得光的私下改动，装填快了一大截。",
    "IncreasedCaliberRounds": "口径加大，弹头更重。后坐力会提醒你这一点。",
    "ExpandedAmmoBags": "口袋不够就再缝几个。总备弹量上去了，优香检查装备时会当没看见。",
    "FloatingBarrel": "枪管改成浮动式，连射时不再被机匣带着乱晃。手感更稳，拆装更麻烦——工程部不在乎后者。",
    "TacticalStride": "持枪姿态调整过，开火时脚步不再被拖住，还能快上三成。冲锋的人终于不用在原地挨打。",
    "HollowPointBullets": "弹头命中瞬间展开，专门招呼敌人的软肋。打中要害的收益再多两成。",
    "HardenedRounds": "钨芯弹头。写字楼那边管这叫「合规性穿甲」，因为它终于能穿过那些本该穿过去的东西。",
    "ImprovedGasSystem": "导气系统整体重做，射速再上一档。耐久测试报告？工程部说通过了。",
    "OverpressureCore": "装药加到弹匣开始抗议的程度。每一发都更疼，代价是每匣少几发。",
    "PenetratorCore": "硬芯穿甲弹，专治各种高硬度装甲。对面那层壳从此只是个建议。",
}

with io.open(SPEC, encoding="utf-8") as f:
    spec = json.load(f)

hits, bad = 0, []
for tier in spec["tiers"]:
    for it in tier["items"]:
        if it["asset"] in WUD:
            it["desc"] = WUD[it["asset"]]
            hits += 1
            # 自检：禁用的对称句式
            for pat in ("不是", "而是", "不是……而是"):
                if pat in it["desc"]:
                    bad.append("%s 含禁用词「%s」" % (it["asset"], pat))

assert hits == 12, "只命中 %d 条，期望 12" % hits
assert not bad, "；".join(bad)

with io.open(SPEC, "w", encoding="utf-8", newline="\n") as f:
    json.dump(spec, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("WUD 描述已重写：%d 条（含禁用句式自检通过）" % hits)
