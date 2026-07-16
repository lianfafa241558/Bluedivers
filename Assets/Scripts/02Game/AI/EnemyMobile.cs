using GameContract;
using Unity.BaseTool;
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
            Patrol,
            Follow,
            Attack,
            Death,
        }


        [InspectorName("停止移动时攻击范围的系数")]
        [Tooltip("攻击范围指侦测模块的攻击范围")]
        [Range(0f, 1f)]
        public float AttackStopDistanceRatio = 0.5f;
        [InspectorName("保持在攻击距离范围上的距离")]//目标靠近就跑，远离就追
        public bool MaintainMaxDis = false;

        [InspectorName("巡逻速度")]
        public float PatrolSpeed = 2;

        public AIState AiState2 = AIState.Follow;

        public AIState AiState {
            get => AiState2;
            set
            {
                //if (enableDebug) Debug.LogError($"{gameObject},原状态{AiState2}，目标状态{value}", gameObject);

                AiState2 = value;
            }
        }

        protected override void Start()
        {
            base.Start();
            m_EnemyController = m_Controller as EnemyController;

            //m_EnemyController.OnDie += OnDie;
            // Start patrolling
            SwitchState(AIState.Patrol);

        }




        /// <summary>状态机切换</summary>
        protected override void UpdateAiStateTransitions()
        {
            // Handle transitions 
            switch (AiState)
            {
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

                case AIState.Patrol:
                    if (m_EnemyController.UpdatePathDestination())
                    {
                        //移除
                        m_EnemyController.Kill(true);
                    }
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
                        m_EnemyController.SetNavDestination(transform.position);
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
                        m_EnemyController.SetNavDestination(transform.position);
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
            if (AiState == AIState.Patrol)
            {
                SwitchState(AIState.Follow);
            }
            m_TimeStartedDetection = Time.time;
        }

        protected override void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                SwitchState(AIState.Patrol);
            }

            m_TimeLostDetection = Time.time;
            turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
            m_EnemyController.SetNavDestination(transform.position);
        }

        /// <summary>炮台锁头(LateUpdate)</summary>
        protected override void UpdateTurretAiming()
        {
           if(AiState!=AIState.Patrol&& AiState != AIState.Death) turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
        }

        protected override bool AimTargrt()
        {
            //m_EnemyController.m_Actor.OrientTowards(TargetPosition,turrets[0].aimSharpness);
            bool mustShoot = false;
            foreach(var item in turrets){
                if(mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay)break;
            }

            //计算我们炮塔的期望旋转（瞄准目标）
            //从炮口到目标的方向
            //KnownDetectedTarget就已经是aimpoint了
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
        //[SerializeField]
        //private bool enableDebug;
        private PEMaths.PEInt speedScale;
        private void SwitchState(AIState state)
        {
            if (state != AiState)
            {
                //if (enableDebug) Debug.LogError($"{gameObject},原状态{AiState}，目标状态{state}", gameObject);

                //进入巡逻
                if (state == AIState.Patrol)
                {
                    speedScale = (PEMaths.PEInt)PatrolSpeed - m_EnemyController.Speed.FinalValue;
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, speedScale);
                    //if(enableDebug) Debug.LogError($"{gameObject}进入巡逻，速度变化{speedScale}，最终速度{m_EnemyController.Speed.FinalValue}",gameObject);
                }
                //结束巡逻
                else if (AiState == AIState.Patrol)
                {
                    m_EnemyController.Speed.AddModifier(Game.ModifierType.Extra, -speedScale);
                    //if (enableDebug) Debug.LogError($"{gameObject}结束巡逻,速度变化{-speedScale}，最终速度{m_EnemyController.Speed.FinalValue}", gameObject);
                }


                AiState = state;
            }
            
        }
    }
}