# -*- coding: utf-8 -*-
"""向 2026-10-06 记忆日志追加：打包版「设置缺项 → ArchivesFloat 空串 float.Parse」修复（append-only）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-06.md"

NOTE = """

## ✅ 修掉打包版启动即崩：设置缺项 → `ArchivesFloat` 空串 `float.Parse`

- 报错（只在**打包版**出现）：`FormatException` ← `ArchivesData_SO+ArchivesFloat.get_RawFloat`（`CanvasController.Awake:32`、`OnWindowStateChange:61`）与 `get_RawInt`（`ArchiveLoader.SyncDefaultSettings:42`）。
- 根因链（四条，全部实测）：
  1. 打包目录存档 `D:\\Pro\\Verion\\BlueDivers\\KivotosCraftArc.json` 的 `settingDic.arr` 只有 **15** 项，**缺 `沉浸模式` / `默认操作视角`**（默认资产 `Resources/GameData/Archive_Default.asset:421/432` 有 17 项）；编辑器存档 `D:\\Pro\\KivotosCraftArc.json:1512/1569` 两个键都在 ⇒ **编辑器不报、打包报**。
  2. 缺键时 `DisplayDic.cs` 索引器返回 `DefaultValue` 模板，而它的 `ArchivesFloat.value` 是**空串**（资产 `:296`、存档 `"DefaultValue":{"value":{"value":""}}`）。
  3. `ArchivesFloat.RawFloat` 用 `float.Parse(value ?? "0")` —— `??` 只挡 null、**挡不住空串** ⇒ FormatException。
  4. `ArchiveLoader` 的补齐是协程且 `yield return null`（比 `GameRoot` 的 Awake 晚一帧），而 `DisplayDic` 索引器兜底时**把空心条目写进 dic** ⇒ 下一帧 `Synchronize` 认为"键已存在"拒绝补齐、`ForEach` 再炸；`haveNewSetting=false` 时还不 `Save()` ⇒ **坏存档永久自锁，每次启动炸两次**（stack 里两个报错点就是这么来的）。
- 修法（用户拍板 1+2+3，已全部落码）：
  1. `ArchivesData_SO.ArchivesFloat`：`RawFloat => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f`、`RawInt => (int)RawFloat`、构造 `ToString("F" + digit, CultureInfo.InvariantCulture)`（原来读写都吃当前文化：逗号小数点区域写 "0,10"、读 "0.10" 会当千分位得 10）。
  2. `ArchiveLoader`：三处 `Synchronize` **移到 `Init()` 同步执行**（保证本帧任何更早 Awake 的读取方都能取到键）；广播 + 新增回写仍在协程里（等订阅者就绪），`haveNewSetting` 提升为字段 `_haveNewSetting`。
  3. `GetSetting` 改 `TryGet` + 缺键 `LogError`（不再产生副作用）；`DisplayDic` 加 `[NonSerialized] HashSet<Key> _placeholderKeys` 标记"DefaultValue 兜底插入的占位键"，`Synchronize` 允许用真实默认值**覆盖占位项**（索引器 setter / `Add` 清标记）⇒ 时序再被破坏也能自愈。⚠ 特意**保留**"兜底插入"本身：`VehicleWnd` 的 `NowArchData.skinIndex = …` 靠索引器返回**同一引用**就地改（去掉插入会让载具改装不落盘）。
- 验证：`plans/offline_compile.py 00_Core 06_Gameplay 09_Managers 10_UI` **0 错误**（仅既有 CS0649/CS0414 警告）；`read_lints` 0。
- 可复用排查手法：`ArchivesDataBase_SO.DeletePath()` = `Application.dataPath/../`（编辑器再 `/../`）⇒ **编辑器与打包读的是两个不同 json**；"编辑器好、打包坏"先比这两个文件的键集合。
"""

with io.open(PATH, "a", encoding="utf-8") as f:
    f.write(NOTE)

print("appended", len(NOTE), "chars")
