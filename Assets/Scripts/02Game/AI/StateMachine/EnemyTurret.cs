using System.Collections.Generic;

using UnityEngine;

namespace FPSGame.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyTurret : AIInputUnitController<EnemyTurret.AIState>
    {
        protected EnemyController m_EnemyController;

        protected Vector3 TargetPosition => m_EnemyController.Target.Pos;

        public bool PreJudgment = true;

        [InspectorName("预判强度")]
        [Tooltip("预判位移的放大系数（对目标每帧位移 dx 的缩放），1=按目标位移等量预判，0=不预判。独立于转向速度")]
        public float PreJudgmentFactor = 1f;

        public enum AIState
        {
            Idle,
            Attack,
            Death,
        }

        private Vector3 lastTargetPos;//上一帧目标的位置

        [ContextMenu("重置")]
        private void ResetQu()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                if (turrets[i].barrelSetOffset.w == 0)
                {
                    Debug.LogError("炮塔的w=0    " + i);
                    turrets[i].barrelSetOffset = new Quaternion(turrets[i].barrelSetOffset.x, turrets[i].barrelSetOffset.y, turrets[i].barrelSetOffset.z, 1);
                }
            }
        }
        protected override void Start()
        {
            base.Start();
            AiState = AIState.Idle;
            m_EnemyController = m_Controller as EnemyController;
            //m_EnemyController.OnDie += OnDie;
        }

        protected override Dictionary<AIState, StateInfo> InitState()
        {
            return new Dictionary<AIState, StateInfo>
            {
                [AIState.Idle] = new StateInfo
                {
                    onUpdate = IdleBehavior,
                },
                [AIState.Attack] = new StateInfo
                {
                    onUpdate = AttackBehavior,
                },
                [AIState.Death] = new StateInfo(),
            };
        }

        protected override void OnDie()
        {
            SwitchState(AIState.Death);
            turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
        }
        protected override void UpdateTurretAiming()
        {
            if(AiState == AIState.Attack) base.UpdateTurretAiming();
        }

        /// <summary>Idle：对开启自动巡逻旋转的炮塔巡逻转动（未开启的自动跳过）</summary>
        private void IdleBehavior()
        {
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].AutoRotate(Time.deltaTime);
            }
        }

        /// <summary>Attack：瞄准并射击</summary>
        private void AttackBehavior()
        {
            if (m_EnemyController.Target.Pos == default) return;
            // shoot
            if (AimTargrt()) {
                for (int i = 0; i < turrets.Count; i++)
                {
                    var t = turrets[i];
                    // 需炮管实际转到目标附近(dot 达标)才开火，避免获得目标瞬间未瞄准就射击
                    if (t.weapon && t.IsLockTarget(TargetPosition) && t.CanFireAt(TargetPosition))
                        m_EnemyController.TryAtack(t.weapon);
                }
            }
            lastTargetPos = TargetPosition;
        }

        protected override void UpdateCurrentAiState()
        {
            if (AiState == AIState.Death) return;
            if (!m_EnemyController.BirthComplete) return;
            /*
            if (!m_EnemyController.KnownDetectedTarget && AiState != AIState.Idle)
            {
                OnLostTarget();
            }*/
            // 查表调用当前状态的行为
            InvokeCurrentState();
        }

        protected override void OnDetectedTarget()
        {
            if (AiState == AIState.Idle)
            {
                SwitchState(AIState.Attack);
            }
            m_TimeStartedDetection = Time.time;

        }

        protected override void OnLostTarget()
        {
            if (AiState != AIState.Death)
            {
                SwitchState(AIState.Idle);
                m_TimeLostDetection = Time.time;
                turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
            }
        }

        protected override bool AimTargrt()
        {
            bool mustShoot = false;
            foreach (var item in turrets) {
                if (mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay) break;
            }

            CalculationAimTargrt(PreJudgmentDirection());

            return mustShoot;
        }



        /// <summary>预判方向</summary>
        protected Vector3 PreJudgmentDirection()
        {

            var tar = TargetPosition;
           
            //Debug.DrawLine(transform.position, tar, Color.red, Time.deltaTime);
            //Debug.DrawLine(transform.position + 0.2f * Vector3.up, tar + turrets[0].aimSharpness * dx, Color.green, Time.deltaTime);
            if (PreJudgment)
            {
                var dx = tar - lastTargetPos;
                return tar + PreJudgmentFactor * dx;
            }
            else
            {
                return tar;
            }
        }

        protected override void UpdateAiStateTransitions()
        {
            
        }

        protected override void OnDamaged(Collider collider)
        {
            
        }

        /*
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (!m_EnemyController.KnownDetectedTarget) return;
            foreach (var item in turrets)
            {
                Vector3 chassisDir = Vector3.ProjectOnPlane((m_EnemyController.KnownDetectedTarget.transform.position - item.chassis.position), Vector3.up);
                Vector3 barrelDir = (m_EnemyController.KnownDetectedTarget.transform.position - item.barrel.position);

                Gizmos.DrawRay(item.chassis.position, Quaternion.LookRotation(chassisDir.normalized) * item.chassisOffset*Vector3.forward*20);
                Gizmos.DrawRay(item.barrel.position, Quaternion.LookRotation(barrelDir.normalized) * item.barrelOffset * item.barrelSetOffset * Vector3.forward*20);
                Gizmos.DrawWireSphere(m_EnemyController.KnownDetectedTarget.transform.position,0.5f);
            }   
        }
        */


    }

}
