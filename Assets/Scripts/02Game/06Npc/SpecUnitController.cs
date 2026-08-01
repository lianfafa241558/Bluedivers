using System.Collections;
using System.Collections.Generic;
using GameContract;
using PEMaths;

using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.AI
{

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
        /// <summary>
        /// 设置目标点
        /// </summary>
        /// <param name="destination"></param>
        public void SetNavDestination(Vector3 destination)
        {
            if (NavMeshAgent)
            {
                NavMeshAgent.SetDestination(destination);
            }
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