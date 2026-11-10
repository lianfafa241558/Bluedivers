# -*- coding: utf-8 -*-
"""跨嵌套 prefab 追踪 GameObject 的父链与组件（stripped 对象沿 m_CorrespondingSourceObject 跳到源 prefab）。
用法: python resolve_chain.py <prefab> -- <goId> [goId...]
"""
import os, re, sys

ASSETS = r"d:\Pro\Bluedivers\Assets"
G = {
    "c661b922f5a2365469cd86394bae0e06": "TransferDamageable",
    "f9b0ce1bb69c96b49a7ac1098a7f5796": "Damageable",
}
BLOCK_RE = re.compile(r"^--- !u!(\d+) &(\d+)( stripped)?", re.M)

_guid2path = None


def guid2path(guid):
    global _guid2path
    if _guid2path is None:
        _guid2path = {}
        for dp, _, fs in os.walk(ASSETS):
            for fn in fs:
                if fn.endswith(".meta"):
                    p = os.path.join(dp, fn)
                    try:
                        with open(p, "r", encoding="utf-8", errors="ignore") as f:
                            head = f.readline() + f.readline()
                    except OSError:
                        continue
                    m = re.search(r"guid: ([0-9a-f]{32})", head)
                    if m:
                        _guid2path[m.group(1)] = p[:-5]
    return _guid2path.get(guid)


_cache = {}


def parse(path):
    if path in _cache:
        return _cache[path]
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()
    marks = [(m.start(), m.group(1), m.group(2), bool(m.group(3))) for m in BLOCK_RE.finditer(text)]
    info = {}
    for i, (start, cls, fid, stripped) in enumerate(marks):
        end = marks[i + 1][0] if i + 1 < len(marks) else len(text)
        body = text[start:end]
        d = {"cls": int(cls), "stripped": stripped, "body": body}
        src = re.search(r"m_CorrespondingSourceObject: \{fileID: (\d+), guid: ([0-9a-f]{32})", body, re.S)
        if src:
            d["src"] = (src.group(2), src.group(1))
        info[fid] = d
    _cache[path] = info
    return info


def name_of(path, fid, depth=0):
    info = parse(path).get(fid)
    if not info:
        return "?"
    m = re.search(r"m_Name: (.*)", info["body"])
    if m and m.group(1).strip():
        return m.group(1).strip()
    if "src" in info and depth < 6:
        guid, sfid = info["src"]
        p = guid2path(guid)
        if p:
            return name_of(p, sfid, depth + 1) + "*"
    return "(stripped)"


def transform_of_go(path, gofid):
    for tid, d in parse(path).items():
        if d["cls"] == 4:
            m = re.search(r"m_GameObject: \{fileID: (\d+)\}", d["body"])
            if m and m.group(1) == gofid:
                return tid
    return None


def go_of_transform(path, tid):
    d = parse(path).get(tid)
    if not d:
        return None
    m = re.search(r"m_GameObject: \{fileID: (\d+)\}", d["body"])
    return m.group(1) if m else None


def comps_of(path, gofid):
    out = []
    for tid, d in parse(path).items():
        if d["cls"] == 114:
            m = re.search(r"m_GameObject: \{fileID: (\d+)\}", d["body"])
            if m and m.group(1) == gofid:
                for guid, nm in G.items():
                    if guid in d["body"]:
                        out.append((nm, tid))
    return out


def walk(path, gofid, depth=0):
    while gofid and depth < 40:
        tid = transform_of_go(path, gofid)
        line = "  " * (depth // 2) + "GO {} {} tr={} {}".format(
            gofid, name_of(path, gofid), tid, comps_of(path, gofid) or "")
        print(line)
        if not tid:
            return
        d = parse(path).get(tid)
        m = re.search(r"m_Father: \{fileID: (\d+)\}", d["body"])
        fa = m.group(1) if m else "0"
        if fa == "0":
            return
        parent_go = go_of_transform(path, fa)
        if parent_go:
            gofid = parent_go
            depth += 2
            continue
        # 父 transform 可能是 stripped（本源 prefab 里的对象）⇒ 跳到源 prefab 继续
        fd = parse(path).get(fa)
        if fd and "src" in fd:
            guid, sfid = fd["src"]
            p = guid2path(guid)
            if p:
                pg = go_of_transform(p, sfid)
                print("  " * (depth // 2) + ">> 跳到 {}".format(os.path.basename(p)))
                path, gofid = p, pg
                depth += 2
                continue
        print("  " * (depth // 2) + "(父 transform {} 无法解析)".format(fa))
        return


if __name__ == "__main__":
    prefab = sys.argv[1]
    targets = sys.argv[sys.argv.index("--") + 1:]
    for t in targets:
        print("\n### 目标", t, name_of(prefab, t))
        walk(prefab, t)
