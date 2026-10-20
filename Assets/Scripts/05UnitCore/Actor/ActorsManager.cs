using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;
using FPSGame.Utils;      // Tool.Destroy（00_Utils 已在 05_UnitCore.asmdef 的 references 里）

using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 维护场上所有单位的注册与查询。
    /// </summary>
    [AddComponentMenu("管理/单位注册表")]
    public class ActorsManager : Singleton<ActorsManager>
    {
        public static Queue<KVP<UnitTypeEnum, IActor>> OnActorCreat=new();//用来创建跟随UI的

        public static List<IActor> Enemys { get; private set; } = new();
        public static List<IActor> Actors { get; private set; } = new();
        public static List<IActor> Players { get; private set; } = new();
        public static List<IActor> SpecUnits { get; private set; } = new();


        /// <summary>本局敌人 <c>NetId</c> 计数器（每局在 <see cref="Awake"/> 归零）。
        /// <para>▍为什么计数挂在这里：分配点只有 <c>Actor.Awake</c> 一处（见 <c>Actor.NetId</c>），
        /// 它比任何刷怪调用点都早、且枚举不全刷怪路径 ⇒ 需要一个"每局一份、且早于全部敌人"的对象来持计数。</para></summary>
        private static int _enemyNetIdSeq;

        /// <summary>特殊单位（炮塔/探照灯/凯伊这类 <see cref="UnitTypeEnum.SpecUnit"/>）的 <c>NetId</c> **独立号段**起点。
        /// <para>▍为什么必须与敌人**不共用**同一个计数器：号段 = "两端第几个被创建"，而这个顺序只在"同一类单位
        /// 的创建路径两端一致"时才成立。SpecUnit 的创建路径（空投落点是墙钟计时、任务实体、场景摆放）与敌人的
        /// 刷怪路径**互不相干** ⇒ 一旦混在一个计数器里，任何一侧多/少一个 SpecUnit 就会把**其后所有敌人**的号整体平移，
        /// 表现为"房主的伤害落到客机另一只怪身上"（比"完全没同步"更坏）。分开号段后，最坏只影响 SpecUnit 之间。</para>
        /// <para>▍为什么用一个大数而不是从 1 开始：<c>EnemyNetBridge.FindActor</c> 统一在 <see cref="Actors"/> 里按号查，
        /// 号段不相交才能保证"一个号只指一个单位"（也便于日志里一眼看出是哪一类）。</para></summary>
        private const int SpecNetIdBase = 1000000;

        /// <summary>本局特殊单位的 <c>NetId</c> 序号（每局在 <see cref="Awake"/> 归零；见 <see cref="SpecNetIdBase"/>）。</summary>
        private static int _specUnitNetIdSeq;

        public static IActor Player { get; private set; }

        /// <summary>
        /// 离 <paramref name="point"/> 最近的玩家（本地玩家与盟友幽灵都在 <see cref="Players"/> 里）。
        ///
        /// <para>▍为什么口径是"最近"而不是"本机玩家"：刷怪中心若取本机玩家位置，两端算出的点必然不同
        /// （各端只有自己那台是真身，盟友是同步过来的近似位置）⇒ 同一波怪会落到不同地方。
        /// 取"离锚点最近的玩家"则两端**用同一份已同步的位置**算出同一个答案（2026-10-07 用户口径）。</para>
        /// </summary>
        public static IActor NearestPlayer(Vector3 point)
        {
            IActor best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Players.Count; ++i)
            {
                var p = Players[i];
                if (p == null) continue;
                // ⚠ 接口引用不能靠 Unity 的假 null（`== null` 判不出已销毁）⇒ 借组件引用判一次
                var comp = p as Component;
                if (comp == null) continue;
                float sqr = (comp.transform.position - point).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = p;
            }
            return best;
        }

        /// <summary>同 <see cref="NearestPlayer"/>，直接给位置；没有玩家时用锚点兜底。</summary>
        public static Vector3 NearestPlayerPos(Vector3 point)
        {
            var p = NearestPlayer(point);
            return p != null ? p.transform.position : point;
        }
        public void RegisterPlayer(IActor player)
        {
            Player = player;
            Players.Add(player);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Player, player));
            //Debug.LogError("玩家出生" + player + "  " + player.transform.position, player.transform);
        }
        public void RegisterFriend(Actor friend)
        {
            Players.Add(friend);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Friend, friend));
        }
        public void RegisterSpecUnit(Actor specUnit)
        {
            //Debug.LogError("响应特殊单位出生"+ specUnit, specUnit);
            SpecUnits.Add(specUnit);
            OnActorCreat.Enqueue(new(UnitTypeEnum.SpecUnit, specUnit));
        }
        public void RegisterEnemy(Actor specUnit)
        {
            //Debug.LogError("特殊单位出生"+ specUnit, specUnit);
            Enemys.Add(specUnit);
        }

        /// <summary>【联机】分配下一个敌人网络标识（唯一调用点 = <c>Actor.Awake</c>）。
        /// <para>按创建顺序自增 ⇒ 两端同序同号；⚠ 未经 <see cref="Instance"/> 判定不得调用（见调用点注释）。</para></summary>
        public static int NextEnemyNetId() => ++_enemyNetIdSeq;

        /// <summary>【联机】分配下一个特殊单位的网络标识（唯一调用点 = <c>Actor.Awake</c>；号段见 <see cref="SpecNetIdBase"/>）。</summary>
        public static int NextSpecUnitNetId() => SpecNetIdBase + (++_specUnitNetIdSeq);

        /// <summary>【联机】**房主分配段**起点：运行时生成（巢穴 / 召唤师 / 刷怪器）走这里。
        /// <para>▍为什么不与敌人自增段共用：自增号的口径是"两端第几个被创建"，而这类生成的**时机由各自的 AI / 计时驱动**，
        /// 会与波次的逐 Tick 投放交错 ⇒ 两端"谁先谁后"必然不同，混在一个计数器里会把其后所有号整体平移。
        /// 这一段由房主分配、随 4052 下发，成员只覆盖 ⇒ 与自增段彻底解耦。</para></summary>
        public const int HostSpawnNetIdBase = 0x2000_0000;

        /// <summary>本局"房主分配段"的序号（每局在 <see cref="Awake"/> 归零）。</summary>
        private static int _hostSpawnNetIdSeq;

        /// <summary>【联机】分配下一个"房主分配"号（唯一调用点 = <c>WaveManager.CreatUnit</c> 的统一入口）。</summary>
        public static int NextHostSpawnNetId() => HostSpawnNetIdBase + (++_hostSpawnNetIdSeq);

        /// <summary>【联机】**空投载具段**起点：战备呼叫出来的载具（机甲/炮台/运输机这类可交互大件）的号。
        /// <para>▍为什么既不能用自增段也不能用派生键：载具**两端各生成一份、且生成时机不同**
        /// （呼叫方立刻生成、其余端收到 4028 才生成）⇒ 自增号必然错位；它的落点又是**各端各自打地面射线**求的
        /// （地形被炸过后差几厘米）⇒ 派生键也会分叉。只能"**房主随 4028 分配 + 两端显式覆盖**"。</para>
        /// <para>⚠ 与既有段互不相交（<see cref="HostSpawnNetIdBase"/> / <see cref="DerivedNetIdBase"/> / 敌人自增 / SpecUnit 自增）：
        /// <c>EnemyNetBridge.FindActor</c> 统一按号查 ⇒ 号段相交就会"一个号指两个单位"。</para></summary>
        public const int VehicleNetIdBase = 0x3000_0000;

        /// <summary>本局"空投载具段"的序号（每局在 <see cref="Awake"/> 归零）。</summary>
        private static int _vehicleNetIdSeq;

        /// <summary>【联机】分配下一个载具号（调用点：房主自己的呼叫 + 房主转发成员呼叫时补号）。
        /// <para>⚠ 只有房主该调它；成员端**不看**这个计数器（否则与房主分配的号撞车）。</para></summary>
        public static int NextVehicleNetId() => VehicleNetIdBase + (++_vehicleNetIdSeq);

        /// <summary>【联机】"接下来创建的单位一律取**房主分配**号"（运行时生成的房主侧；Instantiate 前开、finally 关）。</summary>
        public static bool ForceHostSpawnNetId;

        /// <summary>【联机】"正在复刻房主下发的单位"：<c>Actor.Awake</c> 期间**一个号都不发**（号来自 4052，由 <c>ReplaySpawn</c> 覆盖）。
        /// <para>⚠ 与 <see cref="ForceDerivedNetId"/> 语义相反、必须分开：那个是"按身份算号"，这个是"不许发号"。</para></summary>
        public static bool Replicating;

        /// <summary>【联机】**场景摆放 / 任务实体**这类"两端都能算出同一个出生坐标"的单位的号段起点（高位打标）。
        /// <para>▍为什么单独一段：这类单位两端各自创建、**不发任何消息**（也不用为它们补发全量），号必须由两端
        /// **独立算出同一个值** ⇒ 用 <see cref="DerivedNetId"/> 的哈希，而不是自增计数。高位打标是为了与
        /// 敌人自增段（1 起）、特殊单位段（<see cref="SpecNetIdBase"/>）**互不相交**，这样
        /// <c>EnemyNetBridge.FindActor</c> 才能统一在 <see cref="Actors"/> 里按号查。</para>
        /// <para>⚠ 取号只跟"身份坐标"有关，与 <c>UnitTypeEnum</c> 无关（<c>Other</c> 与 <c>Enemy</c> 都可能走这一段）。</para></summary>
        public const int DerivedNetIdBase = 0x4000_0000;

        /// <summary>【联机】"接下来创建的单位一律按**场景/任务实体**取派生号"的强制开关。
        /// <para>▍用途：`MissionBase.InitMission` 是在**战斗开始之后**实例化任务实体的 —— 光看
        /// `BattleState.IsStartBattle` 会被判成"运行时生成"而拿走自增号（被巢穴/召唤师挤错）。在 `Instantiate`
        /// 前后开一下这个开关，里面那批 `Actor` 就在各自的 `Awake` 里按派生号取。</para>
        /// <para>⚠ `Instantiate` 是**同步**跑完子树里所有 `Awake` 才返回的 ⇒ try/finally 包住那一句就够，不会漏。</para></summary>
        public static bool ForceDerivedNetId;

        /// <summary>【联机】**派生号**：`FNV1a(Actor.Id + 世界坐标 0.1m 量化 + 同 Id 同位置的序号)`，放 <see cref="DerivedNetIdBase"/> 段。
        /// <para>▍适用判据：**"这个单位两端能不能算出同一个出生坐标？"** 能（场景摆放、任务实体 —— 位置来自场景文件、
        /// 朝向来自 `MissionBase.MissionAngle()` 的确定性种子）⇒ 派生号；不能（运行时由 AI/逻辑触发、两端会分叉）⇒ 房主分配。</para>
        /// <para>▍为什么要带"同 Id 同位置的序号"：同类物件多实例（油桶全叫 <c>OilDrum</c>、场景里两盏同名灯）只用
        /// Id+位置会撞号；序号按 <see cref="Actors"/> 里的登记顺序数（两端场景加载顺序一致 ⇒ 同序）。</para>
        /// <para>⚠ 必须在 <c>Actor.Awake</c> 里（自己还没 Add 进 <see cref="Actors"/> 之前）调用，否则序号会把自己数进去。</para>
        /// </summary>
        public static int DerivedNetId(string id, Vector3 pos)
        {
            int qx = Mathf.RoundToInt(pos.x * 10f);
            int qy = Mathf.RoundToInt(pos.y * 10f);
            int qz = Mathf.RoundToInt(pos.z * 10f);

            // 同 Id + 同量化位置的"第几个"（按 Actors 登记顺序数；⚠ 调用点必须早于自己 Add 进 Actors）
            int ordinal = 0;
            for (int i = 0; i < Actors.Count; ++i)
            {
                var a = Actors[i] as Actor;
                if (a == null || a.transform == null) continue;
                if (!string.Equals(a.Id, id, StringComparison.Ordinal)) continue;

                var p = a.transform.position;
                if (Mathf.RoundToInt(p.x * 10f) != qx) continue;
                if (Mathf.RoundToInt(p.y * 10f) != qy) continue;
                if (Mathf.RoundToInt(p.z * 10f) != qz) continue;
                ++ordinal;
            }

            int netId = DerivedNetIdBase | (int)(Fnv1a($"{id}|{qx}|{qy}|{qz}|{ordinal}") & 0x3FFF_FFFFu);
            // 诊断：把**输入**也记下来（两端号不一致时要能分清是 Id / 坐标 / 序号 哪一项分叉，见 NetIdProbe.DumpMap）
            NetIdProbe.RecordDerived(id, qx, qy, qz, ordinal, netId);
            return netId;
        }

        /// <summary>FNV-1a 32 位（零分配、跨端稳定；与 <c>Furniture_Attached.SyncId</c> / 任务指纹同族做法）。</summary>
        private static uint Fnv1a(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; ++i)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return h;
            }
        }
        /// <summary>
        /// 【单位死亡事件入口】⚠ 死亡 **只是"倒地"**，单位还在场上、还可能被救起
        /// <para>▍"离场"走的是另一条路（对象被销毁）⇒ 见 <see cref="Unregister"/>。</para>
        /// </summary>
        public void UnRegisterUnit(Actor actor)
        {
            switch (actor.Type)
            {
                case UnitTypeEnum.Player:
                    //不移除（见上面注释：倒地不离开队伍，复活还是同一个 Actor）
                    break;
                case UnitTypeEnum.Friend:
                    //不移除（见上面注释：倒地待救，救援系统还要在 Players 里找它）
                    break;
                case UnitTypeEnum.Enemy:
                    NetIdProbe.Dead(actor.NetId, actor.Id);   // 只是倒地：对象还在场，之后到达的伤害属"打尸体"
                    Enemys.Remove(actor);
                    Actors.Remove(actor);
                    break;
                case UnitTypeEnum.SpecUnit:
                case UnitTypeEnum.Other:
                    NetIdProbe.Dead(actor.NetId, actor.Id);
                    SpecUnits.Remove(actor);
                    Actors.Remove(actor);
                    break;
            }
        }

        /// <summary>
        /// 【对象被销毁时调用（<c>Actor.OnDestroy</c>）】把单位从**所有**注册表里摘掉，
        /// <para>▍与 <see cref="UnRegisterUnit"/> 的分工：**销毁 ⇒ 不可能再复活，条目一定是垃圾 ⇒ 全部移除**；
        /// </summary>
        public static void Unregister(IActor actor)
        {
            if (actor == null) return;
            // 销毁期间读被打断的对象有风险，而"某类只会出现在某表"是假设；
            // 一次清干净可能存在的重复项。
            Actors.RemoveAll(x => ReferenceEquals(x, actor));
            Enemys.RemoveAll(x => ReferenceEquals(x, actor));
            SpecUnits.RemoveAll(x => ReferenceEquals(x, actor));
            Players.RemoveAll(x => ReferenceEquals(x, actor));
        }

        /// <summary>
        /// 【单位离场】摘注册表 + 销毁 GameObject（两个动作必须成对、顺序固定，所以封在一起）。
        /// <para>⚠ 顺序反了（先 Destroy 后摘表）就会 MissingReferenceException：注册表被一堆系统每帧遍历。</para>
        /// <para>⚠ 刻意不叫 <c>Kill</c>：<see cref="Actor.Kill"/> 是"致死"（走 <c>OnDie</c> 死亡链，单位还在场上、能被救起），
        /// 与本方法"离场销毁"不是一回事。</para>
        /// </summary>
        public static void Despawn(IActor actor)
        {
            if (actor == null) return;

            Unregister(actor);                  // ① 先摘表

            // ② 再销毁。⚠ 接口引用不能用 `?.` / `??`：绕过 Unity 的假 null 重载，会把已销毁对象判成有效
            var comp = actor as Component;
            if (comp == null) return;           // 已销毁 / 不是组件 ⇒ 表已摘干净，收工
            Tool.Destroy(comp.gameObject);
        }

        public override void Awake()
        {
            //不管这个新的单例会不会被覆盖，都刷新列表
            Actors = new();
            Players = new();
            SpecUnits = new();
            OnActorCreat = new();
            _enemyNetIdSeq = 0;   // 本局的敌人编号从头开始（分配点会先判 Instance，保证不会有人在归零之前分号）
            _specUnitNetIdSeq = 0;   // 特殊单位同理（独立号段，见 SpecNetIdBase）
            _hostSpawnNetIdSeq = 0;  // 房主分配段同理（独立号段，见 HostSpawnNetIdBase）
            _vehicleNetIdSeq = 0;    // 空投载具段同理（独立号段，见 VehicleNetIdBase）
            NetIdProbe.Reset();      // 探针账本每局清零（否则上一局的惨案会把这一局误判成"消息迟到"）
            base.Awake();
            UnitEventBus.OnPlayerCreate += RegisterPlayer;
            UnitEventBus.OnFriendCreate += RegisterFriend;
            UnitEventBus.OnSpecUnitCreate += RegisterSpecUnit;
            UnitEventBus.OnUnitDeath += UnRegisterUnit;
            UnitEventBus.OnEnemyCreate += RegisterEnemy;
        }
        private void OnDestroy()
        {
            UnitEventBus.OnPlayerCreate -= RegisterPlayer;
            UnitEventBus.OnFriendCreate -= RegisterFriend;
            UnitEventBus.OnSpecUnitCreate -= RegisterSpecUnit;
            UnitEventBus.OnUnitDeath -= UnRegisterUnit;
            UnitEventBus.OnEnemyCreate -= RegisterEnemy;
        }
    }

}
