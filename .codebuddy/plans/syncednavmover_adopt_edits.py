# -*- coding: utf-8 -*-
"""
把 NPCWalk / SpecUnitController 接入 SyncedNavMover：
- SyncedNavMover：默认落地判据补上 isOnNavMesh（NPCWalk 的老判据，避免"不在网格上 SetDestination"报错）
- NPCWalk：删掉自己的 _pendingDestination/_hasPendingDestination/TryApplyPendingDestination，改用组件
- SpecUnitController：SetNavDestination 改走组件（获得"缓存+重试"，旧实现是失败即丢）

每个 (old,new) 必须在该文件里恰好命中 1 次，否则整脚本中止、不落盘。行尾自适应。
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MV = os.path.join(ROOT, "Assets", "Scripts", "06Gameplay", "AI", "SyncedNavMover.cs")
NPC = os.path.join(ROOT, "Assets", "Scripts", "06Gameplay", "Npc", "NPCWalk.cs")
SPC = os.path.join(ROOT, "Assets", "Scripts", "06Gameplay", "Npc", "SpecUnitController.cs")

EDITS = []


def E(path, old, new):
    EDITS.append((path, old, new))


# ---------- SyncedNavMover：默认判据补 isOnNavMesh ----------
E(MV,
  "            if (_agent == null) return true;                  // 没有 agent（飞行/纯逻辑单位）：算处理完\n"
  "            if (!_agent.isActiveAndEnabled) return false;     // 禁用中：等启用后重试\n"
  "            _agent.SetDestination(destination);\n",

  "            if (_agent == null) return true;                  // 没有 agent（飞行/纯逻辑单位）：算处理完\n"
  "            if (!_agent.isActiveAndEnabled) return false;     // 禁用中：等启用后重试\n"
  "            if (!_agent.isOnNavMesh) return false;            // 不在网格上 SetDestination 会报错 ⇒ 等就位\n"
  "            _agent.SetDestination(destination);\n")


# ---------- NPCWalk：using ----------
E(NPC,
  "using System.Collections;\n"
  "using System.Collections.Generic;\n"
  "using FPSGame.Game;\n"
  "using UnityEngine;\n"
  "using UnityEngine.AI;\n",

  "using System.Collections;\n"
  "using System.Collections.Generic;\n"
  "using FPSGame.AI;\n"
  "using FPSGame.Game;\n"
  "using UnityEngine;\n"
  "using UnityEngine.AI;\n")

# ---------- NPCWalk：字段 ----------
E(NPC,
  "    /// <summary>网络下发但**当帧应用不了**的目标点（agent 未就绪时暂存，<see cref=\"Update\"/> 里补）。</summary>\n"
  "    private Vector3 _pendingDestination;\n"
  "    private bool _hasPendingDestination;\n",

  "    /// <summary>联机落点组件（2026-10-08 接入）：网络目标点的缓存 / 重试全在它里面。\n"
  "    /// <para>⚠ 本类的落地通道**不能**走 <c>UnitEventBus.PathRequest</c>（<c>PathRequestManager</c> 由战场的\n"
  "    /// <c>BattleManager</c> 创建，大厅里没人订阅 ⇒ 请求会被静默丢弃、NPC 一动不动）\n"
  "    /// ⇒ **不注入 applyHandler**，用组件的默认实现（<c>NavMeshAgent.SetDestination</c>，未就绪时自己等）。</para></summary>\n"
  "    private SyncedNavMover _mover;\n"
  "\n"
  "    private SyncedNavMover Mover\n"
  "    {\n"
  "        get\n"
  "        {\n"
  "            if (_mover == null)\n"
  "            {\n"
  "                if (!TryGetComponent(out _mover)) _mover = gameObject.AddComponent<SyncedNavMover>();\n"
  "                // 故意不设 applyHandler：见上面注释（大厅里没有 PathRequestManager）\n"
  "            }\n"
  "            return _mover;\n"
  "        }\n"
  "    }\n")

# ---------- NPCWalk：Start ----------
E(NPC,
  "        TryApplyPendingDestination();\n"
  "        StartWandering();\n",

  "        // ⚠ \"补发早到的远端目标点\"由 SyncedNavMover 自己的 Update 负责（2026-10-08），这里不再驱动\n"
  "        StartWandering();\n")

# ---------- NPCWalk：Update ----------
E(NPC,
  "        // 补发\"早到 / 当时 agent 不可用\"的远端目标点\n"
  "        TryApplyPendingDestination();\n"
  "\n"
  "        // 每一帧检测是否在移动，更新动画\n",

  "        // ⚠ \"补发远端目标点\"已挪进 SyncedNavMover 自己的 Update（2026-10-08）\n"
  "\n"
  "        // 每一帧检测是否在移动，更新动画\n")

# ---------- NPCWalk：ApplyRemoteMove + 删 TryApplyPendingDestination ----------
E(NPC,
  "        if (stop)\n"
  "        {\n"
  "            _hasPendingDestination = false;\n"
  "            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();\n"
  "            return;\n"
  "        }\n"
  "\n"
  "        _pendingDestination = destination;\n"
  "        _hasPendingDestination = true;\n"
  "        TryApplyPendingDestination();\n"
  "    }\n"
  "\n"
  "    /// <summary>agent 真正可用时才落点。\n"
  "    /// <para>⚠ 这里**不能**走 <c>UnitEventSub.PathRequest</c>（敌人那条统一漏斗）：<c>PathRequestManager</c> 由战场的\n"
  "    /// <c>BattleManager</c> 创建，大厅里没人订阅 ⇒ 请求会被静默丢弃、NPC 一动不动。</para></summary>\n"
  "    private void TryApplyPendingDestination()\n"
  "    {\n"
  "        if (!_hasPendingDestination || agent == null) return;\n"
  "        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;\n"
  "\n"
  "        agent.SetDestination(_pendingDestination);\n"
  "        _hasPendingDestination = false;\n"
  "    }\n",

  "        if (stop)\n"
  "        {\n"
  "            // 清待落地点 + ResetPath（组件内部已判 \"agent 未就绪\"）\n"
  "            Mover.Stop();\n"
  "            return;\n"
  "        }\n"
  "\n"
  "        Mover.ApplyRemoteDestination(destination);   // 缓存 + 立即尝试 + 失败则每帧重试\n"
  "    }\n")

# ---------- NPCWalk：PauseWandering ----------
E(NPC,
  "        if (RemoteDriven) return;      // 成员：位置的权威在房主（他暂停时会发 stop），本地别动 agent\n"
  "        if (agent != null && agent.isActiveAndEnabled)\n"
  "            agent.ResetPath();\n",

  "        if (RemoteDriven) return;      // 成员：位置的权威在房主（他暂停时会发 stop），本地别动 agent\n"
  "        Mover.Stop();                  // 顺带清掉待落地点：否则\"暂停\"会被下一帧的补发顶掉\n")

# ---------- NPCWalk：StopWandering ----------
E(NPC,
  "            StopCoroutine(wanderCoroutine);\n"
  "            wanderCoroutine = null;\n"
  "        }\n"
  "        if (RemoteDriven) return;\n"
  "        if (agent != null && agent.isActiveAndEnabled)\n"
  "            agent.ResetPath();\n",

  "            StopCoroutine(wanderCoroutine);\n"
  "            wanderCoroutine = null;\n"
  "        }\n"
  "        if (RemoteDriven) return;\n"
  "        Mover.Stop();\n")

# ---------- NPCWalk：本地游荡决策也走组件 ----------
E(NPC,
  "            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))\n"
  "            {\n"
  "                agent.SetDestination(hit.position);\n"
  "                PublishMove(hit.position, false);\n"
  "            }\n",

  "            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))\n"
  "            {\n"
  "                Mover.SetDestination(hit.position);   // 本端决策也走组件：agent 未就绪时先存着、就绪后再落地\n"
  "                PublishMove(hit.position, false);\n"
  "            }\n")


# ---------- SpecUnitController：字段 + SetNavDestination ----------
E(SPC,
  "        /// <summary>\n"
  "        /// 设置目标点\n"
  "        /// </summary>\n"
  "        /// <param name=\"destination\"></param>\n"
  "        public void SetNavDestination(Vector3 destination)\n"
  "        {\n"
  "            if (NavMeshAgent)\n"
  "            {\n"
  "                NavMeshAgent.SetDestination(destination);\n"
  "            }\n"
  "        }\n",

  "        /// <summary>联机落点组件（2026-10-08 接入）：目标点的缓存 / 重试在它里面。</summary>\n"
  "        private SyncedNavMover _mover;\n"
  "\n"
  "        private SyncedNavMover Mover\n"
  "        {\n"
  "            get\n"
  "            {\n"
  "                if (_mover == null)\n"
  "                {\n"
  "                    if (!TryGetComponent(out _mover)) _mover = gameObject.AddComponent<SyncedNavMover>();\n"
  "                    // 特殊单位在大厅也要能动 ⇒ 不注入 applyHandler（默认 = NavMeshAgent.SetDestination）\n"
  "                }\n"
  "                return _mover;\n"
  "            }\n"
  "        }\n"
  "\n"
  "        /// <summary>\n"
  "        /// 设置目标点\n"
  "        /// <para>▍对凯伊为什么是真修复：它的落点是**网络上重放**过来的（4038 <c>CallKai</c>），\n"
  "        /// 而重放那一刻这只凯伊可能还没 Warp 上导航网格 ⇒ 旧实现 <c>SetDestination</c> 静默失败、\n"
  "        /// 这条呼叫就没了；现在会先存下来，等 agent 就绪再落地。</para>\n"
  "        /// </summary>\n"
  "        /// <param name=\"destination\"></param>\n"
  "        public void SetNavDestination(Vector3 destination)\n"
  "        {\n"
  "            Mover.SetDestination(destination);\n"
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
