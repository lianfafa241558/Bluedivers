# -*- coding: utf-8 -*-
"""
Phase 6 · 窗口收口：调用点批量改写（2026-10-01）

前提（已落地）：`WndManager` 的 10 个具体窗口字段被删除，改为：
  * 窗口自注册 → UI 侧 `WndHub`（`Window.Awake`/`OnDestroy` 里调，另有 `Scan()` 兜底）
  * `WndHub` 把能力登记进契约层 `WindowRegistry`（委托）
  * `WndManager` 的动词（SetWndState/CreatNotice/CreatCountDown/ClearNotice）转调契约

本脚本负责**机械改写调用点**：
  1. 6 个窗口里 `WndManager.Instance.xxxWnd = this/null;`（12 行）⇒ 删除（已由 Window 基类自注册）
  2. `wndManager.CreatTip(...)` / `WndManager.Instance.CreatTip(...)` ⇒ `WndHub.Tip.Creat(...)`
  3. `wndManager.operationWnd` ⇒ `WndHub.Operation`
  4. `WndManager.Instance.selectMapWnd.SetWndState(true)`（BridgeRoleManager，同层）⇒ `WndManager.Instance.SetWndState(GameContract.WndTypeEnum.SelectMap, true)`
  5. `BridgeSys` 的 `public ArmamentWnd armament;` ⇒ `public GameContract.IBridgeArmamentSink armament;`；
     `ArmamentWnd` 声明加上 `, GameContract.IBridgeArmamentSink`
"""
import os
import re
import sys

SCRIPTS = os.path.join(os.getcwd(), "Assets", "Scripts")

DROP_LINES = [
    "WndManager.Instance.airdropConfigWnd = this;",
    "WndManager.Instance.airdropConfigWnd = null;",
    "WndManager.Instance.guideWnd = this;",
    "WndManager.Instance.guideWnd = null;",
    "WndManager.Instance.selectMapWnd = this;",
    "WndManager.Instance.selectMapWnd = null;",
    "WndManager.Instance.selectRoleWnd = this;",
    "WndManager.Instance.selectRoleWnd = null;",
    "WndManager.Instance.settingWnd = this;",
    "WndManager.Instance.vehicleWnd = this;",
    "WndManager.Instance.vehicleWnd = null;",
]

TEXT_RULES = [
    ("wndManager.CreatTip(", "WndHub.Tip.Creat("),
    ("WndManager.Instance.CreatTip(", "WndHub.Tip.Creat("),
    ("wndManager.operationWnd", "WndHub.Operation"),
    ("WndManager.Instance.operationWnd", "WndHub.Operation"),
    ("WndManager.Instance.selectMapWnd.SetWndState(true)",
     "WndManager.Instance.SetWndState(GameContract.WndTypeEnum.SelectMap, true)"),
    ("public ArmamentWnd armament;", "public GameContract.IBridgeArmamentSink armament;"),
]


def main():
    changed = []
    for dirpath, _dn, fns in os.walk(SCRIPTS):
        for fn in fns:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(dirpath, fn)
            rel = os.path.relpath(p, SCRIPTS).replace("\\", "/")
            if rel.endswith("WndHub.cs") or rel.endswith("WndManager.cs") or rel.endswith("Window.cs"):
                continue
            with open(p, encoding="utf-8") as f:
                text = f.read()
            orig = text
            hits = []

            # 1. 删掉整行（保留缩进为空行不必要 ⇒ 直接删行）
            lines = text.split("\n")
            kept = []
            for l in lines:
                s = l.strip()
                if s in DROP_LINES:
                    hits.append("drop: " + s)
                    continue
                kept.append(l)
            text = "\n".join(kept)

            # 2. 其余替换
            for old, new in TEXT_RULES:
                if old in text:
                    hits.append("%s -> %s" % (old, new))
                    text = text.replace(old, new)

            # 3. ArmamentWnd 实现接口
            if fn == "ArmamentWnd.cs":
                m = re.search(r"(?m)^public class ArmamentWnd\s*:\s*Window\s*$", text)
                if m:
                    text = text.replace("public class ArmamentWnd : Window",
                                        "public class ArmamentWnd : Window, GameContract.IBridgeArmamentSink")
                    hits.append("ArmamentWnd 实现 IBridgeArmamentSink")

            if text != orig:
                with open(p, "w", encoding="utf-8", newline="\n") as f:
                    f.write(text)
                changed.append((rel, hits))

    print("改动 %d 个文件：" % len(changed))
    for rel, hits in sorted(changed):
        print("  " + rel)
        for h in hits:
            print("      " + h)


if __name__ == "__main__":
    main()
