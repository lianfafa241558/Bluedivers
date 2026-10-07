# -*- coding: utf-8 -*-
"""验证 .gitattributes 的 `* text=auto eol=lf` 是否「文本归一 LF、二进制不动」。

原理：`git hash-object --path=<路径>` 会用**该路径的 attributes** 跑一遍 clean 过滤器，
返回"入库后"的 blob 哈希 ⇒ 与"未过滤"的哈希对比即可判定是否发生了转换。
探针文件只写在 Temp/ 下（Unity 临时目录，已被 .gitignore 排除），跑完删除。
"""

import os
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROBE_DIR = os.path.join(ROOT, "Temp", "eol_probe")


def run(args, stdin=None):
    p = subprocess.run(args, cwd=ROOT, input=stdin, capture_output=True)
    return p.stdout.decode().strip(), p.stderr.decode().strip(), p.returncode


def main():
    os.makedirs(PROBE_DIR, exist_ok=True)
    text_crlf = os.path.join(PROBE_DIR, "t_crlf.txt")
    bin_crlf = os.path.join(PROBE_DIR, "b_crlf.bin")
    open(text_crlf, "wb").write(b"a\r\nb\r\n")          # 文本 + CRLF
    open(bin_crlf, "wb").write(b"A\r\n\x00B\r\n")        # 含 NUL ⇒ git 判定为二进制

    blob_lf, _, _ = run(["git", "hash-object", "--stdin"], stdin=b"a\nb\n")
    plain_text, _, _ = run(["git", "hash-object", text_crlf])
    filtered_text, _, _ = run(["git", "hash-object", "--path=Assets/probe.cs", text_crlf])
    plain_bin, _, _ = run(["git", "hash-object", bin_crlf])
    filtered_bin_cs, _, _ = run(["git", "hash-object", "--path=Assets/probe.cs", bin_crlf])
    filtered_bin_png, _, _ = run(["git", "hash-object", "--path=Assets/probe.png", bin_crlf])

    print("文本文件（内容 a-CRLF-b）：")
    print("  未过滤 blob      :", plain_text[:12])
    print("  按 .cs 过滤后     :", filtered_text[:12])
    print("  纯 LF 内容 blob   :", blob_lf[:12])
    print("  ⇒ CRLF→LF 归一生效:", "是" if filtered_text == blob_lf else "否")
    print("  原始 CRLF 未被改动:", "是" if plain_text != blob_lf else "否")

    print("\n二进制文件（含 NUL，内容 A-CRLF-NUL-B-CRLF）：")
    print("  未过滤 blob      :", plain_bin[:12])
    print("  按 .cs 路径过滤后 :", filtered_bin_cs[:12])
    print("  按 .png 路径过滤后:", filtered_bin_png[:12])
    print("  ⇒ 二进制未被转换  :", "是" if filtered_bin_cs == plain_bin == filtered_bin_png else "⚠ 否（有转换！）")

    ignored, _, _ = run(["git", "check-ignore", "-v", os.path.relpath(text_crlf, ROOT)])
    print("\nTemp 是否被忽略:", ignored or "⚠ 未忽略（探针会污染 status）")

    for f in (text_crlf, bin_crlf):
        os.remove(f)
    os.rmdir(PROBE_DIR)
    print("探针文件已清理")


if __name__ == "__main__":
    main()
