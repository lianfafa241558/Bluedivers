# -*- coding: utf-8 -*-
"""
本轮三件事的接线：
A. 4039 波次中心（房主 ~2Hz 下发 + 成员插值跟随）
B. （昼夜墙钟在 TimeProgressionModule.cs，已整文件改写）
C. EnemyController 改走抽出的 SyncedNavMover（纯结构收敛、行为不变、不改消息）

每个 (old,new) 必须在该文件里恰好命中 1 次，否则整脚本中止、不落盘。行尾自适应。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EC = os.path.join(ROOT, "Assets", "Scripts", "06Gameplay", "AI", "Controller", "EnemyController.cs")
RF = os.path.join(ROOT, "Assets", "Scripts", "07NetGame", "NetRoomFlow.cs")
WM = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "WaveManager.cs")

EDITS = []


def E(path, old, new):
    EDITS.append((path, old, new))


# ==================== C. EnemyController -> SyncedNavMover ====================

E(EC,
  "        /// <summary>网络下发但**当帧应用不了**的目标点（见 <see cref=\"ApplyRemoteDestination\"/>）。</summary>\n"
  "        private Vector3 _pendingRemoteDestination;\n"
  "\n"
  "        /// <summary>是否有待落地的网络目标点（agent 被禁用时置位，由 <see cref=\"Update\"/> 重试到成功）。</summary>\n"
  "        private bool _hasPendingRemoteDestination;\n",

  "        /// <summary>联机落点组件：网络目标点的缓存 / 重试 / 绕过去重全在它里面。\n"
  "        /// <para>2026-10-08 从本类抽出 —— 同一套逻辑每个\"房主权威移动\"的单位都要一份\n"
  "        /// （见 <see cref=\"SyncedNavMover\"/> 的类注释）。</para></summary>\n"
  "        private SyncedNavMover _mover;\n"
  "\n"
  "        private SyncedNavMover Mover\n"
  "        {\n"
  "            get\n"
  "            {\n"
  "                if (_mover == null)\n"
  "                {\n"
  "                    if (!TryGetComponent(out _mover)) _mover = gameObject.AddComponent<SyncedNavMover>();\n"
  "                    // 敌人的落地通道：走 UnitEventBus.PathRequest（09 的 PathRequestManager 带投影兜底）\n"
  "                    _mover.applyHandler = TryRequestPath;\n"
  "                }\n"
  "                return _mover;\n"
  "            }\n"
  "        }\n")

E(EC,
  "            // 补发\"早到/当时 agent 不可用\"的网络目标点（见 ApplyRemoteDestination）\n"
  "            RetryPendingRemoteDestination();\n"
  "\n"
  "            //DetectionModule?.HandleTargetDetection();\n",

  "            // ⚠ \"补发早到的网络目标点\"已挪进 SyncedNavMover 自己的 Update（2026-10-08），这里不再重复驱动\n"
  "\n"
  "            //DetectionModule?.HandleTargetDetection();\n")

E(EC,
  "        public void ApplyRemoteDestination(Vector3 destination)\n"
  "        {\n"
  "            m_lastDestination = destination;\n"
  "            _pendingRemoteDestination = destination;\n"
  "            _hasPendingRemoteDestination = true;\n"
  "            RetryPendingRemoteDestination();\n"
  "        }\n"
  "\n"
  "        /// <summary>把待落地的网络目标点补发出去；成功后清标记（失败时每帧只做一次状态判断，开销可忽略）。</summary>\n"
  "        private void RetryPendingRemoteDestination()\n"
  "        {\n"
  "            if (!_hasPendingRemoteDestination) return;\n"
  "            if (TryRequestPath(_pendingRemoteDestination)) _hasPendingRemoteDestination = false;\n"
  "        }\n",

  "        public void ApplyRemoteDestination(Vector3 destination)\n"
  "        {\n"
  "            m_lastDestination = destination;\n"
  "            Mover.ApplyRemoteDestination(destination);   // 缓存 + 立即尝试 + 失败则每帧重试（都在组件里）\n"
  "        }\n")

E(EC,
  "            _hasPendingRemoteDestination = false;   // 已死：agent 会被禁用，别再每帧去补发\n",

  "            // 已死：agent 会被禁用，别再每帧去补发（⚠ 故意不碰 Mover 属性，避免为一个「取消」白建组件）\n"
  "            if (_mover != null) _mover.CancelPending();\n")


# ==================== A. 4039 波次中心（NetRoomFlow） ====================

E(RF,
  "        /// <summary>【双向】呼叫凯伊（点）⇒ 远端让本机凯伊走到同一点（放信标 / 就近救人）。</summary>\n"
  "        public static event Action<CallKaiMsg> OnCallKai;\n",

  "        /// <summary>【双向】呼叫凯伊（点）⇒ 远端让本机凯伊走到同一点（放信标 / 就近救人）。</summary>\n"
  "        public static event Action<CallKaiMsg> OnCallKai;\n"
  "\n"
  "        /// <summary>【成员侧】房主下发的追击波次中心（~2Hz）⇒ 本端只跟随，不自己算\"最近玩家\"。</summary>\n"
  "        public static event Action<WaveCenterMsg> OnWaveCenter;\n")

E(RF,
  "            MessageCenter.Register<CallKaiMsg>(CmdId.CallKaiNtf, HandleCallKaiNtf);\n"
  "        }\n",

  "            MessageCenter.Register<CallKaiMsg>(CmdId.CallKaiNtf, HandleCallKaiNtf);\n"
  "            MessageCenter.Register<WaveCenterMsg>(CmdId.WaveCenterNtf, HandleWaveCenterNtf);\n"
  "        }\n")

E(RF,
  "            MessageCenter.Unregister(CmdId.CallKaiNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n",

  "            MessageCenter.Unregister(CmdId.CallKaiNtf);\n"
  "            MessageCenter.Unregister(CmdId.WaveCenterNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n")

E(RF,
  "        private void HandleCallKaiNtf(CallKaiMsg m)\n"
  "        {\n"
  "            if (m != null) OnCallKai?.Invoke(m);\n"
  "        }\n",

  "        private void HandleCallKaiNtf(CallKaiMsg m)\n"
  "        {\n"
  "            if (m != null) OnCallKai?.Invoke(m);\n"
  "        }\n"
  "\n"
  "        /// <summary>【房主】下发追击波次的中心点（~2Hz 节流在 09 侧的 WaveManager 里）。</summary>\n"
  "        public void SendWaveCenter(int waveIndex, Vector3 center)\n"
  "        {\n"
  "            if (!IsHost) return;\n"
  "            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.WaveCenterNtf,\n"
  "                new WaveCenterMsg { WaveIndex = waveIndex, X = center.x, Y = center.y, Z = center.z }));\n"
  "        }\n"
  "\n"
  "        private void HandleWaveCenterNtf(WaveCenterMsg m)\n"
  "        {\n"
  "            if (m != null) OnWaveCenter?.Invoke(m);\n"
  "        }\n")


# ==================== A. 4039 波次中心（WaveManager） ====================

E(WM,
  "            // 联机：成员按房主的开波广播复刻同一波\n"
  "            FPSGame.Net.NetRoomFlow.OnWaveStart += ApplyRemoteWave;\n",

  "            // 联机：成员按房主的开波广播复刻同一波\n"
  "            FPSGame.Net.NetRoomFlow.OnWaveStart += ApplyRemoteWave;\n"
  "            // 联机：追击波次的中心由房主下发（成员不再自己算\"最近玩家\"，见 ApplyRemoteWaveCenter）\n"
  "            FPSGame.Net.NetRoomFlow.OnWaveCenter += ApplyRemoteWaveCenter;\n")

E(WM,
  "            FPSGame.Net.NetRoomFlow.OnWaveStart -= ApplyRemoteWave;\n",

  "            FPSGame.Net.NetRoomFlow.OnWaveStart -= ApplyRemoteWave;\n"
  "            FPSGame.Net.NetRoomFlow.OnWaveCenter -= ApplyRemoteWaveCenter;\n")

E(WM,
  "                WaveBase.PendingWaveIndex = ++waveSeq;\n"
  "                if (online) PublishWaveStart(param, waveSeq);   // ★ 房主：广播（成员按同一 waveSeq 复刻）\n"
  "            }\n",

  "                WaveBase.PendingWaveIndex = ++waveSeq;\n"
  "                if (online) PublishWaveStart(param, waveSeq);   // ★ 房主：广播（成员按同一 waveSeq 复刻）\n"
  "\n"
  "                // ★ 追击中心：房主是**唯一**判定方 —— \"离中心最近的玩家\"会因位姿延迟翻边，\n"
  "                //   各端自己算必然算出不同答案（2026-10-08 定案）。在房主的 centerGetter 外面包一层，\n"
  "                //   按 ~2Hz 把解析结果下发；成员只跟随（见 SmoothRemoteCenter）。\n"
  "                if (online && param.centerGetter != null)\n"
  "                {\n"
  "                    var inner = param.centerGetter;\n"
  "                    int wave = waveSeq;\n"
  "                    param.centerGetter = () =>\n"
  "                    {\n"
  "                        Vector3 c = inner();\n"
  "                        PublishWaveCenter(wave, c);\n"
  "                        return c;\n"
  "                    };\n"
  "                }\n"
  "            }\n")

E(WM,
  "            if (msg.ChaseCenter)\n"
  "            {\n"
  "                Vector3 c = param.center;\n"
  "                param.centerGetter = () => c = ActorsManager.NearestPlayerPos(c);\n"
  "            }\n",

  "            if (msg.ChaseCenter)\n"
  "            {\n"
  "                // ★ 追击中心：**不再自己算\"最近玩家\"**（位姿延迟会翻转答案 ⇒ 中心跳变）。\n"
  "                //   只跟随房主 ~2Hz 下发的中心，并在两包之间做插值平滑（见 ApplyRemoteWaveCenter / Tick）。\n"
  "                m_remoteCenterTarget = param.center;\n"
  "                m_remoteCenter = param.center;\n"
  "                param.centerGetter = SmoothRemoteCenter;\n"
  "            }\n")

E(WM,
  "        public override bool Tick()\n"
  "        {\n"
  "            return true;\n"
  "        }\n",

  "        /// <summary>房主下发中心的最小间隔（秒）：~2Hz 足够（成员做插值），不必跟 Tick 同频。</summary>\n"
  "        const float CenterSendInterval = 0.5f;\n"
  "\n"
  "        /// <summary>成员端\"跟随房主中心\"的最大速度（米/秒）：要把 2Hz 的台阶平滑掉，但不该瞬间贴过去。</summary>\n"
  "        const float RemoteCenterFollowSpeed = 24f;\n"
  "\n"
  "        float m_lastCenterSend = Mathf.NegativeInfinity;\n"
  "\n"
  "        /// <summary>房主下发的追击中心（\"目标值\"，由 <see cref=\"ApplyRemoteWaveCenter\"/> 写入）。</summary>\n"
  "        Vector3 m_remoteCenterTarget;\n"
  "        /// <summary>成员端实际交出去的追击中心（向 <see cref=\"m_remoteCenterTarget\"/> 平滑逼近）。</summary>\n"
  "        Vector3 m_remoteCenter;\n"
  "        /// <summary>是否收到过房主的中心（没收到前用开波包里的 center 兜底）。</summary>\n"
  "        bool m_hasRemoteCenter;\n"
  "\n"
  "        /// <summary>【房主】下发追击中心（~2Hz 节流）。由包在 <c>centerGetter</c> 外层的那个委托调用。</summary>\n"
  "        void PublishWaveCenter(int waveIndex, Vector3 center)\n"
  "        {\n"
  "            if (Time.time < m_lastCenterSend + CenterSendInterval) return;\n"
  "            m_lastCenterSend = Time.time;\n"
  "            FPSGame.Net.NetRoomFlow.Instance?.SendWaveCenter(waveIndex, center);\n"
  "        }\n"
  "\n"
  "        /// <summary>【成员】收到房主的追击中心。\n"
  "        /// <para>⚠ 按 <c>WaveCenterMsg.WaveIndex</c> 丢过期包：迟到的上一波中心会把这一波拽回去。</para></summary>\n"
  "        void ApplyRemoteWaveCenter(FPSGame.Net.WaveCenterMsg msg)\n"
  "        {\n"
  "            var flow = FPSGame.Net.NetRoomFlow.Instance;\n"
  "            if (msg == null || flow == null || flow.IsHost) return;\n"
  "            if (msg.WaveIndex != waveSeq) return;                  // 过期 / 未来包\n"
  "\n"
  "            m_remoteCenterTarget = new Vector3(msg.X, msg.Y, msg.Z);\n"
  "            if (!m_hasRemoteCenter)\n"
  "            {\n"
  "                m_hasRemoteCenter = true;\n"
  "                m_remoteCenter = m_remoteCenterTarget;             // 第一包直接贴上，不要从旧的猜值慢慢挪\n"
  "            }\n"
  "        }\n"
  "\n"
  "        /// <summary>【成员】把中心平滑逼近房主给的目标值。包在 <c>centerGetter</c> 里，被波次每 Tick 调用一次。</summary>\n"
  "        Vector3 SmoothRemoteCenter()\n"
  "        {\n"
  "            if (m_hasRemoteCenter)\n"
  "                m_remoteCenter = Vector3.MoveTowards(m_remoteCenter, m_remoteCenterTarget, RemoteCenterFollowSpeed * Time.deltaTime);\n"
  "            return m_remoteCenter;\n"
  "        }\n"
  "\n"
  "        public override bool Tick()\n"
  "        {\n"
  "            return true;\n"
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
