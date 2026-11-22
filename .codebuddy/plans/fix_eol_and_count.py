import io
import subprocess

p = r"d:\Pro\Bluedivers\Assets\Shader\ToonLit\Main\ToonLit_Shared.hlsl"

data = open(p, "rb").read()
print("workfile bytes", len(data), "crlf", data.count(b"\r\n"), "lf_total", data.count(b"\n"))

head = subprocess.run(
    ["git", "cat-file", "blob", "HEAD:Assets/Shader/ToonLit/Main/ToonLit_Shared.hlsl"],
    cwd=r"d:\Pro\Bluedivers", capture_output=True).stdout
print("head bytes", len(head), "crlf", head.count(b"\r\n"), "lf_total", head.count(b"\n"))

# 统一回 LF，保持与 HEAD 一致（text=auto eol=lf）
fixed = data.replace(b"\r\n", b"\n")
open(p, "wb").write(fixed)
print("after fix crlf", fixed.count(b"\r\n"), "lf_total", fixed.count(b"\n"))
