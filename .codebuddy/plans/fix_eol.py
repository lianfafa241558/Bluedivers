# -*- coding: utf-8 -*-
"""把改动过的文件换行符对齐回 HEAD（replace_in_file 有概率把 LF 文件整份写成 CRLF）。

用法：python fix_eol.py <path> [path...]
"""
import subprocess
import sys

ROOT = r"d:\Pro\Bluedivers"


def head_bytes(path):
    try:
        return subprocess.check_output(["git", "show", "HEAD:" + path], cwd=ROOT)
    except subprocess.CalledProcessError:
        return None


for rel in sys.argv[1:]:
    rel = rel.replace("\\", "/")
    cur = open(rel, "rb").read()
    head = head_bytes(rel)
    if head is None:
        print("no HEAD:", rel)
        continue
    cur_crlf = b"\r\n" in cur
    head_crlf = b"\r\n" in head
    if cur_crlf == head_crlf:
        print("ok  %s (crlf=%s bom=%s)" % (rel, cur_crlf, cur[:3] == b"\xef\xbb\xbf"))
        continue
    fixed = cur.replace(b"\r\n", b"\n") if not head_crlf else cur.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
    open(rel, "wb").write(fixed)
    print("FIX %s : crlf %s -> %s" % (rel, cur_crlf, head_crlf))
