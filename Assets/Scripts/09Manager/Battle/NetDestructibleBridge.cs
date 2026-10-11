using System.Collections.Generic;
using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Net;
using FPSGame.Utils;
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **场景可破坏物**（油桶这类"摆在场上的物件"）同步桥（09）：一处收口"被打掉"这件事。
    ///
    /// <para>▍为什么需要：敌人的生死有 <see cref="EnemyNetBridge"/>（NetId 通道），玩家/盟友有 sid 通道，
    /// 家具交互有 4036；**"摆在场上的可破坏物"一条都没有** ⇒ 客机把油桶打爆、房主那边还是完好
    /// （2026-10-10 用户实测：客机命中日志有、房主那边一点反应都没有）。</para>
    ///
    /// <para>▍口径（2026-10-12 第 8 步升级）：**有号的物件走统一伤害通道**（4025 上报 / 4040 下发，键 = <c>Actor.NetId</c>）
    /// —— 战斗前创建的 <c>Other</c> 现在有派生号，所以"打到一半的血量/受击表现"两端也能一致；
    /// **4043（只传"死了"）保留为兜底**：没号的物件（战斗中途生成）、以及"有号但没走生死通道"的场合，
    /// 仍靠它 + 房主侧 <c>_destroyed</c> 账本（新人入房补发）。谁打死的谁上报，房主收到后**转发全体 + 本地应用**。</para>
    ///
    /// <para>▍4043 的匹配键仍是 <c>FNV1a(Actor.Id + 位置 0.1m 量化)</c>（同 <c>Furniture_Attached.SyncId</c> 口径）：
    /// 无号物件只能这么认；同类物件多实例（油桶全叫 <c>OilDrum</c>）⇒ 只用 <c>Actor.Id</c> 会撞车、用 <c>IndexID</c> 两端不一致。
    /// ⚠ 键必须稳定 ⇒ 会位移的物件不适用（油桶这类静止物件正好合适）。</para>
    ///
    /// <para>▍范围：只认 <c>UnitTypeEnum.Other</c>（场景摆好的物件）。⚠ 不含 <c>SpecUnit</c> 特殊单位 ——
    /// 那些带各自的任务逻辑，先不扩大范围（要放宽只需改 <see cref="IsSceneDestructible"/>）。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> Install/Uninstall（同 <see cref="EnemyNetBridge"/> 模式）。</para>
    /// </summary>
    public static class NetDestructibleBridge
    {
        static bool installed;

        /// <summary>回环门：true = 本端正在"重放远端那次打爆"。此时本地死亡事件**不许再上报**（否则两端互相转发）。</summary>
        static bool _applyingRemote;

        /// <summary>已打掉的键（房主侧账本）：新人入房时补发一次 —— 否则他加载出来的是"一个没炸"的场景。</summary>
        static readonly HashSet<int> _destroyed = new HashSet<int>();

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _applyingRemote = false;
            _destroyed.Clear();

            UnitEventBus.OnUnitDeath += OnLocalDeath;
            UnitEventBus.OnUnitDamaged += OnLocalDamaged;
            NetRoomFlow.OnSceneDestructible += OnRemoteDestroyed;
            NetRoomFlow.OnPlayerList += OnPlayerList;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            UnitEventBus.OnUnitDeath -= OnLocalDeath;
            UnitEventBus.OnUnitDamaged -= OnLocalDamaged;
            NetRoomFlow.OnSceneDestructible -= OnRemoteDestroyed;
            NetRoomFlow.OnPlayerList -= OnPlayerList;

            _destroyed.Clear();
            _applyingRemote = false;
        }

        #region 本端 -> 网络

        static void OnLocalDeath(Actor actor)
        {
            if (_applyingRemote) return;                  // 远端那次重放触发的：别再报一次
            // ★ 远端伤害（4025/4040）把本端副本打死的：那是"应用伤害"的副作用，不算本端打爆 ⇒ 不必再报 4043
            //   （报出去也不出错，但纯属噪音：对面已从伤害通道知道这件事）
            if (EnemyController.ApplyingRemoteDamage) return;
            if (!IsSceneDestructible(actor)) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;     // 单机 / 不在房里：两端本来就一致（同一台机器）

            int key = KeyOf(actor);
            if (key == 0) return;

            _destroyed.Add(key);

            var p = actor.transform.position;
            NetSyncLog.BulletLog("可破坏物·发", $"{actor.ShowName}({actor.Id}) key={key} 位置={p:F2} 是房主={flow.IsHost}");

            flow.SendSceneDestructible(key, flow.IsHost ? 0u : flow.SelfSid);
        }

        /// <summary>【联机】可破坏物**被打掉血** ⇒ 走统一伤害通道（成员 4025 上报 / 房主 4040 下发，键 = <c>NetId</c>）。
        /// <para>▍为什么要这条（2026-10-12 第 8 步）：原来只传"死了" ⇒ 两端血量各算各的：客机把油桶打到一半，
        /// 房主那边还是满血（血条 / 受击表现不一致）。战斗前创建的 <c>Other</c> 现在有**派生号**，于是能并进
        /// <c>EnemyDamagedMsg</c> 那条通道（同一套 <c>attackerNetId</c> / <c>DamagePacket.Resolved</c> 口径）。
        /// ⚠ 没号的物件（战斗中途生成）照旧只靠 4043 兜底。</para></summary>
        static void OnLocalDamaged(GameObject victim, PEInt damage, GameObject source, Collider hitBody)
        {
            if (_applyingRemote) return;                       // 本端正在重放"远端那次打爆"
            if (EnemyController.ApplyingRemoteDamage) return;  // 本端正在应用远端伤害 ⇒ 必须挡回环（否则两端互相转发）
            if (victim == null) return;
            if (!victim.TryGetComponent(out Actor actor)) return;
            if (!IsSceneDestructible(actor)) return;

            int netId = actor.NetId;
            if (netId == 0) return;                            // 没号 ⇒ 只保留"死了"那条兜底（4043）

            int dmg = Mathf.RoundToInt(damage.RawFloat);
            if (dmg <= 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.InRoom) return;          // 单机 / 不在房里：两端本来就一致

            // 攻击者号：接收端拿它重建 DamageSource（玩家是 0 ⇒ 那边退回按 Sid 找盟友实例，同 EnemyController 口径）
            var attacker = source != null ? source.GetComponent<Actor>() : null;
            int attackerNetId = attacker != null ? attacker.NetId : 0;

            NetSyncLog.BulletLog("可破坏物·伤",
                $"{actor.ShowName}({actor.Id}) netId={netId} 伤害={dmg} 攻击者号={attackerNetId} 是房主={flow.IsHost}");

            if (flow.IsHost) BattleEventBus.EnemyDamaged(netId, dmg, attackerNetId);   // 房主：下发全体
            else BattleEventBus.EnemyHit(netId, dmg, attackerNetId);                   // 成员：上报房主（由他转发）
        }

        #endregion

        #region 网络 -> 本端

        static void OnRemoteDestroyed(DamageMsg m)
        {
            if (m == null || m.Key == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost)
            {
                // 房主：转发给全体（含发起者；发起者按 Sid 丢掉自己那条）—— 权威 Sid 原样带上
                flow.SendSceneDestructible(m.Key, m.Sid);
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                                   // 自己发的那条绕回来了：本地已经炸过
            }

            _destroyed.Add(m.Key);

            var actor = FindByKey(m.Key);
            if (actor == null)
            {
                // 本端没有这件（已销毁 / 不在同一场景）：正常情况，静默
                return;
            }

            var pos = actor.transform.position;
            NetSyncLog.BulletLog("可破坏物·收", $"{actor.ShowName}({actor.Id}) key={m.Key} 位置={pos:F2} 发起者sid={m.Sid} ⇒ 本端也炸掉");

            _applyingRemote = true;
            try
            {
                // ⚠ 走 IHealth.Kill()：它先把血抹 0 再死，避免 Actor.OnDie 那条"死亡时血量>0"的守卫误报
                //   （本端这份副本的血量**不是**权威，可能还是满血 —— 同 EnemyNetBridge.OnRemoteDied 的口径）。
                var hp = actor.GetComponent<IHealth>();
                if (hp != null) hp.Kill();
                else actor.Kill();
            }
            finally { _applyingRemote = false; }
        }

        /// <summary>新人入房（名册变化）⇒ 房主把这局已经炸掉的补发一遍（幂等：接收端对已死的单位是空操作）。</summary>
        static void OnPlayerList(PlayerInfo[] infos, PlayerProfile[] profiles)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost || _destroyed.Count == 0) return;

            foreach (int key in _destroyed) flow.SendSceneDestructible(key, 0u);
        }

        #endregion

        #region 键与查表

        /// <summary>是不是"场景可破坏物"（<c>UnitTypeEnum.Other</c>：油桶这类摆在场上的物件）。</summary>
        static bool IsSceneDestructible(Actor actor)
            => actor != null && actor.Type == UnitTypeEnum.Other && !string.IsNullOrEmpty(actor.Id);

        /// <summary>跨端稳定的同步键 = <c>FNV1a(Id + 世界坐标 0.1m 量化)</c>（同 <c>Furniture_Attached.ComputeSyncId</c>）。
        /// <para>▍量化到 0.1m 是为了吃掉两端浮点抖动；位置来自**同一份场景**摆好的落点 ⇒ 跨端一致。
        /// ⚠ 会位移的物件不适用（键必须稳定），油桶这类静止物件正好合适。</para></summary>
        static int KeyOf(Actor actor)
        {
            if (actor == null) return 0;
            Vector3 p = actor.transform.position;
            return Fnv1a($"{actor.Id}|{Mathf.RoundToInt(p.x * 10f)}|{Mathf.RoundToInt(p.y * 10f)}|{Mathf.RoundToInt(p.z * 10f)}");
        }

        /// <summary>FNV-1a 32 位（零分配、跨端稳定；与 <c>Furniture_Attached</c> / 任务指纹同族做法）。</summary>
        static int Fnv1a(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; ++i)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return (int)h;
            }
        }

        /// <summary>按同步键找本端那件物件（**public**：统一死亡消息的兜底寻址也用它，见 <c>EnemyNetBridge.ApplyRemoteDeath</c>）。
        /// <para>▍为什么现扫不建表：这类物件**整批随场景加载**、中途不会新增（被打掉只会变少）；
        /// 一条消息扫一次可接受，换来的是"没有会过期/漏注册的静态表"。</para></summary>
        public static Actor FindByKey(int key)
        {
            var all = UnityEngine.Object.FindObjectsByType<Actor>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; ++i)
            {
                var a = all[i];
                if (a == null || !IsSceneDestructible(a)) continue;
                if (KeyOf(a) == key) return a;
            }
            return null;
        }

        #endregion
    }
}
