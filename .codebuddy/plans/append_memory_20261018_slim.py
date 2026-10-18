# -*- coding: utf-8 -*-
"""向 2026-10-18 流水追加"拦截火车字段精简"一条（append-only，按原文件换行风格）。"""
import io

PATH = r"d:\Pro\Bluedivers\.codebuddy\memory\2026-10-18.md"

NOTE = """
## 拦截火车：Inspector 字段 42 → 18（用户："字段有些太多了"）

- 判据（先取数据）：把 `Robbery.prefab` 的序列化值和代码默认值逐字段比对 ⇒ **42 个字段里只有 5 个偏离**：
  `departureCount` 8(默认12) / `trainSpeed` 3(8) / `edgeInset` 5(2) / `smoothPasses` 3(4) / `clearTarget` **-1**(7)。
  其余 37 个全是"写完没再动过"的旋钮 ⇒ 用"prefab 现值"常量化，行为零变化。
- 结果：**18 个字段** = 任务 5（destroyTarget/escapeLimit/firstDepartureDelay/departureInterval/departureCount）
  + 列车 5（两个 prefab/firstCarCount/maxCarCount/trainSpeed）+ 火车卫队 4（squads×2 + guardSpawnBack + guardLead）
  + 轨道 3（trackCount/railPrefab/edgeInset）+ 附加炮塔 1（additionalTurrets）。
  另有 **24 个常量**：`_Rail.cs` 18（选点/A*/路基/清物）+ `_Guard.cs` 4（侧向/错开/分档/追赶系数）
  + `_Turret.cs` 1（`TurretMountName`）+ `_Train.cs` 1（`CarSpacingFactor`），都放在各自类顶部 `#region 调参常量`。
- ⚠ 唯一有意改的行为：`clearTarget` 从 prefab 里的 **-1**（所有位都置上 ⇒ 连 `Cover` 即巨型悬崖一起清）
  改成 `VegetationAndDetail`（项目口径）。`Robbery.prefab` 里那几个孤儿键（-1/旧字段）会等 Unity 下次存盘自动掉。
- 手法：`plans/slim_intercept_fields.py`（行号锚点替换字段块 + 词边界改名 + 每步 count 断言 + 先备份到 `Temp/split_backup/`）。
  改名踩点：常量按规范用帕斯卡 ⇒ 24 个标识符要同步改所有引用（`\bxxx\b` 断言命中数；
  `TerrainClearTarget` 不会被 `\bclearTarget\b` 误伤，因为大小写不同）。
- 验证：离线编译 06_Gameplay 0 错误 → MCP `refresh_unity` 真编译（dll mtime 21:37、Console 0 error）→
  `dll_members.py` 确认序列化字段只剩那 18 个、常量都在 dll 里。
- 待定：`guardLead`=24（字段）与 `trainSpeed`=3 已不匹配注释里的"建议 = 火车速度×TickTime(9)" —— 刻意保持还是跟着速度走？
"""

text = io.open(PATH, 'rb').read().decode('utf-8-sig')
raw = io.open(PATH, 'rb').read()
bom = raw.startswith(b'\xef\xbb\xbf')
eol = '\r\n' if '\r\n' in text else '\n'
note = NOTE.replace('\n', eol)
if not text.endswith(eol):
    note = eol + note
with io.open(PATH, 'wb') as f:
    f.write((b'\xef\xbb\xbf' if bom else b'') + (text + note).encode('utf-8'))
print('appended', len(note), 'chars, eol=%r bom=%s' % (eol, bom))
