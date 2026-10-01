# -*- coding: utf-8 -*-
"""审计：本轮各批改动的"标志物"是否还在（防宿主编辑器旧缓冲区覆盖）。
用法：python audit_markers.py
"""
import os

ROOT = r"d:\Pro\Bluedivers\Assets\Scripts"

# (范围文件或 None=全仓, 标志物, 期望最小次数, 说明)
CHECKS = [
    ("09Manager\\Battle\\BattleManager.cs", "FPSGame.Game.IUnitQuerySink", 1, "类实现查询接口"),
    ("09Manager\\Battle\\BattleManager.cs", "UnitQuery.Sink = this", 1, "Start 接管查询入口"),
    ("09Manager\\Battle\\BattleManager.cs", "UnitQuery.Sink = null", 1, "OnDestroy 归还"),
    ("09Manager\\Battle\\BattleManager.cs", "FPSGame.Data.BattleState.Reset()", 2, "Awake+OnDestroy 状态归零"),
    ("09Manager\\Battle\\BattleManager.cs", "BattleState.BattleRandom = BattleRandom", 2, "两处随机源同步"),
    ("09Manager\\Battle\\BattleManager.cs", "BattleEventSub.OnEndGame", 2, "订阅+退订"),
    ("09Manager\\Battle\\BattleManager.cs", "BattleEventSub.OnSubmitOOPart", 2, "订阅+退订"),
    ("09Manager\\Battle\\BattleManager.cs", "BattleEventSub.OnRevealAllMissions", 2, "订阅+退订"),
    ("09Manager\\Battle\\BattleManager.cs", "BattleEventSub.OnAddBattleDataItem", 2, "订阅+退订"),
    ("09Manager\\Battle\\BattleManager.cs", "BattleEventSub.OnRequestAuthorize", 2, "订阅+退订"),
    ("09Manager\\Battle\\BattleManager.cs", "HandleRevealAllMissions", 2, "定义+订阅"),
    ("09Manager\\Battle\\BattleManager.cs", "IBattleService.RevealAllMissions", 0, "旧显式实现应已删除"),
    # ⚠ 事件定义处写作 `public static event Action OnXxx`（不带 BattleEventSub 前缀）⇒ 全仓只期望"订阅+退订"这 2 处
    (None, "BattleEventSub.OnEndGame", 2, "订阅+退订"),
    (None, "BattleEventSub.OnRequestAuthorize", 2, "订阅+退订"),
    (None, "BattleEventSub.OnAddBattleDataItem", 2, "订阅+退订"),
    (None, "BattleEventSub.OnRevealAllMissions", 2, "订阅+退订"),
    (None, "BattleEventSub.OnSubmitOOPart", 2, "订阅+退订"),
    (None, "FPSGame.Game.UnitQuery.FindUnits", 14, "FindUnits 14 处消费点"),
    (None, "FPSGame.Data.BattleState.BattleRandom", 7, "BattleRandom 消费点"),
    (None, "BattleHub.Current", 20, "BattleHub 替换点"),
    (None, "ServiceLocator.", 0, "旧定位器引用（注释里允许，故用 -- 判定）"),
    ("06Gameplay\\Common\\FpsHelper_Hit.cs", "battleSvc", 0, "该文件不应再有契约变量"),
    ("00GameContract\\Services\\BattleHub.cs", "NullBattleService.Instance", 2, "空对象单例"),
]

print("%-46s %-42s %s" % ("范围", "标志物", "结果"))
print("-" * 110)
bad = 0
for scope, marker, expect, desc in CHECKS:
    if scope:
        files = [os.path.join(ROOT, scope)]
    else:
        files = []
        for dp, _, fns in os.walk(ROOT):
            for fn in fns:
                if fn.endswith(".cs"):
                    files.append(os.path.join(dp, fn))
    n = 0
    for p in files:
        if not os.path.exists(p):
            continue
        n += open(p, "rb").read().count(marker.encode("utf-8"))
    ok = n >= expect
    if not ok:
        bad += 1
    print("%-46s %-42s %s n=%d (期望>=%d) %s" % (scope or "<全仓>", marker, "OK  " if ok else "MISS", n, expect, desc))
print("-" * 110)
print("缺失项 = %d" % bad)
