using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.Core.Interface;
using PEMaths;
using UnityEngine;
using UnityEngine.Events;

namespace FPSGame.GameContract
{
    public interface IHealth : IMonoVaild
    {
        /// <summary>收到伤害时：值、**直接来源**（显示/AI）、**起源**（统计）、受击点、无源伤害</summary>
        /// <remarks>⚠ 用 <see cref="Action{T1,T2,T3,T4,T5}"/> 而非 UnityAction：后者最多只支持 4 个泛型参数。</remarks>
        public event Action<PEInt, GameObject, GameObject, Collider, bool> OnDamaged;

        /// <summary>**每次真实命中都派发**（值为实际扣减量，**可能为 0**）：联机上报专用。
        /// <para>▍与 <see cref="OnDamaged"/> 的分工（2026-11-04）：那条只在真掉血时发，是表现/统计口径
        /// （受击闪变、喊话、命中次数）；这条多覆盖"0 伤害但有成分"——纯冰冻/眩晕/催化/破甲的命中
        /// 必须过网，否则成员端的积蓄槽永远不动。</para>
        /// <para>⚠ 只在 <c>Damageable.InflictDamage</c> 这条路径发（流血 / 异常状态 tick 不发）。</para></summary>
        public event Action<PEInt, GameObject, GameObject, Collider, bool> OnSettled;

        /// <summary>**本次伤害的成分数值**（类型 + 每个成分拆解后的量，含 <c>Destruction</c>），
        /// 只在 <c>TakeDamage</c> 派发事件的**同步回调期间**有效（挂的就是本次 <see cref="DamageSettleArgs.RawGroups"/>，
        /// 调用返回后自动还原）。
        /// <para>▍用途：联机上报 —— 接收端拿这份数值直接补"护甲破坏 / 异常状态积蓄"，不再按系数分摊。</para>
        /// <para>⚠ 只读：外部不许写；流血 / 异常状态 tick 这类直接调 <c>TakeDamage</c> 的路径为 null
        /// ⇒ 上报退回中性成分（不会顺手给积蓄槽补一次）。</para></summary>
        List<SKVP<DamageTypeEnum, PEInt>> LastRawGroups { get; }
        /// <summary>被击中时 第 1 参 = **直接来源**（显示口径），受击点</summary>
        public event UnityAction<GameObject, Vector3, Vector3, bool> OnHit;

        /// <summary>收到治疗时 治疗值</summary>
        public event UnityAction<PEInt> OnHealed;
        /// <summary>恢复护盾时 恢复值</summary>
        public event UnityAction<PEInt> OnRestoreShield;

        /// <summary>死亡时：**直接来源**（显示/AI）、**起源**（统计）</summary>
        public event UnityAction<GameObject, GameObject> OnDie;
        /// <summary>复活时</summary>
        public event UnityAction OnRevive;

        /// <summary>异常状态满槽事件（参数：异常类型、造成伤害者、是否变为满槽）</summary>
        public event Action<DamageTypeEnum, GameObject, bool> OnAboStateFullChanged;

        public float GetHpMax();
        public float GetHpCurrent();

        public float GetShieldMax();
        public float GetShieldCurrent();

        public float GetHpRatio();
        public float GetShieldRatio();
        public void Kill();

        public void Revive();
        public IDamageable GetMainPart();
        public void Heal(float healAmount);

        /// <summary>恢复护盾</summary>
        public void RestoreShield(float healAmount);

        /// <summary>结算伤害。参数语义见 <see cref="DamageSettleArgs"/>（Origin = 起源/统计，Caused = 直接来源/显示·AI）。</summary>
        /// <remarks>⚠ 用 <see cref="DamageSettleArgs.Of"/> 构造，别裸 <c>default</c>（Response 会变成 false）。</remarks>
        public void TakeDamage(in DamageSettleArgs args);


        /// <summary>取当前积蓄值不为 0 的异常状态（**只给逻辑值**；图标/颜色由表现层查 <c>AboStateData_SO</c> 自解）。</summary>
        public void GetActiveAboStates(List<AboStateGauge> results);

        /// <summary>指定异常状态的积蓄槽是否已满（= 目标身上确实挂着这个异常状态）。<c>Damageable</c> 判易伤用。</summary>
        public bool IsAboStateFull(DamageTypeEnum type);

        /// <summary>直接把指定异常状态的积蓄槽灌满（不计伤害、不走抗性/无敌判定），用于"由别的单位继承而来的异常状态"。</summary>
        /// <param name="source">记作造成者：满槽特效挂载点、骇入倒戈的队伍来源都取它</param>
        public void FillAboGauge(DamageTypeEnum type, GameObject source);
    }
}
