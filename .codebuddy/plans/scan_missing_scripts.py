"""扫描 prefab/scene 里 MonoBehaviour 的 m_Script guid，找出项目里不存在的 guid（= Missing Script）。

用法: python scan_missing_scripts.py <目标文件或目录> [更多...]
默认: Assets/Resources/Prefabs/Manager/GameRoot.prefab + Assets/Scene
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")

guid_re = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
script_re = re.compile(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32}), type: \d+\}")


def collect_meta_guids():
    guids = {}
    roots = [ASSETS, os.path.join(ROOT, "Packages"), os.path.join(ROOT, "Library", "PackageCache")]
    for root in roots:
        if not os.path.isdir(root):
            continue
        for dirpath, dirnames, filenames in os.walk(root):
            for fn in filenames:
                if not fn.endswith(".meta"):
                    continue
                p = os.path.join(dirpath, fn)
                try:
                    with open(p, "r", encoding="utf-8", errors="ignore") as f:
                        m = guid_re.search(f.read(200))
                except OSError:
                    continue
                if m:
                    guids[m.group(1)] = p[: -len(".meta")]
    return guids


def scan(target, guids):
    hits = {}
    try:
        with open(target, "r", encoding="utf-8", errors="ignore") as f:
            text = f.read()
    except OSError as e:
        print("读不到", target, e)
        return hits
    # 记录每个 MonoBehaviour 块所属 GameObject 名
    for m in script_re.finditer(text):
        g = m.group(1)
        if g in guids:
            continue
        # 往前找最近的 "m_Name: "? 更可靠: 块里的 m_GameObject fileID，再回查
        start = text.rfind("--- !u!", 0, m.start())
        block = text[start:m.start()]
        hits.setdefault(g, []).append((target, block.splitlines()[0] if block else ""))
    return hits


def main():
    targets = sys.argv[1:]
    if not targets:
        targets = [os.path.join(ASSETS, "Resources", "Prefabs", "Manager", "GameRoot.prefab"), os.path.join(ASSETS, "Scene")]
    files = []
    for t in targets:
        t = os.path.abspath(t)
        if os.path.isdir(t):
            for dirpath, _, filenames in os.walk(t):
                for fn in filenames:
                    if fn.endswith((".prefab", ".unity")):
                        files.append(os.path.join(dirpath, fn))
        else:
            files.append(t)

    print(f"扫描 {len(files)} 个文件 ...")
    guids = collect_meta_guids()
    print(f"工程 meta guid 共 {len(guids)} 个")
    total = 0
    for f in files:
        hits = scan(f, guids)
        if not hits:
            continue
        total += sum(len(v) for v in hits.values())
        print(f"\n=== {os.path.relpath(f, ROOT)} ===")
        for g, where in hits.items():
            print(f"  缺失 guid {g}  出现 {len(where)} 次")
    print(f"\n合计缺失引用 {total} 处")


if __name__ == "__main__":
    main()
