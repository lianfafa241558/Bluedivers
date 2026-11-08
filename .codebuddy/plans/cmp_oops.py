"""对比 8 个欧帕兹预制体的关键字段，找出 Pendant/Ether 与其它（Dodecahedron 等）的差异。"""
import re
from pathlib import Path

BASE = Path(r"d:\Pro\Bluedivers\Assets\Resources\Prefabs\Prop")
OOPART = "d4e9ce9f163727144956fbda9abc62c9"   # OOPart.cs
BASEOBJ = "669a96ab6487d5b47a05a4f0d3c637ce"  # BaseObject.cs
SCALE = {"65c97e615879fd645a8fe306f6b86910": "StartNearTerrain",
         "259183c26a0b02042811185de07fe558": "VerticalTerrain",
         "2760b2c0a27a2ed4b8430df95b03e394": "StartRandomRorate"}

rows = []
for p in sorted(BASE.glob("*.prefab")):
    t = p.read_text(encoding="utf-8", errors="replace")
    blocks = re.findall(r"^--- !u!(\d+) &(-?\d+).*?\n(.*?)(?=^--- |\Z)", t, flags=re.S | re.M)
    scripts = []
    oop = base = None
    collider = None
    for cls, fid, body in blocks:
        sg = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", body)
        if sg:
            g = sg.group(1)
            if g == OOPART:
                oop = body
            elif g == BASEOBJ:
                base = body
            else:
                scripts.append(SCALE.get(g, g[:8]))
        if cls in ("65", "64") and "m_IsTrigger" in body:
            collider = body

    def f(body, key, default="-"):
        if body is None:
            return default
        m = re.search(rf"^  {key}: (.+)$", body, flags=re.M)
        return m.group(1).strip() if m else default

    rows.append(dict(
        file=p.stem,
        oop_flags=f(oop, "flags"),
        oop_ExtFloat=f(oop, "ExtFloatParameter"),
        oop_meetTime=f(oop, "meetTime"),
        oop_Id=f(oop, "Id"),
        oop_Portrait=("null" if "Portrait: {fileID: 0}" in (oop or "") else "icon"),
        bo_Id=f(base, "Id"),
        bo_ShowName=f(base, "ShowName"),
        bo_Portrait=("null" if "Portrait: {fileID: 0}" in (base or "") else "icon"),
        bo_Extra=("null" if "ExtraPortrait: {fileID: 0}" in (base or "") else "icon"),
        coll_trigger=("TRIGGER" if "m_IsTrigger: 1" in (collider or "") else "SOLID"),
        coll_size=f(collider, "m_Size"),
        extra_scripts=",".join(scripts),
        go_count=t.count("--- !u!1 "),
    ))

cols = list(rows[0].keys())
widths = {c: max(len(c), *(len(str(r[c])) for r in rows)) for c in cols}
print(" | ".join(c.ljust(widths[c]) for c in cols))
print("-+-".join("-" * widths[c] for c in cols))
for r in rows:
    print(" | ".join(str(r[c]).ljust(widths[c]) for c in cols))
