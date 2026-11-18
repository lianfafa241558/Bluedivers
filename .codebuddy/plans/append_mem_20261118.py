import io
import os

p = r"e:/Bluedivers/.codebuddy/memory/2026-11-18.md"
new_file = not os.path.exists(p)
head = "# 2026-11-18\n" if new_file else ""

note = """

## Devastator 动画去掉根前缀 Droid_Yoheki_Shield/（已执行）
- `Assets/Art/Modle/Enemy/Robot/NewRobot/Anim/Devastator/*.anim` 15 个文件、`path:` 行共 **3099 处**去前缀；改完首段统计全是 `bone_root`，残留 0。
- 复用通用脚本 `.codebuddy/plans/strip_anim_path_prefix.py <目录> <前缀> [--apply]`（上一轮的专用脚本已升级成参数化版本，按 `path:` 行处理、字节级保 BOM/LF、断言行数/换行/长度）。
- 改动前核对过：`Droid_Yoheki_Shield` 在该目录只出现在 `path:` 行（无 m_Name 命中），与 NewRobot/Anim 那轮不同，全文件 replace 也可，但仍按 path 行处理。
- 现状提醒：`Devastator_AC.controller`（guid ee08e6a0…）与 `Devastator.fbx`（guid bbff9d99…）目前 **没有任何 prefab 引用**，整套还在接入中；clip 路径已是 `bone_root/...`，接入时 Animator 必须挂在 `bone_root` 的父节点上。
"""

with io.open(p, "a", encoding="utf-8", newline="") as f:
    f.write(head + note)
print("ok", "created" if new_file else "appended")
