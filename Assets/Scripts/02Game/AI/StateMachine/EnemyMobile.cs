using System.Collections.Generic;
using System.Linq;
using Core;
using GameContract;
using Unity.FPS.Game;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public partial class EnemyMobile : AIInputUnitController
    {

        /// <summary>开火时停在原地且炮塔停止旋转：任一武器处于开火状态(蓄力/激光/射击/可连射)时，机体站桩、炮塔保持当前朝向不追踪；不开火时正常追敌并追踪瞄准</summary>
        [InspectorName("开火时停在原地")]
        [Tooltip("开启后：只要任一武器在开火(蓄力/激光/射击/可连射)，就停在原地且炮塔停止旋转(保持当前朝向)；不开火时正常追敌并追踪瞄准")]
        public bool AttackStop;
        protected EnemyController m_EnemyController;

        /// <summary>弱点受击僵直时长（秒），命中弱点后短暂无法攻击（不禁移动）</summary>
        [InspectorName("弱点受击僵直时长(秒)")]
        public float WeakPointHitStunDuration = 0.3f;

        /// <summary>弱点受击僵直是否生效</summary>
        private bool _hitStunActive;
        /// <summary>弱点受击僵直结束时间</summary>
        private float _hitStunEndTime;

        protected Vector3 TargetPosition => m_EnemyController.Target.Pos;

        protected TargetData target;

        public enum AIState
        {
            Idle,
            Patrol,
            Follow,
            Attack,
            Death,
            Beware,
            Return,
        }


        [InspectorName("停止移动时攻击范围的系数")]
        [Tooltip("攻击范围指侦测模块的攻击范围")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;
        [InspectorName("保持在攻击距离范围上的距离")]//目标靠近就跑，远离就追
        public bool MaintainMaxDis = false;

        [InspectorName("巡逻速度")]
        public float PatrolSpeed = 2;

        [InspectorName("警惕前往速度")]
        public float BewareSpeed = 3;

        /// <summary>警惕点到达判定半径</summary>
        [InspectorName("警惕到达半径")]
        public float BewareReachRadius = 2f;

        /// <summary>警惕到达后停留时间</summary>
        [InspectorName("警惕停留时间")]
        public float BewareStayDuration = 2f;

        /// <summary>初始巡逻点（返回状态回到这里）</summary>
        private Vector3 m_OriginPos;

        /// <summary>进入Beware时记录的目标点</summary>
        private Vector3 m_BewareDestination;

        /// <summary>回到起点的时间</summary>
        private float m_ReturnStartTime;

        /// <summary>进入Idle的时间</summary>
        private float m_IdleStartTime;

        [InspectorName("Idle最大停留时间（秒）")]
        public float IdleMaxDuration = 120f;

        public AIState AiState2 = AIState.Follow;

        public AIState AiState {
            get => AiState2;
            set
            {
                AiState2 = value;
            }
        }

        [ContextMenu("重置")]
        private void ResetQu()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                if (turrets[i].barrelSetOffset.w == 0)
                {
                    Debug.LogError("炮塔的w=0    "+i);
                    turrets[i].barrelSetOffset = new Quaternion(turrets[i].barrelSetOffset.x, turrets[i].barrelSetOffset.y, turrets[i].barrelSetOffset.z, 1);
                }
            }
        }
        protected override void Start()
        {
            base.Start();
            m_EnemyController = m_Controller as EnemyController;
            m_OriginPos = transform.position;

            // 有巡逻点就走巡逻，否则原地不动
            if (m_EnemyController.PatrolPos != default)
            {
                SwitchState(AIState.Patrol);
            }
            else
            {
                SwitchState(AIState.Idle);
            }

            InitAboStateListener();
            InitHitStunListener();
        }

        private void OnDestroy()
        {
            OnDestroyAboState();
            UninitHitStunListener();
        }

        /// <summary>订阅 Health.OnHit，命中弱点时触发受击僵直</summary>
        private void InitHitStunListener()
        {
            var health = m_EnemyController.GetComponent<Health>();
            if (health != null)
            {
                health.OnHit += OnHitStun;
            }
        }

        private void UninitHitStunListener()
        {
            var health = m_EnemyController != null ? m_EnemyController.GetComponent<Health>() : null;
            if (health != null)
            {
                health.OnHit -= OnHitStun;
            }
        }

        /// <summary>命中弱点：触发短僵直，期间无法攻击（不禁移动）。仅弱点命中触发</summary>
        private void OnHitStun(GameObject source, Vector3 pos, bool isWeakness)
        {
            if (!isWeakness) return;
            _hitStunActive = true;
            _hitStunEndTime = Time.time + WeakPointHitStunDuration;
        }




        /// <summary>状态机切换</summary>
        protected override void UpdateAiStateTransitions()
        {
            // 死亡后不再进行状态切换
            if (AiState == AIState.Death) return;
            // Vertigo/Terror 期间冻结状态切换
            if (_vertigoActive || _terrorActive) return;

            // Handle transitions 
            switch (AiState)
            {
                case AIState.Beware:
                    // 如果发现目标，清除警惕点并转为追逐
                    if (m_EnemyController.IsSeeingTarget)
                    {
                        m_EnemyController.DetectionModule.ClearBeware();
                        SwitchState(AIState.Follow);
                    }
                    // 到达警惕点后，停留一段时间然后返回
                    else if (Vector3.Distance(transform.position, m_BewareDestination) <= BewareReachRadius)
                    {
                        m_EnemyController.StopNav();
                        m_ReturnStartTime = Time.time;
                        SwitchState(AIState.Return);
                    }
                    break;

                case AIState.Return:
                    // 返回途中如果发现目标，清除警惕点并转为追逐
                    if (m_EnemyController.IsSeeingTarget)
                    {
                        m_EnemyController.DetectionModule.ClearBeware();
                        SwitchState(AIState.Follow);
                    }
                    // 停留时间结束，开始移动回原点
                    else if (Time.time >= m_ReturnStartTime + BewareStayDuration)
                    {
                        m_EnemyController.SetNavDestination(m_OriginPos);
                    }
                    // 回到起点后，根据是否有巡逻点决定状态
                    else if (Vector3.Distance(transform.position, m_OriginPos) <= BewareReachRadius)
                    {
                        m_EnemyController.StopNav();
                        TryReturnToIdleOrPatrol();
                    }
                    break;

                case AIState.Follow:
                    // 当与目标有视线连接时，转为攻击状态
                    if (m_EnemyController.IsSeeingTarget && m_EnemyController.IsTargetInAttackRange&& IsLockTarget()) {
                        SwitchState(AIState.Attack);
                        //在这里写移动没用，下一帧就改了
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_EnemyController.IsTargetInAttackRange||!m_EnemyController.IsSeeingTarget)
                    {
                        SwitchState(AIState.Follow);
                    }

                    break;
            }
        }

        private bool IsLockTarget() {
            foreach(var item in turrets) {
                if (item.IsLockTarget(TargetPosition)) {
                    return true;
                }
            }
            return false;
        }
        /// <summary>任一武器是否正在开火（蓄力/激光/射击中）。注意：不含"武器就绪可开火"(CanShoot)，
        /// 否则进入射程后 mustStop 恒为 true，炮塔会被永久冻结无法转过去对准目标</summary>
        bool IsFiringNow()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                WeaponEnemyController w = turrets[i].weapon;
                if (w != null && (w.InCharging || w.InLasering || w.InShoots))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>是否已过开火延迟（与 AimTargrt 判定一致：任一炮塔过了它的开火延迟即允许开火）</summary>
        bool IsFireDelayPassed()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                if (Time.time > m_TimeStartedDetection + turrets[i].detectionFireDelay)
                {
                    return true;
                }
            }
            return false;
        }
   
        /// <summary>状态机每帧</summary>
        protected override void UpdateCurrentAiState()
        {
            if (!m_EnemyController.BirthComplete) return;

            // 死亡后不再执行任何行为
            if (AiState == AIState.Death) return;

            // 弱点受击僵直超时清除
            if (_hitStunActive && Time.time >= _hitStunEndTime)
            {
                _hitStunActive = false;
            }

            // Vertigo：完全禁止移动，强制停止导航
            if (IsMoveLocked)
            {
                m_EnemyController.StopNav();
                return;
            }

            // Hacker：每帧尝试锁定同队伍单位
            UpdateHackerTarget();

            // Toxicity：乱走+持续攻击
            if (IsForcedAttack)
            {
                if (Time.time >= _toxicityWanderNextTime)
                {
                    RefreshToxicityWanderDestination();
                }
                m_EnemyController.SetNavDestination(_toxicityWanderDestination);
                // 持续攻击：无目标也尝试对炮台朝向方向开火
                turrets.ForEach(item =>
                {
                    if (item.weapon != null)
                    {
                        //中毒
                        m_EnemyController.TryAtack(item.weapon);
                    }
                });
                return;
            }

            // Terror：持续往远离伤害源方向跑，跳过原状态机逻辑
            if (_terrorActive)
            {
                m_EnemyController.SetNavDestination(_terrorFleeDestination);
                return;
            }

            // Handle logic 
            switch (AiState)
            {

                case AIState.Idle:
                    //m_EnemyController.StopNav();

                    // 开启自动巡逻旋转的炮塔（如机枪）在空闲时巡逻转动
                    UpdateAutoRotate();

                    // 非固定单位Idle超时后移动
                    if (!m_EnemyController.IsFixed && Time.time >= m_IdleStartTime + IdleMaxDuration)
                    {
                        if (ActorsManager.Players.Min(item => Vector3.Distance(item.Pos, m_EnemyController.Pos)) > 80)
                        {
                            m_EnemyController.Kill(true);
                        }
                    }
                    break;
                case AIState.Patrol:
                    // 开启自动巡逻旋转的炮塔在巡逻时转动
                    UpdateAutoRotate();
                    if (m_EnemyController.UpdatePathDestination())
                    {
                        //移除
                        m_EnemyController.Kill(true);
                    }
                    break;
                case AIState.Beware:
                    m_EnemyController.SetNavDestination(m_BewareDestination);
                    break;
                case AIState.Return:
                    m_EnemyController.SetNavDestination(m_OriginPos);
                    break;
                case AIState.Follow:
                    // 水平距离(寻路是地面导航，忽略高度差)减去目标半径：避免大型单位因身高把距离"抬高"，导致一直往目标脚下冲
                    float targetHalfFollow = m_EnemyController.Target.Actor?.HalfRange ?? 0f;
                    Vector3 toTargetFollow = TargetPosition - m_EnemyController.Pos;
                    toTargetFollow.y = 0f;
                    float followDis = toTargetFollow.magnitude - targetHalfFollow;
                    float followStopRange = AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange;
                    if (followDis >= followStopRange + 1)
                    {
                        m_EnemyController.SetNavDestination(TargetPosition);
                    }
                    else if (followDis < followStopRange - 1 && MaintainMaxDis)
                    {
                        m_EnemyController.SetNavDestination(transform.position + (transform.position - TargetPosition).normalized);
                    }
                    else
                    {
                        m_EnemyController.StopNav();
                    }

                    AimTargrt();
                    break;
                case AIState.Attack:

                    // 水平距离(寻路是地面导航，忽略高度差)减去目标半径：避免大型单位因身高把距离"抬高"，导致一直往目标脚下冲
                    float targetHalfAttack = m_EnemyController.Target.Actor?.HalfRange ?? 0f;
                    Vector3 toTargetAttack = TargetPosition - m_EnemyController.Pos;
                    toTargetAttack.y = 0f;
                    float dis = toTargetAttack.magnitude - targetHalfAttack;
                    float stopRange = (AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange);
                    bool mustStop = AttackStop && IsFiringNow();
                    if (mustStop)
                    {
                        m_EnemyController.StopNav();
                    }
                    //如果目标到自己的距离大于停止系数*攻击范围，那就追，到范围就停
                    else if (dis >= stopRange + 1)//接近
                    {
                        m_EnemyController.SetNavDestination(TargetPosition);
                    }
                    else if (dis < stopRange - 1 && MaintainMaxDis)//保持最大距离的敌人会在目标接近时远离
                    {
                        m_EnemyController.SetNavDestination(transform.position + (transform.position - TargetPosition).normalized);
                    }
                    else//原地
                    {
                        m_EnemyController.StopNav();
                    }

                    // shoot
                    if (IsAttackLocked)
                    {
                        // Vertigo/Terror：禁止攻击，停止武器
                        turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
                    }
                    else if (mustStop)
                    {
                        // 开火中且勾选 AttackStop：炮塔保持当前朝向不再追踪(不调 AimTargrt/Look)，
                        // 仅对已锁定的炮塔按开火延迟正常开火
                        if (IsFireDelayPassed())
                        {
                            turrets.ForEach(item => {
                                if (item.weapon && item.IsLockTarget(TargetPosition) && item.CanFireAt(TargetPosition))
                                {
                                    m_EnemyController.TryAtack(item.weapon);
                                }
                            });
                        }
                    }
                    else if (AimTargrt())
                    {
                        turrets.ForEach(item => {
                            if (item.weapon && item.IsLockTarget(TargetPosition) && item.CanFireAt(TargetPosition))
                            {
                                m_EnemyController.TryAtack(item.weapon);
                            }
                        });
                    }

                    break;
            }
        }


        protected override void OnDetectedTarget()
        {
            // 不管什么状态，发现目标都清空警惕点
            m_EnemyController.DetectionModule.ClearBeware();
            if (AiState == AIState.Idle || AiState == AIState.Patrol || AiState == AIState.Beware || AiState == AIState.Return)
            {
                SwitchState(AIState.Follow);
                // 首次从非战斗状态进入战斗才重置开火延迟计时
                m_TimeStartedDetection = Time.time;
            }
            // 战斗状态(Follow/Attack)下反复 OnDetect(目标短暂失去视野后重新看见、受击转火等)
            // 不再重置 m_TimeStartedDetection：
            // 否则开火延迟(detectionFireDelay)反复重新计时，期间 IsFireDelayPassed=false，
            // mustStop 分支跳过开火→武器被冻结在射击状态(InShoots)无法结束→卡死，表现为"开火被打断/等换弹完才开火"
        }

        protected override void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                // 丢失目标时，优先前往目标最后已知位置搜索；没有则退回警惕点（枪声/示警点）
                var lastKnown = m_EnemyController.DetectionModule.LastKnownTargetPos;
                var bewarePoint = lastKnown ?? m_EnemyController.DetectionModule.BewarePoint;
                if (bewarePoint.HasValue)
                {
                    m_BewareDestination = bewarePoint.Value;
                    SwitchState(AIState.Beware);
                }
                else
                {
                    // 无警惕点：有巡逻点就走巡逻，否则回出生点
                    if (m_EnemyController.PatrolPos != default)
                    {
                        SwitchState(AIState.Patrol);
                    }
                    else
                    {
                        m_EnemyController.SetNavDestination(m_OriginPos);
                        SwitchState(AIState.Return);
                    }
                }
            }

            m_TimeLostDetection = Time.time;
            turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
            m_EnemyController.StopNav();
        }

        /// <summary>回到起点后决定是Idle还是Patrol</summary>
        private void TryReturnToIdleOrPatrol()
        {
            if (m_EnemyController.PatrolPos != default)
            {
                SwitchState(AIState.Patrol);
            }
            else
            {
                SwitchState(AIState.Idle);
            }
        }

        /// <summary>
        /// 炮台锁头(LateUpdate)，每个炮台独立索敌：
        /// 战斗状态(Follow/Attack)下目标可达则瞄准；目标不可达或非战斗状态则自动巡逻转。
        /// </summary>
        protected override void UpdateTurretAiming()
        {
           if(AiState != AIState.Idle && AiState != AIState.Patrol && AiState != AIState.Death && AiState != AIState.Beware && AiState != AIState.Return) turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
        }

        /// <summary>对开启自动巡逻旋转的炮塔执行巡逻转动（未开启的自动跳过）</summary>
        private void UpdateAutoRotate()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].AutoRotate(Time.deltaTime);
            }
        }

        protected override bool AimTargrt()
        {
            bool mustShoot = false;
            foreach(var item in turrets){
                if(mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay)break;
            }

            CalculationAimTargrt(TargetPosition);

            return mustShoot;
        }

        protected override void OnDamaged(Collider collider)
        {

        }


        protected override void OnDie()
        {
            SwitchState(AIState.Death);
            turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
        }

        private PEMaths.PEInt speedScale;
        private void SwitchState(AIState state)
        {
            if (state != AiState)
            {
                //退出旧状态
                if (AiState == AIState.Patrol)
                {
                    m_EnemyController.Speed?.AddModifier(Game.ModifierType.Extra, -speedScale);
                }
                else if (AiState == AIState.Beware || AiState == AIState.Return)
                {
                    m_EnemyController.Speed?.AddModifier(Game.ModifierType.Extra, -speedScale);
                }

                //进入新状态
                if (state == AIState.Idle)
                {
                    m_IdleStartTime = Time.time;
                    m_EnemyController.StopNav();
                }
                else if (state == AIState.Patrol && m_EnemyController.Speed != null)
                {
                    speedScale = (PEMaths.PEInt)PatrolSpeed - m_EnemyController.Speed.FinalValue;
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, speedScale);
                }
                else if (state == AIState.Beware && m_EnemyController.Speed != null)
                {
                    var bewarePoint = m_EnemyController.DetectionModule.BewarePoint;
                    m_BewareDestination = bewarePoint.HasValue ? bewarePoint.Value : m_OriginPos;

                    speedScale = (PEMaths.PEInt)BewareSpeed - m_EnemyController.Speed.FinalValue;
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, speedScale);
                }
                else if (state == AIState.Return && m_EnemyController.Speed != null)
                {
                    speedScale = (PEMaths.PEInt)BewareSpeed - m_EnemyController.Speed.FinalValue;
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, speedScale);
                }

                AiState = state;
            }
            
        }

    }
}