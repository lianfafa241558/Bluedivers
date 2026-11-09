using FPSGame.Core;
using UnityEngine;

namespace FPSGame.Gameplay
{
    /// <summary>
    /// 可重复引爆的地雷：前 <see cref="ExplodeTimes"/>-1 次只结算范围伤害、**自身留在场上**，
    /// 最后一次（或中途被摧毁 / 寿命到点自爆）才像 <see cref="DeployableMine"/> 一样引爆并回池。
    /// 每次爆炸后要等 <see cref="RearmInterval"/> 秒重新武装，期间不再检测触发（敌人还站着也不会连爆）。
    ///
    /// <para>▍为什么不用"给单位挂 <c>ActorFlag.Invincible</c> 撑住"：
    /// <list type="number">
    /// <item><c>Health.Kill()</c>（本类触发链路的自杀）**不看**无敌标记，只有 <c>TakeDamage</c> 看
    /// ⇒ 拦不住自己触发导致的死亡。</item>
    /// <item>带无敌的单位会被 <c>FpsHelper.VaildTarget</c> 判成**不可选目标**
    /// ⇒ 智能地雷的队伍过滤与其它索敌都会跳过它，<c>TryHit</c> 再也判不到敌人。</item>
    /// <item>无敌还会让敌人打不掉它，"被摧毁提前结算"这条反馈一起消失。</item>
    /// </list>
    /// 所以改为只控制"这次爆完留不留场"（<see cref="DeployableMine.ConsumeOnExplosion"/>），
    /// Health 的血量/死亡语义完全不动。</para>
    /// </summary>
    [AddComponentMenu("单位/可重复引爆地雷", 31)]
    public class RepeatableMine : DeployableMine
    {
        [Header("重复引爆")]
        [InspectorName("引爆次数(含最后自毁那次)")]
        [Tooltip("1 = 与普通地雷完全相同；3 = 前 2 次只掉血不消失，第 3 次引爆后回池")]
        [SerializeField]
        private int ExplodeTimes = 3;
        [InspectorName("两次引爆的间隔(秒)")]
        [Tooltip("爆炸后需等这么久才重新武装；期间既不检测触发也不再结算伤害")]
        [SerializeField]
        private float RearmInterval = 3f;

        /// <summary>剩余引爆次数（含最后一次；被摧毁 / 超时会被压到 1）</summary>
        private int _remain;

        /// <summary>池化复用 / 重新部署：次数复位（OnEnable 与 SetOrigin 各来一次，幂等）</summary>
        protected override void OnMineReset() => _remain = Mathf.Max(1, ExplodeTimes);

        /// <summary>非最后一次不自杀 —— Health 一死就不再派发 OnDie，后续爆炸会失去"被击毁提前结算"</summary>
        protected override void TriggerByHit()
        {
            if (_remain > 1)
            {
                TriggerExplosion();
                return;
            }
            base.TriggerByHit();
        }

        /// <summary>次数扣减：扣到 0 的那一次才回池</summary>
        protected override bool ConsumeOnExplosion() => --_remain <= 0;

        /// <summary>留在场上：按间隔重新武装（间隔内不检测触发）</summary>
        protected override void RearmAfterExplosion() => Rearm(RearmInterval);

        /// <summary>被摧毁 / 超时：剩余次数作废，本次直接按最后一次结算并回池</summary>
        protected override void PrepareFinalExplosion() => _remain = 1;

        /// <summary>寿命到点（没人踩过 / 正在重装）→ 自爆，而不是像普通地雷那样静默消失</summary>
        protected override void OnLifeEndNotTriggered() => ExplodeNow();
    }
}
