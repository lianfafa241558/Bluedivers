# -*- coding: utf-8 -*-
"""静态校验：spec 生成的资产与磁盘 / prefab 是否自洽。不依赖 Unity，可在 CI 里跑。

    python verify_weapon_tree.py --spec examples/Logic.json

检查项：
  1. 生成阶段无 error（属性存在 / 修饰符合法 / 属性有真实 base / 每档 ≤3）
  2. 每个资产与 meta 在磁盘上、且内容与 spec 渲染结果逐字节一致
  3. prefab 里的 Upgrade/Modules 引用全部来自本 spec（不多不少）
  4. meta guid 唯一，且与"由 salt+资产名派生"的期望值一致
退出码：0 = OK，1 = FAIL（可挂 hook）。
"""

import argparse
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_weapon_tree as gen  # noqa: E402

for _s in (sys.stdout, sys.stderr):   # Windows 控制台默认 GBK
    try:
        _s.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", required=True)
    ap.add_argument("--root", default=None)
    args = ap.parse_args()

    root = args.root or gen.project_root(os.path.dirname(os.path.abspath(__file__)))
    with open(args.spec, encoding="utf-8") as f:
        spec = json.load(f)

    res = gen.build(spec, root)
    fails = ["生成: " + e for e in res["errors"]]
    if fails:
        for e in fails:
            print("  [FAIL]", e)
        print("RESULT: FAILED")
        return 1

    # 1) 资产与 meta 逐字节比对
    for rel, text in sorted(res["files"].items()):
        p = os.path.join(root, rel)
        if not os.path.exists(p):
            fails.append("缺失: " + rel)
            continue
        with open(p, encoding="utf-8", newline="") as f:
            if f.read() != text:
                fails.append("内容不一致: " + rel)

    # 2) guid 唯一性 + meta 与派生值一致
    seen = {}
    for rel, text in res["files"].items():
        if not rel.endswith(".asset.meta"):
            continue
        name = os.path.basename(rel)[:-len(".asset.meta")]
        g = re.search(r"^guid: ([0-9a-f]{32})$", text, re.M).group(1)
        if g in seen:
            fails.append("guid 重复: %s 与 %s" % (name, seen[g]))
        seen[g] = name
        if g != gen.guid_of(spec.get("guidSalt", spec["name"]), name):
            fails.append("guid 与派生值不符（改了 guidSalt 或资产名？）: " + name)

    # 3) prefab 引用集合 == spec 的资产集合
    with open(os.path.join(root, spec["prefab"]), encoding="utf-8") as f:
        prefab = f.read()
    upg = re.search(r"(?ms)^  Upgrade:\n(.*?)^  Modules:", prefab)
    mods = re.search(r"(?ms)^  Modules:\n(.*?)^  ActiveModule:", prefab)
    if not upg or not mods:
        fails.append("prefab: 找不到 Upgrade / Modules 块")
    else:
        refs = set(re.findall(r"guid: ([0-9a-f]{32})", upg.group(1) + mods.group(1)))
        want = set(seen)
        want.add(spec.get("noModule", gen.WMD_NONE_GUID))
        if refs - want - {gen.WMD_NONE_GUID}:
            fails.append("prefab 引用了 spec 之外的 guid: %s" % sorted(refs - want - {gen.WMD_NONE_GUID}))
        if want - refs:
            fails.append("spec 里的资产没被 prefab 引用: %s" % sorted(want - refs))

    # 4) showAttr 与 cfg 基础值回读
    info = res["info"]
    m = re.search(r"(?m)^  showAttr: ([0-9a-fA-F]*)$", prefab)
    if not m:
        fails.append("prefab: 找不到 showAttr")
    else:
        h = m.group(1)
        vals = [int.from_bytes(bytes.fromhex(h[i:i + 8]), "little") for i in range(0, len(h), 8)]
        want_vals = [info.num(n) for n in spec["showAttr"]]
        if vals != want_vals:
            fails.append("prefab showAttr=%s，期望 %s" % (vals, want_vals))
    cfg = gen.attr_whitelist.parse_prefab_cfg(os.path.join(root, spec["prefab"]))
    for c in spec.get("cfgAdd", []):
        n = info.num(c["attr"])
        if n not in cfg:
            fails.append("prefab cfg 缺少 %s(%d)" % (c["attr"], n))
        elif abs(cfg[n] - float(c["value"])) > 1e-6:
            fails.append("prefab cfg %s=%s，期望 %s" % (c["attr"], cfg[n], c["value"]))

    for w in res["warnings"]:
        print("  [WARN]", w)
    for f_ in fails:
        print("  [FAIL]", f_)
    print("资产 %d 个 / tiers=%s / modules=%d"
          % (len(seen), [len(t["items"]) for t in spec["tiers"]], len(spec["modules"])))
    print("RESULT:", "OK" if not fails else "FAILED")
    return 0 if not fails else 1


if __name__ == "__main__":
    sys.exit(main())
