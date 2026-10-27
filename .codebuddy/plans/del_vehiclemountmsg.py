# -*- coding: utf-8 -*-
"""删掉 SyncMsg.cs 里已并入 4036 的 VehicleMountMsg（229~246 行），留一行指针。"""
import io

P = r"e:/Bluedivers/Assets/Scripts/07NetGame/Msg/SyncMsg.cs"

with io.open(P, "r", encoding="utf-8-sig", newline="") as f:
    text = f.read()

crlf = "\r\n" in text
nl = "\r\n" if crlf else "\n"
lines = text.split(nl)

# 1-based 229..246 -> 0-based 228..245
assert "public class VehicleMountMsg" in lines[237], lines[237]
assert lines[245].strip() == "}", lines[245]

new_line = ("    // \u26a0 \u539f 4053 VehicleMountMsg \u5df2\u4e8e 2026-10-27 \u5e76\u5165 4036\uff1a"
            "\u89c1 FurnitureOperateMsg \u7684 NetId/VehicleSid/Mounted\uff08\u952e = \u8f7d\u5177 Actor.NetId\uff09\u3002")
lines[228:246] = [new_line]

with io.open(P, "w", encoding="utf-8-sig", newline="") as f:
    f.write(nl.join(lines))
print("ok crlf=%s" % crlf)
