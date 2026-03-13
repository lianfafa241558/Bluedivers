using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.AI
{




    [RequireComponent(typeof(HealthEnemy), typeof(Actor))]
    public partial class EnemyController : AIController
    {

        public string EnemyName => m_Actor.ShowName;

        public Sprite Portrait => m_Actor.Portrait;
        public Sprite Halo => m_Actor.ExtraPortrait;


        public bool Boss => m_Actor.HasFlag(ActorFlag.Boss);


        [CustomLabel("敌人认为已到达当前路径目标点的距离")]
        public float PathReachingRadius = 2f;

        public bool BirthComplete=>Time.time>=birthTime+BirthDuration;

        /// <summary>巡逻路径</summary>
        public PatrolPath PatrolPath { get; set; }
        /// <summary>当前的目标(的AimPoint)</summary>
        public TargetData KnownDetectedTarget => DetectionModule.Target;
        /// <summary>目标是否进入攻击范围</summary>
        public bool IsTargetInAttackRange => DetectionModule.IsTargetInAttackRange;
        /// <summary>目标是否可见</summary>
        public bool IsSeeingTarget => DetectionModule.IsSeeingTarget;

        public float Speed=> NavMeshAgent? NavMeshAgent.speed:0;

        public Vector3 Velocity => NavMeshAgent ? NavMeshAgent.velocity : Vector3.zero;
        

        protected NavMeshAgent NavMeshAgent { get; private set; }
        public DetectionModule DetectionModule { get; private set; }



        int m_PathDestinationNodeIndex;

 
        Collider[] m_SelfColliders;

        int m_CurrentWeaponIndex;
        WeaponEnemyController m_CurrentWeapon;

        //[SerializeField]
        //[Display(false,true,true)]
        WeaponEnemyController[] m_Weapons;

        //[Display(true, false, true)]
        private Vector3 m_lastDestination;


       
        private Transform EyePoint;

        protected override void InitComponent() 
        {
            base.InitComponent();
          
            NavMeshAgent = GetComponent<NavMeshAgent>();
            m_SelfColliders = GetComponentsInChildren<Collider>();

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


        protected override void Start()
        {
            base.Start();

            FindAndInitializeAllWeapons();
            GetCurrentWeapon();
            Invoke(nameof(BirthEnd), BirthDuration+0.1f);

        }


        void Update()
        {
            EnsureIsWithinLevelBounds();

            //DetectionModule?.HandleTargetDetection();

        }

        void BirthEnd()
        {
            if(m_lastDestination!=default) SetNavDestination(m_lastDestination);
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

        //巡逻路径
        bool IsPathValid()
        {
            return PatrolPath && PatrolPath.PathNodes.Count > 0;
        }
        //巡逻路径
        public void ResetPathDestination()
        {
            m_PathDestinationNodeIndex = 0;
        }
        //巡逻路径
        public void SetPathDestinationToClosestNode()
        {
            if (IsPathValid())
            {
                int closestPathNodeIndex = 0;
                for (int i = 0; i < PatrolPath.PathNodes.Count; i++)
                {
                    float distanceToPathNode = PatrolPath.GetDistanceToNode(transform.position, i);
                    if (distanceToPathNode < PatrolPath.GetDistanceToNode(transform.position, closestPathNodeIndex))
                    {
                        closestPathNodeIndex = i;
                    }
                }

                m_PathDestinationNodeIndex = closestPathNodeIndex;
            }
            else
            {
                m_PathDestinationNodeIndex = 0;
            }
        }
        /// <summary>
        /// 获得下一个巡逻点
        /// </summary>
        /// <returns></returns>
        public Vector3 GetDestinationOnPath()
        {
            if (IsPathValid())
            {
                return PatrolPath.GetPositionOfPathNode(m_PathDestinationNodeIndex);
            }
            else
            {
                return transform.position;
            }
        }

        public bool HaveNavMeshAgent() => NavMeshAgent && NavMeshAgent.isActiveAndEnabled;

        /// <summary>
        /// 设置目标点
        /// </summary>
        /// <param name="destination"></param>
        public void SetNavDestination(Vector3 destination)
        {
            m_lastDestination = destination;
            if (HaveNavMeshAgent())
            {
                if(BirthComplete)NavMeshAgent.SetDestination(destination);
            }
        }
        /// <summary>
        /// 更新巡逻路径
        /// </summary>
        /// <param name="inverseOrder"></param>
        public bool UpdatePathDestination(bool inverseOrder = false)
        {
            if (IsPathValid())
            {
                //检查是否到达路径目标
                if ((transform.position - GetDestinationOnPath()).magnitude <= PathReachingRadius)
                {
                    //递增路径目标索引
                    m_PathDestinationNodeIndex =
                        inverseOrder ? (m_PathDestinationNodeIndex - 1) : (m_PathDestinationNodeIndex + 1);
                    if (m_PathDestinationNodeIndex < 0)
                    {
                        m_PathDestinationNodeIndex += PatrolPath.PathNodes.Count;
                    }

                    if (m_PathDestinationNodeIndex >= PatrolPath.PathNodes.Count)
                    {
                        m_PathDestinationNodeIndex -= PatrolPath.PathNodes.Count;
                    }
                    return true;
                }
                
            }
            return false;
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

            if (NavMeshAgent && NavMeshAgent.isActiveAndEnabled)
            {
                NavMeshAgent.isStopped = true;
                NavMeshAgent.enabled = false;
            }
            /*
            for (int i = 0; i < m_Weapons.Length; i++)
            {
                TryStop(WeaponEnemyController weapon)
                m_Weapons[i].Owner = gameObject;
            }*/

            if (GameRoot.GameState == GameStateEnum.Game && source && m_Actor.Team != 1)
            {
                PlayerController player;
                if (source.TryGetComponent(out Actor actor) && actor.Owner != null) actor.Owner.transform.TryGetComponent(out player);
                else source.TryGetComponent(out player);
                BattleManager.Instance.AddBattleDataItem(player ? player.PlayerIndex : RoomManager.Instance.Master.index, "击杀敌人");
            }

        }


        public void OrientWeaponsTowards(Vector3 lookPosition)
        {
            for (int i = 0; i < m_Weapons.Length; i++)
            {
                // orient weapon towards player
                Vector3 weaponForward = (lookPosition - m_Weapons[i].WeaponRoot.transform.position).normalized;
                m_Weapons[i].transform.forward = weaponForward;
            }
        }

        public bool TryAtack(WeaponEnemyController weapon) {
            bool didFire = false;
            float dis = Vector3.Distance(EyePoint.position,KnownDetectedTarget.Pos);
            if (dis <= weapon.CurrentWeaponExtremeRange) {
                didFire |= weapon.HandleShootInputs(true, true, false);
            }

            if (didFire && OnAttack != null) {
                OnAttack.Invoke(weapon);
            }
            return didFire;
        }
        public void TryStop(WeaponEnemyController weapon)
        {
            weapon.HandleShootInputs(false, false, true);
        }


        /// <summary>
        /// 查找并初始化所有武器
        /// </summary>
        void FindAndInitializeAllWeapons()
        {
            if (!m_Weapons.IsValid()|| m_Weapons.Length==0)
            {
                
                m_Weapons = GetComponentsInChildren<WeaponEnemyController>();
                //Debug.LogError("查找武器" + gameObject.name+"数量"+m_Weapons.Length);
                for (int i = 0; i < m_Weapons.Length; i++)
                {
                    m_Weapons[i].Owner = gameObject;
                    m_Weapons[i].IgnoredColliders = m_SelfColliders;
                }
            }
        }

        public bool HaveWeapon() {
             return m_Weapons.Length>0;
        }

        /// <summary>
        /// 不用担心没有武器的问题，建筑不会调用此方法
        /// </summary>
        /// <returns></returns>
        public WeaponEnemyController GetCurrentWeapon()
        {
            //FindAndInitializeAllWeapons();//其他组件尝试调用的更早，只能预先检测有没有了
            //检查当前是否未选择武器
            if (m_CurrentWeapon == null)
            {
                //将武器列表中的第一件武器设置为当前武器
                SetCurrentWeapon(0);
            }
            return m_CurrentWeapon;
        }

        void SetCurrentWeapon(int index)
        {
            m_CurrentWeaponIndex = index;
            if (!m_Weapons.IsValid() ||index >= m_Weapons.Length) return;
            m_CurrentWeapon = m_Weapons[m_CurrentWeaponIndex];
            m_CurrentWeapon.ShowWeapon(true);
        }
    }
}