# -*- coding: utf-8 -*-
"""向 2026-10-02 记忆日志追加一条（append-only）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-02.md"

NOTE = """

## 诊断：Camp 加载报同类 Missing Script（TaskManager.Init，2026-10-02）

- 报错栈：`TaskManager.Init → ResSvc.LoadObjects<CampData_SO>("GameData/Camp")`。**同样不是找不到 `CampData_SO`**：
  Camp 目录 7 个 `.asset` 的 `m_Script` 全部可解析（`find_missing_script_refs.py --under .../Camp` = 0）。
- 真凶（`dep_closure_missing.py`）：`GameData/Camp/CD_Und.asset` → `Resources/Prefabs/Enemy/Zerg/UNG/Sub/Binah_Black.prefab`
  → 该 prefab 有 **3 个 Missing 组件**，脚本 guid `6fd2e8874f60266498a388d7229a1bac`（WarFX 被删脚本，仅 1 个字段 `OnlyDeactivate`）。
- 新工具 `plans/missing_component_locate.py <资产> [guid]` 直接给出宿主 GameObject：
  `WFX_SmokeGrenade Black`、`WFX_SmokeGrenade Black (1)`、`WFX_SmokeGrenade Black`（3 处）。
- 全项目该 guid 共 **72** 个文件引用：71 个在 `Assets/Plugins/Art/WarFX`（第三方，可不动），
  **游戏资源链上只有 `Binah_Black.prefab` 这一处需要处理**。
- ⚠ **闭包结论有时效性**：本轮复跑时 Airdrop 链（`ADSO_Y_Machine` → `Turret_Mission_Machine` →
  `Resources/VFX/Flash/Weapon/VFX_MF 4P RIFLE1.prefab`）已 **报 0** —— 该 prefab 现在连一个 `m_Script` 都没有
  （上一轮还含该缺失组件）⇒ 会话期间被外部改动（疑似用户在 Unity 里清掉了）。**定位后要复跑再下结论**。
- 待办：清理 `Binah_Black.prefab` 的 3 个 Missing 组件（未执行，待拍板：手清 / 编辑器脚本批量清 / 恢复 WarFX 脚本）。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
