# -*- coding: utf-8 -*-
"""4036 收编 4053：两个桥的注释口径（行范围替换，避免中文引号匹配问题）。"""
import io

def load(p):
    with io.open(p, "r", encoding="utf-8-sig", newline="") as f:
        t = f.read()
    crlf = "\r\n" in t
    if crlf:
        t = t.replace("\r\n", "\n")
    return t, crlf

def save(p, t, crlf):
    if crlf:
        t = t.replace("\n", "\r\n")
    with io.open(p, "w", encoding="utf-8-sig", newline="") as f:
        f.write(t)

def repl(p, start, end, new_block, checks):
    """start/end 是 1-based 闭区间；checks = [(行号, 期望子串)]"""
    t, crlf = load(p)
    lines = t.split("\n")
    for ln, sub in checks:
        assert sub in lines[ln - 1], "%s:%d 期望含 %r，实际 %r" % (p, ln, sub, lines[ln - 1])
    lines[start - 1:end] = new_block.split("\n")
    save(p, "\n".join(lines), crlf)
    print("ok %s  %d~%d" % (p, start, end))

# ---------- NetFurnitureBridge：早退注释（87~90） ----------
repl(r"e:/Bluedivers/Assets/Scripts/09Manager/Battle/NetFurnitureBridge.cs", 87, 90,
     "            // \u2605 载具（Furniture_Vehicle）：走 4036 的 **NetId 分支**（NetRoomFlow 解码时已分流，远端应用仍归\n"
     "            //   NetVehicleBridge.ApplyRemoteMount，**不重放** Operate()）。这里的家具事件必须继续摘掉：\n"
     "            //   载具的触发点是 VehicleOwnerChanged（含被打爆 / 驾驶者死亡），换到这里会漏 + 一次上车双报。\n"
     "            if (furn is Furniture_Vehicle) return;",
     [(87, "Furniture_Vehicle"), (90, "if (furn is Furniture_Vehicle) return;")])

# ---------- NetVehicleBridge：类注释（11）+ 不能蹭 4036 那段（16~19） ----------
repl(r"e:/Bluedivers/Assets/Scripts/09Manager/Battle/NetVehicleBridge.cs", 11, 11,
     "    /// \u8054\u673a \u00b7 **\u8f7d\u5177**\u6865\uff0809\uff09\uff1a\u4e0a\u4e0b\u673a\uff08\u539f 4053\uff0c2026-10-27 \u5e76\u5165 **4036** \u7684 NetId \u5206\u652f\uff09"
     "\u2014\u2014 \u5168\u5de5\u7a0b\u552f\u4e00\u540c\u65f6\u770b\u5f97\u89c1\u8f7d\u5177/\u76df\u53cb\u5b9e\u4f8b\u4e0e\u7f51\u7edc\u5c42\u7684\u5730\u65b9\u3002",
     [(11, "4053")])

repl(r"e:/Bluedivers/Assets/Scripts/09Manager/Battle/NetVehicleBridge.cs", 16, 19,
     "    /// <para>\u258d \u26a0 \u8fdc\u7aef**\u4e0d\u80fd**\u8d70\u5bb6\u5177\u91cd\u653e\uff1a4036 \u7684 <c>ApplyRemoteOperate</c> \u4f1a\u8c03 <c>Furniture_Vehicle.Operate()</c>\n"
     "    /// \u21d2 <c>VehicleController.SetOwener(\u8fdc\u7aef\u5b9e\u4f8b)</c>\uff0c\u800c\u5176\u4e2d\u4e00\u6279\u5f00\u5173\uff08<c>ActorState.Hide</c> / \u76f8\u673a / \u672c\u673a\u8f93\u5165\uff09\n"
     "    /// \u672c\u8d28\u662f\"\u672c\u673a\u73a9\u5bb6\u4e13\u5c5e\"\u3002\u6545 4036 \u53ea\u5f53**\u4f20\u8f93**\uff08NetId \u5206\u652f\uff09\uff0c\u5e94\u7528\u4e00\u5f8b\u8d70\n"
     "    /// <see cref=\"Furniture_Vehicle.ApplyRemoteMount\"/>\uff08\u53ea\u505a\"\u6302\u5230\u6302\u70b9 + \u505c\u4f4d\u59ff/\u505c\u52a8\u753b\"\uff09\u3002</para>",
     [(16, "\u4e0d\u80fd\u8e6d\u5bb6\u5177"), (19, "ApplyRemoteMount")])
