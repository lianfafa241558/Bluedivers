using System;
using System.Collections.Generic;
using FPSGame.Core.Interface;
using FPSGame.GameContract;

using FPSGame.Game;
using UnityEngine;

namespace FPSGame.AI
{
    /// <summary>
    /// 炮台/宠物类单位的通用状态机基类。
    /// <para>
    /// 原先自带一套委托表（与 AIInputBaseController 重复），现改为复用
    /// <see cref="StateMachineCore{TState}"/>；本类保留自己的业务差异：
    /// 用 DetectionModule 作事件源、带 turrets 炮台列表、有 Init/Uninit 生命周期。
    /// </para>
    /// </summary>
    internal abstract class StateMachineFrame<StateEnum> : MonoBehaviour
        where StateEnum : System.Enum
    {

        [SerializeField]
        protected DetectionModule DetectionModule;

        /// <summary>发现目标的时间</summary>
        protected float m_TimeStartedDetection { get; set; }
        /// <summary>丢失目标的时间</summary>
        protected float m_TimeLostDetection { get; set; }

        /// <summary>目标中心点：只用于瞄准/朝向，**别拿它算距离**（见 <see cref="DetectionTargetSurfaceDis"/>）</summary>
        protected Vector3 DetectionTargetPos => DetectionModule.Target.Pos;

        /// <summary>
        /// 我方到目标<b>表面</b>的距离（中心距 − 目标半径）。
        /// <para>⚠ 距离门一律用这个：射程小于目标半径时按中心点算会**永远**判"够不着"
        /// （GuardDog 停不下来一直往前挤、炮台贴身也永不开火）。</para>
        /// </summary>
        /// <param name="horizontalOnly">地面单位判停/追击传 true（忽略高度差，与 EnemyMobile 的通用约定一致）</param>
        protected float DetectionTargetSurfaceDis(Vector3 from, bool horizontalOnly = false)
        {
            Vector3 delta = DetectionTargetPos - from;
            if (horizontalOnly) delta.y = 0f;
            var actor = DetectionModule.Target.Actor;
            return delta.magnitude - (actor.IsValidMono() ? actor.HalfRange : 0f);
        }

     
        [SerializeField]
        protected List<Turret> turrets = new();

        /// <summary>序列化字段：供编辑器查看/存档，运行时以状态机内核为准</summary>
        [SerializeField]
        private StateEnum aiState;

        private StateMachineCore<StateEnum> _machine;

        /// <summary>当前状态（赋值会派发旧状态 onExit + 新状态 onEnter，与改造前一致）</summary>
        protected StateEnum AiState
        {
            get => _machine != null ? _machine.Current : aiState;
            set
            {
                aiState = value;
                _machine?.Switch(value);
            }
        }

        protected IActor m_actor;

        protected abstract Dictionary<StateEnum, AiStateHook> InitState();

        protected abstract void Init();
        protected abstract void Uninit();
        protected abstract void OnDetectedTarget();
        protected abstract void OnLostTarget();



        void Start()
        {
            m_actor = GetComponent<IActor>();
            Init();
            // 归属(Actor)不再注入：DetectionModule.Start 会从父级自取
            //skipSameState=false：与改造前一致，AiState=default 会重放 default 状态的 onExit/onEnter
            _machine = new StateMachineCore<StateEnum>(InitState(), skipSameState: false);
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
            _machine.Update();
        }
        void LateUpdate()
        {
            _machine.LateUpdate();
        }




    }

}
