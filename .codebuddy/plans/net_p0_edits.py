# -*- coding: utf-8 -*-
"""
联机 P0：本局结束(4033) / 开始撤离(4034) 的桥接线。
- BattleManager：加 _gameOverRequested（防两个定时器）
- WaveManager：Install/Uninstall NetGameFlowBridge
- NetRoomFlow(07)：事件 + 收发 + 注册/退订

每个 (old,new) 必须在该文件里恰好命中 1 次，否则整脚本中止、不落盘。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BM = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "BattleManager.cs")
WM = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "WaveManager.cs")
RF = os.path.join(ROOT, "Assets", "Scripts", "07NetGame", "NetRoomFlow.cs")

EDITS = []


def E(path, old, new):
    EDITS.append((path, old, new))


# ---------- BattleManager：字段 ----------
E(BM,
  "    public WeatherSystem WeatherCont;\n"
  "\n"
  "    private UnitQueryGrid unitQueryGrid;\n",

  "    public WeatherSystem WeatherCont;\n"
  "\n"
  "    /// <summary>本局\"结束\"是否已经请求过。\n"
  "    /// <para>▍联机：房主广播(4033)与本地触发（团灭/撤离）可能并存 ⇒ 不加门会排两个定时器（两次加载场景）。\n"
  "    /// 本类由 <see cref=\"Creat\"/> 每局新建 ⇒ 不需要额外复位。</para></summary>\n"
  "    private bool _gameOverRequested;\n"
  "\n"
  "    private UnitQueryGrid unitQueryGrid;\n")

# ---------- BattleManager：EndGame 门 ----------
E(BM,
  "    public void EndGame(int delay,GameResult result= GameResult.Unknow)\n"
  "    {\n"
  "        if (result != GameResult.Unknow) TaskManager.Instance.nowTask.result = result;\n"
  "        //GlobalEventManager.Evacuate();\n"
  "        GameRoot.CreateTimer(() => {\n",

  "    public void EndGame(int delay,GameResult result= GameResult.Unknow)\n"
  "    {\n"
  "        if (result != GameResult.Unknow) TaskManager.Instance.nowTask.result = result;\n"
  "\n"
  "        // ★ 联机：本局只结束一次 —— 房主广播(4033)与本地触发可能并存，重复调用会排两个定时器。\n"
  "        //   这里只挡\"再排一次\"，**结果仍允许被补正**（如本地以 Unknow 结束、随后收到房主权威的 Victory）。\n"
  "        if (_gameOverRequested) return;\n"
  "        _gameOverRequested = true;\n"
  "\n"
  "        //GlobalEventManager.Evacuate();\n"
  "        GameRoot.CreateTimer(() => {\n")

# ---------- WaveManager：Install ----------
E(WM,
  "            FPSGame.AI.EnemyRandom.Clear();   // NetId 是本局重新分配的 ⇒ 上一局的随机流要丢\n"
  "            EnemyNetBridge.Install();\n"
  "        }\n",

  "            FPSGame.AI.EnemyRandom.Clear();   // NetId 是本局重新分配的 ⇒ 上一局的随机流要丢\n"
  "            EnemyNetBridge.Install();\n"
  "            NetGameFlowBridge.Install();      // 局内世界状态（本局结束 / 撤离）\n"
  "        }\n")

# ---------- WaveManager：Uninstall ----------
E(WM,
  "            FPSGame.Net.NetRoomFlow.OnWaveStart -= ApplyRemoteWave;\n"
  "            EnemyNetBridge.Uninstall();\n"
  "        }\n",

  "            FPSGame.Net.NetRoomFlow.OnWaveStart -= ApplyRemoteWave;\n"
  "            EnemyNetBridge.Uninstall();\n"
  "            NetGameFlowBridge.Uninstall();\n"
  "        }\n")

# ---------- NetRoomFlow：事件 ----------
E(RF,
  "        public static event Action<int> OnTransition;\n"
  "\n"
  "        /// <summary>连接 + 入房的总超时（秒）。超时按失败处理，避免 UI 一直转圈。</summary>\n",

  "        public static event Action<int> OnTransition;\n"
  "\n"
  "        /// <summary>【成员侧】房主宣告本局结束（结果 / 延迟）⇒ 本地走 BattleManager.EndGame。\n"
  "        /// <para>⚠ 接收端必须\"本局只结束一次\"（BattleManager 有门），否则会排两个定时器。</para></summary>\n"
  "        public static event Action<GameOverMsg> OnGameOver;\n"
  "\n"
  "        /// <summary>【成员侧】房主宣告开始撤离（撤离点）⇒ 本地触发撤离。</summary>\n"
  "        public static event Action<EvacuateMsg> OnEvacuate;\n"
  "\n"
  "        /// <summary>连接 + 入房的总超时（秒）。超时按失败处理，避免 UI 一直转圈。</summary>\n")

# ---------- NetRoomFlow：Awake 注册 ----------
E(RF,
  "            MessageCenter.Register<SceneActorSync>(CmdId.SceneActorReq, HandleSceneActorReq);\n"
  "            MessageCenter.Register<SceneActorSync>(CmdId.SceneActorNtf, HandleSceneActorNtf);\n"
  "        }\n",

  "            MessageCenter.Register<SceneActorSync>(CmdId.SceneActorReq, HandleSceneActorReq);\n"
  "            MessageCenter.Register<SceneActorSync>(CmdId.SceneActorNtf, HandleSceneActorNtf);\n"
  "            // 局内世界状态（本局结束 / 撤离）：房主广播，成员应用\n"
  "            MessageCenter.Register<GameOverMsg>(CmdId.GameOverNtf, HandleGameOverNtf);\n"
  "            MessageCenter.Register<EvacuateMsg>(CmdId.EvacuateNtf, HandleEvacuateNtf);\n"
  "        }\n")

# ---------- NetRoomFlow：OnDestroy 退订 ----------
E(RF,
  "            MessageCenter.Unregister(CmdId.SceneActorReq);\n"
  "            MessageCenter.Unregister(CmdId.SceneActorNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n",

  "            MessageCenter.Unregister(CmdId.SceneActorReq);\n"
  "            MessageCenter.Unregister(CmdId.SceneActorNtf);\n"
  "            MessageCenter.Unregister(CmdId.GameOverNtf);\n"
  "            MessageCenter.Unregister(CmdId.EvacuateNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n")

# ---------- NetRoomFlow：收发 ----------
E(RF,
  "        private void HandleSceneUnitMoveSync(SceneUnitMoveMsg m)\n"
  "        {\n"
  "            if (m != null && !string.IsNullOrEmpty(m.Id))\n"
  "                OnSceneUnitMove?.Invoke(m.Id, new Vector3(m.X, m.Y, m.Z), m.Stop);\n"
  "        }\n",

  "        private void HandleSceneUnitMoveSync(SceneUnitMoveMsg m)\n"
  "        {\n"
  "            if (m != null && !string.IsNullOrEmpty(m.Id))\n"
  "                OnSceneUnitMove?.Invoke(m.Id, new Vector3(m.X, m.Y, m.Z), m.Stop);\n"
  "        }\n"
  "\n"
  "        // ==================== 局内世界状态（本局结束 / 撤离） ====================\n"
  "\n"
  "        /// <summary>【房主】广播\"本局结束\"（⚠ 一局只广播一次，由 09 侧的桥保证）。</summary>\n"
  "        public void SendGameOver(int matchId, int delay, int result)\n"
  "        {\n"
  "            if (!IsHost) return;\n"
  "            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.GameOverNtf,\n"
  "                new GameOverMsg { MatchId = matchId, Delay = delay, Result = result }));\n"
  "        }\n"
  "\n"
  "        /// <summary>【房主】广播\"开始撤离\"（⚠ 一局只广播一次，由 09 侧的桥保证）。</summary>\n"
  "        public void SendEvacuate(float x, float y, float z)\n"
  "        {\n"
  "            if (!IsHost) return;\n"
  "            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.EvacuateNtf,\n"
  "                new EvacuateMsg { X = x, Y = y, Z = z }));\n"
  "        }\n"
  "\n"
  "        private void HandleGameOverNtf(GameOverMsg m)\n"
  "        {\n"
  "            if (m != null) OnGameOver?.Invoke(m);\n"
  "        }\n"
  "\n"
  "        private void HandleEvacuateNtf(EvacuateMsg m)\n"
  "        {\n"
  "            if (m != null) OnEvacuate?.Invoke(m);\n"
  "        }\n")


def main():
    problems = []
    for i, (path, old, new) in enumerate(EDITS):
        if not os.path.isfile(path):
            problems.append("EDIT#%d NOT FOUND: %s" % (i + 1, path))
            continue
        text = open(path, "r", encoding="utf-8", newline="").read()
        n = text.count(old)
        if n != 1:
            problems.append("EDIT#%d HIT=%d (expect 1) %s :: %s"
                            % (i + 1, n, os.path.basename(path), old.strip().splitlines()[0][:60]))
    if problems:
        print("=== ABORT，未落盘 ===")
        for p in problems:
            print("  " + p)
        return 1

    for i, (path, old, new) in enumerate(EDITS):
        text = open(path, "r", encoding="utf-8", newline="").read()
        open(path, "w", encoding="utf-8", newline="").write(text.replace(old, new, 1))
        print("OK  EDIT#%d  %s" % (i + 1, os.path.relpath(path, ROOT)))

    print("\n全部 %d 处改写完成" % len(EDITS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
