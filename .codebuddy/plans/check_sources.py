# -*- coding: utf-8 -*-
"""检查若干 .cs 的"行数 + 是否 UTF-8 BOM + 首行"，防"被脚本清空/丢掉 BOM"这类事故。

⚠ 空 .cs 编译是 0 错误（假通过）——所以改完文件必须像这样核一遍行数，别只看编译结果。

用法：
    python check_sources.py <文件> [<文件> ...]
"""
import io
import sys

for path in sys.argv[1:]:
    raw = io.open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    lines = text.splitlines()
    first = lines[0] if lines else ''
    print(f'{len(lines):5d} 行  BOM={"有" if bom else "无"}  {path}\n          首行: {first}')
    if len(lines) < 3:
        print('          ⚠⚠ 内容疑似被清空！')
