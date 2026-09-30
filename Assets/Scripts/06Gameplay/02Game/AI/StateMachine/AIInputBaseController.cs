using System;
using System.Collections;
using System.Collections.Generic;
using FPSGame.Game;
using UnityEngine;
namespace FPSGame.AI
{
    /// <summary>
    /// 泛型 AI 状态机基类：T 为子类自定义的 AIState 枚举。
    /// <para>
    /// 状态表与切换/驱动逻辑由 <see cref="StateMachineCore{TState}"/> 承担（与 StateMachineFrame 共用同一内核），
    /// 子类通过 InitState() 注册状态行为，在 UpdateAiStateTransitions 中调用 InvokeCurrentState() 查表驱动。
    /// </para>
    /// </summary>
    public abstract class AIInputBaseController<T> : MonoBehaviour where T : System.Enum
    {
        protected I_AIController m_Controller;
        /// <summary>发现目标的时候</summary>
        protected float m_TimeStartedDetection { get; set; }
        /// <summary>丢失目标的时候</summary>
        protected float m_TimeLostDetection;

        #region 状态机

        /// <summary>状态机内核：状态表 + 切换 + 逐帧调度</summary>
        private StateMachineCore<T> _machine;

        /// <summary>
        /// 当前状态。直接赋值**不**触发 onExit/onEnter（与改造前一致，因为首次 InitState 里的初始状态由 InitState 自行处理）；
        /// 需要派发钩子请用 <see cref="SwitchState"/>。
        /// </summary>
        public T AiState
        {
            get => _machine != null ? _machine.Current : default;
            set { if (_machine != null) _machine.SetCurrent(value); }
        }

        /// <summary>子类构建状态表（在基类 Start 中调用，需在首次 SwitchState 前完成）</summary>
        protected abstract Dictionary<T, AiStateHook> InitState();

        /// <summary>状态切换：先退旧状态(onExit)，再进新状态(onEnter)；相同状态会被忽略（首次除外）</summary>
        protected void SwitchState(T state) => _machine.Switch(state);

        /// <summary>查表驱动当前状态的 onUpdate（子类在守卫逻辑后调用）</summary>
        protected void InvokeCurrentState() => _machine.Update();

        #endregion

        protected virtual void Start()
        {
            m_TimeStartedDetection = Mathf.NegativeInfinity;
            _machine = new StateMachineCore<T>(InitState());
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


        /// <summary>受击时</summary>
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
