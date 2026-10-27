# -*- coding: utf-8 -*-
"""静态校验：Logic 武器的 19 个改装资产与 prefab 引用是否自洽（不依赖 Unity）。"""
import glob
import os
import re
import shutil

ROOT = r"d:/Pro/Bluedivers"
PREFAB = os.path.join(ROOT, "Assets/Resources/Weapons/Logic.prefab")
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.M)

ok = True


def fail(msg):
    global ok
    ok = False
    print("  [FAIL]", msg)


metas = sorted(glob.glob(os.path.join(ROOT, "Assets/Resources/GameData/WeaponUpgrade/Logic/*.asset.meta")) +
               glob.glob(os.path.join(ROOT, "Assets/Resources/GameData/WeaponModule/Logic/*.asset.meta")))
print("改装资产 meta 数：", len(metas))
if len(metas) != 19:
    fail("期望 19 个（12 WUD + 7 WMD），实际 %d" % len(metas))

g2f, dup = {}, []
for p in metas:
    with open(p, encoding="utf-8") as f:
        m = GUID_RE.search(f.read())
    if not m:
        fail("meta 无 guid: " + p)
        continue
    g = m.group(1)
    if g in g2f:
        dup.append((g, g2f[g], p))
    g2f[g] = p
if dup:
    fail("guid 重复: %s" % dup)

with open(PREFAB, encoding="utf-8") as f:
    prefab = f.read()

missing = [(g, os.path.basename(p)) for g, p in g2f.items() if ("guid: " + g) not in prefab]
if missing:
    fail("prefab 未引用: %s" % missing)

# 档位结构 3/2/2/2/3
upg = re.search(r"(?ms)^  Upgrade:\n(.*?)^  Modules:", prefab)
if not upg:
    fail("找不到 Upgrade 块")
else:
    body = upg.group(1)
    keys = re.findall(r"(?m)^  - Key: (\d+)", body)
    counts = [len(re.findall(r"guid:", seg)) for seg in re.split(r"(?m)^  - Key: \d+", body)[1:]]
    print("Upgrade Keys:", keys, " 每档项数:", counts)
    if keys != ["2", "4", "8", "12", "16"]:
        fail("档位 Key 不是 2/4/8/12/16")
    if counts != [3, 2, 2, 2, 3]:
        fail("每档项数不是 3/2/2/2/3")

# Modules 8 项且 [0] = WMD_None
mods = re.search(r"(?ms)^  Modules:\n(.*?)^  ActiveModule:", prefab)
if not mods:
    fail("找不到 Modules 块")
else:
    items = re.findall(r"guid: ([0-9a-f]{32})", mods.group(1))
    print("Modules 数:", len(items), " 首项:", items[0] if items else None)
    if len(items) != 8 or items[0] != "a19bae1a0785866409ae31f377bde9cf":
        fail("Modules 应为 8 项且 [0]=WMD_None")

# showAttr 10 项
m = re.search(r"(?m)^  showAttr: ([0-9a-fA-F]+)$", prefab)
if not m:
    fail("找不到 showAttr")
else:
    h = m.group(1)
    vals = [int.from_bytes(bytes.fromhex(h[i:i + 8]), "little") for i in range(0, len(h), 8)]
    print("showAttr:", vals)
    if vals != [20, 2, 1, 0, 25, 102, 100, 9, 7, 10]:
        fail("showAttr 内容不符")

# cfg 追加项
for key in ("- Key: 9\n      Value: 0.7", "- Key: 100\n      Value: 1.0", "- Key: 102\n      Value: 0.0"):
    if key not in prefab:
        fail("cfg 缺少 %s" % key.replace("\n", " "))

# 备份挪出 Assets（避免污染工程）
bak = PREFAB + ".bak"
if os.path.exists(bak):
    dst_dir = os.path.join(ROOT, ".codebuddy/plans/backup")
    os.makedirs(dst_dir, exist_ok=True)
    shutil.move(bak, os.path.join(dst_dir, "Logic.prefab.bak"))
    print("备份已移到 .codebuddy/plans/backup/Logic.prefab.bak")

print("RESULT:", "OK" if ok else "FAILED")
