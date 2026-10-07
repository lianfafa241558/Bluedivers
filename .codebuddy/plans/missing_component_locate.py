# -*- coding: utf-8 -*-
"""在 prefab / 场景里定位「Missing Script 组件」分别挂在哪个 GameObject 上。

Unity 只在 Console 报一句 `The referenced script (Unknown) on this Behaviour is missing!`，
不说是哪个物体；此脚本直接给出 (组件 fileID → 宿主 GameObject 名 → 脚本 guid)。

用法：
    python missing_component_locate.py <资产路径> [脚本 guid]
不传 guid 时，自动把「在 Assets/Packages/PackageCache 的 .meta 里找不到」的 m_Script 视为缺失。
"""
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
META_GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
OBJ_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)
NAME_RE = re.compile(r"^  m_Name: (.*)$", re.M)
SCRIPT_RE = re.compile(r"^  m_Script: \{fileID: -?\d+, guid: ([0-9a-f]{32}), type: 3\}", re.M)
GO_REF_RE = re.compile(r"^  m_GameObject: \{fileID: (\d+)\}", re.M)


def known_guids():
    g = set()
    for sub in ("Assets", "Packages", os.path.join("Library", "PackageCache")):
        base = os.path.join(REPO, sub)
        if not os.path.isdir(base):
            continue
        for dp, _dn, fs in os.walk(base):
            for f in fs:
                if not f.endswith(".meta"):
                    continue
                try:
                    with open(os.path.join(dp, f), encoding="utf-8-sig", errors="replace") as fp:
                        m = META_GUID.search(fp.read(400))
                except OSError:
                    continue
                if m:
                    g.add(m.group(1))
    return g


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    path = sys.argv[1]
    if not os.path.isabs(path):
        path = os.path.join(REPO, path)
    want = sys.argv[2].lower() if len(sys.argv) > 2 else None
    text = open(path, encoding="utf-8-sig", errors="replace").read()

    # 切块
    cuts = [(m.start(), int(m.group(1)), m.group(2)) for m in OBJ_RE.finditer(text)]
    cuts.append((len(text), 0, "0"))
    go_name = {}
    blocks = []
    for i in range(len(cuts) - 1):
        blocks.append((cuts[i][1], cuts[i][2], text[cuts[i][0]:cuts[i + 1][0]]))
    for cls, fid, body in blocks:
        if cls == 1:
            m = NAME_RE.search(body)
            go_name[fid] = m.group(1).strip() if m else "?"

    known = None if want else known_guids()
    hits = []
    for cls, fid, body in blocks:
        ms = SCRIPT_RE.search(body)
        if not ms:
            continue
        g = ms.group(1)
        if want:
            if g != want:
                continue
        elif g in known or g.startswith("0000000000000000"):
            continue
        go = GO_REF_RE.search(body)
        go_fid = go.group(1) if go else "0"
        hits.append((fid, g, go_fid, go_name.get(go_fid, "<非 GameObject>")))

    print("资产 : %s" % os.path.relpath(path, REPO).replace("\\", "/"))
    print("缺失脚本组件 = %d 个" % len(hits))
    for fid, g, go_fid, nm in hits:
        print("  组件 fileID=%s  宿主 GameObject =「%s」(fileID=%s)  脚本 guid=%s" % (fid, nm, go_fid, g))


if __name__ == "__main__":
    main()
