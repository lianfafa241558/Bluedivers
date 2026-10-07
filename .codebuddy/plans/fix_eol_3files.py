# -*- coding: utf-8 -*-
"""把指定文件统一为 UTF-8 with BOM + CRLF（仓库约定），并断言无 U+FFFD。
用法：python fix_eol_3files.py <文件...>
"""
import sys

for p in sys.argv[1:]:
    data = open(p, 'rb').read()
    bom = data.startswith(b'\xef\xbb\xbf')
    body = data[3:] if bom else data
    text = body.decode('utf-8')
    assert '\ufffd' not in text, '有乱码: ' + p
    text = text.replace('\r\n', '\n').replace('\r', '\n').replace('\n', '\r\n')
    out = b'\xef\xbb\xbf' + text.encode('utf-8')
    open(p, 'wb').write(out)
    print(p, 'bom=%s crlf=%s bytes=%d' % (bom, b'\r\n' in out, len(out)))
