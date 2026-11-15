# -*- coding: utf-8 -*-
"""只读：对比两份「单位快照」txt（UnitSnapshot*）。

产出：
  1) 同 netId 在两份里的 名字 / 位置 差异（>0.5m 或名字不同 ⇒ 真问题）
  2) 各自独有的 netId 数量与按类型分组（判断"某端少生成/多生成"）
  3) 同一份里"同 netId 不同名字"或"同名字重复出现"的可疑项

跑法：python -X utf8 .codebuddy/plans/_cmp_unitsnapshot.py <A.txt> <B.txt>
"""
import io
import re
import sys
from collections import defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

BLOCK = re.compile(r"^=====\s*(?P<time>[0-9: \-]+)\s+机器=(?P<machine>\S+)\s+端=(?P<side>\S+)\s*=====")
ROW = re.compile(r"^\s+(?P<type>\S+)\s+(?P<name>.+?)\s+netId=(?P<net>-?\d+)\s+pos=\((?P<x>-?[\d.]+),(?P<y>-?[\d.]+),(?P<z>-?[\d.]+)\)")


def parse(path):
    """返回 records: list of dict(time, side, type, name, net, pos)"""
    recs = []
    time = side = ""
    for line in io.open(path, encoding="utf-8", errors="replace"):
        m = BLOCK.match(line)
        if m:
            time, side = m.group("time").strip(), m.group("side")
            continue
        m = ROW.match(line)
        if m:
            recs.append({
                "time": time, "side": side,
                "type": m.group("type"), "name": m.group("name").strip(),
                "net": int(m.group("net")),
                "pos": (float(m.group("x")), float(m.group("y")), float(m.group("z"))),
            })
    return recs


def main():
    a_path, b_path = sys.argv[1], sys.argv[2]
    a, b = parse(a_path), parse(b_path)
    print("A =", a_path.split("\\")[-1], "记录", len(a), " 时间跨度", a[0]["time"], "→", a[-1]["time"])
    print("B =", b_path.split("\\")[-1], "记录", len(b), " 时间跨度", b[0]["time"], "→", b[-1]["time"])

    def index(recs):
        d = {}
        for r in recs:
            if r["net"] == 0:
                continue          # netId=0 的（玩家 / 未盖章的盟友）不参与按号对比
            d.setdefault(r["net"], r)   # 首次出现即可（位置会动，后面统一用首条）
        return d

    ia, ib = index(a), index(b)
    common = sorted(set(ia) & set(ib))
    only_a = sorted(set(ia) - set(ib))
    only_b = sorted(set(ib) - set(ia))
    print(f"\n同号 {len(common)} 个 | 仅 A {len(only_a)} | 仅 B {len(only_b)}")

    print("\n=== 同号但名字不同（严重：号被别的东西占用） ===")
    bad = 0
    for n in common:
        if ia[n]["name"] != ib[n]["name"]:
            bad += 1
            print(f"  netId={n}  A={ia[n]['name']}({ia[n]['type']})  B={ib[n]['name']}({ib[n]['type']})")
    if not bad:
        print("  （无）")

    print("\n=== 同号同名字但位置差 > 0.5m ===")
    far = 0
    for n in common:
        if ia[n]["name"] != ib[n]["name"]:
            continue
        pa, pb = ia[n]["pos"], ib[n]["pos"]
        d = max(abs(pa[0] - pb[0]), abs(pa[1] - pb[1]), abs(pa[2] - pb[2]))
        if d > 0.5:
            far += 1
            print(f"  netId={n} {ia[n]['name']:<10} Δ={d:6.2f}m  A={pa}  B={pb}")
    if not far:
        print("  （无）")

    def by_type(ids, idx):
        c = defaultdict(int)
        for n in ids:
            c[idx[n]["type"]] += 1
        return dict(sorted(c.items()))

    print("\n=== 各自独有（按类型计数） ===")
    print("  仅 A:", by_type(only_a, ia))
    print("  仅 B:", by_type(only_b, ib))
    # 独有里属于 Enemy/SpecUnit 的号段值得单独看（生成族对不上）
    for tag, ids, idx in (("仅 A", only_a, ia), ("仅 B", only_b, ib)):
        ent = [idx[n] for n in ids if idx[n]["type"] in ("Enemy", "SpecUnit", "Friend")]
        if ent:
            print(f"  {tag} 里的单位（前 20）：")
            for r in ent[:20]:
                print(f"    {r['type']:<9} {r['name']:<10} netId={r['net']} pos={r['pos']} t={r['time']}")

    print("\n=== 同号但出现了多个不同名字（同号复用/撞号） ===")
    for tag, recs in (("A", a), ("B", b)):
        m = defaultdict(set)
        for r in recs:
            if r["net"] != 0:
                m[r["net"]].add(r["name"])
        clash = {k: v for k, v in m.items() if len(v) > 1}
        print(f"  {tag}: {len(clash)} 个" + ("  " + str(list(clash.items())[:10]) if clash else ""))


if __name__ == "__main__":
    main()
