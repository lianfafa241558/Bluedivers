# -*- coding: utf-8 -*-
"""4036 收编 4053：NetRoomFlow 侧 5 处改动（注册/反注册/打包/解码分流/删旧 handler）。"""
import io

P = r"e:/Bluedivers/Assets/Scripts/07NetGame/NetRoomFlow.cs"

with io.open(P, "r", encoding="utf-8-sig", newline="") as f:
    text = f.read()

crlf = "\r\n" in text
if crlf:
    text = text.replace("\r\n", "\n")

pairs = []

# 1) 注册：不再单独注册 VehicleMountMsg
pairs.append((
    "            // 载具上下机（双向一条；\u26a0 一个号只能一个注册者 \u21d2 注册在这里，桥只订阅 OnVehicleMount 事件）\n"
    "            MessageCenter.Register<VehicleMountMsg>(CmdId.VehicleMountNtf, HandleVehicleMountNtf);\n",
    "            // \u26a0 载具上下机已并入 4036（见 HandleFurnitureOperateNtf 的 NetId 分支）\u21d2 这里不再单独注册\n"
))

# 2) 反注册
pairs.append((
    "            MessageCenter.Unregister(CmdId.KeyScreenInputNtf);\n"
    "            MessageCenter.Unregister(CmdId.VehicleMountNtf);\n",
    "            MessageCenter.Unregister(CmdId.KeyScreenInputNtf);\n"
))

# 3) 上行打包：改用 4036 的 FurnitureOperateMsg
pairs.append((
    "            var msg = MessageCenter.Pack(CmdId.VehicleMountNtf,\n"
    "                new VehicleMountMsg { NetId = netId, Sid = sid, Mounted = mounted });",
    "            // 已并入 4036：NetId != 0 即\"这条是上下机\"，键 = 载具 Actor.NetId\n"
    "            // （载具两端生成落点差几厘米 \u21d2 SyncId 必然对不上，只能用车号）\n"
    "            var msg = MessageCenter.Pack(CmdId.FurnitureOperateNtf,\n"
    "                new FurnitureOperateMsg { NetId = netId, VehicleSid = sid, Mounted = mounted });"
))

# 4) 下行解码：NetId 分支分流给 OnVehicleMount
pairs.append((
    "        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)\n"
    "        {\n"
    "            if (m != null) OnFurnitureOperate?.Invoke(m);\n"
    "        }",
    "        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)\n"
    "        {\n"
    "            if (m == null) return;\n"
    "\n"
    "            // \u26a0 载具上下机（原 4053）也走这个号：NetId != 0 \u21d2 键是载具 Actor.NetId，SyncId/State 不参与。\n"
    "            //   必须在这里就分流给 OnVehicleMount（转发与否由桥按**一车一人**裁决），绝不能流进家具重放。\n"
    "            if (m.NetId != 0)\n"
    "            {\n"
    "                OnVehicleMount?.Invoke(m.NetId, m.VehicleSid, m.Mounted);\n"
    "                return;\n"
    "            }\n"
    "\n"
    "            OnFurnitureOperate?.Invoke(m);\n"
    "        }"
))

# 5) 删旧 handler
pairs.append((
    "        private void HandleVehicleMountNtf(VehicleMountMsg m)\n"
    "        {\n"
    "            if (m != null && m.NetId != 0) OnVehicleMount?.Invoke(m.NetId, m.Sid, m.Mounted);\n"
    "        }\n\n",
    ""
))

# 6) 事件注释口径
pairs.append((
    "        /// <summary>载具上下机（双向；**一个号两个方向**，走法同 <c>FurnitureOperateNtf</c>(4036)）。",
    "        /// <summary>载具上下机（双向；**已并入 <c>FurnitureOperateNtf</c>(4036) 的 NetId 分支**，一个号两个方向）。"
))

for i, (old, new) in enumerate(pairs, 1):
    n = text.count(old)
    assert n == 1, "pair %d matched %d times" % (i, n)
    text = text.replace(old, new)

if crlf:
    text = text.replace("\n", "\r\n")

with io.open(P, "w", encoding="utf-8-sig", newline="") as f:
    f.write(text)
print("ok crlf=%s" % crlf)
