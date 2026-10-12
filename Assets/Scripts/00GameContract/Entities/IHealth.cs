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

        /// <summary>**本次伤害的原始成分**（类型 + 系数），结算期间有效（实现见 <c>Health.LastRawGroups</c>）。
        /// <para>▍用途：联机上报 —— 接收端要拿它补"护甲破坏 / 异常状态积蓄"（只带总伤害时那两条分支会被整段跳过）。</para>
        /// <para>⚠ 直接调 <c>TakeDamage</c> 的路径（流血 / 异常状态 tick）为 null ⇒ 上报退回中性成分。</para></summary>
        List<SKVP<DamageTypeEnum, float>> LastRawGroups { get; set; }
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

        /// <summary>结算伤害。<paramref name="damageOrigin"/> = 起源（统计），<paramref name="damageCaused"/> = 直接来源（显示/AI，null 回落 origin）。</summary>
        public void TakeDamage(List<SKVP<DamageTypeEnum, PEInt>> damageGroups, bool noSource, GameObject damageOrigin, Collider damageAffected, Vector3 pos, bool response = true, bool isWeakness = false, int demolishValue = -1, GameObject damageCaused = null);


        /// <summary>取当前积蓄值不为 0 的异常状态（**只给逻辑值**；图标/颜色由表现层查 <c>AboStateData_SO</c> 自解）。</summary>
        public void GetActiveAboStates(List<AboStateGauge> results);
    }
}
