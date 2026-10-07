# -*- coding: utf-8 -*-
"""把一段笔记（UTF-8 文本文件）追加到若干目标 .md（append-only，保留各文件原有 BOM 与换行风格）。

用法：
    python append_note.py <note.txt> <target1.md> [target2.md ...]

为什么不直接用编辑器写：记忆文件（daily / topics）必须**只追加不覆盖**，
且各文件 BOM / CRLF 状态不一，字节级写入最稳。
"""
import io
import os
import sys

ROOT = r'd:\Pro\Bluedivers'


def append(target, note):
    path = target if os.path.isabs(target) else os.path.join(ROOT, target)
    raw = open(path, 'rb').read()
    bom = raw[:3] == b'\xef\xbb\xbf'
    txt = raw.decode('utf-8-sig')
    nl = '\r\n' if '\r\n' in txt else '\n'
    if not txt.endswith(nl):
        txt += nl
    txt += note.replace('\r\n', '\n').replace('\n', nl)
    with open(path, 'wb') as f:
        f.write((b'\xef\xbb\xbf' if bom else b'') + txt.encode('utf-8'))
    print('appended -> %s  (BOM=%s, nl=%s, bytes=%d)' % (path, bom, repr(nl), len(open(path, 'rb').read())))


def main():
    note = io.open(sys.argv[1], encoding='utf-8').read()
    for t in sys.argv[2:]:
        append(t, note)


main()
