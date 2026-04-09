using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using PEMaths;
using Unity.BaseTool;
using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace Unity.FPS.Game {
    public abstract class Health : MonoBehaviour {
        /// <summary>最大生命值</summary>
        [CustomLabel("最大生命值")]
        public int MaxHealth = 100;
        /// <summary>最大生命值</summary>
        [CustomLabel("最大护盾值")] 
        public int MaxShield;

        /// <summary>主体部位</summary>
        [CustomLabel("主体部位")]
        public Damageable MainPart;


        /// <summary>收到伤害时 值，来源，受击点,无源伤害</summary>
        public UnityAction<PEInt, GameObject, Collider,bool> OnDamaged;

        /// <summary>被击中时 来源，受击点</summary>
        public UnityAction<GameObject,Vector3> OnHit;

        /// <summary>是否是破盾伤害</summary>
        public UnityAction<bool> OnShieldDamaged;

        /// <summary>收到治疗时</summary>
        public UnityAction<PEInt> OnHealed;
        /// <summary>死亡时</summary>
        public UnityAction<GameObject> OnDie;

        /// <summary>复活时</summary>
        public UnityAction OnRevive;

        [Space]
        [DisplayField]
        [CustomLabel("剩余生命值")]
        [SerializeField]
        public int showHealth;

        //剩余血量
        public PEInt CurrentHealth { get; set; }
        //剩余护盾
        public PEInt CurrentShield { get; set; }

        //是否无敌
        public bool Invincible { get; set; }

        public bool CanPickup() => CurrentHealth < MaxHealth;

        public float GetHpRatio() => CurrentHealth.RawFloat / MaxHealth;

        public float GetShieldRatio() => CurrentShield.RawFloat / MaxShield;

        [DisplayField]
        [SerializeField]
        protected bool m_IsDead;

        protected float m_LastHitTime = Mathf.NegativeInfinity;

        protected virtual void Start() {
            CurrentHealth = MaxHealth;
            if(MainPart.IsValid()) MainPart.isMain = true;
        }
        /// <summary>受到治疗</summary>
        public void Heal(float healAmount) {
            PEInt healthBefore = CurrentHealth;
            CurrentHealth += (PEInt)healAmount;
            CurrentHealth = PEMath.Clamp(CurrentHealth, 0, MaxHealth);
            showHealth = CurrentHealth.RawInt;
            // call OnHeal action
            PEInt trueHealAmount = CurrentHealth - healthBefore;
            if (trueHealAmount > 0) {
                OnHealed?.Invoke(trueHealAmount);
            }
        }

        /// <summary>受到伤害</summary>
        public void TakeDamage(List<KVP<DamageTypeEnum, PEInt>> damageGroups, bool noSource, GameObject damageSource,Collider damageAffected,Vector3 pos) {
            if (Invincible)
                return;
            bool haveshield= CurrentShield > 0, isBreakShield=false;
            PEInt finaldamgage = 0;
            damageGroups.ForEach(item => finaldamgage += HandleDamage(item.Key,PEMath.Max(item.Value,0)));
            //Debug.LogWarning("最终伤害" + finaldamgage + "血量" + CurrentHealth);
#if UNITY_EDITOR
            if(damageAffected) Tool.DrawLabel(damageAffected.RandomPoint(out var normal),""+Tool.Round(finaldamgage.RawFloat), 3, Color.red);
            else Tool.DrawLabel(transform.position+RandomUtils.RandomVector3(), "" + Tool.Round(finaldamgage.RawFloat), 3, Color.red);
#endif

            PEInt healthBefore = CurrentHealth, shieldBefore = CurrentShield;
            if (shieldBefore > 0) {
                CurrentShield -= finaldamgage;

                if (CurrentShield < 0) {
                    finaldamgage = -CurrentShield;
                    CurrentShield = 0;
                    isBreakShield = true;
                }
                else {
                    finaldamgage = 0;
                }
            }
            if (finaldamgage > 0) {
                CurrentHealth -= finaldamgage;
                CurrentHealth = PEMath.Clamp(CurrentHealth, 0, MaxHealth);
            }


            // call OnDamage action
            PEInt trueDamageAmount = healthBefore - CurrentHealth + shieldBefore - CurrentShield;
            if (trueDamageAmount > 0) {
                m_LastHitTime = Time.time;
                OnDamaged?.Invoke(trueDamageAmount, damageSource,damageAffected, noSource);
                OnHit?.Invoke(damageSource,pos);
                if (haveshield) OnShieldDamaged?.Invoke(isBreakShield);
                GlobalEventManager.UnitHit(gameObject, damageSource);
            }
            showHealth = CurrentHealth.RawInt;
            HandleDeath(damageSource);
        }

        private PEInt HandleDamage(DamageTypeEnum type, PEInt value) {
            //只有需求特殊处理的才单独写，目前没有状态槽，直接全部默认
            //Debug.LogWarning("对"+gameObject.name+"伤害:类型"+type+"值"+value);
            switch (type) {
                case DamageTypeEnum.Destruction:
                    return 0;//护甲破坏不计入伤害量
                case DamageTypeEnum.Terrain:
                    return 0;//地形破坏不计入伤害量
                case DamageTypeEnum.Weakness:
                    return 0;//弱点加成不计入伤害量
                default:
                    return value;
            }
        }


        /// <summary>复活</summary>
        public virtual void Revive()
        {
            CurrentHealth = MaxHealth;
            CurrentShield = MaxShield;
            OnRevive?.Invoke();
            m_IsDead = false;
        }

        /// <summary>代码杀</summary>
        public void Kill() {
            CurrentHealth = 0;

            // call OnDamage action
            //OnDamaged?.Invoke(MaxHealth, null,null,true);

            HandleDeath(null);
        }

        /// <summary>死亡处理</summary>
        protected virtual void HandleDeath(GameObject source) {
            if (m_IsDead)
                return;

            // call OnDie action
            if (CurrentHealth <= 0) {
                Debug.Log(gameObject+"单位死亡",gameObject);
                m_IsDead = true;
                OnDie?.Invoke(source);
            }
        }

    }

}