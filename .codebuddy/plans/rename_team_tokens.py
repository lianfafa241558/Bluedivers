# -*- coding: utf-8 -*-
r"""[批量改名] RoomManager → TeamManager 的收尾（token 级）。

背景：用户已把 `RoomManager` 类改名 `TeamManager`（文件也改了），但留下三类残留：
  ① `04Data/RoomState.cs` 类名/快照点仍叫 RoomState（TeamManager.SyncRoomState() 在写它）
  ② `Window` 的字段名仍叫 `roomManager`（类型已是 TeamManager），37 处调用点没动
  ③ 若干注释里仍写 RoomManager
本脚本按文件做字节级替换：保 BOM / 保行尾；**替换后断言文件里不再残留旧 token**（dry-run 只报）。
注意规则顺序：`SyncRoomState` 必须在 `RoomState` 之前替换（否则前者会被后者改掉、规则失效）。

用法：
    python -X utf8 .codebuddy/plans/rename_team_tokens.py            # dry-run
    python -X utf8 .codebuddy/plans/rename_team_tokens.py --apply    # 落盘
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = r"d:\Pro\Bluedivers"
APPLY = "--apply" in sys.argv

R_SYNC = ("SyncRoomState", "SyncTeamState")
R_CLS = ("RoomState", "TeamState")
R_MGR = ("RoomManager", "TeamManager")
R_FLD = ("roomManager", "teamManager")

# 文件 → 规则序列（顺序敏感）
PLAN = {
    "Assets/Scripts/04Data/RoomState.cs": [R_SYNC, R_MGR, R_CLS],
    "Assets/Scripts/04Data/FlowState.cs": [R_CLS],
    "Assets/Scripts/04Data/UIState.cs": [R_CLS],
    "Assets/Scripts/04Data/TaskState.cs": [R_CLS],
    "Assets/Scripts/04Data/BattleState.cs": [R_CLS],
    "Assets/Scripts/00GameContract/BattleHub.cs": [R_CLS],
    "Assets/Scripts/09Manager/Global/TeamManager.cs": [R_SYNC, R_MGR, R_CLS],
    "Assets/Scripts/09Manager/Battle/BattleManager.cs": [R_MGR],
    "Assets/Scripts/09Manager/Bridge/BridgeRoleManager.cs": [R_MGR, R_FLD],
    "Assets/Scripts/10UI/Window.cs": [R_FLD],
    "Assets/Scripts/10UI/Wnd/ArmamentWnd.cs": [R_FLD],
    "Assets/Scripts/10UI/Wnd/GameEndWnd.cs": [R_FLD],
    "Assets/Scripts/10UI/Wnd/SettingWnd.cs": [R_FLD],
    "Assets/Scripts/10UI/Wnd/SelectMapWnd.cs": [R_MGR],
    "Assets/Scripts/06Gameplay/Mission/Main/MissionOilRefining.cs": [R_CLS, R_FLD],
    "Assets/Scripts/06Gameplay/AI/Controller/EnemyController.cs": [R_CLS, R_MGR, R_FLD],
    "Assets/Scripts/NetTmp/Client/NetRoomFlow.cs": [R_MGR],
}

# 替换后文件里不允许再出现的旧 token
FORBIDDEN = ["RoomManager", "roomManager", "RoomState", "SyncRoomState"]


def main():
    bad, buffers, report = [], {}, []

    for rel, rules in PLAN.items():
        path = os.path.join(ROOT, rel.replace("/", os.sep))
        if not os.path.exists(path):
            bad.append((rel, "文件不存在"))
            continue
        with open(path, "rb") as f:
            raw = f.read()

        bom = raw.startswith(b"\xef\xbb\xbf")
        text = raw[3:].decode("utf-8") if bom else raw.decode("utf-8")

        counts = []
        for old, new in rules:
            n = text.count(old)
            counts.append("%s→%s x%d" % (old, new, n))
            text = text.replace(old, new)

        left = [t for t in FORBIDDEN if t in text]
        if left:
            bad.append((rel, "仍残留 %s" % ",".join(left)))

        buffers[path] = (bom, text)
        report.append((rel, counts))

    if bad:
        print("!! 有 %d 处不符，未写盘：" % len(bad))
        for rel, why in bad:
            print("   -", rel, ":", why)
        return 1

    for rel, counts in report:
        print("  " + rel)
        print("      " + " | ".join(c for c in counts if not c.endswith("x0")))

    for path, (bom, text) in buffers.items():
        if APPLY:
            with open(path, "wb") as f:
                f.write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
    print("共 %d 个文件；%s" % (len(buffers), "已落盘" if APPLY else "dry-run，加 --apply 落盘"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
