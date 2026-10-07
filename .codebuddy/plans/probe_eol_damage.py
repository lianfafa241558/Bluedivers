# -*- coding: utf-8 -*-
r"""检查重命名脚本是否改坏了行尾/编码：对比 git HEAD 版本与工作区版本的字节特征。

用法： python -X utf8 .codebuddy/plans/probe_eol_damage.py <file1> [file2...]
输出：每个文件的 HEAD/当前 行尾计数、NUL 计数、是否带 BOM。
"""
import io
import os
import subprocess
import sys

ROOT = r'd:\Pro\Bluedivers'


def head_bytes(rel):
    out = subprocess.run(['git', '--no-pager', 'show', 'HEAD:' + rel],
                         cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if out.returncode != 0:
        return None
    return out.stdout


def feats(tag, blob):
    if blob is None:
        print('  %-6s <不在 HEAD 里>' % tag)
        return
    crlf = blob.count(b'\r\n')
    lf = blob.count(b'\n') - crlf
    nul = blob.count(b'\x00')
    bom = blob[:3] == b'\xef\xbb\xbf'
    print('  %-6s bytes=%-7d CRLF=%-5d LF=%-5d NUL=%-3d BOM=%s 前40字节=%r'
          % (tag, len(blob), crlf, lf, nul, bom, blob[:40]))


def main():
    for rel in sys.argv[1:]:
        rel = rel.replace('/', os.sep)
        print(rel)
        head = head_bytes(rel.replace(os.sep, '/'))
        feats('HEAD', head)
        path = os.path.join(ROOT, rel)
        feats('NOW', open(path, 'rb').read())
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
