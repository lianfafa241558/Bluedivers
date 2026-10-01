# -*- coding: utf-8 -*-
"""按 JSON 清单给指定文件补 `using <ns>;`（字节级保 BOM），已有则跳过。
用法: python add_usings.py <pairs.json> [--apply]   # [[relpath, ns], ...]
"""
import os
import re
import sys
import json

ROOT = r'd:\Pro\Bluedivers'
APPLY = '--apply' in sys.argv
USE_ORD = re.compile(rb'(?m)^[ \t]*using\s+(?!static\b)[A-Za-z_][\w\.]*\s*;')


def add_using(b, ns):
    bom = b[:3] if b[:3] == b'\xef\xbb\xbf' else b''
    body = b[len(bom):]
    nl = b'\r\n' if b'\r\n' in body else b'\n'
    line = b'using ' + ns.encode() + b';' + nl
    us = list(USE_ORD.finditer(body))
    if us:
        pos = us[-1].end()
        if body[pos:pos + len(nl)] == nl:
            return bom + body[:pos + len(nl)] + line + body[pos + len(nl):]
        return bom + body[:pos] + nl + line + body[pos:]
    m = re.search(rb'(?m)^[ \t]*namespace\b', body)
    if m:
        return bom + body[:m.start()] + line + body[m.start():]
    return bom + line + body


def main():
    pairs = json.load(open(sys.argv[1], encoding='utf-8'))
    for rel, ns in pairs:
        p = os.path.join(ROOT, rel.replace('/', os.sep))
        b = open(p, 'rb').read()
        if re.search((r'(?m)^[ \t]*using\s+' + re.escape(ns) + r'\s*;').encode(), b):
            print('  SKIP(已有) %s' % rel)
            continue
        nb = add_using(b, ns)
        print('  +using %-22s %s' % (ns, rel))
        if APPLY:
            open(p, 'wb').write(nb)
    print('已处理（APPLY=%s）' % APPLY)


if __name__ == '__main__':
    main()
