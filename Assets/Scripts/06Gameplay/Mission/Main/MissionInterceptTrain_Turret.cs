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
    /// 拦截火车 · 车厢附加炮塔（partial）：配置与概率计划表、房主生成（<c>CreatUnit</c> + 4052）、
    /// 成员按槽位复刻（含"车厢还没建出来"时的挂起补挂）、随车厢一起收尾。
    /// <para>⚠ 静态同步：概率计划表在本任务的<b>第一个 yield 之前</b>算好（两端同一份 BattleRandom 流），
    /// 发车只查表 ⇒ 连"该不该挂"都一致；复刻位置一律取<b>本端车厢挂点</b>，不用房主消息里的落点
    /// （否则会把 RTT 期间车厢已开出的距离算进去）。</para>
    /// </summary>
    public partial class MissionInterceptTrain
    {
        [Foldout("附加炮塔", true)]
        [InspectorName("附加炮塔(概率, 预制体)")]
        [SerializeField] private List<SKVP<int, GameObject>> additionalTurrets = new();
        [InspectorName("炮塔挂点(车厢子物体名)")]
        [SerializeField] private string turretMountName = "Additional";

        /// <summary>
        /// 附加炮塔计划：[班次][车厢序号] ⇒ <see cref="additionalTurrets"/> 里的下标，-1 = 不挂。
        /// <para>⚠ 与 <c>departureTracks</c> 同一套口径：开局（本任务的第一个 yield 之前）用 <c>random</c> 一次算好，
        /// 发车时只查表、不再动随机流 ⇒ 两端必然一致（静态同步）。</para>
        /// </summary>
        private int[][] turretPlan;
        /// <summary>过滤后的可用炮塔条目（计划表里存的下标就是这个表的下标）</summary>
        private readonly List<SKVP<int, GameObject>> turretEntries = new();

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
            if (turret.TryGetComponent(out Actor actor))
            {
                actor.NetId = netId;
                actor.AddFlag(ActorFlag.AllowFloating);
                //Debug.LogError("给"+actor+"加浮空标签",actor);
            }
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
            if (!turret.TryGetComponent(out Actor actor))
            {
                Debug.LogWarning($"拦截火车：炮塔预制体 {turretEntries[entry].Value.name} 上没有 Actor（不是单位）"
                    + "⇒ 房主不会上报、联机端也不会复刻", this);
            }
            else
            {
                actor.AddFlag(ActorFlag.AllowFloating);
                //Debug.LogError("给" + actor + "加浮空标签", actor);
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
    }
}