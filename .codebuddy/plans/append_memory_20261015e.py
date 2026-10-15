# -*- coding: utf-8 -*-
"""追加 2026-10-15 的「文件被清空」事故与防护（可重跑）。"""
import io, os

path = r'e:\Bluedivers\.codebuddy\memory\2026-10-15.md'
text = io.open(path, encoding='utf-8', errors='ignore').read() if os.path.exists(path) else ''

text += """

## 事故复盘：我把两个 .cs 清空了（用户发现「MissionInterceptTrain.cs 变 0 行」）
- 起因：用一行 python 补 BOM —— `io.open(p,'w',...).write(io.open(p,encoding='utf-8-sig').read())`。**参数先于调用求值**，`open(p,'w')` 会先截断文件，再读就是空串 ⇒ 写成只剩 BOM 的 3 字节文件
- 一次清了两个：TerrainMainUtils_Reserved.cs（当时发现并修了）+ MissionInterceptTrain.cs（**漏掉**，因为随后 06_Gameplay 编译仍报 0 错误 —— 空文件天然编译通过，是「假通过」；我据此说了"三证齐"，是错的）
- 发现方式：用户报告行数变 0；`Get-ChildItem` 显示 .cs = 3 字节而 .meta 仍是 11:02（未动，所以 prefab 的脚本引用没断）
- 修复：write_to_file 原样写回（625 行 / 31KB）→ `plans/add_bom.py` 补 BOM → `plans/check_sources.py` 核行数 → 离线编译 4 程序集 0 错误 → refresh_unity 后 06_Gameplay.dll / 09_Managers.dll 15:28:50 前进、Console 0 error
- 固化防护：`.codebuddy/plans/add_bom.py`（**先读进内存再打开写**）+ `.codebuddy/plans/check_sources.py`（打印行数/BOM/首行，<3 行就告警）
- ⭐ 结论规则：**改完 .cs 不能只看编译结果**（空 .cs 也是 0 错误）——必须核行数或内容；批量/编码类脚本收尾一律跑 check_sources.py
"""

io.open(path, 'w', encoding='utf-8').write(text)
print('ok', len(text))
