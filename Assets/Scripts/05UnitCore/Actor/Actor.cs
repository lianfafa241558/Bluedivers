using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using PEMaths;
using FPSGame.Core;
using FPSGame.Core.Interface;
using FPSGame.GameContract;
using UnityEngine.AI;
using System.Collections;
using FPSGame.Attributes;
using FPSGame.Utils;

namespace FPSGame.Game
{
    //此类包含描述单位(actor)（玩家或敌人）的一般信息
    //它主要用于AI检测逻辑，并确定参与者是朋友还是敌人
    /// <summary>
    /// 单位通用信息组件（队伍、名称、占位范围），是 AI 与伤害体系的载体。
    /// </summary>
    [AddComponentMenu("单位/单位信息")]
    public class Actor : BaseObject, IActor
    {
        private static int GlobalIndexID = 0;
        //public event UnityAction<I_Actor> OnStateChange;
        public event UnityAction<IActor> OnPosChange;
        public event UnityAction<IActor> OnAngleChange;
        
        public event UnityAction OnDeath;

        #region 接口
        /*
        public new string ShowName { get => base.ShowName; set => base.ShowName = value; }
        public new string Id { get => base.Id; set => base.Id = value; }
        public new Sprite Portrait { get => base.Portrait; set => base.Portrait = value; }
        public new Sprite ExtraPortrait { get => base.ExtraPortrait; set => base.ExtraPortrait = value; }
        
        public new Color Color
        {
            get => base.Color;
            set => base.Color = value;
        }*/


        public int IndexID => indexID;
        private int indexID;

        /// <summary>
        /// **跨端稳定的网络标识**（0 = 不是同步单位）。由房主在为"会同步的世界实体"（敌人 / 巡逻队 / 兴趣点）分配时写入，
        /// 随生成事件下发给成员；两端拿它指同一个单位。
        ///
        /// <para>▍为什么不用 <see cref="IndexID"/>：它是**进程内自增**（<c>GlobalIndexID++</c>，Awake 里赋），
        /// 而且还数着玩家、盟友幽灵、道具等两端创建时机不同的东西 ⇒ 必然错位（工程里早有同类结论，
        /// 见 <c>NetFriendBridge</c> 的快照注释）。玩家/盟友的身份用 <c>sid</c> 就够，这里只服务于"世界实体"。</para>
        /// </summary>
        private int _netId;
        public int NetId
        {
            get => _netId;
            set
            {
                if (_netId == value) return;
                // 复刻（4051）是"先 Awake 后覆盖号"⇒ 账本必须挂在 setter 上，否则探针看不到这类单位，
                // 会把"消息迟到"误判成"本端从未有过这个号"。
                if (_netId != 0) NetIdProbe.Died(_netId, Id);
                _netId = value;
                if (value != 0)
                {
                    NetIdProbe.Born(value, Id);
                    // 号 → 身份 映射（两端对照用）。挂在 setter 上 ⇒ 自增 / 特殊 / 4052 覆盖**全都**经过这里；
                    // 派生段在 ActorsManager.DerivedNetId 里已带"同 Id 同坐标序号"记过，RecordMap 内部会跳过它。
                    NetIdProbe.RecordMap(value, Id, transform.position);
                }
            }
        }

        /// <summary>【联机】本端**本地生成、两端各一份**的部署/支援单位（空投炮台、治疗无人机、火箭包…）：
        /// 它的血量权威归房主 —— 非权威端不本地结算 AI 来源的伤害，只应用房主下发的血与死亡。
        /// <para>▍为什么需要：这类单位两端各自 <c>Instantiate</c>、各自跑 AI、各自挨一份子弹 ⇒ 各扣一次必然分叉，
        /// 且成员端还会把这一扣上报给房主 ⇒ 变成 2 倍。与载具是同一个门
        /// （<c>NetVehicleDamageBridge.Gate</c>，口径 = <c>BattleState.IsVehicleDamageAuthority</c>）。</para>
        /// <para>⚠ 运行期标记、不参与序列化：由 <c>VFXAirdropEffect.SpawnCreatObject</c> 在生成时置位。</para></summary>
        public bool HostAuthorityUnit { get; set; }

        public ActorState ActorState { 
            get => actorState;
            set{
                actorState = value;
                //OnStateChange?.Invoke(this);
            } 
        }
        [SerializeField]
        [InspectorName("状态")]
        private ActorState actorState = ActorState.Normal;
        public UnitTypeEnum Type => type;
        public IPERange Range=> range;

        bool IActor.IsFixed{
            get=>IsFixed;
            set =>IsFixed = value; 
        }

        public override float HalfRange => rangeLength;

        /// <summary>
        /// 单位半高度：单位竖直占位区间 = [CenterPos.y - HalfHeight, CenterPos.y + HalfHeight]
        /// 0 表示未配置，需要做竖直判定的逻辑会退化为"不做高度过滤"
        /// 编辑器工具(菜单 Tools/单位半高度批量设置)可批量按 AimPoint 局部 Y 填充
        /// </summary>
        public override float HalfHeight => halfHeight;

        public int Team { get => team; set => team=value; }

        List<UnitQueryGridNode> IActor.GridNodes => curQueryGridNodes;

        public Transform AimPoint=> aimPoint;
        public IDamageable MainDamageable => mainDamageable;
        public IDamageable[] Damageables => damageables;

        /// <summary>【联机】把一次真实命中映射成**部位下标**（<see cref="Damageables"/> 的稳定下标）：命中体自己、
        /// 或它父级上的那个 <c>Damageable</c>，在表里排第几。<c>-1</c> = 认不出（接收端退回自己挑主躯干）。
        ///
        /// <para>▍为什么要传下标（2026-10-12）：两端"挑哪个部位"的算法不完全等价（装甲 / 弱点倍率口径会差一点），
        /// 传下标就能让对端打在**同一部位**上（见 <c>DamageMsg.Part</c> / <c>EnemyNetBridge.ResolveDamageable</c>）。</para>
        /// <para>▍下标为何跨端稳定：<see cref="damageables"/> 由 <c>GetComponentsInChildren&lt;Damageable&gt;</c> 按层级序采集，
        /// 同一把 prefab 两端同序。</para></summary>
        public int IndexOfDamageable(Collider hit)
        {
            if (hit == null || damageables == null) return -1;

            var d = hit.GetComponentInParent<Damageable>();
            if (d == null) return -1;

            for (int i = 0; i < damageables.Length; ++i)
            {
                if (ReferenceEquals(damageables[i], d)) return i;
            }
            return -1;
        }

        public float Threat => threat;

        public override Vector3 CenterPos {
            get {
                if (this == null) return default;
                return AimPoint != null ? AimPoint.position : base.CenterPos;
            }
        }

        public Vector3 HpPos => AimPoint.position + Vector3.up * HpHeight;

        //I_Actor I_Actor.Owner => this.Owner;

        public bool HasFlag(ActorFlag flag) => this.flag.HasFlag(flag);


        public void AddFlag(ActorFlag flag) => this.flag|=flag;

        public void RemoveFlag(ActorFlag flag) => this.flag &= ~flag;



        /// <summary>是否为固定单位(只对EnemyMoble有效)</summary>
        [InspectorName("固定单位(只对EnemyMoble有效)")]
        public bool IsFixed;

        public bool Equals(IActor obj) => (obj!=null)&&obj.IndexID == IndexID;

        public override bool Equals(object other)=>this == other as Actor;

        public override int GetHashCode()
        {
            return indexID.GetHashCode();
        }

        // 重载 == 运算符
        public static bool operator == (Actor left, Actor right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;
            return left.Equals(right);
        }

        // 重载 != 运算符
        public static bool operator !=(Actor left, Actor right)
        {
            return !(left == right);
        }

        #endregion

        #region 属性
        [SerializeField]
        [InspectorName("标识")]
        private ActorFlag flag;

        [InspectorName("类型")]
        [SerializeField]
        private UnitTypeEnum type;

        [SerializeField]
        [InspectorName("队伍")]
        private int team;

        [SerializeField]
        [InspectorName("仇恨系数")]
        private float threat = 1;

   
        [SerializeField]
        [InspectorName("瞄准点")]
        private Transform aimPoint;

        [InspectorName("显示血条")]
        public bool UseHpBar = true;
        [InspectorName("血条的额外高度")]
        [Compare("UseHpBar",1,CompareOperate.Equal)]
        public float HpHeight=1;


        #endregion

        #region 逻辑碰撞
        [Foldout("逻辑碰撞", true)]
        public ShapeType shape= ShapeType.Circle;
        [SerializeField]
        [InspectorName("半径/半长度")]
        private float rangeLength;

        [SerializeField]
        [InspectorName("半高度")]
        [Tooltip("单位竖直占位半高度，以瞄准点高度为中心上下各延伸该值。0=未配置，退化不做高度过滤")]
        private float halfHeight;

        //[DisplayField]
        //[InspectorName("召唤者")]
        public IActor Origin { get; set; }

        private Vector3 lastAngle;
        private IPERange range;
        public List<UnitQueryGridNode> curQueryGridNodes = new List<UnitQueryGridNode>();
        [HideInInspector]
        Damageable[] damageables;
        [HideInInspector]
        IDamageable mainDamageable;
        #endregion

        private bool isInitialized;

        private void Awake()
        {
            indexID = GlobalIndexID++;
            // ★ 联机：敌人网络标识的**唯一分配点**。放在这里（Instantiate 内同步执行）而不是各生成点 ——
            //   刷怪路径既多又互不相干（ZergWave/RobotWave 走裸 Instantiate、巡逻队与场景刷新走
            //   WaveManager.CreatUnit、巢穴/召唤/任务召唤各走各的），漏掉任何一处就是"那只怪 NetId=0"⇒
            //   房主不广播它的移动意图、成员端（RemoteDrivenMovement）又不自行决策 ⇒ 永远原地罚站（2026-10-08 实测）。
            //   ⚠ 不能在 WaitSetPos 的 EnemyCreate 事件里分配：那条事件要等 IsMainStage 且晚于本帧，
            //     而波次单位创建后**同帧**就会 SetNavDestination（房主要在那里广播）。
            //   两端创建顺序一致 ⇒ 同一个 NetId 指同一个单位；ActorsManager 未就绪时分 0（不参与同步）。
            // ★ 联机取号分两档（判据 = "两端能不能算出同一个出生坐标"）：
            //   ① 战斗开始**前**就存在的（场景摆放 / 大厅）⇒ **派生号**（Id + 位置量化 + 同组序号，两端各算各的、零消息）。
            //      它们从此不受任何"创建顺序分叉"影响 —— 巢穴/召唤师多生几只也不会把它们挤错号。
            //   ② 战斗开始**后**由运行时生成的（波次 / 巢穴 / 召唤 / 刷怪器）⇒ 自增号（波次那条链路两端同序）。
            //      ⚠ 这批正是"两端会分叉"的，后续由"房主唯一创建 + 4052"接管（见 topics/net-battle §7.1）。
            //   ⚠ 玩家 / 盟友不取号：它们的身份就是 sid（房主 = 0），不参与这套号空间。
            //   ★ 2026-10-12 再拆两档：③ 战斗开始后的"运行时生成"（巢穴 / 召唤师 / 刷怪器）改走**房主分配段**
            //     （NextHostSpawnNetId，两端时机必然不同、不能再赌同序）；④ 复刻期间**不发号**（Replicating，号随 4052 来）。
            if (ActorsManager.Instance != null && type != UnitTypeEnum.Player && type != UnitTypeEnum.Friend)
            {
                if (ActorsManager.Replicating)
                {
                    // ★ 联机复刻（4052）：号随消息带来，由 INetSpawner.ReplaySpawn 覆盖 ⇒ 这里**一个号都不能领**，
                    //   领了就会把房主与自己这侧的计数器推歪（其后所有自增号整体错位）。
                }
                else if (ActorsManager.ForceHostSpawnNetId)
                {
                    // ★ 运行时生成（巢穴 / 召唤师 / 刷怪器）在**房主侧**取"房主分配"号段（与敌人自增段解耦，见 HostSpawnNetIdBase）
                    NetId = ActorsManager.NextHostSpawnNetId();
                }
                else if (ActorsManager.ForceDerivedNetId || !FPSGame.Data.BattleState.IsStartBattle)
                {
                    NetId = ActorsManager.DerivedNetId(Id, transform.position);
                }
                else if (type == UnitTypeEnum.Enemy)
                {
                    NetId = ActorsManager.NextEnemyNetId();
                }
                else if (type == UnitTypeEnum.SpecUnit && !HasFlag(ActorFlag.Unimportant))
                {
                    // 独立号段（见 ActorsManager.SpecNetIdBase）：与敌人共用一个计数器会让"任一端的 SpecUnit
                    // 数量差一只"把其后所有敌人的号整体平移。
                    // ⚠ 排除 Unimportant：它们连 SpecUnits 都不注册，且多半是背景物/无敌物
                    //   （如 Turret_Mission_Machine 的 flag 177 含 Unimportant + Invincible）⇒ 发了号也查不到。
                    NetId = ActorsManager.NextSpecUnitNetId();
                }
            }
            switch (shape)
            {
                case ShapeType.Circle:
                    range = new PECircle(LogicPos, (PEInt)rangeLength);
                    break;
                case ShapeType.Rectangle:
                    range = new PERect(LogicPos, (PEInt)rangeLength, (PEInt)rangeLength);
                    break;
            }
            if (!ActorsManager.Actors.Contains(this))
            {
                ActorsManager.Actors.Add(this);
            }
            var m_Health = GetComponent<IHealth>();
            if (m_Health.IsValidMono())
            {
                m_Health.OnDie += OnDie;
                m_Health.OnRevive += OnRevive;
                mainDamageable = m_Health.GetMainPart();
            }

            damageables = GetComponentsInChildren<Damageable>();

            Range.SetXY((PEVector2)Pos);
            lastAngle = transform.eulerAngles;
            UnitEventBus.UnitPosChange(this);
            StartCoroutine(WaitSetPos());
        }

      
        /// <summary>是否已进入主流程（2026-10-01 改走「数据自持」`FPSGame.Data.FlowState`，取代 ServiceLocator.Flow）。
        /// 未就绪时 <c>GameState</c> 为默认的 <c>Front</c> ⇒ 返回 false，与原语义一致。
        /// ⚠ 本方法在 <c>WaitSetPos</c> 里被**每帧轮询** ⇒ 数据自持的属性读取零转换开销。</summary>
        private static bool IsMainStage() => FPSGame.Data.FlowState.IsMainStage;

        private IEnumerator WaitSetPos()
        {
            yield return null;
            // ⚠ 除了"主流程"，还必须等**注册表就位**：场景里的 Actor 在场景激活那一帧就已经 Awake，
            //   而 ActorsManager 是 BattleManager.Creat 时才 new 出来的（晚一两帧）。从舰桥进新手教程时
            //   GameState 本来就是 Bridge ⇒ 只判 IsMainStage 会把创建事件发给一排**还没人订阅**的静态事件，
            //   结果是玩家与引导 NPC 都没注册：字幕框不出现、语音也没人播（2026-10-26 实测）。
            while (!IsMainStage() || ActorsManager.Instance == null)
            {
                yield return null;
            }
            //Debug.Log("创建了单位" + this.gameObject, this.gameObject);
            switch (type)
            {
                case UnitTypeEnum.Enemy:
                    UnitEventBus.EnemyCreate(this);
                    break;
                case UnitTypeEnum.Player:
                    //Debug.Log("创建了玩家" + this.gameObject, this.gameObject);
                    UnitEventBus.PlayerCreate(this);
                    break;
                case UnitTypeEnum.Friend:
                    UnitEventBus.FriendCreate(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    //Debug.LogError("特殊单位出生" + ShowName, this);
                    if (!HasFlag(ActorFlag.Unimportant)) UnitEventBus.SpecUnitCreate(this);
                    //Debug.LogError("创建特殊单位"+ShowName);
                    break;
                case UnitTypeEnum.Other:
                    if (HasFlag(ActorFlag.AutoRegister)) UnitEventBus.SpecUnitCreate(this);
                    break;
            }
            isInitialized = true;

            yield return null;
            if (!HasFlag(ActorFlag.AllowFloating))
            {
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 100, UnityEngine.AI.NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                }
                //transform.position = TerrainUtils.WSToTS(transform.position);
            }
            Range.SetXY(LogicPos);
            UnitEventBus.UnitPosChange(this);
            OnPosChange?.Invoke(this);

        }


        void OnRevive()
        {
            Debug.LogError("单位复活");
            ActorState = ActorState.Normal;
            switch (type)
            {
                case UnitTypeEnum.Player:
                    UnitEventBus.PlayerRevive(this);
                    break;
                case UnitTypeEnum.Friend:
                    //GlobalEventManager.FriendDead(this);
                    break;
                case UnitTypeEnum.Enemy:
                    //GlobalEventManager.EnemyDead(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    //GlobalEventManager.SpecUnitDead(this);
                    break;
                case UnitTypeEnum.Other:
                    //if (HasFlag(ActorFlag.AutoRegister)) GlobalEventManager.SpecUnitDead(this);
                    break;
            }
        }
        /// <param name="caused">**直接来源**（显示/AI；本类只用于调试/排查）</param>
        /// <param name="origin">**起源**（统计）—— `UnitKill` 报的是它，保证"炮台击杀算呼叫者"的口径不变</param>
        void OnDie(GameObject caused, GameObject origin)
        {
            //Debug.LogError("单位死亡"+gameObject,gameObject);
            UnitEventBus.UnitDeath(this);
            OnDeath?.Invoke();
            ActorState = ActorState.Dead;
            var m_Health = GetComponent<IHealth>();
            if (m_Health.IsValidMono()&&m_Health.GetHpCurrent()>0)
            {
                Debug.LogError($"[Health] 单位死亡时生命值>0！CurrentHealth={m_Health.GetHpCurrent()}, MaxHealth={m_Health.GetHpMax()}", gameObject);
            }
            if (origin.IsValid()) UnitEventBus.UnitKill(origin.GetComponent<Actor>(),this);
            switch (type)
            {
                case UnitTypeEnum.Player:
                    UnitEventBus.PlayerDead(this);
                    //StartCoroutine(WaitSetPos());//防止死天上
                    break;
                case UnitTypeEnum.Friend:
                    //FriendDead 事件已删除（全仓零订阅者），此处不再派发
                    break;
                case UnitTypeEnum.Enemy:
                    UnitEventBus.EnemyDead(this);
                    break;
                case UnitTypeEnum.SpecUnit:
                    UnitEventBus.SpecUnitDead(this);
                    break;
                case UnitTypeEnum.Other:
                    if (HasFlag(ActorFlag.AutoRegister)) UnitEventBus.SpecUnitDead(this);
                    break;
            }
            //Debug.LogError("剩余的移动回调"+ OnPosChange, gameObject);
        }

        void OnDestroy()
        {
            // ⚠ 必须自愈式注销注册表：联机盟友"离场"是直接 Destroy（不走死亡事件，也不会有 UnitDeath），
            //   不在销毁时摘除 ⇒ 被销毁的 Actor 会永远留在 ActorsManager.Players/Actors 里，
            //   所有遍历它们的系统都会 MissingReferenceException（2026-10-07 实测）。
            //   ⚠ 与"死亡"区分：死亡只是倒地（不销毁对象），走 UnitEventSub.OnUnitDeath ⇒ UnRegisterUnit，
            //     那条路刻意**不移除** Player/Friend（倒地待救 / 全队阵亡统计要靠它们）。
            //   放最前面：后面的清理可能抛异常/早退，而注销注册表必须无条件完成。
            ActorsManager.Unregister(this);
            NetIdProbe.Died(NetId, Id);   // 记账：之后到达的伤害/死亡消息据此判定为"迟到"

            // 盟友离场的事件通知：销毁之后谁也读不出它是谁（name/Type 还能读，但引用已经废了）
            // ⇒ 必须**在这里、趁引用还有效**发出去，让为它建的 UI/桥做收尾。
            if (Type == UnitTypeEnum.Friend) UnitEventBus.FriendLeave(this);

            /*
            if (ActorState != ActorState.Dead)
            {
                Debug.LogError("非正常死亡"+gameObject,gameObject);
                OnStateChange?.Invoke(ActorState.Dead);
            }*/
            //Debug.LogError("单位被移除"+gameObject);
            //OnStateChange = null;
            var m_Health = GetComponent<IHealth>();
            if (m_Health.IsValidMono())
            {
                m_Health.OnDie -= OnDie;
                m_Health.OnRevive -= OnRevive;
            }
            OnDeath = null;
            OnPosChange = null;
            OnAngleChange = null;
        }

        public void Kill()
        {
            OnDie(null, null);
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (Range.GetXY() != LogicPos)
            {
                Range.SetXY(LogicPos);
                UnitEventBus.UnitPosChange(this);
                OnPosChange?.Invoke(this);
            }
            if (lastAngle!= transform.eulerAngles)
            {
                lastAngle = transform.eulerAngles;
                OnAngleChange?.Invoke(this);
            }
        }


        private void OnDrawGizmosSelected()
        {
            DrawRangeGizmo();
        }

        /// <summary>占位 Gizmo 圆周分段数</summary>
        private const int GizmoSegments = 16;

        /// <summary>
        /// 绘制单位占位范围：配置了半高度时绘制由 半径(rangeLength) 与 半高度(halfHeight) 控制的胶囊体，否则退化为球体
        /// 胶囊中心为 <see cref="CenterPos"/>，总高度 = 2 * halfHeight（底部即单位脚底，顶部为头顶）
        /// </summary>
        private void DrawRangeGizmo()
        {
            Vector3 center = CenterPos;
            float radius = rangeLength;

            // 未配置半高度(0)：退化为球体，保持原有表现
            if (halfHeight <= 0f)
            {
                Gizmos.DrawWireSphere(center, radius);
                return;
            }

            // 胶囊 = 上下半球 + 中间圆柱；半高度不足一个半径时圆柱段高度为 0，自然退化为球
            float bodyHalf = Mathf.Max(0f, halfHeight - radius);
            Vector3 top = center + Vector3.up * bodyHalf;
            Vector3 bottom = center - Vector3.up * bodyHalf;

            DrawWireCap(top, radius, Vector3.up);
            DrawWireCap(bottom, radius, Vector3.down);

            // 圆柱段：上下两个圆环 + 竖向连接线（复用同一次圆周遍历）
            Vector3 prevTop = top + Vector3.right * radius;
            Vector3 prevBottom = bottom + Vector3.right * radius;
            for (int i = 1; i <= GizmoSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / GizmoSegments;
                Vector3 offset = (Mathf.Cos(angle) * Vector3.right + Mathf.Sin(angle) * Vector3.forward) * radius;
                Vector3 curTop = top + offset;
                Vector3 curBottom = bottom + offset;

                Gizmos.DrawLine(prevTop, curTop);
                Gizmos.DrawLine(prevBottom, curBottom);
                if(i%(GizmoSegments/4)==0) Gizmos.DrawLine(curTop, curBottom);

                prevTop = curTop;
                prevBottom = curBottom;
            }
        }

        /// <summary>绘制胶囊端盖：水平圆环 + 两条互相垂直的竖直半圆弧（使其呈现半球而非平盖）</summary>
        private void DrawWireCap(Vector3 center, float radius, Vector3 poleDir)
        {
            Vector3 prev = center + Vector3.right * radius;
            for (int i = 1; i <= GizmoSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / GizmoSegments;
                Vector3 cur = center + (Mathf.Cos(angle) * Vector3.right + Mathf.Sin(angle) * Vector3.forward) * radius;
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }

            DrawWireArc(center, radius, Vector3.right, poleDir);
            DrawWireArc(center, radius, Vector3.forward, poleDir);
        }

        /// <summary>在 axis1(水平起始方向) 与 poleDir(极点方向) 张成的平面上绘制半圆弧</summary>
        private void DrawWireArc(Vector3 center, float radius, Vector3 axis1, Vector3 poleDir)
        {
            int segments = GizmoSegments / 2;
            Vector3 prev = center + axis1 * radius;
            for (int i = 1; i <= segments; i++)
            {
                float t = i * Mathf.PI / segments;
                Vector3 cur = center + (Mathf.Cos(t) * axis1 + Mathf.Sin(t) * poleDir) * radius;
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }





        /*
/// <summary> 转向目标(需要每帧调调整</summary>
/// <param name="lookPosition"></param>
public void OrientTowards(Vector3 lookPosition,float speed)
{
   //计算两者的差值得到方向，投影??x,z)平面上得到y轴旋转方??
   Vector3 lookDirection = Vector3.ProjectOnPlane(lookPosition - transform.position, Vector3.up).normalized;
   if (lookDirection.sqrMagnitude != 0f)
   {
       Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
       transform.rotation =
           Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * speed);
   }
}
*/
    }


}
