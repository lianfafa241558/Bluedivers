# -*- coding: utf-8 -*-
"""对比「工作区文件」与「git HEAD 版本」的 UTF-8 BOM 状态，并只修复本次被改掉的 BOM。

用法：
    python bom_vs_head.py            # 只报告
    python bom_vs_head.py --fix      # 恢复"HEAD 有 BOM、现在没有"的文件（= 本次误删的）

判据：只在 HEAD 有 BOM 而工作区没有时才补 ⇒ 不会给项目里"本来就没 BOM"的文件擅自加 BOM。
"""
import os
import subprocess
import sys

# ⚠ 2026-10-01：程序集重构后 01Manager/* 已搬到 09Manager/*（旧路径会让脚本 FileNotFoundError）
#   ⇒ 默认清单换成当前路径；每批收尾更推荐直接命令行传本次改过的文件。
FILES = [
    r"Assets/Scripts/00Core/GameRootBase.cs",
    r"Assets/Scripts/00GameContract/ServiceLocator.cs",
    r"Assets/Scripts/00GameContract/Services/IBattleService.cs",
    r"Assets/Scripts/00GameContract/Services/NullServices.cs",
    r"Assets/Scripts/09Manager/Battle/BattleManager.cs",
    r"Assets/Scripts/09Manager/Battle/PathRequestManager.cs",
    r"Assets/Scripts/09Manager/Battle/VFXManager.cs",
    r"Assets/Scripts/05UnitCore/Damageable.cs",
    r"Assets/Scripts/05UnitCore/HealthPlayer.cs",
    r"Assets/Scripts/05UnitCore/HealthShield.cs",
    r"Assets/Scripts/06Gameplay/Common/FpsHelper_Hit.cs",
    r"Assets/Scripts/06Gameplay/02Game/AI/Controller/EnemyController.cs",
    r"Assets/Scripts/06Gameplay/02Game/AI/DetectionModule/DetectionModule.cs",
    r"Assets/Scripts/06Gameplay/02Game/AI/StateMachine/EnemyMobile_AboState.cs",
    r"Assets/Scripts/06Gameplay/02Game/Game/Shared/Weapon/WeaponBaseController.cs",
]

BOM = b"\xef\xbb\xbf"


def has_bom(data):
    return data[:3] == BOM


def head_bytes(root, rel):
    out = subprocess.run(["git", "show", "HEAD:" + rel.replace("\\", "/")],
                         cwd=root, capture_output=True)
    if out.returncode != 0:
        return None
    return out.stdout


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    fix = "--fix" in sys.argv
    # 支持命令行传路径（相对项目根），不传则用默认清单
    extra = [a for a in sys.argv[1:] if not a.startswith("--")]
    files = extra if extra else FILES
    regressions = []
    for rel in files:
        path = os.path.join(root, rel)
        if not os.path.exists(path):
            print("%s %s" % ("MISSING    ", rel))
            continue
        with open(path, "rb") as fp:
            now = fp.read()
        old = head_bytes(root, rel)
        now_bom = has_bom(now)
        old_bom = None if old is None else has_bom(old)
        if now_bom:
            state = "OK        "
        elif old_bom:
            state = "REGRESSION"
            regressions.append(path)
        else:
            state = "NEVER-BOM "
        if state == "REGRESSION" and fix:
            with open(path, "wb") as fp:
                fp.write(BOM + now)
            state = "RESTORED  "
        print("%s head_bom=%-5s now_bom=%-5s %s" % (state, old_bom, now_bom, rel))
    print("---")
    print("regressions=%d fix=%s" % (len(regressions), fix))


if __name__ == "__main__":
    main()
