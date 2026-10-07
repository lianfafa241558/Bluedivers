"""只读探测：在本机若干根目录下查找 KCPNet 源码工程（用于确认能否重编 KCPNet.dll）。

判定：目录里同时存在 `*.cs` 与（`KCPNet.csproj` 或 `*.sln`），或存在名为 LanRoomInfo.cs 的文件。
限制深度与访问错误，避免扫全盘卡住。

跑法：python -X utf8 .codebuddy/plans/find_kcp_source.py
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOTS = [r"D:\Pro", r"D:\Project", r"D:\Game", r"D:\Work", r"D:\Code", r"C:\Users\Administrator\source"]
SKIP = {"node_modules", ".git", "Library", "Temp", "obj", "bin", "Assets", "Packages"}
MAX_DEPTH = 5

hits = []
csproj_hits = []
lanroom_hits = []


def walk(root, depth=0):
    if depth > MAX_DEPTH:
        return
    try:
        entries = list(os.scandir(root))
    except OSError:
        return
    names = {e.name for e in entries}
    has_cs = any(n.endswith(".cs") for n in names)
    if any(n == "KCPNet.csproj" or n.lower() == "kcpnet.sln" for n in names) or (
            has_cs and any(n.lower().endswith(".csproj") for n in names) and "kcp" in root.lower()):
        csproj_hits.append(root)
    if "LanRoomInfo.cs" in names:
        lanroom_hits.append(root)
    if has_cs and names & {"LanRoomInfo.cs"}:
        hits.append(root)
    for e in entries:
        if not e.is_dir(follow_symlinks=False):
            continue
        if e.name in SKIP:
            continue
        walk(e.path, depth + 1)


for r in ROOTS:
    if os.path.isdir(r):
        walk(r)

print("[含 KCPNet.csproj / kcpnet.sln 的目录]")
for h in csproj_hits:
    print("  ", h)
print("[含 LanRoomInfo.cs 的目录]")
for h in lanroom_hits:
    print("  ", h)
if not csproj_hits and not lanroom_hits:
    print("   （未找到源码工程；KCPNet 仍是纯预编译库）")
