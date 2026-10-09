using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;
using PEMaths;

using FPSGame.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using FPSGame.Utils;
using FPSGame.Gameplay;
using FPSGame.AI;
using FPSGame.Weapon;

namespace FPSGame.Gameplay
{

    /// <summary>
    /// 特殊单位（Kei 等）的 AI 控制器。
    /// </summary>
    [AddComponentMenu("AI/特殊单位控制器")]
    public class SpecUnitController : AIController
    {

        public TargetData KnownDetectedTarget => DetectionModule.Target;
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        /// <summary>目标是否可见</summary>
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;
        public override Vector3 Velocity => NavMeshAgent ? NavMeshAgent.velocity : Vector3.zero;

        public NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }

        Transform EyePoint;


        protected override void InitComponent()
        {
            base.InitComponent();
            NavMeshAgent = GetComponent<NavMeshAgent>();
            DetectionModule = GetComponentInChildren<DetectionModule>();


            if (DetectionModule.IsValid())
            {
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
                    [UnitAttrType.Speed] = (PEInt)NavMeshAgent.speed,
                    [UnitAttrType.AngularSpeed] = (PEInt)NavMeshAgent.angularSpeed,
                    [UnitAttrType.Size] = (PEInt)m_Actor.HalfRange,
                });
                var Speed = GetAttribute(UnitAttrType.Speed);
                if (Speed.PrimeValue > 0) Speed.OnFinalValueChange += (value) => { NavMeshAgent.speed = value.RawFloat; };
                var AngularSpeed = GetAttribute(UnitAttrType.AngularSpeed);
                if (AngularSpeed.PrimeValue > 0) Speed.OnFinalValueChange += (value) => { NavMeshAgent.angularSpeed = value.RawFloat; };
            }

        }
        /// <summary>联机落点组件（2026-10-08 接入）：目标点的缓存 / 重试在它里面。</summary>
        private SyncedNavMover _mover;

        private SyncedNavMover Mover
        {
            get
            {
                if (_mover == null)
                {
                    if (!TryGetComponent(out _mover)) _mover = gameObject.AddComponent<SyncedNavMover>();
                    // 特殊单位在大厅也要能动 ⇒ 不注入 applyHandler（默认 = NavMeshAgent.SetDestination）
                }
                return _mover;
            }
        }

        /// <summary>
        /// 设置目标点
        /// <para>▍对凯伊为什么是真修复：它的落点是**网络上重放**过来的（4038 <c>CallKai</c>），
        /// 而重放那一刻这只凯伊可能还没 Warp 上导航网格 ⇒ 旧实现 <c>SetDestination</c> 静默失败、
        /// 这条呼叫就没了；现在会先存下来，等 agent 就绪再落地。</para>
        /// </summary>
        /// <param name="destination"></param>
        public void SetNavDestination(Vector3 destination)
        {
            Mover.SetDestination(destination);
        }
        /// <summary>尝试使用某个武器攻击</summary>
        public bool TryAtack(WeaponEnemyController weapon)
        {
           
            bool didFire = false;
            float dis = Vector3.Distance(EyePoint.position, KnownDetectedTarget.Pos);
            if (dis <= weapon.CurrentWeaponExtremeRange)
            {
                didFire |= weapon.HandleShootInputs(false, true, false);
            }

            if (didFire && OnAttack != null)
            {
                OnAttack?.Invoke(weapon);
            }
            return didFire;
        }
        /// <summary>使某个武器停止攻击</summary>
        public void TryStop(WeaponEnemyController weapon)
        {
            weapon.ShootInputs(false, false, true);
        }


    }
}
