# -*- coding: utf-8 -*-
"""
P5-3 · 消掉两个"刚搬进玩法层的文件"里的管理器直连（2026-10-01）

`MedivacController` 与 `ModifyTerrain` 原在 `Effect/`（在使用者**之上**，直连管理器合法），
搬进玩法层后就成了上行依赖 ⇒ 改走契约（`ServiceLocator.Task` / `ServiceLocator.Battle`）。

替换表：
  MedivacController.cs
    `TaskManager taskManager;`                                  -> `GameContract.ITaskService taskSvc;`
    `taskManager = TaskManager.Instance;`                       -> `taskSvc = GameContract.ServiceLocator.Task;`
    `taskManager.nowTask.IsValid()`                             -> `taskSvc.HasTask`
    `taskManager.nowTask.Countdown`                             -> `taskSvc.Countdown`
    `taskManager.EnterTransition()`                             -> `taskSvc.EnterTransition()`
    `BattleManager.Instance&&BattleManager.Instance.HaveBooster(BoosterType.ExpertPilot)`
                                                                -> `GameContract.ServiceLocator.Battle.HaveBooster(BoosterType.ExpertPilot)`
  ModifyTerrain.cs
    `if (BattleManager.Instance)`                               -> `if (GameContract.ServiceLocator.Battle.IsStartBattle)`
"""
import os
import sys

SCRIPTS = os.path.join(os.getcwd(), "Assets", "Scripts")
JOBS = [
    ("06Gameplay/Common/MedivacController.cs", [
        ("TaskManager taskManager;", "GameContract.ITaskService taskSvc;"),
        ("taskManager = TaskManager.Instance;", "taskSvc = GameContract.ServiceLocator.Task;"),
        ("taskManager.nowTask.IsValid()", "taskSvc.HasTask"),
        ("taskManager.nowTask.Countdown", "taskSvc.Countdown"),
        ("taskManager.EnterTransition()", "taskSvc.EnterTransition()"),
        ("BattleManager.Instance&&BattleManager.Instance.HaveBooster(BoosterType.ExpertPilot)",
         "GameContract.ServiceLocator.Battle.HaveBooster(BoosterType.ExpertPilot)"),
        ("BattleManager.Instance && BattleManager.Instance.HaveBooster(BoosterType.ExpertPilot)",
         "GameContract.ServiceLocator.Battle.HaveBooster(BoosterType.ExpertPilot)"),
    ]),
    ("06Gameplay/Common/ModifyTerrain.cs", [
        ("if (BattleManager.Instance)", "if (GameContract.ServiceLocator.Battle.IsStartBattle)"),
    ]),
]


def main():
    for rel, pairs in JOBS:
        p = os.path.join(SCRIPTS, rel)
        if not os.path.exists(p):
            print("!! 缺文件 " + rel)
            continue
        with open(p, encoding="utf-8") as f:
            t = f.read()
        n = 0
        for old, new in pairs:
            c = t.count(old)
            if c:
                t = t.replace(old, new)
                n += c
                print("   %-70s x%d" % (old[:70], c))
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(t)
        print("%s : 替换 %d 处" % (rel, n))
        left = [l for l in t.split("\n") if ("TaskManager" in l or "BattleManager" in l) and not l.strip().startswith("//")]
        if left:
            print("   ⚠ 仍有残留：")
            for l in left:
                print("      " + l.strip())


if __name__ == "__main__":
    main()
