using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using Unity.BaseTool;
using UnityEngine;
using Utils;

namespace Unity.FPS.AI
{
    public class SpecUnitKei : AIInputUnitController
    {
        public enum AIState
        {
            Wait,
            Move,
            Follow,
            Attack,
            Death,
        }
        [SerializeField]
        new Renderer renderer;
        [SerializeField]
        List<NoticeData_SO> call;
        [SerializeField]
        GameObject callPoint;
        [SerializeField]
        List<AudioClip> moveCilps;

        MpbController mpb;


        new SpecUnitController m_Controller;
        Vector3 m_Target;
        GameObject m_callInstance;
        AudioSource m_audioSource;

        Vector3 TargetPosition => m_Controller.KnownDetectedTarget.Pos;

        public AIState AiState { get; private set; }
        float lastSpeechTime, speechShowTime;


        protected override void Start()
        {
            base.Start();
            GlobalEventManager.OnCallKai += OnCall;
            m_audioSource = AudioManager.CreatSource(gameObject, AudioGroups.General);
            m_audioSource.loop = false;
            m_Controller = base.m_Controller as SpecUnitController;
            AiState = AIState.Wait;
            mpb = new(renderer);
            mpb.Set("_Expression", 1).Apply();
        }

        private void OnDestroy()
        {
            GlobalEventManager.OnCallKai -= OnCall;

        }

        void OnCall(GameObject source, Vector3 point)
        {
            if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, 100, LayerDefinition.GroundLayers))
            {
                point = hit.point;
            }

            m_callInstance =VFXManager.Creat(callPoint, point);
            AudioManager.PlaySound(new("Kei/MollyBeaconPlace", AudioGroups.General, 0.5f));

            if (Vector3.Distance(m_Target,point)>1)
            {
                m_Target = point;
                AiState = AIState.Move;
                m_Controller.SetNavDestination(m_Target);
                mpb.Set("_Expression", 20).Apply();
            }
            if (Time.time > speechShowTime + lastSpeechTime)
            {
                lastSpeechTime = Time.time;
                var item = call.RandomTake();
                speechShowTime = item.Clip.length;
                GlobalEventManager.ActorSpeech(gameObject, item);
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
                        AiState = AIState.Attack;
                        mpb.Set("_Expression", 20).Apply();
                        //在这里写移动没用，下一帧就改了
                    }

                    break;
                case AIState.Attack:
                    // Transition to follow when no longer a target in attack range
                    if (!m_Controller.IsTargetInAttackRange)
                    {
                        AiState = AIState.Follow;
                        mpb.Set("_Expression", 3).Apply();
                    }

                    break;
                case AIState.Wait:
                    break;
                case AIState.Move:
                    if (!m_audioSource.isPlaying)
                    {
                        m_audioSource.clip = moveCilps.RandomTake();
                        m_audioSource.Play();
                    }
                    if (Vector3.Distance(transform.position, m_Target) < 1)
                    {
                        AiState = AIState.Wait;
                        mpb.Set("_Expression", 1).Apply();
                        m_callInstance.GetComponent<LimitedLife>().allowRelease = true;
                    }
                    break;
            }
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


        /// <summary>状态机每帧</summary>
        protected override void UpdateCurrentAiState()
        {
            // Handle logic 
            switch (AiState)
            {
                case AIState.Wait:

                    break;
                case AIState.Move:

                    break;
                case AIState.Follow:
                    m_Controller.SetNavDestination(TargetPosition);
                    AimTargrt();
                    break;
                case AIState.Attack:

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
                            if (item.IsLockTarget(TargetPosition)) m_Controller.TryAtack(item.weapon);
                        });
                    }

                    break;
            }
        }


        protected override void OnDetectedTarget()
        {
            if (AiState == AIState.Wait)
            {
                AiState = AIState.Follow;
            }

            m_TimeStartedDetection = Time.time;
        }

        protected override void OnLostTarget()
        {
            if (AiState == AIState.Follow || AiState == AIState.Attack)
            {
                AiState = AIState.Wait;
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
            //计算我们炮塔的期望旋转（瞄准目标）
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
            AiState = AIState.Death;
        }
    }
}