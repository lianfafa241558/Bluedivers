using System;
using UnityEngine;

namespace FPSGame.Net
{
    /// <summary>
    /// 【位姿同步编排】"谁该上报、怎么转发、谁生效"——与 <see cref="NetRoomFlow"/> 同型的编排层。
    ///
    /// <para>▍通信模型（星型，房主权威）：</para>
    /// <code>
    /// 成员 --PlayerTransformUp(20Hz)--> 房主（写入聚合表 _poses）
    /// 房主 --TransformBatchSync(20Hz)--> 全体（含房主自己的位姿）
    ///       └─ 房主本地也 MessageCenter.Dispatch 一次 ⇒ 房主与成员走**同一条消费路径**
    /// </code>
    ///
    /// <para>▍本类只做"发出去 / 收进来 + 抛事件"：不碰 <c>TeamManager</c> / <c>Actor</c> / <c>Transform</c>
    /// —— <c>02_Net</c> 的 <c>references</c> 是空的，看不见那些类型（落点在 09_Managers 的桥）。</para>
    ///
    /// <para>▍安装点：挂在 <c>GameRoot/NetRoot</c> 节点下（与 <see cref="NetSvc"/> / <see cref="NetHostSvc"/> /
    /// <see cref="NetRoomFlow"/> 同一批常驻组件）。</para>
    /// </summary>
    public class NetTransformFlow : MonoBehaviour
    {
        /// <summary>单例（由 <c>GameRoot</c> 下的常驻节点提供）。</summary>
        public static NetTransformFlow Instance;

        /// <summary>收到一批位姿（成员：来自房主广播；房主：来自本地自派发）。
        /// <para>消费方按 <c>Sid</c> 找自己的实体并应用；<c>Sid</c> 等于"本机自己"时应跳过。</para></summary>
        public static event Action<PoseBatchMsg> OnPose;

        /// <summary>本机是不是房主（开了房）。</summary>
        public bool IsHost => NetHostSvc.Instance != null && NetHostSvc.Instance.RoomInfo != null;

        private void Awake()
        {
            Instance = this;
            MessageCenter.Register<PoseBatchMsg>(CmdId.TransformBatchSync, HandleBatch);
        }

        private void OnDestroy()
        {
            MessageCenter.Unregister(CmdId.TransformBatchSync);
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        /// <summary>
        /// 【上行】把"本机权威的实体位姿"发出去。
        /// <para>房主 = 直接写进聚合表（本地权威）；成员 = 发给房主，由房主聚合并转发。</para>
        /// </summary>
        public void SendLocalPose(PoseSnapshot snap, int matchId)
        {
            if (snap == null) return;

            if (IsHost)
            {
                NetHostSvc.Instance.SetLocalPose(snap, matchId);
                return;
            }

            if (NetSvc.Instance == null || !NetSvc.Instance.IsConnected) return;
            NetSvc.Instance.SendMsg(MessageCenter.Pack(CmdId.PlayerTransformUp,
                new PoseBatchMsg { MatchId = matchId, Items = new[] { snap } }));
        }

        private void HandleBatch(PoseBatchMsg batch)
        {
            if (batch == null || batch.Items == null || batch.Items.Length == 0) return;
            OnPose?.Invoke(batch);
        }
    }
}
