using System.Collections.Generic;
using Core;
using FPSGame.Attribute;
using GameContract;
using PEMaths;

using Unity.Burst.CompilerServices;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Utils;

namespace FPSGame.AI
{




    [RequireComponent(typeof(HealthEnemy), typeof(Actor))]
    public partial class EnemyController : AIController
    {
        public override Vector3 Velocity => NavMeshAgent ? NavMeshAgent.velocity : Vector3.zero;

        public string EnemyName => m_Actor.ShowName;

        public Sprite Portrait => m_Actor.Portrait;
        public Sprite Halo => m_Actor.ExtraPortrait;

        public bool IsFixed => m_Actor.IsFixed;

        public bool Boss => m_Actor.HasFlag(ActorFlag.Boss);

        public override Vector3 Pos
        {
            get => base.Pos;
            set
            {
                if (FpsHelper.HaveNavMeshAgent(NavMeshAgent)) NavMeshAgent.Warp(value);
                else base.Pos = value;
            }
        }

        [InspectorName("敌人认为已到达当前路径目标点的距?")]
        public float PathReachingRadius = 2f;

        public bool BirthComplete=>Time.time>=birthTime+BirthDuration;

        /// <summary>巡逻目标点</summary>
        public Vector3 PatrolPos { get; set; }

        /// <summary>当前的目??的AimPoint)</summary>
        public TargetData Target => DetectionModule.Target;
        /// <summary>目标是否进入攻击范围</summary>
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        /// <summary>目标是否可见</summary>
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;

        public GameAttribute Speed=>GetAttribute(UnitAttrType.Speed);




        protected NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }




 
        Collider[] m_SelfColliders;

        //[Display(true, false, true)]
        private Vector3 m_lastDestination;
        private NavMeshPath m_lastPath;



        private Transform EyePoint;

        protected override void InitComponent() 
        {
            base.InitComponent();
          
            NavMeshAgent = GetComponent<NavMeshAgent>();
            m_SelfColliders = GetComponentsInChildren<Collider>();

            if(NavMeshAgent) NavMeshAgent.updateRotation = true;
            //NavMeshAgent.enabled = true;
            //NavMeshAgent.Warp(transform.position);
            /*
            if (NavMesh.SamplePosition(new(transform.position.x, 0, transform.position.z), out var hit, 200, NavMesh.AllAreas))
            {
                NavMeshAgent.Warp(hit.position);
                //transform.position = hit.position;
            }*/

            DetectionModule = GetComponentInChildren<DetectionModule>();
            if (DetectionModule.IsValid()) {
                DetectionModule.SetActor((Actor)m_Actor);
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
                    [UnitAttrType.Speed] = (PEInt)NavMeshAgent.speed ,
                    [UnitAttrType.AngularSpeed] = (PEInt)NavMeshAgent.angularSpeed,
                    [UnitAttrType.Size] = (PEInt)m_Actor.HalfRange,
                });
                var Speed = GetAttribute(UnitAttrType.Speed);
                if (Speed.PrimeValue > 0) Speed.OnFinalValueChange += (value) => { NavMeshAgent.speed = value.RawFloat; };
                var AngularSpeed = GetAttribute(UnitAttrType.AngularSpeed);
                if (AngularSpeed.PrimeValue > 0) Speed.OnFinalValueChange += (value) => { NavMeshAgent.angularSpeed = value.RawFloat; };
            }

        }
        protected override void Start()
        {
            base.Start();

            FindAndInitializeAllWeapons();
            Invoke(nameof(BirthEnd), BirthDuration+0.1f);
            Speed.AddModifier(ModifierType.Factor,-1);
        }


        void Update()
        {
            EnsureIsWithinLevelBounds();

            //DetectionModule?.HandleTargetDetection();

        }

        void BirthEnd()
        {
            if (m_lastDestination!=default) SetNavDestination(m_lastDestination);
            Speed.AddModifier(ModifierType.Factor, 1);
        }


        void EnsureIsWithinLevelBounds()
        {
            // at every frame, this tests for conditions to kill the enemy
            if (transform.position.y < Constants.KillHeight)
            {
                Tool.Destroy(gameObject);
                return;
            }
        }


        /// <summary>
        /// 更新巡逻路径
        /// </summary>
        /// <param name="inverseOrder"></param>
        public bool UpdatePathDestination()
        {
            if (PatrolPos!=default)
            {
                //检查是否到达路径目标点
                if ((Pos - PatrolPos).ToVector2().magnitude <= PathReachingRadius)
                {
                    return true;
                }
                SetNavDestination(PatrolPos);

                if (FpsHelper.HaveNavMeshAgent(NavMeshAgent) && NavMeshAgent.hasPath)
                {
                    Vector3 steerDir = NavMeshAgent.steeringTarget - transform.position;
                    steerDir.y = 0;
                    if (steerDir.sqrMagnitude > 0.01f)
                    {
                        transform.rotation = Quaternion.LookRotation(steerDir);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 设置目标点（通过PathRequestManager排队，避免单帧寻路瓶颈
        /// </summary>
        public void SetNavDestination(Vector3 destination)
        {
            if (Vector3.Distance(destination, m_lastDestination) < 1) return;

            m_lastDestination = destination;
            if (FpsHelper.HaveNavMeshAgent(NavMeshAgent) && NavMeshAgent.isOnNavMesh)
            {
                if (BirthComplete)
                {
                    PathRequestManager.Instance.RequestPath(NavMeshAgent, destination);
                }
            }
        }


        public void StopNav()
        {
            if (FpsHelper.HaveNavMeshAgent(NavMeshAgent))
            {
                NavMeshAgent?.ResetPath();
            }

            m_lastDestination = default;
        }

        protected override void _OnDamaged(PEInt damage, GameObject damageSource, Collider collider,bool noSource)
        {
            
            if (damageSource &&damageSource.GetComponent<Actor>().Type != UnitTypeEnum.Other&& !damageSource.GetComponent<Actor>().HasFlag(ActorFlag.Invincible))
            {
                DetectionModule?.OnDamaged(damageSource, noSource);
                OnDamaged?.Invoke(collider);
            }
            if (!noSource&&GameRoot.GameState == GameStateEnum.Game && damageSource.TryGetComponent(out PlayerController player)) BattleManager.Instance.AddBattleDataItem(player.PlayerIndex, "命中次数");
        }

        protected override void _OnDie(GameObject source)
        {
            base._OnDie(source);
            if (FpsHelper.HaveNavMeshAgent(NavMeshAgent))
            {
                NavMeshAgent.isStopped = true;
                NavMeshAgent.enabled = false;
            }
            if (DetectionModule.IsValid())
            {
                DetectionModule.enabled = false;
            }
            Speed.AddModifier(ModifierType.Factor, -1);

            if (GameRoot.GameState == GameStateEnum.Game && source && m_Actor.Team != 1)
            {
                PlayerController player;
                if (source.TryGetComponent(out Actor actor) && actor.Owner != null) actor.Owner.transform.TryGetComponent(out player);
                else source.TryGetComponent(out player);
                BattleManager.Instance.AddBattleDataItem(player ? player.PlayerIndex : RoomManager.Instance.Master.index, "击杀敌人");
            }

        }


        public bool TryAtack(WeaponEnemyController weapon) {
            bool didFire = false;
            // 自体炮塔等无武器炮台直接跳过，避免 NRE
            if (!weapon) return false;
            float dis = Vector3.Distance(EyePoint.position,Target.Pos);
            if (dis <= weapon.CurrentWeaponExtremeRange) {
                didFire |= weapon.HandleShootInputs(true, true, false);
            }
            //Debug.LogWarning(gameObject+"使用"+weapon+"攻击"+"触发"+ didFire+"事件"+(OnAttack != null),gameObject);
            if (didFire && OnAttack != null) {
                OnAttack?.Invoke(weapon);
            }
            return didFire;
        }
        public void TryStop(WeaponEnemyController weapon)
        {
            if (!weapon) return;
            weapon.ShootInputs(false, false, true);
        }


        /// <summary>
        /// 为所有武器绑定归属与忽略自身碰撞体
        /// </summary>
        void FindAndInitializeAllWeapons()
        {
            var weapons = GetComponentsInChildren<WeaponBaseController>();
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].Owner = gameObject;
                weapons[i].IgnoredColliders = m_SelfColliders;
            }
        }

        /// <summary>
        /// 没有侦测组件的控制器什么都不做
        /// </summary>
        /// <param name="point"></param>
        public override void Beware(Vector3 point,bool spread)
        {
            DetectionModule.Beware(point, spread);
        }





        private void OnDrawGizmosSelected()
        {
            if (NavMeshAgent == null || !NavMeshAgent.hasPath) return;

            NavMeshPath path = NavMeshAgent.path;
            Vector3[] corners = path.corners;

            if (corners.Length < 2) return;

            // 用红线绘制完整路径
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(corners[i], corners[i + 1]);

                // 在每个拐点画小圆
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(corners[i], 0.2f);
            }

            // 用绿色标记下一个转向点
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(NavMeshAgent.steeringTarget, 0.3f);

            // 用蓝色标记最终目标
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(NavMeshAgent.destination, 0.4f);
        }
    }
}