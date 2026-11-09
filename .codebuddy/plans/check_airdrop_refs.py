"""一次性体检：把每个战备 SO 的 creatObect 引用解析成路径，报告 NULL / 丢失。

用法：python .codebuddy/plans/check_airdrop_refs.py
"""
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "Assets")

guid_map = {}
for dirpath, _, files in os.walk(ASSETS):
    for fn in files:
        if not fn.endswith(".meta"):
            continue
        p = os.path.join(dirpath, fn)
        try:
            with open(p, "r", encoding="utf-8", errors="ignore") as f:
                for _ in range(3):
                    line = f.readline()
                    if not line:
                        break
                    m = re.match(r"guid:\s*([0-9a-f]{32})", line)
                    if m:
                        guid_map.setdefault(m.group(1), p[:-5].replace(ROOT + os.sep, ""))
                        break
        except OSError:
            pass

print("meta mapped:", len(guid_map))

adir = os.path.join(ASSETS, "Resources", "GameData", "Airdrop")
pat = re.compile(r"creatObect:\s*\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-f]{32}))?")
for fn in sorted(os.listdir(adir)):
    if not fn.endswith(".asset"):
        continue
    with open(os.path.join(adir, fn), "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()
    m = pat.search(text)
    if not m:
        print(f"{fn[:-6]:<26} <no field>")
        continue
    fid, guid = m.group(1), m.group(2)
    if fid == "0":
        path = "<NULL>"
    elif guid and guid in guid_map:
        path = guid_map[guid]
        if not os.path.isfile(os.path.join(ROOT, path)):
            path = f"<FILE GONE: {path}>"
    elif guid:
        path = f"<MISSING guid {guid}>"
    else:
        path = "<NO GUID>"
    print(f"{fn[:-6]:<26} {path}")
