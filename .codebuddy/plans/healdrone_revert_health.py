# -*- coding: utf-8 -*-
"""回滚 healdrone_add_health.py 的误写（模板块匹配跨了多个 YAML 块）。

- Normal / Fire：本次之前 = HEAD 原样 ⇒ 直接从 git 取回字节。
- Laser：本身有未提交的武器数值改动 ⇒ 只截掉本次追加的块 + 还原 m_AddedComponents 为空列表。
"""
import re
import subprocess

ROOT = r"d:\Pro\Bluedivers"
FILES_RESET = [
    "Assets/Resources/Prefabs/Airdrop/Healdrone_Normal.prefab",
    "Assets/Resources/Prefabs/Airdrop/Healdrone_Fire.prefab",
]
LASER = "Assets/Resources/Prefabs/Airdrop/Healdrone_Laser.prefab"
LASER_HID = "1259886069420472185"


def git_show(path):
    return subprocess.check_output(
        ["git", "show", "HEAD:" + path.replace("\\", "/")], cwd=ROOT)


for path in FILES_RESET:
    data = git_show(path)
    with open(path, "wb") as f:
        f.write(data)
    print("reset from HEAD:", path, len(data), "bytes")

with open(LASER, "r", encoding="utf-8", newline="") as f:
    text = f.read()
marker = "--- !u!114 &" + LASER_HID
i = text.find(marker)
if i < 0:
    print("laser: 找不到本次追加的块（可能已清理）")
else:
    text = text[:i]
    restored, n = re.subn(
        r"(?ms)^    m_AddedComponents:\r?\n"
        r"    - targetCorrespondingSourceObject: \{fileID: 3934286715313500991, guid: 2f7f2b02984600145bdbad132e552728,\r?\n"
        r"        type: 3\}\r?\n"
        r"      insertIndex: -1\r?\n"
        r"      addedObject: \{fileID: " + LASER_HID + r"\}\r?\n",
        "    m_AddedComponents: []\n", text)
    print("laser: 截断到", i, "字节；m_AddedComponents 还原", n, "处")
    if n != 1:
        raise SystemExit("laser 的 m_AddedComponents 没还原成功，停手")
    with open(LASER, "w", encoding="utf-8", newline="") as f:
        f.write(restored)
