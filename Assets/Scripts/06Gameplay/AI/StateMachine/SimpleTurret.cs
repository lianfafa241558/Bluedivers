using System.Collections.Generic;
using UnityEngine;

namespace FPSGame.AI
{

    /// <summary>
    /// 简易炮台的瞄准与开火逻辑。
    /// </summary>
    [AddComponentMenu("单位/_状态机/简易炮台")]
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
        protected override Dictionary<AIState, AiStateHook> InitState()
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
            for (int i = 0; i < turrets.Count; i++) TryStop(turrets[i]);
        }
        public void TryStop(Turret turret)
        {
            if (!turret.weapon) return;
            turret.weapon.ShootInputs(false, false, true);
        }

        void IdleUpdate()
        {
            // for 循环：ForEach(lambda) 每次调用都会新建一个委托（Idle 态下炮台每帧都在跑）
            float delta = RotationSpeed * Time.deltaTime;
            for (int i = 0; i < turrets.Count; i++)
            {
                turrets[i].Rotate(delta);
                turrets[i].Synchro();
            }
        }


        #endregion

        #region Attack
        void AttackUpdate()
        {
            if (AimTargrt())
            {
                for (int i = 0; i < turrets.Count; i++) TryAtack(turrets[i]);
            }
        }

        protected bool AimTargrt()
        {
            bool mustShoot = false;
            for (int i = 0; i < turrets.Count; i++)
            {
                if (mustShoot |= Time.time > m_TimeStartedDetection + turrets[i].detectionFireDelay) break;
            }

            //设置锁定目标（缓存一次目标点，别在循环里反复读属性）
            Vector3 aimPos = DetectionTargetPos;
            for (int i = 0; i < turrets.Count; i++) turrets[i].Look(aimPos);
            return mustShoot;
        }

        /// <summary>炮台锁头</summary>
        void AttackLateUpdate()
        {
            float blendTime = Time.time - m_TimeStartedDetection;
            for (int i = 0; i < turrets.Count; i++) turrets[i].Aiming(blendTime);
        }


        public void TryAtack(Turret turret)
        {
            if (!turret.weapon) return;
            if (!turret.CanFireAt(DetectionTargetPos)) return;
            // 按表面距离判射程：射程小于目标半径时，只按中心点算会永远"够不着"⇒ 贴身也不开火
            float dis = DetectionTargetSurfaceDis(turret.FirePosition);
            if (dis <= turret.weapon.CurrentWeaponExtremeRange)
            {
                turret.weapon.ShootInputs(true, true, false);
            }
        }
        #endregion

    }
}
