using System.Collections.Generic;
using FPSGame.AI;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>巡逻队目标点语义（＝ <c>HostSpawnMsg.Sub</c> 的取值范围；只有 Kind = Patrol 用）。</summary>
    public enum PatrolTargetKind
    {
        /// <summary>逐单位目标点（<c>EnemyController.PatrolPos</c>，据点压力巡逻）。</summary>
        PatrolPos = 0,
        /// <summary>全体同一处（<c>EnemyController.HomePoint</c>，任务维修巡逻）。</summary>
        HomePoint = 1,
    }

    /// <summary>
    /// 联机 · **房主生成族**的统一同步桥（09，2026-10-12 由 <c>NetPatrolBridge</c> + <c>NetEnemySpawnBridge</c> 合并）。
    ///
    /// <para>▍为什么合并：两条通道的前提**完全同型** —— 都是"房主是唯一的生成权威"，都是因为 <c>Actor.Awake</c> 会领 NetId
    /// （号的口径是"房主分配 + 两端创建顺序一致"；成员端自造必然错位 ⇒ 之后房主下发的移动/伤害消息会作用到**别的单位**上）。
    /// 合并后：一条 <c>HostSpawnMsg</c>、一处回环门、一处日志口径，差异只在 <c>Kind</c>。</para>
    ///
    /// <para>▍Kind = Patrol：<c>WaveManager.CreatPatrol</c> 按**落点位置**派生随机流 ⇒ 同落点 ⇒ 同构成同顺序，
    /// 所以只传生成点 + **逐单位目标点**（后者由房主侧的几何/玩家位置算出，成员算不出同一个值）。</para>
    ///
    /// <para>▍Kind = Unit：运行时生成（巢穴 / 召唤师 / 刷怪器 / 环绕装甲）**不传 prefab**（过不了网），只传
    /// **来源键 + 槽位**：成员拿来源键找回本端同一份生成器、按槽位取同一把 prefab 复刻，号由消息覆盖
    /// （见 <see cref="INetSpawner"/>）。</para>
    ///
    /// <para>▍入口三个：09 自己的 <c>PatrolContriller</c> 直接调 <see cref="ReportPatrol"/>；06 的任务（看不见 09）
    /// 经 <c>BattleEventBus.ReportPatrolSpawn</c> 中转（见 <see cref="OnLocalPatrolReport"/>）；
    /// 运行时生成由 <c>WaveManager.CreatUnit</c> 调 <see cref="ReportUnit"/>。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> 在 Awake/OnDestroy 里 Install/Uninstall。</para>
    /// </summary>
    public static class NetHostSpawnBridge
    {
        static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;

            FPSGame.Net.NetRoomFlow.OnHostSpawn += OnRemoteSpawn;
            BattleEventBus.OnPatrolSpawnReport += OnLocalPatrolReport;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            FPSGame.Net.NetRoomFlow.OnHostSpawn -= OnRemoteSpawn;
            BattleEventBus.OnPatrolSpawnReport -= OnLocalPatrolReport;
        }

        #region 本端 -> 网络（只有房主发）

        /// <summary>【房主】报告一支巡逻队已生成（成员端据此复刻）。非房主 / 单机调用直接返回。</summary>
        public static void ReportPatrol(Vector3 spawnPos, IList<Vector3> targets, PatrolTargetKind kind)
        {
            if (targets == null || targets.Count == 0) return;

            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost) return;

            var arr = new float[targets.Count * 3];
            for (int i = 0; i < targets.Count; ++i)
            {
                arr[i * 3] = targets[i].x;
                arr[i * 3 + 1] = targets[i].y;
                arr[i * 3 + 2] = targets[i].z;
            }

            FPSGame.Utils.NetSyncLog.SyncLog("生成/发·巡逻",
                $"生成点={spawnPos:F1} 单位={targets.Count} kind={kind}");

            flow.SendHostSpawn(new FPSGame.Net.HostSpawnMsg
            {
                Kind = (int)FPSGame.Net.HostSpawnKind.Patrol,
                X = spawnPos.x, Y = spawnPos.y, Z = spawnPos.z,
                Sub = (int)kind,
                Targets = arr,
            });
        }

        /// <summary>【房主】上报"我刚生成了一只单位"（非房主 / 单机 / 没号的单位直接返回）。
        /// <para>⚠ 调用点只有一处：<c>WaveManager.CreatUnit</c> 的统一生成入口。</para></summary>
        public static void ReportUnit(INetSpawner source, int slot, GameObject spawned, Quaternion rot, Vector3 target)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost || source == null || spawned == null) return;

            var actor = spawned.GetComponent<Actor>();
            if (actor == null || actor.NetId == 0)
            {
                // 没号 = 对端不会认这只单位（这一发不会发出去，别让它悄悄消失）
                FPSGame.Utils.NetSyncLog.Warn("生成/发·单位",
                    $"生成的单位没有号（Actor={(actor != null)}）⇒ 对端不会有这只：{spawned.name}");
                return;
            }

            var pos = spawned.transform.position;
            FPSGame.Utils.NetSyncLog.SyncLog("生成/发·单位",
                $"来源键={source.SpawnSourceKey} 槽位={slot} 号={actor.NetId} 单位={actor.Id} 位置={pos:F1} 目标={target:F1}");

            flow.SendHostSpawn(new FPSGame.Net.HostSpawnMsg
            {
                Kind = (int)FPSGame.Net.HostSpawnKind.Unit,
                SourceKey = source.SpawnSourceKey,
                Slot = slot,
                NetId = actor.NetId,
                X = pos.x, Y = pos.y, Z = pos.z,
                Yaw = rot.eulerAngles.y,          // 单位只用水平朝向（俯仰由 AI/瞄准点决定）
                Tx = target.x, Ty = target.y, Tz = target.z,
                PrefabId = actor.Id,              // 只有"按类型抽表"的刷怪器会用到它
            });
        }

        /// <summary>06 侧（任务）的巡逻队报告入口：任务看不见 09 ⇒ 经 <c>BattleEventBus</c> 中转到这里统一走房主权威。</summary>
        static void OnLocalPatrolReport(Vector3 spawnPos, Vector3 target, bool home)
            => ReportPatrol(spawnPos, new[] { target }, home ? PatrolTargetKind.HomePoint : PatrolTargetKind.PatrolPos);

        #endregion

        #region 网络 -> 本端

        static void OnRemoteSpawn(FPSGame.Net.HostSpawnMsg m)
        {
            if (m == null) return;

            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (flow == null || flow.IsHost) return;      // 房主自己那份上面已经建过（不做本地自派发）

            if (m.Kind == (int)FPSGame.Net.HostSpawnKind.Patrol) ReplayPatrol(m);
            else ReplayUnit(m);
        }

        static void ReplayPatrol(FPSGame.Net.HostSpawnMsg m)
        {
            var battle = BattleManager.Instance;
            var wave = battle != null ? battle.WaveCont : null;
            if (wave == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("生成/收·巡逻", "本端没有 WaveManager（不在战斗场景？）⇒ 这支巡逻队被丢弃");
                return;
            }

            var spawnPos = new Vector3(m.X, m.Y, m.Z);
            var units = wave.CreatPatrolRemote(spawnPos);
            if (units == null || units.Count == 0)
            {
                FPSGame.Utils.NetSyncLog.Warn("生成/收·巡逻", $"生成点={spawnPos:F1} 复刻出 0 个单位（构成对不上？）");
                return;
            }

            var kind = (PatrolTargetKind)m.Sub;
            var arr = m.Targets;
            int groups = (arr != null) ? arr.Length / 3 : 0;

            for (int i = 0; i < units.Count; ++i)
            {
                var go = units[i];
                if (go == null || !go.TryGetComponent(out EnemyController c)) continue;

                Vector3 target;
                if (groups <= 0) target = spawnPos;
                else
                {
                    // HomePoint：全体同一处（取第 0 组）；PatrolPos：逐单位（不足时退回第 0 组，别越界）
                    int gi = (kind == PatrolTargetKind.HomePoint || i >= groups) ? 0 : i;
                    target = new Vector3(arr[gi * 3], arr[gi * 3 + 1], arr[gi * 3 + 2]);
                }

                if (kind == PatrolTargetKind.HomePoint) c.HomePoint = target;
                else c.PatrolPos = target;
            }

            FPSGame.Utils.NetSyncLog.SyncLog("生成/收·巡逻",
                $"生成点={spawnPos:F1} 单位={units.Count} kind={kind}");
        }

        static void ReplayUnit(FPSGame.Net.HostSpawnMsg m)
        {
            var spawner = SpawnerRegistry.Find(m.SourceKey);
            if (spawner == null)
            {
                FPSGame.Utils.NetSyncLog.Warn("生成/收·单位",
                    $"来源键={m.SourceKey} 在本端找不到生成器（槽位={m.Slot} 号={m.NetId}）⇒ 这只单位在本端不存在");
                return;
            }

            var pos = new Vector3(m.X, m.Y, m.Z);
            var target = new Vector3(m.Tx, m.Ty, m.Tz);

            GameObject go = null;
            // ★ 复刻期间**不许发号**：号由本条覆盖（见 Actor.Awake / ActorsManager.Replicating）
            ActorsManager.Replicating = true;
            try { go = spawner.ReplaySpawn(m.Slot, m.NetId, pos, Quaternion.Euler(0f, m.Yaw, 0f), target, m.PrefabId); }
            finally { ActorsManager.Replicating = false; }

            if (go == null)
            {
                var comp = spawner as Component;
                var gen = comp != null ? comp.name : "<无>";
                // ★ 挂起 ≠ 失败：生成器在"前置对象还没建出来"时会先记账再返回 null（例：火车炮塔等车厢建出来）
                if (spawner.HasPending)
                {
                    FPSGame.Utils.NetSyncLog.SyncLog("生成/收·单位",
                        $"来源键={m.SourceKey} 槽位={m.Slot} 号={m.NetId} prefabId={m.PrefabId} ⇒ 已挂起，等前置对象（生成器={gen}）");
                    return;
                }
                FPSGame.Utils.NetSyncLog.Warn("生成/收·单位",
                    $"来源键={m.SourceKey} 槽位={m.Slot} 号={m.NetId} prefabId={m.PrefabId} 复刻失败（生成器={gen}）");
                return;
            }

            FPSGame.Utils.NetSyncLog.SyncLog("生成/收·单位",
                $"来源键={m.SourceKey} 槽位={m.Slot} 号={m.NetId} prefabId={m.PrefabId} 单位={go.name} 位置={pos:F1} 目标={target:F1}");
        }

        #endregion
    }
}
