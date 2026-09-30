using System.Collections.Generic;
using FPSGame.Attributes;

using UnityEngine;
using FPSGame.Audio;
using FPSGame.Utils;
using FPSGame.Core;
namespace FPSGame.AI
{
    /// <summary>
    /// 工厂型敌人的状态机
    /// </summary>
    [AddComponentMenu("AI/巢穴建造")]
    public class EnemyNestBuild : AIInputBaseController<EnemyNestBuild.AIState>
    {
        public enum AIState
        {
            Idle,
            Active,
            Death,
        }
        [SerializeField]
        [InspectorName("创建点")]
        private Transform creatPoint;
        [SerializeField]
        [InspectorName("创建音效")]
        private AudioClip creatCilp;
        [SerializeField]
        [InspectorName("生产单位")]
        private List<SKVP<GameObject,int>> creatData;
        [SerializeField]
        [InspectorName("脱战时间")]
        private int damagedTime=12;
        [SerializeField]
        [DisplayField(DisplayFieldEnum.RunRead)]
        private float lastDamagedTime = Mathf.NegativeInfinity, creatTime= Mathf.Infinity;

        protected override Dictionary<AIState, AiStateHook> InitState()
        {
            return new Dictionary<AIState, AiStateHook>
            {
                [AIState.Idle] = new AiStateHook(),
                [AIState.Active] = new AiStateHook
                {
                    onUpdate = ActiveBehavior,
                },
                [AIState.Death] = new AiStateHook(),
            };
        }

        /// <summary>Active：按生产节奏生成单位并派遣</summary>
        private void ActiveBehavior()
        {
            if (Time.time > creatTime)
            {
                var item = RandomUtils.RandomTake(creatData);
                creatTime = Time.time + item.Value;
                var go = Instantiate(item.Key, creatPoint.position + creatPoint.forward, creatPoint.rotation, transform.parent);
                go.GetComponent<EnemyController>().SetNavDestination(GetComponent<EnemyController>().Target.Pos);
                AudioSvc.PlaySound(new(creatCilp, transform.position, 40, FPSGame.Core.AudioGroups.Enemy));
            }
        }

        protected override void UpdateCurrentAiState()
        {
            InvokeCurrentState();
        }
        protected override void UpdateAiStateTransitions()
        {
            switch (AiState)
            {
                case AIState.Idle:
                    if (Time.time - damagedTime < lastDamagedTime)
                    {
                        SwitchState(AIState.Active);

                        creatTime = Time.time + RandomUtils.Range(5,10);//第一个生产时间随机
                    }
                    break;
                case AIState.Active:
                    if (Time.time - damagedTime > lastDamagedTime)
                    {
                        SwitchState(AIState.Idle);
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
            SwitchState(AIState.Death);
        }
    }
}
