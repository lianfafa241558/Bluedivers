using System.Collections.Generic;
using FPSGame.Attributes;
using FPSGame.AI;
using FPSGame.Core;
using FPSGame.Game;
using FPSGame.GameContract;
using FPSGame.Gameplay;
using FPSGame.Utils;
using UnityEngine;

namespace FPSGame.Mission
{
    /// <summary>
    /// 拦截火车 · 列车本体（partial）：按时刻表发车、沿轨道行驶、"到站逃走"与"被打掉"两种收尾、
    /// 进度 / 失败判定与开战节拍（<c>TickTime</c> = 3 秒）。附加炮塔见 <c>MissionInterceptTrain_Turret</c>、
    /// 卫队见 <c>MissionInterceptTrain_Guard</c>。
    /// <para>⚠ 生成口径分两类：① **车头 + 车厢**（都没有 <c>Actor</c>）两端各自 Instantiate，靠"名字/顺序一致"对齐；
    /// ② **附加炮塔是有 <c>Actor</c> 的单位** ⇒ 必须走 <c>CreatUnit</c>（房主唯一生成 + 4052）——
    /// 成员端不生成、由 <see cref="ReplaySpawn"/> 按槽位复刻（同卫队的 <c>CreatPatrol</c>）。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        [Foldout("列车生成", true)]
        [InspectorName("车厢间距系数")]
        [SerializeField] private float carSpacingFactor = 0.92f;

        /// <summary>一趟列车（车头 + 车厢 + 它自己的卫队）</summary>
        private class TrainRuntime
        {
            public TrainUnit Unit;
            public GameObject Engine;
            public IHealth Health;
            public UnityEngine.Events.UnityAction<GameObject, GameObject> DieHandler;
            public readonly List<GameObject> Cars = new();
            /// <summary>本趟挂在车厢上的附加炮塔（收尾时静默移除，见 <c>RemoveTurrets</c>）</summary>
            public readonly List<GameObject> Turrets = new();
            public readonly List<GameObject> Guards = new();
            /// <summary>已收尾（到站移除 / 被打掉）——用来区分"不计进度的移除"</summary>
            public bool Ended;
        }

        private readonly List<TrainRuntime> liveTrains = new();

        /// <summary>已发车班次（0 基）</summary>
        private int departedCount;
        /// <summary>已逃走的火车数</summary>
        private int escapedTrains;
        /// <summary>开战后的秒数（按 Tick 累加）</summary>
        private float serviceTime;
        private bool serviceStopped;

        /// <summary>开战：接管列车服务（由主文件的 StartMission 调用）</summary>
        private void BeginTrainService()
        {
            // 3 秒粒度足够：发车、卫队引导都按 3 秒一跳，列车行驶本身在 TrainUnit.Update 里是逐帧的
            TickTime = 3f;
            MaxProgress = Mathf.Max(1, destroyTarget);
            serviceTime = 0f;
            serviceStopped = false;
            RefreshTrainTip();
            NetSyncLog.LoadLog("任务", $"[拦截火车] 开战：计划 {departureTracks?.Length ?? 0} 班车 / 需摧毁 {MaxProgress} / 允许逃走 {escapeLimit}");
        }

        /// <summary>任务结束：把还在跑的火车与卫队一起收掉（由主文件的 Uninit 调用）</summary>
        private void StopTrainService()
        {
            serviceStopped = true;
            for (int i = liveTrains.Count - 1; i >= 0; i--)
            {
                TrainRuntime runtime = liveTrains[i];
                if (runtime.Ended) continue;
                runtime.Ended = true;
                DetachTrain(runtime, true);
                RemoveGuards(runtime);
            }
            liveTrains.Clear();
            pendingTurrets.Clear();   // 还没等到车厢的复刻也一并作废（本任务已经结束了）
        }

        public override bool Tick()
        {
            base.Tick();
            if (end || serviceStopped) return true;

            serviceTime += TickTime;
            if (departureTracks != null && departedCount < departureTracks.Length
                && serviceTime >= firstDepartureDelay + departureInterval * departedCount)
            {
                DepartTrain();
            }
            // ⚠ 卫队引导必须在"发车"之后、同一帧内跑：新下线的卫队这一刻才拿得到 PatrolPos。
            //   晚一个 Tick（3 秒）就会被 EnemyMobile.Start 判成"无巡逻点"进 Idle，而 Idle 不会因为后来补上
            //   PatrolPos 自动醒（IdleBehavior 不查它）⇒ 这支卫队会原地站到被打死。
            RefreshAllGuards();
            return true;
        }

        protected override void Update()
        {
            base.Update();   // ⚠ 必须先跑基类：Tick 节拍在基类 Update 里发
            // 按帧只剩"等着车厢出现"的炮塔复刻要补：它可能在本端还没建车时就到了，那时活车列表还是空的
            // ⚠ 卫队引导**不在这里**（走 Tick，见 Tick()/RefreshAllGuards）：目标点每 3 秒挪 24 米，
            //   按帧重发等于给每只卫队不停排寻路请求（正是 PathRequestManager 被打爆的那种场景）
            if (pendingTurrets.Count > 0) FlushPendingTurrets();
        }

        #region 发车

        /// <summary>发一趟车：车厢数 = 首车 5 节，每趟 +1（上限 9）；卫队 = 首车 0 队，之后每趟 +2（上限 6）</summary>
        private void DepartTrain()
        {
            int trackIndex = departureTracks[departedCount];
            int carCount = Mathf.Clamp(firstCarCount + departedCount, 0, Mathf.Max(0, maxCarCount));
            int squads = Mathf.Clamp(departedCount * guardSquadsPerDeparture, 0, Mathf.Max(0, guardSquadLimit));
            departedCount++;
            int scheduleIndex = departedCount - 1;   // 本趟在发车时刻表 / 炮塔计划里的班次号

            RailTrack track = GetTrack(trackIndex);
            if (track == null)
            {
                Debug.LogWarning($"拦截火车：时刻表第 {departedCount} 班指向的轨道不存在，跳过", this);
                return;
            }
            // Inspector 没拖也能跑：预制体就在 Resources 里，兜底按路径取
            GameObject enginePrefab = trainEnginePrefab;
            GameObject carPrefab = trainCarPrefab;

            RailMetrics engineMetrics = RailMetrics.Measure(enginePrefab);
            RailMetrics carMetrics = carPrefab != null ? RailMetrics.Measure(carPrefab) : default;
            Vector2 start = track.SampleXZ(0f);
            Vector3 forward = track.SampleForward(0f);
            Quaternion heading = Quaternion.LookRotation(forward);
            Vector3 spawnPos = new(start.x, track.BedHeight(0f) - engineMetrics.PivotToBottom, start.y);

            GameObject engine = Instantiate(enginePrefab, spawnPos, heading * Quaternion.Euler(0f, engineMetrics.YawOffset, 0f), transform);
            engine.name = "TrainEngine_" + departedCount;
            TrainRuntime runtime = new() { Engine = engine };
            Damageable engineDamageable = engine.GetComponent<Damageable>();

            List<Transform> carTransforms = new();
            List<float> carOffsets = new();
            List<float> carHeights = new();
            float halfEngine = Mathf.Max(0.5f, engineMetrics.Length * 0.5f);
            float halfCar = Mathf.Max(0.5f, carMetrics.Length * 0.5f);
            for (int i = 0; i < carCount && carPrefab != null; i++)
            {
                GameObject car = Instantiate(carPrefab, spawnPos,
                    heading * Quaternion.Euler(0f, carMetrics.YawOffset, 0f), transform);
                car.name = CarName(scheduleIndex, i);
                // 共享血量：车厢上打的伤害整包转给车头的 Damageable（source 是 public 字段，运行时指定即可）
                if (engineDamageable != null && car.TryGetComponent(out TransferDamageable transfer)) transfer.source = engineDamageable;
                runtime.Cars.Add(car);
                // 附加炮塔：查计划表这一节该挂哪门（-1 = 不挂）⇒ 走 CreatUnit（房主生成 + 4052 复刻）
                GameObject turret = SpawnTurret(scheduleIndex, i, car);
                if (turret != null) runtime.Turrets.Add(turret);
                carTransforms.Add(car.transform);
                carOffsets.Add(halfEngine + halfCar + carMetrics.Length * carSpacingFactor * i);
                carHeights.Add(-carMetrics.PivotToBottom);
            }

            TrainUnit unit = engine.AddComponent<TrainUnit>();
            unit.Init(track, trainSpeed, -engineMetrics.PivotToBottom, engineMetrics.YawOffset,
                carTransforms, carOffsets, carHeights);
            unit.OnArrived += OnTrainArrived;
            runtime.Unit = unit;
            runtime.Health = engine.GetComponent<IHealth>();
            if (runtime.Health != null)
            {
                runtime.DieHandler = (caused, origin) => OnEngineDied(runtime);
                runtime.Health.OnDie += runtime.DieHandler;
            }
            liveTrains.Add(runtime);

            SpawnGuards(runtime, squads);
            RefreshTrainTip();
            NetSyncLog.LoadLog("任务", $"[拦截火车] 发车 #{departedCount}：轨道 {trackIndex + 1} / 车厢 {runtime.Cars.Count} 节 / "
                + $"附加炮塔 {runtime.Turrets.Count} 门 / 卫队 {squads} 队（总长 {track.TotalLength:F0}m，速度 {trainSpeed:F0}m/s）");
            CreatNotice("Yuuka", "TrainDeparture");
        }

        #endregion

        #region 收尾与进度

        private void OnTrainArrived(TrainUnit unit)
        {
            TrainRuntime runtime = liveTrains.Find(item => item.Unit == unit);
            if (runtime == null) return;
            // 到站：火车 + 卫队一起移除，**不计摧毁进度**，记一次"逃走"
            runtime.Ended = true;
            DetachTrain(runtime, true);
            RemoveGuards(runtime);
            escapedTrains++;
            RefreshTrainTip();
            NetSyncLog.LoadLog("任务", $"[拦截火车] 火车到站逃走：{escapedTrains}/{escapeLimit}");
            if (escapedTrains >= escapeLimit)
            {
                serviceStopped = true;
                FailMission();
            }
            else
            {
                CreatNotice("Yuuka", "TrainFailure");
            }
        }

        /// <summary>车头被打掉：计一次进度（到站移除走 <see cref="OnTrainArrived"/>，不会进这里）</summary>
        private void OnEngineDied(TrainRuntime runtime)
        {
            if (runtime.Ended) return;
            runtime.Ended = true;
            DetachTrain(runtime, false);
            NetSyncLog.LoadLog("任务", $"[拦截火车] 火车被打掉：{NowProgress + 1}/{MaxProgress}");
            
            if(!AddProgress(true, true))
            {
                if (NowProgress == MaxProgress / 2)
                {
                    CreatNotice("Yuuka", "TrainHalf");
                }
                else
                {
                    CreatNotice("Yuuka", "TrainDestroy");
                }
            }

        }

        /// <summary>
        /// 摘掉死亡订阅并移除车头 / 车厢。
        /// <para>车头走 <c>Health.Kill()</c>（origin 为空 ⇒ 不计击杀；仍会 fire OnEnemyDead，HpWnd 据此收掉 boss 血条），
        /// 车厢直接销毁（它们只是视觉 + 伤害转发，没有 Actor）。</para>
        /// </summary>
        private void DetachTrain(TrainRuntime runtime, bool killEngine)
        {
            liveTrains.Remove(runtime);
            if (runtime.Health != null && runtime.DieHandler != null) runtime.Health.OnDie -= runtime.DieHandler;
            if (runtime.Unit != null)
            {
                runtime.Unit.OnArrived -= OnTrainArrived;
                runtime.Unit.enabled = false;
            }
            RemoveTurrets(runtime);
            for (int i = 0; i < runtime.Cars.Count; i++)
            {
                if (runtime.Cars[i] != null) Destroy(runtime.Cars[i]);
            }
            runtime.Cars.Clear();
            if (runtime.Engine != null)
            {
                if (runtime.Engine.TryGetComponent(out Actor actor)) actor.RemoveFlag(ActorFlag.Boss);
                if (killEngine && runtime.Health != null) runtime.Health.Kill();
                Destroy(runtime.Engine);
            }
        }

        private void RefreshTrainTip()
        {
            UpdateTip($"摧毁火车 {NowProgress}/{MaxProgress}，已逃走 {escapedTrains}/{escapeLimit}");
        }

        #endregion
    }
}