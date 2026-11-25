# -*- coding: utf-8 -*-
"""把 .cs 规范化成「UTF-8 with BOM + CRLF」（项目规范），保持与 HEAD 一致的换行符。

⚠ 先读进内存再写：禁止 open(p,'w').write(open(p).read())（先截断后读 ⇒ 清空文件）。

用法：
    python normalize_cs_encoding.py <文件> [<文件> ...]
"""
import io
import sys

for path in sys.argv[1:]:
    raw = io.open(path, 'rb').read()
    if raw.startswith(b'\xef\xbb\xbf'):
        raw = raw[3:]
    text = raw.decode('utf-8').replace('\r\n', '\n')
    out = '\ufeff' + text.replace('\n', '\r\n')
    io.open(path, 'wb').write(out.encode('utf-8'))
    print('normalized:', path, len(text), 'chars')
