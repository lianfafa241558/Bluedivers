# -*- coding: utf-8 -*-
"""
联机重构 · 阶段 1 源码改写（02_Net 断开会话→服务耦合 / NetSvc+NetHostSvc 改走 NetInbox / CmdId 分段清理）

安全策略：每个 (old, new) 必须在该文件里**恰好命中 1 次**，否则整脚本中止、不落盘。
文件均为 UTF-8 with BOM + LF（已探测）⇒ 读写用 utf-8，BOM 作为 \ufeff 原样保留。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
NET = os.path.join(ROOT, "Assets", "Scripts", "NetTmp")

EDITS = []


def E(rel, old, new):
    EDITS.append((os.path.join(NET, rel), old, new))


# ---------- ClientSession：不再回调游戏层的 NetSvc ----------
E("Client/ClientSession.cs",
  "        // 丢进 NetSvc 的消息队列，等主线程处理\n"
  "        NetSvc.Instance.AddMsgQue(msg);\n",
  "        // 丢进传输层队列（NetInbox），等主线程处理\n"
  "        NetInbox.Enqueue(msg);\n")

# ---------- HostSession：不再回调游戏层的 NetHostSvc ----------
E("Client/HostSession.cs",
  "        // 带上本会话的 sid 一起入队（成员端不用带，因为只有一条连接）\n"
  "        NetHostSvc.Instance.AddMsgQue(msg, GetSessionID());\n",
  "        // 带上本会话的 sid 一起入队（成员端不用带，因为只有一条连接）\n"
  "        NetInbox.Enqueue(msg, GetSessionID());\n")

# ---------- NetSvc：删自带队列/锁，改抽 NetInbox；心跳走 NetCmdId ----------
E("Client/NetSvc.cs",
  "    /// <summary>消息队列：网络线程收到的消息先进这里，主线程再取出处理（线程安全缓冲）</summary>\n"
  "    private Queue<NetMessage> msgPackQue;\n"
  "\n"
  "    /// <summary>是否已建立 KCP 连接（供业务层判断能否发送消息）</summary>\n"
  "    public bool IsConnected => client != null && client.clientSession != null && client.clientSession.IsConnected();\n"
  "\n"
  "    /// <summary>队列锁：保证多线程访问队列时安全（因为入队和出队在不同线程）</summary>\n"
  "    public static readonly string pkgque_lock = \"pkgque_lock\";\n",
  "    /// <summary>是否已建立 KCP 连接（供业务层判断能否发送消息）</summary>\n"
  "    public bool IsConnected => client != null && client.clientSession != null && client.clientSession.IsConnected();\n")

E("Client/NetSvc.cs",
  "        // 创建消息队列\n"
  "        msgPackQue = new Queue<NetMessage>();\n"
  "\n"
  "        if (connectDefault)\n",
  "        if (connectDefault)\n")

E("Client/NetSvc.cs",
  "            client.CloseClient();\n"
  "            client = null;\n"
  "        }\n"
  "        msgPackQue?.Clear();\n",
  "            client.CloseClient();\n"
  "            client = null;\n"
  "        }\n"
  "        NetInbox.ClearClient();\n")

E("Client/NetSvc.cs",
  "    /// <summary>\n"
  "    /// 【入队】把网络线程收到的消息放进队列，等主线程处理。\n"
  "    /// 由 ClientSession.OnReciveMsg 调用。\n"
  "    /// </summary>\n"
  "    public void AddMsgQue(NetMessage msg)\n"
  "    {\n"
  "        lock (pkgque_lock)\n"
  "        {\n"
  "            msgPackQue.Enqueue(msg);\n"
  "        }\n"
  "    }\n"
  "\n"
  "    /// <summary>\n"
  "    /// 【主线程轮询】每帧把队列里的消息取出来，交给 MessageCenter 分发。\n"
  "    /// 这是\"网络线程 → 主线程\"的关键桥梁。\n"
  "    /// 旧项目这里是一个 40+ 行的巨型 switch，现在压缩成一行分发调用。\n"
  "    /// </summary>\n"
  "    private void Update()\n"
  "    {\n"
  "        if (msgPackQue == null) return;\n"
  "\n"
  "        SendHeartbeat();\n"
  "\n"
  "        // 循环取出队列里所有待处理的消息\n"
  "        while (msgPackQue.Count > 0)\n"
  "        {\n"
  "            lock (pkgque_lock)\n"
  "            {\n"
  "                NetMessage msg = msgPackQue.Dequeue();\n"
  "                // 核心：交给 MessageCenter 按 CmdId 查表分发（替代巨型 switch）\n"
  "                MessageCenter.Dispatch(msg);\n"
  "            }\n"
  "        }\n"
  "    }\n",
  "    /// <summary>\n"
  "    /// 【主线程轮询】每帧把队列里的消息取出来，交给 MessageCenter 分发。\n"
  "    /// 这是\"网络线程 → 主线程\"的关键桥梁（队列在传输层的 <see cref=\"NetInbox\"/>）。\n"
  "    /// </summary>\n"
  "    private void Update()\n"
  "    {\n"
  "        SendHeartbeat();\n"
  "\n"
  "        // 主线程泵：把传输层队列里的消息逐条交给 MessageCenter 按 CmdId 分发\n"
  "        while (NetInbox.TryDequeueClient(out NetMessage msg))\n"
  "        {\n"
  "            MessageCenter.Dispatch(msg);\n"
  "        }\n"
  "    }\n")

E("Client/NetSvc.cs",
  "MessageCenter.Register<PingRspMsg>(CmdId.PingRsp, OnPingRsp);",
  "MessageCenter.Register<PingRspMsg>(NetCmdId.PingRsp, OnPingRsp);")

E("Client/NetSvc.cs",
  "SendToHost(MessageCenter.Pack(CmdId.PingReq, new PingReqMsg",
  "SendToHost(MessageCenter.Pack(NetCmdId.PingReq, new PingReqMsg")

E("Client/NetSvc.cs",
  "        MessageCenter.Unregister(CmdId.PingRsp);\n"
  "        Disconnect();\n"
  "        msgPackQue = null;\n"
  "        Instance = null;\n",
  "        MessageCenter.Unregister(NetCmdId.PingRsp);\n"
  "        Disconnect();\n"
  "        Instance = null;\n")

# ---------- NetHostSvc：删 HostMsg/队列/AddMsgQue，改抽 NetInbox；心跳走 NetCmdId ----------
E("Client/NetHostSvc.cs",
  "    /// <summary>一条来自成员的待处理消息（NetMessage + 来源 sid）</summary>\n"
  "    public struct HostMsg\n"
  "    {\n"
  "        public NetMessage Msg;\n"
  "        public uint Sid;       // 来源成员的会话ID\n"
  "    }\n"
  "\n"
  "    /// <summary>\n"
  "    /// KCP 服务器（房主）。泛型：\n",
  "    /// <summary>\n"
  "    /// KCP 服务器（房主）。泛型：\n")

E("Client/NetHostSvc.cs",
  "    /// <summary>消息队列：网络线程收到的消息先进这里，主线程再取出处理（线程安全缓冲）</summary>\n"
  "    private Queue<HostMsg> msgPackQue;\n"
  "\n"
  "    /// <summary>会话事件（成员建立连接 / 断开）。</summary>\n",
  "    /// <summary>会话事件（成员建立连接 / 断开）。</summary>\n")

E("Client/NetHostSvc.cs",
  "        Instance = this;\n"
  "        msgPackQue = new Queue<HostMsg>();\n"
  "        // 注册房间协议处理器（在主线程由 Update 分发时触发）\n",
  "        Instance = this;\n"
  "        // 注册房间协议处理器（在主线程由 Update 分发时触发）\n")

E("Client/NetHostSvc.cs",
  "MessageCenter.Register<PingReqMsg>(CmdId.PingReq, OnPingReq);",
  "MessageCenter.Register<PingReqMsg>(NetCmdId.PingReq, OnPingReq);")

E("Client/NetHostSvc.cs",
  "SendToSession(sid, MessageCenter.Pack(CmdId.PingRsp, new PingRspMsg",
  "SendToSession(sid, MessageCenter.Pack(NetCmdId.PingRsp, new PingRspMsg")

E("Client/NetHostSvc.cs",
  "    // ==================== 消息队列：网络线程 → 主线程 ====================\n"
  "    /// <summary>\n"
  "    /// 【入队】由 HostSession.OnReciveMsg 调用，把网络线程消息转到主线程。\n"
  "    /// 必须带上来源 sid。\n"
  "    /// </summary>\n"
  "    public void AddMsgQue(NetMessage msg, uint sid)\n"
  "    {\n"
  "        lock (pkgque_lock)\n"
  "        {\n"
  "            msgPackQue.Enqueue(new HostMsg { Msg = msg, Sid = sid });\n"
  "        }\n"
  "    }\n"
  "\n"
  "    /// <summary>\n",
  "    // ==================== 消息队列：网络线程 → 主线程 ====================\n"
  "    /// <summary>\n")

E("Client/NetHostSvc.cs",
  "    private void Update()\n"
  "    {\n"
  "        if (msgPackQue == null) return;\n"
  "\n"
  "        // ⚠ 先处理会话事件（连接/断开），再做消息派发：它们按到达顺序入队，\n"
  "        //   而且\"离开 → 广播名单\"必须跑在主线程（见 _sessionEvents 的注释）。\n"
  "        ProcessSessionEvents();\n"
  "\n"
  "        while (msgPackQue.Count > 0)\n"
  "        {\n"
  "            HostMsg hm;\n"
  "            lock (pkgque_lock)\n"
  "            {\n"
  "                hm = msgPackQue.Dequeue();\n"
  "            }\n"
  "            // 记录当前消息来源，供房间协议处理器识别是哪个成员发的\n",
  "    private void Update()\n"
  "    {\n"
  "        // ⚠ 先处理会话事件（连接/断开），再做消息派发：它们按到达顺序入队，\n"
  "        //   而且\"离开 → 广播名单\"必须跑在主线程（见 _sessionEvents 的注释）。\n"
  "        ProcessSessionEvents();\n"
  "\n"
  "        // 主线程泵：从传输层队列取一条（带来源 sid）\n"
  "        while (NetInbox.TryDequeueHost(out var hm))\n"
  "        {\n"
  "            // 记录当前消息来源，供房间协议处理器识别是哪个成员发的\n")

E("Client/NetHostSvc.cs",
  "        MessageCenter.Unregister(CmdId.PingReq);\n"
  "        StopHost();\n"
  "        msgPackQue = null;\n"
  "        Instance = null;\n",
  "        MessageCenter.Unregister(NetCmdId.PingReq);\n"
  "        StopHost();\n"
  "        Instance = null;\n")

# ---------- CmdId：ping 交 NetCmdId；删 login/chat 段 ----------
E("Services/CmdId.cs",
  "        // ===== 通用 =====\n"
  "        public const int PingReq = 1001;\n"
  "        public const int PingRsp = 1002;\n"
  "\n"
  "        // ===== 账号模块 =====\n"
  "        public const int LoginReq = 2001;\n"
  "        public const int LoginRsp = 2002;\n"
  "\n"
  "        // ===== 聊天模块 =====\n"
  "        /// <summary>客户端 -> 服务器</summary>\n"
  "        public const int ChatSend = 3001;\n"
  "        /// <summary>服务器 -> 所有客户端</summary>\n"
  "        public const int ChatBroadcast = 3002;\n"
  "\n"
  "        // ===== 房间模块（房主权威 / 局域网联机）=====\n",
  "        // ===== 房间模块（房主权威 / 局域网联机）=====\n")


def main():
    dry = "--dry" in sys.argv
    problems = []
    for path, old, new in EDITS:
        if not os.path.isfile(path):
            problems.append("NOT FOUND: " + path)
            continue
        text = open(path, "r", encoding="utf-8", newline="").read()
        n = text.count(old)
        if n != 1:
            problems.append("HIT=%d (expect 1): %s :: %s" % (n, os.path.basename(path), old.strip().splitlines()[-1][:60]))
    if problems:
        print("=== ABORT，未落盘 ===")
        for p in problems:
            print("  " + p)
        return 1

    for path, old, new in EDITS:
        text = open(path, "r", encoding="utf-8", newline="").read()
        text = text.replace(old, new, 1)
        open(path, "w", encoding="utf-8", newline="").write(text)
        print("OK  " + os.path.relpath(path, ROOT))

    print("\n全部 %d 处改写完成" % len(EDITS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
