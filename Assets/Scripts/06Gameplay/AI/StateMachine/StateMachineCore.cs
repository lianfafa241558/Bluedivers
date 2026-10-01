using System;
using System.Collections.Generic;

namespace FPSGame.AI
{
    /// <summary>
    /// 单个状态的钩子表（AI 状态机两套框架共用的表项）。
    /// <para>onLateUpdate 只有 StateMachineFrame 体系会调度，AI 输入体系留空即可。</para>
    /// </summary>
    public struct AiStateHook
    {
        /// <summary>进入该状态</summary>
        public Action onEnter;
        /// <summary>每帧驱动</summary>
        public Action onUpdate;
        /// <summary>每帧的 LateUpdate 驱动（可选）</summary>
        public Action onLateUpdate;
        /// <summary>离开该状态</summary>
        public Action onExit;
    }

    /// <summary>
    /// 委托表状态机内核：只负责「状态表 + 切换 + 逐帧调度」，不依赖 MonoBehaviour 生命周期。
    /// <para>
    /// 由 AIInputBaseController&lt;T&gt; 与 StateMachineFrame&lt;&gt; 共用，消除两套框架里重复的
    /// 状态字典与 onExit/onEnter 派发逻辑；各框架的事件源、炮台列表等业务差异仍留在各自基类。
    /// </para>
    /// <para>
    /// 两套框架对"给 AiState 赋值的语义"不同，故内核同时提供两种入口：
    /// <see cref="Switch"/>（派发 onExit/onEnter）与 <see cref="SetCurrent"/>（纯赋值、不派发）。
    /// </para>
    /// </summary>
    public class StateMachineCore<TState> where TState : System.Enum
    {
        private readonly Dictionary<TState, AiStateHook> _hooks;

        /// <summary>切换到相同状态时是否直接忽略</summary>
        private readonly bool _skipSameState;

        /// <summary>首次切换标志：配合 <see cref="_skipSameState"/> 保证初始状态的 onEnter 一定执行</summary>
        private bool _firstSwitch = true;

        /// <summary>当前状态</summary>
        public TState Current { get; private set; }

        /// <param name="hooks">状态表（通常由子类 InitState 构建）</param>
        /// <param name="skipSameState">
        /// true（默认）= 切换到相同状态时忽略，偏"显式切换"语义（AI 输入体系）；
        /// false = 无条件重放旧状态 onExit + 新状态 onEnter，即原先直接给 AiState 赋值的行为（炮台/宠物体系）。
        /// </param>
        public StateMachineCore(Dictionary<TState, AiStateHook> hooks, bool skipSameState = true)
        {
            _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            _skipSameState = skipSameState;
        }

        /// <summary>派发式切换：先退旧状态(onExit)，再进新状态(onEnter)</summary>
        public void Switch(TState next)
        {
            if (_skipSameState && !_firstSwitch && EqualityComparer<TState>.Default.Equals(next, Current)) return;
            _firstSwitch = false;

            if (_hooks.TryGetValue(Current, out var old)) old.onExit?.Invoke();
            Current = next;
            if (_hooks.TryGetValue(next, out var cur)) cur.onEnter?.Invoke();
        }

        /// <summary>纯赋值：只改当前状态，不触发任何钩子</summary>
        public void SetCurrent(TState next)
        {
            Current = next;
        }

        /// <summary>驱动当前状态的 onUpdate</summary>
        public void Update()
        {
            if (_hooks.TryGetValue(Current, out var hook)) hook.onUpdate?.Invoke();
        }

        /// <summary>驱动当前状态的 onLateUpdate</summary>
        public void LateUpdate()
        {
            if (_hooks.TryGetValue(Current, out var hook)) hook.onLateUpdate?.Invoke();
        }
    }
}
