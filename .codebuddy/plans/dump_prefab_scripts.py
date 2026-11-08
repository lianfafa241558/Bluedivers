"""列出 prefab 里每个 MonoBehaviour 的脚本文��路径 + 类名，并对可疑项做静态检查。

用法: python dump_prefab_scripts.py <prefab 路径>
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ASSETS = os.path.join(ROOT, "Assets")
guid_re = re.compile(r"^guid: ([0-9a-f]{32})$", re.M)
script_re = re.compile(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32}), type: \d+\}")
name_re = re.compile(r"m_Name: (.*)")
go_re = re.compile(r"m_GameObject: \{fileID: (\d+)\}")


def collect_guids():
    guids = {}
    for root in (ASSETS, os.path.join(ROOT, "Packages"), os.path.join(ROOT, "Library", "PackageCache")):
        if not os.path.isdir(root):
            continue
        for dirpath, _, filenames in os.walk(root):
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


def main():
    prefab = os.path.abspath(sys.argv[1])
    guids = collect_guids()
    with open(prefab, "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()

    # GameObject fileID -> name
    go_names = {}
    for m in re.finditer(r"--- !u!1 &(\d+)\nGameObject:\n(.*?)(?=\n--- !u!)", text, re.S):
        nm = name_re.search(m.group(2))
        go_names[m.group(1)] = nm.group(1) if nm else "?"

    rows = []
    for m in script_re.finditer(text):
        g = m.group(1)
        start = text.rfind("--- !u!", 0, m.start())
        block = text[start:m.start()]
        go = go_re.search(block)
        owner = go_names.get(go.group(1), "?") if go else "?"
        path = guids.get(g, "<缺失>")
        rows.append((owner, os.path.relpath(path, ROOT) if path != "<缺失>" else path, g))

    print(f"{len(rows)} 个 MonoBehaviour：")
    for owner, path, g in sorted(rows):
        print(f"  {owner:<28} {path}")

    # 静态检查：脚本文件里是否有 #if UNITY_EDITOR 包裹整个类 / 类名与文件名不一致
    print("\n可疑项检查：")
    for owner, path, g in sorted(set(rows)):
        if path == "<缺失>" or not path.endswith(".cs"):
            print(f"  [!] {owner}: {path} ({g})")
            continue
        full = os.path.join(ROOT, path)
        try:
            with open(full, "r", encoding="utf-8-sig", errors="ignore") as f:
                src = f.read()
        except OSError:
            print(f"  [!] 读不到 {path}")
            continue
        cls = os.path.splitext(os.path.basename(path))[0]
        if not re.search(r"\b(class|struct|enum|interface)\s+" + re.escape(cls) + r"\b", src):
            print(f"  [!] {owner}: 文件名与主类名不一致 {path}")
        if re.search(r"#if\s+UNITY_EDITOR", src) and "Assembly-CSharp-Editor" not in path:
            print(f"  [?] {owner}: 含 #if UNITY_EDITOR → {path}")


if __name__ == "__main__":
    main()
