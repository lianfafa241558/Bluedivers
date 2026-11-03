"""扫描所有带 Parabolic 的武器 prefab：WeaponMuzzle 指向哪个节点，是否等于 Parabolic / WeaponRoot。"""
import io
import os
import re

ROOT = r"d:\Pro\Bluedivers\Assets\Resources\Weapons"


def parse(path):
    text = io.open(path, encoding="utf-8").read()
    # fileID -> GameObject 名（Transform 块里 m_GameObject 指向 GameObject）
    go_name = {}
    for m in re.finditer(r"--- !u!1 &(-?\d+)\nGameObject:\n(.*?)(?=\n--- |\Z)", text, re.S):
        nm = re.search(r"m_Name: (.*)", m.group(2))
        go_name[m.group(1)] = nm.group(1).strip() if nm else "?"
    tr_go = {}
    for m in re.finditer(r"--- !u!4 &(-?\d+)\nTransform:\n(.*?)(?=\n--- |\Z)", text, re.S):
        g = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", m.group(2))
        if g:
            tr_go[m.group(1)] = go_name.get(g.group(1), "（stripped/外部）")
    # stripped Transform（prefab 实例）
    for m in re.finditer(r"--- !u!4 &(-?\d+) stripped\nTransform:\n(.*?)(?=\n--- |\Z)", text, re.S):
        tr_go[m.group(1)] = "（prefab实例根）"
    # Parabolic 实例的 stripped transform id
    para = set()
    for m in re.finditer(r"--- !u!4 &(-?\d+) stripped\nTransform:\n\s+m_CorrespondingSourceObject: \{fileID: 9186266124265134975", text):
        para.add(m.group(1))

    wroot = re.search(r"WeaponRoot: \{fileID: (-?\d+)\}", text)
    wmz = re.search(r"WeaponMuzzle: \{fileID: (-?\d+)\}", text)
    wroot_name = tr_go.get(wroot.group(1), "?") if wroot else "-"
    wmz_id = wmz.group(1) if wmz else "-"
    return wmz_id, tr_go.get(wmz_id, "?"), wroot_name, wmz_id in para


for dirpath, _, files in os.walk(ROOT):
    for f in files:
        if not f.endswith(".prefab"):
            continue
        p = os.path.join(dirpath, f)
        txt = io.open(p, encoding="utf-8").read()
        if "9186266124265134975" not in txt:
            continue
        wid, wname, rname, is_para = parse(p)
        print(f"{f:28s} muzzle={wid:>22s} 节点={wname:16s} WeaponRoot={rname:12s} 是Parabolic={is_para}")
