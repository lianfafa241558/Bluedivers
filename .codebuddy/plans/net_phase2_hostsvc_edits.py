# -*- coding: utf-8 -*-
"""
联机重构 · 二期：NetHostSvc 把「连接/会话/存活/心跳应答」交给 02_Net 的 NetServer，自己只留房间语义。

安全策略：每个 (old, new) 必须在该文件里**恰好命中 1 次**，否则整脚本中止、不落盘。
文件为 UTF-8 with BOM + LF（一期已探测；二期同文件）。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PATH = os.path.join(ROOT, "Assets", "Scripts", "07NetGame", "NetHostSvc.cs")

EDITS = []


def E(old, new):
    EDITS.append((old, new))


# ---- 1) 传输字段：KCPNet<HostSession,...> → 持一个 NetServer ----
E("    /// <summary>\n"
  "    /// KCP 服务器（房主）。泛型：\n"
  "    ///   &lt;HostSession&gt;：代表一个成员的会话类（收/发/连接/断开回调）\n"
  "    ///   &lt;NetMessage&gt;  ：这条连接收发消息的类型（我们的信封）\n"
  "    /// </summary>\n"
  "    private KCPNet<HostSession, NetMessage> host;\n",

  "    /// <summary>【传输层】房主侧连接管理（纯类，见 02_Net）：socket / 会话表 / 广播 / 会话事件 / 心跳应答。</summary>\n"
  "    private NetServer _server;\n")

# ---- 2) 会话事件队列 + 锁：整体搬进 NetServer ----
E("    /// <summary>会话事件（成员建立连接 / 断开）。</summary>\n"
  "    private struct SessionEvent\n"
  "    {\n"
  "        public uint Sid;\n"
  "        public bool Joined;\n"
  "    }\n"
  "\n"
  "    /// <summary>\n"
  "    /// 会话事件队列 —— 由**传输线程**入队、**主线程**处理。\n"
  "    ///\n"
  "    /// <para>▍为什么必须过一遍队列：<c>host.OnSessionConnected/OnSessionDisconnected</c> 是 KCP\n"
  "    /// <c>KCPSession.UpdateAsync</c> 的续体，跑在 **ThreadPool 线程**上（不是主线程）；而\n"
  "    /// \"成员离开 → <c>BroadcastPlayerList()</c> → 09 侧桥 <c>NetFriendBridge.HandleRoster</c> →\n"
  "    /// <c>ResSvc.LoadRes</c> / <c>Instantiate</c>\" 全程都是 Unity API ⇒ 直接在回调里跑会抛\n"
  "    /// <c>UnityException: Load can only be called from the main thread</c>（2026-10-06 打包版实测），\n"
  "    /// 而且它会在<b>广播名单中途</b>炸掉，导致该清退的盟友留在场上变成幽灵。</para>\n"
  "    /// </summary>\n"
  "    private readonly Queue<SessionEvent> _sessionEvents = new Queue<SessionEvent>();\n"
  "\n"
  "    /// <summary>队列锁</summary>\n"
  "    public static readonly string pkgque_lock = \"pkgque_lock\";\n"
  "\n",

  "")

# ---- 3) _lastSeen 字段搬进 NetServer ----
E("    /// <summary>sid → 最后一次收到该成员消息的时刻（<c>Time.unscaledTime</c>）。\n"
  "    /// <para>⚠ 只在主线程读写（写入点在 <see cref=\"Update\"/> 出队时，读取点在 <see cref=\"EvictIdleMembers\"/>）⇒ 不需要锁。</para></summary>\n"
  "    private readonly Dictionary<uint, float> _lastSeen = new Dictionary<uint, float>();\n"
  "\n",

  "")

# ---- 4) _selfClosed 字段搬进 NetServer ----
E("    /// <summary>**本端主动关掉**的会话 sid（离开房间 / 空闲踢人）。库随后回调的断开事件只会再清一遍痕迹，\n"
  "    /// 不再重复提示 —— 否则同一个人会先\"成员离开\"、隔一帧又\"成员断连（未入房）\"（2026-10-07 用户实测）。</summary>\n"
  "    private readonly HashSet<uint> _selfClosed = new HashSet<uint>();\n"
  "\n",

  "")

# ---- 5) Awake：心跳注册搬去 NetServer ----
E("        // 心跳：成员定时发 Ping，房主回 Pong。\n"
  "        // ① 让成员端\"收不到数据就判掉线\"的库内检查不会误杀健康的空转连接（舰桥阶段两端本来没业务报文）；\n"
  "        // ② 顺带成为\"这个成员还活着吗\"的依据（见 EvictIdleMembers）。\n"
  "        MessageCenter.Register<PingReqMsg>(NetCmdId.PingReq, OnPingReq);\n"
  "    }\n",

  "        // 心跳（NetCmdId.PingReq → 回 Pong）在传输层 NetServer 里注册：它必须对\"任何会话\"都回\n"
  "    }\n")

# ---- 6) StartHost：创建 KCPNet → 启动 NetServer ----
E("        // ① 创建 KCP 服务器（房主）\n"
  "        host = new KCPNet<HostSession, NetMessage>();\n"
  "\n"
  "        // 以\"服务器\"身份监听端口，等待成员回连。\n"
  "        // 说明：P2P 模式下 DLL 直接放客户端，房主客户端内嵌一个迷你\"服务器\"，\n"
  "        //       它只负责建连/转发，真正的游戏逻辑在房主本地的 Unity 主线程跑。\n"
  "        // ⚠️ UDP 端口关闭后不会立刻释放（TIME_WAIT），若刚关房立即重开同一端口，\n"
  "        //    会抛 SocketException。这里捕获并转成清晰报错，避免中断主循环。\n"
  "        try\n"
  "        {\n"
  "            host.StartAsServer(\"0.0.0.0\", hostPort);\n"
  "        }\n"
  "        catch (System.Net.Sockets.SocketException e)\n"
  "        {\n"
  "            host = null;\n"
  "            _broadcaster?.Stop();\n"
  "            _broadcaster = null;\n"
  "            RoomInfo = null;\n"
  "            KCPTool.ColorLog(KCPLogColor.Red, \"开房失败，端口 {0} 可能尚未释放:{1}。请稍等片刻再开房。\", hostPort, e.Message);\n"
  "            throw new System.Exception($\"[NetHostSvc] 开房失败，端口 {hostPort} 尚未释放（刚关房后需等待几秒）。原因:{e.Message}\");\n"
  "        }\n"
  "\n"
  "        // 监听成员进出，用于更新房间人数/清理成员数据\n"
  "        host.OnSessionConnected += OnMemberJoined;\n"
  "        host.OnSessionDisconnected += OnMemberLeft;\n"
  "\n"
  "        KCPTool.ColorLog(KCPLogColor.Green, \"房主服务器已启动，监听端口 {0}\", hostPort);\n",

  "        // ① 创建并启动传输层服务器：socket / 会话 / 心跳 / 存活都交给 02_Net 的 NetServer，\n"
  "        //    房主本地只跑游戏逻辑（P2P 模式下 DLL 直接放客户端，房主内嵌一个迷你\"服务器\"）。\n"
  "        _server = new NetServer();\n"
  "        _server.OnConnected += OnSessionConnected;\n"
  "        _server.OnDisconnected += HandleMemberLeft;\n"
  "\n"
  "        // ⚠️ UDP 端口关闭后不会立刻释放（TIME_WAIT），若刚关房立即重开同一端口会抛 SocketException；\n"
  "        //    传输层把原始原因回传，这里保持原来的提示文案与异常类型（调用方 NetRoomFlow.Host 在抓）。\n"
  "        if (!_server.Start(\"0.0.0.0\", hostPort, out string sockErr))\n"
  "        {\n"
  "            _server = null;\n"
  "            _broadcaster?.Stop();\n"
  "            _broadcaster = null;\n"
  "            RoomInfo = null;\n"
  "            KCPTool.ColorLog(KCPLogColor.Red, \"开房失败，端口 {0} 可能尚未释放:{1}。请稍等片刻再开房。\", hostPort, sockErr);\n"
  "            throw new System.Exception($\"[NetHostSvc] 开房失败，端口 {hostPort} 尚未释放（刚关房后需等待几秒）。原因:{sockErr}\");\n"
  "        }\n"
  "\n"
  "        KCPTool.ColorLog(KCPLogColor.Green, \"房主服务器已启动，监听端口 {0}\", hostPort);\n")

# ---- 7a) StopHost：关闭服务器 ----
E("        if (host != null)\n"
  "        {\n"
  "            host.OnSessionConnected -= OnMemberJoined;\n"
  "            host.OnSessionDisconnected -= OnMemberLeft;\n"
  "            host.CloseServer();\n"
  "            host = null;\n"
  "        }\n",

  "        if (_server != null)\n"
  "        {\n"
  "            _server.OnConnected -= OnSessionConnected;\n"
  "            _server.OnDisconnected -= HandleMemberLeft;\n"
  "            _server.Stop();          // 内部：关服务器 + 退订心跳 + 清会话/存活/主动关闭登记表\n"
  "            _server = null;\n"
  "        }\n")

# ---- 7b) StopHost：清表（存活/主动关闭登记表已随 NetServer.Stop 清） ----
E("        _poses.Clear();          // 关房：位姿表一并清掉（否则下一房会带出旧位姿）\n"
  "        _lastSeen.Clear();       // 心跳计时也一并清（新房里重新计）\n"
  "        _selfClosed.Clear();     // 主动关会话的登记表同理（新房的 sid 从零开始）\n"
  "        _ghostLogged.Clear();    // 同上\n",

  "        _poses.Clear();          // 关房：位姿表一并清掉（否则下一房会带出旧位姿）\n"
  "        _ghostLogged.Clear();    // \"未入房断连\"日志去重表（存活表/主动关闭登记表由 NetServer.Stop 清）\n")

# ---- 8) 成员进出：删传输线程入队 + ProcessSessionEvents + 旧 CloseSessionOf，改成订阅传输层事件 ----
E("    // ==================== 成员进出（传输层事件 → 只入队） ====================\n"
  "    /// <summary>成员建立 TCP/KCP 会话（此时还没 JoinRoom，只是连上了）。\n"
  "    /// <para>⚠ 本方法跑在**传输线程**（见 <see cref=\"_sessionEvents\"/>）⇒ 这里只入队，别动任何游戏状态。</para></summary>\n"
  "    private void OnMemberJoined(uint sid)\n"
  "    {\n"
  "        lock (pkgque_lock)\n"
  "        {\n"
  "            _sessionEvents.Enqueue(new SessionEvent { Sid = sid, Joined = true });\n"
  "        }\n"
  "    }\n"
  "\n"
  "    /// <summary>成员会话断开。\n"
  "    /// <para>⚠ 同 <see cref=\"OnMemberJoined\"/>：传输线程回调，只入队；真正的清理在主线程做。</para></summary>\n"
  "    private void OnMemberLeft(uint sid)\n"
  "    {\n"
  "        lock (pkgque_lock)\n"
  "        {\n"
  "            _sessionEvents.Enqueue(new SessionEvent { Sid = sid, Joined = false });\n"
  "        }\n"
  "    }\n"
  "\n"
  "    // ==================== 成员进出（主线程处理） ====================\n"
  "    /// <summary>主线程：按到达顺序处理会话事件（每帧在 <see cref=\"Update\"/> 里跑一次）。</summary>\n"
  "    private void ProcessSessionEvents()\n"
  "    {\n"
  "        while (true)\n"
  "        {\n"
  "            SessionEvent ev;\n"
  "            lock (pkgque_lock)\n"
  "            {\n"
  "                if (_sessionEvents.Count == 0) return;\n"
  "                ev = _sessionEvents.Dequeue();\n"
  "            }\n"
  "\n"
  "            // ⚠ 出队之后在锁外处理：BroadcastPlayerList 会走网络发送 + 本地派发，不该占着锁\n"
  "            if (ev.Joined) Debug.Log($\"[NetHostSvc] 成员建立连接 sid={ev.Sid}（尚未入房）\");\n"
  "            else HandleMemberLeft(ev.Sid);\n"
  "        }\n"
  "    }\n"
  "\n"
  "    /// <summary>成员会话断开（主线程）：若已入房，从房间移除并广播玩家列表</summary>\n"
  "    private void HandleMemberLeft(uint sid)\n"
  "    {\n"
  "        bool selfClosed = _selfClosed.Remove(sid);   // 本端主动关的（离开房间 / 踢人）⇒ 上面那步已经处理过，别重复\n"
  "\n"
  "        if (_players.Remove(sid))\n"
  "        {\n"
  "            _profiles.Remove(sid);   // 资料跟人走，人走了资料也删（否则 BuildProfileArray 会漏出僵尸项）\n"
  "            _poses.Remove(sid);      // 位姿表同样要清：不清的话这条\"幽灵位姿\"会跟着每一批快照一直下发\n"
  "            _lastSeen.Remove(sid);\n"
  "            Debug.Log($\"[NetHostSvc] 成员离开 sid={sid}，剩余玩家 {TotalPlayers}\");\n"
  "            BroadcastPlayerList();   // ⚠ 战斗期名单**冻结**，这一句会被跳过（下面那条\"离开\"消息才是关键）\n"
  "            // ★ 无论名单冻不冻结都要告诉各端\"这个人走了\"（只带 sid、不重排）：\n"
  "            //   否则战斗中强退的盟友模型会一直留在别人屏幕上（2026-10-07 实测）。\n"
  "            SendToAll(MessageCenter.Pack(CmdId.PlayerLeftNtf, new PlayerLeftMsg { Sid = sid }));\n"
  "            return;\n"
  "        }\n"
  "\n"
  "        // 未入房的会话（连上没 JoinRoom / 离房后滞留 / 客户端被强杀）：没有房间状态要广播，但**也要清** ——\n"
  "        // Update 对\"任何来源的消息\"都记过 _lastSeen（含未入房会话）⇒ 不清就是每来一次连接留一条永久条目。\n"
  "        _poses.Remove(sid);\n"
  "        _lastSeen.Remove(sid);\n"
  "        // ⚠ 日志只报**第一次**：没入过房的连接本来就没有任何游戏意义，循环重连时会刷屏\n"
  "        //   （2026-10-07 用户实测：每二十来秒一串，分不清到底有没有人被踢）。\n"
  "        if (!selfClosed && _ghostLogged.Add(sid)) Debug.Log($\"[NetHostSvc] 成员断连 sid={sid}（未入房，未参与游戏，后续同类不再提示）\");\n"
  "    }\n"
  "\n"
  "    /// <summary>主动关闭某个成员的会话（离开房间 / 空闲踢人）。\n"
  "    /// <para>▍为什么要先登记 <see cref=\"_selfClosed\"/>：<c>CloseSession</c> 会触发库的断开回调 ⇒ 又会进\n"
  "    /// <see cref=\"HandleMemberLeft\"/>，登记过就只静默清痕迹。</para></summary>\n"
  "    private void CloseSessionOf(uint sid)\n"
  "    {\n"
  "        _selfClosed.Add(sid);\n"
  "        if (host != null && host.TryGetSession(sid, out HostSession s)) s.CloseSession();\n"
  "    }\n",

  "    // ==================== 成员进出（传输层已转主线程，这里只做游戏侧清理） ====================\n"
  "    /// <summary>成员建立了会话（还没 JoinRoom，只是连上了）。</summary>\n"
  "    private void OnSessionConnected(uint sid)\n"
  "    {\n"
  "        Debug.Log($\"[NetHostSvc] 成员建立连接 sid={sid}（尚未入房）\");\n"
  "    }\n"
  "\n"
  "    /// <summary>成员会话断开（传输层已在主线程回调）：若已入房，从房间移除并广播玩家列表。\n"
  "    /// <para><paramref name=\"selfClosed\"/> = 本端主动关的（离开房间 / 踢人）⇒ 上面那步已经处理过，不再重复提示。</para></summary>\n"
  "    private void HandleMemberLeft(uint sid, bool selfClosed)\n"
  "    {\n"
  "        if (_players.Remove(sid))\n"
  "        {\n"
  "            _profiles.Remove(sid);   // 资料跟人走，人走了资料也删（否则 BuildProfileArray 会漏出僵尸项）\n"
  "            _poses.Remove(sid);      // 位姿表同样要清：不清的话这条\"幽灵位姿\"会跟着每一批快照一直下发\n"
  "            Debug.Log($\"[NetHostSvc] 成员离开 sid={sid}，剩余玩家 {TotalPlayers}\");\n"
  "            BroadcastPlayerList();   // ⚠ 战斗期名单**冻结**，这一句会被跳过（下面那条\"离开\"消息才是关键）\n"
  "            // ★ 无论名单冻不冻结都要告诉各端\"这个人走了\"（只带 sid、不重排）：\n"
  "            //   否则战斗中强退的盟友模型会一直留在别人屏幕上（2026-10-07 实测）。\n"
  "            SendToAll(MessageCenter.Pack(CmdId.PlayerLeftNtf, new PlayerLeftMsg { Sid = sid }));\n"
  "            return;\n"
  "        }\n"
  "\n"
  "        // 未入房的会话（连上没 JoinRoom / 离房后滞留 / 客户端被强杀）：没有房间状态要广播，但**也要清**位姿表。\n"
  "        _poses.Remove(sid);\n"
  "        // ⚠ 日志只报**第一次**：没入过房的连接本来就没有任何游戏意义，循环重连时会刷屏\n"
  "        //   （2026-10-07 用户实测：每二十来秒一串，分不清到底有没有人被踢）。\n"
  "        if (!selfClosed && _ghostLogged.Add(sid)) Debug.Log($\"[NetHostSvc] 成员断连 sid={sid}（未入房，未参与游戏，后续同类不再提示）\");\n"
  "    }\n")

# ---- 9) 删 OnPingReq（搬去 NetServer）；EvictIdleMembers 改查传输层存活表 ----
E("    // ==================== 心跳 / 空闲踢人 ====================\n"
  "    /// <summary>【成员心跳】房主回一条 <see cref=\"PingRspMsg\"/>（成员据此测 RTT，也让它的会话\"有收到数据\"）。\n"
  "    ///\n"
  "    /// <para>▍⚠⚠ **必须对\"任何会话\"都回，包括还没入房的**（2026-10-07 实测的\"战备界面等一会儿就反复断开\"就是它的锅）：\n"
  "    /// 库的会话超时判的是\"**收不到数据**就判掉线\"（<c>TimeoutMs</c> 默认 15s），而成员从\"连上房主\"到\"入房成功\"\n"
  "    /// 之间**只有心跳这一条报文**。旧实现在这里加了 <c>_players.ContainsKey(sid)</c> 的早退 ⇒ 未入房的连接\n"
  "    /// **一个字节都收不到** ⇒ 15s 后必然被**它自己的库**判死 → 客户端重连（新会话又是同一 sid）→ 再被判死 ⇒\n"
  "    /// 房主控制台每二十来秒刷一串 <c>成员断开 / 成员断连（未入房）</c>，而真正在房间里的那个成员一直好着。</para>\n"
  "    ///\n"
  "    /// <para>关闸（<see cref=\"CloseJoin\"/>，Armament 之后不收人）由入房校验负责，**不该靠\"不回心跳\"来饿死对方**。</para>\n"
  "    /// </summary>\n"
  "    private void OnPingReq(PingReqMsg req)\n"
  "    {\n"
  "        if (req == null) return;\n"
  "\n"
  "        uint sid = CurrentSid;\n"
  "        if (sid == 0) return;                        // 没有来源会话 ⇒ 不该发生\n"
  "\n"
  "        SendToSession(sid, MessageCenter.Pack(NetCmdId.PingRsp, new PingRspMsg\n"
  "        {\n"
  "            Id = req.Id,\n"
  "            ServerTime = System.DateTime.UtcNow.Ticks / System.TimeSpan.TicksPerMillisecond,\n"
  "        }));\n"
  "    }\n"
  "\n"
  "    /// <summary>\n"
  "    /// 【空闲踢人】成员被强杀 / 断网时，房主收不到任何\"我走了\"的通知（UDP 无连接），\n"
  "    /// 会话会一直挂着、盟友实例也就一直留在场上。这里用\"最后一次收到该成员消息的时刻\"兜底：\n"
  "    /// 超时即按\"他离开了\"处理（清表 + 广播名单 ⇒ 各端桥会清掉盟友实例），并主动关掉该会话。\n"
  "    ///\n"
  "    /// <para>▍为什么本帧就能判定：<see cref=\"Update\"/> 出队每条消息时都会刷新 <see cref=\"_lastSeen\"/>，\n"
  "    /// 心跳（<c>PingReqMsg</c>）与战斗中的位姿上行都是\"活着的证据\"。</para>\n"
  "    /// </summary>\n"
  "    private void EvictIdleMembers()\n"
  "    {\n"
  "        if (memberIdleTimeout <= 0f || _players.Count == 0) return;\n"
  "\n"
  "        float now = Time.unscaledTime;\n"
  "        _idleBuffer.Clear();\n"
  "        foreach (var kv in _players)\n"
  "        {\n"
  "            // 刚入房、还没收到过任何消息：先记当前时刻，从下一帧起算超时\n"
  "            if (!_lastSeen.TryGetValue(kv.Key, out float t))\n"
  "            {\n"
  "                _lastSeen[kv.Key] = now;\n"
  "                continue;\n"
  "            }\n"
  "            if (now - t > memberIdleTimeout) _idleBuffer.Add(kv.Key);\n"
  "        }\n"
  "\n"
  "        for (int i = 0; i < _idleBuffer.Count; ++i)\n"
  "        {\n"
  "            uint sid = _idleBuffer[i];\n"
  "            Debug.LogWarning($\"[NetHostSvc] 成员 sid={sid} 已 {memberIdleTimeout:0.#}s 无任何消息（疑似强退/断网）⇒ 按离开处理\");\n"
  "            HandleMemberLeft(sid);\n"
  "            // 主动收尾会话：让库把该会话的重传缓冲/句柄释放掉，并触发一次 OnSessionDisconnected\n"
  "            //（那时 _players 里已经没有他了，会走\"未入房\"分支；已在 CloseSessionOf 里登记 ⇒ 不再重复提示）\n"
  "            CloseSessionOf(sid);\n"
  "        }\n"
  "    }\n",

  "    // ==================== 空闲踢人 ====================\n"
  "    /// <summary>\n"
  "    /// 【空闲踢人】成员被强杀 / 断网时，房主收不到任何\"我走了\"的通知（UDP 无连接），\n"
  "    /// 会话会一直挂着、盟友实例也就一直留在场上。这里用\"最后一次收到该成员消息的时刻\"兜底：\n"
  "    /// 超时即按\"他离开了\"处理（清表 + 广播名单 ⇒ 各端桥会清掉盟友实例），并主动关掉该会话。\n"
  "    ///\n"
  "    /// <para>▍存活时刻表在传输层 <see cref=\"NetServer\"/>（它出队每条消息时刷新），本方法只做\"谁是玩家\"的判断。</para>\n"
  "    /// </summary>\n"
  "    private void EvictIdleMembers()\n"
  "    {\n"
  "        if (_server == null || memberIdleTimeout <= 0f || _players.Count == 0) return;\n"
  "\n"
  "        float now = Time.unscaledTime;\n"
  "        _idleBuffer.Clear();\n"
  "        foreach (var kv in _players)\n"
  "        {\n"
  "            float t = _server.LastSeen(kv.Key);\n"
  "            // 刚入房、还没收到过任何消息：先记当前时刻，从下一帧起算超时\n"
  "            if (t < 0f) { _server.Touch(kv.Key); continue; }\n"
  "            if (now - t > memberIdleTimeout) _idleBuffer.Add(kv.Key);\n"
  "        }\n"
  "\n"
  "        for (int i = 0; i < _idleBuffer.Count; ++i)\n"
  "        {\n"
  "            uint sid = _idleBuffer[i];\n"
  "            Debug.LogWarning($\"[NetHostSvc] 成员 sid={sid} 已 {memberIdleTimeout:0.#}s 无任何消息（疑似强退/断网）⇒ 按离开处理\");\n"
  "            HandleMemberLeft(sid, false);\n"
  "            // 主动收尾会话（内部登记 _selfClosed ⇒ 随后库回调的那次 OnDisconnected 不再重复提示）\n"
  "            _server.CloseSession(sid);\n"
  "        }\n"
  "    }\n")

# ---- 10) OnLeaveRoomNtf：清表交给传输层；关会话走 NetServer ----
E("            _profiles.Remove(sid);\n"
  "            _poses.Remove(sid);\n"
  "            _lastSeen.Remove(sid);\n"
  "            Debug.Log($\"[NetHostSvc] 玩家离开房间 sid={sid}，当前玩家 {TotalPlayers}/{MaxPlayers}\");\n"
  "            BroadcastPlayerList();\n"
  "        }\n"
  "\n"
  "        // ★ 主动离开 = 断开连接：立刻回收会话。否则它会以\"未入房\"的身份滞留到库超时，\n"
  "        //   十几秒后再多报一条\"成员断连（未入房）\"——客户端来回加入/退出几次就攒出一串幽灵会话（2026-10-07 实测）。\n"
  "        CloseSessionOf(sid);\n",

  "            _profiles.Remove(sid);\n"
  "            _poses.Remove(sid);\n"
  "            Debug.Log($\"[NetHostSvc] 玩家离开房间 sid={sid}，当前玩家 {TotalPlayers}/{MaxPlayers}\");\n"
  "            BroadcastPlayerList();\n"
  "        }\n"
  "\n"
  "        // ★ 主动离开 = 断开连接：立刻回收会话。否则它会以\"未入房\"的身份滞留到库超时，\n"
  "        //   十几秒后再多报一条\"成员断连（未入房）\"——客户端来回加入/退出几次就攒出一串幽灵会话（2026-10-07 实测）。\n"
  "        _server?.CloseSession(sid);\n")

# ---- 11) CurrentSid 改成委托传输层 ----
E("    /// <summary>当前正在主线程分发的这条消息来自哪个成员（由 Update 出队时设置）。\n"
  "    /// <para>⚠ 公开读：<c>NetRoomFlow</c> 的路由处理器要靠它知道\"这条请求是谁发的\"（同一个派发口径）。</para></summary>\n"
  "    public uint CurrentSid { get; private set; }\n",

  "    /// <summary>当前正在主线程分发的这条消息来自哪个成员（由传输层 <see cref=\"NetServer\"/> 出队时设置）。\n"
  "    /// <para>⚠ 公开读：<c>NetRoomFlow</c> 的路由处理器要靠它知道\"这条请求是谁发的\"（同一个派发口径）。</para></summary>\n"
  "    public uint CurrentSid => _server != null ? _server.CurrentSid : 0u;\n")

# ---- 12) 发送/广播 → 委托传输层 ----
E("    /// <summary>广播给所有成员（不含房主本地）。</summary>\n"
  "    public void SendToAll(NetMessage msg)\n"
  "    {\n"
  "        host?.SendToAll(msg);\n"
  "    }\n"
  "\n"
  "    /// <summary>定向发送给指定成员（按 sid）。房主权威下常用来做私密回复。</summary>\n"
  "    public void SendToSession(uint sid, NetMessage msg)\n"
  "    {\n"
  "        if (host != null && host.TryGetSession(sid, out HostSession s) && s.IsConnected())\n"
  "        {\n"
  "            s.SendMsg(msg);\n"
  "        }\n"
  "    }\n",

  "    /// <summary>广播给所有成员（不含房主本地）。</summary>\n"
  "    public void SendToAll(NetMessage msg) => _server?.SendToAll(msg);\n"
  "\n"
  "    /// <summary>定向发送给指定成员（按 sid）。房主权威下常用来做私密回复。</summary>\n"
  "    public void SendToSession(uint sid, NetMessage msg) => _server?.SendTo(msg, sid);\n")

# ---- 13) Update：泵交给传输层 ----
E("    // ==================== 消息队列：网络线程 → 主线程 ====================\n"
  "    /// <summary>\n"
  "    /// 【主线程轮询】每帧把队列里的消息取出来，交给 MessageCenter 分发。\n"
  "    /// </summary>\n"
  "    private void Update()\n"
  "    {\n"
  "        // ⚠ 先处理会话事件（连接/断开），再做消息派发：它们按到达顺序入队，\n"
  "        //   而且\"离开 → 广播名单\"必须跑在主线程（见 _sessionEvents 的注释）。\n"
  "        ProcessSessionEvents();\n"
  "\n"
  "        // 主线程泵：从传输层队列取一条（带来源 sid）\n"
  "        while (NetInbox.TryDequeueHost(out var hm))\n"
  "        {\n"
  "            // 记录当前消息来源，供房间协议处理器识别是哪个成员发的\n"
  "            CurrentSid = hm.Sid;\n"
  "            // 收到任何一条来自该成员的消息（心跳/位姿/准备…）都算\"他还活着\"（见 EvictIdleMembers）\n"
  "            if (hm.Sid != 0) _lastSeen[hm.Sid] = Time.unscaledTime;\n"
  "            try\n"
  "            {\n"
  "                MessageCenter.Dispatch(hm.Msg);\n"
  "            }\n"
  "            finally\n"
  "            {\n"
  "                CurrentSid = 0;\n"
  "            }\n"
  "        }\n"
  "\n"
  "        // ⚠ 放在 BroadcastPoses 之前：刚被判离线的成员不该再出现在这一批快照里\n"
  "        EvictIdleMembers();\n"
  "\n"
  "        BroadcastPoses();   // 位姿聚合下行（固定频率，见 poseBroadcastHz）\n"
  "    }\n",

  "    // ==================== 每帧泵 ====================\n"
  "    /// <summary>\n"
  "    /// 【主线程轮询】<c>NetServer.Pump()</c> 负责\"会话事件 + 分发 + 存活刷新\"，\n"
  "    /// 这里只补游戏侧的两件：空闲踢人、位姿聚合下行。\n"
  "    /// </summary>\n"
  "    private void Update()\n"
  "    {\n"
  "        // ⚠ 传输层的 Pump 里顺序是\"会话事件 → 分发\"（会话事件按到达顺序入队，\n"
  "        //   而且\"离开 → 广播名单\"必须跑在主线程）\n"
  "        _server?.Pump();\n"
  "\n"
  "        // ⚠ 放在 BroadcastPoses 之前：刚被判离线的成员不该再出现在这一批快照里\n"
  "        EvictIdleMembers();\n"
  "\n"
  "        BroadcastPoses();   // 位姿聚合下行（固定频率，见 poseBroadcastHz）\n"
  "    }\n")

# ---- 14) OnDestroy：心跳退订已在 NetServer.Stop ----
E("        MessageCenter.Unregister(CmdId.PlayerTransformUp);\n"
  "        MessageCenter.Unregister(NetCmdId.PingReq);\n"
  "        StopHost();\n",

  "        MessageCenter.Unregister(CmdId.PlayerTransformUp);\n"
  "        StopHost();\n")

# ---- 15) 类注释：职责更新 ----
E("///   1. 以服务器身份 StartAsServer 监听端口，等待成员回连。\n"
  "///   2. 维护成员会话列表 + 玩家信息（谁加入了、是否准备）。\n"
  "///   3. 处理房间业务协议：加入/离开/准备/开始游戏（见 CmdId.Room*）。\n"
  "///   4. 提供 SendToAll / SendToSession，实现房主权威的\"转发/广播\"。\n"
  "///   5. 用消息队列把网络线程消息转到主线程，交给 MessageCenter 分发。\n"
  "///   6. 联动 LanBroadcaster：把监听端口注入房间广播，成员才能发现并回连。\n",

  "///   1. 用传输层 <c>NetServer</c>（02_Net）监听端口、管理会话（socket/心跳/存活都在那里）。\n"
  "///   2. 维护成员玩家信息（谁加入了、是否准备）。\n"
  "///   3. 处理房间业务协议：加入/离开/准备/开始游戏（见 CmdId.Room*）。\n"
  "///   4. 提供 SendToAll / SendToSession，实现房主权威的\"转发/广播\"。\n"
  "///   5. 每帧由传输层把网络线程消息转到主线程并分发（<c>NetServer.Pump</c>）。\n"
  "///   6. 联动 LanBroadcaster：把监听端口注入房间广播，成员才能发现并回连。\n")


def main():
    problems = []
    for i, (old, new) in enumerate(EDITS):
        text = open(PATH, "r", encoding="utf-8", newline="").read()
        n = text.count(old)
        if n != 1:
            problems.append("EDIT#%d HIT=%d (expect 1) :: %s" % (i + 1, n, old.strip().splitlines()[0][:70]))
    if problems:
        print("=== ABORT，未落盘 ===")
        for p in problems:
            print("  " + p)
        return 1

    for i, (old, new) in enumerate(EDITS):
        text = open(PATH, "r", encoding="utf-8", newline="").read()
        open(PATH, "w", encoding="utf-8", newline="").write(text.replace(old, new, 1))
        print("OK  EDIT#%d" % (i + 1))

    print("\n全部 %d 处改写完成" % len(EDITS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
