# -*- coding: utf-8 -*-
"""给 PlayerWnd.prefab 的 CrosshairManager 组件挂上 m_CenterPoint（Center 物体），保留 LF。"""
p = r"e:\Bluedivers\Assets\Resources\UI\Wnd\PlayerWnd.prefab"

with open(p, "r", encoding="utf-8", newline="") as f:
    s = f.read()

anchor = (
    "  m_RicochetGo: {fileID: 1537952602055708470}\n"
    "  m_WeaponsManager: {fileID: 0}\n"
    "  isStartHave: 0\n"
)
assert s.count(anchor) == 1, "锚点数量异常: %d" % s.count(anchor)
assert "m_CenterPoint" not in s, "已存在 m_CenterPoint 序列化"

s = s.replace(anchor, anchor + "  m_CenterPoint: {fileID: 926829075494487567}\n")

with open(p, "w", encoding="utf-8", newline="") as f:
    f.write(s)

print("ok")
