using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Attributes;
using FPSGame.GameContract;
using PEMaths;

using Unity.Burst.CompilerServices;
using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using FPSGame.Utils;
using FPSGame.Gameplay;
using FPSGame.Weapon;

namespace FPSGame.AI
{




    //[RequireComponent(typeof(HealthEnemy), typeof(Actor))]
    /// <summary>
    /// 敌人 AI 主控制器，负责属性初始化与行为驱动。
    /// </summary>
    [AddComponentMenu("AI/敌人控制器")]
    public partial class EnemyController : AIController
    {
        /// <summary>同一目标点的请求失败后的最小重发间隔(秒)：坏目标不要每帧重算路径</summary>
        private const float NavRetryInterval = 0.5f;

        public override Vector3 Velocity => NavMeshAgent ? NavMeshAgent.velocity : Vector3.zero;

        public string EnemyName => m_Actor.ShowName;

        public Sprite Portrait => m_Actor.Portrait;
        public Sprite Halo => m_Actor.ExtraPortrait;

        public bool IsFixed => m_Actor.IsFixed;

        public bool Boss => m_Actor.HasFlag(ActorFlag.Boss);

        public override Vector3 Pos
        {
            get => base.Pos;
            set
            {
                if (FpsHelper.HaveNavMeshAgent(NavMeshAgent)) NavMeshAgent.Warp(value);
                else base.Pos = value;
            }
        }

        [InspectorName("敌人认为已到达当前路径目标点的距?")]
        public float PathReachingRadius = 2f;

        /// <summary>联机时"移动由房主决定"（成员端为 true）：本端 AI 不再自己决定目标点，只应用下发的。
        /// 由 09 的战斗侧在开局时按"是不是房主"置位。</summary>
        public static bool RemoteDrivenMovement;

        /// <summary>正在应用"网络上来的"伤害（房主结算成员上报的命中）⇒ 不再回传，避免回声。</summary>
        public static bool ApplyingRemoteDamage;

        public bool BirthComplete=>Time.time>=birthTime+BirthDuration;

        /// <summary>巡逻目标点</summary>
        public Vector3 PatrolPos { get; set; }

        /// <summary>
        /// 长期落点（波次空投等的待命点）：到达后不移除，期间目标被中断(回Idle)会继续走向该点。
        /// 区别于 PatrolPos：PatrolPos 是巡逻队"到达即自毁"的哨兵点，HomePoint 是"到达即待命"的落点。
        /// default 表示未设置。
        /// </summary>
        public Vector3 HomePoint { get; set; }

        /// <summary>当前的目??的AimPoint)</summary>
        public TargetData Target => DetectionModule.Target;
        /// <summary>目标是否进入攻击范围</summary>
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        /// <summary>目标是否可见</summary>
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;

        public GameAttribute Speed=>GetAttribute(UnitAttrType.Speed);




        protected NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }




 
        Collider[] m_SelfColliders;

        //[DisplayField]
        [SerializeField]
        public Vector3 m_lastDestination;
        private NavMeshPath m_lastPath;

        /// <summary>同一目标点下一次允许重发的时间（请求失败/被挡掉后按 <see cref="NavRetryInterval"/> 节流重试）</summary>
        private float _nextNavRetryTime;

        /// <summary>网络下发但**当帧应用不了**的目标点（见 <see cref="ApplyRemoteDestination"/>）。</summary>
        private Vector3 _pendingRemoteDestination;

        /// <summary>是否有待落地的网络目标点（agent 被禁用时置位，由 <see cref="Update"/> 重试到成功）。</summary>
        private bool _hasPendingRemoteDestination;



        private Transform EyePoint;

        protected override void InitComponent() 
        {
            base.InitComponent();
          
            NavMeshAgent = GetComponent<NavMeshAgent>();
            m_SelfColliders = GetComponentsInChildren<Collider>();

            if(NavMeshAgent) NavMeshAgent.updateRotation = true;
            //NavMeshAgent.enabled = true;
            //NavMeshAgent.Warp(transform.position);
            /*
            if (NavMesh.SamplePosition(new(transform.position.x, 0, transform.position.z), out var hit, 200, NavMesh.AllAreas))
            {
                NavMeshAgent.Warp(hit.position);
                //transform.position = hit.position;
            }*/

            DetectionModule = GetComponentInChildren<DetectionModule>();
            if (DetectionModule.IsValid()) {
                DetectionModule.SetActor((Actor)m_Actor);
                DetectionModule.onDetectedTarget += _OnDetectedTarget;
                DetectionModule.onLostTarget += _OnLostTarget;
                OnAttack += DetectionModule.OnAttack;
                EyePoint = DetectionModule.GetCorePoint();
            }
            else
            {
                EyePoint = AimPoint;
            }

         }

        public override void InitAttribute()
        {
            if (!FpsHelper.HaveNavMeshAgent(NavMeshAgent))
            {
                base.InitAttribute();
            }
            else
            {
                attrs = UnitAttributeFactory.CreateBaseUnit(new Dictionary<UnitAttrType, PEInt> {
                    [UnitAttrType.Speed] = (PEInt)NavMeshAgent.speed ,
                    [UnitAttrType.AngularSpeed] = (PEInt)NavMeshAgent.angularSpeed,
                    [UnitAttrType.Size] = (PEInt)m_Actor.HalfRange,
                });
                var Speed = GetAttribute(UnitAttrType.Speed);
                if (Speed.PrimeValue > 0) Speed.OnFinalValueChange += (value) => { NavMeshAgent.speed = value.RawFloat; };
                var AngularSpeed = GetAttribute(UnitAttrType.AngularSpeed);
                if (AngularSpeed.PrimeValue > 0) Speed.OnFinalValueChange += (value) => { NavMeshAgent.angularSpeed = value.RawFloat; };
            }

        }
        protected override void Start()
        {
            base.Start();

            FindAndInitializeAllWeapons();
            Invoke(nameof(BirthEnd), BirthDuration+0.1f);
            Speed.AddModifier(ModifierType.Factor,-1);
        }


        void Update()
        {
            EnsureIsWithinLevelBounds();

            // 结算外力推挤(爆炸/踩踏击退，见 EnemyController_Physical.cs)
            UpdateKnockback();

            // 补发"早到/当时 agent 不可用"的网络目标点（见 ApplyRemoteDestination）
            RetryPendingRemoteDestination();

            //DetectionModule?.HandleTargetDetection();

        }

        void BirthEnd()
        {
            if (m_lastDestination != default)
            {
                var target = m_lastDestination;
                m_lastDestination = default;
                if (isImportant) Debug.LogError("重置目标点",gameObject);
                SetNavDestination(target);
            }
            Speed.AddModifier(ModifierType.Factor, 1);
        }


        void EnsureIsWithinLevelBounds()
        {
            // at every frame, this tests for conditions to kill the enemy
            if (transform.position.y < Constants.KillHeight)
            {
                Tool.Destroy(gameObject);
                return;
            }
        }


        /// <summary>
        /// 更新巡逻路径
        /// </summary>
        /// <param name="inverseOrder"></param>
        public bool UpdatePathDestination()
        {
            if (PatrolPos!=default)
            {
                //检查是否到达路径目标点
                if ((Pos - PatrolPos).ToVector2().magnitude <= PathReachingRadius)
                {
                    return true;
                }
                SetNavDestination(PatrolPos);

                if (FpsHelper.HaveNavMeshAgent(NavMeshAgent) && NavMeshAgent.hasPath)
                {
                    Vector3 steerDir = NavMeshAgent.steeringTarget - transform.position;
                    steerDir.y = 0;
                    if (steerDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.LookRotation(steerDir);
                    }
                }
            }
            return false;
        }


        [SerializeField]
        bool isImportant;

        /// <summary>
        /// 设置目标点（通过PathRequestManager排队，避免单帧寻路瓶颈
        /// </summary>
        public void SetNavDestination(Vector3 destination)
        {
            // ★ 联机成员：移动意图由房主下发（见 ApplyRemoteDestination），本端 AI 的决策一律作废 ——
            //   否则两端各追各的目标，怪物位置永远对不上（2026-10-07 用户实测）。
            if (RemoteDrivenMovement) return;

            bool agentReady = FpsHelper.HaveNavMeshAgent(NavMeshAgent);
            bool sameTarget = Vector3.Distance(destination, m_lastDestination) < 1;

            if (sameTarget)
            {
                // ⚠ 只有"路径确实已存在/正在计算"才算这个目标已经生效，可以直接跳过；
                // 旧实现无条件按 <1m 去重，于是"曾经失败过/被挡住过"的目标会被记成"已完成"，
                // 同一个目标点永远不再请求（单位永久发呆：状态机在巡逻，NavMeshAgent 却 hasPath=false）
                if (agentReady && (NavMeshAgent.pathPending || NavMeshAgent.hasPath)) return;

                // 同一个目标点的重复请求做节流：失败的目标不要每帧重算路径
                if (Time.time < _nextNavRetryTime) return;
            }
            _nextNavRetryTime = Time.time + NavRetryInterval;

            if (isImportant) Debug.LogError("设置目标点为" + destination + "旧目标" + m_lastDestination, gameObject);
            m_lastDestination = destination;

            if (isImportant) Debug.LogError("设置目标点成功" + destination + "和" + m_lastDestination, gameObject);

            if (agentReady)
            {
                if (BirthComplete)
                {
                    if (isImportant) Debug.LogError("发送目标点" + destination, gameObject);

                    //走事件总线（2026-10-01 取代 ServiceLocator.Path）：订阅方 = PathRequestManager（09_Managers）
                    FPSGame.Game.UnitEventSub.PathRequest(NavMeshAgent, destination, isImportant);
                    // ★ 房主：把"这只怪要去哪"广播给成员（成员按同一个目标点各自本地算路径 ⇒ 位置接近一致）
                    if (m_Actor != null && m_Actor.NetId != 0)
                        FPSGame.Gameplay.BattleEventSub.EnemyMove(m_Actor.NetId, destination);
                }
            }
        }


        /// <summary>【网络下发】应用房主给的移动目标：**绕开**本端的去重/节流（那套判据基于本地状态，两端不一定同步命中）。
        /// <para>▍为什么要缓存重试：成员的移动**全靠这条**下发，而房主只在"目标变化"时发（`&lt;1m` 去重）⇒
        /// 丢掉一条就再也不会来第二条，那只怪会一直原地不动。收到时 agent 正好被禁用（空投单位在落地前
        /// 整套 Behaviour 都是关的）就会丢 ⇒ 先存下来，等 agent 启用后在 <see cref="Update"/> 里补发。</para>
        /// ⚠ 只由 09 的联机桥调用。</summary>
        public void ApplyRemoteDestination(Vector3 destination)
        {
            m_lastDestination = destination;
            _pendingRemoteDestination = destination;
            _hasPendingRemoteDestination = true;
            RetryPendingRemoteDestination();
        }

        /// <summary>把待落地的网络目标点补发出去；成功后清标记（失败时每帧只做一次状态判断，开销可忽略）。</summary>
        private void RetryPendingRemoteDestination()
        {
            if (!_hasPendingRemoteDestination) return;
            if (TryRequestPath(_pendingRemoteDestination)) _hasPendingRemoteDestination = false;
        }

        /// <summary>agent 真正可用时才发寻路请求。
        /// <para>⚠ 只拦"agent 被禁用"这一种（<c>SetDestination</c> 对它必然失败、还会刷错误日志）；
        /// 其余情况（含离网格/飞行单位）保持原行为 —— 交给 <c>PathRequestManager</c> 的投影兜底处理。</para></summary>
        /// <returns>true = 本次算处理完（无论 agent 存不存在），false = agent 暂不可用、需要重试</returns>
        private bool TryRequestPath(Vector3 destination)
        {
            if (!FpsHelper.HaveNavMeshAgent(NavMeshAgent)) return true;
            if (!NavMeshAgent.isActiveAndEnabled) return false;
            FPSGame.Game.UnitEventSub.PathRequest(NavMeshAgent, destination, false);
            return true;
        }

        public void StopNav()
        {
            if (FpsHelper.HaveNavMeshAgent(NavMeshAgent))
            {
                NavMeshAgent?.ResetPath();
            }
            if(isImportant)Debug.LogError("取消了目标点",gameObject);
            m_lastDestination = default;
        }

        protected override void _OnDamaged(PEInt damage, GameObject damageSource, Collider collider,bool noSource)
        {
            // ★ 联机成员：本机的命中只**上报**给房主结算（血量/死亡以房主为唯一权威）；
            //   应用远端伤害时（房主侧）不再回传，避免回声。
            if (!ApplyingRemoteDamage && RemoteDrivenMovement && m_Actor != null && m_Actor.NetId != 0)
                FPSGame.Gameplay.BattleEventSub.EnemyHit(m_Actor.NetId, Mathf.RoundToInt(damage.RawFloat));
            
            if (damageSource &&damageSource.GetComponent<Actor>().Type != UnitTypeEnum.Other&& !damageSource.GetComponent<Actor>().HasFlag(ActorFlag.Invincible))
            {
                DetectionModule?.OnDamaged(damageSource, noSource);
                OnDamaged?.Invoke(collider);
            }
            if (!noSource&&FPSGame.Data.FlowState.GameState == GameStateEnum.Game && damageSource.TryGetComponent(out PlayerController player)) FPSGame.Gameplay.BattleEventSub.AddBattleDataItem(player.PlayerIndex, "命中次数");
        }

        protected override void _OnDie(GameObject source)
        {
            base._OnDie(source);
            _hasPendingRemoteDestination = false;   // 已死：agent 会被禁用，别再每帧去补发
            if (FpsHelper.HaveNavMeshAgent(NavMeshAgent))
            {
                NavMeshAgent.isStopped = true;
                NavMeshAgent.enabled = false;
            }
            if (DetectionModule.IsValid())
            {
                DetectionModule.enabled = false;
            }
            Speed.AddModifier(ModifierType.Factor, -1);

            if (FPSGame.Data.FlowState.GameState == GameStateEnum.Game && source && m_Actor.Team != 1)
            {
                PlayerController player;
                if (source.TryGetComponent(out Actor actor) && actor.Owner != null) actor.Owner.transform.TryGetComponent(out player);
                else source.TryGetComponent(out player);
                //房主序号走「数据自持」（2026-10-01 取代 ServiceLocator.Room）：TeamManager 在其 4 个变更点同步 TeamState
                int masterIndex = FPSGame.Data.TeamState.MasterIndex;
                FPSGame.Gameplay.BattleEventSub.AddBattleDataItem(player ? player.PlayerIndex : masterIndex, "击杀敌人");
            }

        }


        public bool TryAtack(WeaponEnemyController weapon) {
            bool didFire = false;
            // 自体炮塔等无武器炮台直接跳过，避免 NRE
            if (!weapon) return false;
            float dis = Vector3.Distance(EyePoint.position,Target.Pos);
            if (dis <= weapon.CurrentWeaponExtremeRange) {
                didFire |= weapon.HandleShootInputs(true, true, false);
            }
            //Debug.LogWarning(gameObject+"使用"+weapon+"攻击"+"触发"+ didFire+"事件"+(OnAttack != null),gameObject);
            if (didFire && OnAttack != null) {
                OnAttack?.Invoke(weapon);
            }
            return didFire;
        }
        public void TryStop(WeaponEnemyController weapon)
        {
            if (!weapon) return;
            weapon.ShootInputs(false, false, true);
        }


        /// <summary>
        /// 为所有武器绑定归属与忽略自身碰撞体
        /// </summary>
        void FindAndInitializeAllWeapons()
        {
            var weapons = GetComponentsInChildren<WeaponBaseController>();
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].Owner = gameObject;
                weapons[i].IgnoredColliders = m_SelfColliders;
            }
        }

        /// <summary>
        /// 没有侦测组件的控制器什么都不做
        /// </summary>
        /// <param name="point">警惕点(要去查看的噪声点)</param>
        /// <param name="noise">该噪声的响度(半径/米)：比当前警惕点更轻时不覆盖</param>
        /// <param name="spread">是否把同一噪声扩散给附近队友</param>
        public override void Beware(PEVector3 point, PEInt noise, bool spread)
        {
            DetectionModule.Beware(point, noise, spread);
        }





        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, m_lastDestination);
            Gizmos.DrawWireSphere(m_lastDestination, 1f);

            if (NavMeshAgent == null || !NavMeshAgent.hasPath) return;

            NavMeshPath path = NavMeshAgent.path;
            Vector3[] corners = path.corners;

            if (corners.Length < 2) return;

            // 用红线绘制完整路径
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(corners[i], corners[i + 1]);

                // 在每个拐点画小圆
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(corners[i], 0.2f);
            }

            // 用绿色标记下一个转向点
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(NavMeshAgent.steeringTarget, 0.3f);

            // 用蓝色标记最终目标
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(NavMeshAgent.destination, 0.4f);
        }
    }
}
