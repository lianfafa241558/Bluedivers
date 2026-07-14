using System.Collections;
using System.Collections.Generic;

using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using static Unity.FPS.AI.AIInputUnitController;

namespace FPSGame.AI
{

    internal class SimpleTurret : StateMachineFrame<SimpleTurret.AIState>
    {
        internal enum AIState
        {
            Idle,
            Attack,
        }

        [InspectorName("每秒旋转度数")]
        [SerializeField]
        private float RotationSpeed = 30f;

        #region 继承
        protected override void Init()
        {
            
        }

        protected override void Uninit()
        {
            
        }
        protected override Dictionary<AIState, StateInfo> InitState()
        {
            return new() {
                [AIState.Idle] = new(){
                    onEnter= IdleEnter,
                    onUpdate = IdleUpdate,
                },
                [AIState.Attack] = new() {
                    onUpdate = AttackUpdate,
                    onLateUpdate = AttackLateUpdate,
                },
            };
        }


        protected override void OnDetectedTarget()
        {
            m_TimeStartedDetection = Time.time;
            AiState = AIState.Attack;
        }

        protected override void OnLostTarget()
        {
            m_TimeLostDetection = Time.time;
            AiState = AIState.Idle;
        }
        #endregion

        #region Idle

        void IdleEnter()
        {
            turrets.ForEach(TryStop);
        }
        public void TryStop(Turret turret)
        {
            if (!turret.weapon) return;
            turret.weapon.ShootInputs(false, false, true);
        }

        void IdleUpdate()
        {
            turrets.ForEach(item => {
                item.Rotate(RotationSpeed * Time.deltaTime);
                item.Synchro();
            });
        }


        #endregion

        #region Attack
        void AttackUpdate()
        {
            if (AimTargrt())
            {
                turrets.ForEach(TryAtack);
            }
        }

        protected bool AimTargrt()
        {
            bool mustShoot = false;
            foreach (var item in turrets)
            {
                if (mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay) break;
            }

            //设置锁定目标
            turrets.ForEach(item => item.Look(DetectionTargetPos));
            return mustShoot;
        }

        /// <summary>炮台锁头</summary>
        void AttackLateUpdate()
        {
            turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
        }


        public void TryAtack(Turret turret)
        {
            if (!turret.weapon) return;
            float dis = Vector3.Distance(turret.barrel.position, DetectionTargetPos);
            if (dis <= turret.weapon.CurrentWeaponExtremeRange)
            {
                turret.weapon.ShootInputs(true, true, false);
            }
        }
        #endregion

    }
}