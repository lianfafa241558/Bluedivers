using System.Collections.Generic;
using FPSGame.Mission;
using FPSGame.Net;
using UnityEngine;

namespace FPSGame.Managers
{
    /// <summary>
    /// 联机 · **拦截火车**桥（09）：房主的"列车已行驶弧长 + 收尾状态" → 4067 → 成员端列车平滑追赶 / 同刻收尾。
    ///
    /// <para>▍为什么同步弧长而不是位姿：两端轨道由 <c>BattleRandom</c> 同序规划（同一份 <c>RailTrack</c>）
    /// ⇒ 弧长唯一决定车头 + 全部车厢的位姿（`TrainUnit.Place`）。传整套位姿反而多一份"车厢各自摆"的账，
    /// 而且会与 <c>TrainUnit</c> 每帧写 transform 打架。</para>
    ///
    /// <para>▍键 = **班次**（<c>TrainPoseMsg.Tour</c>，0 基发车序号）：两端同一趟车同号；
    /// ⚠ 不能用 <c>Actor.Id</c>（所有趟次都是 `TrainEngineSam`，多车必撞），也不能用 <c>Actor.NetId</c>
    /// （车头两端各自 <c>Instantiate</c>，走自增段 ⇒ 号本身就可能对不上）。</para>
    ///
    /// <para>▍自举：<c>[RuntimeInitializeOnLoadMethod]</c>（同 <c>NetVehicleBridge</c> / <c>NetUnitDamageReporter</c>）。
    /// 06 看不见 07，所以上报走任务侧的 <see cref="MissionInterceptTrain.TrainPoseTick"/> 事件，桥只做搬运：
    /// 节流（2Hz）留在数据所在地（<c>MissionInterceptTrain_Train.PublishTrainPoses</c>）。</para>
    /// </summary>
    public static class NetTrainBridge
    {
        private static bool installed;

        /// <summary>打包用的临时容器（每 2Hz 复用一个数组，不做每帧分配）。</summary>
        private static readonly List<TrainPoseMsg> _items = new(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (installed) return;
            installed = true;

            MissionInterceptTrain.TrainPoseTick += OnPoseTick;     // 【房主】2Hz 弧长上报
            MissionInterceptTrain.TrainEndTick += OnTrainEnd;      // 【房主】收尾（到站 / 被打掉）一次性上报
            NetRoomFlow.OnTrainPose += OnRemoteTrainPose;          // 【成员】应用
        }

        private static void OnPoseTick(IReadOnlyList<TrainUnit> trains)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost || trains == null || trains.Count == 0) return;

            _items.Clear();
            for (int i = 0; i < trains.Count; i++)
            {
                TrainUnit unit = trains[i];
                if (unit == null) continue;
                _items.Add(new TrainPoseMsg { Tour = unit.Tour, Distance = unit.Distance });
            }
            if (_items.Count > 0) flow.SendTrainPose(_items);
        }

        /// <summary>【房主】某趟车收尾：立刻单独发一包（不等 2Hz 周期包——那时它已从活车列表删掉）。
        /// <para>KCP 是可靠有序通道，这一包不会丢也不会越过后续包。</para></summary>
        private static void OnTrainEnd(int tour, byte state)
        {
            var flow = NetRoomFlow.Instance;
            if (flow == null || !flow.IsHost) return;

            _items.Clear();
            _items.Add(new TrainPoseMsg { Tour = tour, State = state });
            flow.SendTrainPose(_items);
        }

        private static void OnRemoteTrainPose(TrainPoseBatchMsg m)
        {
            if (m?.Items == null) return;

            MissionInterceptTrain mission = MissionInterceptTrain.Active;
            if (mission == null) return;   // 本端任务还没建：幂等全量包，下一包再来，不做挂起

            for (int i = 0; i < m.Items.Length; i++)
            {
                TrainPoseMsg it = m.Items[i];
                if (it == null) continue;
                // State != 0 = 收尾包（到站 / 被打掉）；0 = 周期弧长包
                if (it.State != 0) mission.ApplyRemoteTrainEnd(it.Tour, it.State);
                else mission.ApplyRemoteTrainDistance(it.Tour, it.Distance);
            }
        }
    }
}
