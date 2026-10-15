using System.Collections;
using FPSGame.Attributes;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 拦截火车（主线）：开局在地图边缘生成 <see cref="trackCount"/> 条轨道（见 partial：MissionInterceptTrain_Rail），
    /// 之后按时刻表定时发车（车头 + 车厢 + 火车卫队）沿轨道到站；摧毁足够多火车通关，逃走太多则失败。
    /// <para>⚠ 轨道规划/走廊登记在 <c>InitMission</c> 阶段同步完成（早于其它任务点生成），
    /// 所以其它任务点/兴趣点会反过来躲开铁轨。</para>
    /// <para>⚠ 随机全部走 <c>random</c>（BattleRandom 派生）⇒ 两端铺出的轨道与时刻表一致。</para>
    /// </summary>
    [AddComponentMenu("任务/主要/拦截火车", 30)]
    public partial class MissionInterceptTrain : MissionBase, INetSpawner
    {
        [Foldout("任务", true)]
        [InspectorName("需要摧毁的火车数")]
        [SerializeField] private int destroyTarget = 4;
        [InspectorName("允许逃走的火车数(超过则失败)")]
        [SerializeField] private int escapeLimit = 2;
        [InspectorName("首车延迟(秒)")]
        [SerializeField] private float firstDepartureDelay = 60f;
        [InspectorName("发车间隔(秒)")]
        [SerializeField] private float departureInterval = 180f;
        [InspectorName("发车班次数(时刻表长度)")]
        [SerializeField] private int departureCount = 12;

        [Foldout("列车", true)]
        [InspectorName("车头预制体")]
        [SerializeField] private GameObject trainEnginePrefab;
        [InspectorName("车厢预制体")]
        [SerializeField] private GameObject trainCarPrefab;
        [InspectorName("首列车厢数")]
        [SerializeField] private int firstCarCount = 5;
        [InspectorName("车厢数上限(每趟+1)")]
        [SerializeField] private int maxCarCount = 9;
        [InspectorName("火车速度(米/秒)")]
        [SerializeField] private float trainSpeed = 8f;

        [Foldout("火车卫队", true)]
        [InspectorName("每次发车增加的巡逻队数")]
        [SerializeField] private int guardSquadsPerDeparture = 2;
        [InspectorName("卫队巡逻队数上限")]
        [SerializeField] private int guardSquadLimit = 6;

        /// <summary>发车时刻表：第 n 趟走哪条轨道（InitMission 阶段用 BattleRandom 定好 ⇒ 两端一致）</summary>
        private int[] departureTracks;

        /// <summary>铺轨协程是否已经起过（<see cref="InitMission"/> 与 <see cref="StartMission"/> 两处各起一次，防重）</summary>
        private bool _buildStarted;

        /// <summary>
        /// 主线任务在<b>生成任务点阶段</b>就把轨道铺起来（而不是等到开战）。
        /// <para>为什么：本任务通常是第一批创建的 ⇒ 这里把"铁轨走廊"登记进 <see cref="TerrainUtils.ReservedCircles"/>，
        /// 之后创建的其它任务点 / 兴趣点 / 场景点（<c>MissionController.GenerateNewMissionPoint</c>）就会<b>反过来躲开铁轨</b>；
        /// 反过来（铁轨去猜已经摆好的任务点）只能靠事后硬绕，实测很容易绕不出去。</para>
        /// <para>⚠ 协程体会同步执行到第一个 yield 为止，所以"规划 + 走廊登记 + 时刻表"必须放在那个点之前；
        /// <c>CreatMission</c> 随后会把本组件 <c>enabled=false</c>（协程挂起），到开战才继续铺/清/整平。</para>
        /// </summary>
        protected override void InitMission()
        {
            base.InitMission();
            // 附加炮塔是"有 Actor 的运行时生成单位"⇒ 本任务要挂进生成器登记表（成对注销见 Uninit）：
            // 成员端凭 4052 带回来的来源键（本任务 SpawnSourceKey）找回本端这一份，再由它复刻炮塔
            SpawnerRegistry.Register(this);
            BuildRailOnce();
        }

        protected override void StartMission()
        {
            BuildRailOnce();      // 兜底：InitMission 没走到（如场景模式异常）也不会漏铺
            BeginTrainService();  // 开战才开始发车（见 MissionInterceptTrain_Train）
        }

        private void BuildRailOnce()
        {
            if (_buildStarted) return;
            _buildStarted = true;
            StartCoroutine(BuildAllTracks());
        }

        protected override void Uninit()
        {
            base.Uninit();
            SpawnerRegistry.Unregister(this);   // 与 InitMission 的登记成对（别把废对象留在登记表里）
            StopTrainService();   // 还在跑的火车/卫队一起收掉（见 MissionInterceptTrain_Train）
            // 已铺出的轨道当场景遗迹留下，不随任务销毁；只停掉没跑完的协程
            StopAllCoroutines();
        }

        /// <summary>
        /// 发车时刻表：每趟随机一条轨道。
        /// <para>⚠ 必须在 <c>InitMission</c> 阶段（开战前、两端都还没跑过别的随机）定好 —— 这是"静态同步"的口径：
        /// 时刻表只依赖 BattleRandom 的种子与调用顺序，两端算出来必然一样。</para>
        /// <para>⚠ 轨道数为 0（规划失败）时给 0，调用方要判 <see cref="GetTrack"/> 是否为 null。</para>
        /// </summary>
        private void BuildDepartureSchedule()
        {
            int count = Mathf.Max(1, departureCount);
            departureTracks = new int[count];
            int trackTotal = Mathf.Max(0, tracks.Count);
            for (int i = 0; i < count; i++)
            {
                departureTracks[i] = trackTotal > 0 ? random.Next(trackTotal) : 0;
            }
        }
    }
}
