# -*- coding: utf-8 -*-
"""扫描 prefab/scene：找出没有被任何 Damageable.LineArmor 收录的 TransferDamageable。
这类肢体 source 永远是 null ⇒ 任何读 IDamageable.ActorGo 的地方都会 NRE。
"""
import os
import re

ROOT = r"d:\Pro\Bluedivers\Assets"
TRANSFER_GUID = "c661b922f5a2365469cd86394bae0e06"   # TransferDamageable.cs
DAMAGEABLE_GUID = "f9b0ce1bb69c96b49a7ac1098a7f5796"  # Damageable.cs

BLOCK_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)


def blocks(text):
    marks = [(m.start(), m.group(1), m.group(2)) for m in BLOCK_RE.finditer(text)]
    out = []
    for i, (start, cls, fid) in enumerate(marks):
        end = marks[i + 1][0] if i + 1 < len(marks) else len(text)
        out.append((int(cls), fid, text[start:end]))
    return out


def scan(path):
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()
    if TRANSFER_GUID not in text:
        return []
    limbs = {}       # fileID -> (goFileID, armorLevel, isWeakness)
    linked = set()
    for cls, fid, body in blocks(text):
        if cls != 114:
            continue
        if TRANSFER_GUID in body:
            m_go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            m_lv = re.search(r"armorLevel: (-?\d+)", body)
            limbs[fid] = (m_go.group(1) if m_go else "?", m_lv.group(1) if m_lv else "?")
        elif DAMAGEABLE_GUID in body:
            m_armor = re.search(r"LineArmor:(.*?)(?=\n  \w|\n--- )", body, re.S)
            if m_armor:
                for ref in re.findall(r"fileID: (\d+)", m_armor.group(1)):
                    linked.add(ref)
    return [(fid, info) for fid, info in limbs.items() if fid not in linked]


def main():
    bad = 0
    for dirpath, _, files in os.walk(ROOT):
        for name in files:
            if not name.endswith((".prefab", ".unity")):
                continue
            path = os.path.join(dirpath, name)
            miss = scan(path)
            if miss:
                bad += 1
                print(os.path.relpath(path, ROOT))
                for fid, (go, lv) in miss:
                    print("    TransferDamageable &{}  m_GameObject={}  armorLevel={}".format(fid, go, lv))
    print("\n共 {} 个文件存在未连 source 的肢体".format(bad))


if __name__ == "__main__":
    main()
