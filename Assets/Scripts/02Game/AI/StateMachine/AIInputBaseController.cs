using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
namespace Unity.FPS.AI
{
    /// <summary>
    /// 这个是真的设置状态机控制
    /// </summary>
    public abstract class AIInputBaseController : MonoBehaviour
    {

        protected I_AIController m_Controller;
        /// <summary>发现目标的时候/summary>
        protected float m_TimeStartedDetection { get; set; }//;
        /// <summary>丢失目标的时候/summary>
        protected float m_TimeLostDetection;


        protected virtual void Start()
        {

            m_TimeStartedDetection = Mathf.NegativeInfinity;
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