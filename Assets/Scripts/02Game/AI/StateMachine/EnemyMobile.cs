using System.Linq;
using Core;
using GameContract;
using Unity.FPS.Game;
using UnityEngine;
namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyMobile : AIInputUnitController
    {

        public bool AttackStop;
        protected EnemyController m_EnemyController;

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
        }




        /// <summary>状态机切换</summary>
        protected override void UpdateAiStateTransitions()
        {
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
        bool InAttackState()
        {
            bool re = false;
            turrets.ForEach(item => {
                re|=item.weapon.InAttackState();
            });
            return re;
        }
   
        /// <summary>状态机每帧</summary>
        protected override void UpdateCurrentAiState()
        {
            if (!m_EnemyController.BirthComplete) return;
            // Handle logic 
            switch (AiState)
            {

                case AIState.Idle:
                    //m_EnemyController.StopNav();
                    
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
                    m_EnemyController.SetNavDestination(TargetPosition);
                    AimTargrt();
                    break;
                case AIState.Attack:

                    float dis = Vector3.Distance(TargetPosition, m_EnemyController.CenterPos);
                    float stopRange = (AttackStopDistanceRatio * m_EnemyController.DetectionModule.AttackRange);
                    bool mustStop = AttackStop && InAttackState();
                    if (mustStop)
                    {
                        m_EnemyController.StopNav();
                    }
                    //如果目标到自己的距离大于停止系数*攻击范围，那就追，到范围就停
                    else if (dis >= stopRange + 1 / m_EnemyController.DetectionModule.AttackRange)//接近
                    {
                        m_EnemyController.SetNavDestination(TargetPosition);
                    }
                    else if (dis < stopRange - 1 / m_EnemyController.DetectionModule.AttackRange && MaintainMaxDis)//保持最大距离的敌人会在目标接近时远离
                    {
                        m_EnemyController.SetNavDestination(transform.position + (transform.position - TargetPosition).normalized);
                    }
                    else//原地
                    {
                        m_EnemyController.StopNav();
                    }

                    // shoot
                    if (mustStop)
                    {
                        turrets.ForEach(item => {
                            if (item.IsLockTarget(TargetPosition)) m_EnemyController.TryStop(item.weapon);
                        });
                    }
                    else if (AimTargrt())
                    {
                        turrets.ForEach(item => {
                            if (item.IsLockTarget(TargetPosition))
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
            }
            m_TimeStartedDetection = Time.time;
        }

        protected override void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                // 丢失目标时，如果有警惕点则前往警惕点
                var bewarePoint = m_EnemyController.DetectionModule.BewarePoint;
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

        /// <summary>炮台锁头(LateUpdate)</summary>
        protected override void UpdateTurretAiming()
        {
           if(AiState != AIState.Idle && AiState != AIState.Patrol && AiState != AIState.Death && AiState != AIState.Beware && AiState != AIState.Return) turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
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
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, -speedScale);
                }
                else if (AiState == AIState.Beware || AiState == AIState.Return)
                {
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, -speedScale);
                }

                //进入新状态
                if (state == AIState.Idle)
                {
                    m_IdleStartTime = Time.time;
                    m_EnemyController.StopNav();
                }
                else if (state == AIState.Patrol)
                {
                    speedScale = (PEMaths.PEInt)PatrolSpeed - m_EnemyController.Speed.FinalValue;
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, speedScale);
                }
                else if (state == AIState.Beware)
                {
                    var bewarePoint = m_EnemyController.DetectionModule.BewarePoint;
                    m_BewareDestination = bewarePoint.HasValue ? bewarePoint.Value : m_OriginPos;

                    speedScale = (PEMaths.PEInt)BewareSpeed - m_EnemyController.Speed.FinalValue;
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, speedScale);
                }
                else if (state == AIState.Return)
                {
                    speedScale = (PEMaths.PEInt)BewareSpeed - m_EnemyController.Speed.FinalValue;
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, speedScale);
                }

                AiState = state;
            }
            
        }
    }
}