# -*- coding: utf-8 -*-
"""打印指定 GameObject 的父链，并在每一级标注是否带 Damageable / TransferDamageable / Health / Actor"""
import re, sys

G = {
    "c661b922f5a2365469cd86394bae0e06": "TransferDamageable",
    "f9b0ce1bb69c96b49a7ac1098a7f5796": "Damageable",
}
BLOCK_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)


def blocks(text):
    marks = [(m.start(), m.group(1), m.group(2)) for m in BLOCK_RE.finditer(text)]
    out = []
    for i, (start, cls, fid) in enumerate(marks):
        end = marks[i + 1][0] if i + 1 < len(marks) else len(text)
        out.append((int(cls), fid, text[start:end]))
    return out


def main(path, targets):
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()
    bs = blocks(text)
    gos, trs, comps = {}, {}, {}     # go->name, go->transformId, go->[组件名]
    tr_owner, tr_father = {}, {}
    for cls, fid, body in bs:
        if cls == 1:
            m = re.search(r"m_Name: (.*)\n", body)
            gos[fid] = (m.group(1).strip() if m else "?") or "(stripped)"
        elif cls == 4:
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            fa = re.search(r"m_Father: \{fileID: (\d+)\}", body)
            if go:
                tr_owner[fid] = go.group(1)
                gos.setdefault(go.group(1), "(stripped)")
                trs[go.group(1)] = fid
            tr_father[fid] = fa.group(1) if fa else "0"
        elif cls == 114:
            go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
            if not go:
                continue
            for guid, name in G.items():
                if guid in body:
                    comps.setdefault(go.group(1), []).append(name)
    for t in targets:
        print("\n### 目标 GO", t, gos.get(t, "?"))
        cur = t
        while cur and cur != "0":
            tr = trs.get(cur, "?")
            print("  GO {} ({})  tr={}  组件={}".format(
                cur, gos.get(cur, "?"), tr, comps.get(cur, [])))
            fa = tr_father.get(tr, "0")
            cur = tr_owner.get(fa) if fa and fa != "0" else None
        print("  ↑ 已到根")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[3:])
    # 用法: python chain_limbs.py <prefab> -- <goId> [goId...]
