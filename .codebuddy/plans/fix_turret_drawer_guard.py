# -*- coding: utf-8 -*-
"""字节级给 TurretDrawer.cs 首尾加 #if UNITY_EDITOR 守卫（保 BOM、保原换行）。"""
p = r"d:\Pro\Bluedivers\Assets\Scripts\06Gameplay\02Game\AI\StateMachine\Editor\TurretDrawer.cs"
raw = open(p, "rb").read()
assert raw[:3] == b"\xef\xbb\xbf", "无 BOM"
body = raw[3:].decode("utf-8")
nl = "\r\n" if "\r\n" in body else "\n"
assert "#if UNITY_EDITOR" not in body.split("\n")[0], "已加过守卫"
head = ("// asmdef 下的 Editor 文件夹不再拥有 editor-only 特权（Unity 手册），\n"
        "// 本文件必须自行用 #if UNITY_EDITOR 包裹，否则非 Editor 平台构建报 CS0246。\n"
        "#if UNITY_EDITOR\n")
body = head.replace("\n", nl) + body
if not body.endswith(nl):
    body += nl
body += "#endif" + nl
open(p, "wb").write(b"\xef\xbb\xbf" + body.encode("utf-8"))
print("done, new len =", len(open(p, "rb").read()))
