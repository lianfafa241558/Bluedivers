# -*- coding: utf-8 -*-
"""
P5-3 · 第①步：把 `AirdropData` / `AirdropState` 从 `AirdropController`（01Manager/Battle）里**解嵌套**，
下沉到玩法层（`06Gameplay/Airdrop/`）。

为什么是"玩法层"而不是契约层：
  `AirdropData` 有字段 `AirdropData_SO cfg`，而 `AirdropData_SO` 在 `02Data/`＝`04_Data`；
  `04_Data.asmdef` **引用了** `01_GameContract` ⇒ 契约**不能反过来引用 `04_Data`**（成环）
  ⇒ 只能放到"在 `04_Data` 之上"的最低层＝玩法层。实测其全部消费者都在玩法层及以上（合法）。

做法（同 `TaskData.cs` 的套路）：按行区间**原样抽取 + 去 4 空格缩进**（不手抄，避免成员顺序/值出错），
再全局把 `AirdropController.AirdropData` → `AirdropData`、`AirdropController.AirdropState` → `AirdropState`，
并清掉因此失效的 `using static AirdropController;` / `using X = AirdropController.X;`。
"""
import os
import re
import sys

SCRIPTS = os.path.join(os.getcwd(), "Assets", "Scripts")
SRC = os.path.join(SCRIPTS, "01Manager", "Battle", "AirdropController.cs")
DST_DIR = os.path.join(SCRIPTS, "06Gameplay", "Airdrop")
DST = os.path.join(DST_DIR, "AirdropData.cs")

HEADER = """// 由 AirdropController（01Manager/Battle）解嵌套下沉而来（2026-10-01，P5-3）。
// 下沉理由：AirdropController 属 09_Managers，而玩法层（Mission/Player/AI/Interactable…）到处用这两个类型
// ⇒ 不搬出来玩法层无法成集。**放玩法层而不是契约层**：AirdropData 依赖 `AirdropData_SO`（04_Data），
// 而 04_Data 引用了 01_GameContract ⇒ 契约不能反过来引 04_Data（成环）。
// ⚠ 不要再把它们塞回 AirdropController；`AirdropController.WaitRelease` 仍在原处（管理器侧运行时状态）。

"""


def main():
    with open(SRC, encoding="utf-8") as f:
        lines = f.read().split("\n")

    # 定位两个块（1-based -> 0-based）
    i_data_decl = next(i for i, l in enumerate(lines) if re.match(r"^    public class AirdropData\b", l))
    i_state_decl = next(i for i, l in enumerate(lines) if re.match(r"^    public enum AirdropState\b", l))
    i_data_attr = i_data_decl - 1
    assert lines[i_data_attr].strip() == "[System.Serializable]", "AirdropData 前一行不是 [System.Serializable]"

    # AirdropData 块结束 = 其后第一个 4 空格缩进的 "}"（嵌套类型/成员都在更深处）
    i_data_end = next(i for i in range(i_state_decl, i_data_decl, -1) if lines[i] == "    }")
    # AirdropState 块结束
    i_state_end = next(i for i in range(len(lines) - 1, i_state_decl, -1) if lines[i] == "    }")

    data_block = lines[i_data_attr:i_data_end + 1]
    state_block = lines[i_state_decl:i_state_end + 1]

    def dedent(block):
        out = []
        for l in block:
            out.append(l[4:] if l.startswith("    ") else l)
        return out

    body = dedent(data_block) + [""] + dedent(state_block)

    os.makedirs(DST_DIR, exist_ok=True)
    with open(DST, "w", encoding="utf-8", newline="\n") as f:
        f.write(HEADER + "\n".join(body) + "\n")
    print("新建 %s  (%d 行，含 AirdropData %d 行 + AirdropState %d 行)"
          % (os.path.relpath(DST, SCRIPTS), len(body), len(data_block), len(state_block)))

    # 从 AirdropController.cs 删除这两个块（连同前面的 [System.Serializable]）
    remain = lines[:i_data_attr] + lines[i_state_end + 1:]
    with open(SRC, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(remain))
    print("AirdropController.cs：%d 行 → %d 行" % (len(lines), len(remain)))

    # 全局改引用
    changed = []
    for dirpath, _dirnames, filenames in os.walk(SCRIPTS):
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(dirpath, fn)
            if os.path.abspath(p) == os.path.abspath(DST):
                continue
            with open(p, encoding="utf-8") as f:
                t = f.read()
            o = t
            t = t.replace("AirdropController.AirdropData", "AirdropData")
            t = t.replace("AirdropController.AirdropState", "AirdropState")
            # 失效的别名 using
            t = re.sub(r"(?m)^\s*using\s+AirdropState\s*=\s*AirdropState\s*;\s*\n", "", t)
            t = re.sub(r"(?m)^\s*using\s+AirdropData\s*=\s*AirdropData\s*;\s*\n", "", t)
            # 这两个文件只在签名里用 AirdropData ⇒ `using static` 已失效（会随玩法层成集而变成跨层依赖）
            if fn in ("GlobalEventSub.cs", "BattleEventSub.cs", "PlayerWeaponsManager.cs"):
                t = re.sub(r"(?m)^\s*using\s+static\s+AirdropController\s*;\s*\n", "", t)
            if t != o:
                with open(p, "w", encoding="utf-8", newline="\n") as f:
                    f.write(t)
                changed.append(os.path.relpath(p, SCRIPTS))

    print("改引用 %d 个文件：" % len(changed))
    for c in sorted(changed):
        print("   " + c)


if __name__ == "__main__":
    main()
