using System;
using System.Collections.Generic;
using GameContract;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using static Unity.FPS.AI.AIInputUnitController;

namespace FPSGame.AI
{
    internal abstract class StateMachineFrame<StateEnum> : MonoBehaviour
        where StateEnum : System.Enum
    {

        [SerializeField]
        protected DetectionModule DetectionModule;

        /// <summary>发现目标的时间</summary>
        protected float m_TimeStartedDetection { get; set; }
        /// <summary>丢失目标的时间</summary>
        protected float m_TimeLostDetection { get; set; }
        protected Vector3 DetectionTargetPos => DetectionModule.Target.Pos;

     
        [SerializeField]
        protected List<Turret> turrets = new();
        [SerializeField]
        private StateEnum aiState;
        protected StateEnum AiState { 
            get => aiState; 
            set {
                stateInfos[aiState].onExit?.Invoke();
                aiState = value;
                stateInfos[value].onEnter?.Invoke();
            } 
        }

        protected I_Actor m_actor;
        private Dictionary<StateEnum, StateInfo> stateInfos;

        protected struct StateInfo
        {
            public StateEnum state;
            public Action onEnter;
            public Action onUpdate;
            public Action onLateUpdate;
            public Action onExit;
        }

        protected abstract Dictionary<StateEnum, StateInfo> InitState();

        protected abstract void Init();
        protected abstract void Uninit();
        protected abstract void OnDetectedTarget();
        protected abstract void OnLostTarget();



        void Start()
        {
            m_actor = GetComponent<I_Actor>();
            Init();
            DetectionModule.SetActor(m_actor as Actor);
            stateInfos = InitState();
            m_TimeStartedDetection = Mathf.NegativeInfinity;
            turrets.ForEach(item => item.Init(transform));
            AiState = default;

            DetectionModule.onDetectedTarget += OnDetectedTarget;
            DetectionModule.onLostTarget += OnLostTarget;
        }
        void OnDestroy()
        {
            Uninit();
            if (DetectionModule == null) return;
            DetectionModule.onDetectedTarget -= OnDetectedTarget;
            DetectionModule.onLostTarget -= OnLostTarget;
        }



        void Update()
        {
            //Debug.LogWarning("当前状态"+ AiState+"回调"+ stateInfos[AiState].onUpdate);
            stateInfos[AiState].onUpdate?.Invoke();
        }
        void LateUpdate()
        {
            stateInfos[AiState].onLateUpdate?.Invoke();
        }




    }

}
