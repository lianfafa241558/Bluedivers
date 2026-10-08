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
    /// <para>▍分工：房主的 <c>NPCWalk</c> 决策 → <see cref="BattleEventBus.OnSceneUnitMove"/> → 本组件转上网络；
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

        /// <summary>
        /// 【自举】保证本组件一定在场（2026-10-09）。
        ///
        /// <para>▍为什么需要：本类原本的设计是"**挂哪个场景就管哪个场景**"，但实测**全项目没有任何场景/预制体挂过它**
        /// ⇒ <c>NPCWalk.RemoteDriven</c> 永远是 false ⇒ 成员端照旧自己摇随机游荡 ⇒ 两端 NPC 位置各走各的
        /// （用户实测"NPC 位置没有同步成功"）。手挂容易再被漏掉，改成与 <c>NetRoot</c> 同款的常驻自举。</para>
        ///
        /// <para>▍常驻为什么不会串场景：本组件只订阅全局事件，找对象走 <c>NPCWalk</c> 自记的在场表
        /// （<c>OnEnable</c>/<c>OnDisable</c> 维护）⇒ 换场景时旧 NPC 随场景卸载自动出表，新场景的自动被接管。
        /// 场景里若**手工挂过**（老做法），本方法直接让位、不重复建。</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<SceneUnitMoveSink>() != null) return;   // 场景里手工挂了的就用那个

            var go = new GameObject("SceneUnitMoveSink(Auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<SceneUnitMoveSink>();
        }

        private void Awake()
        {
            NetRoomFlow.OnSceneUnitMove += OnRemoteMove;
            BattleEventBus.OnSceneUnitMove += OnLocalMove;
            NetRoomFlow.OnJoinResult += OnJoinResult;
        }

        private void Start()
        {
            RefreshRemoteDriven();
        }

        private void OnDestroy()
        {
            NetRoomFlow.OnSceneUnitMove -= OnRemoteMove;
            BattleEventBus.OnSceneUnitMove -= OnLocalMove;
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
            // 数据说话：这一句就是"NPC 会不会各走各的"的总闸（成员端 true、房主/单机 false）
            FPSGame.Utils.NetSyncLog.SyncLog("NPC 总闸", $"RemoteDriven={NPCWalk.RemoteDriven} (flow={(flow != null)} SelfSid={(flow != null ? flow.SelfSid.ToString() : "-")})");
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

        /// <summary>
        /// 按同步键找本端那只 NPC —— 走 <see cref="NPCWalk.FindById"/>（NPC 自己维护的在场表）。
        ///
        /// <para>▍为什么不扫 <c>ActorsManager.Actors</c>（2026-10-09 修）：那张表在 <c>ActorsManager.Awake</c> 里被
        /// <c>Actors = new()</c> 整个换新 ⇒ **场景里先 Awake 的 NPC 不在表里**，按它查永远查不到，
        /// 指令只会堆在 <c>_pending</c>（实测症状：客机 NPC 全员罚站）。
        /// 换成 NPC 自记的表后，顺带也解决了"玩家角色与 NPC 同名"（只在 NPC 之间找，撞不上玩家）。</para>
        /// </summary>
        private static NPCWalk Find(string id) => NPCWalk.FindById(id);
    }
}
