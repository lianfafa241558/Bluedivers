using System.Collections.Generic;
using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机战斗同步桥（09）：把玩法层（06）的"移动意图 / 命中"送上网，把网络下发的"意图 / 死亡"落回玩法层。
    /// 每局一份，由 <see cref="WaveManager"/> 在 Awake/OnDestroy 里 Install/Uninstall。
    ///
    /// <para>▍分工：**房主**是意图与生死的权威（发移动目标、结算伤害、广播死亡）；成员只做三件事 ——
    /// 应用房主给的目标点（本地各自算路径）、上报自己的命中、按死亡广播干掉副本。</para>
    /// </summary>
    public static class EnemyNetBridge
    {
        static bool installed;

        /// <summary>NetId → 还没落到单位身上的移动目标（"消息比副本先到"时暂存，见 <see cref="OnRemoteMove"/>）。</summary>
        static readonly Dictionary<int, Vector3> _pendingMoves = new Dictionary<int, Vector3>();

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _pendingMoves.Clear();

            BattleEventSub.OnEnemyMove += OnLocalMove;
            BattleEventSub.OnEnemyHit += OnLocalHit;
            UnitEventSub.OnEnemyDead += OnLocalDeath;
            UnitEventSub.OnEnemyCreate += OnEnemyCreated;

            FPSGame.Net.NetRoomFlow.OnEnemyMove += OnRemoteMove;
            FPSGame.Net.NetRoomFlow.OnEnemyHitUp += OnRemoteHit;
            FPSGame.Net.NetRoomFlow.OnEnemyDied += OnRemoteDied;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;
            _pendingMoves.Clear();

            BattleEventSub.OnEnemyMove -= OnLocalMove;
            BattleEventSub.OnEnemyHit -= OnLocalHit;
            UnitEventSub.OnEnemyDead -= OnLocalDeath;
            UnitEventSub.OnEnemyCreate -= OnEnemyCreated;

            FPSGame.Net.NetRoomFlow.OnEnemyMove -= OnRemoteMove;
            FPSGame.Net.NetRoomFlow.OnEnemyHitUp -= OnRemoteHit;
            FPSGame.Net.NetRoomFlow.OnEnemyDied -= OnRemoteDied;
        }

        #region 本端 → 网络

        static void OnLocalMove(int netId, Vector3 destination)
        {
            FPSGame.Net.NetRoomFlow.Instance?.SendEnemyMove(netId, destination);   // 内部判 IsHost
        }

        static void OnLocalHit(int netId, int damage)
        {
            FPSGame.Net.NetRoomFlow.Instance?.SendEnemyHit(netId, damage);         // 内部判"成员才发"
        }

        static void OnLocalDeath(Actor actor)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            if (actor == null || flow == null || actor.NetId == 0) return;
            flow.SendEnemyDied(actor.NetId);                                       // 内部判 IsHost
        }

        #endregion

        #region 网络 → 本端

        static void OnRemoteMove(int netId, Vector3 destination)
        {
            var enemy = Find(netId);
            if (enemy != null)
            {
                enemy.ApplyRemoteDestination(destination);
                return;
            }

            // 本端这份副本还没建出来（成员靠 WaveStartSync 才生成本波单位，比房主晚半步）⇒ 暂存。
            // ⚠ 不能直接丢：房主只在"目标变化"时发（<1m 去重），丢了就没有第二条 ⇒ 那只怪会一直原地不动。
            if (netId != 0) _pendingMoves[netId] = destination;
        }

        /// <summary>本端把这只怪建出来了 ⇒ 把"早到"的移动目标补上（同帧应用，不必等房主下一次下发）。</summary>
        static void OnEnemyCreated(Actor actor)
        {
            if (actor == null || _pendingMoves.Count == 0) return;
            if (!_pendingMoves.TryGetValue(actor.NetId, out Vector3 destination)) return;

            _pendingMoves.Remove(actor.NetId);
            var enemy = actor.GetComponent<EnemyController>();
            if (enemy != null) enemy.ApplyRemoteDestination(destination);
        }

        /// <summary>房主侧结算成员上报的命中。走正常伤害链 ⇒ 打出致死伤害时本端自己会进 OnEnemyDead，再广播死亡。</summary>
        static void OnRemoteHit(int netId, int damage)
        {
            var actor = FindActor(netId);
            if (actor == null || damage <= 0) return;

            var dmg = actor.GetComponent<IDamageable>();
            if (dmg == null) return;

            EnemyController.ApplyingRemoteDamage = true;
            try
            {
                dmg.InflictDamage(new DamagePacket
                {
                    Damage = (PEInt)damage,
                    // ⚠ DamageGroups 不能空（InflictDamage 见空就直接返回）⇒ 给一条中性的普通伤害成分
                    DamageGroups = new List<SKVP<DamageTypeEnum, float>> { new SKVP<DamageTypeEnum, float>(default, 1f) },
                    DamageSource = null,
                    NoSource = true,
                    Pos = actor.transform.position,
                });
            }
            finally { EnemyController.ApplyingRemoteDamage = false; }
        }

        static void OnRemoteDied(int netId)
        {
            var actor = FindActor(netId);
            if (actor == null) return;
            actor.Kill();        // 走正常死亡链：掉落/特效/波次账本都会跟着走
        }

        #endregion

        #region 查表

        static EnemyController Find(int netId)
        {
            var actor = FindActor(netId);
            return actor != null ? actor.GetComponent<EnemyController>() : null;
        }

        /// <summary>按 NetId 找本端那份副本（只在敌人表里找；波次单位与巡逻队都有 NetId）。</summary>
        static Actor FindActor(int netId)
        {
            if (netId == 0) return null;

            var list = ActorsManager.Enemys;
            for (int i = 0; i < list.Count; ++i)
            {
                if (list[i] is Actor a && a != null && a.NetId == netId) return a;
            }
            return null;
        }

        #endregion
    }
}
