# -*- coding: utf-8 -*-
"""把 CmdId.VehicleMountNtf(4053) 的注释改成"已废弃并入 4036"。
只改注释块（176~186 行），const 名字与值不动（号不许复用）。
"""
import io, os

P = r"e:/Bluedivers/Assets/Scripts/07NetGame/CmdId.cs"

new_block = '''        /// <summary>\u26a0 **已废弃（2026-10-27 并入 <see cref="FurnitureOperateNtf"/>(4036)）**：载具上下机现在走
        /// 4036 的 <c>FurnitureOperateMsg.NetId != 0</c> 分支（键仍是载具 <c>Actor.NetId</c>，驾驶者 sid 看
        /// <c>VehicleSid</c>、是否上机看 <c>Mounted</c>）；远端应用仍旧走桥的 <c>ApplyRemoteMount</c>，
        /// **不重放** <c>Furniture_Vehicle.Operate()</c>。常量保留只为**不复用这个号**：本端不注册也不外发。
        /// <para>\u25b7 一车一人的占用裁决仍在 <c>NetVehicleBridge._drivers</c>（房主唯一裁决、先到先得）。</para></summary>
        public const int VehicleMountNtf = 4053;
'''

with io.open(P, "r", encoding="utf-8-sig", newline="") as f:
    text = f.read()

crlf = "\r\n" in text
lines = text.split("\r\n") if crlf else text.split("\n")

# 1-based 176~186 -> 0-based 175..185
assert "VehicleMountNtf = 4053" in lines[185], lines[185]
assert "\u8f7d\u5177\u4e0a\u4e0b\u673a" in lines[175], lines[175]   # 载具上下机

nl = "\r\n" if crlf else "\n"
block = new_block.replace("\n", nl)
lines[175:186] = block.split(nl)[:-1]

out = nl.join(lines)
with io.open(P, "w", encoding="utf-8-sig", newline="") as f:
    f.write(out)
print("ok, crlf=%s" % crlf)
