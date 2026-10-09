using System.Collections.Generic;
using FPSGame.Gameplay;
using FPSGame.Mission;
using FPSGame.Net;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · 任务同步桥（09）：任务**状态 + 进度**以房主权威下发，成员端不自行判定完成。
    ///
    /// <para>▍键：<c>MissionBase.netOrder</c>（由 <see cref="MissionController"/> 按创建顺序赋值、每局归零）。
    /// 各端任务**集合与顺序一致**（本局配置"以内容为准"+ 生成顺序确定）⇒ 序号即稳定键。
    /// ⚠ 不能用 <c>title</c> 当键（可能重复）。</para>
    ///
    /// <para>▍为什么消息要带 State、不能只带进度：<c>MissionBase</c> 有**两个独立出口** ——
    /// <c>UpdateMission()→MissionUpdate</c>（进度）与 <c>CompleteMission/FailMission</c>（结果）；
    /// 触发型任务（进撤离区 / 交互完成）根本不走进度 ⇒ 只同步进度会漏掉它们。</para>
    ///
    /// <para>▍成员端"不自判"：<c>MissionBase.RemoteDriven = true</c> ⇒ 本端的
    /// <c>AddProgress/TryAddProgress/CompleteMission/FailMission</c> 一律不生效
    /// （同 <c>EnemyController.RemoteDrivenMovement</c> 的思路），等房主下发再置位。</para>
    ///
    /// <para>▍每局一份，由 <see cref="WaveManager"/> Install/Uninstall（同 <see cref="EnemyNetBridge"/> 模式）。</para>
    /// </summary>
    public static class NetMissionBridge
    {
        const int StateRunning = 1;
        const int StateCompleted = 2;
        const int StateFailed = 3;

        static bool installed;

        /// <summary>回环门：true = 正在应用远端状态（此时本地事件不许再上行；同时放行被 <c>RemoteDriven</c> 门住的出口）。</summary>
        static bool _applyingRemote;

        /// <summary>上次已广播的状态 / 进度 —— 去重，避免每次 <c>UpdateMission</c> 都发包。</summary>
        static readonly Dictionary<int, int> _lastState = new Dictionary<int, int>();
        static readonly Dictionary<int, int> _lastProgress = new Dictionary<int, int>();

        public static void Install()
        {
            if (installed) return;
            installed = true;
            _applyingRemote = false;
            _lastState.Clear();
            _lastProgress.Clear();

            // 成员端：任务不自行推进 / 完成（一律以房主下发为准）
            var flow = NetRoomFlow.Instance;
            MissionBase.RemoteDriven = flow != null && flow.SelfSid != 0u;

            BattleEventBus.OnMissionStart += OnLocalMissionChanged;
            BattleEventBus.OnMissionUpdate += OnLocalMissionChanged;
            BattleEventBus.OnMissionCompleted += OnLocalMissionChanged;
            BattleEventBus.OnMissionFail += OnLocalMissionChanged;
            BattleEventBus.OnMissionEnd += OnLocalMissionChanged;

            NetRoomFlow.OnMissionUpdate += OnRemoteMissionUpdate;
        }

        public static void Uninstall()
        {
            if (!installed) return;
            installed = false;

            BattleEventBus.OnMissionStart -= OnLocalMissionChanged;
            BattleEventBus.OnMissionUpdate -= OnLocalMissionChanged;
            BattleEventBus.OnMissionCompleted -= OnLocalMissionChanged;
            BattleEventBus.OnMissionFail -= OnLocalMissionChanged;
            BattleEventBus.OnMissionEnd -= OnLocalMissionChanged;

            NetRoomFlow.OnMissionUpdate -= OnRemoteMissionUpdate;

            MissionBase.RemoteDriven = false;   // 退场复位：否则单机时任务会一直不自判、永远完不成
        }

        #region 本端 -> 网络（房主权威）

        static void OnLocalMissionChanged(MissionBase m)
        {
            if (_applyingRemote) return;                 // 远端应用触发的，别再上行

            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost) return;    // 只有房主广播

            Broadcast(m);
        }

        static void Broadcast(MissionBase m)
        {
            if (m == null) return;

            int key = m.netOrder;
            int state = m.completed ? StateCompleted : (m.end ? StateFailed : StateRunning);
            int progress = m.NowProgress;

            // 没变就不发（AddProgress 会频繁触发 UpdateMission）
            int lastState;
            if (_lastState.TryGetValue(key, out lastState) && lastState == state)
            {
                int lastProgress;
                if (_lastProgress.TryGetValue(key, out lastProgress) && lastProgress == progress) return;
            }

            _lastState[key] = state;
            _lastProgress[key] = progress;
            NetRoomFlow.Instance.SendMissionUpdate(key, state, progress, m.MaxProgress, m.percentage);
        }

        #endregion

        #region 网络 -> 本端（成员应用）

        static void OnRemoteMissionUpdate(MissionUpdateMsg m)
        {
            if (m == null) return;

            var battle = BattleManager.Instance;
            var cont = battle != null ? battle.MissionCont : null;
            if (cont == null) return;

            var mission = cont.FindByNetOrder(m.Key);
            if (mission == null) return;   // 任务还没建出来 / 已销毁 ⇒ 忽略（房主不会给不存在的键发）

            _applyingRemote = true;
            try
            {
                mission.ApplyRemoteState(m.State, m.Progress, m.MaxProgress, m.Percentage);
            }
            finally
            {
                _applyingRemote = false;
            }

            _lastState[m.Key] = m.State;
            _lastProgress[m.Key] = m.Progress;
        }

        #endregion
    }
}
