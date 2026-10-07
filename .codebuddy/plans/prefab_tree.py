"""只读：打印一个 prefab 的节点树，并把 MonoBehaviour 的脚本/序列化引用解析成「节点名」。

用法：
  python -X utf8 .codebuddy/plans/prefab_tree.py <prefab 路径> [子树根节点名] [--depth N]
例：
  python -X utf8 .codebuddy/plans/prefab_tree.py Assets/Resources/UI/Wnd/TipWnd.prefab
  python -X utf8 .codebuddy/plans/prefab_tree.py Assets/Resources/UI/Wnd/SettingWnd.prefab stateWnd

设计：Unity 的 prefab YAML 里 `m_Children` 存的是**子 RectTransform/Transform 的 fileID**（不是 GameObject），
所以必须 RT→m_GameObject→GO.name 两级查表。
"""
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = r"d:/Pro/Bluedivers"

HEAD = re.compile(r"^--- !u!(\d+) &(\d+)")
FIELD = re.compile(r"^  ([A-Za-z_][\w]*):\s*(.*)$")
LIST_ITEM = re.compile(r"^  - \{fileID: (\d+)\}")


def load_guids():
    """guid -> 脚本相对路径（扫 Assets 下 *.cs.meta，一次；约 1 秒）。"""
    m = {}
    for base, _dirs, files in os.walk(os.path.join(ROOT, "Assets")):
        if "Library" in base:
            continue
        for f in files:
            if f.endswith(".cs.meta"):
                p = os.path.join(base, f)
                try:
                    with open(p, "r", encoding="utf-8", errors="replace") as fh:
                        for line in fh:
                            if line.startswith("guid:"):
                                m[line.split(":", 1)[1].strip()] = os.path.relpath(
                                    p[:-5], ROOT).replace("\\", "/")
                                break
                except OSError:
                    pass
    return m


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return
    path = sys.argv[1]
    if not os.path.isabs(path):
        path = os.path.join(ROOT, path)
    root_name = None
    depth = 99
    args = sys.argv[2:]
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--depth":
            depth = int(args[i + 1])
            i += 2
            continue
        if not a.startswith("--"):
            root_name = a
        i += 1

    with open(path, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()

    blocks = []
    cur = None
    for ln in lines:
        hm = HEAD.match(ln)
        if hm:
            cur = {"kind": hm.group(1), "id": hm.group(2), "fields": {}, "children": []}
            blocks.append(cur)
            continue
        if cur is None:
            continue
        if ln.startswith("  m_Children:"):
            cur["children"] = []
            continue
        lm = LIST_ITEM.match(ln)
        if lm:
            cur["children"].append(lm.group(1))
            continue
        fm = FIELD.match(ln)
        if fm:
            cur["fields"][fm.group(1)] = fm.group(2).strip()

    by_id = {b["id"]: b for b in blocks}
    rt_children = {b["id"]: b["children"] for b in blocks if b["kind"] == "224"}  # RectTransform
    rt_go = {b["id"]: b["fields"].get("m_GameObject", "") for b in blocks if b["kind"] == "224"}
    go_rt = {}
    for rt_id, go in rt_go.items():
        mm = re.search(r"fileID: (\d+)", go)
        if mm:
            go_rt[mm.group(1)] = rt_id

    def go_name(go_id):
        b = by_id.get(go_id)
        return b["fields"].get("m_Name", "?") if b else "?"

    def node_label(ref_id):
        """fileID → 描述（GO / 组件）。"""
        b = by_id.get(ref_id)
        if b is None:
            return f"<外部 {ref_id}>"
        if b["kind"] == "1":
            return go_name(ref_id)
        if b["kind"] == "224":
            mm = re.search(r"fileID: (\d+)", b["fields"].get("m_GameObject", ""))
            return go_name(mm.group(1)) if mm else "?"
        mm = re.search(r"fileID: (\d+)", b["fields"].get("m_GameObject", ""))
        return ("组件@" + go_name(mm.group(1))) if mm else "组件"

    guids = load_guids()

    # 找子树根
    root_rt = None
    if root_name:
        for go_id, rt_id in go_rt.items():
            if go_name(go_id) == root_name:
                root_rt = rt_id
                break
        if root_rt is None:
            print("未找到节点:", root_name)
            return
    else:
        # 无根：取 m_Father 为 0/缺失的 RT
        for b in blocks:
            if b["kind"] != "224":
                continue
            fa = b["fields"].get("m_Father", "")
            fm = re.search(r"fileID: (\d+)", fa)
            if fm is None or fm.group(1) == "0":
                root_rt = b["id"]
                break

    print(f"=== {os.path.relpath(path, ROOT)}  根={root_name or '*(scene root)*'} ===")

    def dump(rt_id, indent, d):
        if d > depth:
            return
        go_id = re.search(r"fileID: (\d+)", rt_go.get(rt_id, "") or "")
        go_id = go_id.group(1) if go_id else None
        b = by_id.get(go_id) if go_id else None
        name = go_name(go_id) if go_id else "?"
        active = b["fields"].get("m_IsActive", "1") if b else "1"
        comps = [c for c in (b["fields"].get("m_Component", ""),)] if False else None
        print("  " * indent + f"- {name}" + ("  [inactive]" if active == "0" else ""))
        for c in rt_children.get(rt_id, []):
            dump(c, indent + 1, d + 1)

    # 打印根子树
    if root_rt:
        # 根自身
        go_id = re.search(r"fileID: (\d+)", rt_go.get(root_rt, "") or "")
        go_id = go_id.group(1) if go_id else None
        print(f"- {go_name(go_id) if go_id else '?'}   (根 rt={root_rt})")
        for c in rt_children.get(root_rt, []):
            dump(c, 1, 1)

    # 打印 MonoBehaviour（脚本名 + 指向节点的字段）
    print("\n--- MonoBehaviour 引用 ---")
    for b in blocks:
        if b["kind"] != "114":
            continue
        script = b["fields"].get("m_Script", "")
        mm = re.search(r"guid: (\w+)", script)
        spath = guids.get(mm.group(1)) if mm else None
        sname = os.path.basename(spath) if spath else (mm.group(1) if mm else "?")
        refs = []
        for k, v in b["fields"].items():
            if k in ("m_GameObject", "m_Script", "m_Name", "m_EditorClassIdentifier",
                     "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
                     "m_PrefabAsset", "m_Enabled", "m_EditorHideFlags"):
                continue
            rm = re.search(r"fileID: (\d+)", v)
            if rm and rm.group(1) != "0":
                refs.append(f"{k}={node_label(rm.group(1))}")
        loc = re.search(r"fileID: (\d+)", b["fields"].get("m_GameObject", ""))
        host = go_name(loc.group(1)) if loc else "?"
        print(f"  [{sname}] @ {host}")
        for r in refs:
            print(f"      {r}")


if __name__ == "__main__":
    main()
