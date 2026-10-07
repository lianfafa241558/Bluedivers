"""只读：打印 SelectMapWnd.prefab 里 Rooms 子树的层级 + 关键引用。

用途：为「房间列表」面板新增节点（加入按钮 / 状态文本 / 密码输入）前，
确认现有节点名与挂靠位置，避免凭记忆写错路径。

跑法：python .codebuddy/plans/rooms_hierarchy.py
"""
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

PREFAB = r"d:/Pro/Bluedivers/Assets/Resources/UI/Wnd/SelectMapWnd.prefab"

# 每行形如 "--- !u!1 &123" / "--- !u!224 &456"
HEAD = re.compile(r"^--- !u!(\d+) &(\d+)")
# 关键字段（缩进不敏感）
KEY = re.compile(r"^  (m_Name|m_GameObject|m_Father|m_Children|m_IsActive|m_Script|m_Text|m_FontSize):\s*(.*)$")


def block_kind(t):
    return {"1": "GO", "224": "RT", "114": "Mono", "222": "CanvasRenderer"}.get(t, "?")


def main():
    with open(PREFAB, "r", encoding="utf-8", errors="replace") as f:
        lines = f.readlines()

    blocks = []  # (kind, fileid, {field: value or list})
    cur = None
    for ln in lines:
        m = HEAD.match(ln)
        if m:
            cur = {"kind": block_kind(m.group(1)), "id": m.group(2),
                   "name": None, "go": None, "father": None, "children": [],
                   "active": None, "script": None, "text": None}
            blocks.append(cur)
            continue
        if cur is None:
            continue
        m = KEY.match(ln)
        if not m:
            continue
        k, v = m.group(1), m.group(2).strip()
        if k == "m_Children":
            cur["children"] = []
        elif k in ("m_Name",):
            cur["name"] = v
        elif k == "m_IsActive":
            cur["active"] = v
        elif k == "m_GameObject":
            fm = re.search(r"fileID: (\d+)", v)
            cur["go"] = fm.group(1) if fm else None
        elif k == "m_Father":
            fm = re.search(r"fileID: (\d+)", v)
            cur["father"] = fm.group(1) if fm else None
        elif k == "m_Script":
            fm = re.search(r"guid: (\w+)", v)
            cur["script"] = fm.group(1) if fm else None
        elif k in ("m_Text",):
            cur["text"] = v
        elif k == "m_FontSize":
            pass
        # m_Children 列表项形如 "  - {fileID: 123}"
        if k == "m_Children":
            pass
        # 单独处理 children 列表行
    # 二遍：收集 children（形如 "  - {fileID: 123}" 紧跟 m_Children 之后）
    cur = None
    in_children = False
    for ln in lines:
        m = HEAD.match(ln)
        if m:
            cur = {"id": m.group(2)}
            in_children = False
            continue
        if cur is None:
            continue
        if ln.startswith("  m_Children:"):
            in_children = True
            continue
        if in_children:
            cm = re.match(r"^  - \{fileID: (\d+)\}", ln)
            if cm:
                for b in blocks:
                    if b["id"] == cur["id"]:
                        b["children"].append(cm.group(1))
                        break
            else:
                in_children = False

    by_id = {b["id"]: b for b in blocks}
    rt_by_go = {b["go"]: b for b in blocks if b["kind"] == "RT"}
    go_by_rt = {b["id"]: b for b in blocks if b["kind"] == "RT"}

    # 找 Rooms 的 GO
    rooms_go = None
    for b in blocks:
        if b["kind"] == "GO" and b["name"] == "Rooms":
            rooms_go = b
            break
    if rooms_go is None:
        print("未找到 Rooms")
        return

    def name_of_go(gid):
        go = by_id.get(gid)
        return go["name"] if go else "?"

    def dump(rt_id, depth):
        """rt_id = RectTransform 的 fileID（m_Children 里存的就是它）。"""
        rt = by_id.get(rt_id)
        if rt is None:
            print("  " * depth + f"- <未解析 {rt_id}>")
            return
        go = by_id.get(rt["go"])
        name = go["name"] if go else "?"
        extra = " [inactive]" if (go and go["active"] == "0") else ""
        print("  " * depth + f"- {name}{extra}  (rt={rt_id}, go={rt['go']})")
        for c in rt["children"]:
            dump(c, depth + 1)

    rooms_rt = rt_by_go.get(rooms_go["id"])
    kids = rooms_rt["children"] if rooms_rt else []
    print(f"[Rooms 子树] 直接子物体 {len(kids)} 个")
    for c in kids:
        dump(c, 0)

    # 打印 ServerListPanel 的序列化引用
    print("\n[ServerListPanel 序列化引用]")
    for b in blocks:
        if b["kind"] != "Mono":
            continue
        if b["script"] == "469eb650628793948a41d8c2966cc06a":
            # 从原始行里取该 block 原文
            start = None
            for i, ln in enumerate(lines):
                m = HEAD.match(ln)
                if m and m.group(2) == b["id"]:
                    start = i
                    break
            for ln in lines[start:start + 25]:
                print("  " + ln.rstrip())


if __name__ == "__main__":
    main()
