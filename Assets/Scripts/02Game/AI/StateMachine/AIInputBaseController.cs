using System;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
namespace FPSGame.AI
{
    /// <summary>
    /// 泛型 AI 状态机基类：T 为子类自定义的 AIState 枚举。
    /// 提供委托表状态机框架（StateInfo 的 onEnter/onUpdate/onExit + SwitchState + InvokeCurrentState），
    /// 子类通过 InitState() 注册状态行为，UpdateCurrentAiState 中调用 InvokeCurrentState() 查表驱动。
    /// </summary>
    public abstract class AIInputBaseController<T> : MonoBehaviour where T : System.Enum
    {
        protected I_AIController m_Controller;
        /// <summary>发现目标的时候/summary>
        protected float m_TimeStartedDetection { get; set; }//;
        /// <summary>丢失目标的时候/summary>
        protected float m_TimeLostDetection;

        #region 状态机委托表结构

        /// <summary>单个状态的钩子表</summary>
        protected struct StateInfo
        {
            public Action onEnter;
            public Action onUpdate;
            public Action onExit;
        }

        /// <summary>状态表：枚举 -> 状态钩子</summary>
        private Dictionary<T, StateInfo> _stateInfos;

        /// <summary>首次切换标志：首次 SwitchState 总是触发（保证初始状态 onEnter 执行）</summary>
        private bool _firstSwitch = true;

        /// <summary>当前状态</summary>
        public T AiState { get; set; }

        /// <summary>子类构建状态表（在基类 Start 中调用，需在首次 SwitchState 前完成）</summary>
        protected abstract Dictionary<T, StateInfo> InitState();

        /// <summary>状态切换：先退旧状态(onExit)，再进新状态(onEnter)</summary>
        protected void SwitchState(T state)
        {
            if (!_firstSwitch && EqualityComparer<T>.Default.Equals(state, AiState)) return;
            _firstSwitch = false;

            _stateInfos[AiState].onExit?.Invoke();
            AiState = state;
            _stateInfos[state].onEnter?.Invoke();
        }

        /// <summary>查表驱动当前状态的 onUpdate（子类在守卫逻辑后调用）</summary>
        protected void InvokeCurrentState()
        {
            _stateInfos[AiState].onUpdate?.Invoke();
        }

        #endregion

        protected virtual void Start()
        {
            m_TimeStartedDetection = Mathf.NegativeInfinity;
            _stateInfos = InitState();
            m_Controller = GetComponent<I_AIController>();
            //攻击本身就是从这里控制的，再从控制器传回来太荒谬了
            //m_AIController.OnAttack += OnAttack;
            m_Controller.OnDetectedTarget += OnDetectedTarget;
            m_Controller.OnLostTarget += OnLostTarget;
            m_Controller.OnDamaged += OnDamaged;
            m_Controller.OnDie += OnDie;
        }

        private void OnDestroy()
        {
            if (m_Controller == null) return;
            m_Controller.OnDetectedTarget -= OnDetectedTarget;
            m_Controller.OnLostTarget -= OnLostTarget;
            m_Controller.OnDamaged -= OnDamaged;
            m_Controller.OnDie -= OnDie;
        }

        protected virtual void Update()
        {
            UpdateAiStateTransitions();
            UpdateCurrentAiState();
        }
        /// <summary> 状态机切换 </summary>
        protected abstract void UpdateAiStateTransitions();

        /// <summary> 状态机(Update) </summary>
        protected abstract void UpdateCurrentAiState();


        /// <summary>受击时/summary>
        protected abstract void OnDamaged(Collider collider);

        // <summary>攻击时</summary>
        //protected abstract void OnAttack(int animName);

        /// <summary>发现目标 </summary>
        protected abstract void OnDetectedTarget();

        /// <summary> 丢失目标 </summary>
        protected abstract void OnLostTarget();

        /// <summary> 死亡 </summary>
        protected abstract void OnDie();
    }
}
