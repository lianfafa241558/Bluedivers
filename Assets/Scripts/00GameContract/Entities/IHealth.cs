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
        /// <summary>收到伤害时,值，来源，受击点,无源伤害</summary>
        public event UnityAction<PEInt, GameObject, Collider, bool> OnDamaged;
        /// <summary>被击中时 来源，受击点</summary>
        public event UnityAction<GameObject, Vector3, Vector3, bool> OnHit;

        /// <summary>收到治疗时 治疗值</summary>
        public event UnityAction<PEInt> OnHealed;
        /// <summary>恢复护盾时 恢复值</summary>
        public event UnityAction<PEInt> OnRestoreShield;

        /// <summary>死亡时</summary>
        public event UnityAction<GameObject> OnDie;
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

        public void TakeDamage(List<SKVP<DamageTypeEnum, PEInt>> damageGroups, bool noSource, GameObject damageSource, Collider damageAffected, Vector3 pos, bool response = true, bool isWeakness = false, int demolishValue = -1);


        /// <summary>取当前积蓄值不为 0 的异常状态（**只给逻辑值**；图标/颜色由表现层查 <c>AboStateData_SO</c> 自解）。</summary>
        public void GetActiveAboStates(List<AboStateGauge> results);
    }
}
