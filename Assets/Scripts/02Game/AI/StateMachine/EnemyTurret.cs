using System.Collections.Generic;

using UnityEngine;

namespace FPSGame.AI
{
    //[RequireComponent(typeof(EnemyController))]
    public class EnemyTurret : AIInputUnitController<EnemyTurret.AIState>
    {
        protected EnemyController m_EnemyController;

        protected Vector3 TargetPosition => m_EnemyController.Target.Pos;

        public bool PreJudgment = true;

        [InspectorName("预判强度")]
        [Tooltip("预判位移的放大系数（对目标每帧位移 dx 的缩放），1=按目标位移等量预判，0=不预判。独立于转向速度")]
        public float PreJudgmentFactor = 1f;

        /// <summary>警惕查看时长(秒)：听到枪声/示警后炮塔朝该方向看这么久，超时恢复自动巡逻</summary>
        [InspectorName("警惕查看时长(秒)")]
        [Tooltip("听到枪声/示警(DetectionModule.BewarePoint)后，炮塔朝该方向看这么久；超时后清除警惕点并恢复自动巡逻转动。<=0 表示不响应")]
        public float BewareLookDuration = 3f;

        public enum AIState
        {
            Idle,
            Attack,
            Death,
        }

        private Vector3 lastTargetPos;//上一帧目标的位置

        /// <summary>是否正在朝警惕方向看（Idle 期间）</summary>
        private bool _bewareLooking;
        /// <summary>朝警惕方向看的结束时间</summary>
        private float _bewareLookEndTime;

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
            // Attack 追踪目标；Idle 期间"警惕查看"同样应用 Look() 算出的目标旋转（炮台不能走动，只转头看）
            if (AiState == AIState.Attack || (AiState == AIState.Idle && _bewareLooking)) base.UpdateTurretAiming();
        }

        /// <summary>Idle：听到枪声先朝警惕方向看一会儿，否则对开启自动巡逻旋转的炮塔巡逻转动</summary>
        private void IdleBehavior()
        {
            // 正在警惕查看时不自动巡逻转动，避免两个旋转互相打架
            if (UpdateBewareLook()) return;

            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].AutoRotate(Time.deltaTime);
            }
        }

        /// <summary>
        /// 警惕查看：逻辑层噪声(BattleEventSub.OnNoise)把枪声/弹着点写进 BewarePoint 后，炮塔朝该方向看 BewareLookDuration 秒。
        /// 超时就把警惕点消费掉并恢复自动巡逻（炮台不会走过去，所以只能"看向"目标方向）。
        /// </summary>
        /// <returns>true=本帧正在警惕查看（调用方不要再自动巡逻转动）</returns>
        private bool UpdateBewareLook()
        {
            var module = m_EnemyController != null ? m_EnemyController.DetectionModule : null;
            if (module == null || BewareLookDuration <= 0f || !module.BewarePoint.HasValue)
            {
                _bewareLooking = false;
                return false;
            }

            // 太远的点无视：超过"听力/视野取较大者"就不看（也不消费，交给后续判定）
            var bewarePoint = module.BewarePoint.Value;
            float senseRange = Mathf.Max(module.HearingRange, module.DetectionRange);
            if (Vector3.Distance(transform.position, bewarePoint.RawVector3) > senseRange)
            {
                _bewareLooking = false;
                return false;
            }

            if (!_bewareLooking)
            {
                _bewareLooking = true;
                _bewareLookEndTime = Time.time + BewareLookDuration;
            }
            // 到点：消费掉警惕点，否则恢复巡逻后会被同一个点反复拉回来
            if (Time.time > _bewareLookEndTime)
            {
                _bewareLooking = false;
                module.ClearBeware();
                return false;
            }

            // 只取水平方向：不要把炮管压到地面上的落点上
            Vector3 lookAt = bewarePoint.RawVector3;
            lookAt.y = transform.position.y;
            CalculationAimTargrt(lookAt);
            return true;
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
                // 回到 Idle 时重新开始计时：否则警惕查看会沿用上一轮已过期的结束时间，刚开打就被判定"看完了"
                _bewareLooking = false;
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
