using System.Collections.Generic;
using FPSGame.Attribute;
using UnityEngine;
namespace Unity.FPS.AI
{
    /// <summary>
    /// 工厂型敌人的状态机
    /// </summary>
    public class EnemyNestBuild : AIInputBaseController
    {
        public enum AIState
        {
            Idle,
            Active,
            Death,
        }

        [InspectorName("创建点")]
        public Transform creatPoint;

        [InspectorName("生产单位")]
        public List<SKVP<GameObject,int>> creatData;

        [InspectorName("脱战时间")]
        public int damagedTime=12;
        [SerializeField]
        [DisplayField(true,false)]
        private float lastDamagedTime = Mathf.NegativeInfinity, creatTime= Mathf.Infinity;

        public AIState AiState;// { get; private set; }

        protected override void UpdateCurrentAiState()
        {
            switch (AiState)
            {
                case AIState.Idle:
                    break;
                case AIState.Active:
                    if(Time.time > creatTime)
                    {
                        var item = RandomUtils.RandomTake(creatData);
                        creatTime = Time.time+ item.Value;
                        var go = Instantiate(item.Key, creatPoint.position + creatPoint.forward, creatPoint.rotation, transform.parent);
                        go.GetComponent<EnemyController>().SetNavDestination(GetComponent<EnemyController>().Target.Pos);
                    }
                    break;
            }
        }
        protected override void UpdateAiStateTransitions()
        {
            switch (AiState)
            {
                case AIState.Idle:
                    if (Time.time - damagedTime < lastDamagedTime)
                    {
                        AiState = AIState.Active;

                        creatTime = Time.time + RandomUtils.Range(5,10);//第一个生产时间随机
                    }
                    break;
                case AIState.Active:
                    if (Time.time - damagedTime > lastDamagedTime)
                    {
                        AiState = AIState.Idle;
                    }
                    break;
            }
        }

        /// <summary>
        /// 受击
        /// </summary>
        protected override void OnDamaged(Collider collider)
        {
            lastDamagedTime = Time.time;
        }
        protected override void OnDetectedTarget()
        {
            lastDamagedTime = Time.time;
        }
        protected override void OnLostTarget()
        {

        }


        protected override void OnDie()
        {
            AiState = AIState.Death;
        }
    }
}