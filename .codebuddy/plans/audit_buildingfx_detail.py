"""详细打印 5 个可疑 prefab 里 BuildingFXController 宿主 GameObject 的组件构成。

用法：python -X utf8 .codebuddy/plans/audit_buildingfx_detail.py
"""
import os
import re

ROOT = r"e:\Bluedivers"
FX_GUID = "531719c9934cdc647bacfd252c11ff06"

FILES = [
    r"Assets\Art\Prefabs\Object\ContainerYellow.prefab",
    r"Assets\Resources\Prefabs\Airdrop\Artillery Player.prefab",
    r"Assets\Resources\Prefabs\Airdrop\MineLauncher.prefab",
    r"Assets\Resources\Prefabs\GameEvent\TrainEngineSam.prefab",
    r"Assets\Resources\Prefabs\InterestPoint\CampA.prefab",
    # 对照组（审计通过）
    r"Assets\Art\Prefabs\Object\ContainerBlue.prefab",
    r"Assets\Resources\Prefabs\Airdrop\MineLauncher_Fire.prefab",
]

SECTION_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)


def load(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        return f.read()


def report(rel):
    path = os.path.join(ROOT, rel)
    text = load(path)
    parts = SECTION_RE.split(text)
    blocks = []
    for i in range(1, len(parts) - 2, 3):
        blocks.append((parts[i], parts[i + 1], parts[i + 2]))

    print("=" * 90)
    print(rel)
    print(f"  段数={len(blocks)}  含 PrefabInstance={'!u!1001' in text}  "
          f"含 stripped={'!u!1001' in text}")

    fx_gos = []
    go_names = {}
    comps_on_go = {}
    gos_exist = set()
    for cid, fid, body in blocks:
        m = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", body)
        gid = m.group(1) if m else None
        if cid == "1":
            gos_exist.add(fid)
            n = re.search(r"m_Name:\s*(.*)", body)
            go_names[fid] = (n.group(1).strip() if n else "?")
        if cid == "114":
            s = re.search(r"m_Script:\s*\{fileID:\s*\d+, guid:\s*([0-9a-f]{32})", body)
            guid = s.group(1) if s else "?"
            name = re.search(r"m_Name:\s*(.*)", body)
            comps_on_go.setdefault(gid, []).append((guid, name.group(1).strip() if name else ""))
            if guid == FX_GUID:
                fx_gos.append((fid, gid))

    for fx_fid, gid in fx_gos:
        print(f"  FX 组件 fileID={fx_fid}  宿主 GO fileID={gid}  "
              f"GO 在本文件内={'是' if gid in gos_exist else '否(来自嵌套 prefab/实例覆盖)'}  "
              f"名字={go_names.get(gid, '?')}")
        for guid, cname in comps_on_go.get(gid, []):
            print(f"      - {guid}  name='{cname}'")


for rel in FILES:
    report(rel)
