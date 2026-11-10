# -*- coding: utf-8 -*-
"""dump 指定 prefab 里 Damageable / TransferDamageable 的结构，用于判断肢体该挂到哪个 Damageable"""
import re, sys

TRANSFER_GUID = "c661b922f5a2365469cd86394bae0e06"
DAMAGEABLE_GUID = "f9b0ce1bb69c96b49a7ac1098a7f5796"
BLOCK_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)


def load(path):
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        return f.read()


def blocks(text):
    marks = [(m.start(), m.group(1), m.group(2)) for m in BLOCK_RE.finditer(text)]
    out = []
    for i, (start, cls, fid) in enumerate(marks):
        end = marks[i + 1][0] if i + 1 < len(marks) else len(text)
        out.append((int(cls), fid, text[start:end]))
    return out


def go_names(bs):
    names = {}
    for cls, fid, body in bs:
        if cls == 1:
            m = re.search(r"m_Name: (.*)", body)
            if m:
                names[fid] = m.group(1).strip()
    return names


def main(path):
    text = load(path)
    bs = blocks(text)
    names = go_names(bs)
    print("==", path, "总块数", len(bs))
    for cls, fid, body in bs:
        if cls != 114:
            continue
        kind = None
        if TRANSFER_GUID in body:
            kind = "TransferDamageable"
        elif DAMAGEABLE_GUID in body:
            kind = "Damageable"
        if not kind:
            continue
        go = re.search(r"m_GameObject: \{fileID: (\d+)\}", body)
        go = go.group(1) if go else "?"
        extra = ""
        if kind == "TransferDamageable":
            lv = re.search(r"armorLevel: (-?\d+)", body)
            extra = "armorLevel=" + (lv.group(1) if lv else "?")
        else:
            av = re.search(r"armorValue: (-?\d+)", body)
            ma = re.search(r"LineArmor:(.*?)(?=\n  \w|\n--- )", body, re.S)
            refs = re.findall(r"fileID: (\d+)", ma.group(1)) if ma else []
            extra = "armorValue={} LineArmor=[{}]".format(
                av.group(1) if av else "?", ", ".join(refs))
        print("  &{} {:<22} GO={} ({})  {}".format(fid, kind, go, names.get(go, "?"), extra))


if __name__ == "__main__":
    for p in sys.argv[1:]:
        main(p)
