"""粗解析 .prefab：列出 GameObject / Transform 父子 / 各组件挂在哪个 GO。
只用于对比 Pendant / Ether / Dodecahedron 的结构差异（为什么销毁后模型还在）。"""
import re
import sys
from pathlib import Path

OOPART_GUID = "d4e9ce9f163727144956fbda9abc62c9"


def parse(path: Path):
    text = path.read_text(encoding="utf-8", errors="replace")
    blocks = re.split(r"^--- !u!(\d+) &(-?\d+).*$", text, flags=re.M)
    # blocks: [pre, class, id, body, class, id, body, ...]
    objs = {}
    for i in range(1, len(blocks), 3):
        cls, fid, body = blocks[i], blocks[i + 1], blocks[i + 2]
        objs[(cls, fid)] = body

    gos = {}
    transforms = {}
    comps = []  # (class, fid, go_id, script_guid)
    for (cls, fid), body in objs.items():
        if cls == "1":
            m = re.search(r"m_Name: (.*)", body)
            name = m.group(1).strip() if m else "?"
            active = re.search(r"m_IsActive: (\d)", body)
            gos[fid] = (name, active.group(1) if active else "1")
        elif cls in ("4", "224"):
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
            fa = re.search(r"m_Father: \{fileID: (-?\d+)\}", body)
            kids = re.findall(r"^\s+- \{fileID: (-?\d+)\}\s*$", body, flags=re.M)
            pos = re.search(r"m_LocalPosition: \{x: ([^,]+), y: ([^}]+)\}", body)
            transforms[fid] = dict(go=go.group(1) if go else "0",
                                   father=fa.group(1) if fa else "0",
                                   kids=kids,
                                   pos=pos.groups() if pos else None)
        elif cls == "1001":
            parent = re.search(r"m_TransformParent: \{fileID: (-?\d+)\}", body)
            src = re.search(r"m_SourcePrefab: \{fileID: 100100000, guid: (\w+)", body)
            name = re.search(r"m_Name: (.*)", body)
            transforms["PI" + fid] = dict(go="<PrefabInstance:" + (src.group(1) if src else "?") + ">",
                                          father=parent.group(1) if parent else "0",
                                          kids=[], pos=None)
        else:
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
            sg = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", body)
            comps.append((cls, fid, go.group(1) if go else "0", sg.group(1) if sg else ""))

    return gos, transforms, comps


def dump(path: Path):
    print("=" * 20, path.name)
    gos, trs, comps = parse(path)
    # root transforms
    roots = [t for t, d in trs.items() if d["father"] == "0"]
    print("root count:", len(roots), roots)
    print("GameObjects:")
    for fid, (name, act) in gos.items():
        print(f"  GO {fid:>22} name={name!r} active={act}")
    print("Transforms:")
    for t, d in trs.items():
        nm = gos.get(d["go"], ("?",))[0]
        print(f"  TR {t:>22} go={d['go']}({nm!r}) father={d['father']} kids={d['kids']}")
    print("OOPart / 其他组件所在 GO:")
    for cls, fid, go, sg in comps:
        tag = "  <<< OOPart" if sg == OOPART_GUID else ""
        if sg:
            print(f"  cls={cls} comp={fid} go={go}({gos.get(go,('?',))[0]!r}) script={sg[:8]}{tag}")


if __name__ == "__main__":
    base = Path(r"d:\Pro\Bluedivers\Assets\Resources\Prefabs\Prop")
    for n in sys.argv[1:] or ["Pendant", "Ether", "Dodecahedron", "Crystal"]:
        dump(base / f"{n}.prefab")
