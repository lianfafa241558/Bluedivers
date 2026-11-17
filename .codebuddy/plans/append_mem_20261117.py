import io

note = """

## 新机器人动画去掉根前缀 Droid_Kurokame_AR/（已执行）
- 目标 `Assets/Art/Modle/Enemy/Robot/NewRobot/Anim/*.anim`（13 个，整个目录在 git 里还是 untracked）；`path:` 行统一去前缀，共 **2082 处**。脚本 `.codebuddy/plans/strip_anim_root_prefix.py`（按行处理、字节级读写保 BOM/LF，断言行数与 CRLF 数不变）。
- ⚠ 坑：`Droid_Kurokame_AR_Vital_Panic.anim` 的 `m_Name`（clip 名）也是同一前缀，**全文件 replace 会误改 clip 名** ⇒ 必须只对 `path:` 行替换。
- 改完 `path` 首段统计：2082 处全是 `bone_root`；另有少量数字 fileID 形式的 path（原本就无前缀）。
- 提醒口径：`PMC_SMG.prefab` 里 `NewRobot.fbx`（guid 59160b63…）层级 = `PMC_SMG/NewRobot`（挂 Animator、未覆写 m_Controller）→ `Robot` → `bone_root`；clip 变成 `bone_root/...` 后，要求 Animator 在 `NewRobot/Robot` 上，挂 `NewRobot` 则路径需带 `Robot/` 前缀，否则动画静默不驱动骨骼。
"""

with io.open(r"e:/Bluedivers/.codebuddy/memory/2026-11-17.md", "a", encoding="utf-8", newline="") as f:
    f.write(note)
print("ok")
