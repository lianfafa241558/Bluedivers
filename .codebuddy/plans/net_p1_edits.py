# -*- coding: utf-8 -*-
"""
联机 P1：任务状态/进度同步（4035）。
- MissionBase：netOrder + RemoteDriven 门 + ApplyRemoteState
- MissionController：按创建顺序 / 按位置排序 赋 netOrder + FindByNetOrder
- NetRoomFlow(07)：事件 + 收发 + 注册/退订
- WaveManager：Install/Uninstall NetMissionBridge
- NetMissionBridge：Uninstall 复位 RemoteDriven

每个 (old,new) 必须在该文件里恰好命中 1 次，否则整脚本中止、不落盘。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MB = os.path.join(ROOT, "Assets", "Scripts", "06Gameplay", "Mission", "MissionBase.cs")
MC = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "MissionController.cs")
RF = os.path.join(ROOT, "Assets", "Scripts", "07NetGame", "NetRoomFlow.cs")
WM = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "WaveManager.cs")
NB = os.path.join(ROOT, "Assets", "Scripts", "09Manager", "Battle", "NetMissionBridge.cs")

EDITS = []


def E(path, old, new):
    EDITS.append((path, old, new))


# ---------- MissionBase：netOrder + RemoteDriven ----------
E(MB,
  "        //[HideInInspector]\n"
  "        public MissionView entity;\n"
  "        public bool end;\n"
  "        public bool completed;\n"
  "\n"
  "        private Transform entityParent;\n"
  "\n"
  "        public bool IsInitialized { get; set;}\n",

  "        //[HideInInspector]\n"
  "        public MissionView entity;\n"
  "        public bool end;\n"
  "        public bool completed;\n"
  "\n"
  "        /// <summary>跨端稳定键：由 <c>MissionController</c> 按创建顺序赋值（每局归零），联机同步用。\n"
  "        /// <para>⚠ 用属性而非字段 ⇒ 不参与序列化（不产生 prefab / .meta 变动）。</para></summary>\n"
  "        public int netOrder { get; set; }\n"
  "\n"
  "        /// <summary>成员侧 = true：任务**不自行推进 / 完成**，状态与进度一律以房主下发为准。\n"
  "        /// <para>由 09 侧的 <c>NetMissionBridge</c> 在 Install 时按角色置位（同 <c>EnemyController.RemoteDrivenMovement</c>）。</para></summary>\n"
  "        public static bool RemoteDriven;\n"
  "\n"
  "        /// <summary>远端应用中的放行标记（见 <see cref=\"ApplyRemoteState\"/>）：只影响\"是否被 <see cref=\"RemoteDriven\"/> 门住\"。</summary>\n"
  "        static bool _applyingRemote;\n"
  "\n"
  "        private Transform entityParent;\n"
  "\n"
  "        public bool IsInitialized { get; set;}\n")

# ---------- MissionBase：CompleteMission 门 ----------
E(MB,
  "        public virtual void CompleteMission()\n"
  "        {\n"
  "            data.complete = true;\n",

  "        public virtual void CompleteMission()\n"
  "        {\n"
  "            // 成员：完成与否由房主下发（ApplyRemoteState）⇒ 本端自判一律作废，避免双重触发\n"
  "            if (RemoteDriven && !_applyingRemote) return;\n"
  "\n"
  "            data.complete = true;\n")

# ---------- MissionBase：FailMission 门 ----------
E(MB,
  "        protected virtual void FailMission()\n"
  "        {\n"
  "            if (missionType == MissionType.Main) root.result = GameResult.Failure;\n",

  "        protected virtual void FailMission()\n"
  "        {\n"
  "            if (RemoteDriven && !_applyingRemote) return;   // 同 CompleteMission：成员不自判\n"
  "\n"
  "            if (missionType == MissionType.Main) root.result = GameResult.Failure;\n")

# ---------- MissionBase：TryAddProgress 门 + ApplyRemoteState ----------
E(MB,
  "        protected bool TryAddProgress()\n"
  "        {\n"
  "            if (completed) return true;\n"
  "            if (NowProgress < MaxProgress) ++NowProgress;\n"
  "            return NowProgress >= MaxProgress;\n"
  "        }\n",

  "        protected bool TryAddProgress()\n"
  "        {\n"
  "            // 成员：进度以房主为准（返回 false = \"尚未达成\"，安全，不会被上层当成\"已完成\"）\n"
  "            if (RemoteDriven && !_applyingRemote) return false;\n"
  "\n"
  "            if (completed) return true;\n"
  "            if (NowProgress < MaxProgress) ++NowProgress;\n"
  "            return NowProgress >= MaxProgress;\n"
  "        }\n"
  "\n"
  "        /// <summary>\n"
  "        /// 【联机】成员侧：按房主权威的\"状态 / 进度\"**直接置位**（不自行判定）。\n"
  "        /// <para>由 09 侧的 <c>NetMissionBridge</c> 收包后调用。内部短暂放行 <see cref=\"RemoteDriven\"/> 门，\n"
  "        /// 让 <c>CompleteMission/FailMission/UpdateMission</c> 走完同一条链（UI 刷新与收尾都复用）。</para>\n"
  "        /// <para>状态约定（与 <c>NetMissionBridge</c> 一致）：1=进行中 2=完成 3=失败。</para>\n"
  "        /// </summary>\n"
  "        public void ApplyRemoteState(int state, int progress, int maxProgress, float percentage)\n"
  "        {\n"
  "            if (end) return;                    // 已结束的不再改\n"
  "\n"
  "            MaxProgress = maxProgress;\n"
  "            NowProgress = progress;\n"
  "            this.percentage = percentage;\n"
  "\n"
  "            _applyingRemote = true;\n"
  "            try\n"
  "            {\n"
  "                if (state == 2) { if (!completed) CompleteMission(); return; }\n"
  "                if (state == 3) { FailMission(); return; }\n"
  "                UpdateMission();\n"
  "            }\n"
  "            finally\n"
  "            {\n"
  "                _applyingRemote = false;\n"
  "            }\n"
  "        }\n")

# ---------- MissionController：数据生成模式按创建顺序赋值 ----------
E(MC,
  "        go.Init(root, task, task.cfg.sprite, GenerateNewMissionPoint(size), size, EntityRoot);\n"
  "        while (!go.IsInitialized) yield return null;\n"
  "        missions.Add(go);\n"
  "        go.enabled = false;\n",

  "        go.Init(root, task, task.cfg.sprite, GenerateNewMissionPoint(size), size, EntityRoot);\n"
  "        while (!go.IsInitialized) yield return null;\n"
  "        // 联机稳定键：按创建顺序（本模式的顺序由本局配置 + 稳定排序决定 ⇒ 两端一致）\n"
  "        go.netOrder = missions.Count;\n"
  "        missions.Add(go);\n"
  "        go.enabled = false;\n")

# ---------- MissionController：场景模式按位置排序赋值 ----------
E(MC,
  "            missions.Add(mission);\n"
  "            mission.transform.parent = transform;\n"
  "            mission.enabled = false;\n"
  "            yield return null;\n"
  "        }\n"
  "\n"
  "        // 按 MissionEnum 分类（与 InitAllMission 的分类逻辑一致）\n",

  "            missions.Add(mission);\n"
  "            mission.transform.parent = transform;\n"
  "            mission.enabled = false;\n"
  "            yield return null;\n"
  "        }\n"
  "\n"
  "        // 联机稳定键：本模式走 FindObjectsByType，**顺序两端不保证一致** ⇒ 按世界坐标排序后再赋值\n"
  "        AssignNetOrdersByPosition(missions);\n"
  "\n"
  "        // 按 MissionEnum 分类（与 InitAllMission 的分类逻辑一致）\n")

# ---------- MissionController：两个辅助方法 ----------
E(MC,
  "    public void AddBattleDataItem(int playerIndex,string name)\n"
  "    {\n"
  "        //Debug.LogError(\"root\"+root);\n",

  "    /// <summary>\n"
  "    /// 【联机】按世界坐标（0.1m 量化）排序后赋 <c>netOrder</c>。\n"
  "    /// <para>用于 <see cref=\"MissionInitMode.FindFromScene\"/>：<c>FindObjectsByType</c> 的顺序两端不保证一致，\n"
  "    /// 而场景预置任务的位置由场景决定 ⇒ 位置才是跨端稳定的排序键（量化是为了吃掉浮点抖动）。</para>\n"
  "    /// <para>⚠ 只决定\"赋值顺序\"，**不改动 <paramref name=\"list\"/> 本身**（下游 main/subTask 分类依赖原顺序）。</para>\n"
  "    /// </summary>\n"
  "    static void AssignNetOrdersByPosition(List<MissionBase> list)\n"
  "    {\n"
  "        if (list == null || list.Count == 0) return;\n"
  "\n"
  "        var sorted = new List<MissionBase>(list);\n"
  "        sorted.Sort((a, b) =>\n"
  "        {\n"
  "            Vector3 pa = a.transform.position, pb = b.transform.position;\n"
  "            int c = Mathf.RoundToInt(pa.x * 10f).CompareTo(Mathf.RoundToInt(pb.x * 10f));\n"
  "            if (c != 0) return c;\n"
  "            c = Mathf.RoundToInt(pa.z * 10f).CompareTo(Mathf.RoundToInt(pb.z * 10f));\n"
  "            if (c != 0) return c;\n"
  "            return Mathf.RoundToInt(pa.y * 10f).CompareTo(Mathf.RoundToInt(pb.y * 10f));\n"
  "        });\n"
  "        for (int i = 0; i < sorted.Count; ++i) sorted[i].netOrder = i;\n"
  "    }\n"
  "\n"
  "    /// <summary>【联机】按 <c>netOrder</c> 找本地任务实例（任务量小，线性查找足够）。</summary>\n"
  "    public MissionBase FindByNetOrder(int order)\n"
  "    {\n"
  "        if (missions == null) return null;\n"
  "        for (int i = 0; i < missions.Count; ++i)\n"
  "        {\n"
  "            if (missions[i] != null && missions[i].netOrder == order) return missions[i];\n"
  "        }\n"
  "        return null;\n"
  "    }\n"
  "\n"
  "    public void AddBattleDataItem(int playerIndex,string name)\n"
  "    {\n"
  "        //Debug.LogError(\"root\"+root);\n")

# ---------- NetRoomFlow：事件 ----------
E(RF,
  "        /// <summary>【成员侧】房主宣告开始撤离（撤离点）⇒ 本地触发撤离。</summary>\n"
  "        public static event Action<EvacuateMsg> OnEvacuate;\n",

  "        /// <summary>【成员侧】房主宣告开始撤离（撤离点）⇒ 本地触发撤离。</summary>\n"
  "        public static event Action<EvacuateMsg> OnEvacuate;\n"
  "\n"
  "        /// <summary>【成员侧】房主下发任务状态 / 进度（Key = MissionBase.netOrder）⇒ 本地直接置位，不自行判定。</summary>\n"
  "        public static event Action<MissionUpdateMsg> OnMissionUpdate;\n")

# ---------- NetRoomFlow：注册 ----------
E(RF,
  "            MessageCenter.Register<GameOverMsg>(CmdId.GameOverNtf, HandleGameOverNtf);\n"
  "            MessageCenter.Register<EvacuateMsg>(CmdId.EvacuateNtf, HandleEvacuateNtf);\n"
  "        }\n",

  "            MessageCenter.Register<GameOverMsg>(CmdId.GameOverNtf, HandleGameOverNtf);\n"
  "            MessageCenter.Register<EvacuateMsg>(CmdId.EvacuateNtf, HandleEvacuateNtf);\n"
  "            MessageCenter.Register<MissionUpdateMsg>(CmdId.MissionUpdateNtf, HandleMissionUpdateNtf);\n"
  "        }\n")

# ---------- NetRoomFlow：退订 ----------
E(RF,
  "            MessageCenter.Unregister(CmdId.GameOverNtf);\n"
  "            MessageCenter.Unregister(CmdId.EvacuateNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n",

  "            MessageCenter.Unregister(CmdId.GameOverNtf);\n"
  "            MessageCenter.Unregister(CmdId.EvacuateNtf);\n"
  "            MessageCenter.Unregister(CmdId.MissionUpdateNtf);\n"
  "            if (ReferenceEquals(Instance, this)) Instance = null;\n")

# ---------- NetRoomFlow：收发 ----------
E(RF,
  "        private void HandleEvacuateNtf(EvacuateMsg m)\n"
  "        {\n"
  "            if (m != null) OnEvacuate?.Invoke(m);\n"
  "        }\n",

  "        private void HandleEvacuateNtf(EvacuateMsg m)\n"
  "        {\n"
  "            if (m != null) OnEvacuate?.Invoke(m);\n"
  "        }\n"
  "\n"
  "        /// <summary>【房主】广播某任务的状态 / 进度（去重由 09 侧的桥负责，见 NetMissionBridge.Broadcast）。</summary>\n"
  "        public void SendMissionUpdate(int key, int state, int progress, int maxProgress, float percentage)\n"
  "        {\n"
  "            if (!IsHost) return;\n"
  "            NetHostSvc.Instance?.SendToAll(MessageCenter.Pack(CmdId.MissionUpdateNtf,\n"
  "                new MissionUpdateMsg\n"
  "                {\n"
  "                    Key = key,\n"
  "                    State = state,\n"
  "                    Progress = progress,\n"
  "                    MaxProgress = maxProgress,\n"
  "                    Percentage = percentage,\n"
  "                }));\n"
  "        }\n"
  "\n"
  "        private void HandleMissionUpdateNtf(MissionUpdateMsg m)\n"
  "        {\n"
  "            if (m != null) OnMissionUpdate?.Invoke(m);\n"
  "        }\n")

# ---------- WaveManager：Install ----------
E(WM,
  "            EnemyNetBridge.Install();\n"
  "            NetGameFlowBridge.Install();      // 局内世界状态（本局结束 / 撤离）\n"
  "        }\n",

  "            EnemyNetBridge.Install();\n"
  "            NetGameFlowBridge.Install();      // 局内世界状态（本局结束 / 撤离）\n"
  "            NetMissionBridge.Install();       // 任务状态 / 进度（房主权威）\n"
  "        }\n")

# ---------- WaveManager：Uninstall ----------
E(WM,
  "            EnemyNetBridge.Uninstall();\n"
  "            NetGameFlowBridge.Uninstall();\n"
  "        }\n",

  "            EnemyNetBridge.Uninstall();\n"
  "            NetGameFlowBridge.Uninstall();\n"
  "            NetMissionBridge.Uninstall();\n"
  "        }\n")

# ---------- NetMissionBridge：退场复位 RemoteDriven ----------
E(NB,
  "            NetRoomFlow.OnMissionUpdate -= OnRemoteMissionUpdate;\n"
  "        }\n",

  "            NetRoomFlow.OnMissionUpdate -= OnRemoteMissionUpdate;\n"
  "\n"
  "            MissionBase.RemoteDriven = false;   // 退场复位：否则单机时任务会一直不自判、永远完不成\n"
  "        }\n")


def _conv(text, crlf):
    """把锚点里的 \\n 换成目标文件的行尾（各文件 EOL 不统一：MissionController 是 CRLF）。"""
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
