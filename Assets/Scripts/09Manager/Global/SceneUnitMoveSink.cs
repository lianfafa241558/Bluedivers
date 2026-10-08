using System.Collections.Generic;
using FPSGame.Game;
using FPSGame.Gameplay;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 场景单位（NPC 这类**由场景摆好**、会自己走动的氛围单位）的移动同步落地端 —— **挂哪个场景就管哪个场景**。
    ///
    /// <para>▍分工：房主的 <c>NPCWalk</c> 决策 → <see cref="BattleEventSub.OnSceneUnitMove"/> → 本组件转上网络；
    /// 收到房主下发的目标点后按 <c>Actor.Id</c> 找本端那只 NPC 应用。</para>
    /// <para>⚠ 本组件**不做消息注册**（注册在常驻的 <c>NetRoomFlow</c>）：否则场景不在时收到这条会刷
    /// "未注册消息:4032" 并丢包。它只负责"场景内找对象 + 落地"。</para>
    /// </summary>
    public class SceneUnitMoveSink : MonoBehaviour
    {
        /// <summary>一条待落地的移动指令。</summary>
        private struct MoveCmd
        {
            public Vector3 Destination;
            public bool Stop;
        }

        /// <summary>Id → 还没找到对应 NPC 的移动指令（"消息比对象先到"时暂存，见 <see cref="Flush"/>）。</summary>
        private readonly Dictionary<string, MoveCmd> _pending = new Dictionary<string, MoveCmd>();

        /// <summary>遍历 <see cref="_pending"/> 时的键暂存（避免遍历中改字典，也避免每帧产生垃圾）。</summary>
        private readonly List<string> _flushBuffer = new List<string>();

        private void Awake()
        {
            NetRoomFlow.OnSceneUnitMove += OnRemoteMove;
            BattleEventSub.OnSceneUnitMove += OnLocalMove;
            NetRoomFlow.OnJoinResult += OnJoinResult;
        }

        private void Start()
        {
            RefreshRemoteDriven();
        }

        private void OnDestroy()
        {
            NetRoomFlow.OnSceneUnitMove -= OnRemoteMove;
            BattleEventSub.OnSceneUnitMove -= OnLocalMove;
            NetRoomFlow.OnJoinResult -= OnJoinResult;

            // 桥不在场 ⇒ 退回"各端本地游荡"（不能让标志留给下一个场景的 NPC）
            NPCWalk.RemoteDriven = false;
        }

        private void Update()
        {
            if (_pending.Count > 0) Flush();
        }

        /// <summary>入房是**异步**的（大厅有可能先加载、后入房）⇒ 入房成功要重判一次"我是不是成员"。</summary>
        private void OnJoinResult(bool ok, string reason)
        {
            if (ok) RefreshRemoteDriven();
        }

        /// <summary>成员端：NPC 不自己决策，等房主下发。单机 / 房主 = false（行为与联机前完全一致）。</summary>
        private void RefreshRemoteDriven()
        {
            var flow = NetRoomFlow.Instance;
            NPCWalk.RemoteDriven = flow != null && flow.SelfSid != 0u;
        }

        /// <summary>【上行】本端（房主）的 NPC 决策 ⇒ 转发出去（<c>SendSceneUnitMove</c> 内部判 IsHost，成员/单机自动忽略）。</summary>
        private void OnLocalMove(string id, Vector3 destination, bool stop)
        {
            NetRoomFlow.Instance?.SendSceneUnitMove(id, destination, stop);
        }

        /// <summary>【下行】房主下发的目标点 ⇒ 落到本端同名 NPC；对象还没建出来就先存着。</summary>
        private void OnRemoteMove(string id, Vector3 destination, bool stop)
        {
            var walk = Find(id);
            if (walk != null)
            {
                walk.ApplyRemoteMove(destination, stop);
                return;
            }
            _pending[id] = new MoveCmd { Destination = destination, Stop = stop };
        }

        /// <summary>把暂存的指令补发出去（还没建出来的留着，下一帧再看）。</summary>
        private void Flush()
        {
            _flushBuffer.Clear();
            foreach (var kv in _pending) _flushBuffer.Add(kv.Key);

            for (int i = 0; i < _flushBuffer.Count; ++i)
            {
                string id = _flushBuffer[i];
                var walk = Find(id);
                if (walk == null) continue;

                var cmd = _pending[id];
                _pending.Remove(id);
                walk.ApplyRemoteMove(cmd.Destination, cmd.Stop);
            }
        }

        /// <summary>按 <c>Actor.Id</c> 找本端那只 NPC。
        /// <para>⚠ 不能用 <c>ActorsManager.SpecUnits</c>：那张表要靠 <c>ActorsManager</c> 实例订阅事件才有内容，
        /// 而**大厅里没有 ActorsManager**（只有战场的 <c>BattleManager</c> 才建）⇒ 只能扫静态的
        /// <c>ActorsManager.Actors</c>（<c>Actor.Awake</c> 无条件登记，大厅同样成立）。</para></summary>
        private static NPCWalk Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var all = ActorsManager.Actors;
            for (int i = 0; i < all.Count; ++i)
            {
                var a = all[i];
                // 接口引用不能用 == null 判"已销毁"（Unity 假 null）⇒ 借组件引用判一次
                var comp = a as Component;
                if (comp == null) continue;
                if (!string.Equals(a.Id, id, System.StringComparison.Ordinal)) continue;

                // ⚠ 同名也要继续找：Id 只保证"同一份场景里唯一"，**玩家角色可能与 NPC 同名**
                //   （角色 Aris ↔ NPC Aris），先撞上玩家那个 Actor 并不代表这条指令没有落点
                var walk = comp.GetComponent<NPCWalk>();
                if (walk != null) return walk;
            }
            return null;
        }
    }
}
