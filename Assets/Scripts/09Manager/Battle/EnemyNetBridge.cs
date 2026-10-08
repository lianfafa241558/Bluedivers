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

            BattleEventBus.OnEnemyMove += OnLocalMove;
            BattleEventBus.OnEnemyHit += OnLocalHit;
            BattleEventBus.OnEnemyDamaged += OnLocalDamaged;
            UnitEventBus.OnEnemyDead += OnLocalDeath;
            UnitEventBus.OnEnemyCreate += OnEnemyCreated;

            FPSGame.Net.NetRoomFlow.OnEnemyMove += OnRemoteMove;
            FPSGame.Net.NetRoomFlow.OnEnemyHitUp += OnRemoteHit;
            FPSGame.Net.NetRoomFlow.OnEnemyDamaged += OnRemoteDamaged;
            FPSGame.Net.NetRoomFlow.OnEnemyDied += OnRemoteDied;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;
            _pendingMoves.Clear();

            BattleEventBus.OnEnemyMove -= OnLocalMove;
            BattleEventBus.OnEnemyHit -= OnLocalHit;
            BattleEventBus.OnEnemyDamaged -= OnLocalDamaged;
            UnitEventBus.OnEnemyDead -= OnLocalDeath;
            UnitEventBus.OnEnemyCreate -= OnEnemyCreated;

            FPSGame.Net.NetRoomFlow.OnEnemyMove -= OnRemoteMove;
            FPSGame.Net.NetRoomFlow.OnEnemyHitUp -= OnRemoteHit;
            FPSGame.Net.NetRoomFlow.OnEnemyDamaged -= OnRemoteDamaged;
            FPSGame.Net.NetRoomFlow.OnEnemyDied -= OnRemoteDied;
        }

        #region 本端 → 网络

        static void OnLocalMove(int netId, Vector3 destination)
        {
            var flow = FPSGame.Net.NetRoomFlow.Instance;
            FPSGame.Utils.NetSyncLog.AiLog("敌人移动·发", $"netId={netId} 目标={destination:F2} 是房主={(flow != null && flow.IsHost)} 有人在房={(flow != null && flow.SelfSid != 0u)}");
            flow?.SendEnemyMove(netId, destination);   // 内部判 IsHost
        }

        static void OnLocalHit(int netId, int damage)
        {
            FPSGame.Net.NetRoomFlow.Instance?.SendEnemyHit(netId, damage);         // 内部判"成员才发"
        }

        static void OnLocalDamaged(int netId, int damage)
        {
            FPSGame.Net.NetRoomFlow.Instance?.SendEnemyDamaged(netId, damage);     // 内部判"房主才发"
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
                FPSGame.Utils.NetSyncLog.AiLog("敌人移动·收", $"netId={netId} 目标={destination:F2} 副本=已找到 ⇒ 应用");
                enemy.ApplyRemoteDestination(destination);
                return;
            }

            // 本端这份副本还没建出来（成员靠 WaveStartSync 才生成本波单位，比房主晚半步）⇒ 暂存。
            // ⚠ 不能直接丢：房主只在"目标变化"时发（<1m 去重），丢了就没有第二条 ⇒ 那只怪会一直原地不动。
            if (netId != 0)
            {
                FPSGame.Utils.NetSyncLog.Warn("敌人移动·收", $"netId={netId} 目标={destination:F2} 本端还没有这份副本 ⇒ 暂存（等 OnEnemyCreate 补发）");
                _pendingMoves[netId] = destination;
            }
        }

        /// <summary>本端把这只怪建出来了 ⇒ 把"早到"的移动目标补上（同帧应用，不必等房主下一次下发）。</summary>
        static void OnEnemyCreated(Actor actor)
        {
            if (actor == null || _pendingMoves.Count == 0) return;
            if (!_pendingMoves.TryGetValue(actor.NetId, out Vector3 destination)) return;

            _pendingMoves.Remove(actor.NetId);
            var enemy = actor.GetComponent<EnemyController>();
            FPSGame.Utils.NetSyncLog.AiLog("敌人移动·补发", $"netId={actor.NetId} 目标={destination:F2} 本端该怪={actor.Id} 找到控制器={(enemy != null)}");
            if (enemy != null) enemy.ApplyRemoteDestination(destination);
        }

        /// <summary>房主侧结算成员上报的命中。走正常伤害链 ⇒ 打出致死伤害时本端自己会进 OnEnemyDead，再广播死亡。</summary>
        static void OnRemoteHit(uint sid, int netId, int damage)
        {
            ApplyRemoteDamage(sid, netId, damage);
        }

        /// <summary>成员侧应用房主下发的伤害（房主是血量权威）。</summary>
        static void OnRemoteDamaged(uint sid, int netId, int damage)
        {
            ApplyRemoteDamage(sid, netId, damage);
        }

        /// <summary>
        /// 把远端伤害落到本端那份副本上，并**当作一次正常命中**跑完整条链（受击特效 / 血条 / 仇恨 / 命中统计）。
        ///
        /// <para>▍为什么必须给 <c>DamageSource</c>：老实现是 <c>DamageSource=null + NoSource=true</c> ⇒ 那只怪
        /// **静默掉血** —— 对端屏幕上没有受击表现、血条也不刷新，看起来就是"这边命中、那边没命中"
        /// （2026-10-09 用户实测）。传上"开枪者那个单位的实例"后，走的正是本端自己挨打时的同一条链。</para>
        ///
        /// <para>⚠ 解析不到开枪者（盟友实例还没建出来 / 已离开）⇒ 退回静默结算：宁可不表现，也别报错。</para>
        /// </summary>
        static void ApplyRemoteDamage(uint attackerSid, int netId, int damage)
        {
            var actor = FindActor(netId);
            if (actor == null || damage <= 0) return;

            var dmg = actor.GetComponent<IDamageable>();
            if (dmg == null) return;

            var source = ResolveRemoteUser(attackerSid);

            FPSGame.Utils.NetSyncLog.AiLog("远端伤害落地", $"netId={netId} 伤害={damage} 开枪者sid={attackerSid} 本端实例={(source != null ? source.name : "<没解析到，将静默结算>")} 落点={actor.transform.position:F2}");
            EnemyController.ApplyingRemoteDamage = true;
            try
            {
                dmg.InflictDamage(new DamagePacket
                {
                    Damage = (PEInt)damage,
                    // ⚠ DamageGroups 不能空（InflictDamage 见空就直接返回）⇒ 给一条中性的普通伤害成分
                    DamageGroups = new List<SKVP<DamageTypeEnum, float>> { new SKVP<DamageTypeEnum, float>(default, 1f) },
                    DamageSource = source,
                    NoSource = source == null,
                    Pos = actor.transform.position,
                });
            }
            finally { EnemyController.ApplyingRemoteDamage = false; }
        }

        static NetFriendBridge _friendBridge;

        /// <summary>权威 sid → 本端那个玩家的实例（<c>0</c> = 房主）。解析不到给 null。</summary>
        static GameObject ResolveRemoteUser(uint sid)
        {
            // ⚠ NetFriendBridge 没有静态 Instance（挂在常驻 GameRoot 上的 I_GlobaManager）⇒ 惰性解析一次并缓存
            if (_friendBridge == null) _friendBridge = UnityEngine.Object.FindObjectOfType<NetFriendBridge>();
            return _friendBridge != null && _friendBridge.TryGetFriendObject(sid, out GameObject go) ? go : null;
        }

        static void OnRemoteDied(int netId)
        {
            var actor = FindActor(netId);
            if (actor == null) return;

            // ★ 先把血抹 0 再杀：本端这份副本的血量**不是权威**（可能还是满血），而 `Actor.Kill()` 的实现
            //   就是 `OnDie(null)`、**完全不碰血量** ⇒ `Actor.OnDie` 里那条"死亡时生命值>0"的守卫会误报
            //   （那条打印是给**真异常**用的，不该被这条正常同步路径刷）。
            //   ⚠ 改走 `IHealth.Kill()`：它内部先 `CurrentHealth = 0` 再 `HandleDeath(null)`，
            //   派发的还是同一个 `OnDie` ⇒ 掉落 / 特效 / 波次账本照常跟着走，只是血量已归零。
            var hp = actor.GetComponent<IHealth>();
            if (hp != null) hp.Kill();
            else actor.Kill();   // 没有生命组件（极少见）：退回原来的直接死
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
