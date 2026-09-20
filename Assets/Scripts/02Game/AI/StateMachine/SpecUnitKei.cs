using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using FPSGame.Furn;
using GameContract;
using PEMaths;
using TMPro;
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

        /// <summary>带队撤离时每隔多少米留一根回收标记</summary>
        private const float ReturnPointInterval = 5f;
        /// <summary>取地面用的射线长度</summary>
        private const float GroundRayLength = 100f;
        /// <summary>抵达目标点的判定距离</summary>
        private const float ArriveDistance = 1f;

        /// <summary>取出回收信标时触发(撤离任务据此播报提示)</summary>
        public event Action OnReturnBagShow;

        /// <summary>回收信标(未初始化时自动补建，见 InitReturnBag)</summary>
        public Furniture_ReturnBag ReturnBag
        {
            get
            {
                InitReturnBag();
                return m_ReturnBag;
            }
        }

#pragma warning disable CS0108
        [SerializeField]
        Renderer renderer;
        [SerializeField]
        GameObject returnBag;

        [SerializeField]
        SoundGroup_SO callSound;
        [SerializeField]
        GameObject returnPoint;
        [SerializeField]
        GameObject callPoint;
        [SerializeField]
        SoundGroup_SO moveCilps;
        [SerializeField]
        SoundGroup_SO stopCilps;
        [SerializeField]
        TextMeshPro text;

        MpbController mpb;


        new SpecUnitController m_Controller;
        Vector3 m_Target;
        GameObject m_callInstance;
        AudioSource m_audioSource;

        Vector3 TargetPosition => m_Controller.KnownDetectedTarget.Pos;

        float lastSpeechTime, speechShowTime;
        PEInt helpCool=0;

        /// <summary>身上的回收信标(玩家在其上发起动态撤离)</summary>
        Furniture_ReturnBag m_ReturnBag;
        /// <summary>是否正在带队前往撤离点(途中每隔一段距离留回收标记)</summary>
        bool m_Guiding;
        /// <summary>撤离是否已经开始(凯伊要在撤离点等玩家登机，不再响应呼叫)</summary>
        bool m_Evacuating;
        /// <summary>上一根回收标记的位置</summary>
        Vector3 m_LastReturnPointPos;

        protected override void Start()
        {
            base.Start();
            BattleEventSub.OnCallKai += OnCall;
            BattleEventSub.OnEvacuate += OnEvacuate;
            m_audioSource = AudioSvc.CreatSource(gameObject, AudioGroups.General);
            m_audioSource.loop = false;
            m_Controller = base.m_Controller as SpecUnitController;
            AiState = AIState.Wait;
            mpb = new(renderer);
            mpb.Set("_Expression", 1).Apply();
            InitReturnBag();
        }

        private void OnDestroy()
        {
            BattleEventSub.OnCallKai -= OnCall;
            BattleEventSub.OnEvacuate -= OnEvacuate;

        }

        #region 回收信标

        /// <summary>准备回收信标：回收包挂在身上但默认不可交互，凯伊取出后才开放</summary>
        private void InitReturnBag()
        {
            if (!returnBag || m_ReturnBag) return;

            if (!returnBag.TryGetComponent(out m_ReturnBag))
            {
                //预制体上还没挂家具组件时运行时补一个，保证交互可用
                m_ReturnBag = returnBag.AddComponent<Furniture_ReturnBag>();
            }
            //取出前不参与交互扫描
            m_ReturnBag.enabled = false;
        }

        /// <summary>取出回收信标：玩家可在其上进行交互以请求动态撤离</summary>
        private void ShowReturnBag()
        {
            if (returnBag && !returnBag.activeSelf) returnBag.SetActive(true);
            if (m_ReturnBag && !m_ReturnBag.enabled) m_ReturnBag.enabled = true;

            //每次都通知，避免任务在取出信标之后才激活时收不到提示
            OnReturnBagShow?.Invoke();
        }

        #endregion

        void OnCall(GameObject source, Vector3 point)
        {
            //撤离开始后不再响应呼叫：凯伊要带路/在撤离点等玩家登机，不能被叫走
            if (m_Evacuating) return;

            point += (transform.position - point).normalized * 0.5f;
            if (Physics.Raycast(point, Vector3.down, out RaycastHit hit, GroundRayLength, LayerDefinition.GroundLayers))
            {
                point = hit.point;
            }

            m_callInstance = VFXManager.Creat(callPoint, point);
            AudioSvc.PlaySound(new("Student/Kei/MollyBeaconPlace", AudioGroups.General, 0.5f));

            if (Vector3.Distance(m_Target, point) > ArriveDistance)
            {
                m_Target = point;
                SwitchState(AIState.Move);
                m_Controller.SetNavDestination(m_Target);
                mpb.Set("_Expression", 20).Apply();
            }
            else
            {
                HelpPlayer();
                ShowReturnBag();
            }

            TrySpeech(callSound);
        }

        /// <summary>撤离开始：带队前往撤离点，沿途留下回收标记</summary>
        private void OnEvacuate(PEVector3 point)
        {
            Vector3 target = point.RawVector3;
            m_Evacuating = true;
            m_Guiding = true;
            m_Target = target;
            //从当前位置开始量，避免刚起步就把标记丢在原地
            m_LastReturnPointPos = transform.position;

            if (Vector3.Distance(transform.position, m_Target) <= ArriveDistance)
            {
                ArriveEvacuatePoint();
                return;
            }

            SwitchState(AIState.Move);
            m_Controller.SetNavDestination(m_Target);
            mpb.Set("_Expression", 20).Apply();
        }

        /// <summary>抵达撤离点</summary>
        private void ArriveEvacuatePoint()
        {
            m_Guiding = false;
            SwitchState(AIState.Wait);
            AudioSvc.PlaySound(stopCilps.Get(transform.position));
            mpb.Set("_Expression", 1).Apply();
        }

        /// <summary>抵达目标点(呼叫点/撤离点)后的收尾</summary>
        private void ArriveTarget()
        {
            if (m_Guiding)
            {
                ArriveEvacuatePoint();
                return;
            }

            SwitchState(AIState.Wait);
            AudioSvc.PlaySound(stopCilps.Get(transform.position));
            mpb.Set("_Expression", 1).Apply();
            if (m_callInstance && m_callInstance.TryGetComponent(out LimitedLife life)) life.allowRelease = true;

            HelpPlayer();
            ShowReturnBag();
        }

        /// <summary>带队途中每隔一段距离往地上放一根回收标记</summary>
        private void DropReturnPoint()
        {
            if (FlatDistance(transform.position, m_LastReturnPointPos) < ReturnPointInterval) return;

            Vector3 point = transform.position;
            if (Physics.Raycast(point + Vector3.up * 2, Vector3.down, out RaycastHit hit, GroundRayLength, LayerDefinition.GroundLayers))
            {
                point = hit.point;
            }

            m_LastReturnPointPos = transform.position;
            //回收标记与凯伊当前朝向一致
            VFXManager.Creat(returnPoint, point, transform.rotation);
            //TrySpeech(callSound);
        }

        /// <summary>水平距离(忽略高度差，按行进距离决定是否放标记)</summary>
        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0;
            b.y = 0;
            return Vector3.Distance(a, b);
        }

        /// <summary>上一句台词播完后再说一句</summary>
        private void TrySpeech(SoundGroup_SO group)
        {
            if (!group || Time.time <= speechShowTime + lastSpeechTime) return;

            lastSpeechTime = Time.time;
            var item = group.Get(transform.position);
            speechShowTime = item.Clip.length;
            GlobalEventSub.ActorSpeech(gameObject, item);
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
                    //带队撤离：边走边留回收标记
                    if (m_Guiding) DropReturnPoint();
                    if (Vector3.Distance(transform.position, m_Target) < ArriveDistance)
                    {
                        ArriveTarget();
                    }
                    break;
            }
        }

        private void HelpPlayer()
        {
            if (helpCool > 0) return;
            bool have= false;
            //到目标点了，尝试对着拉人
            ActorsManager.Players.ForEach((item) => {
                if (Vector3.Distance(item.Pos, transform.position) <= 2.5f
                    && item.transform.TryGetComponent(out Furniture_PlayerDown furn))
                {
                    if (furn.Handle(gameObject))
                    {
                        item.transform.GetComponent<Health>().Heal(100);
                        have = true;
                    }
                }
            });
            if (have)
            {
                helpCool = 60;
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


        /// <summary>状态机每帧（查表调用当前状态行为）</summary>
        protected override void UpdateCurrentAiState()
        {
            // 查表调用当前状态行为
            InvokeCurrentState();
        }

        private void FixedUpdate()
        {
            helpCool -= Constants.LoginFrame;
            text.text = helpCool > 0 ? "" + helpCool.RawInt : "";
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
