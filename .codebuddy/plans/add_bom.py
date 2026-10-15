# -*- coding: utf-8 -*-
"""给指定 .cs 补 UTF-8 BOM（项目规范）。

⚠ 必须"先读进内存、再打开写"：写成 open(p,'w').write(open(p).read()) 会先截断文件，
读到的就是空字符串 —— 2026-10-15 实际把 TerrainMainUtils_Reserved.cs 清空过一次。

用法：
    python add_bom.py <文件> [<文件> ...]
"""
import io
import sys

for path in sys.argv[1:]:
    text = io.open(path, encoding='utf-8-sig').read()
    io.open(path, 'w', encoding='utf-8-sig', newline='').write(text)
    print('BOM ok:', path, len(text))
