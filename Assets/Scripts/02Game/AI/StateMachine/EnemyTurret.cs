using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AI
{
    [RequireComponent(typeof(EnemyController))]
    public class EnemyTurret : AIInputUnitController
    {
        protected EnemyController m_EnemyController;

        protected Vector3 TargetPosition => m_EnemyController.Target.Pos;

        public bool PreJudgment = true;

        //[InspectorName("每秒旋转度数")]
        public float RotationSpeed = 30f;

        public enum AIState
        {
            Idle,
            Attack,
            Death,
        }

        public AIState AiState;// { get; private set; }

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

        protected override void OnDie()
        {
            AiState = AIState.Death;
            turrets.ForEach(item => m_EnemyController.TryStop(item.weapon));
        }
        protected override void UpdateTurretAiming()
        {
            if(AiState == AIState.Attack) base.UpdateTurretAiming();
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
            // Handle logic 
            switch (AiState)
            {
                case AIState.Idle:
                    turrets.ForEach(item => {
                        item.Rotate(RotationSpeed*Time.deltaTime);
                        item.Synchro();
                    });
                    break;
                case AIState.Attack:
                    if (m_EnemyController.Target==null) break;
                    // shoot
                    if (AimTargrt()) {
                        turrets.ForEach(item => m_EnemyController.TryAtack(item.weapon));
                    }
                    lastTargetPos = TargetPosition;
                    break;
            }
        }

        protected override void OnDetectedTarget()
        {
            if (AiState == AIState.Idle)
            {
                AiState = AIState.Attack;
            }
            m_TimeStartedDetection = Time.time;

        }

        protected override void OnLostTarget()
        {
            if (AiState != AIState.Death)
            {
                AiState = AIState.Idle;
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
                return tar + turrets[0].aimSharpness * dx;
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