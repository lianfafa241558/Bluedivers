# -*- coding: utf-8 -*-
"""
联机 P3：标记点位(4037) / 呼叫凯伊(4038) 的收发接线。
- NetRoomFlow(07)：事件 + 收发 + 注册/退订
- WaveManager：Install/Uninstall NetActionBridge

（P3 第三项"PlayMeetSpeech 收口"经核查**已天然具备**：PlayerSpeechManager.OnMeetSpeech → Speech() → BattleEventBus.PlayerSpeech → 已有桥，无需改动。）

每个 (old,new) 必须在该文件里恰好命中 1 次，否则整脚本中止、不落盘。行尾自适应。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RF = os.path.join(ROOT, "Assets", "Scripts", "07NetGame", "NetRoomFlow.cs")
WM = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "WaveManager.cs")

EDITS = []


def E(path, old, new):
    EDITS.append((path, old, new))


# ---------- NetRoomFlow：事件 ----------
E(RF,
  "        /// <summary>【双向】家具交互（<c>SyncId</c> = 家具跨端稳定键）⇒ 远端重放同一交互。</summary>\n"
  "        public static event Action<FurnitureOperateMsg> OnFurnitureOperate;\n",

  "        /// <summary>【双向】家具交互（<c>SyncId</c> = 家具跨端稳定键）⇒ 远端重放同一交互。</summary>\n"
  "        public static event Action<FurnitureOperateMsg> OnFurnitureOperate;\n"
  "\n"
  "        /// <summary>【双向】标记点位（表现类；目标实体是引用不能过网 ⇒ 只传点）⇒ 远端在自己的字幕/光环上复现。</summary>\n"
  "        public static event Action<MarkMsg> OnMark;\n"
  "\n"
  "        /// <summary>【双向】呼叫凯伊（点）⇒ 远端让本机凯伊走到同一点（放信标 / 就近救人）。</summary>\n"
  "        public static event Action<CallKaiMsg> OnCallKai;\n")

# ---------- NetRoomFlow：注册 ----------
E(RF,
  "            MessageCenter.Register<FurnitureOperateMsg>(CmdId.FurnitureOperateNtf, HandleFurnitureOperateNtf);\n"
  "        }\n",

  "            MessageCenter.Register<FurnitureOperateMsg>(CmdId.FurnitureOperateNtf, HandleFurnitureOperateNtf);\n"
  "            MessageCenter.Register<MarkMsg>(CmdId.MarkNtf, HandleMarkNtf);\n"
  "            MessageCenter.Register<CallKaiMsg>(CmdId.CallKaiNtf, HandleCallKaiNtf);\n"
  "        }\n")

# ---------- NetRoomFlow：退订 ----------
E(RF,
  "            MessageCenter.Unregister(CmdId.FurnitureOperateNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n",

  "            MessageCenter.Unregister(CmdId.FurnitureOperateNtf);\n"
  "            MessageCenter.Unregister(CmdId.MarkNtf);\n"
  "            MessageCenter.Unregister(CmdId.CallKaiNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n")

# ---------- NetRoomFlow：收发 ----------
E(RF,
  "        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)\n"
  "        {\n"
  "            if (m != null) OnFurnitureOperate?.Invoke(m);\n"
  "        }\n",

  "        private void HandleFurnitureOperateNtf(FurnitureOperateMsg m)\n"
  "        {\n"
  "            if (m != null) OnFurnitureOperate?.Invoke(m);\n"
  "        }\n"
  "\n"
  "        /// <summary>标记点位：成员上报房主 / 房主转发全体（走向由 09 侧的 NetActionBridge 决定）。</summary>\n"
  "        public void SendMark(uint sid, float x, float y, float z)\n"
  "        {\n"
  "            var msg = MessageCenter.Pack(CmdId.MarkNtf,\n"
  "                new MarkMsg { Sid = sid, Kind = 0, X = x, Y = y, Z = z });\n"
  "\n"
  "            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);\n"
  "            else NetSvc.Instance?.SendMsg(msg);\n"
  "        }\n"
  "\n"
  "        /// <summary>呼叫凯伊：成员上报房主 / 房主转发全体。</summary>\n"
  "        public void SendCallKai(uint sid, float x, float y, float z)\n"
  "        {\n"
  "            var msg = MessageCenter.Pack(CmdId.CallKaiNtf,\n"
  "                new CallKaiMsg { Sid = sid, X = x, Y = y, Z = z });\n"
  "\n"
  "            if (IsHost) NetHostSvc.Instance?.SendToAll(msg);\n"
  "            else NetSvc.Instance?.SendMsg(msg);\n"
  "        }\n"
  "\n"
  "        private void HandleMarkNtf(MarkMsg m)\n"
  "        {\n"
  "            if (m != null) OnMark?.Invoke(m);\n"
  "        }\n"
  "\n"
  "        private void HandleCallKaiNtf(CallKaiMsg m)\n"
  "        {\n"
  "            if (m != null) OnCallKai?.Invoke(m);\n"
  "        }\n")

# ---------- WaveManager：Install ----------
E(WM,
  "            NetFurnitureBridge.Install();     // 家具交互（共享世界物件，一处收口）\n"
  "        }\n",

  "            NetFurnitureBridge.Install();     // 家具交互（共享世界物件，一处收口）\n"
  "            NetActionBridge.Install();        // 标记点位 / 呼叫凯伊\n"
  "        }\n")

# ---------- WaveManager：Uninstall ----------
E(WM,
  "            NetFurnitureBridge.Uninstall();\n"
  "        }\n",

  "            NetFurnitureBridge.Uninstall();\n"
  "            NetActionBridge.Uninstall();\n"
  "        }\n")


def _conv(text, crlf):
    return text.replace("\n", "\r\n") if crlf else text


def main():
    problems = []
    plans = []

    for i, (path, old, new) in enumerate(EDITS):
        if not os.path.isfile(path):
            problems.append("EDIT#%d NOT FOUND: %s" % (i + 1, path))
            continue
        raw = open(path, "r", encoding="utf-8", newline="").read()
        crlf = "\r\n" in raw
        o, n = _conv(old, crlf), _conv(new, crlf)
        hit = raw.count(o)
        plans.append((path, o, n))
        if hit != 1:
            problems.append("EDIT#%d HIT=%d (expect 1) %s%s :: %s"
                            % (i + 1, hit, os.path.basename(path), " [CRLF]" if crlf else "",
                               old.strip().splitlines()[0][:60]))

    if problems:
        print("=== ABORT，未落盘 ===")
        for p in problems:
            print("  " + p)
        return 1

    for i, (path, o, n) in enumerate(plans):
        text = open(path, "r", encoding="utf-8", newline="").read()
        open(path, "w", encoding="utf-8", newline="").write(text.replace(o, n, 1))
        print("OK  EDIT#%d  %s" % (i + 1, os.path.relpath(path, ROOT)))

    print("\n全部 %d 处改写完成" % len(plans))
    return 0


if __name__ == "__main__":
    sys.exit(main())
