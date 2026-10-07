# -*- coding: utf-8 -*-
"""向 2026-10-02 记忆日志追加一条（append-only）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-02.md"

NOTE = """

## 诊断：Airdrop 加载报 "The referenced script (Unknown) ... is missing"（2026-10-02）

- **报错语义**：不是"找不到 `AirdropData_SO` 这个类型/脚本文件"，而是加载某个 Behaviour 时
  `asset 里的 m_Script guid → MonoScript → C# 类` 这条链断了，Unity 拿不到类名，所以打印 `(Unknown)`。
- **已排除**（静态证据）：
  - Airdrop 目录 57 个 `.asset` 的 `m_Script` guid **全部** = `2dfe521d3dbee1e44a48bd0994d06029`
    = `Assets/Scripts/04Data/AirdropData_SO.cs.meta` 的 guid（脚本与资产没断链）；类定义唯一、无重名。
  - `plans/offline_compile.py 04_Data 09_Managers` → **0 error**（不是编译失败导致 MonoScript 失效）。
  - `plans/find_missing_script_refs.py --under Assets/Resources/GameData/Airdrop` → **0 条**。
- **真凶**：`Resources.LoadAll` 会把资源的**依赖对象一并反序列化**，所以报在 LoadAll 栈上的缺失脚本
  来自依赖链深处的 prefab。新工具 `plans/dep_closure_missing.py <根路径> [--max N]`（依赖闭包 + 父链回溯）给出：
  `GameData/Airdrop/ADSO_Y_Machine.asset` → `Resources/Prefabs/Airdrop/Turret_Mission_Machine.prefab`
  → `Resources/VFX/Flash/Weapon/VFX_MF 4P RIFLE1.prefab`，其上组件 guid `6fd2e8874f60266498a388d7229a1bac`
  （字段 `OnlyDeactivate`）已不存在：WarFX 插件目录（`Assets/Plugins/Art/WarFX`）只剩 prefab/材质/shader，
  `.cs` 全被删；全项目 **75 个** WarFX prefab 都引用这个 guid（git HEAD 里也无该 meta，脚本从未入库）。
- **影响**：只刷日志，**不阻断** `AirdropData_SO` 加载（57 项仍能进 `airdropDic`）。
- **待拍板修复**（未执行）：① 清掉该 prefab / 75 个 WarFX prefab 上的 Missing 组件；② 从 WarFX 原包恢复该脚本。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
