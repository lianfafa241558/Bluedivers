using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Net;
using FPSGame.Utils;
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
    /// <para>▍口径：**只同步"死了"**（不传伤害）。这类物件的血量不是关键状态（打爆才是唯一有意义的结果），
    /// 少一条伤害通道就少一处两端不一致。谁打死的谁上报，房主收到后**转发全体 + 本地应用**
    /// （走向与 <see cref="NetFurnitureBridge"/> 同款：一条 4043 双向）。</para>
    ///
    /// <para>▍匹配键 = <c>FNV1a(Actor.Id + 位置 0.1m 量化)</c>（同 <c>Furniture_Attached.SyncId</c> 的口径）：
    /// 这类物件**没有 NetId**（<c>Actor.NetId</c> 只在 <c>UnitTypeEnum.Enemy</c> 的 <c>Awake</c> 里分配），
    /// 而同类物件多实例（油桶全叫 <c>OilDrum</c>）⇒ 只用 <c>Actor.Id</c> 会撞车、用 <c>IndexID</c> 两端不一致。</para>
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
            NetRoomFlow.OnSceneDestructible += OnRemoteDestroyed;
            NetRoomFlow.OnPlayerList += OnPlayerList;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            UnitEventBus.OnUnitDeath -= OnLocalDeath;
            NetRoomFlow.OnSceneDestructible -= OnRemoteDestroyed;
            NetRoomFlow.OnPlayerList -= OnPlayerList;

            _destroyed.Clear();
            _applyingRemote = false;
        }

        #region 本端 -> 网络

        static void OnLocalDeath(Actor actor)
        {
            if (_applyingRemote) return;                  // 远端那次重放触发的：别再报一次
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

        #endregion

        #region 网络 -> 本端

        static void OnRemoteDestroyed(SceneDestructibleMsg m)
        {
            if (m == null || m.SyncId == 0) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null) return;

            if (flow.IsHost)
            {
                // 房主：转发给全体（含发起者；发起者按 Sid 丢掉自己那条）—— 权威 Sid 原样带上
                flow.SendSceneDestructible(m.SyncId, m.Sid);
            }
            else if (m.Sid == flow.SelfSid)
            {
                return;                                   // 自己发的那条绕回来了：本地已经炸过
            }

            _destroyed.Add(m.SyncId);

            var actor = FindByKey(m.SyncId);
            if (actor == null)
            {
                // 本端没有这件（已销毁 / 不在同一场景）：正常情况，静默
                return;
            }

            var pos = actor.transform.position;
            NetSyncLog.BulletLog("可破坏物·收", $"{actor.ShowName}({actor.Id}) key={m.SyncId} 位置={pos:F2} 发起者sid={m.Sid} ⇒ 本端也炸掉");

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

        /// <summary>按同步键找本端那件物件。
        /// <para>▍为什么现扫不建表：这类物件**整批随场景加载**、中途不会新增（被打掉只会变少）；
        /// 一条消息扫一次可接受，换来的是"没有会过期/漏注册的静态表"。</para></summary>
        static Actor FindByKey(int key)
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
