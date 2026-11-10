using FPSGame.Data;
using FPSGame.GameContract;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 全队强化 · 效果组件基类：Inspector 里选"吃哪个强化 + 什么时候生效"，时机与幂等由基类兜住。
    ///
    /// <para>▍为什么需要它：强化表是 <c>BattleManager</c> 在**战斗场景加载完之后**写的（<c>AsyncLoadScene</c> 回调里才
    /// <c>BattleManager.Creat</c>），而场景里所有 <c>Awake/OnEnable</c> 早就跑完了 ⇒ 组件自己在 <c>OnEnable</c> 里读
    /// <c>BattleState.HaveBooster</c> **恒 false**。基类把"已就绪就立刻跑 / 没就绪就等事件"这一层收掉。</para>
    ///
    /// <para>▍三档时机：<see cref="Trigger.Enhance"/>（表一写好就生效，只适合改数值）、
    /// <see cref="Trigger.BattleStart"/>（开打时生效，**要在世界里生成单位必须用这档**：那时 ActorsManager / 生成桥才在）、
    /// <see cref="Trigger.Manual"/>（由宿主在合适时机调 <see cref="EvaluateNow"/>，例如空投舱落地）。</para>
    /// </summary>
    public abstract class BoosterEffectBehaviour : MonoBehaviour
    {
        /// <summary>生效时机。</summary>
        public enum Trigger
        {
            /// <summary>强化表一写好就生效（此时地形 / ActorsManager 还没建，**不要在这里生成任何东西**）。</summary>
            [InspectorName("强化表就绪")] Enhance,
            /// <summary>开打时生效（世界已就绪）。</summary>
            [InspectorName("开打时")] BattleStart,
            /// <summary>手动：等外部调 <see cref="EvaluateNow"/>（例：Inspector 把 <c>AirdropPod.OnLanded</c> 绑到它）。</summary>
            [InspectorName("手动·外部调用")] Manual,
        }

        [SerializeField, InspectorName("触发时机")] protected Trigger _trigger = Trigger.BattleStart;
        [SerializeField, InspectorName("生效的全队强化")] protected BoosterType _booster;

        private bool _bound;
        private bool _applied;

        /// <summary>Inspector 里选的时机（子类按它决定要不要自己找触发源）。</summary>
        protected Trigger CurrentTrigger => _trigger;

        /// <summary>当前是否处于"已生效"状态（子类判它来决定要不要刷新数值）。</summary>
        protected bool IsApplied => _applied;

        private void OnEnable() => Bind();

        private void OnDisable()
        {
            Unbind();
            // 池化复位用的对象池会再来一次 OnEnable ⇒ 失效时必须撤掉上一次的产物，否则会叠第二份
            if (!_applied) return;
            _applied = false;
            OnRevert();
        }

        /// <summary>登记完（子类在这里挂自己的触发源 / 注册生成器之类）。</summary>
        protected virtual void OnBind() { }

        /// <summary>注销（与 <see cref="OnBind"/> 成对）。</summary>
        protected virtual void OnUnbind() { }

        /// <summary>条件成立：做效果（**必须幂等**，可能被重复调用）。</summary>
        protected abstract void OnApply();

        /// <summary>条件失效 / 组件关闭：撤掉效果（默认什么都不做）。</summary>
        protected virtual void OnRevert() { }

        /// <summary>手动触发一次判定（<see cref="Trigger.Manual"/> 用；其它档调它也只是再判一次）。</summary>
        public void EvaluateNow()
        {
            if (_bound) Evaluate();
        }

        private void Bind()
        {
            if (_bound) return;
            _bound = true;

            switch (_trigger)
            {
                case Trigger.Enhance:
                    // 已经写过就立刻跑一次（池化对象晚于开局时才被启用的情况）
                    if (BattleState.TeamEnhanceApplied) Evaluate();
                    else BattleState.OnTeamEnhanceApplied += Evaluate;
                    break;

                case Trigger.BattleStart:
                    if (BattleState.IsStartBattle) Evaluate();
                    else BattleState.OnBattleStart += Evaluate;
                    break;

                // Manual：不订阅任何事件，等宿主给信号（见 EvaluateNow）
            }

            OnBind();
        }

        private void Unbind()
        {
            if (!_bound) return;
            _bound = false;

            // 两档都退：只挂在其中一个也只是空转一次，比记住"当初挂了哪个"更省事
            BattleState.OnTeamEnhanceApplied -= Evaluate;
            BattleState.OnBattleStart -= Evaluate;

            OnUnbind();
        }

        private void Evaluate()
        {
            bool on = Match();
            if (on == _applied) return;
            _applied = on;

            if (on) OnApply();
            else OnRevert();
        }

        private bool Match()
        {
            if (_booster == BoosterType.Empty)
            {
                // 策划可改的数据 ⇒ 必须留日志，否则"效果莫名不生效"无从下手
                Debug.LogWarning($"[全队强化]「{name}」没配「生效的全队强化」⇒ 效果不生效", this);
                return false;
            }

            return BattleState.HaveBooster(_booster);
        }
    }
}
