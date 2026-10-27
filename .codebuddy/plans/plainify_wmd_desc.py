# -*- coding: utf-8 -*-
"""把 Logic 的 7 个模组 desc 改回"纯效果"（模组文案不写风味，只有 WUD 卡面才诙谐）。

WMD.desc 是 List<SKVP<bool,string>>，UI 按 Key 渲染绿↑/红↓ 效果行 ⇒ 只写增减，不写梗。
数值区间必须与 cfg 基线一致（MoveSpeedToShoot 基线已从 0.7 调成 1.0）。
"""
import io
import json
import os

SPEC = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    "skills/weapon-upgrade/scripts/examples/Logic.json")

WMD = {
    "CompactAmmo": [(True, "弹匣容量 +5"), (True, "散布 -15%")],
    "GasRerouting": [(True, "射速提升"), (False, "弹匣容量 -4")],
    "OverclockedFiring": [(True, "射速大幅提升"), (False, "散布 3°→6°")],
    "AIStabilityEngine": [(True, "散布 -60%"), (True, "弱点伤害 +35%"),
                          (False, "直击伤害 -10%"), (False, "射速 -23%")],
    "JumboRounds": [(True, "直击伤害 +4"), (False, "弹匣容量 -8"),
                    (False, "装填时间 +0.5s")],
    "HeavyStance": [(True, "护甲破坏 ×2"), (True, "散布 -1.0°"),
                    (False, "射击时移速 100%→60%")],
    "DoubleTap": [(True, "射速大幅提升"), (False, "弹匣容量 -14"),
                  (False, "装填时间 +0.6s")],
}

with io.open(SPEC, encoding="utf-8") as f:
    spec = json.load(f)

hits = 0
for md in spec["modules"]:
    if md["asset"] in WMD:
        md["desc"] = [{"good": g, "text": t} for g, t in WMD[md["asset"]]]
        hits += 1

assert hits == 7, "只命中 %d 个模组，期望 7" % hits
with io.open(SPEC, "w", encoding="utf-8", newline="\n") as f:
    json.dump(spec, f, ensure_ascii=False, indent=2)
    f.write("\n")
print("模组文案已改回纯效果：%d 条" % hits)
