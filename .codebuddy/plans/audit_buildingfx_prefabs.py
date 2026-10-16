"""审计：所有挂了 BuildingFXController 的 prefab，其宿主 GameObject 上是否同时有 I_AIController 实现。

依据：EnemyControllerFX.Start() 里 m_Controller = GetComponent<I_AIController>()，同 GameObject 唯一；
取不到会在订阅事件时 NRE（EnemyControllerFX.cs:29-35），所以这是硬依赖。

用法：python -X utf8 .codebuddy/plans/audit_buildingfx_prefabs.py
"""
import os
import re
import sys

ROOT = r"e:\Bluedivers"
FX_GUID = "531719c9934cdc647bacfd252c11ff06"  # BuildingFXController.cs

# 需要确认「同物体上是否存在」的 I_AIController 家族脚本
AI_CLASS_FILES = [
    "Assets/Scripts/06Gameplay/AI/Controller/AIController.cs",
    "Assets/Scripts/06Gameplay/AI/Controller/EnemyController.cs",
    "Assets/Scripts/06Gameplay/AI/Controller/OtherController.cs",
    "Assets/Scripts/06Gameplay/Npc/SpecUnitController.cs",
]

SECTION_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)


def guid_of(cs_path):
    meta = os.path.join(ROOT, cs_path.replace("/", os.sep) + ".meta")
    if not os.path.exists(meta):
        return None
    with open(meta, "r", encoding="utf-8", errors="replace") as f:
        m = re.search(r"guid:\s*([0-9a-f]{32})", f.read())
    return m.group(1) if m else None


ai_guids = {}
for p in AI_CLASS_FILES:
    g = guid_of(p)
    if g:
        ai_guids[g] = os.path.basename(p)[:-3]

print("I_AIController 家族 guid：")
for g, n in ai_guids.items():
    print(f"  {n:24} {g}")

prefabs = []
for dirpath, _, files in os.walk(ROOT):
    if "\\Library\\" in dirpath or "\\Temp\\" in dirpath or "\\.git" in dirpath:
        continue
    for fn in files:
        if fn.endswith(".prefab"):
            prefabs.append(os.path.join(dirpath, fn))
print(f"\n扫描 {len(prefabs)} 个 prefab …\n")

problems = []
ok = 0
for path in prefabs:
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    if FX_GUID not in text:
        continue

    # 切分 YAML 段
    parts = SECTION_RE.split(text)
    blocks = []  # (classId, fileId, body)
    for i in range(1, len(parts) - 2, 3):
        blocks.append((parts[i], parts[i + 1], parts[i + 2]))

    go_name = {}
    scripts_on_go = {}  # goFileId -> [guid]
    fx_go = []          # 挂了 BuildingFXController 的 go
    for classId, fid, body in blocks:
        m = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", body)
        gid = m.group(1) if m else None
        if classId == "1":  # GameObject
            n = re.search(r"m_Name:\s*(.*)", body)
            go_name[fid] = n.group(1).strip() if n else "?"
        if classId == "114" and gid:
            s = re.search(r"m_Script:\s*\{fileID:\s*\d+, guid:\s*([0-9a-f]{32})", body)
            if s:
                scripts_on_go.setdefault(gid, []).append(s.group(1))
                if s.group(1) == FX_GUID:
                    fx_go.append(gid)

    rel = os.path.relpath(path, ROOT)
    for gid in fx_go:
        found = [ai_guids[g] for g in scripts_on_go.get(gid, []) if g in ai_guids]
        if found:
            ok += 1
            print(f"[OK]   {rel} :: {go_name.get(gid, '?')} -> {', '.join(found)}")
        else:
            problems.append((rel, go_name.get(gid, "?")))
            print(f"[FAIL] {rel} :: {go_name.get(gid, '?')} -> 同物体上没有 I_AIController")

print(f"\n结果：OK {ok} 个，缺失 {len(problems)} 个")
for rel, n in problems:
    print(f"  缺失: {rel}  ({n})")
sys.exit(0)
