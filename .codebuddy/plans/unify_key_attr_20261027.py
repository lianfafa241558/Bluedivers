# -*- coding: utf-8 -*-
"""把 DTO 里 [Key(n)] 属性统一成**与成员同一行**（`[Key(0)] public uint Sid;`）。

理由：仓库里已有 154 处同行、57 处分行（分行集中在 RoomMsg/PingMsg）；
同行更紧凑、且"一个字段一行"读起来更快。纯格式改动，语义不变。

用法： python unify_key_attr_20261027.py [--apply]
"""
import os
import re
import sys

ROOT = os.environ.get('BLUEDIVERS_ROOT') or os.path.abspath(
    os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))

ATTR_ONLY = re.compile(r'^(\s*)\[Key\((\d+)\)\]\s*$')
MEMBER = re.compile(r'^\s*(?:public|private|protected|internal)\s+\S')


def read_text(p):
    raw = open(p, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    crlf = '\r\n' in text
    return text.replace('\r\n', '\n').split('\n'), bom, crlf


def write_text(p, lines, bom, crlf):
    text = '\n'.join(lines)
    if crlf:
        text = text.replace('\n', '\r\n')
    raw = text.encode('utf-8')
    if bom:
        raw = b'\xef\xbb\xbf' + raw
    with open(p, 'wb') as f:
        f.write(raw)


def main():
    apply = '--apply' in sys.argv
    targets, total = [], 0

    for dirpath, _dirs, files in os.walk(os.path.join(ROOT, 'Assets', 'Scripts')):
        for fn in files:
            if not fn.endswith('.cs'):
                continue
            p = os.path.join(dirpath, fn)
            lines, bom, crlf = read_text(p)
            if not any(ATTR_ONLY.match(l) for l in lines):
                continue

            out, i, merged = [], 0, 0
            while i < len(lines):
                m = ATTR_ONLY.match(lines[i])
                if m and i + 1 < len(lines) and MEMBER.match(lines[i + 1]):
                    out.append('%s[Key(%s)] %s' % (m.group(1), m.group(2), lines[i + 1].strip()))
                    i += 2
                    merged += 1
                    continue
                out.append(lines[i])
                i += 1

            if merged:
                targets.append((p, out, bom, crlf, merged))
                total += merged

    for p, _o, _b, _c, n in targets:
        print('%-42s merge %d' % (os.path.relpath(p, ROOT), n))
    print('TOTAL merged = %d' % total)

    if not apply:
        print('[dry-run] 未写盘')
        return 0
    for p, out, bom, crlf, _n in targets:
        write_text(p, out, bom, crlf)
    print('已写盘 %d 个文件' % len(targets))
    return 0


if __name__ == '__main__':
    sys.exit(main())
