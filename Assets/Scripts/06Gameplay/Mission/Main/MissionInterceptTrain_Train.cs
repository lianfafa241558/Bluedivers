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
    /// 拦截火车 · 列车与卫队（partial）：按时刻表发车、沿轨道行驶、附加炮塔、卫队跟随、
    /// "到站逃走"与"被打掉"两种收尾，以及进度 / 失败判定。
    /// <para>⚠ 生成口径分两类：① **车头 + 车厢**（都没有 <c>Actor</c>）两端各自 Instantiate，靠"名字/顺序一致"对齐；
    /// ② **附加炮塔是有 <c>Actor</c> 的单位** ⇒ 必须走 <c>CreatUnit</c>（房主唯一生成 + 4052）——
    /// 成员端不生成、由 <see cref="ReplaySpawn"/> 按槽位复刻（同卫队的 <c>CreatPatrol</c>）。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        [Foldout("列车生成", true)]
        [InspectorName("车厢间距系数")]
        [SerializeField] private float carSpacingFactor = 0.92f;
        [InspectorName("卫队跟随刷新(秒)")]
        [SerializeField] private float guardRefreshInterval = 0.5f;
        [InspectorName("卫队落点后移(米)")]
        [SerializeField] private float guardSpawnBack = 25f;

        [Foldout("附加炮塔", true)]
        [InspectorName("附加炮塔(概率, 预制体)")]
        [SerializeField] private List<SKVP<int, GameObject>> additionalTurrets = new();
        [InspectorName("炮塔挂点(车厢子物体名)")]
        [SerializeField] private string turretMountName = "Additional";

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

        /// <summary>
        /// 附加炮塔计划：[班次][车厢序号] ⇒ <see cref="additionalTurrets"/> 里的下标，-1 = 不挂。
        /// <para>⚠ 与 <c>departureTracks</c> 同一套口径：开局（本任务的第一个 yield 之前）用 <c>random</c> 一次算好，
        /// 发车时只查表、不再动随机流 ⇒ 两端必然一致（静态同步）。</para>
        /// </summary>
        private int[][] turretPlan;
        /// <summary>过滤后的可用炮塔条目（计划表里存的下标就是这个表的下标）</summary>
        private readonly List<SKVP<int, GameObject>> turretEntries = new();

        /// <summary>已发车班次（0 基）</summary>
        private int departedCount;
        /// <summary>已逃走的火车数</summary>
        private int escapedTrains;
        /// <summary>开战后的秒数（按 Tick 累加）</summary>
        private float serviceTime;
        private float guardTimer;
        private bool serviceStopped;

        /// <summary>开战：接管列车服务（由主文件的 StartMission 调用）</summary>
        private void BeginTrainService()
        {
            // 5 秒粒度足够：发车与判定都按 5 秒一跳，列车行驶本身在 TrainUnit.Update 里是逐帧的
            TickTime = 5f;
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
            if (departureTracks == null || departedCount >= departureTracks.Length) return true;
            if (serviceTime >= firstDepartureDelay + departureInterval * departedCount) DepartTrain();
            return true;
        }

        protected override void Update()
        {
            base.Update();   // ⚠ 必须先跑基类：Tick 节拍在基类 Update 里发
            // 先把"等着车厢出现"的炮塔复刻补上：它可能在本端还没建车时就到了，那时活车列表还是空的
            if (pendingTurrets.Count > 0) FlushPendingTurrets();
            if (serviceStopped || liveTrains.Count == 0) return;
            guardTimer -= Time.deltaTime;
            if (guardTimer > 0f) return;
            guardTimer = Mathf.Max(0.1f, guardRefreshInterval);
            for (int i = 0; i < liveTrains.Count; i++) RefreshGuards(liveTrains[i]);
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
            GameObject enginePrefab = trainEnginePrefab != null
                ? trainEnginePrefab
                : Resources.Load<GameObject>("Prefabs/GameEvent/TrainEngineSam");
            GameObject carPrefab = trainCarPrefab != null
                ? trainCarPrefab
                : Resources.Load<GameObject>("Prefabs/GameEvent/TrainEngineCar");
            if (enginePrefab == null)
            {
                Debug.LogError("拦截火车：没有车头预制体（Inspector 与 Resources 都没找到）", this);
                return;
            }

            RailMetrics engineMetrics = MeasureRail(enginePrefab);
            RailMetrics carMetrics = carPrefab != null ? MeasureRail(carPrefab) : default;
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
        }

        #endregion

        #region 附加炮塔

        /// <summary>
        /// 附加炮塔计划：把"每一班、每一节车厢挂不挂炮塔、挂哪一门"一次算好。
        /// <para>⚠ 静态同步口径同 <c>BuildDepartureSchedule</c>：只在<b>本任务的第一个 yield 之前</b>跑一次
        /// （见 <c>InitMission</c> 的说明），只用 <c>random</c>（BattleRandom）—— 之后发车只查表，不再动随机流，
        /// 两端必然一致（连"该不该挂"都一致，不会一端多一门）。</para>
        /// <para>概率口径：表里所有条目的 <c>Key</c> 之和 = <b>单节车厢挂炮塔的概率</b>（%）；
        /// 命中后按各自 <c>Key</c> 的占比挑预制体。总和 &gt; 100 ⇒ 视为必挂，<c>Key</c> 退化成权重。</para>
        /// </summary>
        private void BuildTurretPlan()
        {
            turretEntries.Clear();
            int totalChance = 0;
            if (additionalTurrets != null)
            {
                for (int i = 0; i < additionalTurrets.Count; i++)
                {
                    SKVP<int, GameObject> entry = additionalTurrets[i];
                    if (entry == null || entry.Value == null || entry.Key <= 0) continue;
                    turretEntries.Add(entry);
                    totalChance += entry.Key;
                }
            }
            if (turretEntries.Count == 0 || totalChance <= 0)
            {
                turretPlan = null;   // 空表 ⇒ 不挂（AttachTurret 直接跳过）
                NetSyncLog.LoadLog("任务", "[拦截火车] 附加炮塔：概率表为空，本次不发炮塔");
                return;
            }

            int departures = Mathf.Max(1, departureCount);
            int slots = Mathf.Max(1, maxCarCount);
            int chance = Mathf.Min(100, totalChance);
            turretPlan = new int[departures][];
            int planned = 0;
            for (int d = 0; d < departures; d++)
            {
                int[] plan = new int[slots];
                for (int c = 0; c < slots; c++)
                {
                    plan[c] = RollTurretIndex(chance, totalChance);
                    if (plan[c] >= 0) planned++;
                }
                turretPlan[d] = plan;
            }
            NetSyncLog.LoadLog("任务", $"[拦截火车] 附加炮塔计划：{planned} 门（{departures} 班 × 最多 {slots} 节，"
                + $"单节概率 {chance}%，候选 {turretEntries.Count} 种）");
        }

        /// <summary>
        /// 掷一节车厢的炮塔：先按总概率决定挂不挂，再按各自占比挑预制体。
        /// <para>⚠ 固定取两次随机（不挂也取），让随机流长度与"挂不挂"无关 —— 少一处隐性分叉。</para>
        /// </summary>
        /// <returns>炮塔条目下标；-1 = 本节不挂</returns>
        private int RollTurretIndex(int chance, int totalWeight)
        {
            bool attach = chance >= 100 || random.Next(100) < chance;
            int roll = random.Next(Mathf.Max(1, totalWeight));
            if (!attach) return -1;

            int accumulated = 0;
            for (int i = 0; i < turretEntries.Count; i++)
            {
                accumulated += turretEntries[i].Key;
                if (roll < accumulated) return i;
            }
            return turretEntries.Count - 1;
        }

        #region 生成器（INetSpawner：附加炮塔走"房主生成 + 4052 复刻"）

        /// <summary>
        /// 【联机】生成者身份键（<see cref="INetSpawner.SpawnSourceKey"/>）：房主上报、成员凭它在本端找回"同一个任务"。
        /// <para>用任务的跨端稳定键 <see cref="MissionBase.netOrder"/>（<c>MissionController</c> 按创建顺序赋值、
        /// 场景模式按世界坐标排序 ⇒ 两端一致）混上类名。
        /// <para>⚠ 不走 <see cref="NetSpawnKey.Of"/>(gameObject)：任务根是 <c>MissionController</c> 的子物体、
        /// transform 位置都一样、名字又只差 "(Clone)" ⇒ 多个任务的键会撞（复刻到别的任务里）。</para>
        /// </summary>
        public int SpawnSourceKey =>
            ActorsManager.DerivedNetId(nameof(MissionInterceptTrain) + "#" + netOrder, transform.position);

        /// <summary>
        /// 【成员】按房主下发的槽位复刻一节车厢的附加炮塔（由 <c>NetHostSpawnBridge</c> 在
        /// <c>ActorsManager.Replicating = true</c> 期间调用）。
        /// <para>▍槽位口径：<c>[班次, 车厢序号]</c> 拍平 = <c>班次 × 最多车厢数 + 车厢序号</c>（两端算式一致）；
        /// 槽位里**不带 prefab** —— 这一节该挂哪门从本端同一份计划表 <see cref="turretPlan"/> 读
        /// （开局用同一条 BattleRandom 流算的 ⇒ 两端抽签结果一致）。</para>
        /// <para>⚠ 车厢按名字找回（两端车厢同名，见 <see cref="CarName"/>）：两端发车各自 Tick 驱动，复刻可能比本端
        /// 建车早一两帧 ⇒ 那时先挂起（见 <see cref="FlushPendingTurrets"/>），否则炮塔会掉在轨道上不跟车。</para>
        /// </summary>
        public GameObject ReplaySpawn(int slot, int netId, Vector3 pos, Quaternion rot, Vector3 target, string prefabId)
        {
            int carSlots = CarSlots;
            int departure = slot / carSlots;
            int carIndex = slot % carSlots;
            int entry = PlannedTurretEntry(departure, carIndex);
            if (entry < 0 || entry >= turretEntries.Count) return null;

            GameObject car = FindCar(departure, carIndex);
            if (car == null)
            {
                if (pendingTurrets.Count >= PendingTurretLimit)
                {
                    NetSyncLog.Warn("任务", $"[拦截火车] 附加炮塔挂起项已满（>{PendingTurretLimit}）⇒ 槽位 {slot} 丢弃");
                    return null;
                }
                pendingTurrets.Add(new PendingTurret
                {
                    Slot = slot, NetId = netId, Pos = pos, ExpireAt = Time.time + PendingTurretTimeout,
                });
                return null;
            }
            return ReplayTurretOnCar(entry, departure, carIndex, netId, car);
        }

        /// <summary>挂起项上限（正常只在"车厢还没建出来"的那几帧里存在）</summary>
        private const int PendingTurretLimit = 32;
        /// <summary>挂起超时（秒）：车厢始终没出现（例如本端没发这一班）就丢掉，别一直留着</summary>
        private const float PendingTurretTimeout = 10f;

        /// <summary>炮塔复刻挂起项（车厢还没出现时先记着，见 <see cref="FlushPendingTurrets"/>）</summary>
        private struct PendingTurret
        {
            public int Slot;
            public int NetId;
            /// <summary>房主侧的落点（只用于诊断日志；复刻位置取**本端车厢挂点** —— 免得把"RTT 期间车厢已往前开了几米"算进去）</summary>
            public Vector3 Pos;
            public float ExpireAt;
        }

        private readonly List<PendingTurret> pendingTurrets = new();

        /// <summary>把挂起的复刻补上（车厢出现后挂到它下面；超时的丢掉并告警）</summary>
        private void FlushPendingTurrets()
        {
            for (int i = pendingTurrets.Count - 1; i >= 0; i--)
            {
                PendingTurret pending = pendingTurrets[i];
                int carSlots = CarSlots;
                int departure = pending.Slot / carSlots;
                int carIndex = pending.Slot % carSlots;
                int entry = PlannedTurretEntry(departure, carIndex);
                GameObject car = entry >= 0 ? FindCar(departure, carIndex) : null;
                if (car == null)
                {
                    if (Time.time < pending.ExpireAt) continue;
                    pendingTurrets.RemoveAt(i);
                    NetSyncLog.Warn("任务", $"[拦截火车] 附加炮塔复刻超时：槽位 {pending.Slot} 号 {pending.NetId} "
                        + $"落点 {pending.Pos} 的车厢 {CarName(departure, carIndex)} 始终没出现");
                    continue;
                }
                pendingTurrets.RemoveAt(i);
                if (entry < turretEntries.Count) ReplayTurretOnCar(entry, departure, carIndex, pending.NetId, car);
            }
        }

        /// <summary>【成员】把炮塔复刻到指定车厢的挂点下：⚠ 覆盖 <c>NetId</c> 必须早于任何用到号的动作</summary>
        private GameObject ReplayTurretOnCar(int entry, int departure, int carIndex, int netId, GameObject car)
        {
            Transform mount = FindTurretMount(car);
            if (mount == null) return null;

            GameObject turret = Instantiate(turretEntries[entry].Value, mount.position, mount.rotation, mount);
            // ★ 号随 4052 来：复刻期间 Actor.Awake 一个号都不发（ActorsManager.Replicating），这里必须补上
            if (turret.TryGetComponent(out Actor actor)) actor.NetId = netId;
            turret.name = TurretName(departure, carIndex);
            RegisterTurret(car, turret);
            return turret;
        }

        /// <summary>把炮塔记到对应那趟车名下（收尾时随车厢一起静默移除，见 <see cref="RemoveTurrets"/>）</summary>
        private void RegisterTurret(GameObject car, GameObject turret)
        {
            for (int i = 0; i < liveTrains.Count; i++)
            {
                if (liveTrains[i].Cars.Contains(car))
                {
                    liveTrains[i].Turrets.Add(turret);
                    return;
                }
            }
        }

        #endregion

        /// <summary>
        /// 【房主 / 单机】按计划表给一节车厢生成附加炮塔：走 <c>CreatUnit</c>（运行时生成的唯一入口）——
        /// 房主真生成并上报 4052，成员端这一句直接返回 null（等复刻，见 <see cref="ReplaySpawn"/>）。
        /// <para>⚠ 生成完**必须挂到车厢挂点下**：<c>CreatUnit</c> 默认挂在 <c>ActorsManager</c> 下
        /// （"生成者死了不能把单位一起带走"）⇒ 不挂过来炮塔就不跟车走。</para>
        /// </summary>
        /// <returns>生成出来的炮塔（成员端 / 挂点缺失 / 没配表 ⇒ null）</returns>
        private GameObject SpawnTurret(int departure, int carIndex, GameObject car)
        {
            int entry = PlannedTurretEntry(departure, carIndex);
            if (entry < 0 || entry >= turretEntries.Count || car == null || manager == null) return null;

            Transform mount = FindTurretMount(car);
            if (mount == null) return null;

            GameObject turret = manager.CreatUnit(turretEntries[entry].Value, mount.position, mount.rotation,
                this, TurretSlot(departure, carIndex));
            if (turret == null) return null;   // 成员端：不生成（号由 4052 覆盖）

            MountTurret(turret, mount);
            turret.name = TurretName(departure, carIndex);
            if (!turret.TryGetComponent(out Actor _))
            {
                Debug.LogWarning($"拦截火车：炮塔预制体 {turretEntries[entry].Value.name} 上没有 Actor（不是单位）"
                    + "⇒ 房主不会上报、联机端也不会复刻", this);
            }
            return turret;
        }

        /// <summary>槽位 = <c>[班次, 车厢序号]</c> 拍平（两端算式一致）</summary>
        private int TurretSlot(int departure, int carIndex) => departure * CarSlots + carIndex;

        /// <summary>槽位基数 = 一班最多几节车厢（两端同一个序列化值 ⇒ 槽位口径一致）</summary>
        private int CarSlots => Mathf.Max(1, maxCarCount);

        /// <summary>车厢 / 炮塔命名（两端同名 ⇒ 名称就是"跨端稳定键"）</summary>
        private static string CarName(int departure, int carIndex) => $"TrainCar_{departure + 1}_{carIndex + 1}";
        private static string TurretName(int departure, int carIndex) => $"TrainTurret_{departure + 1}_{carIndex + 1}";

        /// <summary>按名字找回本端的车厢（发车是两端各自建的，名字一致）</summary>
        private GameObject FindCar(int departure, int carIndex)
        {
            Transform car = transform.Find(CarName(departure, carIndex));
            return car != null ? car.gameObject : null;
        }

        /// <summary>计划表里"这一班、这一节"该挂哪门（-1 = 不挂）</summary>
        private int PlannedTurretEntry(int departure, int carIndex)
        {
            if (turretPlan == null || departure < 0 || departure >= turretPlan.Length) return -1;
            int[] plan = turretPlan[departure];
            if (plan == null || carIndex < 0 || carIndex >= plan.Length) return -1;
            return plan[carIndex];
        }

        /// <summary>把炮塔挂到车厢挂点下（<c>CreatUnit</c> 生成时挂在 <c>ActorsManager</c> 下 ⇒ 换成挂点下的局部零位）</summary>
        private static void MountTurret(GameObject turret, Transform mount)
        {
            turret.transform.SetParent(mount, false);
            turret.transform.localPosition = Vector3.zero;
            turret.transform.localRotation = Quaternion.identity;
        }

        /// <summary>找车厢上的炮塔挂点（默认 <see cref="turretMountName"/> = Additional）；找不到告警并跳过本节</summary>
        private Transform FindTurretMount(GameObject car)
        {
            if (string.IsNullOrEmpty(turretMountName)) return car.transform;
            Transform mount = car.transform.Find(turretMountName);
            if (mount == null)
            {
                Debug.LogWarning($"拦截火车：车厢 {car.name} 里找不到炮塔挂点 \"{turretMountName}\" ⇒ 本节不挂炮塔", car);
            }
            return mount;
        }

        /// <summary>静默收掉本趟的附加炮塔（<c>Kill(true)</c> 语义同卫队：不算击杀、不放死亡动画）</summary>
        private void RemoveTurrets(TrainRuntime runtime)
        {
            for (int i = 0; i < runtime.Turrets.Count; i++)
            {
                GameObject turret = runtime.Turrets[i];
                if (turret == null) continue;
                if (turret.TryGetComponent(out I_AIController ai)) ai.Kill(true);
                else Destroy(turret);
            }
            runtime.Turrets.Clear();
        }

        #endregion

        #region 卫队

        /// <summary>
        /// 生成火车卫队（房主权威：<c>CreatPatrol</c> 内部就是房主生成 + 4052 复刻）。
        /// <para>⚠ 卫队里带 <see cref="ActorFlag.Boss"/> 的单位要**立刻摘掉**这个标：否则会占一个 boss 血条。</para>
        /// </summary>
        private void SpawnGuards(TrainRuntime runtime, int squads)
        {
            if (squads <= 0 || manager == null || !manager.IsPresent) return;
            Vector2 now = runtime.Unit != null ? runtime.Unit.Position : default;
            Vector3 behind = new(now.x, 0f, now.y);
            if (runtime.Engine != null) behind -= runtime.Engine.transform.forward * guardSpawnBack;
            Vector3 spawnPos = FpsHelper.GetNavMeshPoint(behind);

            for (int i = 0; i < squads; i++)
            {
                List<GameObject> units = manager.CreatPatrol(spawnPos);
                if (units == null) continue;
                for (int k = 0; k < units.Count; k++)
                {
                    GameObject unit = units[k];
                    if (unit == null) continue;
                    if (unit.TryGetComponent(out Actor actor) && actor.HasFlag(ActorFlag.Boss)) actor.RemoveFlag(ActorFlag.Boss);
                    runtime.Guards.Add(unit);
                }
            }
        }

        /// <summary>把卫队的巡逻目标点一直挪到火车当前位置（<c>PatrolPos</c> 是静态点，只能外部改写才能"跟随"）</summary>
        private void RefreshGuards(TrainRuntime runtime)
        {
            if (runtime.Ended || runtime.Guards.Count == 0 || runtime.Unit == null) return;
            Vector2 p = runtime.Unit.Position;
            // ⚠ 必须先投影到 NavMesh：目标不可达时 SetDestination 会失败，而 EnemyController 的去重会把失败记成"已请求"
            Vector3 target = FpsHelper.GetNavMeshPoint(new Vector3(p.x, 0f, p.y));
            for (int i = runtime.Guards.Count - 1; i >= 0; i--)
            {
                GameObject unit = runtime.Guards[i];
                if (unit == null)
                {
                    runtime.Guards.RemoveAt(i);
                    continue;
                }
                if (unit.TryGetComponent(out EnemyController controller)) controller.PatrolPos = target;
            }
        }

        /// <summary>静默移除卫队（<c>IsRemove</c> 语义：无死亡动画、不计击杀归属）</summary>
        private void RemoveGuards(TrainRuntime runtime)
        {
            for (int i = 0; i < runtime.Guards.Count; i++)
            {
                GameObject unit = runtime.Guards[i];
                if (unit == null) continue;
                if (unit.TryGetComponent(out I_AIController ai)) ai.Kill(true);
                else Destroy(unit);
            }
            runtime.Guards.Clear();
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
        }

        /// <summary>车头被打掉：计一次进度（到站移除走 <see cref="OnTrainArrived"/>，不会进这里）</summary>
        private void OnEngineDied(TrainRuntime runtime)
        {
            if (runtime.Ended) return;
            runtime.Ended = true;
            DetachTrain(runtime, false);
            NetSyncLog.LoadLog("任务", $"[拦截火车] 火车被打掉：{NowProgress + 1}/{MaxProgress}");
            AddProgress(true, true);
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
