using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Net;
using PEMaths;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · 局内世界状态同步桥（09）：**本局结束 / 开始撤离**。
    ///
    /// <para>▍分工：**房主权威** —— 房主把"结束 / 撤离"各广播一次，成员按同一结果与时机本地应用，
    /// 两端结算界面与撤离闸门才一致。房主**不**自收自己的广播（<c>SendToAll</c> 只发成员）。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> 在 Awake/OnDestroy 里 Install/Uninstall
    /// （同 <see cref="EnemyNetBridge"/> 的模式 ⇒ 不用往 prefab 上加组件）。</para>
    ///
    /// <para>▍为什么要"回环门"：远端应用时我们会**直接派发玩法层事件**（<c>BattleEventBus.EndGame/Evacuate</c>），
    /// 而本桥也订阅了同一个事件（用于上行）⇒ 不加门就会把"远端来的"当成"本机产生的"再上报一次。</para>
    /// </summary>
    public static class NetGameFlowBridge
    {
        static bool installed;

        /// <summary>本局"结束"是否已处理（本地触发或远端应用）——防两个定时器。</summary>
        static bool _gameOverHandled;

        /// <summary>本局"撤离"是否已广播（房主侧一次性）。</summary>
        static bool _evacuateSent;

        /// <summary>回环门：true = 正在"应用远端消息"，此时本地事件不许再上行。</summary>
        static bool _applyingRemote;

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _gameOverHandled = false;
            _evacuateSent = false;
            _applyingRemote = false;

            BattleEventBus.OnEndGame += OnLocalEndGame;
            BattleEventBus.OnEvacuate += OnLocalEvacuate;

            NetRoomFlow.OnGameOver += OnRemoteGameOver;
            NetRoomFlow.OnEvacuate += OnRemoteEvacuate;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            BattleEventBus.OnEndGame -= OnLocalEndGame;
            BattleEventBus.OnEvacuate -= OnLocalEvacuate;

            NetRoomFlow.OnGameOver -= OnRemoteGameOver;
            NetRoomFlow.OnEvacuate -= OnRemoteEvacuate;
        }

        #region 本端 -> 网络

        /// <summary>本端要结束本局 ⇒ 房主广播一次（成员不广播：结果以房主为准）。</summary>
        static void OnLocalEndGame(int delay, GameResult result)
        {
            if (_applyingRemote) return;      // 远端应用触发的，别再上报
            if (_gameOverHandled) return;     // 一局只广播一次

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost) return;

            _gameOverHandled = true;
            flow.SendGameOver(NetRoomFlow.CurrentMatchId, delay, (int)result);
        }

        /// <summary>本端开始撤离 ⇒ 房主广播一次。</summary>
        static void OnLocalEvacuate(PEVector3 pos)
        {
            if (_applyingRemote) return;
            if (_evacuateSent) return;

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost) return;

            var v = pos.RawVector3;
            _evacuateSent = true;
            flow.SendEvacuate(v.x, v.y, v.z);
        }

        #endregion

        #region 网络 -> 本端

        /// <summary>房主宣告本局结束 ⇒ 本地走同一条 <c>BattleManager.EndGame</c>（结果/延迟以房主为准）。</summary>
        static void OnRemoteGameOver(GameOverMsg m)
        {
            if (m == null || _gameOverHandled) return;   // 本地已经结束过 ⇒ 不再排第二个定时器

            _gameOverHandled = true;
            _applyingRemote = true;
            try
            {
                BattleEventBus.EndGame(m.Delay, (GameResult)m.Result);
            }
            finally
            {
                _applyingRemote = false;
            }
        }

        /// <summary>房主宣告开始撤离 ⇒ 本地触发同一条撤离（含撤离点）。</summary>
        static void OnRemoteEvacuate(EvacuateMsg m)
        {
            if (m == null) return;

            _applyingRemote = true;
            try
            {
                BattleEventBus.Evacuate(new PEVector3(new Vector3(m.X, m.Y, m.Z)));
            }
            finally
            {
                _applyingRemote = false;
            }
        }

        #endregion
    }
}
