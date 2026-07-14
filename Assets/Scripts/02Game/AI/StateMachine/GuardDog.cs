using System;
using System.Collections.Generic;
using GameContract;

using Unity.FPS.AI;
using UnityEngine;
using static Unity.FPS.AI.AIInputUnitController;

namespace FPSGame.AI
{
    internal class GuardDog : StateMachineFrame<GuardDog.AIState>, IEquippable
    {

        internal enum AIState
        {
            WaitOwner,
            Idle,
            Follow,
            Attack,
            Return,
        }


        [InspectorName("移动速度")]
        public float moveSpeed = 5f;
        [InspectorName("跟随范围")]
        public float floowRaudius;
        [InspectorName("出击范围")]
        public float attackRaudius;

       //public bool AttackStop;


        Vector3 targetPoint;
        private Vector3 _formationOffset;
        public float _formationOffsetAngle;
        #region


        public string ID =>m_actor.Id;

        public I_Actor Owner { get;private set; }
        private Func<IEnumerable<IEquippable>> GetEquippableList;
        public event Action<IEquippable> OnEquipDestroy;


        public void OnInstall(I_Actor actor, Func<IEnumerable<IEquippable>> getEquippableList)
        {
            GetEquippableList = getEquippableList;
            Owner = actor;
            AiState = AIState.Idle;
           

        }

        public void OnUninstall()
        {
            GetEquippableList = null;
            Owner = null;
            AiState = AIState.WaitOwner;
        }

        public bool NeedUninstall(IEquippable newEquip)
        {
            var list = new List<IEquippable>(GetEquippableList()).FindAll(item => item.ID.Contains("Dog"));
            var index = list.FindIndex(item => ReferenceEquals(item, this));
            Debug.LogError($"当前装备列表中共有{list.Count}个GuardDog，当前装备位于第{index}个");
            // 基础方向在Owner 右侧 45度，第n个偏移角度= 360/n
            int count = list.Count;
            float angle = 360f / count * (index + 1);
            Vector3 dir = Quaternion.AngleAxis(45f + angle, Vector3.up) * Owner.Forward;
            _formationOffset = dir.normalized * (floowRaudius - 2);
            _formationOffsetAngle = Mathf.DeltaAngle(0, 45f + angle);
            return newEquip.ID == ID;//ID相同时卸载
        }

        private void OnDestroy()
        {
            OnEquipDestroy?.Invoke(this);
        }

        #endregion
        #region 继承
        private CharacterController _controller;
        private Vector3 _moveVelocity = Vector3.zero;

        protected override void Init()
        {
            _controller = GetComponent<CharacterController>();
        }

        protected override void Uninit()
        {

        }
        protected override Dictionary<AIState, StateInfo> InitState()
        {
            return new() {
                [AIState.WaitOwner] = new() {
                    onEnter = StopWeapon,
                },
                [AIState.Idle] = new() {
                    onEnter = StopWeapon,
                    onUpdate = IdleUpdate,
                },
                [AIState.Follow] = new() {
                    onUpdate = FollowUpdate,
                },
                [AIState.Attack] = new() {
                    onUpdate = AttackUpdate,
                    onLateUpdate = AttackLateUpdate,
                },
                [AIState.Return] = new() {
                    onEnter = StopWeapon,
                    onUpdate = ReturnUpdate,
                },
            };
        }


        protected override void OnDetectedTarget()
        {
            m_TimeStartedDetection = Time.time;
            if (AiState != AIState.Return) AiState = AIState.Attack;
        }

        protected override void OnLostTarget()
        {
            m_TimeLostDetection = Time.time;
            if(AiState != AIState.Return) AiState = AIState.Follow;
        }
        #endregion

        bool InStopRaudius() => Vector3.Distance(m_actor.Pos, Owner.Pos) <= floowRaudius-2;
        bool InFloowRaudius() => Vector3.Distance(m_actor.Pos, Owner.Pos) <= floowRaudius;
        bool InAttackRaudius() => Vector3.Distance(m_actor.Pos, Owner.Pos) <= attackRaudius;

        #region WaitOwner

        #endregion

        #region Idle

        void StopWeapon()
        {
            turrets.ForEach(TryStop);
        }


        void IdleUpdate()
        {
            targetPoint = Owner.transform.TransformPoint(_formationOffset);
            MoveToTarget(0.05f);
            RotateToTarget(Owner.Pos + Owner.Forward * 10);
            if (!InFloowRaudius()) AiState = AIState.Follow;
        }
        #endregion

        #region Follow
        void FollowUpdate()
        {
            targetPoint = Owner.transform.TransformPoint(_formationOffset);
            MoveToTarget();
            RotateToTarget(Owner.Pos + Owner.Forward*10);

            if (Vector3.Distance(transform.position, targetPoint) < 0.25f) AiState = AIState.Idle;
        }

        #endregion

        #region Attack
        void AttackUpdate()
        {
            float dis = Vector3.Distance(DetectionTargetPos, m_actor.Pos);
            float stopRange = DetectionModule.AttackRange;
            bool mustStop = dis< stopRange;

            targetPoint = mustStop?m_actor.Pos: DetectionTargetPos;
            MoveToTarget();

            if (AimTargrt() && dis < DetectionModule.AttackRange && DetectionModule.IsSeeingTarget)
            {
                turrets.ForEach(item => {
                    if (item.IsLockTarget(DetectionTargetPos))
                    {
                        TryAtack(item);
                    }
                });
            }
            if (!InAttackRaudius()) AiState = AIState.Return;
        }

        protected bool AimTargrt()
        {
            bool mustShoot = false;
            foreach (var item in turrets)
            {
                if (mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay) break;
            }

            //设置锁定点
            turrets.ForEach(item => item.Look(DetectionTargetPos));
            return mustShoot;
        }

        /// <summary>炮台锁头</summary>
        void AttackLateUpdate()
        {
            turrets.ForEach(item => item.Aiming(Time.time - m_TimeStartedDetection));
        }



        #endregion

        #region Return
        void ReturnUpdate()
        {
            targetPoint = Owner.transform.TransformPoint(_formationOffset);
            MoveToTarget();
            RotateToTarget(Owner.Pos + Owner.Forward * 10);
            //这个过程中不会索敌
            if (InStopRaudius())AiState= AIState.Idle;

        }

        #endregion



        public void TryAtack(Turret turret)
        {
            if (!turret.weapon) return;
            float dis = Vector3.Distance(turret.barrel.position, DetectionTargetPos);
            if (dis <= turret.weapon.CurrentWeaponExtremeRange)
            {
                turret.weapon.ShootInputs(true, true, false);
            }
        }
        public void TryStop(Turret turret)
        {
            if (!turret.weapon) return;
            turret.weapon.ShootInputs(false, false, true);
        }


        /// <summary>
        /// 使用CharacterController 向目标移动
        /// </summary>
        private void MoveToTarget(float scale = 1)
        {
            if (targetPoint == default || _controller == null) return;

            float distance = Vector3.Distance(transform.position, targetPoint);

            if (distance < 0.25f)
            {
                // 直接到位（CC会处理碰撞）
                _controller.TryMove(targetPoint - transform.position);
                _moveVelocity = Vector3.zero;
                return;
            }

            // Idle 时用低速缓动，但若距离较远（玩家移动导致），自动提高
            float speedScale = scale;
            if (distance > 1.5f && scale < 1f)
            {
                speedScale = Mathf.Lerp(scale, 1f, Mathf.InverseLerp(1.5f, 5f, distance));
            }

            // SmoothDamp 计算期望速度
            Vector3 targetVelocity = Vector3.SmoothDamp(
                _moveVelocity,
                (targetPoint - transform.position).normalized * (moveSpeed * speedScale),
                ref _moveVelocity,
                0.2f,
                moveSpeed * speedScale
            );

            // 使用CharacterController 驱动位移
            _controller.TryMove(targetVelocity * Time.deltaTime);
        }

        /// <summary>
        /// 匀速向目标旋转
        /// </summary>
        private void RotateToTarget(Vector3 target)
        {
            Vector3 direction = (target - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                90 * Time.deltaTime
            );

            turrets.ForEach(item => item.Synchro());
        }



    }
}