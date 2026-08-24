using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Furn;
using GameContract;

using Unity.FPS.Game;
using UnityEngine;
using Utils;

namespace FPSGame.AI
{
    public class SpecUnitKei : AIInputUnitController<SpecUnitKei.AIState>
    {
        public enum AIState
        {
            Wait,
            Move,
            Follow,
            Attack,
            Death,
        }
#pragma warning disable CS0108
        [SerializeField]
        Renderer renderer;

        [SerializeField]
        SoundGroup_SO callSound;

        [SerializeField]
        GameObject callPoint;
        [SerializeField]
        SoundGroup_SO moveCilps;
        [SerializeField]
        SoundGroup_SO stopCilps;

        MpbController mpb;


        new SpecUnitController m_Controller;
        Vector3 m_Target;
        GameObject m_callInstance;
        AudioSource m_audioSource;

        Vector3 TargetPosition => m_Controller.KnownDetectedTarget.Pos;

        float lastSpeechTime, speechShowTime;


        protected override void Start()
        {
            base.Start();
            BattleEventSub.OnCallKai += OnCall;
            m_audioSource = AudioSvc.CreatSource(gameObject, AudioGroups.General);
            m_audioSource.loop = false;
            m_Controller = base.m_Controller as SpecUnitController;
            AiState = AIState.Wait;
            mpb = new(renderer);
            mpb.Set("_Expression", 1).Apply();
        }

        private void OnDestroy()
        {
            BattleEventSub.OnCallKai -= OnCall;

        }

        void OnCall(GameObject source, Vector3 point)
        {
            point += (transform.position - point).normalized * 0.5f;
            if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, 100, LayerDefinition.GroundLayers))
            {
                point = hit.point;
            }

            m_callInstance =VFXManager.Creat(callPoint, point);
            AudioSvc.PlaySound(new("Student/Kei/MollyBeaconPlace", AudioGroups.General, 0.5f));

            if (Vector3.Distance(m_Target,point)>1)
            {
                m_Target = point;
                SwitchState(AIState.Move);
                m_Controller.SetNavDestination(m_Target);
                mpb.Set("_Expression", 20).Apply();
            }
            else
            {
                HelpPlayer();
            }
            if (Time.time > speechShowTime + lastSpeechTime)
            {
                lastSpeechTime = Time.time;
                var item = callSound.Get(transform.position);
                speechShowTime = item.Clip.length;
                GlobalEventSub.ActorSpeech(gameObject, item);
            }
        }

        protected override Dictionary<AIState, StateInfo> InitState()
        {
            return new Dictionary<AIState, StateInfo>
            {
                [AIState.Wait] = new StateInfo(),
                [AIState.Move] = new StateInfo(),
                [AIState.Follow] = new StateInfo
                {
                    onUpdate = FollowBehavior,
                },
                [AIState.Attack] = new StateInfo
                {
                    onUpdate = AttackBehavior,
                },
                [AIState.Death] = new StateInfo(),
            };
        }

        /// <summary>Follow：追敌并瞄准</summary>
        private void FollowBehavior()
        {
            m_Controller.SetNavDestination(TargetPosition);
            AimTargrt();
        }

        /// <summary>Attack：逼近/保持距离并射击</summary>
        private void AttackBehavior()
        {
            float dis = Vector3.Distance(TargetPosition,
                    m_Controller.CenterPos);
            float stopRange = (0.95f * m_Controller.DetectionModule.AttackRange);//停止距离
            //如果目标到自己的距离大于停止系数*攻击范围，那就追，到范围就停
            if (dis >= stopRange + 1 / m_Controller.DetectionModule.AttackRange)//接近
            {
                m_Controller.SetNavDestination(TargetPosition);
            }
            else//原地
            {
                m_Controller.SetNavDestination(transform.position);
            }
            // shoot
            if (AimTargrt())
            {
                turrets.ForEach(item => {
                    if (item.IsLockTarget(TargetPosition) && item.CanFireAt(TargetPosition)) m_Controller.TryAtack(item.weapon);
                });
            }
        }


        /// <summary>状态机切换</summary>
        protected override void UpdateAiStateTransitions()
        {
            // Handle transitions 
            switch (AiState)
            {

                case AIState.Follow:
                    // 当与目标有视线连接时，转为攻击状态
                    if (m_Controller.IsSeeingTarget && m_Controller.IsTargetInAttackRange && IsLockTarget())
                    {
                        SwitchState(AIState.Attack);
                        mpb.Set("_Expression", 20).Apply();
                        //在这里写移动没用，下一帧就改了
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_Controller.IsTargetInAttackRange)
                    {
                        SwitchState(AIState.Follow);
                        mpb.Set("_Expression", 3).Apply();
                    }

                    break;
                case AIState.Wait:
                    break;
                case AIState.Move:
                    if (!m_audioSource.isPlaying)
                    {
                        m_audioSource.clip = moveCilps.Get().Clip;
                        m_audioSource.Play();
                    }
                    if (Vector3.Distance(transform.position, m_Target) < 1)
                    {
                        SwitchState(AIState.Wait);
                        AudioSvc.PlaySound(stopCilps.Get(transform.position));

                        mpb.Set("_Expression", 1).Apply();
                        if(m_callInstance) m_callInstance.GetComponent<LimitedLife>().allowRelease = true;

                        HelpPlayer();
                    }
                    break;
            }
        }

        private void HelpPlayer()
        {
            //到目标点了，尝试对着拉人
            ActorsManager.Players.ForEach((item) => {
                if (Vector3.Distance(item.Pos, transform.position) <= 2
                    && item.transform.TryGetComponent(out Furniture_PlayerDown furn))
                {
                    furn.Handle(gameObject);
                }
            });
        }

        private bool IsLockTarget()
        {
            foreach (var item in turrets)
            {
                if (item.IsLockTarget(TargetPosition))
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary>状态机每帧（查表调用当前状态行为）</summary>
        protected override void UpdateCurrentAiState()
        {
            // 查表调用当前状态行为
            InvokeCurrentState();
        }


        protected override void OnDetectedTarget()
        {
            if (AiState == AIState.Wait)
            {
                SwitchState(AIState.Follow);
            }

            m_TimeStartedDetection = Time.time;
        }

        protected override void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                SwitchState(AIState.Wait);
            }

            m_TimeLostDetection = Time.time;
            turrets.ForEach(item => m_Controller.TryStop(item.weapon));
        }

        protected override bool AimTargrt()
        {
            bool mustShoot = false;
            foreach (var item in turrets)
            {
                if (mustShoot |= Time.time > m_TimeStartedDetection + item.detectionFireDelay) break;
            }
            //计算我们炮塔的期望旋转（瞄准目标点）
            //从炮口到目标的方向
            //KnownDetectedTarget就已经是aimpoint了
            CalculationAimTargrt(TargetPosition);

            return mustShoot;
        }

        protected override void OnDamaged(Collider collider)
        {
            
        }


        protected override void OnDie()
        {
            //真的会死吗？
            SwitchState(AIState.Death);
        }
    }
}
